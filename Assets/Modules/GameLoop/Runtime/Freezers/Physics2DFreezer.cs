#if GAMELOOP_PHYSICS_2D
using UnityEngine;

namespace Ticking
{
    /// <summary>
    /// Stops the 2D physics world while paused: Physics2D.simulationMode is Script, and nobody calls Simulate. Bodies
    /// keep their velocities and contacts (unlike Rigidbody2D.simulated = false, which drops contacts and sends exits).
    /// Resume brings back the mode that was there — also an edited Project Settings value must not stay Script; in the
    /// editor the mode is kept for the way out of Play Mode too, should the freezer itself be lost.
    /// A game that simulates physics itself (Script mode) has to do it from the loop to stand still.
    /// </summary>
    public sealed class Physics2DFreezer : IPauseListener
    {
        private SimulationMode2D _mode;
        private bool _frozen;

        public void OnPause()
        {
            if (_frozen)
                return;

            _mode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            _frozen = true;
#if UNITY_EDITOR
            FrozenPhysicsModes.Remember(FrozenPhysicsModes.PHYSICS_2D, (int)_mode);
#endif
        }

        public void OnResume()
        {
            if (!_frozen)
                return;

            Physics2D.simulationMode = _mode;
            _frozen = false;
#if UNITY_EDITOR
            FrozenPhysicsModes.Forget(FrozenPhysicsModes.PHYSICS_2D);
#endif
        }
    }
}
#endif
