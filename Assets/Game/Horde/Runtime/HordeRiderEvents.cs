using System;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Horde
{
    /// <summary>Where on the tank a rider holds on (one rider per point).</summary>
    public enum HordeAttachPoint : byte
    {
        FrontLeft = 0,
        FrontRight = 1,
        Left = 2,
        Right = 3,
        Rear = 4,
        /// <summary>On the turret's roof: turns with the turret.</summary>
        Turret = 5
    }

    /// <summary>A rider as the animation code sees it (a copy, valid for the call).</summary>
    public struct HordeRiderInfo
    {
        /// <summary>Horde slot and its generation (the combat target handle of the same monster).</summary>
        public int Slot;
        public int Version;
        /// <summary>Row of the horde's kind table (0 the crawler).</summary>
        public byte Kind;
        public HordeRiderState State;
        public HordeRiderState Previous;
        public HordeAttachPoint Point;
        /// <summary>Feet, world, and yaw (radians, 0 faces +Z) this frame.</summary>
        public float3 Feet;
        public float Heading;
        /// <summary>Seconds in <see cref="State"/>.</summary>
        public float StateTime;
        /// <summary>1 a firm hold, 0 it lets go.</summary>
        public float Grip;
        /// <summary>Why it was shaken off (with <see cref="HordeRiderState.ShakenOff"/>).</summary>
        public HordeShakeCause Cause;
        /// <summary>Damage of the strike (with <see cref="HordeRiderEvents.Struck"/>).</summary>
        public float Damage;
    }

    /// <summary>
    /// What one rider is drawn with this frame: feet, yaw and the VAT clip with the time into it. The rider system fills
    /// it with its placeholder (see <c>Docs/Game/HordeRiders.md</c>); an <see cref="IHordeRiderAnimator"/> may change any of it.
    /// </summary>
    public struct HordeRiderPose
    {
        public float3 Feet;
        public float Heading;
        public HordeClip Clip;
        public float ClipTime;
    }

    /// <summary>
    /// Hook for the riders' own animations: called once per rider per frame, before it is drawn. Leave the pose as it
    /// is to keep the placeholder. Main thread; must not allocate.
    /// </summary>
    public interface IHordeRiderAnimator
    {
        void Pose(in HordeRiderInfo rider, ref HordeRiderPose pose);
    }

    /// <summary>
    /// The riders' events for animation, sound and effects (main thread, no allocations: the argument is a struct).
    /// <see cref="StateChanged"/>: Leaping → Clinging → Tearing, → ShakenOff (from any of them) → None when it lands;
    /// None also when the tank or the arena goes. <see cref="Struck"/>: every strike while tearing.
    /// <see cref="Died"/>: killed on the tank (its corpse lies by the hull, the goo flies from where it held on).
    /// </summary>
    public static class HordeRiderEvents
    {
        public static event Action<HordeRiderInfo> StateChanged;
        public static event Action<HordeRiderInfo> Struck;
        public static event Action<HordeRiderInfo> Died;

        /// <summary>The riders' animator (null: the placeholder clips).</summary>
        public static IHordeRiderAnimator Animator { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            StateChanged = null;
            Struck = null;
            Died = null;
            Animator = null;
        }

        public static void RaiseStateChanged(in HordeRiderInfo rider) => StateChanged?.Invoke(rider);

        public static void RaiseStruck(in HordeRiderInfo rider) => Struck?.Invoke(rider);

        public static void RaiseDied(in HordeRiderInfo rider) => Died?.Invoke(rider);
    }
}
