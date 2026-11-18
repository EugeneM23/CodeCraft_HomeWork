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

                // The audio adapter's IUiSounds in this branch has no methods for these yet: the nearest old sound.
                // They move to IUiSounds.Play(AudioKeys.UI.*) when the audio team's new interface is merged.
                case UiSound.Close: sounds.Back(); break;
                case UiSound.Install: sounds.Purchase(); break;
                case UiSound.Upgrade: sounds.Purchase(); break;
                case UiSound.MarksTick: sounds.Hover(); break;
                case UiSound.SamplesTick: sounds.Hover(); break;
                case UiSound.PauseOn: sounds.Open(); break;
                case UiSound.PauseOff: sounds.Back(); break;
                case UiSound.SkillChoice: sounds.Click(); break;
                case UiSound.SkillTaken: sounds.Click(); break;
                case UiSound.LevelUp: sounds.Open(); break;
            }
        }
    }
}
