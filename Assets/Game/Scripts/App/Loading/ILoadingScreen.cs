namespace Game
{
    /// <summary>
    /// What covers the screen while GameFlow changes scenes. GameFlow calls Show at the start of a transition, waits
    /// for IsCovering before touching the scenes, sends SetProgress every frame and calls Hide at the end. Runs on
    /// real time: the game may be paused.
    /// </summary>
    public interface ILoadingScreen
    {
        /// <summary>Shown or being shown (between Show and Hide).</summary>
        bool IsShown { get; }

        /// <summary>The screen is fully covered: scenes may change under it unseen.</summary>
        bool IsCovering { get; }

        /// <summary>Covers the screen and blocks input; instant — at once, otherwise fades in.</summary>
        void Show(bool instant);

        /// <summary>0..1, the loading progress.</summary>
        void SetProgress(float value);

        /// <summary>Fades out and lets input through.</summary>
        void Hide();
    }
}
