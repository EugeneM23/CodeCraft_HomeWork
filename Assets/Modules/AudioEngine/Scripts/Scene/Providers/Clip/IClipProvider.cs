using UnityEngine;

namespace AudioEngine
{
    public interface IClipProvider
    {
        AudioClip Value { get; }
        
        float MaxLength { get; }
    }
}