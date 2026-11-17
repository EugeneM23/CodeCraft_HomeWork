using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The character and the objects of the world (step 90): the registry its searches ask, the object it occupies now
    /// (behind a cover, on a perch, at a turret — Occupied, InCover: what its status says), the home it patrols around
    /// (where it woke up) and how its last search went.
    /// </summary>
    public sealed partial class AgentContext
    {
        private WorldObjects _worldObjects;
        private WorldObject _occupied;

        /// <summary>
        /// The registry of the objects of the agent's world: the world's own (AIWorld.Objects), the scene's without a
        /// world. Set — the agent searches that one (a test).
        /// </summary>
        public WorldObjects WorldObjects
        {
            get => _worldObjects ?? Agent?.World?.Objects ?? WorldObjects.Scene;
            set => _worldObjects = value;
        }

        /// <summary>
        /// The object the character occupies now (it stands behind the cover, on the perch, mans the turret); null —
        /// none. Asked, not kept: a character that walked off occupies nothing, whoever asks first. The status reads it
        /// (Occupying).
        /// </summary>
        public Transform Occupied => OccupiedObject != null ? _occupied.transform : null;

        /// <summary>The object the character occupies now, as an object of the world; null — none.</summary>
        public WorldObject OccupiedObject => _occupied != null && _occupied.IsOccupiedBy(this) ? _occupied : null;

        /// <summary>
        /// The character occupies a cover that still shields it from the threat it took it against (handbook §18а.2: the
        /// threat walked round the cover — it is in cover no more, and its search for cover holds again). The status
        /// reads it (In Cover).
        /// </summary>
        public bool InCover => OccupiedObject is Cover cover && cover.Shields(this);

        /// <summary>The object's scenario: the character stands at it from now on (one object at a time).</summary>
        internal void Occupy(WorldObject target, Transform threat)
        {
            if (_occupied != null && _occupied != target)
            {
                WorldObject previous = _occupied;
                _occupied = null;
                previous.LetGoOf(this);
            }

            _occupied = target;
        }

        /// <summary>The object lets the character go (it walked off, the object went).</summary>
        internal void LeftObject(WorldObject target)
        {
            if (_occupied == target)
                _occupied = null;
        }

        // ---------- home ----------

        private bool _hasHome;
        private Vector3 _home;

        /// <summary>
        /// Where the character woke up (its first step in the world; handbook §25б.1: home = the spawn point): Patrol
        /// Area without an area walks around it. Fixed once — a chase does not move it.
        /// </summary>
        public Vector3 Home
        {
            get => _hasHome ? _home : Position;
            set
            {
                _home = value;
                _hasHome = true;
            }
        }

        /// <summary>The home is fixed (the character has woken up, or a test set it).</summary>
        public bool HasHome => _hasHome;

        /// <summary>The character wakes up here: its home is fixed, once (later calls change nothing).</summary>
        public void WakeUp()
        {
            if (!_hasHome)
                Home = Position;
        }

        // ---------- searches ----------

        /// <summary>
        /// When the character's last search (a Find) found nothing, on its own clock; negative — never. The decision
        /// whose chain it was (Search For) rests after it.
        /// </summary>
        public float NothingFoundAt { get; internal set; } = float.NegativeInfinity;
    }
}
