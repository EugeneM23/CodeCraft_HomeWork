using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Game
{
    /// <summary>
    /// STUB run for the battle UI until the adapter over the core's IRunService: a simulation good enough to see
    /// and test every battle screen. Not a game rule anywhere; screens know only IRunHudSource.
    /// - A run starts whenever a scene with a GameContext appears (a battle start or restart).
    /// - Every enemy death in the GameContext world (DeathTakenEvent, as the old SandboxBattleStats) gives
    ///   1 kill, 1 mark and XP_PER_KILL experience; the curve is GDD's (RunXpUseCase). A level up raises
    ///   SkillOffered with 3 offers of the 6 milestone skills (core SkillIds), one level up at a time; the next
    ///   pending one is offered from inside ChooseSkill, as the core does.
    /// - KILLS_PER_ARENA kills clear the arena (+10 + 2·n marks); the gate opens GATE_DISTANCE ahead of the
    ///   tank and driving to it (or F6) leads into the next arena. Arena 5 cleared: victory.
    /// - The elite comes from the AI team's EliteStatus (+40 marks, +2 samples when it dies).
    /// - The tank's health at 0: defeat. ExitToHangar: abandoned, then the game flow goes to the hangar.
    ///   Crush streak stays 0.
    /// - Arena hints: controls on arena 1, crush on 2, destroy cover on 3.
    /// - Special01 is -1 (no module) until the combat team's ISpecialModule.
    /// - Debug keys: F6 clears the arena or goes through the open gate, F7 gives a level's experience.
    /// Marks and samples are not credited to the progress (the hangar source has no such command yet).
    /// No allocations per frame: events and the runner's Tick only compare and add numbers.
    /// </summary>
    public sealed class StubRunHudSource : IRunHudSource
    {
        private const int ARENA_COUNT = 5;
        private const int KILLS_PER_ARENA = 12;
        private const int XP_PER_KILL = 4;
        private const int ELITE_MARKS = 40;
        private const int ELITE_SAMPLES = 2;
        private const float GATE_DISTANCE = 18f;
        private const float GATE_REACH = 5f;
        private const string HANGAR_SCREEN = "Hangar";

        private static readonly string[] PoolIds =
        {
            SkillIds.Turret.Value, SkillIds.AirStrike.Value, SkillIds.Ricochet.Value,
            SkillIds.RateOfFire.Value, SkillIds.Armor.Value, SkillIds.Magnet.Value
        };

        private static readonly bool[] PoolIsWeapon = { true, true, false, false, false, false };

        private static int s_bestArena;

        public event Action Changed;
        public event Action<IReadOnlyList<SkillOffer>> SkillOffered;
        public event Action<RunSummary> RunEnded;

        private readonly List<RunSkill> _skills = new();
        private readonly List<SkillOffer> _offers = new();
        private readonly List<int> _scratch = new();
        private readonly Dictionary<IEntity, IEvent<TakeDamageArgs>> _enemies = new();
        private readonly System.Random _random = new();

        private readonly Action<IEntity> _onEntityAdded;
        private readonly Action<IEntity> _onEntityRemoved;
        private readonly Action<TakeDamageArgs> _onEnemyDeath;
        private readonly Action<IEntity> _onCharacterChanged;
        private readonly Action _onTankDestroyed;
        private readonly Action _onEliteAppeared;
        private readonly Action<float> _onEliteHealth;
        private readonly Action _onEliteDied;

        private GameContext _context;
        private IEntityWorld<IEntity> _world;
        private Subscription<IEntity> _characterSubscription;
        private Health _vehicleHealth;
        private Health _characterHealth;
        private Transform _tank;

        private int _arena = 1;
        private int _level = 1;
        private int _xp;
        private int _marks;
        private int _samples;
        private int _kills;
        private int _killsInArena;
        private int _elitesKilled;
        private float _elapsed;
        private bool _gateOpen;
        private Vector3 _gatePosition;
        private int _pendingLevels;
        private bool _offerOpen;
        private bool _ended;

        public StubRunHudSource()
        {
            _onEntityAdded = Track;
            _onEntityRemoved = Untrack;
            _onEnemyDeath = HandleEnemyDeath;
            _onCharacterChanged = HandleCharacterChanged;
            _onTankDestroyed = HandleTankDestroyed;
            _onEliteAppeared = RaiseChanged;
            _onEliteHealth = HandleEliteHealth;
            _onEliteDied = HandleEliteDied;

            if (!Application.isPlaying)
                return;

            var go = new GameObject(nameof(StubRunHudSourceRunner)) { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<StubRunHudSourceRunner>().Source = this;
        }

        // ---------- IRunHudSource ----------

        public int ArenaIndex => _arena;
        public int ArenaCount => ARENA_COUNT;
        public ArenaHint ArenaHints => _arena switch
        {
            1 => ArenaHint.Controls,
            2 => ArenaHint.Crush,
            3 => ArenaHint.DestroyCover,
            _ => ArenaHint.None
        };
        public int Level => _level;
        public int Xp => _xp;
        public int XpToNextLevel => RunXpUseCase.ToNextLevel(_level);
        public int Marks => _marks;
        public int Samples => _samples;
        public int Kills => _kills;
        public int Crushes => 0;
        public int CrushStreak => 0;
        public int ElitesKilled => _elitesKilled;
        public float ElapsedTime => _elapsed;
        public bool EliteAlive => EliteStatus.IsAlive;
        public float EliteHealth01 => EliteStatus.Health01;
        public bool GateOpen => _gateOpen;
        public Vector3 GatePosition => _gatePosition;
        public IReadOnlyList<RunSkill> Skills => _skills;
        public IReadOnlyList<SkillOffer> PendingOffer => _offerOpen ? _offers : Array.Empty<SkillOffer>();
        /// <summary>No special module until combat's ISpecialModule: -1 hides it.</summary>
        public float Special01 => -1f;

        public void ChooseSkill(int index)
        {
            if (!_offerOpen || index < 0 || index >= _offers.Count)
                return;

            SkillOfferUseCase.Apply(_skills, _offers[index]);
            _offerOpen = false;
            RaiseChanged();
            // As the core does: the next pending level is offered at once, from inside this call.
            while (!_offerOpen && _pendingLevels > 0)
                Offer();
        }

        /// <summary>Ends the run as abandoned, then goes to the hangar (the core's LeaveToHangar does both).</summary>
        public void ExitToHangar()
        {
            if (_ended)
                return;

            End(false, true);
            if (!AppServices.GameFlow.IsBusy)
                AppServices.GameFlow.ReturnToMenu(HANGAR_SCREEN);
        }

        // ---------- Runner ----------

        /// <summary>Per frame from the runner: binds a new battle scene, counts time, reads the debug keys.</summary>
        internal void Tick(float deltaTime)
        {
            if (!BindToScene() || _ended || AppServices.Pause.IsPaused)
                return;

            _elapsed += deltaTime;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[Key.F6].wasPressedThisFrame)
            {
                if (_gateOpen)
                    EnterNextArena();
                else
                    ClearArena();
            }
            if (keyboard != null && keyboard[Key.F7].wasPressedThisFrame)
                AddXp(RunXpUseCase.ToNextLevel(_level) - _xp);

            if (_ended)
                return;

            if (_gateOpen && _tank != null)
            {
                Vector3 offset = _tank.position - _gatePosition;
                offset.y = 0f;
                if (offset.sqrMagnitude <= GATE_REACH * GATE_REACH)
                    EnterNextArena();
            }

            if (!_offerOpen && _pendingLevels > 0)
                Offer();
        }

        /// <summary>True while a battle scene is bound; a new GameContext starts a new run.</summary>
        private bool BindToScene()
        {
            bool has = GameContext.TryGetInstance(out GameContext context) && context != null;
            if (has && ReferenceEquals(context, _context))
                return true;

            Unbind();
            if (!has)
                return false;

            _context = context;
            StartRun();
            return true;
        }

        private void StartRun()
        {
            _arena = 1;
            _level = 1;
            _xp = _marks = _samples = _kills = _killsInArena = _elitesKilled = 0;
            _elapsed = 0f;
            _gateOpen = false;
            _pendingLevels = 0;
            _offerOpen = false;
            _ended = false;
            _skills.Clear();
            _offers.Clear();

            _world = _context.GetEntityWorld();
            foreach (IEntity entity in _world) // once per run, not in a tick
                Track(entity);
            _world.OnAdded += _onEntityAdded;
            _world.OnRemoved += _onEntityRemoved;
            _characterSubscription = _context.GetPlayerContext().GetCharacter().Observe(_onCharacterChanged);

            EliteStatus.Appeared += _onEliteAppeared;
            EliteStatus.HealthChanged += _onEliteHealth;
            EliteStatus.Died += _onEliteDied;

            RaiseChanged();
        }

        internal void Unbind()
        {
            if (_context is null)
                return;

            _characterSubscription.Dispose();
            _characterSubscription = default;
            SetVehicleHealth(null);
            SetCharacterHealth(null);
            _tank = null;

            if (_world != null)
            {
                _world.OnAdded -= _onEntityAdded;
                _world.OnRemoved -= _onEntityRemoved;
                _world = null;
            }

            foreach (KeyValuePair<IEntity, IEvent<TakeDamageArgs>> enemy in _enemies)
                enemy.Value.OnEvent -= _onEnemyDeath;
            _enemies.Clear();

            EliteStatus.Appeared -= _onEliteAppeared;
            EliteStatus.HealthChanged -= _onEliteHealth;
            EliteStatus.Died -= _onEliteDied;

            _context = null;
        }

        // ---------- Enemies ----------

        private void Track(IEntity entity)
        {
            if (entity == null || !entity.HasEnemyTag() || _enemies.ContainsKey(entity))
                return;
            if (!entity.TryGetHealth(out Health health) || !health.Exists())
                return;
            if (!entity.TryGetDeathTakenEvent(out IEvent<TakeDamageArgs> death))
                return;

            death.OnEvent += _onEnemyDeath;
            _enemies.Add(entity, death);
        }

        // A dead enemy leaves the world; a pooled one may come back and is tracked again.
        private void Untrack(IEntity entity)
        {
            if (entity == null || !_enemies.TryGetValue(entity, out IEvent<TakeDamageArgs> death))
                return;

            death.OnEvent -= _onEnemyDeath;
            _enemies.Remove(entity);
        }

        private void HandleEnemyDeath(TakeDamageArgs _)
        {
            if (_ended)
                return;

            _kills++;
            _killsInArena++;
            _marks++;
            AddXp(XP_PER_KILL);
            if (!_gateOpen && _killsInArena >= KILLS_PER_ARENA)
                ClearArena();
            RaiseChanged();
        }

        private void HandleEliteHealth(float _) => RaiseChanged();

        private void HandleEliteDied()
        {
            if (_ended)
                return;

            _elitesKilled++;
            _marks += ELITE_MARKS;
            _samples += ELITE_SAMPLES;
            RaiseChanged();
        }

        // ---------- Arena and gate ----------

        private void ClearArena()
        {
            if (_ended || _gateOpen)
                return;

            _marks += 10 + 2 * _arena;
            if (_arena >= ARENA_COUNT)
            {
                End(true, false);
                return;
            }

            _gateOpen = true;
            Vector3 origin = _tank != null ? _tank.position : Vector3.zero;
            Vector3 forward = _tank != null ? _tank.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            _gatePosition = origin + forward.normalized * GATE_DISTANCE;
            RaiseChanged();
        }

        private void EnterNextArena()
        {
            if (!_gateOpen || _ended)
                return;

            _gateOpen = false;
            _arena++;
            _killsInArena = 0;
            RaiseChanged();
        }

        // ---------- Experience and skills ----------

        private void AddXp(int amount)
        {
            if (amount <= 0 || _ended)
                return;

            _pendingLevels += RunXpUseCase.Add(ref _level, ref _xp, amount);
            RaiseChanged();
        }

        private void Offer()
        {
            SkillOfferUseCase.Draw(_skills, PoolIds, PoolIsWeapon, SkillOfferUseCase.OFFER_COUNT, _random, _offers, _scratch);
            _pendingLevels--;
            if (_offers.Count == 0)
                return; // every skill at the top level: the level goes without a choice

            _offerOpen = true;
            SkillOffered?.Invoke(_offers);
        }

        // ---------- Tank ----------

        private void HandleCharacterChanged(IEntity character)
        {
            if (character == null)
            {
                SetCharacterHealth(null);
                return;
            }

            character.TryGetHealth(out Health health);
            if (character.HasVehicleTag())
            {
                // The tank stays "the player's tank" after the player gets out, until another vehicle.
                SetVehicleHealth(health);
                SetCharacterHealth(null);
                _tank = character is Component component ? component.transform
                    : character.TryGetTransform(out Transform transform) ? transform : null;
            }
            else
            {
                SetCharacterHealth(health);
            }
        }

        private void SetVehicleHealth(Health health)
        {
            if (_vehicleHealth != null)
                _vehicleHealth.OnHealthEmpty -= _onTankDestroyed;
            _vehicleHealth = health;
            if (_vehicleHealth != null)
                _vehicleHealth.OnHealthEmpty += _onTankDestroyed;
        }

        private void SetCharacterHealth(Health health)
        {
            if (_characterHealth != null)
                _characterHealth.OnHealthEmpty -= _onTankDestroyed;
            _characterHealth = health;
            if (_characterHealth != null)
                _characterHealth.OnHealthEmpty += _onTankDestroyed;
        }

        private void HandleTankDestroyed() => End(false, false);

        // ---------- End ----------

        private void End(bool victory, bool abandoned)
        {
            if (_ended)
                return;

            _ended = true;
            _gateOpen = false;
            _offerOpen = false;
            _pendingLevels = 0;

            bool record = _arena > s_bestArena;
            if (record)
                s_bestArena = _arena;

            var summary = new RunSummary
            {
                Victory = victory,
                Abandoned = abandoned,
                ChapterId = null,
                ArenaReached = _arena,
                ArenaCount = ARENA_COUNT,
                Time = _elapsed,
                Kills = _kills,
                Crushes = 0,
                ElitesKilled = _elitesKilled,
                Marks = _marks,
                Samples = _samples,
                NewRecord = record
            };
            RaiseChanged();
            RunEnded?.Invoke(summary);
        }

        private void RaiseChanged() => Changed?.Invoke();

        // Domain reload may be off in the editor: the record starts again with each Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_bestArena = 0;
    }
}
