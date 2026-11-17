using Newtonsoft.Json.Linq;

namespace Saving
{
    /// <summary>One system's data in a save file plus the version it was written with.</summary>
    public sealed class SaveSection
    {
        public int V;
        public JToken Data;
    }
}
