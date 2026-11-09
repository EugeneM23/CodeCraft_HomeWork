using UnityEngine;
using UnityEngine.UI;

namespace BallArena
{
    /// <summary>
    /// Реакция на изменение, а не опрос каждый кадр. Подписка на статическое событие:
    /// MatchManager спавнится в рантайме, и сослаться на него из сцены нельзя.
    /// </summary>
    public sealed class ScoreView : MonoBehaviour
    {
        [SerializeField]
        private Text _label;

        private void OnEnable()
        {
            MatchManager.ScoreChanged += this.OnScoreChanged;
            this.OnScoreChanged(0, 0);
        }

        private void OnDisable()
        {
            MatchManager.ScoreChanged -= this.OnScoreChanged;
        }

        private void OnScoreChanged(int left, int right)
        {
            _label.text = $"{left} : {right}";
        }
    }
}
