using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A link field of a block that holds a value the block gives out (step 91): drawn as an output data pin of the block
    /// (its own value node, never drawn apart) — readers wire it into their value pins like any value node.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ValueOutputAttribute : Attribute
    {
    }

    /// <summary>A value a block gives out through an output data pin: the block it belongs to (null — never run yet).</summary>
    public interface IBlockOutput
    {
        AgentTask Block { get; }
    }

    /// <summary>A block with output data pins: its outputs are told their block once the graph's links are set.</summary>
    public interface IHasOutputs
    {
        void BindOutputs();
    }

    /// <summary>
    /// A component of an object a Get or an Is reads through its As pin (step 91): the type it is of and the component
    /// itself for the agent asking. The Component output of Try Get Component.
    /// </summary>
    [Serializable]
    public abstract class ComponentValue : AgentValue
    {
        public override ValueKind Kind => ValueKind.Object;

        /// <summary>The component's type; null — none picked, or no loaded code has it.</summary>
        public abstract Type Type { get; }

        /// <summary>The type in words ("Rigidbody").</summary>
        public string TypeWords => Type != null ? AIReadables.Words(Type) : AIReadables.Words(TypeName);

        /// <summary>The type's full name as the graph keeps it.</summary>
        public abstract string TypeName { get; }

        /// <summary>The variable of the object it was looked for on ("Target"); empty — none wired.</summary>
        public abstract string ObjectVariable { get; }

        /// <summary>The component for this agent; false — no data, or the object has none of the type.</summary>
        public abstract bool TryGetComponent(AgentContext context, out Component component);

        /// <summary>The object has a component of the type: true / false; null — no data (nothing looked at yet, no object).</summary>
        public abstract bool? IsOne(AgentContext context);

        /// <summary>Why there is no component now, in words (asked when there is none).</summary>
        public abstract string WhyNone(AgentContext context);

        public override bool TryGetObject(AgentContext context, out Transform value)
        {
            value = TryGetComponent(context, out Component component) ? component.transform : null;
            return value != null;
        }

        /// <summary>In words: "Target's Rigidbody".</summary>
        public string Phrase => $"{CharacterNodes.NameOf(ObjectVariable)}'s {TypeWords}";
    }

    /// <summary>
    /// Try Get Component (step 91; Unity's TryGetComponent, Unreal's Cast To): the flow comes in, the block looks at the
    /// object wired into Object for a component of the type Type (on it or above it — a collider child finds its
    /// character's) and goes on at once. It gives out two values by its output data pins: Component — the component found
    /// (wire it into the As pin of a Get: any public number or vector of it — Rigidbody's mass, Transform's position; or
    /// of an Is: any flag of it), and Found — whether one was found (a flag: into a Compare, a Debug Log). What it gives is
    /// what it found the last time it ran for this agent: before its first run — no data. Any component of the game or of
    /// Unity (AIReadables). The component is looked up anew only when the object changes (handbook §8.2).
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Data/Try Get Component", "Task", "aikit.task.try-get-component", Success = "Then", Simple = true,
        NeverFails = true, Order = 1)]
    public sealed class TryGetComponentTask : AgentTask, IHasOutputs
    {
        /// <summary>The Transform variable of the object looked at (usually Target) — a pin, by a wire.</summary>
        [VariableName(VariableType.Transform)] public string Object;

        /// <summary>The type of component looked for: any component of the game or of Unity.</summary>
        [TypeName(typeof(Component), AnyComponent = true)] public string Type = "";

        /// <summary>The component found (output pin): wired into the As pin of a Get or an Is.</summary>
        [NonSerialized] [ValueOutput] public ComponentOutput Component;

        /// <summary>Whether a component was found (output pin): a flag.</summary>
        [NonSerialized] [ValueOutput] public FoundOutput Found;

        [NonSerialized] private BlackboardVariable<Transform> _object;

        // Per agent: what it found the last time it ran
        internal sealed class Result
        {
            public bool Ran;
            public Transform Object;
            public System.Type Type;
            public Component Component;
        }

        public TryGetComponentTask()
        {
            Component = new ComponentOutput();
            Found = new FoundOutput();
            BindOutputs();
        }

        public TryGetComponentTask(string wiredObject, System.Type type) : this()
        {
            Object = wiredObject;
            Type = type != null ? type.FullName : "";
        }

        public override bool IsInstant => true;

        /// <summary>The type named by Type; null — none picked, or no loaded code has it.</summary>
        public System.Type ComponentType => AIReadables.TypeNamed(Type);

        /// <summary>The outputs know their block (made anew, or read from an asset: the links are set after the block is made).</summary>
        public void BindOutputs()
        {
            if (Component != null)
                Component.Of = this;
            if (Found != null)
                Found.Of = this;
        }

        internal Result ResultOf(AgentContext context) => Record<Result>(context);

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            BindOutputs();
            if (context == null)
                return TaskStatus.Success;

            Result result = ResultOf(context);
            System.Type type = ComponentType;
            Transform subject = string.IsNullOrEmpty(Object) ? null : _object.Get(context.Blackboard, Object);
            if (!result.Ran || result.Object != subject || result.Type != type || result.Component == null && subject != null)
            {
                result.Object = subject;
                result.Type = type;
                result.Component = subject != null && type != null && AIReadables.IsComponentType(type)
                    ? subject.GetComponentInParent(type, true)
                    : null;
            }

            result.Ran = true;
            return TaskStatus.Success;
        }
    }

    /// <summary>The Component output of Try Get Component: the component it found for the agent the last time it ran.</summary>
    [Serializable]
    [AIKitNode("Graph/Data/Try Get Component: Component", "Value", "aikit.output.component")]
    public sealed class ComponentOutput : ComponentValue, IBlockOutput
    {
        [NonSerialized] internal TryGetComponentTask Of;

        public AgentTask Block => Of;

        public override System.Type Type => Of?.ComponentType;

        public override string TypeName => Of?.Type ?? "";

        public override string ObjectVariable => Of?.Object ?? "";

        public override bool TryGetComponent(AgentContext context, out Component component)
        {
            component = null;
            if (Of == null)
                return false;

            TryGetComponentTask.Result result = Of.ResultOf(context);
            component = result.Ran ? result.Component : null;
            return component != null;                   // a destroyed component is null too
        }

        public override bool? IsOne(AgentContext context)
        {
            if (Of == null)
                return null;

            TryGetComponentTask.Result result = Of.ResultOf(context);
            if (!result.Ran || result.Object == null || result.Type == null)
                return null;

            return result.Component != null;
        }

        public override string WhyNone(AgentContext context)
        {
            if (Of == null)
                return "Try Get Component has not run";
            if (string.IsNullOrEmpty(Of.Type))
                return "Try Get Component has no type";
            if (Of.ComponentType == null)
                return $"no type {Of.Type} in the project";
            if (string.IsNullOrEmpty(Of.Object))
                return "nothing wired into Try Get Component's Object";

            TryGetComponentTask.Result result = Of.ResultOf(context);
            if (!result.Ran)
                return "Try Get Component has not run yet";
            if (result.Object == null)
                return $"{CharacterNodes.NameOf(Of.Object)} held nothing";
            return result.Component == null ? $"{CharacterNodes.NameOf(Of.Object)} has no {TypeWords}" : null;
        }
    }

    /// <summary>
    /// The Found output of Try Get Component: a flag — a component was found the last time it ran for the agent (no data
    /// before its first run).
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Data/Try Get Component: Found", "Value", "aikit.output.found")]
    public sealed class FoundOutput : AgentValue, IBlockOutput
    {
        [NonSerialized] internal TryGetComponentTask Of;

        public AgentTask Block => Of;

        public override ValueKind Kind => ValueKind.Flag;

        public override bool TryGetFlag(AgentContext context, out bool value)
        {
            value = false;
            if (Of == null)
                return false;

            TryGetComponentTask.Result result = Of.ResultOf(context);
            if (!result.Ran)
                return false;

            value = result.Component != null;
            return true;
        }

        /// <summary>In words: "Rigidbody found".</summary>
        public string Phrase => Of != null ? $"{AIReadables.Words(Of.Type)} found" : "found";
    }
}
