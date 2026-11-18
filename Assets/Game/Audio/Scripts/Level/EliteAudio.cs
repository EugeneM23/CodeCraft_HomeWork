using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The elite Regax's sounds, called by the AI team through <see cref="GameAudio.Elite"/> at the point of each
    /// action (AI limits the rate: Hurt at most twice a second). Silent while the level has no GameAudio.
    /// <see cref="Appeared"/> and <see cref="Gone"/> follow AI's EliteStatus (the warning, the elite music);
    /// <see cref="Died"/> follows the death report.
    /// </summary>
    public sealed class EliteAudio
    {
        private bool _present;

        /// <summary>An elite is on the arena (between Appeared and Gone).</summary>
        public bool Present => _present;

        /// <summary>An elite came onto the arena (EliteStatus.Appeared): the warning and the elite music.</summary>
        public void Appeared()
        {
            if (_present)
                return;

            _present = true;
            GameAudio.Run.EliteAppeared();
            if (AppAudio.Instance != null)
                AppAudio.Instance.SetEliteMusic(true);
        }

        /// <summary>The elite is gone (EliteStatus.Died): the battle music comes back.</summary>
        public void Gone()
        {
            if (!_present)
                return;

            _present = false;
            if (AppAudio.Instance != null)
                AppAudio.Instance.SetEliteMusic(false);
        }

        public void Roar(Vector3 point) => GameAudio.Play(AudioKeys.Elite.ROAR, point);

        public void Step(Vector3 point) => GameAudio.Play(AudioKeys.Elite.STEP, point);

        public void Dash(Vector3 point) => GameAudio.Play(AudioKeys.Elite.DASH, point);

        public void Leap(Vector3 point) => GameAudio.Play(AudioKeys.Elite.JUMP, point);

        public void Land(Vector3 point) => GameAudio.Play(AudioKeys.Elite.LAND, point);

        public void Claws(Vector3 point) => GameAudio.Play(AudioKeys.Elite.CLAWS, point);

        /// <summary>It tears a piece out of a house.</summary>
        public void TearChunk(Vector3 point) => GameAudio.Play(AudioKeys.Elite.TEAR, point);

        public void Throw(Vector3 point) => GameAudio.Play(AudioKeys.Elite.THROW, point);

        public void Hurt(Vector3 point) => GameAudio.Play(AudioKeys.Elite.HURT, point);

        /// <summary>Finished off by a crush at low health; Died follows.</summary>
        public void Finished(Vector3 point) => GameAudio.Play(AudioKeys.Elite.FINISHER, point);

        /// <summary>Its death cry and the burst into pieces (the death report with the Elite flag).</summary>
        public void Died(Vector3 point) => GameAudio.Play(AudioKeys.Elite.DEATH, point);

        /// <summary>The level is gone: no elite left, the elite music ends with the battle music.</summary>
        internal void Reset() => _present = false;
    }
}
