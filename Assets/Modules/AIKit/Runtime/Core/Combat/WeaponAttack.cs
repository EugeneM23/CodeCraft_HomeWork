using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// One character's attack with the weapon in its hand, from the command to the end of the move (AttackRunner
    /// carries it out). The weapon asset is shared data and keeps no state (handbook C.5) — what one attack needs
    /// between the steps lives here, one per character and node.
    /// </summary>
    public sealed class WeaponAttack
    {
        /// <summary>The weapon the move is made with (the one in the hand at the command).</summary>
        public WeaponAsset Weapon;

        /// <summary>The move the attack is made with.</summary>
        public WeaponMove Move;

        /// <summary>Whom it is aimed at (the node sets it every step: the variable may change).</summary>
        public Transform Target;

        /// <summary>Where the swing started: the reach is counted from here (a blow that steps forward with its clip).</summary>
        public Vector3 From;

        /// <summary>Seconds since the swing started.</summary>
        public float Time;

        /// <summary>The attack can be made from here: the target was in reach when it was commanded.</summary>
        public bool Started;

        /// <summary>The swing is on (a weapon that asks for a place first starts it once it has one).</summary>
        public bool Swinging;

        /// <summary>The blow has been delivered (it may have missed).</summary>
        public bool Struck;

        /// <summary>The blow was delivered on a target in reach.</summary>
        public bool Landed;

        /// <summary>The body shows the move's own action (not the plain swing).</summary>
        public bool ShowsAction;

        /// <summary>
        /// The presentation tells the blow's moment for this swing (ActionState.WillTell … HIT): the blow lands when it is
        /// told, not after the move's wind-up (handbook §20.3).
        /// </summary>
        public bool ByMoment;

        /// <summary>The serial of the swing's request (ActionState.Serial), the one its moment is told for.</summary>
        public int Request;

        /// <summary>The node may turn the body to the target this step (it holds the facing).</summary>
        public bool MayFace;

        /// <summary>Seconds the weapon has been asking for a place to attack from.</summary>
        public float Asked;

        /// <summary>The group of places the weapon holds one of (or asks for); null — none.</summary>
        public Engagement Place;

        /// <summary>The pool of that place.</summary>
        public string Pool;

        /// <summary>A fresh attack with this move at this target.</summary>
        public void Reset(WeaponAsset weapon, WeaponMove move, Transform target, Vector3 from)
        {
            Weapon = weapon;
            Move = move;
            Target = target;
            From = from;
            Time = 0f;
            Started = false;
            Swinging = false;
            Struck = false;
            Landed = false;
            ShowsAction = false;
            ByMoment = false;
            Request = 0;
            MayFace = false;
            Asked = 0f;
            Place = null;
            Pool = null;
        }

        /// <summary>The move's whole time: the swing and the rest after it.</summary>
        public float Length => Move != null ? Move.WindUp + Move.Recovery : 0f;
    }
}
