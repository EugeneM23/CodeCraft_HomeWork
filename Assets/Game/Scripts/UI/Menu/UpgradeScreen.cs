using System;
using System.Collections.Generic;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Overlay over the hangar: every owned module with its level n/3, the price of the next level in marks and
    /// samples (red when short) and Upgrade, whose label says why it is off (not enough marks, top level).
    /// Plays Upgrade or Error by the result. Items are cloned from a switched-off template and reused.
    /// </summary>
    public sealed class UpgradeScreen : UIScreen
    {
        public event Action CloseClicked;

        [Tooltip("Switched-off module item (with Price and an Action button); cloned as needed")]
        [SerializeField] private HangarItemView _itemTemplate;
        [SerializeField] private Button _close;

        private readonly List<HangarItemView> _items = new();
        private readonly List<string> _ids = new();
        private IHangarSource _source;
        private string _levelFormat;

        private void Awake()
        {
            _source = AppServices.Hangar;
            _levelFormat = UiStrings.Get("hangar.level_format");
        }

        private void OnEnable()
        {
            _close.onClick.AddListener(HandleClose);
            _source.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            _close.onClick.RemoveListener(HandleClose);
            _source.Changed -= Refresh;
        }

        private void Refresh()
        {
            _ids.Clear();
            IReadOnlyList<string> slots = _source.Slots;
            for (int s = 0; s < slots.Count; s++)
            {
                IReadOnlyList<HangarModule> modules = _source.ModulesFor(slots[s]);
                for (int i = 0; i < modules.Count; i++)
                    if (modules[i].Owned)
                        _ids.Add(modules[i].Id);
            }

            while (_items.Count < _ids.Count)
            {
                int index = _items.Count;
                HangarItemView item = _itemTemplate.Clone("Module_" + index);
                item.Action.onClick.AddListener(() => HandleUpgrade(index)); // once per item, at creation
                _items.Add(item);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                HangarItemView item = _items[i];
                bool used = i < _ids.Count;
                item.gameObject.SetActive(used);
                if (used)
                    Draw(item, _source.Module(_ids[i]));
            }
        }

        private void Draw(HangarItemView item, HangarModule module)
        {
            item.SetIcon(module.SlotId, module.Id);
            item.Title.text = HangarLook.ModuleName(module.Id);
            item.Info.SetText(_levelFormat, module.Level, module.MaxLevel);
            item.Extra.text = HangarLook.SlotName(module.SlotId);

            PurchaseResult result = _source.CanUpgrade(module.Id);
            bool atTop = result == PurchaseResult.MaxLevel || module.UpgradeMarks < 0;
            if (atTop)
            {
                item.Price.text = UiStrings.Get("hangar.max_level");
                item.Price.color = UiStyle.TextMuted;
            }
            else
            {
                HangarLook.SetPrice(item.Price, module.UpgradeMarks, module.UpgradeSamples);
                item.Price.color = result == PurchaseResult.Done ? UiStyle.Text : UiStyle.Bad;
            }

            item.Action.interactable = result == PurchaseResult.Done;
            item.ActionLabel.text = result == PurchaseResult.Done
                ? UiStrings.Get("hangar.upgrade")
                : HangarLook.Result(result);
        }

        private void HandleUpgrade(int index)
        {
            if (index >= _ids.Count)
                return;

            // The button clicks plainly; the result decides the one sound. Changed redraws the screen.
            PurchaseResult result = _source.TryUpgrade(_ids[index]);
            AppServices.UiSounds.Play(result == PurchaseResult.Done ? UiSound.Upgrade : UiSound.Error);
        }

        private void HandleClose() => CloseClicked?.Invoke();
    }
}
