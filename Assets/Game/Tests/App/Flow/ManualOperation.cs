using Loading;

namespace Game.Tests
{
    /// <summary>A loading step the test finishes by hand.</summary>
    public sealed class ManualOperation : LoadingOperation
    {
        private float _progress;

        public ManualOperation(float weight = 1f) : base(weight)
        {
        }

        public bool Begun { get; private set; }

        public void SetProgress(float value) => _progress = value;

        public void Complete() => Succeed();

        public void FailWith(string error) => Fail(error);

        protected override void OnBegin() => Begun = true;

        protected override float MeasureProgress() => _progress;
    }
}
