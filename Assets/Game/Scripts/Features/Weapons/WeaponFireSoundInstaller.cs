using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A shot sound for a weapon that has no visual of its own in a hand — the vehicles' guns.
    ///
    /// Each shot restarts the next of a few audio sources in turn, so a fast gun keeps at most that
    /// many voices. It used to be a PlayOneShot per shot: the helicopter's 32 rounds a second kept
    /// some fifteen one-shots ringing, out of Unity's 32 real voices, and as the quietest sounds in
    /// the scene they were the ones Unity made virtual — the gun fired in silence.
    /// </summary>
    public sealed class WeaponFireSoundInstaller : MonoEntityInstaller
    {
        [Tooltip("Restarted in turn, one per shot: as many voices as sources at most.")]
        [SerializeField] private AudioSource[] _sources;
        [Tooltip("One is picked at random for each shot.")]
        [SerializeField] private AudioClip[] _clips;
        [Tooltip("A sound every this many shots: a fast gun need not sound every round to sound like one.")]
        [SerializeField, Min(1)] private int _shotsPerSound = 1;

        public override void Install(IEntity entity) =>
            entity.AddBehaviour(new WeaponFireSoundBehaviour(_sources, _clips, _shotsPerSound));
    }

    /// <summary>
    /// Restarts the next source in turn with a random clip each time the weapon's FireEvent goes
    /// off — on the first shot and then every <c>shotsPerSound</c>-th one.
    /// </summary>
    public sealed class WeaponFireSoundBehaviour : IEntityInit, IEntityDispose
    {
        private readonly AudioSource[] _sources;
        private readonly AudioClip[] _clips;
        private readonly int _shotsPerSound;
        private int _next;
        private int _shot;

        public WeaponFireSoundBehaviour(AudioSource[] sources, AudioClip[] clips, int shotsPerSound = 1)
        {
            _sources = sources ?? new AudioSource[0];
            _clips = clips;
            _shotsPerSound = Mathf.Max(1, shotsPerSound);
        }

        public void Init(IEntity entity) => entity.GetFireEvent().Subscribe(Play);

        public void Dispose(IEntity entity) => entity.GetFireEvent().Unsubscribe(Play);

        private void Play()
        {
            bool sounds = _shot == 0;
            _shot = (_shot + 1) % _shotsPerSound;
            if (!sounds)
                return;

            if (_sources.Length == 0 || _clips == null || _clips.Length == 0)
                return;

            AudioSource source = _sources[_next];
            _next = (_next + 1) % _sources.Length;

            AudioClip clip = _clips[Random.Range(0, _clips.Length)];
            if (source == null || clip == null)
                return;

            source.clip = clip;
            source.Play();
        }
    }
}
