#if GAMELOOP_PHYSICS
using UnityEngine;

namespace Ticking
{
    /// <summary>The 3D twin of <see cref="Physics2DFreezer"/>: Physics.simulationMode is Script while paused.</summary>
    public sealed class PhysicsFreezer : IPauseListener
    {
        private SimulationMode _mode;
        private bool _frozen;

        public void OnPause()
        {
            if (_frozen)
                return;

            _mode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            _frozen = true;
#if UNITY_EDITOR
            FrozenPhysicsModes.Remember(FrozenPhysicsModes.PHYSICS_3D, (int)_mode);
#endif
        }

        public void OnResume()
        {
            if (!_frozen)
                return;

            Physics.simulationMode = _mode;
            _frozen = false;
#if UNITY_EDITOR
            FrozenPhysicsModes.Forget(FrozenPhysicsModes.PHYSICS_3D);
#endif
        }
    }
}
#endif
