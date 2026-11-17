using System;
using System.Text;
using UnityEngine;

namespace AIKit
{
    /// <summary>Whether an Is asks that the character is in the state, or that it is not (no data stays no data).</summary>
    public enum IsCheck
    {
        Is,
        IsNot
    }

    /// <summary>Whom a node about a character asks (Is, Get): the agent itself, or the object wired into its Object pin.</summary>
    public enum Whose
    {
        /// <summary>The agent itself.</summary>
        Me,

        /// <summary>The object of the variable wired into the node's Object pin (the Brain's target, an ally…).</summary>
        WiredObject
    }

    /// <summary>
    /// Is (step 90, C13): is a character in a state — moving, running, winding up a blow, blocking, dead, in combat… —
    /// as it says itself (ICharacterStatus: the AI character's own status, the game's on the player). Of — whom: Me, or
    /// the Wired Object (a variable wired into the Object pin; nothing wired — a problem of the graph, and no data:
    /// never a silent "me", spec 10.10). Wire it into a Branch ("Attack → Branch (Is Target Blocking) → Heavy": handbook
    /// §17 p.2 — an exceptional move is a condition) or a decision's Condition pin. A character that does not say — no
    /// data (handbook §4.3: not "no"), and the verdict says "Target does not say whether it is swimming". Reading another
    /// character's status is perception — its own say about itself — not one agent driving another (invariant 1). The
    /// status of an object is looked up once per object, not every think.
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Data/Is", "Condition", "aikit.condition.is", Simple = true)]
    public sealed class IsCondition : AgentCondition, IExplainedCondition, IReadsMembers
    {
        /// <summary>Whom it asks: the agent itself, or the object wired into Object.</summary>
        public Whose Of = Whose.Me;

        /// <summary>The Transform variable of the object asked about (Of: Wired Object) — a pin, by a wire.</summary>
        [VariableName(VariableType.Transform)] public string Object;

        /// <summary>Is — the answer as it is; Is Not — the opposite (yes and no swap, no data stays no data).</summary>
        public IsCheck Is = IsCheck.Is;

        /// <summary>The state asked about.</summary>
        public CharacterState State = CharacterState.Moving;

        /// <summary>
        /// The Component of a Try Get Component wired in (step 91): Is asks about it instead of Of / Object / State —
        /// with no Member, whether the object has one ("Target has a Rigidbody"); with a Member, that public flag of the
        /// component. Unplugged — Is asks as before.
        /// </summary>
        [NonSerialized] public ComponentValue As;

        /// <summary>The flag of the component it asks about; empty — whether the object has one.</summary>
        [MemberName] public string Member = "";

        [NonSerialized] private BlackboardVariable<Transform> _object;

        public System.Type MemberSource => As?.Type;

        public bool ReadsFlags => true;

        public bool Shows(string field) => As != null
            ? field != nameof(Of) && field != nameof(Object) && field != nameof(State)
            : field != nameof(Member);

        public IsCondition()
        {
        }

        public IsCondition(CharacterState state, string wiredObject = null)
        {
            State = state;
            if (!string.IsNullOrEmpty(wiredObject))
            {
                Of = Whose.WiredObject;
                Object = wiredObject;
            }
        }

        protected override bool Evaluate(AgentContext context) => Answer(context) == Truth.Yes;

        // Per agent: the object asked about last and what it says about itself (looked up when the object changes)
        private CharacterNodes.Seen Seen(AgentContext context) => context != null ? Record<CharacterNodes.Seen>(context) : null;

        protected override Truth Answer(AgentContext context)
        {
            Truth answer = Raw(context);
            if (Is == IsCheck.Is || answer == Truth.NoData)
                return answer;

            return answer == Truth.Yes ? Truth.No : Truth.Yes;
        }

        private Truth Raw(AgentContext context)
        {
            if (As != null)
                return CastAnswer(context);

            ICharacterStatus status = CharacterNodes.StatusOf(Seen(context), context, Of, Object, ref _object);
            if (status == null || !status.TryGetState(State, out bool value))
                return Truth.NoData;

            return value ? Truth.Yes : Truth.No;
        }

        // With a component wired in: has the object one, or — with a Member — is that flag of the type on
        private Truth CastAnswer(AgentContext context)
        {
            bool? isOne = As.IsOne(context);
            if (isOne == null)
                return Truth.NoData;

            if (string.IsNullOrEmpty(Member))
                return isOne.Value ? Truth.Yes : Truth.No;

            ReadableMember member = AIReadables.Find(As.Type, Member);
            if (member == null || !isOne.Value || !As.TryGetComponent(context, out Component component) ||
                !member.TryFlag(component, out bool value))
                return Truth.NoData;

            return value ? Truth.Yes : Truth.No;
        }

        public string Phrase(AgentContext context)
        {
            bool not = Is == IsCheck.IsNot;
            if (As != null)
                return string.IsNullOrEmpty(Member)
                    ? $"{CharacterNodes.NameOf(As.ObjectVariable)} has {(not ? "no" : "a")} {As.TypeWords}"
                    : $"{As.Phrase}: {(not ? "not " : "")}{CharacterNodes.MemberWords(As, Member)}";

            string state = (not ? "not " : "") + CharacterNodes.Words(State);
            return Of == Whose.Me ? $"I am {state}" : $"{CharacterNodes.NameOf(Object)} is {state}";
        }

        public string WhyNoData(AgentContext context)
        {
            if (As != null)
                return CharacterNodes.WhyNoCast(context, As, Member, flags: true);

            string unknown = CharacterNodes.WhyNoStatus(Seen(context), context, Of, Object, ref _object);
            return unknown ?? $"{(Of == Whose.Me ? "I do" : CharacterNodes.NameOf(Object) + " does")} not say whether " +
                   $"{(Of == Whose.Me ? "I am" : "it is")} {CharacterNodes.Words(State)}";
        }
    }

    /// <summary>What the nodes about a character (Is, Get) share: whom they ask and how it is said in words.</summary>
    internal static class CharacterNodes
    {
        public sealed class Seen
        {
            public Transform Object;
            public ICharacterStatus Status;
            public bool Looked;
        }

        /// <summary>
        /// The object a node about a character asks about: the agent itself (Me), or the object of the variable wired in;
        /// null — nothing wired, or the variable holds nothing.
        /// </summary>
        public static Transform SubjectOf(AgentContext context, Whose of, string variable,
            ref BlackboardVariable<Transform> handle)
        {
            if (context == null)
                return null;

            if (of == Whose.Me)
                return context.Agent != null ? context.Agent.EntityRoot ?? context.Agent.transform : context.Self;

            return string.IsNullOrEmpty(variable) ? null : handle.Get(context.Blackboard, variable);
        }

        /// <summary>
        /// What the object asked about says about itself (its ICharacterStatus — the AI character's own, the game's on the
        /// player), looked up once per object in the node's memory; null — nothing to ask, or it says nothing.
        /// </summary>
        public static ICharacterStatus StatusOf(Seen seen, AgentContext context, Whose of, string variable,
            ref BlackboardVariable<Transform> handle)
        {
            if (context == null)
                return null;

            if (of == Whose.Me && context.Agent != null)
                return context.Agent;                   // the character's own status: no look-up

            Transform subject = SubjectOf(context, of, variable, ref handle);
            seen ??= new Seen();
            if (!seen.Looked || seen.Object != subject)
            {
                seen.Looked = true;
                seen.Object = subject;
                seen.Status = subject != null ? CharacterStatuses.Of(subject) : null;
            }

            return seen.Status is UnityEngine.Object gone && gone == null ? null : seen.Status;
        }

        /// <summary>Why a node about a character has nobody to ask now; null — it has.</summary>
        public static string WhyNoStatus(Seen seen, AgentContext context, Whose of, string variable,
            ref BlackboardVariable<Transform> handle)
        {
            if (context == null)
                return "no agent";

            if (of == Whose.WiredObject && string.IsNullOrEmpty(variable))
                return "nothing wired into Object";

            Transform subject = SubjectOf(context, of, variable, ref handle);
            if (subject == null)
                return of == Whose.Me ? "no body" : $"{variable} holds nothing";

            return StatusOf(seen, context, of, variable, ref handle) == null ? $"{subject.name} says nothing about itself" : null;
        }

        /// <summary>A member of a cast type in words: its label ("Stamina"); none picked — "?".</summary>
        public static string MemberWords(ComponentValue cast, string member)
        {
            if (string.IsNullOrEmpty(member))
                return "?";

            return AIReadables.Find(cast?.Type, member)?.Label ?? AIReadables.Spaced(member);
        }

        /// <summary>Why a Get or an Is reading a component has no data now (never null: it is asked when there is none).</summary>
        public static string WhyNoCast(AgentContext context, ComponentValue cast, string member, bool flags)
        {
            string none = cast.WhyNone(context);
            if (none != null)
                return none;
            if (string.IsNullOrEmpty(member))
                return flags ? "no data" : "no member picked";

            ReadableMember read = AIReadables.Find(cast.Type, member);
            if (read == null)
                return $"{cast.TypeWords} has no {AIReadables.Spaced(member)} a graph may read";
            return read.IsFlag != flags ? $"{read.Label} is {(read.IsFlag ? "a flag" : "a number")}" : "no data";
        }

        /// <summary>The name of the wired variable as the canvas shows it ("Target"); nothing wired — "the object".</summary>
        public static string NameOf(string variable) => string.IsNullOrEmpty(variable) ? "the object" : variable;

        /// <summary>An enum value in plain words: WindingUp — "winding up", InCombat — "in combat".</summary>
        public static string Words<T>(T value) where T : Enum
        {
            string name = value.ToString();
            var words = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0)
                    words.Append(' ');
                words.Append(char.ToLowerInvariant(c));
            }

            return words.ToString();
        }
    }
}
