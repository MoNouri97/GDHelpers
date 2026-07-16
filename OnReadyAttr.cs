namespace GDHelpers
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class OnReadyAttribute : System.Attribute
    {
        public string NodePath { get; }

        public OnReadyAttribute(string nodePath = null)
        {
            NodePath = nodePath;
        }
    }
}
