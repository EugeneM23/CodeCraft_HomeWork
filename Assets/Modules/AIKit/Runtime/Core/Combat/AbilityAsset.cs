using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AIKit
{
    /// <summary>
    /// An ability as an asset, made of parts (step 90, C6; handbook §16 — an ability is data): when it may be used
    /// (Conditions: range, line, ground, angle, the pause from its end, a start delay, a score), how it takes turns (Queue:
    /// how many at once in its pool, its weight among the abilities of Use Any Ability), its swing and rest (Timing), how it
    /// gets where it acts (Delivery: a blow, a projectile — the same flight as a weapon's shot —, a lob to a place, on
    /// itself, or the game's own code), what it does there (Effects: damage, area damage) and what is shown while it
    /// flies (Cues: a ground mark). The graph names the ability (its Use Ability / Use Any Ability nodes); the character
    /// prepares its own runner for each one its graph names. Shared by every character — nothing here changes in play
    /// (handbook C.5). Name is the ability's id and the action the body shows for it.
    /// </summary>
    [CreateAssetMenu(menuName = "AI Kit/Ability", fileName = "Ability")]
    public class AbilityAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [Tooltip("The ability's id, and the action the body shows for it (a state of the animator)")]
        public string Name = "Ability";

        [Tooltip("When it may be used: every condition must hold (asked before a decision is taken)")]
        [SerializeReference] public List<AbilityCondition> Conditions = new() { new RangeCondition(), new CooldownCondition() };

        public AbilityQueue Queue = new();

        public AbilityTiming Timing = new();

        [Tooltip("How it gets where it acts")]
        [SerializeReference] public AbilityDelivery Delivery = new StrikeDelivery();

        [Tooltip("What happens where it acts")]
        [SerializeReference] public List<AbilityEffect> Effects = new() { new DamageEffect() };

        [Tooltip("What is shown while its projectile flies")]
        [SerializeReference] public List<AbilityCue> Cues = new();

        // ---------- before step 90: the flat numbers, read once and turned into parts ----------
        // The marker of the old format: an asset of before step 90 has its MaxRange here (≥ 0); a converted or new one -1

        [FormerlySerializedAs("MaxRange")] [SerializeField, HideInInspector] private float _oldMaxRange = -1f;
        [FormerlySerializedAs("MinRange")] [SerializeField, HideInInspector] private float _oldMinRange;
        [FormerlySerializedAs("Cooldown")] [SerializeField, HideInInspector] private float _oldCooldown;
        [FormerlySerializedAs("NeedsSight")] [SerializeField, HideInInspector] private bool _oldNeedsSight;
        [FormerlySerializedAs("WindUp")] [SerializeField, HideInInspector] private float _oldWindUp;
        [FormerlySerializedAs("Recovery")] [SerializeField, HideInInspector] private float _oldRecovery;
        [FormerlySerializedAs("Damage")] [SerializeField, HideInInspector] private float _oldDamage;
        [FormerlySerializedAs("Weight")] [SerializeField, HideInInspector] private float _oldWeight = 1f;
        [FormerlySerializedAs("AtOnce")] [SerializeField, HideInInspector] private int _oldAtOnce;

        [Tooltip("Before step 90 the ability asked for the place the node Take Attack Slot gives (Needs My Place): the " +
                 "editor's upgrade of a graph that names it wires an Attack Slot condition into its decision")]
        [FormerlySerializedAs("NeedsMyPlace")] [SerializeField, HideInInspector] private bool _neededMyPlace;

        /// <summary>The asset was of the old flat format and was turned into parts when it was read (not saved yet).</summary>
        [System.NonSerialized] public bool ConvertedOnLoad;

        /// <summary>Before step 90 the ability asked for the attack place of its user (the old Needs My Place).</summary>
        public bool NeededMyPlace => _neededMyPlace;

        /// <summary>The first part of this kind (a condition, the delivery, an effect, a cue); null — none.</summary>
        public T Find<T>() where T : AbilityPart
        {
            if (Delivery is T delivery)
                return delivery;

            T found = FindIn<T, AbilityCondition>(Conditions) ?? FindIn<T, AbilityEffect>(Effects);
            return found ?? FindIn<T, AbilityCue>(Cues);
        }

        private static T FindIn<T, TPart>(List<TPart> parts) where T : AbilityPart where TPart : AbilityPart
        {
            if (parts == null)
                return null;

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] is T part)
                    return part;
            }

            return null;
        }

        /// <summary>The game plays it with its own code (an IAbilityRunner registered under Name).</summary>
        public bool IsCodeRunner => Delivery is CodeRunnerDelivery;

        /// <summary>The pause after its end (its Cooldown part); 0 — it has none.</summary>
        public float Cooldown => Find<CooldownCondition>()?.Seconds ?? 0f;

        /// <summary>
        /// The ability as the agent's abilities check it (handbook §16 shape): what the conditions ask, the turns of its
        /// queue, the timing. The definition is the character's own (each runner writes its own).
        /// </summary>
        public void Describe(AbilityDefinition definition)
        {
            definition.Id = Name;
            definition.Range = new RangeBand(0f, float.MaxValue);
            definition.Cooldown = 0f;
            definition.StartDelay = 0f;
            definition.RequiresGround = false;
            definition.RequiresLineOfSight = false;
            definition.Angles = AbilityAngles.All;
            definition.Considerations = System.Array.Empty<Consideration>();
            definition.RequiresTarget = Delivery == null || Delivery.NeedsTarget;
            if (Conditions != null)
            {
                for (int i = 0; i < Conditions.Count; i++)
                    Conditions[i]?.Describe(definition);
            }

            AbilityQueue queue = Queue ?? new AbilityQueue();
            definition.Weight = queue.Weight;
            definition.RepeatPenalty = queue.RepeatPenalty;
            definition.TokenPool = queue.AtOnce > 0 ? (string.IsNullOrEmpty(queue.Pool) ? Name : queue.Pool) : "";
            definition.TokenCapacity = queue.AtOnce;
            definition.TokenCost = 1;
            definition.WindUp = Timing?.WindUp ?? 0f;
            definition.Recovery = Timing?.Recovery ?? 0f;
        }

        /// <summary>An ability made in code (tests, builders): a name and its parts; the rest as a new asset has them.</summary>
        public static AbilityAsset Create(string name, params AbilityPart[] parts)
        {
            var asset = CreateInstance<AbilityAsset>();
            asset.name = name;
            asset.Name = name;
            if (parts.Length == 0)
                return asset;

            asset.Conditions = new List<AbilityCondition>();
            asset.Effects = new List<AbilityEffect>();
            asset.Cues = new List<AbilityCue>();
            asset.Delivery = null;
            foreach (AbilityPart part in parts)
            {
                switch (part)
                {
                    case AbilityCondition condition:
                        asset.Conditions.Add(condition);
                        break;
                    case AbilityDelivery delivery:
                        asset.Delivery = delivery;
                        break;
                    case AbilityEffect effect:
                        asset.Effects.Add(effect);
                        break;
                    case AbilityCue cue:
                        asset.Cues.Add(cue);
                        break;
                }
            }

            asset.Delivery ??= new StrikeDelivery();
            return asset;
        }

        /// <summary>
        /// An ability the game plays with its own code, named after the game's runner (a demo's, a game's skill): the graph
        /// names it, the game's IAbilityRunner registered under the same id plays it. Made in play (no file).
        /// </summary>
        public static AbilityAsset CodeRunner(string name)
        {
            AbilityAsset asset = Create(name, new CodeRunnerDelivery());
            asset.Queue.AtOnce = 0;                     // the game's own definition says how it takes turns
            return asset;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        // An asset of before step 90 (its flat numbers are there) is turned into parts once, the way it worked then: a
        // blow at its Max Range with its damage, from the ground, the line if it needed it, its pause, its turns
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_oldMaxRange < 0f)
                return;

            Conditions = new List<AbilityCondition>
            {
                new RangeCondition { Min = _oldMinRange, Max = Mathf.Max(_oldMinRange, _oldMaxRange) },
                new GroundCondition()
            };
            if (_oldNeedsSight)
                Conditions.Add(new LineCondition());
            if (_oldCooldown > 0f)
                Conditions.Add(new CooldownCondition { Seconds = _oldCooldown });

            Queue = new AbilityQueue { AtOnce = Mathf.Max(0, _oldAtOnce), Weight = _oldWeight, RepeatPenalty = 0.5f };
            Timing = new AbilityTiming { WindUp = _oldWindUp, Recovery = _oldRecovery };
            Delivery = new StrikeDelivery { Reach = Mathf.Max(_oldMinRange, _oldMaxRange) };
            Effects = new List<AbilityEffect> { new DamageEffect { Damage = _oldDamage } };
            Cues = new List<AbilityCue>();
            _oldMaxRange = -1f;
            ConvertedOnLoad = true;
            Debug.Log($"AI Kit: the ability '{Name}' was made of the flat numbers of before step 90 — turned into parts " +
                      "(save the project to keep them)");
        }
    }
}
