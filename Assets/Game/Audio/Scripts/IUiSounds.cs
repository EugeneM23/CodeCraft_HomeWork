namespace Game.AudioEngine.Adapters
{
    /// <summary>Interface sounds the menu plays. Victory and Defeat are the battle result stingers.</summary>
    public interface IUiSounds
    {
        void Hover();
        void Click();
        void Back();
        void Open();
        void Purchase();
        void Error();
        void Victory();
        void Defeat();

        /// <summary>Any key of the UI catalog (<see cref="AudioKeys.UI"/>); a key without a clip stays silent.</summary>
        void Play(string key);
    }
}
