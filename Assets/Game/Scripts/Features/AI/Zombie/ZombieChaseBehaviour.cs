using System.Collections.Generic;
using AIKit;
using AIKit.Entities;
using Atomic.Elements;
using Atomic.Entities;
using Game.Horde;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// A skinned zombie's brain (the sandbox's hand-placed <c>Zombie.prefab</c>): it goes for the player's tank, takes a
    /// place round the hull and bites it.
    /// <list type="bullet">
    /// <item>Far from the hull: the level's A* (<see cref="VoxelGridNavigator"/> over <see cref="LevelNav"/>), asked
    /// again every <see cref="REPLAN"/> s; no grid or no way â€” straight at the tank.</item>
    /// <item>Within <see cref="NEAR"/> of the hull: to its place on the ring (<see cref="ZombieRing"/>), round the
    /// hull, not through it; with no free place it waits a body further out.</item>
    /// <item>The hull within the bite's reach (<see cref="RegaxBiteRunner"/>): it fires its held weapon â€” the attack
    /// animation and the hit at its event, as every enemy's.</item>
    /// <item>Bodies: a soft push from the other skinned zombies near, and the elite's motor (<see cref="EliteMotor"/>):
    /// no walking through walls of the grid, through the hull or through another zombie. The horde steps out of its way
    /// (<see cref="HordeObstacles"/>).</item>
    /// </list>
    /// The walk itself is the entity's own (<c>MoveDirection</c> at its <c>MoveSpeed</c>): the speed is the prefab's.
    /// Steps with the level's agents (<see cref="AIWorldBehaviour"/>); the motor places the body after the walk
    /// (<see cref="Tick"/>). No managed allocations after start.
    /// </summary>
    public sealed class ZombieChaseBehaviour : IEntityInit, IEntityTick, IEntityDispose, IAIAgent
    {
        /// <summary>Radius of the body (its capsule), m.</summary>
        public const float BODY_RADIUS = 0.5f;

        /// <summary>Half the distance two skinned zombies keep between their middles outright (the motor), m: a body and a hand.</summary>
        public const float KEEP_APART = 0.55f;

        /// <summary>Within this of the hull it goes for its place on the ring instead of following the path, m.</summary>
        public const float NEAR = 4f;

        /// <summary>Seconds between path plans while far.</summary>
        public const float REPLAN = 1f;

        /// <summary>Within this far round the ring it heads straight for its place, m.</summary>
        private const float RING_DIRECT = 1f;

        private const float ARRIVE = 0.1f;
        private const float SLOW = 1f;

        /// <summary>Other zombies within this push it aside (softly; the motor keeps them 1.1 m apart outright), m.</summary>
        private const float SEPARATION = 1.6f;

        private const float SEPARATION_WEIGHT = 0.8f;

        private readonly IEntity _level;

        private IEntity _entity;
        private Transform _transform;
        private Health _health;
        private PlayerTank _tank;
        private LevelNav _nav;
        private AIAgentList _agents;
        private EliteMotor _motor;
        private AtomicActuator _actuator;
        private Stick _stick;
        private VoxelGridNavigator _navigator;
        private RegaxBiteRunner _bite;
        private int _obstacle = -1;
        private float _sincePlan;
        private bool _bound;

        /// <param name="level">The level's AI entity; null â€” <see cref="LevelAI.Ensure"/> at start.</param>
        public ZombieChaseBehaviour(IEntity level = null) => _level = level;

        public AICharacter Character => null;

        /// <summary>Its place on the hull ring, -1 none (<see cref="ZombieRing"/>).</summary>
        public int RingSlot { get; internal set; } = -1;

        /// <summary>Where it stands (feet).</summary>
        public Vector3 Position => _transform != null ? _transform.position : Vector3.zero;

        /// <summary>It is alive and chasing (it may hold a place on the ring).</summary>
        public bool CanHoldPlace => _bound;

        public RegaxBiteRunner Bite => _bite;

        public VoxelGridNavigator Navigator => _navigator;

        public void Init(IEntity entity)
        {
            _entity = entity;
            _transform = entity.GetTransform();
            _health = entity.TryGetHealth(out Health health) ? health : null;

            IEntity level = _level ?? LevelAI.Ensure();
            _tank = level.GetPlayerVehicle();
            _nav = level.GetLevelNavigation();
            _agents = level.GetWorldAgentList();

            _motor = new EliteMotor(_transform, KEEP_APART);
            _motor.Bind(_nav, _tank);
            _actuator = new AtomicActuator(_transform, _motor);
            _actuator.Bind(entity);
            _stick = new Stick(_actuator);
            _navigator = new VoxelGridNavigator(_stick, _nav);
            _bite = new RegaxBiteRunner(_transform, _tank, entity);

            if (_transform.TryGetComponent(out Rigidbody rigidbody))
                rigidbody.isKinematic = true;              // the motor places it on the grid; the crush finds it by overlap
            _motor.Snap();

            // Spread the first plans over a second: twenty zombies must not ask the one A* in the same frame.
            _sincePlan = REPLAN * UnityEngine.Random.value;
            _obstacle = HordeObstacles.Register(_transform.position, BODY_RADIUS);
            ZombieRing.Join(this);
            _agents.Add(this);
            _bound = true;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (!_bound)
                return;
            if (IsDead)
            {
                Unbind();
                return;
            }

            _motor.Tick(deltaTime);
            HordeObstacles.Set(_obstacle, _transform.position, BODY_RADIUS);
        }

        public void Dispose(IEntity entity) => Unbind();

        public void Step(float dt)
        {
            if (!_bound)
                return;
            if (IsDead)
            {
                Unbind();
                return;
            }

            _bite.Tick(dt);
            ZombieRing.Update(_tank);

            if (_tank == null || !_tank.IsPresent)
            {
                _navigator.Clear();
                _actuator.Apply(dt);                       // nothing pressed: it stands
                return;
            }

            Vector3 p = _transform.position;
            Vector3 toTank = _tank.AimPoint - p;
            toTank.y = 0f;

            if (_bite.IsRunning)
            {
                _actuator.Face(toTank);
                _actuator.Apply(dt);
                return;
            }

            if (_bite.CanStart(null))
            {
                _bite.Begin(null);
                _actuator.Face(toTank);
                _actuator.Apply(dt);
                return;
            }

            Vector3 move = _tank.DistanceToHull(p) <= NEAR && _tank.TryGetHullFrame(out Vector3 c, out Vector3 r, out Vector3 f, out _)
                ? ToPlace(p, c, r, f)
                : Chase(p, toTank, dt);

            move += Separation(p) * SEPARATION_WEIGHT;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            if (move.sqrMagnitude > 1e-4f)
                _actuator.Move(move, move.magnitude);
            else
                _actuator.Face(toTank);                    // standing at its place: it faces the tank
            _actuator.Apply(dt);
        }

        private bool IsDead => _health != null && _health.IsEmpty();

        // Far: the path to the tank, re-planned every REPLAN s; no path (yet) â€” straight at it
        private Vector3 Chase(Vector3 p, Vector3 toTank, float dt)
        {
            _sincePlan += dt;
            if (_sincePlan >= REPLAN || _navigator.Status == NavStatus.Idle)
            {
                _sincePlan = 0f;
                NavVerdict verdict = _navigator.SetDestination(_tank.Transform.position);
                if (verdict == NavVerdict.NoGraph || verdict == NavVerdict.OffGraph)
                    _navigator.Clear();
            }

            _stick.Clear();
            _navigator.Step(dt);
            if (_stick.Pressed)
                return _stick.Direction;
            return toTank.sqrMagnitude > 1e-6f ? toTank.normalized : Vector3.zero;
        }

        // Near: its place on the ring (or the waiting row a place further out), round the hull
        private Vector3 ToPlace(Vector3 p, Vector3 centre, Vector3 right, Vector3 forward)
        {
            _navigator.Clear();
            float2 half = ZombieRing.Count > 0 ? ZombieRing.Half : float2.zero;
            if (ZombieRing.Count == 0)
                return Vector3.zero;

            float2 local = ZombieRing.ToLocal(p, centre, right, forward);
            float standoff;
            float target;
            if (RingSlot >= 0)
            {
                standoff = ZombieRing.STANDOFF;
                target = HordeHullRing.SlotParam(RingSlot, ZombieRing.Count, ZombieRing.Length);
            }
            else
            {
                standoff = ZombieRing.STANDOFF + ZombieRing.SPACING;
                target = HordeHullRing.Param(local, half, standoff);
            }

            // Round the hull along the waiting row's lane, behind the ones already at their places; in at its own.
            float2 aim = HordeHullRing.Approach(local, target, half, standoff, ZombieRing.STANDOFF + ZombieRing.SPACING,
                RING_DIRECT);
            float2 to = aim - local;
            float d = math.length(to);
            if (d < ARRIVE)
                return Vector3.zero;
            Vector3 world = right * to.x + forward * to.y;
            world.y = 0f;
            return world.normalized * Mathf.Clamp01(d / SLOW);
        }

        // A soft push away from the other skinned zombies near (the motor's hard keep-apart is the backstop)
        private Vector3 Separation(Vector3 p)
        {
            Vector3 push = Vector3.zero;
            List<ZombieChaseBehaviour> members = ZombieRing.Members;
            for (int i = 0; i < members.Count; i++)
            {
                ZombieChaseBehaviour other = members[i];
                if (other == this)
                    continue;
                Vector3 d = p - other.Position;
                d.y = 0f;
                float distance = d.magnitude;
                if (distance >= SEPARATION || distance < 1e-4f)
                    continue;
                push += d / distance * (1f - distance / SEPARATION);
            }

            return push;
        }

        private void Unbind()
        {
            if (!_bound)
                return;
            _bound = false;
            _agents?.Remove(this);
            ZombieRing.Leave(this);
            HordeObstacles.Unregister(_obstacle);
            _obstacle = -1;
            _bite?.Cancel();
            _navigator?.Clear();
            _actuator?.Stop();
            _motor?.Release();
        }

        /// <summary>
        /// What the navigator presses, kept for the brain to add the push from the others before it goes to the body;
        /// the body is the walker's (<see cref="AtomicActuator"/>).
        /// </summary>
        private sealed class Stick : IAgentActuator
        {
            private readonly AtomicActuator _body;

            public Stick(AtomicActuator body) => _body = body;

            public bool Pressed { get; private set; }

            public Vector3 Direction { get; private set; }

            public void Clear()
            {
                Pressed = false;
                Direction = Vector3.zero;
            }

            public AgentBodyInfo Body => _body.Body;

            public void Move(Vector3 direction, float speedScale = 1f)
            {
                direction.y = 0f;
                Direction = direction.sqrMagnitude > 1f ? direction.normalized : direction;
                Direction *= Mathf.Clamp01(speedScale);
                Pressed = Direction.sqrMagnitude > 1e-6f;
            }

            public void SetVertical(float axis)
            {
            }

            public void Face(Vector3 direction)
            {
            }

            public void Jump(float holdTime)
            {
            }

            public float HoldTimeForHeight(float rise) => 0f;

            public void Crouch(bool crouch)
            {
            }

            public void DropThrough()
            {
            }

            public void Stop() => Clear();

            public void Apply(float dt)
            {
            }
        }
    }
}
