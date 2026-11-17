using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// One move of a weapon: how far, how hard, how long the swing and the rest after it are. Its name is a slot the
    /// character's graph asks for ("Attack 1", "Heavy") — every weapon of a character has moves of the same names, each
    /// with its own numbers and its own clip (the weapon's Motions put its clips on the animator's states).
    /// </summary>
    [System.Serializable]
    public sealed class WeaponMove
    {
        public string Name = "Attack 1";
        [Tooltip("The action the body shows for it (a state of the character's animator, one for every weapon — the " +
                 "weapon's Motions give it this weapon's clip); empty — its plain swing")]
        public string Action = "";
        [Tooltip("Metres from where the swing starts to the target it still lands on (centre to centre). For a body whose " +
                 "blows step forward with their clips (root motion of actions) the step is part of it")]
        [Min(0f)] public float Reach = 2f;
        public float Damage = 1f;
        [Tooltip("Seconds from the start of the swing until it lands (a shot: until the projectile leaves) — the warning " +
                 "the target gets")]
        [Min(0f)] public float WindUp = 0.4f;
        [Tooltip("Seconds the character stands after it")]
        [Min(0f)] public float Recovery = 0.5f;
        [Tooltip("Only with a clear line to the target (a shot)")]
        public bool NeedsSight;
    }

    /// <summary>Which way a dash goes, relative to the target the character faces.</summary>
    public enum DashDirection
    {
        Back,
        Forward,
        Left,
        Right,
        BackLeft,
        BackRight,
        ForwardLeft,
        ForwardRight,

        /// <summary>Left or right, at random.</summary>
        Aside
    }

    /// <summary>
    /// One dash a character holding the weapon can make (a dodge clip): which way, the action the body shows for it,
    /// and how far and how long — measured from the clip (handbook §20.3), so the body goes as far as the feet do.
    /// </summary>
    [System.Serializable]
    public sealed class WeaponDash
    {
        public DashDirection Direction = DashDirection.Back;
        [Tooltip("The action the body shows for it (a state of the animator)")]
        public string Action = "Dash Back";
        [Tooltip("Metres the clip carries the body")]
        [Min(0f)] public float Distance = 2f;
        [Tooltip("Seconds the clip carries it for")]
        [Min(0.05f)] public float Time = 0.5f;
    }

    /// <summary>
    /// The charge a character with the weapon can make (the Charge node, step 90 — a move, its numbers the weapon's like
    /// its dashes): it gathers its strength for Charge Time turning to the target no faster than Aim Speed (the warning),
    /// rushes Distance metres at Speed along the line it aimed, and stands Recovery seconds (the window to strike its
    /// back) — handbook §19.1. It deals no damage of its own (as before: the rush is a move of the body).
    /// </summary>
    [System.Serializable]
    public sealed class WeaponRush
    {
        [Tooltip("The action the body shows while it gathers its strength (a state of the animator); empty — none")]
        [ActionName] public string ChargeAction = "Charge Up";

        [Tooltip("The action the body shows while it rushes; empty — none")]
        [ActionName] public string RushAction = "Charge";

        [Tooltip("Seconds it gathers its strength before it shoots off (the warning)")]
        [Min(0f)] public float ChargeTime = 2f;

        [Tooltip("Degrees per second it turns to the target while it gathers its strength")]
        [Min(0f)] public float AimSpeed = 90f;

        [Tooltip("Metres it rushes")]
        [Min(0f)] public float Distance = 12f;

        [Tooltip("Metres per second of the rush")]
        [Min(0.1f)] public float Speed = 14f;

        [Tooltip("Seconds it stands after the rush (the window to strike its back)")]
        [Min(0f)] public float Recovery = 2f;
    }

    /// <summary>
    /// What a character attacks with — data only, no logic and no graph of its own: the moves by the slots the
    /// character's graph asks for (handbook §20b.4: each move has its own distance and timing), the clips the character
    /// shows them with (Motions — handbook §20.5: what differs in the picture only is a table of clips, not branches of
    /// the graph or arrows of the animator), the shot if the weapon shoots, the moves it lasts for (a rock: one), and the
    /// numbers of taking it up and putting it away. The character carries every move out (AttackRunner) by these
    /// numbers. Shared by every character that holds it — no state here (handbook C.5): how many moves a character has
    /// left is the character's.
    /// </summary>
    [CreateAssetMenu(menuName = "AI Kit/Weapon", fileName = "Weapon")]
    public class WeaponAsset : ScriptableObject
    {
        [SerializeField] private System.Collections.Generic.List<WeaponMove> _moves = new() { new WeaponMove() };

        [Tooltip("The clips the character shows with this weapon: an override of the character's animator (the same " +
                 "states for every weapon). Empty — the animator's own clips")]
        public AnimatorOverrideController Motions;

        [Tooltip("Moves a weapon taken from the world lasts for (a rock: 1 — it is thrown and gone, the character's own " +
                 "weapon comes back into the hand); 0 — no end. Counted on the character, never here")]
        [Min(0)] public int Uses;

        [Header("Shooting")]
        [Tooltip("On — the weapon shoots: every move lets a projectile go at the end of its wind-up (Shot), and the " +
                 "decisions that need a weapon that shoots (Kite) are taken with it")]
        public bool Shoots;

        public WeaponShot Shot = new();

        [Header("Taking it up and putting it away (the Switch Weapon node)")]
        [Tooltip("The action the body shows while it takes the weapon up (a state of the animator); empty — none")]
        public string DrawAction = "";

        [Tooltip("Seconds into taking it up when the weapon is in the hand (the model moves from the back to the hand)")]
        [Min(0f)] public float DrawAt = 0.3f;

        [Tooltip("Seconds taking it up lasts")]
        [Min(0f)] public float DrawTime = 0.6f;

        [Tooltip("The action the body shows while it puts the weapon away; empty — none")]
        public string SheatheAction = "";

        [Tooltip("Seconds into putting it away when the weapon is on the back")]
        [Min(0f)] public float SheatheAt = 0.3f;

        [Tooltip("Seconds putting it away lasts")]
        [Min(0f)] public float SheatheTime = 0.6f;

        public System.Collections.Generic.IReadOnlyList<WeaponMove> Moves => _moves;

        [Tooltip("The dashes a character with this weapon can make (Dash node): measured from the dodge clips")]
        [SerializeField] private System.Collections.Generic.List<WeaponDash> _dashes = new();

        public System.Collections.Generic.List<WeaponDash> Dashes => _dashes;

        [Header("Charging (the Charge node)")]
        [Tooltip("On — a character with this weapon charges by its Rush; off — the Charge node uses its own numbers")]
        public bool Charges;

        public WeaponRush Rush = new();

        /// <summary>The weapon's charge; null — it has none.</summary>
        public WeaponRush RushOrNone => Charges ? Rush : null;

        /// <summary>The weapon fights from afar: its moves let projectiles go.</summary>
        public bool Ranged => Shoots;

        /// <summary>
        /// The reach of its shortest move — near enough for every move of it to land (step 90: a distance of 0 on a node
        /// means "from the weapon in hand"); 0 — it has no moves.
        /// </summary>
        public float ShortestReach
        {
            get
            {
                float shortest = 0f;
                for (int i = 0; i < _moves.Count; i++)
                {
                    float reach = _moves[i].Reach;
                    if (reach > 0f && (shortest <= 0f || reach < shortest))
                        shortest = reach;
                }

                return shortest;
            }
        }

        /// <summary>The dash that way; null — the weapon has none.</summary>
        public WeaponDash DashTo(DashDirection direction)
        {
            for (int i = 0; i < _dashes.Count; i++)
            {
                if (_dashes[i].Direction == direction)
                    return _dashes[i];
            }

            return null;
        }

        /// <summary>The move of this name; empty name — the first move; null — none.</summary>
        public WeaponMove Find(string move)
        {
            if (_moves.Count == 0)
                return null;
            if (string.IsNullOrEmpty(move))
                return _moves[0];
            for (int i = 0; i < _moves.Count; i++)
            {
                if (_moves[i].Name == move)
                    return _moves[i];
            }
            return null;
        }

        /// <summary>The weapon's moves are these from now on (builders that measure them from the clips).</summary>
        public void ReplaceMoves(params WeaponMove[] moves) =>
            _moves = new System.Collections.Generic.List<WeaponMove>(moves);

        /// <summary>Builds a weapon in code (tests, builders).</summary>
        public static WeaponAsset Create(params WeaponMove[] moves) => Create<WeaponAsset>(moves);

        /// <summary>Builds a weapon in code (tests, builders).</summary>
        public static T Create<T>(params WeaponMove[] moves) where T : WeaponAsset
        {
            var weapon = CreateInstance<T>();
            weapon._moves = new System.Collections.Generic.List<WeaponMove>(moves);
            return weapon;
        }
    }

    /// <summary>
    /// The projectile a weapon that shoots lets go (an arrow, a bolt, a thrown rock) and the place it asks the target for
    /// first: ranged attacks have their own group of places (handbook §18.2 p. 1 — one count for everyone either chokes
    /// the shooters or lets everyone in close); the place is held for one shot and given back after it, so the shooters
    /// take turns. The flight is the body's (IProjectileLauncher — a flying projectile moves by the engine's time; straight
    /// or a lob by Arc); a body with none lands the shot at once.
    /// </summary>
    [System.Serializable]
    public sealed class WeaponShot
    {
        [Tooltip("The projectile's model (no colliders needed); empty — a thin stick")]
        public GameObject Projectile;

        [Tooltip("On — the weapon's own model in the hand is what flies (a rock taken from the world), not Projectile; " +
                 "it is gone where it lands")]
        public bool ThrowsModel;

        [Tooltip("The shape of the flight: 0 — straight, like an arrow; above 0 — a lob rising this share of its length " +
                 "(a rock ≈ 0.15), that comes down where the target stood when it was let go and hits it if it is still " +
                 "within Hit Radius there")]
        [Min(0f)] public float Arc;

        [Tooltip("Metres per second")]
        [Min(1f)] public float Speed = 22f;

        [Tooltip("Where it leaves from, above the feet")]
        public float Height = 1.4f;

        [Tooltip("Where it leaves from, ahead of the body")]
        public float Forward = 0.5f;

        [Tooltip("Where on the target it flies to, above its feet")]
        public float AimHeight = 1.2f;

        [Tooltip("How near it must pass the target to hit it")]
        [Min(0.1f)] public float HitRadius = 0.7f;

        [Tooltip("Metres of free room the flight needs on each side of its line: a line that only grazes the edge of a " +
                 "wall is not shot along (the shot is held, the shooter steps out further)")]
        [Min(0f)] public float Clearance = 0.3f;

        [Tooltip("The group of places of the target to ask for one of before a shot (Attack Slot Capacity of the target " +
                 "says how many); empty — it shoots without asking")]
        public string Pool = "Ranged";

        [Tooltip("Seconds it waits for a place before the Attack node gives up (the group answers once per step)")]
        [Min(0f)] public float AskFor = 0.3f;
    }
}
