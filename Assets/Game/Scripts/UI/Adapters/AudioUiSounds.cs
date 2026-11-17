using Game.AudioEngine.Adapters;

namespace Game.UI.Adapters
{
    /// <summary>
    /// The menus' interface sounds (Game.IUiSounds) played by the sound team's AppAudio. AppAudio is looked up on
    /// every sound: it is made by its own bootstrap and may come after this bridge; without it nothing plays.
    /// </summary>
    public sealed class AudioUiSounds : Game.IUiSounds
    {
        public void Play(UiSound sound)
        {
            AppAudio audio = AppAudio.Instance;
            Game.AudioEngine.Adapters.IUiSounds sounds = audio != null ? audio.UiSounds : null;
            if (sounds == null)
                return;

            switch (sound)
            {
                case UiSound.Hover: sounds.Hover(); break;
                case UiSound.Click: sounds.Click(); break;
                case UiSound.Back: sounds.Back(); break;
                case UiSound.Open: sounds.Open(); break;
                case UiSound.Purchase: sounds.Purchase(); break;
                case UiSound.Error: sounds.Error(); break;
                case UiSound.Victory: sounds.Victory(); break;
                case UiSound.Defeat: sounds.Defeat(); break;
            }
        }
    }
}
