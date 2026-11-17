namespace AudioEngine
{
    public interface IAudioEventBehaviour
    {
        void OnStart(in AudioSourceEvent evt);

        void OnUpdate(in AudioSourceEvent evt, in float deltaTime);

        void OnStop(in AudioSourceEvent evt);

        void OnReset(in AudioSourceEvent evt);
    }
}