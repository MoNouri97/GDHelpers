using System;
using System.Linq;
using System.Reflection;
using Godot;

namespace GDHelpers
{
    // Extension methods for the Node class.
    public static class NodeExtensions
    {
        public static void RemoveChildren(
            this Node instance,
            Node[] except = null,
            bool free = true
        )
        {
            foreach (var child in instance.GetChildren())
            {
                if (except?.Contains(child) ?? false)
                {
                    continue;
                }
                instance.RemoveChild(child);
                if (free)
                {
                    child.QueueFree();
                }
            }
        }

        public static void MoveChildrenToRoot(this Node instance)
        {
            var root = instance.GetTree().Root;
            foreach (var child in instance.GetChildren())
            {
                instance.RemoveChild(child);
                root.AddChild(child);
                child.Owner = root;
            }
        }

        // Initializes fields in a Node instance that are marked with the OnReadyAttribute.
        public static void WireOnReady(this Node instance)
        {
            // Get all instance fields of the node that are non-public (like private or protected).
            FieldInfo[] fields = instance
                .GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            // Iterate over each field.
            foreach (FieldInfo field in fields)
            {
                // ***** OnReadyAttribute **********************
                CheckOnReadyMember(instance, field);

                // ***** AutoloadAttribute **********************
                CheckAutoloadMember(instance, field);
                CheckPreLoadMember(instance, field);
            }

            // Get all instance properties of the node
            PropertyInfo[] properties = instance
                .GetType()
                .GetProperties(
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance
                );
            // Iterate over each property.
            foreach (PropertyInfo property in properties)
            {
                CheckOnReadyMember(instance, property);
                // ***** AutoloadAttribute **********************
                CheckAutoloadMember(instance, property);
            }
        }

        private static void CheckPreLoadMember(Node instance, MemberInfo member)
        {
            if (Engine.IsEditorHint())
            {
                return;
            }
            if (!Attribute.IsDefined(member, typeof(PreloadAttribute)))
            {
                return;
            }
            // Get the attribute.
            var attr = (PreloadAttribute)
                Attribute.GetCustomAttribute(member, typeof(PreloadAttribute));

            PackedScene scene = (PackedScene)GD.Load(attr.Path);
            if (scene == null)
            {
                GD.PrintErr($"{instance.Name}:failed to preload: {attr.Path},{member.Name}");
            }
            if (member is FieldInfo field)
            {
                //static support
                object targetInstance = field.IsStatic ? null : instance;
                field.SetValue(targetInstance, scene);
            }
        }

        private static void CheckAutoloadMember(Node instance, MemberInfo member)
        {
            if (!Attribute.IsDefined(member, typeof(AutoloadAttribute)))
            {
                return;
            }
            if (Engine.IsEditorHint())
            {
                return;
            }
            // Get the attribute.
            AutoloadAttribute attr = (AutoloadAttribute)
                Attribute.GetCustomAttribute(member, typeof(AutoloadAttribute));
            // Fetch the node based on the path provided in the attribute.
            string nodePath = "/root/" + (attr.NodePath ?? member.Name);

            Node node = instance.GetNode(nodePath);
            if (node == null)
            {
                GD.PrintErr($"{instance.Name}:failed to wire autoload: {nodePath}");
            }
            if (member is FieldInfo field)
            {
                field.SetValue(instance, node);
            }
            else if (member is PropertyInfo property)
            {
                // Ensure the property has a setter
                if (property.SetMethod != null)
                {
                    property.SetValue(instance, node);
                }
                else
                {
                    GD.PrintErr(
                        $"The property {property.Name} in {instance.GetType().Name} has an [Autload] attribute but lacks a 'setter'."
                    );
                }
            }
        }

        private static void CheckOnReadyMember(Node instance, MemberInfo member)
        {
            if (!Attribute.IsDefined(member, typeof(OnReadyAttribute)))
            {
                return;
            }
            // Get the attribute.
            OnReadyAttribute attr = (OnReadyAttribute)
                Attribute.GetCustomAttribute(member, typeof(OnReadyAttribute));
            // Fetch the node based on the path provided in the attribute.
            string nodePath = attr.NodePath ?? member.Name;

            Node node = GetFromName(instance, nodePath);
            try
            {
                if (member is FieldInfo field && field.GetValue(instance) == null)
                {
                    // Set the value of the field to the fetched node.
                    field.SetValue(instance, node);
                }
                else if (member is PropertyInfo property && property.GetValue(instance) == null)
                {
                    // Ensure the property has a setter
                    if (property.SetMethod != null)
                    {
                        property.SetValue(instance, node);
                    }
                    else
                    {
                        GD.PrintErr(
                            $"The property {property.Name} in {instance.GetType().Name} has an [OnReady] attribute but lacks a 'setter'."
                        );
                    }
                }
            }
            catch (Exception)
            {
                if (Engine.IsEditorHint())
                {
                    instance.LogYellow("in editor: can't wire nodes");
                    return;
                }
            }
        }

        private static Node GetFromName(Node instance, string nodePath)
        {
            Node node = instance.GetNodeOrNull(nodePath) ?? instance.GetNodeOrNull($"%{nodePath}");
            if (node == null && nodePath.Length > 0)
            {
                char first = nodePath[0];
                string toggled = (char.IsUpper(first) ? char.ToLower(first) : char.ToUpper(first)) + nodePath[1..];
                node = instance.GetNodeOrNull(toggled) ?? instance.GetNodeOrNull($"%{toggled}");
            }
            if (node == null)
            {
                instance.LogYellow("failed to wire node: " + nodePath);
            }

            return node;
        }
    }
}
