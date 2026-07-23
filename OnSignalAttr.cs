namespace GDHelpers
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class OnSignalAttribute : System.Attribute
    {
        public string Signal { get; }
        public string NodePath { get; }
        public uint Flags { get; }

        public OnSignalAttribute(string signal, string nodePath = null, uint flags = 0)
        {
            Signal = signal;
            NodePath = nodePath;
            Flags = flags;
        }
    }
}
