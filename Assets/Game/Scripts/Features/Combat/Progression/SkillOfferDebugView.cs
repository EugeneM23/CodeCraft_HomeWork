using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Debug skill cards of <see cref="LocalRunService"/> until the menu's skill screen: the 3 offered skills on keys
    /// 1/2/3 and a line «Lv N  xp/next». Made only by LocalRunService.ShowDebugView; the texts are rebuilt only when
    /// the run's Revision changes.
    /// </summary>
    public sealed class SkillOfferDebugView : MonoBehaviour
    {
        private LocalRunService _run;
        private int _revision = -1;
        private string _xpLine = string.Empty;
        private readonly string[] _cards = new string[LocalRunService.OFFER_SIZE];
        private int _cardCount;
        private GUIStyle _box;

        public void Bind(LocalRunService run)
        {
            _run = run;
            _revision = -1;
        }

        private void Update() // perf-ok: ticks during pause (debug skill offer)
        {
            if (_run == null || _run.SkillOffer.Count == 0)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            int choice = keyboard.digit1Key.wasPressedThisFrame ? 0
                : keyboard.digit2Key.wasPressedThisFrame ? 1
                : keyboard.digit3Key.wasPressedThisFrame ? 2
                : -1;

            if (choice >= 0 && choice < _run.SkillOffer.Count)
                _run.ChooseSkill(_run.SkillOffer[choice]);
        }

        private void OnGUI() // perf-ok: debug UI until the menu's skill screen
        {
            if (_run == null)
                return;

            Refresh();
            _box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, fontSize = 16 };

            GUI.Label(new Rect(16f, Screen.height - 40f, 300f, 24f), _xpLine);

            if (_cardCount == 0)
                return;

            const float width = 360f;
            const float height = 40f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.35f;
            GUI.Box(new Rect(x, y - 34f, width, 30f), "LEVEL UP — choose a skill", _box);
            for (int i = 0; i < _cardCount; i++)
                GUI.Box(new Rect(x, y + i * (height + 6f), width, height), _cards[i], _box);
        }

        private void Refresh()
        {
            if (_revision == _run.Revision)
                return;

            _revision = _run.Revision;
            _xpLine = $"Lv {_run.Level}  {_run.Experience}/{_run.ExperienceToNextLevel}";

            _cardCount = Mathf.Min(_run.SkillOffer.Count, _cards.Length);
            for (int i = 0; i < _cardCount; i++)
            {
                SkillId skill = _run.SkillOffer[i];
                int level = _run.GetSkillLevel(skill);
                _cards[i] = $"[{i + 1}]  {skill.Value}   {level} → {level + 1}";
            }
        }
    }
}
