using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Sounds of the run through the arenas, through <see cref="GameAudio.Run"/>: arena start and end, the gate,
    /// the curtain, experience, the elite's arrival, crushes. Called by the core's run events and the gameplay
    /// signals; every call is silent while the level has no GameAudio. A new level and the skill choice are UI
    /// sounds (<see cref="AudioKeys.UI"/>): they open over the paused battle, which freezes the level's sounds.
    ///
    /// Experience pickups climb a semitone a step while they come within <see cref="XP_WINDOW"/> s of each other
    /// (run.xp_streak); crushes within <see cref="CRUSH_WINDOW"/> s add tank.crush_streak over the crush itself.
    /// </summary>
    public sealed class RunAudio
    {
        public const float XP_WINDOW = 0.6f;

        /// <summary>An octave: run.xp_streak 1 is pitch ×2 (AudioParameterCurves).</summary>
        public const int XP_STEPS = 12;

        public const float CRUSH_WINDOW = 1.5f;
        public const int CRUSH_STEPS = 8;

        private readonly AudioStreak _xp = new(XP_WINDOW, XP_STEPS);
        private readonly AudioStreak _crush = new(CRUSH_WINDOW, CRUSH_STEPS);

        /// <summary>A crush reported again this close (m², s) is the same crush.</summary>
        private const float SAME_CRUSH_SQR = 1f;
        private const float SAME_CRUSH_TIME = 0.1f;

        private float _lastCrushTime = float.NegativeInfinity;
        private Vector3 _lastCrushPoint;

        public AudioStreak XpStreak => _xp;

        public AudioStreak CrushStreak => _crush;

        public void ArenaStarted() => GameAudio.Play(AudioKeys.Run.ARENA_START);

        public void ArenaCleared() => GameAudio.Play(AudioKeys.Run.ARENA_CLEARED);

        public void GateOpened() => GameAudio.Play(AudioKeys.Run.GATE_OPEN);

        /// <summary>The tank drives through the gate and the curtain falls.</summary>
        public void Transition() => GameAudio.Play(AudioKeys.Run.TRANSITION);

        public void EliteAppeared() => GameAudio.Play(AudioKeys.Run.ELITE_APPEAR);

        /// <summary>An experience clot was picked up: quiet, a semitone higher for each one in quick succession.</summary>
        public void XpPicked() => XpPicked(Time.time);

        public void XpPicked(float time)
        {
            _xp.Register(time);
            GameAudio.SetParameter(AudioKeys.Run.XP_STREAK, _xp.Level);
            GameAudio.Play(AudioKeys.Run.XP_PICKUP);
        }

        /// <summary>The tank crushed a monster: the crunch, and from the second in a streak the streak sound over it.</summary>
        public void Crushed(Vector3 point) => Crushed(point, Time.time);

        public void Crushed(Vector3 point, float time)
        {
            // An entity's crush is heard twice: the tank's GameSignals.Crushed and AI's death report, same moment and place.
            if (time - _lastCrushTime < SAME_CRUSH_TIME && (point - _lastCrushPoint).sqrMagnitude < SAME_CRUSH_SQR)
                return;

            _lastCrushTime = time;
            _lastCrushPoint = point;
            GameAudio.Play(AudioKeys.Monsters.CRUSHED, point);
            if (_crush.Register(time) == 0)
                return;

            GameAudio.SetParameter(AudioKeys.Tank.CRUSH_STREAK_LEVEL, _crush.Level);
            GameAudio.Play(AudioKeys.Tank.CRUSH_STREAK);
        }

        /// <summary>The level is gone: streaks start over.</summary>
        internal void Reset()
        {
            _xp.Reset();
            _crush.Reset();
            _lastCrushTime = float.NegativeInfinity;
        }
    }
}
