using System;
using System.Collections.Generic;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Overlay over the hangar: the owned modules of one slot, the tank's numbers «было → стало» for the hovered or
    /// selected module, its description and Install (free). Plays Install or Error by the result. Items are cloned
    /// from a switched-off template and reused.
    /// </summary>
    public sealed class SlotAssemblyScreen : UIScreen
    {
        public event Action CloseClicked;

        [SerializeField] private TMP_Text _title;
        [Tooltip("Switched-off module item; cloned as needed")]
        [SerializeField] private HangarItemView _itemTemplate;
        [SerializeField] private HangarStatsView _stats;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private Button _install;
        [SerializeField] private TMP_Text _installLabel;
        [SerializeField] private Button _close;

        private readonly List<HangarItemView> _items = new();
        private readonly List<string> _ids = new();
        private IHangarSource _source;
        private string _slot;
        private string _selected;
        private string _preview;
        private string _levelFormat;

        /// <summary>The slot to show; called by MenuFlow before pushing the screen.</summary>
        public void ShowSlot(string slotId)
        {
            _slot = slotId;
            _selected = null;
            if (isActiveAndEnabled)
                Rebuild();
        }

        private void Awake()
        {
            _source = AppServices.Hangar;
            _levelFormat = UiStrings.Get("hangar.level_format");
        }

        private void OnEnable()
        {
            _install.onClick.AddListener(HandleInstall);
            _close.onClick.AddListener(HandleClose);
            _source.Changed += Refresh;
            Rebuild();
        }

        private void OnDisable()
        {
            _install.onClick.RemoveListener(HandleInstall);
            _close.onClick.RemoveListener(HandleClose);
            _source.Changed -= Refresh;
        }

        // Hover selects (SelectOnPointerEnter): the selected item previews its numbers, else the clicked one does
        protected override void Update() // perf-ok: ticks during pause (menu)
        {
            base.Update();

            EventSystem events = EventSystem.current;
            GameObject current = events != null ? events.currentSelectedGameObject : null;
            string preview = _selected;
            for (int i = 0; i < _ids.Count; i++)
            {
                if (_items[i].Button.gameObject == current)
                {
                    preview = _ids[i];
                    break;
                }
            }

            if (preview != _preview)
            {
                _preview = preview;
                RefreshStats();
            }
        }

        private void Rebuild()
        {
            _title.text = HangarLook.SlotName(_slot);
            _ids.Clear();
            IReadOnlyList<HangarModule> modules = _source.ModulesFor(_slot);
            for (int i = 0; i < modules.Count; i++)
                if (modules[i].Owned)
                    _ids.Add(modules[i].Id);

            while (_items.Count < _ids.Count)
            {
                int index = _items.Count;
                HangarItemView item = _itemTemplate.Clone("Module_" + index);
                item.Button.onClick.AddListener(() => Select(index)); // once per item, at creation
                _items.Add(item);
            }

            if (_selected == null || !_ids.Contains(_selected))
                _selected = _source.InstalledIn(_slot) ?? (_ids.Count > 0 ? _ids[0] : null);
            _preview = _selected;
            Refresh();
        }

        private void Refresh()
        {
            string installed = _source.InstalledIn(_slot);
            for (int i = 0; i < _items.Count; i++)
            {
                HangarItemView item = _items[i];
                bool used = i < _ids.Count;
                item.gameObject.SetActive(used);
                if (!used)
                    continue;

                string id = _ids[i];
                HangarModule module = _source.Module(id);
                item.SetIcon(_slot, id);
                item.SetSelected(id == _selected);
                item.Title.text = HangarLook.ModuleName(id);
                item.Info.SetText(_levelFormat, module.Level, module.MaxLevel);
                item.Extra.text = id == installed ? UiStrings.Get("hangar.installed") : string.Empty;
            }

            RefreshDetails(installed);
            RefreshStats();
        }

        private void RefreshDetails(string installed)
        {
            bool any = _selected != null;
            _install.gameObject.SetActive(any);
            _name.text = any ? HangarLook.ModuleName(_selected) : UiStrings.Get("hangar.no_modules");
            _description.text = any ? HangarLook.ModuleDescription(_selected) : string.Empty;
            if (!any)
                return;

            bool isInstalled = _selected == installed;
            _install.interactable = !isInstalled;
            _installLabel.text = UiStrings.Get(isInstalled ? "hangar.installed" : "hangar.install");
        }

        private void RefreshStats()
        {
            HangarTankStats current = _source.Stats();
            bool differs = _preview != null && _preview != _source.InstalledIn(_slot);
            _stats.Show(current, differs ? _source.Stats(_preview) : current);
        }

        private void Select(int index)
        {
            if (index >= _ids.Count)
                return;

            _selected = _ids[index];
            _preview = _selected;
            Refresh();
        }

        private void HandleInstall()
        {
            if (_selected == null)
                return;

            // The button clicks plainly; the result decides the one sound. Changed redraws the screen.
            PurchaseResult result = _source.TryInstall(_selected);
            AppServices.UiSounds.Play(result == PurchaseResult.Done ? UiSound.Install : UiSound.Error);
        }

        private void HandleClose() => CloseClicked?.Invoke();
    }
}
