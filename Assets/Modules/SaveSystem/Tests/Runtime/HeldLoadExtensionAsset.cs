using System;
using System.Collections.Generic;

namespace Saving.Tests
{
    /// <summary>Settings asset whose layer holds every load open until the test calls <see cref="Layer.Finish"/>.</summary>
    public sealed class HeldLoadExtensionAsset : SaveExtensionAsset
    {
        public Layer Created { get; private set; }

        public override ISaveExtension CreateExtension(SaveLoadService service) => Created = new Layer();

        public sealed class Layer : ISaveExtension
        {
            private Action _finished;

            public void CaptureInto(IDictionary<string, SaveSection> sections)
            {
            }

            public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
            {
                problem = null;
                return LoadOutcome.Loaded;
            }

            public void BeginLoad(Action finished) => _finished = finished;

            public void Finish()
            {
                Action finished = _finished;
                _finished = null;
                finished?.Invoke();
            }

            public void Dispose()
            {
            }
        }
    }
}
