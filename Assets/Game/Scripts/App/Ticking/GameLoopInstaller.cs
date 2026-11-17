using Atomic.Elements;
using Atomic.Entities;
using Game.GameLoop.Adapters;
using Ticking;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Puts the game loop into the app: the loop of the GameLoopRunner on the App prefab becomes the GameLoop service,
    /// the Atomic entities and the voxels tick from it (<see cref="AtomicTickBridge"/>, <see cref="VoxelTickBridge"/>),
    /// and game MonoBehaviours find it through <see cref="GameTicks"/>. First in the app's installer list: the other
    /// installers read the loop from the context. Play Mode only; the bridges come off on Uninstall or with the app.
    /// </summary>
    public sealed class GameLoopInstaller : MonoEntityInstaller<AppContext>
    {
        [Tooltip("The runner on the App prefab that drives the loop from Unity's Update, FixedUpdate and LateUpdate")]
        [SerializeField] private GameLoopRunner _runner;

        private IGameLoop _loop;
        private AtomicTickBridge _atomic;
        private VoxelTickBridge _voxels;

        public override void Install(AppContext context)
        {
            if (!AtomicUtils.IsPlayMode() || _loop != null)
                return;

            if (_runner == null)
            {
                Debug.LogError($"{nameof(GameLoopInstaller)}: no GameLoopRunner set, the game runs without its loop", this);
                return;
            }

            _loop = _runner.Loop;
            context.AddGameLoop(_loop);

            _atomic = new AtomicTickBridge();
            _voxels = new VoxelTickBridge(_loop);
            _loop.Add(_atomic);
            _loop.Add(_voxels);
            GameTicks.Bind(_loop, context);
        }

        public override void Uninstall(AppContext context)
        {
            if (_loop == null)
                return;

            context.DelGameLoop();
            TearDown();
        }

        // The app can go without Uninstall (uninstallOnDestroy off): Atomic and the voxels must not stay switched off
        private void OnDestroy() => TearDown();

        private void TearDown()
        {
            if (_loop == null)
                return;

            _loop.Remove(_atomic);
            _loop.Remove(_voxels);
            _atomic.Dispose();
            _voxels.Dispose();
            GameTicks.Unbind(_loop);

            _atomic = null;
            _voxels = null;
            _loop = null;
        }
    }
}
