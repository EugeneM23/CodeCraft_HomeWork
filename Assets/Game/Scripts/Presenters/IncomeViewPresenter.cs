using System;
using Modules.Planets;
using Zenject;

namespace Game.Views
{
    public class IncomeViewPresenter : ITickable, IInitializable, IDisposable, IIncomeViewPresenter
    {
        private readonly Planet _planet;

        public event Action<float, string> OnIncomeTimeChanged;
        public event Action<bool> OnStateChanged;
        public bool IsPlanetUnlocked => _planet.IsUnlocked;

        private bool _previousState;

        public IncomeViewPresenter(Planet planet) => _planet = planet;

        public void Initialize() => _planet.OnIncomeTimeChanged += UpdateProgress;

        public void Dispose() => _planet.OnIncomeTimeChanged -= UpdateProgress;

        public void Tick()
        {
            if (ShouldUpdateState(out bool currentState))
                OnStateChanged?.Invoke(currentState);
        }

        private bool ShouldUpdateState(out bool currentState)
        {
            currentState = _planet.IsUnlocked && !_planet.IsIncomeReady;

            if (_previousState != currentState)
            {
                _previousState = currentState;
                return true;
            }

            return false;
        }

        public void UpdateProgress(float timeLeft)
        {
            float progress = _planet.IncomeProgress;
            string timeText = GetTimeText(timeLeft);

            OnIncomeTimeChanged?.Invoke(progress, timeText);
        }

        private string GetTimeText(float timeLeft)
        {
            int minutes = TimeSpan.FromSeconds(timeLeft).Minutes;
            int seconds = TimeSpan.FromSeconds(timeLeft).Seconds;
            string text = $"{minutes}m " + $": {seconds}s";

            return text;
        }
    }
}