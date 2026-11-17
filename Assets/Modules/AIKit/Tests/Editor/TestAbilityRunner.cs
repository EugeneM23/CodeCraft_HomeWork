namespace AIKit.Tests
{
    /// <summary>An ability that simply takes time: the test controls how long it plays and counts its starts.</summary>
    internal sealed class TestAbilityRunner : IAbilityRunner
    {
        private float _left;

        public TestAbilityRunner(string id, float duration = 0f)
        {
            Id = id;
            Duration = duration;
        }

        public string Id { get; }

        public float Duration { get; set; }

        public int Starts { get; private set; }

        public int Cancels { get; private set; }

        public bool Blocked { get; set; }

        public bool IsRunning => _left > 0f;

        public bool CanStart(AgentContext context) => !Blocked && !IsRunning;

        public void Begin(AgentContext context)
        {
            Starts++;
            _left = Duration;
        }

        public void Cancel()
        {
            Cancels++;
            _left = 0f;
        }

        public void Tick(float dt)
        {
            if (_left > 0f)
                _left -= dt;
        }
    }
}
