using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Puts the game's one IGameInput into the app with the player's saved key overrides loaded before any map
    /// is used. On the App prefab after SaveInstaller (it reads the settings store). The menus apply later
    /// changes of the settings (AppServices.EnsureSettingsApplied).
    /// </summary>
    public sealed class GameInputInstaller : MonoEntityInstaller<AppContext>
    {
        private GameInput _input;

        public override void Install(AppContext context)
        {
            if (!AtomicUtils.IsPlayMode())
                return;

            _input = new GameInput();
            if (context.TryGetSettings(out SettingsStore settings))
                SettingsApplyUseCase.ApplyControls(settings.Current.controls, _input);

            context.AddGameInput(_input);
        }

        public override void Uninstall(AppContext context)
        {
            if (_input == null)
                return;

            context.DelGameInput();
            _input.Controls.Dispose();
            _input = null;
        }
    }
}
