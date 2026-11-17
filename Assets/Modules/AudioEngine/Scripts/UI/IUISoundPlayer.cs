using UnityEngine;

namespace AudioEngine
{
    public interface IUISoundPlayer
    {
        bool PlayOneShot(string soundKey);
        
        void PlayOneShot(AudioClip sound);
    }
}