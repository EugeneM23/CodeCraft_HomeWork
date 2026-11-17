using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>Import settings <see cref="AudioImportRules"/> decides for one clip.</summary>
    public readonly struct AudioImportRule
    {
        public readonly AudioClipLoadType LoadType;
        public readonly bool Preload;
        public readonly bool LoadInBackground;
        public readonly bool ForceToMono;

        public AudioImportRule(AudioClipLoadType loadType, bool preload, bool loadInBackground, bool forceToMono)
        {
            LoadType = loadType;
            Preload = preload;
            LoadInBackground = loadInBackground;
            ForceToMono = forceToMono;
        }

        public override string ToString() =>
            $"{LoadType}, preload {Preload}, background {LoadInBackground}, mono {ForceToMono}";
    }
}
