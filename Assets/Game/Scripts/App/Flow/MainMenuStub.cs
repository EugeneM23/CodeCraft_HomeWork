using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The core's main menu until the menu team's: Play opens the first level of the catalog, Quit leaves the game.
    /// The buttons are set in the Inspector. Without the app (the scene opened with cold start off) does nothing.
    /// </summary>
    public sealed class MainMenuStub : MonoBehaviour
    {
        [SerializeField] private Button _play;
        [SerializeField] private Button _quit;

        private void OnEnable()
        {
            if (_play != null)
                _play.onClick.AddListener(Play);
            if (_quit != null)
                _quit.onClick.AddListener(Quit);
        }

        private void OnDisable()
        {
            if (_play != null)
                _play.onClick.RemoveListener(Play);
            if (_quit != null)
                _quit.onClick.RemoveListener(Quit);
        }

        private void Play()
        {
            if (!TryGetFlow(out GameFlow flow))
                return;

            LevelConfig level = flow.Catalog.FirstLevel;
            if (level == null)
            {
                Debug.LogError($"{nameof(MainMenuStub)}: the level catalog has no levels", this);
                return;
            }

            flow.OpenLevel(level);
        }

        private void Quit()
        {
            if (TryGetFlow(out GameFlow flow))
                flow.Quit();
        }

        private bool TryGetFlow(out GameFlow flow)
        {
            flow = null;
            if (AppContext.TryGet(out AppContext app) && app.TryGetGameFlow(out flow))
                return true;

            Debug.LogWarning($"{nameof(MainMenuStub)}: the app is not running (Play from the Boot scene)", this);
            return false;
        }
    }
}
