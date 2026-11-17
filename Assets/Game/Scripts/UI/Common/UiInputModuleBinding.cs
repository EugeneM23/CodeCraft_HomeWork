using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Game
{
    /// <summary>
    /// Points the scene's InputSystemUIInputModule at the game's one GameControls (UI map), so the
    /// menus and the key overrides use the same actions. Sits on the EventSystem object.
    /// </summary>
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public sealed class UiInputModuleBinding : MonoBehaviour
    {
        private void Awake()
        {
            InputSystemUIInputModule module = GetComponent<InputSystemUIInputModule>();
            GameControls controls = AppServices.Input.Controls;
            AppServices.EnsureSettingsApplied(); // saved volumes and keys before the first frame

            module.actionsAsset = controls.asset;
            module.point = InputActionReference.Create(controls.UI.Point);
            module.leftClick = InputActionReference.Create(controls.UI.Click);
            module.rightClick = InputActionReference.Create(controls.UI.RightClick);
            module.middleClick = InputActionReference.Create(controls.UI.MiddleClick);
            module.scrollWheel = InputActionReference.Create(controls.UI.ScrollWheel);
            module.move = InputActionReference.Create(controls.UI.Navigate);
            module.submit = InputActionReference.Create(controls.UI.Submit);
            module.cancel = InputActionReference.Create(controls.UI.Cancel);
        }
    }
}
