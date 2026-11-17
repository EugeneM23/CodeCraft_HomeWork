using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The «Звук» tab: a 0–100 % slider per AudioChannel. Edits the bound AudioSettingsData and plays the
    /// volume live through IAudioVolumes as a preview; the settings screen restores it on revert.
    /// </summary>
    public sealed class AudioSettingsTab : MonoBehaviour
    {
        private const float PERCENT = 100f;

        public event Action Changed;

        [Tooltip("One per AudioChannel, in the enum's order: Master, Music, Effects, Ui, Ambient")]
        [SerializeField] private Slider[] _sliders;
        [Tooltip("The «80 %» label next to each slider, same order")]
        [SerializeField] private TMP_Text[] _values;

        private UnityAction<float>[] _handlers;
        private AudioSettingsData _audio;

        public void Bind(AudioSettingsData audio)
        {
            _audio = audio;
            for (int i = 0; i < _sliders.Length; i++)
            {
                float percent = Mathf.Round(AudioSettingsUseCase.Get(audio, (AudioChannel)i) * PERCENT);
                _sliders[i].SetValueWithoutNotify(percent);
                ShowValue(i, percent);
            }
        }

        private void Awake()
        {
            _handlers = new UnityAction<float>[_sliders.Length];
            for (int i = 0; i < _sliders.Length; i++)
            {
                int index = i;
                _handlers[i] = value => HandleSlider(index, value);
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _sliders.Length; i++)
                _sliders[i].onValueChanged.AddListener(_handlers[i]);
        }

        private void OnDisable()
        {
            for (int i = 0; i < _sliders.Length; i++)
                _sliders[i].onValueChanged.RemoveListener(_handlers[i]);
        }

        private void HandleSlider(int index, float percent)
        {
            if (_audio == null)
                return;

            AudioChannel channel = (AudioChannel)index;
            float volume = percent / PERCENT;
            AudioSettingsUseCase.Set(_audio, channel, volume);
            AppServices.Audio.SetVolume(channel, volume);
            ShowValue(index, percent);
            Changed?.Invoke();
        }

        private void ShowValue(int index, float percent)
        {
            if (index < _values.Length && _values[index] != null)
                _values[index].SetText("{0:0} %", percent); // TMP: {0:N} = N decimals
        }
    }
}
