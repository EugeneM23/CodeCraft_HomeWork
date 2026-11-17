using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// What a Get node reads: a number of the character's status (its own say — CharacterStat, the same order and
    /// meaning), or a relation of the object to the agent that the module works out itself.
    /// </summary>
    public enum GetParameter
    {
        Health = (int)CharacterStat.Health,
        MaxHealth = (int)CharacterStat.MaxHealth,
        HealthPercent = (int)CharacterStat.HealthPercent,

        /// <summary>Metres per second it moves now (measured).</summary>
        Speed = (int)CharacterStat.Speed,

        /// <summary>Its full speed.</summary>
        RunSpeed = (int)CharacterStat.RunSpeed,

        TurnSpeed = (int)CharacterStat.TurnSpeed,
        Acceleration = (int)CharacterStat.Acceleration,
        Height = (int)CharacterStat.Height,
        Radius = (int)CharacterStat.Radius,

        /// <summary>Moves left of the weapon in its hand (no data when there is no end).</summary>
        UsesLeft = (int)CharacterStat.UsesLeft,

        /// <summary>How far the object is from me, metres.</summary>
        Distance = 100,

        /// <summary>Degrees between where I face and the way to the object: 0 — straight ahead, 180 — right behind me.</summary>
        AngleFromMyFacing = 101,

        /// <summary>Degrees between where the object faces and the way from it to me: 0 — it faces me squarely.</summary>
        FacesMe = 102
    }

    /// <summary>
    /// Get (step 90, C11 / C12): a number about a character, read live where it lives — its health, how fast it moves,
    /// its run speed, the uses left of its weapon (its status, ICharacterStatus: the AI character's own, the game's on the
    /// player) — or a relation of the object to the agent the module works out (Distance, Angle From My Facing, Faces Me).
    /// A pure getter, no flow pins: its output goes into Compare (A / B), Set / Add… (a snapshot into a variable) or Debug
    /// Log. Of — whom: Me, or the Wired Object (the variable wired into the Object pin; nothing wired — a problem of the
    /// graph and no data, never a silent "me", spec 10.10). One owner of every value (handbook §8.2: no copies of
    /// settings on the blackboard; §0.1: a node reads the module's interface, once per think, visibly): the answer is
    /// kept for the think, the object's status is looked up once per object. No data — the object does not say, or
    /// nothing to ask — is an answer of its own (TryGetNumber false), never a zero (handbook §4.3, §38 №19).
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Data/Get", "Value", "aikit.value.get", Simple = true)]
    public sealed class GetValue : AgentValue, IReadsMembers
    {
        /// <summary>Whom it asks: the agent itself, or the object wired into Object.</summary>
        public Whose Of = Whose.Me;

        /// <summary>The Transform variable of the object asked about (Of: Wired Object) — a pin, by a wire.</summary>
        [VariableName(VariableType.Transform)] public string Object;

        /// <summary>The number it reads.</summary>
        public GetParameter Parameter = GetParameter.Health;

        /// <summary>
        /// The Component of a Try Get Component wired in (step 91): Get reads a Member of it — any public number or
        /// vector axis of the type (AIReadables) — instead of Of / Object / Parameter. Unplugged — Get reads as before.
        /// </summary>
        [NonSerialized] public ComponentValue As;

        /// <summary>The number of the component it reads (with a component wired into As).</summary>
        [MemberName] public string Member = "";

        [NonSerialized] private BlackboardVariable<Transform> _object;

        // Per agent: the answer of the think it was asked in (asked again in that think — the same answer)
        private sealed class Answered
        {
            public int Think;
            public bool Has;
            public float Value;
            public readonly CharacterNodes.Seen Seen = new();
        }

        public GetValue()
        {
        }

        public GetValue(GetParameter parameter, string wiredObject = null)
        {
            Parameter = parameter;
            if (!string.IsNullOrEmpty(wiredObject))
            {
                Of = Whose.WiredObject;
                Object = wiredObject;
            }
        }

        public override ValueKind Kind => ValueKind.Number;

        public System.Type MemberSource => As?.Type;

        public bool ReadsFlags => false;

        public bool Shows(string field) => As != null
            ? field != nameof(Of) && field != nameof(Object) && field != nameof(Parameter)
            : field != nameof(Member);

        /// <summary>It reads a relation of the object to the agent (worked out here), not the object's own say.</summary>
        public bool IsRelation => Parameter >= GetParameter.Distance;

        public override bool TryGetNumber(AgentContext context, out float value)
        {
            int think = context?.AnswerStamp ?? 0;
            Answered answered = context != null ? Record<Answered>(context) : null;
            if (answered != null && think != 0 && answered.Think == think)
            {
                value = answered.Value;
                return answered.Has;
            }

            bool has = Read(context, answered?.Seen, out value);
            if (answered != null && think != 0)
            {
                answered.Think = think;
                answered.Has = has;
                answered.Value = value;
            }

            return has;
        }

        private bool Read(AgentContext context, CharacterNodes.Seen seen, out float value)
        {
            value = 0f;
            if (context == null)
                return false;

            if (As != null)
            {
                ReadableMember member = AIReadables.Find(As.Type, Member);
                return member != null && As.TryGetComponent(context, out Component component) &&
                       member.TryNumber(component, out value);
            }

            if (!IsRelation)
            {
                ICharacterStatus status = CharacterNodes.StatusOf(seen, context, Of, Object, ref _object);
                return status != null && status.TryGetStat((CharacterStat)Parameter, out value);
            }

            // A relation to myself says nothing
            if (Of == Whose.Me)
                return false;

            Transform subject = CharacterNodes.SubjectOf(context, Of, Object, ref _object);
            if (subject == null)
                return false;

            Vector3 mine = context.Position;
            Vector3 its = subject.position;
            if (Parameter == GetParameter.Distance)
            {
                value = Vector3.Distance(mine, its);
                return true;
            }

            // Angles in the plane the agent sees in, from where the bodies look (the Senses' rule: a body in 3D over the
            // ground along its Forward, a side-view body in the plane of the view along its facing)
            Vector3 normal = AgentPerception.ViewOf(context, out Vector3 look);
            return Parameter == GetParameter.AngleFromMyFacing
                ? AngleIn(normal, look, its - mine, out value)
                : AngleIn(normal, FacingOf(subject), mine - its, out value);
        }

        // Where the object looks: an AI character's body says it (its facing in side view), anything else its transform
        private static Vector3 FacingOf(Transform subject)
        {
            if (subject.TryGetComponent(out AICharacter character) && character.Context?.Actuator != null)
            {
                AgentBodyInfo body = character.Context.Actuator.Body;
                return AgentPerception.LookOf(body, body.Up.sqrMagnitude > 1e-6f ? body.Up.normalized : Vector3.up);
            }

            return subject.forward;
        }

        // Degrees between two directions in the plane across normal; false — one of them lies along normal (straight up or
        // down over the ground): no way to tell
        private static bool AngleIn(Vector3 normal, Vector3 facing, Vector3 way, out float degrees)
        {
            facing = Vector3.ProjectOnPlane(facing, normal);
            way = Vector3.ProjectOnPlane(way, normal);
            degrees = 0f;
            if (facing.sqrMagnitude < 1e-6f || way.sqrMagnitude < 1e-6f)
                return false;

            degrees = Vector3.Angle(facing, way);
            return true;
        }

        /// <summary>The node in words, as the canvas and a Debug Log say it: "Health of Target", "my Speed".</summary>
        public string Phrase => As != null
            ? $"{CharacterNodes.MemberWords(As, Member)} of {As.Phrase}"
            : Of == Whose.Me
            ? $"my {CharacterNodes.Words(Parameter)}"
            : $"{CharacterNodes.Words(Parameter)} of {CharacterNodes.NameOf(Object)}";
    }
}
