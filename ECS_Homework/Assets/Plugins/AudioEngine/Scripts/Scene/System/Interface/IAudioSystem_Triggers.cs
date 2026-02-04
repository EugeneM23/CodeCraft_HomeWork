using System;

namespace AudioEngine
{
    public partial interface IAudioSystem
    {
        void SetTrigger(string triggerId, Action trigger);
        void ResetTrigger(string triggerId);
    }
}