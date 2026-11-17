namespace AudioEngine
{
    public interface IAudioEventAction
    {
        void Invoke(in AudioSourceEvent evt);
    }
}