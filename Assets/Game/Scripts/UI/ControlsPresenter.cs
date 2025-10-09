using System;
using SaveLoadSystem;

namespace Game.Gameplay
{
    public sealed class ControlsPresenter : IControlsPresenter
    {
        private readonly SaveLoadService _service;

        public ControlsPresenter(SaveLoadService service)
        {
            _service = service;
        }

        public void Save(Action<bool, int> callback)
        {
            //TODO:
            bool result = _service.Save();
            callback.Invoke(result, -1);
        }

        public void Load(string versionText, Action<bool, int> callback)
        {
            //TODO:
            bool result = _service.Load();
            callback.Invoke(result, -1);
        }
    }
}