using System.Collections;
using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// VoxelSandbox starts with the player already in the tank: one frame after the scene is up
    /// (the contexts and the player's character are installed by then), the tank's own "get in"
    /// action runs for the player's character, exactly as pressing E next to it would.
    /// Each cannon shot then shakes the tank like a wet dog: mud and blood fly off it, and a few
    /// shots in a row leave it clean (<see cref="ShotShare"/>).
    /// </summary>
    public sealed class VoxelSandboxStart : MonoBehaviour
    {
        [SerializeField] private MonoEntity _vehicle;

        /// <summary>The vehicle the player starts in (the tank).</summary>
        public MonoEntity Vehicle => _vehicle;

        // Was `_mudShakenPerShot` (0.3 of the loose mud, stains stayed). Renamed so the value the
        // scene holds gives way.
        [Tooltip("Cannon shots in a row that shake the tank clean of mud and blood: the first takes a third of what is on it, the second half of the rest, the third all of it.")]
        [Min(1)]
        [SerializeField] private int _shotsToClean = 3;

        [Tooltip("Seconds without a shot after which the count of shots in a row starts over. The cannon reloads in 2 s.")]
        [SerializeField] private float _shotSeriesGap = 15f;

        // Mud no longer slows the tank: it runs through it as on grass (`_mudSpeed` 0.6 is gone).
        [Tooltip("Top speed on dry ground (dry grass, bare earth, rock, streets), as a share of the tank's own.")]
        [SerializeField] private float _drySpeed = 1.2f;

        private IEvent _fire;
        private VoxelTrackRuts _mud;
        private TankDirtBehaviour _dirt;
        private IReactiveVariable<float> _moveSpeed;
        private float _baseSpeed;
        private int _shotsInRow;
        private float _lastShot = float.NegativeInfinity;

        /// <summary>
        /// Share of what is still on the tank that shot <paramref name="shot"/> of a row (from 1)
        /// shakes off, so that shot <paramref name="shotsToClean"/> leaves it clean: 1/3, 1/2, 1.
        /// </summary>
        public static float ShotShare(int shot, int shotsToClean)
        {
            int left = Mathf.Max(1, shotsToClean - Mathf.Max(1, shot) + 1);
            return 1f / left;
        }

        private IEnumerator Start()
        {
            yield return null;

            if (_vehicle == null || GameContext.Instance == null)
                yield break;

            IEntity character = GameContext.Instance.GetPlayerContext().GetCharacter().Value;
            if (character != null && _vehicle.TryGetInteractAction(out var getIn))
                getIn.Invoke(character);

            _mud = _vehicle.GetComponent<VoxelTrackRuts>();
            _vehicle.TryGetBehaviour(out _dirt);
            if (_mud != null && _vehicle.TryGetWeapon(out IReactiveVariable<IEntity> weapon) && weapon.Value != null &&
                weapon.Value.TryGetFireEvent(out _fire))
                _fire.Subscribe(OnFire);

            if (_vehicle.TryGetMoveSpeed(out _moveSpeed))
                _baseSpeed = _moveSpeed.Value;
        }

        /// <summary>
        /// The ground under the tracks sets the top speed: hard dry ground lets the tank run, grass
        /// and mud keep it at its own. The tank's drive eases toward the new top speed with its own
        /// acceleration.
        /// </summary>
        private void Update()
        {
            if (_moveSpeed == null || _mud == null)
                return;

            float dry = Mathf.Clamp01(_mud.DryShare);
            _moveSpeed.Value = _baseSpeed * Mathf.Lerp(1f, _drySpeed, dry);
        }

        private void OnDestroy() => _fire?.Unsubscribe(OnFire);

        private void OnFire()
        {
            if (_mud == null)
                return;

            // A row is shots no more than the gap apart; once it has shaken the tank clean, the next starts a new one.
            if (Time.time - _lastShot > _shotSeriesGap || _shotsInRow >= _shotsToClean)
                _shotsInRow = 0;
            _lastShot = Time.time;
            _shotsInRow++;

            float share = ShotShare(_shotsInRow, _shotsToClean);
            // The dog shakes one way, then back the other.
            _mud.Shudder(share, _shotsInRow % 2 == 1 ? 1f : -1f);
            _dirt?.Shed(share);
        }
    }
}
