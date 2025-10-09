using System;

namespace SaveLoadSystem
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SaveFieldAttribute : Attribute
    {
        public string? Alias { get; }

        public SaveFieldAttribute(string? alias = null)
        {
            Alias = alias;
        }
    }
}