using System;
using System.Collections.Generic;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The skill choice of a new level (GDD §3, §11): the game stands, up to 3 cards; a click on a card or the
    /// keys 1/2/3 (UI.Choose1..3) pick one. Esc does not close it (IBackHandler keeps it): a level always ends
    /// with a skill. BattleFlow stops and resumes the game and passes the pick to the run.
    /// </summary>
    public sealed class SkillChoiceScreen : UIScreen, IBackHandler
    {
        private static readonly string[] ChooseActions = { "UI/Choose1", "UI/Choose2", "UI/Choose3" };

        public event Action<int> Chosen;

        [SerializeField] private SkillCardView[] _cards;

        private readonly InputAction[] _keys = new InputAction[3];
        private Action<InputAction.CallbackContext>[] _onKeys;
        private Action<int> _onCardClicked;
        private int _count;
        private bool _chosen;

        /// <summary>Fills the cards before the screen is pushed (once per level up).</summary>
        public void Setup(IReadOnlyList<SkillOffer> offers)
        {
            EnsureInit();
            _chosen = false;
            _count = Mathf.Min(offers.Count, _cards.Length);
            for (int i = 0; i < _cards.Length; i++)
            {
                if (i < _count)
                    _cards[i].Setup(offers[i], i, KeyName(i));
                else
                    _cards[i].Hide();
            }
            FirstSelected = _count > 0 ? _cards[0].Button : null;
        }

        public bool HandleBack() => true;

        private void Awake() => EnsureInit();

        private void EnsureInit()
        {
            if (_onKeys != null)
                return;

            _onCardClicked = Choose;
            _onKeys = new Action<InputAction.CallbackContext>[ChooseActions.Length];
            for (int i = 0; i < ChooseActions.Length; i++)
            {
                int index = i;
                _onKeys[i] = _ => Choose(index);
            }

            // By name: the actions exist in the asset whether or not GameControls.cs is regenerated yet.
            InputActionAsset asset = AppServices.Input.Controls.asset;
            for (int i = 0; i < ChooseActions.Length; i++)
                _keys[i] = asset.FindAction(ChooseActions[i], throwIfNotFound: false);
        }

        private void OnEnable()
        {
            EnsureInit();
            for (int i = 0; i < _cards.Length; i++)
                _cards[i].Clicked += _onCardClicked;
            for (int i = 0; i < _keys.Length; i++)
            {
                if (_keys[i] != null)
                    _keys[i].performed += _onKeys[i];
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _cards.Length; i++)
                _cards[i].Clicked -= _onCardClicked;
            for (int i = 0; i < _keys.Length; i++)
            {
                if (_keys[i] != null)
                    _keys[i].performed -= _onKeys[i];
            }
        }

        private string KeyName(int index)
        {
            InputAction action = index < _keys.Length ? _keys[index] : null;
            return action != null ? KeyNameUseCase.OfBinding(action, 0) : (index + 1).ToString();
        }

        private void Choose(int index)
        {
            if (_chosen || !IsInteractable || index < 0 || index >= _count)
                return;

            _chosen = true;
            AppServices.UiSounds.Play(UiSound.SkillChoice);
            Chosen?.Invoke(index);
        }
    }
}
