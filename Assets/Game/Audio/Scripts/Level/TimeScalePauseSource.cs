using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>Paused while Time.timeScale is 0: how the menu's stub pause stops the game for now.</summary>
    public sealed class TimeScalePauseSource : IAudioPauseSource
    {
        public bool IsPaused => Time.timeScale == 0f;
    }
}
