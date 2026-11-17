using System;

namespace Saving.Objects
{
    /// <summary>
    /// Saves the field or property with its object; members without it are settings and stay out of the save. The key
    /// is the member name, or the given one: put the former name here after a rename and old saves still read. A
    /// property needs a getter and a setter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SaveAttribute : Attribute
    {
        public SaveAttribute(string key = null) => Key = key;

        public string Key { get; }
    }
}
