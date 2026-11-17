using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AudioEngine
{
    public abstract class AudioEventBase : ScriptableObject
    {
        public string Id => _identifier;
        public bool IsValid => _spawned;
        public bool Poolable => _poolable;

        [HideInPlayMode]
        [SerializeField]
        private bool _poolable = true;
        
        private protected string _identifier;
        private protected bool _spawned;
        private protected AudioSystem _audioSystem;

        private protected Vector3 _position;
        private protected Quaternion _rotation;
        
        private Action<AudioEventBase> _completeCallback;
        
        protected internal virtual Vector3 Position
        {
            get { return _position; }
            set { _position = value; }
        }

        protected internal virtual Quaternion Rotation
        {
            get { return _rotation; }
            set { _rotation = value; }
        }

        protected internal abstract void OnStart();
        protected internal abstract void OnUpdate(float deltaTime);
        protected internal abstract void OnStop();

        protected internal abstract void OnPause();
        protected internal abstract void OnResume();

        internal void Initialize(string identifier, AudioSystem audioSystem)
        {
            _identifier = identifier;
            _audioSystem = audioSystem;
        }

        internal void OnSpawn()
        {
            _spawned = true;
        }

        internal void OnDespawn()
        {
            _spawned = false;
        }

        internal void SetCallback(Action<AudioEventBase> callback) => _completeCallback = callback;

        protected void Complete() => _completeCallback?.Invoke(this);

        // Volume the AudioSystem fades out (the old per-event FadeOut coroutine is gone: it allocated
        // on every stop and kept playing through a pause).
        protected internal virtual float FadeVolume
        {
            get => 1f;
            set { }
        }
    }
}