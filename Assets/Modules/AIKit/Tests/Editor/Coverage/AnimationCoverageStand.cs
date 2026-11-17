using System.Collections.Generic;
using AIKit.Mecanim;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The animation recheck's stand (step 90 recheck, lane T4): an AI Character with a body the test sets, a model below
    /// it with a real Animator of a controller built in code, and an AI Animator on the model that reads the character.
    /// Edit Mode runs no Update: a frame is FrameTick then the Animator's own Update, by hand.
    /// </summary>
    internal sealed class AnimationCoverageStand
    {
        private readonly List<Object> _made = new();

        public GameObject Owner { get; private set; }

        public GameObject Model { get; private set; }

        public AICharacter Character { get; private set; }

        public Animator Animator { get; private set; }

        public AIAnimator AI { get; private set; }

        public RecordingBody Body { get; private set; }

        public ActionState Actions => Character.Context.Actions;

        /// <summary>Every change of the action shown, as AI Animator raised it ("-" — none).</summary>
        public readonly List<string> Changes = new();

        public void Destroy()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        public T Keep<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }

        /// <summary>A character on this body, its model playing this controller, shown by an AI Animator (Smoothing 0).</summary>
        public AnimationCoverageStand Make(RuntimeAnimatorController controller, RecordingBody body = null, bool attach = true)
        {
            Owner = Keep(new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave });
            Character = Owner.AddComponent<AICharacter>();
            Character.Simulation = AICharacter.SimulationMode.Manual;
            Body = body ?? new RecordingBody();
            Character.Body = Body;
            Character.Initialize();
            Model = new GameObject("Body");
            Model.transform.SetParent(Owner.transform, false);
            Animator = Model.AddComponent<Animator>();
            Animator.runtimeAnimatorController = controller;
            Animator.Rebind();
            Animator.Update(0f);
            AI = Model.AddComponent<AIAnimator>();
            AI.Tuning.Smoothing = 0f;
            AI.ActionChanged += action => Changes.Add(action ?? "-");
            if (attach)
                AI.Attach();
            return this;
        }

        /// <summary>One frame: the AI Animator reads the character, then the Animator steps.</summary>
        public void Frame(float dt)
        {
            AI.FrameTick(dt);
            Animator.Update(dt);
        }

        public void Frames(int count, float dt)
        {
            for (int i = 0; i < count; i++)
                Frame(dt);
        }

        public bool IsIn(string state) => Animator.GetCurrentAnimatorStateInfo(0).IsName(state);

        /// <summary>The animator is in this state or blending into it.</summary>
        public bool IsInOrEntering(string state) =>
            IsIn(state) || Animator.IsInTransition(0) && Animator.GetNextAnimatorStateInfo(0).IsName(state);

        // ---------- controllers built in code ----------

        /// <summary>A controller with the AI Animator's parameters (optionally no Attack trigger) and an "Idle" state first.</summary>
        public AnimatorController Controller(bool attack = true)
        {
            var controller = Keep(new AnimatorController());
            controller.AddLayer("Base");
            foreach (string parameter in new[] { "MoveRate", "MoveX", "MoveZ", "Stride", "Backing", "Speed", "Climb" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            foreach (string parameter in new[] { "Combat", "Braking" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter("Weapon", AnimatorControllerParameterType.Int);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Int);
            if (attack)
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.layers[0].stateMachine.AddState("Idle");
            return controller;
        }

        /// <summary>A state of the base layer (tagged Action, or not), with a clip of this many seconds (0 — no motion).</summary>
        public AnimatorState State(AnimatorController controller, string name, bool action = true, float seconds = 0f)
        {
            AnimatorState state = controller.layers[0].stateMachine.AddState(name);
            if (action)
                state.tag = "Action";
            if (seconds > 0f)
                state.motion = Clip(name, seconds);
            return state;
        }

        /// <summary>The state goes back to the default one when its clip is over (exit time 1, no blend).</summary>
        public static void ExitsWhenOver(AnimatorState state)
        {
            AnimatorStateTransition exit = state.AddExitTransition();
            exit.hasExitTime = true;
            exit.exitTime = 1f;
            exit.duration = 0f;
        }

        /// <summary>A clip this long that moves nothing that matters (a still curve on the root's x).</summary>
        public AnimationClip Clip(string name, float seconds)
        {
            var clip = Keep(new AnimationClip { name = name });
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.x"),
                AnimationCurve.Constant(0f, seconds, 1f));
            return clip;
        }

        /// <summary>A generic climbing clip: the root rises Height and goes Reach forward over Climb seconds, then stands.</summary>
        public AnimationClip ClimbClip(string name, float height, float reach, float climb, float length)
        {
            var clip = Keep(new AnimationClip { name = name });
            Line(clip, "m_LocalPosition.y", height, climb, length);
            Line(clip, "m_LocalPosition.z", reach, climb, length);
            return clip;
        }

        // Three keys: 0 → value at climb (straight), flat to length
        private static void Line(AnimationClip clip, string property, float value, float climb, float length)
        {
            float slope = value / climb;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f, slope, slope),
                new Keyframe(climb, value, slope, 0f),
                new Keyframe(length, value, 0f, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), property), curve);
        }
    }

    /// <summary>
    /// A body as the picture sees it, recording what the AI Animator hands it: its climb, its jump, whether the clips carry
    /// it, the walk it is asked for, its measured clips — and each root motion it got (count, dt, in an action).
    /// </summary>
    internal sealed class RecordingBody : AgentBody
    {
        public int Clip;
        public JumpPhase Phase;
        public bool TakesRootMotion;
        public bool WalksByClips;
        public Vector3 Asked;
        public bool Climbs;
        public Vector3 Returns;
        public MeasuredTraversal Measured;

        public int Moves { get; private set; }

        public float LastDt { get; private set; } = float.NaN;

        public bool LastInAction { get; private set; }

        public override IAgentActuator Actuator => null;

        public override bool UsesRootMotion => TakesRootMotion;

        public override bool WalksByAnimation => WalksByClips;

        public override Vector3 ShownVelocity => Asked;

        public override int ClimbDirection => Clip;

        public override JumpPhase Jump => Phase;

        public override bool Traverses => Climbs;

        public override MeasuredTraversal Traversal
        {
            get => Measured ??= new MeasuredTraversal();
            set => Measured = value;
        }

        public override void Attach(Transform owner)
        {
        }

        public override Vector3 MoveByAnimation(Vector3 deltaPosition, Quaternion deltaRotation, bool inAction, float dt)
        {
            Moves++;
            LastDt = dt;
            LastInAction = inAction;
            return Returns;
        }
    }
}
