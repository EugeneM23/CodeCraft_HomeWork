using AudioEngine;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The menu's interface sounds through the module's UISoundPlayer. A key without a clip stays silent:
    /// the catalog is read here, since UISoundPlayer would hand a null clip to PlayOneShot. Victory and Defeat
    /// go to the stinger source (Music channel) when there is one.
    /// </summary>
    public sealed class GameUiSounds : IUiSounds
    {
        private readonly UISoundPlayer _player;
        private readonly UISoundCatalog _catalog;
        private readonly AudioSource _stingerSource;

        public GameUiSounds(UISoundPlayer player, UISoundCatalog catalog, AudioSource stingerSource = null)
        {
            _player = player;
            _catalog = catalog;
            _stingerSource = stingerSource;
        }

        public void Hover() => Play(AudioKeys.UI.HOVER);
        public void Click() => Play(AudioKeys.UI.CLICK);
        public void Back() => Play(AudioKeys.UI.BACK);
        public void Open() => Play(AudioKeys.UI.OPEN);
        public void Purchase() => Play(AudioKeys.UI.PURCHASE);
        public void Error() => Play(AudioKeys.UI.ERROR);
        public void Victory() => PlayStinger(AudioKeys.UI.VICTORY);
        public void Defeat() => PlayStinger(AudioKeys.UI.DEFEAT);

        public void Play(string key)
        {
            if (_player != null && TryGetClip(key, out AudioClip clip))
                _player.PlayOneShot(clip);
        }

        private void PlayStinger(string key)
        {
            if (_stingerSource == null)
            {
                Play(key);
                return;
            }

            if (TryGetClip(key, out AudioClip clip))
                _stingerSource.PlayOneShot(clip);
        }

        private bool TryGetClip(string key, out AudioClip clip)
        {
            clip = null;
            return _catalog != null && _catalog.sounds != null && _catalog.TryGetSound(key, out clip) && clip != null;
        }
    }
}
