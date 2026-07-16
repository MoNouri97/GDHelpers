namespace GDHelpers
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    sealed class PreloadAttribute : System.Attribute
    {
        public string Path { get; }

        public PreloadAttribute(string path = null)
        {
            Path = path;
        }
    }
}
