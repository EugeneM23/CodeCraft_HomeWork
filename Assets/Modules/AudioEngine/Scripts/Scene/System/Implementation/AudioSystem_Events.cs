using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AudioEngine
{
    public sealed partial class AudioSystem
    {
        private static readonly AnimationCurve s_FadeOutLinearCurve = AnimationCurve.Linear(0, 1, 1, 0);

        [FoldoutGroup("Events")]
        [ShowInInspector, ReadOnly, HideInEditorMode]
        private readonly Dictionary<AudioEventBase, string> _createdEvents = new();
        private readonly List<KeyValuePair<AudioEventBase, string>> _createdEventsCache = new();

        [FoldoutGroup("Events")]
        [ShowInInspector, ReadOnly, HideInEditorMode]
        private readonly HashSet<AudioEventBase> _playingEvents = new();
        private readonly List<AudioEventBase> _playingEventsCache = new();

        [FoldoutGroup("Events")]
        [ShowInInspector, ReadOnly, HideInEditorMode]
        private readonly Dictionary<string, float> _maxFriquencyEvents = new();
        private readonly List<KeyValuePair<string, float>> _maxFriquencyEventsCache = new();

        [FoldoutGroup("Events")]
        [ShowInInspector, ReadOnly, HideInEditorMode]
        private readonly Dictionary<AudioEventBase, Transform> _eventPivots = new();

        private readonly List<AudioEventBase> _lostPivotsCache = new();

        // Running fade-outs, ticked from Update (replaces the per-stop coroutines).
        private readonly List<AudioEventFade> _fades = new(16);

        // One delegate for all events: SetCallback(this.DisposeEvent) allocated a new one per sound.
        private Action<AudioEventBase> _disposeCallback;

        private Action<AudioEventBase> DisposeCallback =>
            _disposeCallback ??= new Action<AudioEventBase>(this.DisposeEvent);

        internal int FadingCount => _fades.Count;

        #region Play

        [Title("Methods")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PlayEvent(string eventId, float maxFriquency = 0) =>
            this.PlayEvent(eventId, Vector3.zero, Quaternion.identity, maxFriquency);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PlayEvent(string eventId, out AudioEventHandle handle, float maxFriquency = 0) =>
            this.PlayEvent(eventId, Vector3.zero, Quaternion.identity, out handle, maxFriquency);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PlayEvent(string eventId, Vector3 position, float maxFriquency = 0) =>
            this.PlayEvent(eventId, position, Quaternion.identity, maxFriquency);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PlayEvent(string eventId, Vector3 position, out AudioEventHandle handle, float maxFriquency = 0) =>
            this.PlayEvent(eventId, position, Quaternion.identity, out handle, maxFriquency);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PlayEvent(string eventId, Vector3 position, Quaternion rotation, float maxFriquency = 0)
            => this.PlayEvent(eventId, position, rotation, out _, maxFriquency);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PlayEvent(string eventId, Transform pivot, float maxFriquency = 0) =>
            this.PlayEvent(eventId, pivot, out _, maxFriquency);

        [Button, GUIColor(1f, 0.83f, 0f), HideInEditorMode]
        public bool PlayEvent(
            string eventId,
            Vector3 position,
            Quaternion rotation,
            out AudioEventHandle handle,
            float maxFriquency = 0
        )
        {
            if (_maxFriquencyEvents.ContainsKey(eventId))
            {
                handle = default;
                return false;
            }

            if (!this.audioEventPool.TryGet(eventId, out AudioEventBase audioEvent))
            {
                handle = default;
                return false;
            }

            audioEvent.Position = position;
            audioEvent.Rotation = rotation;
            audioEvent.SetCallback(this.DisposeCallback);

            _createdEvents.Add(audioEvent, eventId);

            if (maxFriquency > 0)
                _maxFriquencyEvents.Add(eventId, maxFriquency);

            this.StartEvent(audioEvent);
            handle = new AudioEventHandle(audioEvent, this);
            return true;
        }

        [Button, GUIColor(1f, 0.83f, 0f), HideInEditorMode]
        public bool PlayEvent(string eventId, Transform pivot, out AudioEventHandle handle, float maxFriquency = 0)
        {
            if (_maxFriquencyEvents.ContainsKey(eventId))
            {
                handle = default;
                return false;
            }

            if (!this.audioEventPool.TryGet(eventId, out AudioEventBase audioEvent))
            {
                handle = default;
                return false;
            }

            audioEvent.Position = pivot.position;
            audioEvent.Rotation = pivot.rotation;
            audioEvent.SetCallback(this.DisposeCallback);

            _createdEvents.Add(audioEvent, eventId);
            _eventPivots.Add(audioEvent, pivot);

            if (maxFriquency > 0)
                _maxFriquencyEvents.Add(eventId, maxFriquency);

            this.StartEvent(audioEvent);
            handle = new AudioEventHandle(audioEvent, this);
            return true;
        }

        #endregion

        #region Create

        public AudioEventHandle CreateEvent(string eventId) =>
            this.CreateEvent(eventId, Vector3.zero, Quaternion.identity);

        public AudioEventHandle CreateEvent(string eventId, Vector3 position, Quaternion rotation)
        {
            AudioEventBase audioEvent = this.audioEventPool.Get(eventId);
            audioEvent.Position = position;
            audioEvent.Rotation = rotation;
            audioEvent.SetCallback(null);

            _createdEvents.Add(audioEvent, eventId);

            return new AudioEventHandle(audioEvent, this);
        }

        public bool TryCreateEvent(string eventId, out AudioEventHandle result) =>
            this.TryCreateEvent(eventId, Vector3.zero, Quaternion.identity, out result);

        public bool TryCreateEvent(string eventId, Vector3 position, Quaternion rotation, out AudioEventHandle result)
        {
            if (!this.audioEventPool.TryGet(eventId, out AudioEventBase audioEvent))
            {
                result = default;
                return false;
            }

            audioEvent.Position = position;
            audioEvent.Rotation = rotation;
            audioEvent.SetCallback(null);

            _createdEvents.Add(audioEvent, eventId);

            result = new AudioEventHandle(audioEvent, this);
            return true;
        }

        internal bool IsCreatedEvent(AudioEventBase audioEvent)
        {
            return _createdEvents.ContainsKey(audioEvent);
        }

        #endregion

        #region Dispose

        public bool DisposeEvent(string eventId)
        {
            _createdEventsCache.Clear();
            _createdEventsCache.AddRange(_createdEvents);

            for (int i = 0, count = _createdEventsCache.Count; i < count; i++)
            {
                (AudioEventBase audioEvent, string fullId) = _createdEventsCache[i];
                if (fullId == eventId)
                {
                    this.DisposeEvent(audioEvent);
                    return true;
                }
            }

            return false;
        }

        public void DisposeEvents(string eventId)
        {
            _createdEventsCache.Clear();
            _createdEventsCache.AddRange(_createdEvents);

            for (int i = 0, count = _createdEventsCache.Count; i < count; i++)
            {
                (AudioEventBase audioEvent, string fullId) = _createdEventsCache[i];
                if (fullId == eventId)
                    this.DisposeEvent(audioEvent);
            }
        }

        internal void DisposeEvent(AudioEventBase audioEvent)
        {
            if (!_createdEvents.Remove(audioEvent))
                return;

            // A pending stop-fade must not touch the event after it went back to the pool.
            this.RemoveFade(audioEvent);

            if (_playingEvents.Remove(audioEvent))
                audioEvent.OnStop();

            _eventPivots.Remove(audioEvent);
            this.audioEventPool.Release(audioEvent);
        }

        public bool DisposeEvent(string eventId, float fadeoutTime) =>
            this.DisposeEvent(eventId, s_FadeOutLinearCurve, fadeoutTime);

        public void DisposeEvents(string eventId, float fadeoutTime) =>
            this.DisposeEvents(eventId, s_FadeOutLinearCurve, fadeoutTime);

        public bool DisposeEvent(string eventId, AnimationCurve curve, float fadeoutTime)
        {
            _createdEventsCache.Clear();
            _createdEventsCache.AddRange(_createdEvents);

            for (int i = 0, count = _createdEventsCache.Count; i < count; i++)
            {
                (AudioEventBase audioEvent, string fullId) = _createdEventsCache[i];
                if (fullId == eventId)
                {
                    this.DisposeEventFaded(audioEvent, curve, fadeoutTime);
                    return true;
                }
            }

            return false;
        }

        // Fades out and disposes this exact event. The handle used to dispose by id, which picked
        // whichever event with that id came first, not necessarily the one the handle points to.
        internal void DisposeEvent(AudioEventBase audioEvent, AnimationCurve curve, float fadeoutTime)
        {
            if (_createdEvents.ContainsKey(audioEvent))
                this.DisposeEventFaded(audioEvent, curve, fadeoutTime);
        }

        // Same steps as the old DisposeEventRoutine: forget the event at once, fade it if it plays,
        // then stop it and give it back to the pool when the fade ends (see FinishFade).
        private void DisposeEventFaded(AudioEventBase audioEvent, AnimationCurve curve, float fadeoutTime)
        {
            if (!_createdEvents.Remove(audioEvent))
                return;

            int index = this.FindFade(audioEvent);
            float startVolume = audioEvent.FadeVolume;
            if (index >= 0)
                _fades.RemoveAt(index);

            if (!_playingEvents.Remove(audioEvent))
            {
                _eventPivots.Remove(audioEvent);
                this.audioEventPool.Release(audioEvent);
                return;
            }

            AudioEventFade fade = new AudioEventFade(audioEvent, curve, fadeoutTime, startVolume, true);
            if (fadeoutTime > 0f)
                _fades.Add(fade);
            else
                this.FinishFade(fade);
        }

        public void DisposeEvents(string eventId, AnimationCurve curve, float fadeoutTime)
        {
            _createdEventsCache.Clear();
            _createdEventsCache.AddRange(_createdEvents);

            for (int i = 0, count = _createdEventsCache.Count; i < count; i++)
            {
                (AudioEventBase audioEvent, string fullId) = _createdEventsCache[i];
                if (fullId == eventId)
                    this.DisposeEventFaded(audioEvent, curve, fadeoutTime);
            }
        }

        #endregion

        #region Find

        public bool FindEvent(string eventId, out AudioEventHandle result)
        {
            foreach ((AudioEventBase audioEventBase, string fullKey) in _createdEvents)
            {
                if (fullKey == eventId)
                {
                    result = new AudioEventHandle(audioEventBase, this);
                    return true;
                }
            }

            result = default;
            return false;
        }

        public int FindEventsNonAlloc(string eventId, AudioEventHandle[] results)
        {
            int count = 0;

            foreach ((AudioEventBase audioEventBase, string fullKey) in _createdEvents)
                if (fullKey == eventId)
                    results[count++] = new AudioEventHandle(audioEventBase, this);

            return count;
        }

        #endregion

        #region Lifecycle

        internal void StartEvent(AudioEventBase audioEvent)
        {
            // Starting an event cancels its pending stop-fade and brings the volume back.
            int index = this.FindFade(audioEvent);
            if (index >= 0)
            {
                AudioEventFade fade = _fades[index];
                _fades.RemoveAt(index);
                if (!fade.dispose)
                    audioEvent.FadeVolume = fade.startVolume;
            }

            if (_playingEvents.Add(audioEvent))
                audioEvent.OnStart();
        }

        internal bool IsPlayingEvent(AudioEventBase audioEvent)
        {
            return _playingEvents.Contains(audioEvent);
        }

        internal void StopEvent(AudioEventBase audioEvent, float fadeoutTime)
        {
            this.StopEvent(audioEvent, fadeoutTime, s_FadeOutLinearCurve);
        }

        internal void StopEvent(AudioEventBase audioEvent, float fadeoutTime, AnimationCurve curve)
        {
            // A stop-fade keeps the event in _playingEvents until the end, as the old coroutine did.
            if (!_playingEvents.Contains(audioEvent))
                return;

            int index = this.FindFade(audioEvent);
            if (index >= 0 && _fades[index].dispose)
                return;

            AudioEventFade fade = new AudioEventFade(audioEvent, curve, fadeoutTime, audioEvent.FadeVolume, false);
            if (index >= 0)
                _fades.RemoveAt(index);

            if (fadeoutTime > 0f)
                _fades.Add(fade);
            else
                this.FinishFade(fade);
        }

        internal void StopEvent(AudioEventBase audioEvent)
        {
            this.RemoveFade(audioEvent);

            if (_playingEvents.Remove(audioEvent))
                audioEvent.OnStop();
        }

        #endregion

        #region Fades

        private int FindFade(AudioEventBase audioEvent)
        {
            for (int i = 0, count = _fades.Count; i < count; i++)
                if (ReferenceEquals(_fades[i].audioEvent, audioEvent))
                    return i;

            return -1;
        }

        private void RemoveFade(AudioEventBase audioEvent)
        {
            int index = this.FindFade(audioEvent);
            if (index >= 0)
                _fades.RemoveAt(index);
        }

        private void UpdateFades(float deltaTime)
        {
            for (int i = _fades.Count - 1; i >= 0; i--)
            {
                if (i >= _fades.Count)
                    continue;

                AudioEventFade fade = _fades[i];
                fade.elapsed += deltaTime;

                if (fade.elapsed < fade.duration)
                {
                    float t = fade.elapsed / fade.duration;
                    float multiplier = fade.curve != null ? fade.curve.Evaluate(t) : 1f - t;
                    fade.audioEvent.FadeVolume = fade.startVolume * multiplier;
                    _fades[i] = fade;
                    continue;
                }

                _fades.RemoveAt(i);
                this.FinishFade(fade);
            }
        }

        // End of a fade: silence, stop, and for a dispose-fade give the event back to the pool,
        // exactly like the end of the old DisposeEventRoutine / StopEventRoutine.
        private void FinishFade(in AudioEventFade fade)
        {
            AudioEventBase audioEvent = fade.audioEvent;
            audioEvent.FadeVolume = 0f;

            if (fade.dispose)
            {
                // A dispose-fade left _playingEvents when it began, so it is still running here.
                audioEvent.OnStop();
                _eventPivots.Remove(audioEvent);
                this.audioEventPool.Release(audioEvent);
                return;
            }

            if (_playingEvents.Remove(audioEvent))
                audioEvent.OnStop();
        }

        // Dispose-fades are not in _playingEvents, so Pause()/Resume() reach their sources here.
        private void PauseFadingEvents()
        {
            for (int i = 0, count = _fades.Count; i < count; i++)
                if (_fades[i].dispose)
                    _fades[i].audioEvent.OnPause();
        }

        private void ResumeFadingEvents()
        {
            for (int i = 0, count = _fades.Count; i < count; i++)
                if (_fades[i].dispose)
                    _fades[i].audioEvent.OnResume();
        }

        #endregion

        private void UpdatePlayingEvents(float deltaTime)
        {
            _playingEventsCache.Clear();
            _playingEventsCache.AddRange(_playingEvents);

            for (int i = 0, count = _playingEventsCache.Count; i < count; i++)
            {
                AudioEventBase audioEvent = _playingEventsCache[i];
                audioEvent.OnUpdate(deltaTime);
            }
        }

        private void UpdateMaxFrequencyEvents(float deltaTime)
        {
            _maxFriquencyEventsCache.Clear();
            _maxFriquencyEventsCache.AddRange(_maxFriquencyEvents);

            for (int i = 0, count = _maxFriquencyEventsCache.Count; i < count; i++)
            {
                (string eventId, float maxFriquency) = _maxFriquencyEventsCache[i];
                maxFriquency -= deltaTime;

                if (maxFriquency <= 0)
                    _maxFriquencyEvents.Remove(eventId);
                else
                    _maxFriquencyEvents[eventId] = maxFriquency;
            }
        }

        // A destroyed follow-target used to throw here every frame and freeze all audio:
        // such an event now stays where it was and keeps playing.
        internal void UpdatePivotEvents()
        {
            foreach ((AudioEventBase audioEvent, Transform pivot) in _eventPivots)
            {
                if (pivot == null)
                {
                    _lostPivotsCache.Add(audioEvent);
                    continue;
                }

                audioEvent.Position = pivot.position;
                audioEvent.Rotation = pivot.rotation;
            }

            if (_lostPivotsCache.Count == 0)
                return;

            for (int i = 0, count = _lostPivotsCache.Count; i < count; i++)
                _eventPivots.Remove(_lostPivotsCache[i]);

            _lostPivotsCache.Clear();
        }
    }
}