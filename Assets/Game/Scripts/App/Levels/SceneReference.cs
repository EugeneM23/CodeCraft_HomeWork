using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A scene picked in the Inspector and kept as its path (Assets/.../Name.unity), which is what the game loads by:
    /// a build has no SceneAsset. In the editor the picked SceneAsset is serialized next to the path and the path
    /// follows it on every save, so moving or renaming the scene does not break the reference.
    /// </summary>
    [Serializable]
    public sealed class SceneReference : ISerializationCallbackReceiver
    {
#if UNITY_EDITOR
        [SerializeField] private UnityEditor.SceneAsset _asset;
#endif
        [Tooltip("Filled from the scene asset; the game loads the scene by this path")]
        [SerializeField] private string _path = string.Empty;

        public SceneReference()
        {
        }

        /// <summary>From code (tests): the scene by its path.</summary>
        public SceneReference(string path)
        {
            _path = path ?? string.Empty;
        }

        public string Path => _path;
        public bool IsEmpty => string.IsNullOrEmpty(_path);

        public bool Is(string scenePath) => !IsEmpty && string.Equals(_path, scenePath, StringComparison.Ordinal);

        public override string ToString() => IsEmpty ? "<no scene>" : _path;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
#if UNITY_EDITOR
            // A reference made from code has a path and no asset: keep the path
            if (_asset == null)
                return;

            string path = UnityEditor.AssetDatabase.GetAssetPath(_asset);
            if (!string.IsNullOrEmpty(path))
                _path = path;
#endif
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
        }
    }
}
