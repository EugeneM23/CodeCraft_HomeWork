using System;

namespace AudioEngine
{
    public partial interface IAudioSystem
    {
        void SubscribeOnCallback(string callbackId, Action callback);
        void UnsubscribeFromCallback(string callbackId, Action callback);
    }
}