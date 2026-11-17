using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Gives the character a stock of grenades of each type it carries, the selected type, a throw
    /// and a switch. The grenades are the character's own, not a weapon: switching or dropping the
    /// weapon in hand leaves them be. The player throws and switches with PlayerCharacterGrenadeController;
    /// GrenadePickUpInstaller tops a stock up.
    /// </summary>
    public sealed class GrenadeThrowerInstaller : MonoEntityInstaller
    {
        [Serializable]
        public sealed class Slot
        {
            public GrenadeType type;
            public MonoEntity prefab;
            [Min(0)] public int count;
        }

        [Tooltip("The grenade types the character carries, in switching order; the first is selected at start.")]
        [SerializeField] private List<Slot> _slots = new();
        [Tooltip("Where the grenade leaves the hand, at the shoulder.")]
        [SerializeField] private Transform _throwPoint;
        [Tooltip("Seconds between two throws.")]
        [SerializeField] private float _cooldown = 0.8f;
        [Tooltip("A target farther than this (on the ground plane) is pulled in to this distance.")]
        [SerializeField] private float _maxRange = 15f;
        [Tooltip("Speed along the ground, m/s: the farther the target, the longer and higher the arc.")]
        [SerializeField] private float _throwSpeed = 12f;
        [Tooltip("Even a throw at one's own feet flies at least this long, so it arcs instead of hitting the floor.")]
        [SerializeField] private float _minFlightTime = 0.6f;
        [Header("View")]
        [Tooltip("Shows the selected type and its stock. Optional.")]
        [SerializeField] private Text _hud;

        public override void Install(IEntity entity)
        {
            Dictionary<GrenadeType, Ammo> ammo = new();
            Dictionary<GrenadeType, MonoEntity> prefabs = new();

            foreach (Slot slot in _slots)
            {
                if (slot.prefab == null || prefabs.ContainsKey(slot.type))
                    continue;

                prefabs.Add(slot.type, slot.prefab);
                ammo.Add(slot.type, new Ammo(slot.count));
            }

            entity.AddGrenadeAmmo(ammo);
            entity.AddGrenadePrefabs(prefabs);
            entity.AddGrenadeSelected(new ReactiveVariable<GrenadeType>(_slots.Count > 0 ? _slots[0].type : default));
            entity.AddGrenadeSwitchAction(new GrenadeSwitchAction(entity));

            // Starts expired: the first grenade can be thrown at once.
            entity.AddGrenadeCooldown(new CooldownTimer(_cooldown));
            entity.WhenTick(deltaTime => entity.GetGrenadeCooldown().Tick(deltaTime));

            entity.AddGrenadeThrowCondition(new AndExpression(
                new ExpressionMember<bool>(() => !entity.TryGetHealth(out Health health) || health.Exists()),
                new ExpressionMember<bool>(() =>
                    ammo.TryGetValue(entity.GetGrenadeSelected().Value, out Ammo stock) && stock.Exists()),
                new ExpressionMember<bool>(() => entity.GetGrenadeCooldown().IsExpired())
            ));

            entity.AddGrenadeThrowAction(new GrenadeThrowAction(entity, _throwPoint,
                _maxRange, _throwSpeed, _minFlightTime));

            if (_hud != null)
                entity.AddBehaviour(new GrenadeHudBehaviour(_hud));
        }
    }
}
