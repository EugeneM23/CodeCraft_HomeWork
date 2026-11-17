using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>Tools → Game → Cold Start: Play in the menu or a level scene starts the app right there (on by default).</summary>
    public static class ColdStartMenu
    {
        private const string MENU_PATH = "Tools/Game/Cold Start";

        [MenuItem(MENU_PATH, priority = 100)]
        private static void Toggle() => EditorColdStart.Enabled = !EditorColdStart.Enabled;

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MENU_PATH, EditorColdStart.Enabled);
            return true;
        }
    }
}
