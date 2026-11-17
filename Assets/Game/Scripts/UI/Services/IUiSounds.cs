namespace Game
{
    /// <summary>Interface sounds. The UI only reports what happened; the sound team plays the clips.</summary>
    public interface IUiSounds
    {
        void Play(UiSound sound);
    }
}
