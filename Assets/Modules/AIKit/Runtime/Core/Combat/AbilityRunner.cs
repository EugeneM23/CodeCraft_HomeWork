using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// One character's run of an AbilityAsset — the parts executor (step 90; IAbilityRunner: "the AI decides which and
    /// when, the runner knows how"). Begin: the action of the ability's name is asked for (or the plain swing — one
    /// request, never both); the wind-up: the aim follows the target; the release: the delivery — a blow on the target in
    /// reach, a projectile or a lob through the body's launcher (the same flight as a weapon's shot), or the user itself —
    /// and the effects where it acts (for a flight: where it comes down, with the cues shown while it flies); then the
    /// recovery. The phase of the blow is written into the agent's status (AgentContext.AttackPhase), as AttackRunner does
    /// for a weapon's move — the two never run at once. The asset is shared and never changes; the run is this runner's.
    /// </summary>
    public sealed class AbilityRunner : ITickedAbilityRunner, IProjectileLanding
    {
        // Throws of this runner made ready for the air at the start (an ability with a short pause); more are added when
        // more fly at once — never one taken from a throw still in the air
        private const int FLIGHTS = 4;

        // The least rise of a lob: one with Arc 0 still flies its Flight time (the time to step out), only flat
        private const float LEAST_RISE = 1e-3f;

        private readonly AbilityAsset _asset;
        private readonly Transform _user;
        private readonly AbilityDefinition _definition = new();
        private readonly WeaponShot _shot;

        private AgentContext _context;
        private Transform _target;
        private Vector3 _aimPoint;
        private float _elapsed = -1f;
        private bool _released;
        private bool _showsAction;
        private Flight _lastFlight;

        // A throw in the air: its own number (0 — the record is free; the number is the launch's token, so a landing
        // finds its own throw), whom it was at and what its cues made (handed back to their End where it comes down)
        private sealed class Flight
        {
            public int Serial;
            public Transform Target;
            public object[] Cues;
        }

        private readonly System.Collections.Generic.List<Flight> _flights = new();
        private int _serial;

        public AbilityRunner(AbilityAsset asset, Transform user)
        {
            _asset = asset;
            _user = user;
            asset.Describe(_definition);
            switch (asset.Delivery)
            {
                case ProjectileDelivery projectile:
                    _shot = projectile.ToShot();
                    break;
                case LobDelivery lob:
                    _shot = lob.ToShot(asset.Find<DamageEffect>()?.HitRadius ?? 0.1f);
                    break;
            }

            for (int i = 0; i < FLIGHTS; i++)
                _flights.Add(NewFlight());
        }

        private Flight NewFlight() => new() { Cues = new object[_asset.Cues?.Count ?? 0] };

        public AbilityAsset Asset => _asset;

        public string Id => _asset.Name;

        /// <summary>How many times the ability was started (tests, debugging).</summary>
        public int Uses { get; private set; }

        /// <summary>The ability as the agent's abilities check it: this runner's own description of the asset's parts.</summary>
        public AbilityDefinition Definition => _definition;

        /// <summary>A throw of it is in the air now.</summary>
        public bool IsFlying
        {
            get
            {
                for (int i = 0; i < _flights.Count; i++)
                {
                    if (_flights[i].Serial != 0)
                        return true;
                }

                return false;
            }
        }

        /// <summary>The character that uses the ability.</summary>
        private Transform User =>
            _context != null && _context.Agent != null && _context.Agent.EntityRoot != null
                ? _context.Agent.EntityRoot
                : _user;

        public bool IsRunning => _elapsed >= 0f;

        public AbilityPhase Phase =>
            !IsRunning ? AbilityPhase.None : _released ? AbilityPhase.Recovery : AbilityPhase.WindUp;

        public bool CanStart(AgentContext context) => !IsRunning;

        public void Begin(AgentContext context)
        {
            _context = context;
            _target = context?.Target;
            Transform user = User;
            _aimPoint = _target != null ? _target.position
                : user != null ? user.position + user.forward * Mathf.Min(_definition.Range.Max, 10f) : Vector3.zero;
            _elapsed = 0f;
            _released = false;
            _lastFlight = null;
            Uses++;

            // The warning: the action of this name if the presentation has one, else its plain swing — one request
            ActionState actions = context?.Actions;
            _showsAction = false;
            if (actions != null)
            {
                actions.Begin(_asset.Name);
                _showsAction = actions.CanShow(_asset.Name);
                if (!_showsAction)
                    actions.Swing();
            }

            if (context != null)
                context.AttackPhase = AbilityPhase.WindUp;          // the agent's status, as a weapon's move writes it
        }

        /// <summary>Time passes for the ability (the agent's abilities call it once per step, with the agent's dt).</summary>
        public void Tick(float dt)
        {
            if (!IsRunning)
                return;

            _elapsed += dt;
            if (!_released)
            {
                if (_target != null)
                    _aimPoint = _target.position;       // it aims until it is released, not after
                if (_elapsed < _definition.WindUp)
                    return;

                _released = true;
                if (_context != null)
                    _context.AttackPhase = AbilityPhase.Recovery;
                Release();
            }

            if (_elapsed < _definition.WindUp + _definition.Recovery)
                return;

            _elapsed = -1f;                             // over: its action ends on its own
            if (_context != null)
                _context.AttackPhase = AbilityPhase.None;
        }

        public void Cancel()
        {
            if (!IsRunning)
                return;

            _elapsed = -1f;
            if (_showsAction)
                _context?.Actions.End(true);
            if (_context != null)
                _context.AttackPhase = AbilityPhase.None;

            // A cue begun by this run ends now (handbook §20.4: End is mandatory on a cancel); what is in the air still
            // comes down and does its work
            if (_released && _lastFlight != null)
                EndCues(_lastFlight);
        }

        // The delivery at the release
        private void Release()
        {
            Transform user = User;
            switch (_asset.Delivery)
            {
                case StrikeDelivery strike:
                    if (_target != null && user != null &&
                        Vector3.Distance(user.position, _target.position) <= strike.Reach)
                        Act(_target.position, _target, _target);
                    break;

                case ProjectileDelivery projectile:
                    if (_target != null && user != null)
                    {
                        int flight = BeginFlight(_target.position, _target);
                        Projectiles.Launch(_context?.Launcher, new ProjectileLaunch(user, _target, _target.position, 0f,
                            _shot, _context?.Sight, projectile.Arc, 0f, null, null, this, flight));
                    }

                    break;

                case LobDelivery lob:
                    if (user != null)
                    {
                        Vector3 from = user.position + Vector3.up * lob.Hand;
                        float rise = Mathf.Max(LEAST_RISE, lob.Arc / Mathf.Max(0.5f, Vector3.Distance(from, _aimPoint)));
                        int flight = BeginFlight(_aimPoint, _target);
                        Projectiles.Launch(_context?.Launcher, new ProjectileLaunch(user, null, _aimPoint, 0f, _shot,
                            _context?.Sight, rise, lob.Flight, null, null, this, flight));
                    }

                    break;

                case SelfDelivery:
                    if (user != null)
                        Act(user.position, user, user);
                    break;
            }
        }

        // The effects where it acts. Who was hit is the delivery's to say when it decided it — a blow in reach, a projectile's
        // own flight by its hit radius — and the effects' own radius only for a lob to a place (one owner of the hit)
        private void Act(Vector3 point, Transform target, Transform struck)
        {
            if (_asset.Effects == null)
                return;

            bool decided = _asset.Delivery is not LobDelivery;
            var impact = new AbilityImpact(_context, User, target, point, struck, decided);
            for (int i = 0; i < _asset.Effects.Count; i++)
                _asset.Effects[i]?.Apply(impact);
        }

        // A throw leaves: a record of its own (a free one, or one more when more fly than ever before), its cues begun
        // where it will come down. Its number is the launch's token: the landing finds this throw and no other
        private int BeginFlight(Vector3 point, Transform target)
        {
            Flight flight = null;
            for (int i = 0; i < _flights.Count && flight == null; i++)
            {
                if (_flights[i].Serial == 0)
                    flight = _flights[i];
            }

            if (flight == null)
            {
                flight = NewFlight();
                _flights.Add(flight);
            }

            _serial = _serial == int.MaxValue ? 1 : _serial + 1;
            flight.Serial = _serial;
            flight.Target = target;
            _lastFlight = flight;
            if (_asset.Cues != null)
            {
                var at = new AbilityImpact(_context, User, target, point, null);
                for (int i = 0; i < _asset.Cues.Count && i < flight.Cues.Length; i++)
                    flight.Cues[i] = _asset.Cues[i]?.Begin(at);
            }

            return flight.Serial;
        }

        private void EndCues(Flight flight)
        {
            object[] made = flight.Cues;
            for (int i = 0; i < made.Length; i++)
            {
                if (made[i] == null)
                    continue;

                if (_asset.Cues != null && i < _asset.Cues.Count)
                    _asset.Cues[i]?.End(made[i]);
                made[i] = null;
            }
        }

        /// <summary>A throw of this runner came down: its cues end, its effects happen there.</summary>
        public void Landed(in ProjectileLaunch launch, Vector3 point, Transform struck)
        {
            int token = launch.Token;
            Flight flight = null;
            for (int i = 0; token != 0 && i < _flights.Count && flight == null; i++)
            {
                if (_flights[i].Serial == token)
                    flight = _flights[i];
            }

            if (flight == null)
                return;                                 // not in the air (told twice, or not this runner's)

            flight.Serial = 0;
            EndCues(flight);
            Transform target = flight.Target;
            flight.Target = null;
            Act(point, target, struck);
        }
    }
}
