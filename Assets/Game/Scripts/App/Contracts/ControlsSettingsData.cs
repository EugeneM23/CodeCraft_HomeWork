using System;

namespace Game
{
    [Serializable]
    public sealed class ControlsSettingsData
    {
        public float mouseSensitivity = 1f;
        /// <summary>InputActionAsset.SaveBindingOverridesAsJson(); empty — default bindings.</summary>
        public string bindingOverrides = "";
    }
}
