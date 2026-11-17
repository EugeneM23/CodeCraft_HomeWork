using System;
using System.Collections.Generic;

namespace Saving
{
    /// <summary>
    /// A layer plugged into <see cref="SaveLoadService"/> that keeps its own sections, such as the objects layer with
    /// scene sections. It writes them on every capture, checks a file before a load applies anything and finishes a
    /// load later, once the saved scene is restored. During SaveLoadService.LoadInPlace (SaveLoadService.KeepsScene) it
    /// keeps the scenes in play instead of loading the saved one.
    /// </summary>
    public interface ISaveExtension : IDisposable
    {
        /// <summary>Writes the layer's sections into the snapshot; runs after the global serializers.</summary>
        void CaptureInto(IDictionary<string, SaveSection> sections);

        /// <summary>Checks the file's sections before anything is applied: Loaded when the layer can restore them.</summary>
        LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem);

        /// <summary>Restores the layer after the global sections; calls <paramref name="finished"/> once, maybe later.</summary>
        void BeginLoad(Action finished);
    }
}
