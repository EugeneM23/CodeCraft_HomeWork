using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>What a search asks the registry for: objects of a component, of an AI Object's kind, of a tag, near a point.</summary>
    public struct ObjectQuery
    {
        /// <summary>The objects have this component (a kind of WorldObject: Cover, Turret…); null — any.</summary>
        public Type Component;

        /// <summary>An AI Object's kind ("Home", "Work"); empty — any (a kind given, only AI Objects of it).</summary>
        public string Kind;

        /// <summary>The objects' Unity tag; empty — any.</summary>
        public string Tag;

        /// <summary>Where the radius is measured from.</summary>
        public Vector3 Center;

        /// <summary>Metres from Center (flat); 0 or less — anywhere.</summary>
        public float Radius;

        /// <summary>The object is one the query asks for (its component, kind and tag; not where it is).</summary>
        public bool Matches(WorldObject candidate)
        {
            if (candidate == null)
                return false;

            // A kind of object of the world (Turret, Cover…) is found as itself — never as another object of the world that
            // happens to stand on the same game object (an AI Object marker on a turret is not the turret: it offers none
            // of the turret's interactions). Any other component (a game's Potion) is found through the object of the world
            // that carries it.
            if (Component != null && !Component.IsInstanceOfType(candidate) &&
                (typeof(WorldObject).IsAssignableFrom(Component) || !typeof(Component).IsAssignableFrom(Component) ||
                 candidate.GetComponent(Component) == null))
                return false;

            if (!string.IsNullOrEmpty(Kind) && (candidate is not AIObject marker || marker.Kind != Kind))
                return false;

            return string.IsNullOrEmpty(Tag) || Tag == "Untagged" || candidate.CompareTag(Tag);
        }

        /// <summary>The object is within the radius of the centre (flat).</summary>
        public bool IsNear(WorldObject candidate)
        {
            if (Radius <= 0f)
                return true;

            Vector3 to = candidate.transform.position - Center;
            to.y = 0f;
            return to.sqrMagnitude <= Radius * Radius;
        }
    }

    /// <summary>
    /// The one gateway to the objects of the world (handbook §24: "a task never scans the world"): every WorldObject of
    /// the scene registers itself here while it is enabled, and a search asks the registry, never the scene. Besides who
    /// is there it keeps, for the characters of its world:
    /// - claims — who goes for an object now (three must not go for one turret): one holder at a time, for a while (a TTL
    ///   counted on the world's clock, renewed while the holder's chain uses the object); the decision in charge leaving
    ///   lets go of all of its agent's claims, and a claim of a character that is gone is nobody's. A claim is an
    ///   efficiency layer, not the truth: whether one may use the object is its own say at the moment of use;
    /// - the unreachable journal — an object a character could find no way to is not offered to it for a while.
    /// The scene's registry (Scene) is made anew with every domain and play start; a world (AIWorld.Objects) uses it
    /// unless given its own (a test, a game with several worlds).
    /// </summary>
    public sealed class WorldObjects
    {
        /// <summary>Seconds a claim lasts unless renewed (a search claims, the blocks after it renew while they use it).</summary>
        public const float CLAIM_TTL = 4f;

        /// <summary>Seconds an object a character found no way to is not offered to it.</summary>
        public const float UNREACHABLE_FOR = 10f;

        /// <summary>The registry the objects of the scene register in (OnEnable / OnDisable).</summary>
        public static WorldObjects Scene { get; private set; } = new();

        // A play start without a domain reload keeps statics: the scene's objects register anew in a clean registry
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetScene() => Scene = new WorldObjects();

        private sealed class Claim
        {
            public AICharacter Holder;
            public float Until;
        }

        private struct Unreachable
        {
            public WorldObject Object;
            public float Until;
        }

        private readonly List<WorldObject> _objects = new();
        private readonly Dictionary<WorldObject, Claim> _claims = new();
        private readonly Dictionary<AICharacter, List<Unreachable>> _unreachable = new();
        private readonly List<WorldObject> _released = new();

        /// <summary>Every object registered now (a destroyed one counts as gone).</summary>
        public IReadOnlyList<WorldObject> All => _objects;

        /// <summary>Seconds of the world's steps since the registry was made: claims and the journal count by it.</summary>
        public float Clock { get; private set; }

        /// <summary>The object is there from now on (it calls it itself, OnEnable).</summary>
        public void Register(WorldObject candidate)
        {
            if (candidate != null && !_objects.Contains(candidate))
                _objects.Add(candidate);
        }

        /// <summary>The object is gone (OnDisable): it is found no more, its claim goes.</summary>
        public void Unregister(WorldObject candidate)
        {
            if (candidate is null)
                return;

            _objects.Remove(candidate);
            _claims.Remove(candidate);
        }

        /// <summary>The world's step: the clock of the claims and of the journal goes on (AIWorld.BeginStep calls it).</summary>
        public void Step(float dt)
        {
            if (dt > 0f)
                Clock += dt;
        }

        /// <summary>
        /// Every registered object the query asks for (component, kind, tag, within the radius), in the order they came;
        /// results is cleared first. No object of the scene is looked at that did not register.
        /// </summary>
        public void Find(in ObjectQuery query, List<WorldObject> results)
        {
            results.Clear();
            for (int i = 0; i < _objects.Count; i++)
            {
                WorldObject candidate = _objects[i];
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
                    continue;

                if (query.Matches(candidate) && query.IsNear(candidate))
                    results.Add(candidate);
            }
        }

        /// <summary>
        /// The tier of an object for a character in the chain of §24: 0 — its own (it is among the owners), 1 — public,
        /// 2 — somebody else's.
        /// </summary>
        public static int TierOf(WorldObject candidate, AICharacter character)
        {
            if (candidate.IsOwnedBy(character))
                return 0;

            return candidate.IsPublic ? 1 : 2;
        }

        /// <summary>
        /// The character owns an object the query asks for (its component, kind and tag) anywhere in the world — the radius
        /// is not looked at: an author's list never falls through to "something near" because its own is far (§24).
        /// </summary>
        public bool OwnsAny(in ObjectQuery query, AICharacter character)
        {
            if (character == null)
                return false;

            for (int i = 0; i < _objects.Count; i++)
            {
                WorldObject candidate = _objects[i];
                if (candidate != null && candidate.IsLive && candidate.IsOwnedBy(character) && query.Matches(candidate))
                    return true;
            }

            return false;
        }

        /// <summary>The highest tier the character may take objects of, given what it owns among the candidates.</summary>
        public static int AllowedTier(Ownership whose, List<WorldObject> candidates, AICharacter character)
        {
            if (whose == Ownership.Mine)
                return 0;

            if (whose == Ownership.Anyone)
                return 2;

            // Mine Or Public: an author's list never falls through to "something near" (§24) — owning one of the kind,
            // the character takes only its own
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != null && candidates[i].IsOwnedBy(character))
                    return 0;
            }

            return 1;
        }

        // ---------- claims ----------

        /// <summary>
        /// The character goes for the object: it holds the claim for ttl seconds of the world (a holder asking again
        /// renews it). False — another character holds it.
        /// </summary>
        public bool TryClaim(WorldObject target, AICharacter by, float ttl = CLAIM_TTL)
        {
            if (target == null || by == null)
                return false;

            AICharacter holder = HolderOf(target);
            if (holder != null && holder != by)
                return false;

            if (!_claims.TryGetValue(target, out Claim claim))
            {
                claim = new Claim();
                _claims.Add(target, claim);
            }

            claim.Holder = by;
            claim.Until = Clock + Mathf.Max(0f, ttl);
            return true;
        }

        /// <summary>The character's claim lasts ttl seconds more (its chain still uses the object); false — it holds none.</summary>
        public bool Renew(WorldObject target, AICharacter by, float ttl = CLAIM_TTL)
        {
            if (target == null || by == null || HolderOf(target) != by)
                return false;

            _claims[target].Until = Clock + Mathf.Max(0f, ttl);
            return true;
        }

        /// <summary>Who holds the claim on the object now; null — nobody (none made, run out, or its holder is gone).</summary>
        public AICharacter HolderOf(WorldObject target)
        {
            if (target is null || !_claims.TryGetValue(target, out Claim claim))
                return null;

            return claim.Holder != null && Clock < claim.Until ? claim.Holder : null;
        }

        /// <summary>Somebody other than the character holds the claim on the object now.</summary>
        public bool IsClaimedByAnother(WorldObject target, AICharacter by)
        {
            AICharacter holder = HolderOf(target);
            return holder != null && holder != by;
        }

        /// <summary>The character lets go of its claim on the object.</summary>
        public void Release(WorldObject target, AICharacter by)
        {
            if (target is not null && _claims.TryGetValue(target, out Claim claim) && claim.Holder == by)
                _claims.Remove(target);
        }

        /// <summary>Every claim of the character goes (its decision left, it died).</summary>
        public void ReleaseAll(AICharacter by)
        {
            if (by is null)
                return;

            foreach (KeyValuePair<WorldObject, Claim> pair in _claims)
            {
                if (pair.Value.Holder == by)
                    _released.Add(pair.Key);
            }

            for (int i = 0; i < _released.Count; i++)
                _claims.Remove(_released[i]);
            _released.Clear();
        }

        // ---------- the unreachable journal ----------

        /// <summary>The character found no way to the object: it is not offered to it for this many seconds of the world.</summary>
        public void ReportUnreachable(AICharacter by, WorldObject target, float seconds = UNREACHABLE_FOR)
        {
            if (by == null || target == null)
                return;

            if (!_unreachable.TryGetValue(by, out List<Unreachable> journal))
            {
                journal = new List<Unreachable>();
                _unreachable.Add(by, journal);
            }

            // Old entries go when a new one comes (the journal never grows past what is still unreachable)
            for (int i = journal.Count - 1; i >= 0; i--)
            {
                if (journal[i].Object == null || journal[i].Object == target || Clock >= journal[i].Until)
                    journal.RemoveAt(i);
            }

            journal.Add(new Unreachable { Object = target, Until = Clock + Mathf.Max(0f, seconds) });
            Release(target, by);
        }

        /// <summary>The character found no way to the object a short while ago (its journal's entry has not run out).</summary>
        public bool IsUnreachableFor(AICharacter by, WorldObject target)
        {
            if (by is null || target is null || !_unreachable.TryGetValue(by, out List<Unreachable> journal))
                return false;

            for (int i = 0; i < journal.Count; i++)
            {
                if (journal[i].Object == target && Clock < journal[i].Until)
                    return true;
            }

            return false;
        }
    }
}
