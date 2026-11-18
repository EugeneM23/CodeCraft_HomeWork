using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The elite Regax's sounds, at the point they happen. The elite calls them; the game's implementation forwards
    /// to the audio team's GameAudio (EliteAudioNull is silent, for tests and before the audio team's methods exist).
    /// </summary>
    public interface IEliteAudio
    {
        void Roar(Vector3 at);

        void Dash(Vector3 at);

        void Leap(Vector3 at);

        void Land(Vector3 at);

        void Claws(Vector3 at);

        void TearChunk(Vector3 at);

        void Throw(Vector3 at);

        void Hurt(Vector3 at);

        /// <summary>Finished off: crushed at low health.</summary>
        void Finished(Vector3 at);
    }
}
