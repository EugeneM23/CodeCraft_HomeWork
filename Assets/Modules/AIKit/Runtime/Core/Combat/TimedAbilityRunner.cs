namespace AIKit
{
    /// <summary>A runner that needs time passing to play (the abilities tick it once per step, before anything else).</summary>
    public interface ITickedAbilityRunner : IAbilityRunner
    {
        void Tick(float dt);
    }

    /// <summary>
    /// An ability without content of its own — a prototype, a demo, a test: it plays WindUp, then activeTime, then
    /// Recovery seconds from its definition, with the phases those numbers make. An ability with a clip reads its phases
    /// from the clip's hit window instead (the definition's WindUp / Recovery numbers are for this runner only, so a
    /// number is never owned twice).
    /// </summary>
    public sealed class TimedAbilityRunner : ITickedAbilityRunner
    {
        private readonly AbilityDefinition _definition;
        private readonly float _activeTime;
        private float _elapsed = -1f;

        public TimedAbilityRunner(AbilityDefinition definition, float activeTime = 0.1f)
        {
            _definition = definition;
            _activeTime = activeTime;
        }

        public string Id => _definition?.Id;

        public bool IsRunning => _elapsed >= 0f && _elapsed < Length;

        public AbilityPhase Phase
        {
            get
            {
                if (!IsRunning)
                    return AbilityPhase.None;

                if (_elapsed < _definition.WindUp)
                    return AbilityPhase.WindUp;

                return _elapsed < _definition.WindUp + _activeTime ? AbilityPhase.Active : AbilityPhase.Recovery;
            }
        }

        private float Length => _definition.WindUp + _activeTime + _definition.Recovery;

        public bool CanStart(AgentContext context) => !IsRunning;

        public void Begin(AgentContext context) => _elapsed = 0f;

        public void Cancel() => _elapsed = -1f;

        public void Tick(float dt)
        {
            if (IsRunning)
                _elapsed += dt;
        }
    }
}
