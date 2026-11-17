using System;
using UnityEngine;

namespace AIKit.Mecanim
{
    /// <summary>A name field that is a parameter of the Animator's controller, of this kind (the inspector lists them).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class AnimatorParameterAttribute : PropertyAttribute
    {
        public AnimatorParameterAttribute(AnimatorControllerParameterType type) => Type = type;

        public AnimatorControllerParameterType Type { get; }
    }

    /// <summary>A name field that is a state of the Animator's controller (the inspector lists them).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class AnimatorStateAttribute : PropertyAttribute
    {
    }

    /// <summary>A name field that is a layer of the Animator's controller (the inspector lists them).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class AnimatorLayerAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// The parameters of the controller the AI Animator writes what the character does into. A parameter the controller
    /// does not have is skipped (empty — never written).
    /// </summary>
    [Serializable]
    public sealed class AnimatorParameters
    {
        [Tooltip("Float: how fast the body moves, metres per second")]
        [AnimatorParameter(AnimatorControllerParameterType.Float)]
        public string Speed = "Speed";

        [Tooltip("Float: the real speed divided by Clip Speed — plug it in as the speed multiplier of the walk state, " +
                 "and the feet do not slide")]
        [AnimatorParameter(AnimatorControllerParameterType.Float)]
        public string MoveRate = "MoveRate";

        [Tooltip("Float: where the body moves relative to where it looks, −1…1 sideways — for a blend tree of forward / " +
                 "backward / strafe clips")]
        [AnimatorParameter(AnimatorControllerParameterType.Float)]
        public string MoveX = "MoveX";

        [Tooltip("Float: where the body moves relative to where it looks, −1…1 forward")]
        [AnimatorParameter(AnimatorControllerParameterType.Float)]
        public string MoveZ = "MoveZ";

        [Tooltip("Float 0…1: how long the steps are — for a walk that blends with the stance by it (0 — the stance, 1 — " +
                 "full steps). A slow body takes shorter steps at Slowest Rate instead of slow ones: no slow motion")]
        [AnimatorParameter(AnimatorControllerParameterType.Float)]
        public string Stride = "Stride";

        [Tooltip("Float 0…1: the body walks the backward half of the circle (1) or the forward half (0) — for a pack " +
                 "with two side clips per side. It turns over only once the body clearly goes forward or back, and " +
                 "slides there in a quarter of a second")]
        [AnimatorParameter(AnimatorControllerParameterType.Float)]
        public string Backing = "Backing";

        [Tooltip("Trigger: the plain swing of an attack (a move with no action of its own)")]
        [AnimatorParameter(AnimatorControllerParameterType.Trigger)]
        public string Attack = "Attack";

        [Tooltip("Bool: the character is in combat — its brain has a target (and had one less than Calm After ago). " +
                 "For a controller with a calm and a combat stance: the sword drawn, the guard up")]
        [AnimatorParameter(AnimatorControllerParameterType.Bool)]
        public string Combat = "Combat";

        [Tooltip("Bool: the body is braking — slowing down faster than Braking At. For a controller with stop clips: a " +
                 "body that walks slowly at a steady pace is walking, not stopping")]
        [AnimatorParameter(AnimatorControllerParameterType.Bool)]
        public string Braking = "Braking";

        [Tooltip("Int: which of the character's weapons is in the hand (its place in the Weapons list, 0 — the first)")]
        [AnimatorParameter(AnimatorControllerParameterType.Int)]
        public string Weapon = "Weapon";

        [Tooltip("Float: the body is on a wall — 1 climbing up, −1 climbing down, 0 not climbing")]
        [AnimatorParameter(AnimatorControllerParameterType.Float)]
        public string Climb = "Climb";

        [Tooltip("Int: the body jumps — 1 taking off, 2 in the air, 3 landing, 0 not jumping")]
        [AnimatorParameter(AnimatorControllerParameterType.Int)]
        public string Jump = "Jump";
    }

    /// <summary>The states of the controller the AI Animator puts the animator into, and how it blends.</summary>
    [Serializable]
    public sealed class AnimatorStates
    {
        [Tooltip("The state the animator goes back to when an action is dropped")]
        [AnimatorState]
        public string Locomotion = "Locomotion";

        [Tooltip("The tag of the states of actions: while one of them plays, the action is not over")]
        public string ActionTag = "Action";

        [Tooltip("Seconds of blending into an action and out of it")]
        [Min(0f)]
        public float Blend = 0.15f;

        [Tooltip("The state of the climb up (the base layer): put at once, from its start, when the climb begins — the " +
                 "body follows the climbing clip from its first frame. Its clip is measured for the body's climb")]
        [AnimatorState]
        public string ClimbUp = "Climb Up";

        [Tooltip("The state of the climb down (the climb played back); none — the body jumps down")]
        [AnimatorState]
        public string ClimbDown = "Climb Down";

        [Tooltip("The jump's start: its clip is measured for the take-off (when the feet leave the ground)")]
        [AnimatorState]
        public string JumpStart = "Jump Start";

        [Tooltip("The jump's landing: its clip is measured for the touch-down and the standing up")]
        [AnimatorState]
        public string JumpLand = "Jump Land";

        [Tooltip("The layer the flinch plays on when the character is hurt (an additive or masked layer over the rest); " +
                 "empty — no flinch")]
        [AnimatorLayer]
        public string HurtLayer = "";

        [Tooltip("The state of the flinch on the Hurt Layer, played from its start every time the character is hurt")]
        [AnimatorState]
        public string Hurt = "";
    }

    /// <summary>How the AI Animator turns the body's motion into the picture.</summary>
    [Serializable]
    public sealed class AnimatorTuning
    {
        [Tooltip("The speed the walk clip is made for, metres per second")]
        [Min(0.1f)]
        public float ClipSpeed = 1.6f;

        [Tooltip("With a Stride parameter: the slowest the walk is played (a part of its own pace); slower bodies take " +
                 "shorter steps. Without one the walk is never played slower than half its pace")]
        [Range(0.1f, 1f)]
        public float SlowestRate = 0.8f;

        [Tooltip("Seconds the shown speed takes to catch up with the real one (0 — at once)")]
        [Min(0f)]
        public float Smoothing = 0.1f;

        [Tooltip("Seconds the combat stance stays after the target is gone: a target lost for a moment does not make " +
                 "the character put the sword away and draw it again")]
        [Min(0f)]
        public float CalmAfter = 4f;

        [Tooltip("Metres per second squared: slowing down faster than this is braking (a body's own braking, from its " +
                 "ramp, is well above it; a walk that only wobbles is below it)")]
        [Min(0f)]
        public float BrakingAt = 0.5f;
    }
}
