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
        private const string OnSignalAttr = "GDHelpers.OnSignalAttribute";

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

            var signalTargets = context
                .SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, _) => IsSignalCandidate(node),
                    transform: static (ctx, token) => GetSignalTarget(ctx, token)
                )
                .Where(static s => s is not null)
                .Select(static (s, _) => s!.Value);

            var allSignals = signalTargets.Collect();

            context.RegisterSourceOutput(
                context.CompilationProvider.Combine(allMembers).Combine(allSignals),
                static (spc, source) =>
                    Execute(spc, source.Left.Left, source.Left.Right, source.Right)
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
                .GetMembers("_Notification")
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

        private static bool IsSignalCandidate(SyntaxNode node)
        {
            return node is MethodDeclarationSyntax { AttributeLists.Count: > 0 };
        }

        private static SignalTarget? GetSignalTarget(
            GeneratorSyntaxContext context,
            CancellationToken token
        )
        {
            if (context.Node is not MethodDeclarationSyntax mds)
                return null;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(mds, token);
            if (
                methodSymbol == null
                || methodSymbol.IsStatic
                || methodSymbol.MethodKind != MethodKind.Ordinary
            )
                return null;

            string? signalName = null;
            string? nodePath = null;
            uint flags = 0;
            bool found = false;

            foreach (var attr in methodSymbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass == null)
                    continue;
                if (attrClass.ToDisplayString() != OnSignalAttr)
                    continue;

                var args = attr.ConstructorArguments;
                if (args.Length >= 1)
                {
                    if (args[0].Value is string s)
                        signalName = s;
                    if (args.Length >= 2 && args[1].Value is string p)
                        nodePath = p;
                    if (args.Length >= 3 && args[2].Value is uint f)
                        flags = f;
                    found = true;
                }
                break;
            }

            if (!found)
                return null;

            var containingType = methodSymbol.ContainingType;
            if (containingType == null || containingType.TypeKind != TypeKind.Class)
                return null;

            var isPartial = containingType.DeclaringSyntaxReferences.Any(r =>
                r.GetSyntax(token) is ClassDeclarationSyntax cls
                && cls.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
            );

            if (!isPartial)
                return null;

            var hasReady = containingType
                .GetMembers("_Notification")
                .Any(m => m is IMethodSymbol { IsOverride: true });

            var nsSymbol = containingType.ContainingNamespace;
            var ns = nsSymbol is { IsGlobalNamespace: false } ? nsSymbol.ToDisplayString() : "";
            var fullTypeName = containingType.ToDisplayString(TypeFormat);

            var className = fullTypeName;
            if (!string.IsNullOrEmpty(ns) && fullTypeName.StartsWith(ns + "."))
                className = fullTypeName.Substring(ns.Length + 1);

            return new SignalTarget(
                ns,
                className,
                methodSymbol.Name,
                signalName ?? "",
                nodePath ?? "",
                flags,
                hasReady
            );
        }

        private static void Execute(
            SourceProductionContext context,
            Compilation compilation,
            ImmutableArray<MemberTarget> members,
            ImmutableArray<SignalTarget> signals
        )
        {
            if (members.IsEmpty && signals.IsEmpty)
                return;

            var memberGroups = members.GroupBy(m => (m.Namespace, m.ClassName));
            var signalGroups = signals.GroupBy(s => (s.Namespace, s.ClassName));
            var allKeys = memberGroups.Select(g => g.Key).Union(signalGroups.Select(g => g.Key));

            foreach (var key in allKeys)
            {
                var memberList =
                    memberGroups.FirstOrDefault(g => g.Key == key)?.ToList()
                    ?? new List<MemberTarget>();

                var signalList =
                    signalGroups.FirstOrDefault(g => g.Key == key)?.ToList()
                    ?? new List<SignalTarget>();

                bool hasReady =
                    memberList.Count > 0
                        ? memberList[0].HasReadyOverride
                        : signalList[0].HasReadyOverride;

                var source = GenerateSource(memberList, signalList, hasReady);
                var nsPrefix = string.IsNullOrEmpty(key.Namespace) ? "" : key.Namespace + ".";
                var hintName = SanitizeHintName($"{nsPrefix}{key.ClassName}.OnReady.g.cs");
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

        private static string GenerateSource(
            List<MemberTarget> members,
            List<SignalTarget> signals,
            bool hasReadyOverride
        )
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

            string ns;
            string fullClassName;
            if (members.Count > 0)
            {
                ns = members[0].Namespace;
                fullClassName = members[0].ClassName;
            }
            else
            {
                ns = signals[0].Namespace;
                fullClassName = signals[0].ClassName;
            }

            var classParts = fullClassName.Split('.');

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
                if (members.Count > 0 || signals.Count > 0)
                    AppendWireNodes(sb, indent, members, signals);
            }
            else
            {
                sb.AppendLine($"{indent}public override void _Notification(int what)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    if (what == NotificationEnterTree)");
                sb.AppendLine($"{indent}    {{");
                if (members.Count > 0 || signals.Count > 0)
                    sb.AppendLine($"{indent}        WireNodes();");
                sb.AppendLine($"{indent}        OnReady();");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($"{indent}partial void OnReady();");
                sb.AppendLine();

                if (members.Count > 0 || signals.Count > 0)
                    AppendWireNodes(sb, indent, members, signals);
            }

            if (signals.Count > 0)
            {
                sb.AppendLine();
                AppendWireSignals(sb, indent, signals);
            }

            if (members.Count > 0)
            {
                sb.AppendLine();
                AppendResolveHelper(sb, indent);
            }

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
            List<MemberTarget> members,
            List<SignalTarget> signals
        )
        {
            sb.AppendLine($"{indent}private void WireNodes()");
            sb.AppendLine($"{indent}{{");
            AppendAssignments(sb, indent + "    ", members);
            if (signals.Count > 0)
                sb.AppendLine($"{indent}    WireSignals();");
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

        private static void AppendWireSignals(
            StringBuilder sb,
            string indent,
            List<SignalTarget> signals
        )
        {
            sb.AppendLine($"{indent}private void WireSignals()");
            sb.AppendLine($"{indent}{{");
            foreach (var s in signals)
            {
                var escapedSignal = EscapeString(s.SignalName);
                var target = string.IsNullOrEmpty(s.NodePath)
                    ? "this"
                    : $"GetNode<Node>(\"{EscapeString(s.NodePath)}\")";
                if (s.Flags == 0)
                    sb.AppendLine(
                        $"{indent}    {target}.Connect(\"{escapedSignal}\", new Callable(this, nameof({s.MethodName})));"
                    );
                else
                    sb.AppendLine(
                        $"{indent}    {target}.Connect(\"{escapedSignal}\", new Callable(this, nameof({s.MethodName})), {s.Flags});"
                    );
            }
            sb.AppendLine($"{indent}}}");
        }

        private static string EscapeString(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    internal readonly struct SignalTarget
    {
        public readonly string Namespace;
        public readonly string ClassName;
        public readonly string MethodName;
        public readonly string SignalName;
        public readonly string NodePath;
        public readonly uint Flags;
        public readonly bool HasReadyOverride;

        public SignalTarget(
            string @namespace,
            string className,
            string methodName,
            string signalName,
            string nodePath,
            uint flags,
            bool hasReadyOverride
        )
        {
            Namespace = @namespace;
            ClassName = className;
            MethodName = methodName;
            SignalName = signalName;
            NodePath = nodePath;
            Flags = flags;
            HasReadyOverride = hasReadyOverride;
        }
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
