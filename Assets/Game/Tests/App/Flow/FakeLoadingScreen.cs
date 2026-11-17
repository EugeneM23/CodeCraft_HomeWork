namespace Game.Tests
{
    /// <summary>A loading screen without a screen: covers at once unless told otherwise.</summary>
    public sealed class FakeLoadingScreen : ILoadingScreen
    {
        public bool CoverAtOnce { get; set; } = true;
        public bool Covered { get; set; }
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public bool LastInstant { get; private set; }
        public float LastProgress { get; private set; }

        public bool IsShown { get; private set; }
        public bool IsCovering => IsShown && Covered;

        public void Show(bool instant)
        {
            IsShown = true;
            Covered = CoverAtOnce || instant;
            LastInstant = instant;
            ShowCount++;
        }

        public void SetProgress(float value) => LastProgress = value;

        public void Hide()
        {
            IsShown = false;
            Covered = false;
            HideCount++;
        }
    }
}
