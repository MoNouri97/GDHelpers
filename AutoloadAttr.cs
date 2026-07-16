using System;

namespace GDHelpers
{
    [AttributeUsage(AttributeTargets.Field)]
    sealed class AutoloadAttribute : Attribute
    {
        public string NodePath { get; }

        public AutoloadAttribute(string name = null)
        {
            NodePath = name;
        }
    }
}
