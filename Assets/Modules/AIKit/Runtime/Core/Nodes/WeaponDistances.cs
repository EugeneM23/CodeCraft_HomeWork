using System.Globalization;

namespace AIKit
{
    /// <summary>
    /// One rule for the distances of the nodes (step 90, spec 10.4; handbook §18.3: the numbers of the distances belong
    /// to the role — here, to the weapon in the hand): a distance set to 0 means "from the weapon in the hand", and the
    /// node says so in plain words. The weapon's word is the reach of its shortest move (WeaponAsset.ShortestReach — near
    /// enough for every move of it to land); a node takes its share of it: the Attack decision the reach itself, Chase and
    /// Flank stop at 0.8 of it, Flank's way round and the radius of Square Off / Circle Target are 1.1 of it, Kite keeps
    /// between 0.35 and 0.65 of it (a bow of 14 m: 5–9 m). With no weapon in the hand — or one with no moves — the node
    /// works with its old default and says that too.
    /// </summary>
    public static class WeaponDistances
    {
        /// <summary>Chase and Flank stop this part of the reach from the target: well inside it.</summary>
        public const float STOP = 0.8f;

        /// <summary>Flank's way round, and the radius Square Off and Circle Target wait at: just outside the reach.</summary>
        public const float AROUND = 1.1f;

        /// <summary>Kite backs away nearer than this part of its weapon's reach…</summary>
        public const float KITE_NEAR = 0.35f;

        /// <summary>…and comes closer farther than this part of it.</summary>
        public const float KITE_FAR = 0.65f;

        /// <summary>The reach of the weapon in the hand (its shortest move), metres; 0 — no weapon, or one with no moves.</summary>
        public static float ReachOf(AgentContext context) =>
            context?.Weapon != null ? context.Weapon.ShortestReach : 0f;

        /// <summary>
        /// A distance setting as it works now: set (above 0) — as it is; 0 — share × the reach of the weapon in the
        /// hand; no weapon — the fallback (the node's old default).
        /// </summary>
        public static float Resolve(float set, AgentContext context, float share, float fallback)
        {
            if (set > 0f)
                return set;

            float reach = ReachOf(context);
            return reach > 0f ? reach * share : fallback;
        }

        /// <summary>
        /// What a node says about a distance of 0 ("stops at 0.8 × my weapon's reach (no weapon: 1.5 m)"); the setting
        /// is a number of its own (above 0) or a variable — nothing to say: null.
        /// </summary>
        public static string Say(string what, BlackboardValue<float> setting, float share, float fallback) =>
            !setting.UsesVariable && setting.Get(null) <= 0f ? Say(what, share, fallback) : null;

        /// <summary>What a node says about a distance taken from the weapon in the hand.</summary>
        public static string Say(string what, float share, float fallback)
        {
            string part = share == 1f ? "my weapon's reach" : $"{Number(share)} × my weapon's reach";
            return $"{what} {part} (no weapon: {Number(fallback)} m)";
        }

        /// <summary>Two requirement lines of a node as one ("; " between them); either may be null.</summary>
        public static string Join(string first, string second) =>
            string.IsNullOrEmpty(first) ? second : string.IsNullOrEmpty(second) ? first : first + "; " + second;

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
