using AIKit;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The elite Regax's side of AIKit, on its entity. At start it binds the character's body to the entity, the level's
    /// grid and the player's vehicle, registers the Bite runner, makes its Rigidbody kinematic (the motor places it) and
    /// joins the level's agents (Manual simulation: the level steps it, <see cref="AIWorldBehaviour"/>). Every frame
    /// (<see cref="Tick"/>, after the entity's walk) the motor puts the body on the ground. It tells the brain what
    /// happened to it: a blast within <see cref="EliteBrain.BLAST_REACH"/> radii (the blackboard's blast facts, the
    /// tank made known, the bite broken off), and a hit from anything else (<c>DamageTakenEvent</c> →
    /// <c>AICharacter.TakeHit</c>, on the next step — a blast damages first and is announced after, and its hit is the
    /// blast's, not a flinch). Leaves the level's agents when the entity is disposed (death).
    /// </summary>
    public sealed class EliteRegaxBehaviour : IEntityInit, IEntityTick, IEntityDispose, IAIAgent
    {
        private readonly AICharacter _character;
        private readonly PerceptionProfile _senses;
        private readonly IEntity _level;

        private IEntity _entity;
        private Transform _transform;
        private EliteRegaxBody _body;
        private RegaxBiteRunner _bite;
        private PlayerTank _tank;
        private AIAgentList _agents;
        private IEvent<TakeDamageArgs> _damageTaken;
        private AtomicHitReceiver _hitReceiver;
        private BlackboardKey<int> _blastCount;
        private BlackboardKey<Vector3> _blastPoint;
        private BlackboardKey<float> _blastTime;

        private bool _hitPending;
        private float _hitDamage;
        private Transform _hitFrom;
        private int _hitFrame;
        private int _blastFrame = -1;
        private bool _bound;

        /// <param name="level">The level's AI entity; null — <see cref="LevelAI.Ensure"/> at start.</param>
        public EliteRegaxBehaviour(AICharacter character, PerceptionProfile senses = null, IEntity level = null)
        {
            _character = character;
            _senses = senses;
            _level = level;
        }

        public AICharacter Character => _character;

        public RegaxBiteRunner Bite => _bite;

        public EliteRegaxBody Body => _body;

        public void Init(IEntity entity)
        {
            _entity = entity;
            _transform = entity.GetTransform();
            if (_character == null)
            {
                Debug.LogError("EliteRegaxBehaviour: no AICharacter on the elite.");
                return;
            }

            _body = _character.Body as EliteRegaxBody;
            if (_body == null)
            {
                Debug.LogError($"EliteRegaxBehaviour: {_character.name}'s AI Character has no Elite Regax Body.", _character);
                return;
            }

            IEntity level = _level ?? LevelAI.Ensure();
            _tank = level.GetPlayerVehicle();
            _agents = level.GetWorldAgents();

            _character.Simulation = AICharacter.SimulationMode.Manual;
            _character.Initialize();                       // the body attached, the graph's abilities described
            _body.Bind(entity, level.GetLevelNavigation(), _tank);
            if (_senses != null)
                SensesSensorTask.SetProfile(_character, _senses);

            _bite = new RegaxBiteRunner(_transform, _tank, entity);
            _character.Abilities.Register(_bite);

            Blackboard board = _character.Blackboard;
            _blastCount = board.Declare<int>(EliteBrain.BLAST_COUNT);
            _blastPoint = board.Declare<Vector3>(EliteBrain.BLAST_POINT);
            _blastTime = board.Declare(EliteBrain.BLAST_TIME, -1000f);

            if (_transform.TryGetComponent(out Rigidbody rigidbody))
                rigidbody.isKinematic = true;              // the motor places it on the grid; the crush finds it by overlap
            _transform.TryGetComponent(out _hitReceiver);
            _body.Motor.Snap();

            if (entity.TryGetDamageTakenEvent(out _damageTaken))
                _damageTaken.Subscribe(OnDamageTaken);
            ExplosionEvents.Exploded += OnExploded;
            _agents.Add(this);
            _bound = true;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_bound)
                _body.Motor.Tick(deltaTime);
        }

        public void Dispose(IEntity entity)
        {
            if (!_bound)
                return;
            _bound = false;
            ExplosionEvents.Exploded -= OnExploded;
            _damageTaken?.Unsubscribe(OnDamageTaken);
            _agents?.Remove(this);
            _bite?.Cancel();
            _character.Abilities.Cancel();
            _body.Pathing.Clear();
            _body.Gamepad.Stop();
            _body.Motor.Release();
        }

        public void Step(float dt)
        {
            if (!_bound)
                return;

            if (_hitPending)
            {
                _hitPending = false;
                if (_blastFrame < _hitFrame)
                    _character.TakeHit(new HitInfo(_hitFrom, _hitDamage));
            }

            _character.Step(dt);
        }

        private void OnDamageTaken(TakeDamageArgs args)
        {
            IEntity source = args?.source;
            if (source != null && _hitReceiver != null && ReferenceEquals(source, _hitReceiver.Source))
                return;                                    // AIKit's own blow: the character has heard it already

            _hitPending = true;
            _hitFrame = Time.frameCount;
            _hitDamage = source != null && source.TryGetDamage(out IReactiveVariable<int> damage)
                ? damage.Value * (args.damageFactor > 0f ? args.damageFactor : 1f)
                : 1f;
            // The player is the only one who fights the elites: a hit is the tank's, whatever struck
            _hitFrom = _tank != null && _tank.IsPresent ? _tank.Transform
                : source != null && source.TryGetTransform(out Transform from) ? from : null;
        }

        private void OnExploded(Vector3 point, float radius)
        {
            if (!_bound || _character.Context == null)
                return;

            Vector3 offset = _transform.position - point;
            offset.y = 0f;
            if (offset.magnitude > radius * EliteBrain.BLAST_REACH)
                return;

            _blastFrame = Time.frameCount;
            Blackboard board = _character.Blackboard;
            board.Set(_blastCount, board.Get(_blastCount) + 1);
            board.Set(_blastPoint, point);
            board.Set(_blastTime, _character.Context.LocalTime);
            _character.Abilities.Cancel();                 // a bite under a blast is broken off
            if (_tank != null && _tank.IsPresent)
                _character.Context.Perception?.Alert(_tank.Transform);
            _character.RequestThink();
        }
    }
}
