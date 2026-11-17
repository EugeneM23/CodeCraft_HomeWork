using UnityEngine;

namespace Saving
{
    /// <summary>
    /// Slots in PlayerPrefs: for WebGL and small games without file access. Mind the platform limits (WebGL keeps about
    /// 1 MB of PlayerPrefs in all); big saves belong in files or a server.
    /// </summary>
    public sealed class PlayerPrefsSaveStorage : KeyValueSaveStorage
    {
        public PlayerPrefsSaveStorage(string prefix = DEFAULT_PREFIX) : base(prefix)
        {
        }

        protected override bool TryGet(string key, out string text)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                text = null;
                return false;
            }

            text = PlayerPrefs.GetString(key);
            return true;
        }

        protected override void Set(string key, string text) => PlayerPrefs.SetString(key, text);
        protected override void Remove(string key) => PlayerPrefs.DeleteKey(key);
        protected override void Flush() => PlayerPrefs.Save();
    }
}
