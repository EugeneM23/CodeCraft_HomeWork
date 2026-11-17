#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Ticking
{
    /// <summary>
    /// Editor safety net of the physics freezers: the mode a freezer replaced is also kept in SessionState, and if Play
    /// Mode ends without its resume (a script reload in Play Mode loses the loop and its freezers), the mode is put
    /// back on the way to Edit Mode — Script must not stay in Project Settings and stop physics in the next session.
    /// </summary>
    internal static class FrozenPhysicsModes
    {
        public const string PHYSICS_2D = "Ticking.FrozenPhysics2DMode";
        public const string PHYSICS_3D = "Ticking.FrozenPhysicsMode";

        public static void Remember(string key, int mode) => SessionState.SetInt(key, mode);

        public static void Forget(string key) => SessionState.EraseInt(key);

        /// <summary>Puts back every mode no freezer gave back.</summary>
        public static void RestoreForgotten()
        {
#if GAMELOOP_PHYSICS_2D
            int mode2D = SessionState.GetInt(PHYSICS_2D, -1);
            if (mode2D >= 0)
                Physics2D.simulationMode = (SimulationMode2D)mode2D;
#endif
#if GAMELOOP_PHYSICS
            int mode3D = SessionState.GetInt(PHYSICS_3D, -1);
            if (mode3D >= 0)
                Physics.simulationMode = (SimulationMode)mode3D;
#endif
            Forget(PHYSICS_2D);
            Forget(PHYSICS_3D);
        }

        [InitializeOnLoadMethod]
        private static void WatchPlayMode() => EditorApplication.playModeStateChanged += HandlePlayModeChanged;

        private static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                RestoreForgotten();
        }
    }
}
#endif
