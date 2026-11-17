using System;
using AudioEngine;
using Game.Voxels;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Sounds of the moments without an entity event: blasts, carved and broken voxels, mud hits, crushes, the
    /// air strike. Listens to <see cref="GameSignals"/> and <see cref="VoxelSignals"/> while its GameAudio is
    /// enabled. Debris landing after a big carve and the bombs whistling are kept in fixed buffers: no
    /// allocation per signal or per frame.
    /// </summary>
    public sealed class GameSignalAudio
    {
        /// <summary>A carve smaller than this (m) is a bullet hole: an impact, not a break.</summary>
        private const float SMALL_CARVE_RADIUS = 0.6f;

        /// <summary>A carve taking more voxels than this rains debris down a moment later.</summary>
        private const int DEBRIS_VOXELS = 30;

        private const float DEBRIS_DELAY_MIN = 0.5f;
        private const float DEBRIS_DELAY_MAX = 0.9f;
        private const int DEBRIS_CAPACITY = 16;

        private const int BOMB_CAPACITY = 8;
        private const float WHISTLE_FADE = 0.1f;

        private readonly Action<Vector3, float> _onExploded;
        private readonly Action<Vector3, float> _onMudHit;
        private readonly Action<Vector3, float> _onCrushed;
        private readonly Action _onAirStrikeCalled;
        private readonly Action<Transform> _onAirBombDropped;
        private readonly Action<Vector3, float, int> _onCarved;
        private readonly Action<Vector3, float, int> _onAirBlasted;
        private readonly Action<Vector3, float> _onFractured;

        private readonly Vector3[] _debrisPoints = new Vector3[DEBRIS_CAPACITY];
        private readonly float[] _debrisTimers = new float[DEBRIS_CAPACITY];
        private int _debrisCount;

        private readonly Transform[] _bombs = new Transform[BOMB_CAPACITY];
        private readonly AudioEventHandle[] _whistles = new AudioEventHandle[BOMB_CAPACITY];
        private int _bombCount;

        private bool _subscribed;

        /// <summary>An ExplosionEvents blast this close (m²) to the frame's air blast is that same bomb.</summary>
        private const float AIR_BLAST_MATCH_SQR = 1f;

        private int _airBlastFrame = -1;
        private Vector3 _airBlastPoint;

        public GameSignalAudio()
        {
            _onExploded = OnExploded;
            _onMudHit = OnMudHit;
            _onCrushed = OnCrushed;
            _onAirStrikeCalled = OnAirStrikeCalled;
            _onAirBombDropped = OnAirBombDropped;
            _onCarved = OnCarved;
            _onAirBlasted = OnAirBlasted;
            _onFractured = OnFractured;
        }

        public void Subscribe()
        {
            if (_subscribed)
                return;

            _subscribed = true;
            ExplosionEvents.Exploded += _onExploded;
            GameSignals.MudHit += _onMudHit;
            GameSignals.Crushed += _onCrushed;
            GameSignals.AirStrikeCalled += _onAirStrikeCalled;
            GameSignals.AirBombDropped += _onAirBombDropped;
            VoxelSignals.Carved += _onCarved;
            VoxelSignals.AirBlasted += _onAirBlasted;
            VoxelSignals.Fractured += _onFractured;
        }

        public void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _subscribed = false;
            ExplosionEvents.Exploded -= _onExploded;
            GameSignals.MudHit -= _onMudHit;
            GameSignals.Crushed -= _onCrushed;
            GameSignals.AirStrikeCalled -= _onAirStrikeCalled;
            GameSignals.AirBombDropped -= _onAirBombDropped;
            VoxelSignals.Carved -= _onCarved;
            VoxelSignals.AirBlasted -= _onAirBlasted;
            VoxelSignals.Fractured -= _onFractured;

            _debrisCount = 0;
            for (int i = 0; i < _bombCount; i++)
            {
                GameAudio.StopLoop(ref _whistles[i], 0f);
                _bombs[i] = null;
            }
            _bombCount = 0;
        }

        /// <summary>Plays the debris that is due and lets the whistle of an exploded bomb go.</summary>
        public void Tick(float deltaTime)
        {
            for (int i = _debrisCount - 1; i >= 0; i--)
            {
                _debrisTimers[i] -= deltaTime;
                if (_debrisTimers[i] > 0f)
                    continue;

                GameAudio.Play(AudioKeys.Destruction.DEBRIS_LAND, _debrisPoints[i]);
                _debrisCount--;
                _debrisPoints[i] = _debrisPoints[_debrisCount];
                _debrisTimers[i] = _debrisTimers[_debrisCount];
            }

            for (int i = _bombCount - 1; i >= 0; i--)
            {
                Transform bomb = _bombs[i];
                if (bomb != null && bomb.gameObject.activeInHierarchy && _whistles[i].IsValid)
                {
                    _whistles[i].SetPosition(bomb.position);
                    continue;
                }

                GameAudio.StopLoop(ref _whistles[i], WHISTLE_FADE);
                _bombCount--;
                _bombs[i] = _bombs[_bombCount];
                _whistles[i] = _whistles[_bombCount];
                _bombs[_bombCount] = null;
                _whistles[_bombCount] = default;
            }
        }

        // Every blast comes through the AI team's ExplosionEvents, the air bomb too: that one already sounded as the
        // large explosion from its AirBlasted (raised first, in the same frame and at the same point), so skip it.
        private void OnExploded(Vector3 point, float radius)
        {
            if (_airBlastFrame == Time.frameCount && (point - _airBlastPoint).sqrMagnitude < AIR_BLAST_MATCH_SQR)
                return;

            GameAudio.Play(AudioKeys.Destruction.EXPLOSION_SMALL, point);
        }

        private static void OnMudHit(Vector3 point, float size) =>
            GameAudio.Play(AudioKeys.Destruction.MUD_SPLASH, point);

        private static void OnCrushed(Vector3 point, float speed) =>
            GameAudio.Play(AudioKeys.Monsters.CRUSHED, point);

        private static void OnAirStrikeCalled() => GameAudio.Play(AudioKeys.Weapons.AIRSTRIKE_CALL);

        // The module has no per-play volume, so a fracture sounds the same at any impact speed.
        private static void OnFractured(Vector3 point, float speed) =>
            GameAudio.Play(AudioKeys.Destruction.VOXEL_FRACTURE, point);

        private void OnAirBlasted(Vector3 point, float radius, int voxels)
        {
            _airBlastFrame = Time.frameCount;
            _airBlastPoint = point;
            GameAudio.Play(AudioKeys.Destruction.EXPLOSION_LARGE, point);
            GameAudio.Play(AudioKeys.Destruction.VOXEL_BREAK, point);
            QueueDebris(point, voxels);
        }

        private void OnCarved(Vector3 point, float radius, int voxels)
        {
            if (voxels <= 0)
                return;

            GameAudio.Play(radius < SMALL_CARVE_RADIUS ? AudioKeys.Weapons.IMPACT_VOXEL : AudioKeys.Destruction.VOXEL_BREAK,
                point);
            QueueDebris(point, voxels);
        }

        private void QueueDebris(Vector3 point, int voxels)
        {
            if (voxels <= DEBRIS_VOXELS || _debrisCount >= DEBRIS_CAPACITY)
                return;

            _debrisPoints[_debrisCount] = point;
            _debrisTimers[_debrisCount] = Random.Range(DEBRIS_DELAY_MIN, DEBRIS_DELAY_MAX);
            _debrisCount++;
        }

        private void OnAirBombDropped(Transform bomb)
        {
            if (bomb == null || _bombCount >= BOMB_CAPACITY)
                return;

            // Moved along each frame rather than made to follow the bomb: the AudioSystem would read the bomb's
            // transform after it is destroyed.
            if (!GameAudio.StartLoop(AudioKeys.Weapons.AIRSTRIKE_WHISTLE, bomb.position, out AudioEventHandle whistle))
                return;

            _bombs[_bombCount] = bomb;
            _whistles[_bombCount] = whistle;
            _bombCount++;
        }
    }
}
