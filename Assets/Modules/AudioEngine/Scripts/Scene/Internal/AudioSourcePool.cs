using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// ReSharper disable UseObjectOrCollectionInitializer

namespace AudioEngine
{
    internal sealed class AudioSourcePool : IDisposable
    {
        private const string INACTIVE_NAME = "<audio_source_inactive>";
        private const string ACTIVE_NAME = "<audio_source_active>";

        private readonly Transform parent;

        [ShowInInspector, ReadOnly]
        private readonly Stack<AudioSource> _pool = new();

        // initialSize comes from AudioSystem (serialized): the old fixed 4 sources ran out in the
        // first second of a fight and the rest were instantiated mid-fight.
        internal AudioSourcePool(Transform parent, int initialSize)
        {
            this.parent = parent;

            for (int i = 0; i < initialSize; i++)
            {
                GameObject sourceGO = new GameObject();
                sourceGO.name = INACTIVE_NAME;
                sourceGO.transform.parent = parent;

                AudioSource source = sourceGO.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.enabled = false;
                _pool.Push(source);
            }
        }

        public void Dispose()
        {
            foreach (AudioSource source in _pool) 
                AudioObjects.Destroy(source.gameObject);

            _pool.Clear();
        }

        internal AudioSource Take()
        {
            AudioSource source;

            if (_pool.Count > 0)
            {
                source = _pool.Pop();
            }
            else
            {
                GameObject sourceGO = new GameObject();
                sourceGO.transform.parent = this.parent;

                source = sourceGO.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }

            source.enabled = true;
            source.name = ACTIVE_NAME;
            return source;
        }

        internal void Release(AudioSource source)
        {
            source.enabled = false;
            source.name = INACTIVE_NAME;
            source.transform.parent = parent;
            _pool.Push(source);
        }
    }
}