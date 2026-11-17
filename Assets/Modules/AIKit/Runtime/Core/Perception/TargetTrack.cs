using UnityEngine;

namespace AIKit
{
    /// <summary>Finds the transform of a subject known only by id (a loaded save, a respawned entity).</summary>
    public interface ITargetResolver
    {
        Transform Resolve(int id);
    }

    /// <summary>
    /// Who the agent is busy with, as a descriptor: an id that survives saves and respawns, plus the transform when it
    /// is known. The transform is resolved lazily (through ITargetResolver when only the id is known) and a subject
    /// whose object was destroyed or went back to a pool (inactive) resolves to nothing.
    /// </summary>
    public readonly struct TargetSubject
    {
        /// <summary>The entity id given by whoever chose the target; 0 — none (the transform is the identity).</summary>
        public readonly int Id;

        private readonly Transform _transform;

        public TargetSubject(Transform transform, int id = 0)
        {
            _transform = transform;
            Id = id;
        }

        public static TargetSubject None => default;

        /// <summary>Something was chosen (it may already be gone — see Resolve).</summary>
        public bool IsSet => Id != 0 || !ReferenceEquals(_transform, null);

        /// <summary>The live object, or null when it is destroyed, pooled or cannot be found.</summary>
        public Transform Resolve(ITargetResolver resolver)
        {
            Transform transform = _transform != null ? _transform : Id != 0 ? resolver?.Resolve(Id) : null;
            return transform != null && transform.gameObject.activeInHierarchy ? transform : null;
        }

        public bool Is(Transform transform) => !ReferenceEquals(transform, null) && ReferenceEquals(transform, _transform);
    }

    /// <summary>
    /// What the agent knows about its target: the subject, how aware of it the agent is, where it was last seen and how
    /// long ago. Perception fills it (the game's bridge in stage 4, the module's sensors in stage 7) — the type does not
    /// change. While the subject is alive the track follows it; once it is destroyed or pooled the subject is dropped
    /// for good (a respawn from the pool is a new target, not the old one coming back) and only the memory remains:
    /// the last known position and the time since it was seen.
    /// </summary>
    public sealed class TargetTrack
    {
        private Transform _live;
        private bool _wasLive;

        /// <summary>Resolves subjects known only by id; null — only subjects with a transform.</summary>
        public ITargetResolver Resolver { get; set; }

        public TargetSubject Subject { get; private set; }

        /// <summary>0..1: how sure the agent is (perception raises it, forgetting lowers it).</summary>
        public float Awareness { get; set; }

        public Vector3 LastKnownPosition
        {
            get => _lastKnown;
            set
            {
                _lastKnown = value;
                _remembers = true;
            }
        }

        private Vector3 _lastKnown;
        private bool _remembers;

        /// <summary>The agent has a target or remembers where one was: there is somewhere to turn to and to go to.</summary>
        public bool Remembers => IsValid || _remembers;

        /// <summary>Seconds since the subject was last seen (0 while it is followed).</summary>
        public float TimeSinceSeen { get; private set; }

        /// <summary>There is a live subject.</summary>
        public bool IsValid => Transform != null;

        /// <summary>The live subject's transform, or null.</summary>
        public Transform Transform
        {
            get
            {
                if (!Subject.IsSet)
                    return null;

                if (_live != null && _live.gameObject.activeInHierarchy)
                    return _live;

                if (_wasLive)
                {
                    Drop();                         // it was there and is gone: destroyed or back in the pool
                    return null;
                }

                _live = Subject.Resolve(Resolver);  // not found yet (an id waiting for its entity) — ask again later
                _wasLive = _live != null;
                return _live;
            }
        }

        /// <summary>Where to go: the live subject, otherwise the last place it was seen.</summary>
        public Vector3 Position => Transform is { } live ? live.position : LastKnownPosition;

        /// <summary>A target was chosen (perception, a debug hook, a test).</summary>
        public void Set(TargetSubject subject, float awareness = 1f)
        {
            Subject = subject;
            _live = null;
            _wasLive = false;
            Awareness = subject.IsSet ? awareness : 0f;
            if (Transform is { } live)
            {
                LastKnownPosition = live.position;
                TimeSinceSeen = 0f;
            }
        }

        public void Set(Transform transform, float awareness = 1f) => Set(new TargetSubject(transform), awareness);

        /// <summary>The subject is no longer the target; the memory of where it was stays.</summary>
        public void Drop()
        {
            Subject = TargetSubject.None;
            _live = null;
            _wasLive = false;
        }

        /// <summary>Forgets everything: no subject, no memory.</summary>
        public void Clear()
        {
            Drop();
            Awareness = 0f;
            _lastKnown = default;
            _remembers = false;
            TimeSinceSeen = 0f;
        }

        /// <summary>One step: a live subject is followed, a lost one ages.</summary>
        public void Advance(float dt)
        {
            if (Transform is { } live)
            {
                LastKnownPosition = live.position;
                TimeSinceSeen = 0f;
            }
            else
            {
                TimeSinceSeen += dt;
            }
        }
    }
}
