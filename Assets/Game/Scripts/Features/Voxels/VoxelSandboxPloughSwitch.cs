using Game.Voxels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>VoxelSandbox: the Sandbox/Plough key (M) switches the tank's plough preset; the HUD shows which.</summary>
    public sealed class VoxelSandboxPloughSwitch : MonoBehaviour
    {
        [SerializeField] private TankInstaller _tank;

        private System.Func<string> _hudLine;
        private string _hudText;
        private TankPloughConfig _shown;

        private void OnEnable()
        {
            _hudLine ??= HudLine;
            VoxelHudLines.Add(_hudLine);
        }

        private void OnDisable() => VoxelHudLines.Remove(_hudLine);

        private void Update() // perf-ok: the sandbox key reads input every frame; one flag check
        {
            if (_tank != null && AppServices.Input.Controls.Sandbox.Plough.WasPressedThisFrame())
                _tank.NextPloughPreset();
        }

        // Rebuilt only when the preset changes: no allocation otherwise.
        private string HudLine()
        {
            TankPloughConfig preset = _tank != null ? _tank.PloughPreset : null;
            if (_hudText != null && preset == _shown)
                return _hudText;
            _shown = preset;
            string key = AppServices.Input.Controls.Sandbox.Plough.GetBindingDisplayString();
            _hudText = $"{key} — tank plough preset (now {(preset != null ? preset.DisplayName : "none")})";
            return _hudText;
        }
    }
}
