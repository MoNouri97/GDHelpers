using Godot;
using static Godot.GD;

namespace GDHelpers
{
    public static class Logger
    {
        public static void Log(this GodotObject instance, params object[] text)
        {
            string s = ConcatinateStr(text);
            Print(instance, s, "white");
        }

        public static void LogGreen(this GodotObject instance, params object[] text)
        {
            string s = ConcatinateStr(text);
            Print(instance, s, "green");
        }

        private static string ConcatinateStr(object[] text)
        {
            var s = "";
            foreach (var item in text)
            {
                s += $" {item}";
            }

            return s;
        }

        public static void LogYellow(this GodotObject instance, params object[] text)
        {
            string s = ConcatinateStr(text);
            Print(instance, s, "yellow");
        }

        public static void LogRed(this GodotObject instance, params object[] text)
        {
            string s = ConcatinateStr(text);
            Print(instance, s, "red", ignore: true);
        }

        public static void LogGray(this GodotObject instance, params object[] text)
        {
            string s = ConcatinateStr(text);
            Print(instance, s, "gray");
        }

        public static void Print(
            this GodotObject instance,
            object text,
            string color = "wite",
            bool ignore = false
        )
        {
            var disabled = instance.Get("logDisabled").AsBool();
            if (!ignore && disabled)
            {
                return;
            }

            string path = "";
            if (instance is Node n)
            {
                path = Engine.IsEditorHint() ? "Editor" : n.GetPath().ToString();
            }
            PrintRich($"[color={color}]{text}[/color] in {path}");
        }
    }
}
