using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// An object of the world characters use — a rock, a pile of them, a turret, a cover, a perch, a place of the village
    /// (AI Object): a smart object (handbook §24, §24.1). It registers itself in the scene's registry while it is
    /// enabled (WorldObjects — no node ever scans the scene), it says itself what it offers to whom right now, where the
    /// user stands and how near counts, and it owns the scenario of the interaction. Its owners (an author's list,
    /// §24: a house, a work place, a gunner's own turret) are named on the object, never on the character; no owners —
    /// it is public. The base offers nothing: a kind of object says what it offers.
    /// </summary>
    public abstract class WorldObject : MonoBehaviour, IInteractable
    {
        /// <summary>Metres a user may stray from its place and still count as there (it was pushed, it turned).</summary>
        public const float LEAVE_SLACK = 1f;

        [Tooltip("The characters this object is for (a house, a work place, a gunner's own turret): their searches " +
                 "find it first, and a character that owns objects of a kind looks for no others. Empty — anyone's")]
        [SerializeField] private List<AICharacter> _owners = new();

        /// <summary>The characters the object is for; empty — it is public.</summary>
        public List<AICharacter> Owners => _owners;

        /// <summary>Nobody owns it.</summary>
        public bool IsPublic
        {
            get
            {
                for (int i = 0; i < _owners.Count; i++)
                {
                    if (_owners[i] != null)
                        return false;
                }

                return true;
            }
        }

        /// <summary>The character is one of its owners.</summary>
        public bool IsOwnedBy(AICharacter character) => character != null && _owners.Contains(character);

        /// <summary>The object is in the world: its component on, its object active (a used-up thing is not).</summary>
        public bool IsLive => this != null && enabled && gameObject.activeInHierarchy;

        protected virtual void OnEnable() => WorldObjects.Scene.Register(this);

        /// <summary>Switched off (pooled, broken): it is found no more, and nobody stands at it any more.</summary>
        protected virtual void OnDisable()
        {
            WorldObjects.Scene.Unregister(this);
            LetGoOfEveryone();
        }

        /// <summary>
        /// The character is about for the objects it uses: its AI Character is there and switched on — and, in Play, its
        /// object is active. A fallen character is switched off — its brain stops (the lab's Lab Life, a game's death) — and
        /// uses nothing any more, though its body may lie at the place for a while; one taken away with its object (back to
        /// a pool) is gone too. In Edit Mode no character's object runs: only the switch counts.
        /// </summary>
        protected static bool IsAwake(AgentContext user)
        {
            AICharacter character = user?.Agent;
            if (character == null || !character.enabled)
                return false;

            return character.gameObject.activeInHierarchy || !Application.isPlaying;
        }

        // ---------- what it offers (IInteractable) ----------

        public virtual bool Offers(Offer offer, AgentContext user) => false;

        public virtual bool OneAtATime(Offer offer) => true;

        /// <summary>The object itself, on the user's ground: walk up to it.</summary>
        public virtual Vector3 PlaceFor(Offer offer, AgentContext user, Transform threat) => transform.position;

        public virtual float Reach => 0.9f;

        public virtual bool Begin(Interaction interaction) => true;

        public virtual TaskStatus Tick(Interaction interaction, float dt) => TaskStatus.Success;

        public virtual void End(Interaction interaction, ExitReason reason)
        {
        }

        /// <summary>
        /// The user stands at its place for the offer — within Reach of it, flat (the place of a cover follows the
        /// threat; slack — a user who was there may be off by this much more).
        /// </summary>
        public bool IsAtPlace(Offer offer, AgentContext user, Transform threat, float slack = 0f)
        {
            if (user == null)
                return false;

            Vector3 to = PlaceFor(offer, user, threat) - user.Position;
            to.y = 0f;
            float reach = Reach + slack;
            return to.sqrMagnitude <= reach * reach;
        }

        // ---------- occupying (cover, perch, a place) ----------

        // Who stands at the object now (Occupy), each with the threat they took it against
        private readonly List<AgentContext> _occupants = new();
        private readonly List<Transform> _threats = new();

        /// <summary>
        /// The character occupies the object (it stands there: behind the cover, on the perch) — true while the object is in
        /// the world and the character still there: switched on (a fallen one is not), and no farther than LEAVE_SLACK
        /// beyond the object's reach of its place. Asked, not kept: a user that walked off or fell occupies nothing any
        /// more, whoever asks first.
        /// </summary>
        public bool IsOccupiedBy(AgentContext user)
        {
            int at = _occupants.IndexOf(user);
            return at >= 0 && StillThere(at);
        }

        /// <summary>How many characters occupy it now.</summary>
        public int OccupantCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _occupants.Count; i++)
                {
                    if (StillThere(i))
                        count++;
                }

                return count;
            }
        }

        /// <summary>Somebody other than the user occupies it now.</summary>
        public bool IsOccupiedByAnother(AgentContext user)
        {
            for (int i = 0; i < _occupants.Count; i++)
            {
                if (_occupants[i] != user && StillThere(i))
                    return true;
            }

            return false;
        }

        /// <summary>The threat the user took the object against (a cover); null — none, or it does not occupy it.</summary>
        public Transform ThreatOf(AgentContext user)
        {
            int at = _occupants.IndexOf(user);
            return at >= 0 ? _threats[at] : null;
        }

        /// <summary>The user stands at the object from now on (the Occupy scenario); it publishes it as occupied.</summary>
        protected void TakeOccupant(AgentContext user, Transform threat)
        {
            if (user == null)
                return;

            Prune();
            int at = _occupants.IndexOf(user);
            if (at < 0)
            {
                _occupants.Add(user);
                _threats.Add(threat);
            }
            else
            {
                _threats[at] = threat;
            }

            user.Occupy(this, threat);
        }

        /// <summary>The user does not stand at the object any more (it is left, or the object goes).</summary>
        public void LetGoOf(AgentContext user)
        {
            int at = _occupants.IndexOf(user);
            if (at < 0)
                return;

            _occupants.RemoveAt(at);
            _threats.RemoveAt(at);
            user?.LeftObject(this);
        }

        /// <summary>
        /// The occupant is still there: switched on (IsAwake), standing within the object's reach of its place for it (plus
        /// LEAVE_SLACK). A kind of object may say otherwise (a gunner is at a turret while it holds its gun).
        /// </summary>
        protected virtual bool StillThere(AgentContext user, Transform threat) =>
            IsAwake(user) && IsAtPlace(Offer.Occupy, user, threat, LEAVE_SLACK);

        // An object switched off has nobody at it; while it is in the world, its kind says who is still there
        private bool StillThere(int at) => IsLive && StillThere(_occupants[at], _threats[at]);

        // Everybody at the object leaves it (it is switched off)
        private void LetGoOfEveryone()
        {
            for (int i = _occupants.Count - 1; i >= 0; i--)
                LetGoOf(_occupants[i]);
        }

        // The occupants who are gone (fallen, destroyed, or walked off) leave their places
        private void Prune()
        {
            for (int i = _occupants.Count - 1; i >= 0; i--)
            {
                if (StillThere(i))
                    continue;

                AgentContext gone = _occupants[i];
                _occupants.RemoveAt(i);
                _threats.RemoveAt(i);
                gone?.LeftObject(this);
            }
        }

        /// <summary>
        /// Where a character goes to reach the object: an object of the world — its place for the character (against the
        /// Brain's target: a cover), anything else — where it stands. For the blocks that move (Move To, Climb To).
        /// </summary>
        public static Vector3 PlaceOf(Transform target, AgentContext context)
        {
            if (target == null)
                return Vector3.zero;

            WorldObject worldObject = target.GetComponentInParent<WorldObject>();
            return worldObject != null
                ? worldObject.PlaceFor(Offer.Occupy, context, context?.BrainTarget)
                : target.position;
        }

        // ---------- helpers of the scenarios ----------

        /// <summary>The user turns to the point (when its block may turn it).</summary>
        protected static void Face(Interaction interaction, Vector3 point)
        {
            AgentContext user = interaction?.User;
            if (user == null || !interaction.MayFace)
                return;

            Vector3 to = point - user.Position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f)
                user.Actuator?.Face(to);
        }

        /// <summary>The body shows the action (a state of its presentation by name); empty — none.</summary>
        protected static void ShowAction(Interaction interaction, string action)
        {
            if (interaction?.User == null || string.IsNullOrEmpty(action))
                return;

            interaction.User.Actions.Begin(action);
            interaction.ShowsAction = true;
        }

        /// <summary>The action asked for is over: shown to its end (finished) or dropped at once (torn away).</summary>
        protected static void EndAction(Interaction interaction, bool atOnce)
        {
            if (interaction?.User == null || !interaction.ShowsAction)
                return;

            interaction.User.Actions.End(atOnce);
            interaction.ShowsAction = false;
        }

        protected static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
