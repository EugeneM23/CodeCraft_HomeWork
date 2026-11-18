using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>Silent <see cref="IEliteAudio"/>: tests, and the game until the audio team's elite sounds exist.</summary>
    public sealed class EliteAudioNull : IEliteAudio
    {
        public static readonly EliteAudioNull Instance = new();

        public void Roar(Vector3 at) { }

        public void Dash(Vector3 at) { }

        public void Leap(Vector3 at) { }

        public void Land(Vector3 at) { }

        public void Claws(Vector3 at) { }

        public void TearChunk(Vector3 at) { }

        public void Throw(Vector3 at) { }

        public void Hurt(Vector3 at) { }

        public void Finished(Vector3 at) { }
    }
}
