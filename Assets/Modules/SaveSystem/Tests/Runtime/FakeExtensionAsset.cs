using System;
using System.Collections.Generic;

namespace Saving.Tests
{
    /// <summary>Settings asset whose layer only counts captures and remembers being disposed.</summary>
    public sealed class FakeExtensionAsset : SaveExtensionAsset
    {
        public Layer Created { get; private set; }

        public override ISaveExtension CreateExtension(SaveLoadService service) => Created = new Layer();

        public sealed class Layer : ISaveExtension
        {
            public int Captures;
            public bool Disposed;

            public void CaptureInto(IDictionary<string, SaveSection> sections) => Captures++;

            public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
            {
                problem = null;
                return LoadOutcome.Loaded;
            }

            public void BeginLoad(Action finished) => finished();

            public void Dispose() => Disposed = true;
        }
    }
}
