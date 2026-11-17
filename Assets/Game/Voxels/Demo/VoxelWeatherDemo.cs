using UnityEngine;

namespace Game.Voxels.Demo
{
    /// <summary>
    /// Sandbox keys for the weather: B sets a smoke cloud off where the cursor points, N turns
    /// the rain on and off.
    /// </summary>
    public sealed class VoxelWeatherDemo : MonoBehaviour
    {
        [SerializeField] private VoxelRain _rain;
        [Tooltip("B sets smoke off at the cursor.")]
        [SerializeField] private bool _smokeKey;

        /// <summary>Whether B sets smoke off (the HUD shows the key only then).</summary>
        public static bool SmokeKey { get; private set; }

        /// <summary>Whether there is rain for N to turn on and off.</summary>
        public static bool RainKey { get; private set; }

        private void OnEnable()
        {
            SmokeKey = _smokeKey;
            RainKey = _rain != null;
        }

        private void OnDisable()
        {
            SmokeKey = false;
            RainKey = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.N) && _rain != null)
                _rain.enabled = !_rain.enabled;

            if (_smokeKey && Input.GetKeyDown(KeyCode.B))
                SmokeAtCursor();
        }

        private static void SmokeAtCursor()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            // A locked cursor (the sandbox's mouse look) aims with the crosshair in the middle.
            Vector3 screen = Cursor.lockState == CursorLockMode.Locked
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : Input.mousePosition;
            Ray ray = camera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out RaycastHit hit, 300f, VoxelSmoke.WallMask, QueryTriggerInteraction.Ignore))
                VoxelSmoke.Deploy(hit.point + hit.normal * 0.1f);
        }
    }
}
