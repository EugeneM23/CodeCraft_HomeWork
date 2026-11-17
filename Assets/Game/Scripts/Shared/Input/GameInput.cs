using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The game's GameControls with the player's key overrides. All maps start enabled; the battle UI
    /// switches the gameplay maps (Tank, Sandbox) off while a menu covers the battle.
    /// </summary>
    public sealed class GameInput : IGameInput
    {
        public GameControls Controls { get; }

        public float LookSensitivity { get; set; } = 1f;

        public GameInput()
        {
            Controls = new GameControls();
            Controls.Enable();
        }

        public void LoadOverrides(string json)
        {
            Controls.asset.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(json))
                Controls.asset.LoadBindingOverridesFromJson(json);
        }

        public string SaveOverrides() => Controls.asset.SaveBindingOverridesAsJson();

        public void SetGameplayEnabled(bool enabled)
        {
            if (enabled)
            {
                Controls.Tank.Enable();
                Controls.Sandbox.Enable();
            }
            else
            {
                Controls.Tank.Disable();
                Controls.Sandbox.Disable();
            }
        }
    }
}
