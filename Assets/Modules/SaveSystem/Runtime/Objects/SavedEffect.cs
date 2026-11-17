using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// A one-shot effect a load brought back (an explosion made from a prefab without scripts). The game destroyed
    /// the original by a timer of its own — Destroy(effect, 5f) — and that timer is not something a save can see, so
    /// the copy would stay for ever. This destroys it once nothing in it is alive any more: no particle system has
    /// particles or is still emitting, no sound is playing. A looping effect stays until its scene goes.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(32001)] // after the Saveable that puts the saved state back in its Start
    internal sealed class SavedEffect : MonoBehaviour
    {
        /// <summary>Something in the object is still going on: particles alive or to come, a sound playing.</summary>
        public static bool IsAlive(GameObject target)
        {
#if SAVE_PARTICLES
            foreach (ParticleSystem particles in target.GetComponentsInChildren<ParticleSystem>(false))
            {
                if (particles.IsAlive(false))
                    return true;
            }
#endif
#if SAVE_AUDIO
            foreach (AudioSource audio in target.GetComponentsInChildren<AudioSource>(false))
            {
                if (audio.isPlaying)
                    return true;
            }
#endif
            return false;
        }

        /// <summary>Takes care of the end of an effect made again by a load.</summary>
        public static void Watch(GameObject effect)
        {
            if (!effect.TryGetComponent(out SavedEffect _))
                effect.AddComponent<SavedEffect>().hideFlags |= HideFlags.HideInInspector;
        }

        private bool _started;

        // The first Update comes after every Start of the frame: the saved state is in by then
        private void Start() => _started = true;

        private void Update()
        {
            if (_started && !IsAlive(gameObject))
                Destroy(gameObject);
        }
    }
}
