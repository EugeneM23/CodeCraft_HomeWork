using System.Collections.Generic;

namespace Saving.Objects
{
    /// <summary>A serializer with switches of its own; the Saveable inspector shows them under the component.</summary>
    public interface ISerializerOptions
    {
        IReadOnlyList<string> Options { get; }
    }
}
