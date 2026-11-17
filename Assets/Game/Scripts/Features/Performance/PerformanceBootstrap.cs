using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Applies the performance tier at startup and to every scene that loads. Nothing is edited in
    /// scenes, prefabs or profile assets: everything is derived at runtime from what is already there.
    /// </summary>
    /// <remarks>
    /// Scene objects are changed from <see cref="SceneManager.sceneLoaded"/>: it runs after every Awake
    /// of the new scene and before any Start or the first rendered frame, so nothing is drawn
    /// with the desktop settings first. Pooled instances are handled by their pool
    /// (<see cref="VfxBudget"/>), HighlightPlus effects spawned any other way by
    /// <see cref="MobileHighlights.CatchNewlyEnabled"/> before each cull.
    ///
    /// Desktop keeps its look. Only what changes nothing on screen runs there: players write Log and
    /// Warning messages without stack traces.
    ///
    /// Switching to or from the Mobile quality level while playing re-applies the tier. Going back to
    /// desktop restores the frame rate, cameras and post-processing; the particle and highlight
    /// budgets stay until the scene is loaded again, because their desktop values are not kept.
    ///
    /// Everything here must stay inside play mode. The editor does not reload the domain when play mode
    /// ends, so these subscriptions would otherwise outlive it: a camera repaint or a quality level
    /// picked in the project settings would then rewrite the open scene's cameras and highlights,
    /// and a scene save would keep that. Every handler checks for play mode, and
    /// leaving it (Application.quitting is raised then too) drops all of them.
    /// </remarks>
    internal static class PerformanceBootstrap
    {
        // Without a target, Android and iOS present at 30 fps whatever the device can do.
        private const int MOBILE_TARGET_FRAME_RATE = 60;

        private static bool _mobile;
        private static bool _framePacingApplied;
        private static int _desktopTargetFrameRate;

        // Domain reload can be turned off for entering play mode; statics and subscriptions would then
        // survive from the previous session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Unsubscribe();
            _mobile = false;
            _framePacingApplied = false;
            MobilePostProcessing.Reset();
            MobileHighlights.Reset();
            VfxBudget.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Unsubscribe();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            QualitySettings.activeQualityLevelChanged += OnQualityLevelChanged;
            Camera.onPreCull += OnCameraPreCull;
            Application.quitting += OnQuitting;

            _mobile = PerformanceTier.IsMobile;
            ApplyFramePacing(_mobile);

#if !UNITY_EDITOR
            // A player walks and symbolicates the managed stack for every Log and Warning it writes,
            // which costs far more than the message. Errors and exceptions keep their traces. Players
            // only: in the editor these switches are the Console's own stack-trace settings.
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif
        }

        private static void Unsubscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            QualitySettings.activeQualityLevelChanged -= OnQualityLevelChanged;
            Camera.onPreCull -= OnCameraPreCull;
            Application.quitting -= OnQuitting;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
                return;

            if (!_mobile)
                return;

            MobilePostProcessing.Apply(scene);
            MobileHighlights.ApplyToScene(scene, false);
        }

        // Only destroys this class's own profile copies, so it needs no play-mode check.
        private static void OnSceneUnloaded(Scene scene) => MobilePostProcessing.Release(scene);

        private static void OnQuitting()
        {
            Unsubscribe();
            MobilePostProcessing.ReleaseAll();

            // Engine-wide statics are not reset when the editor leaves play mode.
            ApplyFramePacing(false);
        }

        private static void OnQualityLevelChanged(int previousLevel, int currentLevel)
        {
            if (!Application.isPlaying)
                return;

            bool mobile = PerformanceTier.IsMobile;
            if (mobile == _mobile)
                return;

            _mobile = mobile;
            ApplyFramePacing(mobile);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                if (mobile)
                {
                    MobilePostProcessing.Apply(scene);
                    MobileHighlights.ApplyToScene(scene, true);
                }
                else
                {
                    MobilePostProcessing.Restore(scene);
                }
            }
        }

        private static void OnCameraPreCull(Camera camera)
        {
            if (!_mobile || !Application.isPlaying)
                return;

            MobileHighlights.CatchNewlyEnabled();
            MobilePostProcessing.ClearStaleDepthFlags();
        }

#if UNITY_EDITOR
        private const string EDITOR_FRAME_RATE_KEY = "Game.PerformanceBootstrap.EditorTargetFrameRate";

        // A frame rate written from Application.quitting does not stick when the editor leaves play
        // mode: after a session on the Mobile level the editor stayed throttled to the mobile 60 fps.
        // So the editor's own value is saved on the way into play mode (SessionState survives the
        // domain reload that follows) and put back once the editor is in edit mode again.
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditorPlayMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        private static void OnEditorPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            {
                UnityEditor.SessionState.SetInt(EDITOR_FRAME_RATE_KEY, Application.targetFrameRate);
            }
            else if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                Application.targetFrameRate = UnityEditor.SessionState.GetInt(EDITOR_FRAME_RATE_KEY, Application.targetFrameRate);
                UnityEditor.SessionState.EraseInt(EDITOR_FRAME_RATE_KEY);
            }
        }
#endif

        private static void ApplyFramePacing(bool mobile)
        {
            if (mobile)
            {
                if (!_framePacingApplied)
                {
                    _desktopTargetFrameRate = Application.targetFrameRate;
                    _framePacingApplied = true;
                }

                // A non-zero vSyncCount overrides the target. It is written only when it differs: the
                // Mobile level already ships with 0, and in the editor a value written during play can
                // stay in the project's quality settings afterwards.
                if (QualitySettings.vSyncCount != 0)
                    QualitySettings.vSyncCount = 0;

                Application.targetFrameRate = MOBILE_TARGET_FRAME_RATE;
            }
            else if (_framePacingApplied)
            {
                // The desktop level brings its own vSyncCount back; only the target was changed here.
                Application.targetFrameRate = _desktopTargetFrameRate;
                _framePacingApplied = false;
            }
        }
    }
}
