using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A tank (GDD §6): its base numbers, its run slots, its price and the standard module of each slot. The combat
    /// team reads the numbers; the hangar shows them.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Tank/Tank", fileName = "TankConfig")]
    public sealed class TankConfig : ScriptableObject
    {
        public const int SLOT_COUNT = 6;

        [SerializeField] private TankId _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [Tooltip("The tank prefab for the level and the hangar")]
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(1)] private float _health = 1000f;
        [Tooltip("m/s; the boost is about x1.8 (GDD §4)")]
        [SerializeField, Min(0)] private float _speed = 6f;
        [Tooltip("Turret, degrees per second")]
        [SerializeField, Min(0)] private float _turretSpeed = 90f;
        [Tooltip("Automatic weapons a run can hold (GDD §3: 4)")]
        [SerializeField, Min(0)] private int _weaponSlots = 4;
        [Tooltip("Gun and tank upgrades a run can hold (GDD §3: 6)")]
        [SerializeField, Min(0)] private int _upgradeSlots = 6;
        [Tooltip("Owned from the start")]
        [SerializeField] private bool _starting;
        [SerializeField, Min(0)] private int _priceMarks;
        [SerializeField, Min(0)] private int _priceSamples;
        [Tooltip("What the shop shows while it is locked, e.g. finish the elite of chapter 1")]
        [SerializeField] private string _unlockHint;
        [Tooltip("The module in each slot of a new tank, in ModuleSlot order")]
        [SerializeField] private ModuleId[] _defaultLoadout = new ModuleId[SLOT_COUNT];

        public TankId Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public GameObject Prefab => _prefab;
        public float Health => _health;
        public float Speed => _speed;
        public float TurretSpeed => _turretSpeed;
        public int WeaponSlots => _weaponSlots;
        public int UpgradeSlots => _upgradeSlots;
        public bool IsStarting => _starting;
        public int PriceMarks => _priceMarks;
        public int PriceSamples => _priceSamples;
        public string UnlockHint => _unlockHint;

        public ModuleId DefaultModule(ModuleSlot slot)
        {
            int index = (int)slot;
            return _defaultLoadout != null && index < _defaultLoadout.Length ? _defaultLoadout[index] : default;
        }

        /// <summary>Setup for tests and the catalog builder; the loadout in ModuleSlot order.</summary>
        public void Configure(TankId id, bool starting, int weaponSlots, int upgradeSlots, params ModuleId[] loadout)
        {
            _id = id;
            _starting = starting;
            _weaponSlots = weaponSlots;
            _upgradeSlots = upgradeSlots;
            _defaultLoadout = new ModuleId[SLOT_COUNT];
            Array.Copy(loadout, _defaultLoadout, Math.Min(loadout.Length, SLOT_COUNT));
        }
    }
}
