using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GDHelpers.SourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public sealed class OnReadyGenerator : IIncrementalGenerator
    {
        private const string OnReadyAttr = "GDHelpers.OnReadyAttribute";
        private const string AutoloadAttr = "GDHelpers.AutoloadAttribute";
        private const string PreloadAttr = "GDHelpers.PreloadAttribute";

        private static readonly SymbolDisplayFormat TypeFormat = new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
        );

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var members = context
                .SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, _) => IsCandidate(node),
                    transform: static (ctx, token) => GetTarget(ctx, token)
                )
                .Where(static m => m is not null)
                .Select(static (m, _) => m!.Value);

            var allMembers = members.Collect();

            context.RegisterSourceOutput(
                context.CompilationProvider.Combine(allMembers),
                static (spc, source) => Execute(spc, source.Left, source.Right)
            );
        }

        private static bool IsCandidate(SyntaxNode node)
        {
            if (
                node is FieldDeclarationSyntax fds
                && fds.AttributeLists.Count > 0
                && fds.Declaration.Variables.Count == 1
            )
                return true;

            if (node is PropertyDeclarationSyntax pds && pds.AttributeLists.Count > 0)
                return true;

            return false;
        }

        private static MemberTarget? GetTarget(
            GeneratorSyntaxContext context,
            CancellationToken token
        )
        {
            var semanticModel = context.SemanticModel;
            ISymbol? memberSymbol = null;

            if (context.Node is FieldDeclarationSyntax fds)
            {
                var variable = fds.Declaration.Variables[0];
                memberSymbol = semanticModel.GetDeclaredSymbol(variable, token);
            }
            else if (context.Node is PropertyDeclarationSyntax pds)
            {
                memberSymbol = semanticModel.GetDeclaredSymbol(pds, token);
            }

            if (memberSymbol == null)
                return null;

            if (memberSymbol.IsStatic)
                return null;

            if (memberSymbol is IFieldSymbol { IsReadOnly: true })
                return null;

            if (memberSymbol is IPropertySymbol { SetMethod: null })
                return null;

            string? attributeName = null;
            string? path = null;

            foreach (var attrData in memberSymbol.GetAttributes())
            {
                var attrClass = attrData.AttributeClass;
                if (attrClass == null)
                    continue;

                var fullName = attrClass.ToDisplayString();
                if (fullName is OnReadyAttr or AutoloadAttr or PreloadAttr)
                {
                    attributeName = fullName;
                    if (attrData.ConstructorArguments.Length > 0)
                    {
                        var arg = attrData.ConstructorArguments[0];
                        if (arg.Value is string s)
                            path = s;
                    }
                    break;
                }
            }

            if (attributeName == null)
                return null;

            var containingType = memberSymbol.ContainingType;
            if (containingType == null || containingType.TypeKind != TypeKind.Class)
                return null;

            var isPartial = containingType.DeclaringSyntaxReferences.Any(r =>
                r.GetSyntax(token) is ClassDeclarationSyntax cls
                && cls.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
            );

            if (!isPartial)
                return null;

            var hasReady = containingType
                .GetMembers("_Ready")
                .Any(m => m is IMethodSymbol { IsOverride: true });

            var nsSymbol = containingType.ContainingNamespace;
            var ns = nsSymbol is { IsGlobalNamespace: false } ? nsSymbol.ToDisplayString() : "";
            var fullTypeName = containingType.ToDisplayString(TypeFormat);

            string memberType;
            if (memberSymbol is IFieldSymbol fieldSym)
                memberType = fieldSym.Type.ToDisplayString(TypeFormat);
            else if (memberSymbol is IPropertySymbol propSym)
                memberType = propSym.Type.ToDisplayString(TypeFormat);
            else
                return null;

            var className = fullTypeName;
            if (!string.IsNullOrEmpty(ns) && fullTypeName.StartsWith(ns + "."))
                className = fullTypeName.Substring(ns.Length + 1);

            return new MemberTarget(
                ns,
                className,
                memberSymbol.Name,
                memberType,
                attributeName,
                path,
                memberSymbol is IFieldSymbol,
                hasReady
            );
        }

        private static void Execute(
            SourceProductionContext context,
            Compilation compilation,
            ImmutableArray<MemberTarget> members
        )
        {
            if (members.IsEmpty)
                return;

            var groups = members.GroupBy(m => (m.Namespace, m.ClassName));

            foreach (var group in groups)
            {
                var list = group.ToList();
                if (list.Count == 0)
                    continue;

                var hasReady = list[0].HasReadyOverride;
                var source = GenerateSource(list, hasReady);
                var nsPrefix = string.IsNullOrEmpty(group.Key.Namespace)
                    ? ""
                    : group.Key.Namespace + ".";
                var hintName = SanitizeHintName($"{nsPrefix}{group.Key.ClassName}.OnReady.g.cs");
                context.AddSource(hintName, source);
            }
        }

        private static string SanitizeHintName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c) || c is '.' or '-' or '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }

        private static string GenerateSource(List<MemberTarget> members, bool hasReadyOverride)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine(
                "#pragma warning disable CS0108, CS0114, CS0162, CS0169, CS0219, CS0628, CS0649, CS0660, CS0661, CS1591, CS8981"
            );
            sb.AppendLine("#nullable enable");
            sb.AppendLine("#pragma warning disable IDE0005");
            sb.AppendLine("using Godot;");
            sb.AppendLine();

            var first = members[0];
            var ns = first.Namespace;
            var classParts = first.ClassName.Split('.');

            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            string indent = string.IsNullOrEmpty(ns) ? "" : "    ";
            for (int i = 0; i < classParts.Length; i++)
            {
                sb.AppendLine($"{indent}partial class {classParts[i]}");
                sb.AppendLine($"{indent}{{");
                indent += "    ";
            }

            if (hasReadyOverride)
            {
                AppendWireNodes(sb, indent, members);
            }
            else
            {
                sb.AppendLine($"{indent}public override void _Ready()");
                sb.AppendLine($"{indent}{{");
                AppendAssignments(sb, indent + "    ", members);
                sb.AppendLine($"{indent}    OnReady();");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($"{indent}partial void OnReady();");
                sb.AppendLine();
                AppendWireNodes(sb, indent, members);
            }

            sb.AppendLine();
            AppendResolveHelper(sb, indent);

            for (int i = 0; i < classParts.Length; i++)
            {
                indent = indent.Substring(0, indent.Length - 4);
                sb.AppendLine($"{indent}}}");
            }

            if (!string.IsNullOrEmpty(ns))
                sb.AppendLine("}");

            return sb.ToString();
        }

        private static void AppendWireNodes(
            StringBuilder sb,
            string indent,
            List<MemberTarget> members
        )
        {
            sb.AppendLine($"{indent}private void WireNodes()");
            sb.AppendLine($"{indent}{{");
            AppendAssignments(sb, indent + "    ", members);
            sb.AppendLine($"{indent}}}");
        }

        private static void AppendAssignments(
            StringBuilder sb,
            string indent,
            List<MemberTarget> members
        )
        {
            foreach (var m in members)
            {
                string rhs;
                switch (m.AttributeName)
                {
                    case OnReadyAttr:
                        var path = m.Path ?? m.MemberName;
                        rhs = $"ResolveOnReadyNode<{m.MemberType}>(this, \"{EscapeString(path)}\")";
                        break;
                    case AutoloadAttr:
                        var autoloadPath = "/root/" + (m.Path ?? m.MemberName);
                        rhs = $"GetNode<{m.MemberType}>(\"{EscapeString(autoloadPath)}\")";
                        break;
                    case PreloadAttr:
                        var preloadPath = m.Path ?? "";
                        rhs = $"GD.Load<{m.MemberType}>(\"{EscapeString(preloadPath)}\")";
                        break;
                    default:
                        continue;
                }
                sb.AppendLine($"{indent}{m.MemberName} = {rhs};");
            }
        }

        private static void AppendResolveHelper(StringBuilder sb, string indent)
        {
            sb.AppendLine(
                $"{indent}private static T? ResolveOnReadyNode<T>(Node node, string path) where T : class"
            );
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    var result = node.GetNodeOrNull<T>(path);");
            sb.AppendLine($"{indent}    if (result != null) return result;");
            sb.AppendLine();
            sb.AppendLine($"{indent}    result = node.GetNodeOrNull<T>($\"%{{path}}\");");
            sb.AppendLine($"{indent}    if (result != null) return result;");
            sb.AppendLine();
            sb.AppendLine($"{indent}    if (path.Length > 0)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        var first = path[0];");
            sb.AppendLine(
                $"{indent}        var toggled = char.IsUpper(first) ? char.ToLowerInvariant(first) + path.Substring(1) : char.ToUpperInvariant(first) + path.Substring(1);"
            );
            sb.AppendLine($"{indent}        result = node.GetNodeOrNull<T>(toggled);");
            sb.AppendLine($"{indent}        if (result != null) return result;");
            sb.AppendLine($"{indent}        result = node.GetNodeOrNull<T>($\"%{{toggled}}\");");
            sb.AppendLine($"{indent}        if (result != null) return result;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine(
                $"{indent}    GD.PrintErr($\"Failed to wire OnReady node: {{path}} on {{node.Name}}\");"
            );
            sb.AppendLine($"{indent}    return null;");
            sb.AppendLine($"{indent}}}");
        }

        private static string EscapeString(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    internal readonly struct MemberTarget
    {
        public readonly string Namespace;
        public readonly string ClassName;
        public readonly string MemberName;
        public readonly string MemberType;
        public readonly string AttributeName;
        public readonly string? Path;
        public readonly bool IsField;
        public readonly bool HasReadyOverride;

        public MemberTarget(
            string @namespace,
            string className,
            string memberName,
            string memberType,
            string attributeName,
            string? path,
            bool isField,
            bool hasReadyOverride
        )
        {
            Namespace = @namespace;
            ClassName = className;
            MemberName = memberName;
            MemberType = memberType;
            AttributeName = attributeName;
            Path = path;
            IsField = isField;
            HasReadyOverride = hasReadyOverride;
        }
    }
}
