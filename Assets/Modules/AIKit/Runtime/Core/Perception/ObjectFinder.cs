using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// "Is there an object of this kind near me": what a sensor of the graph asks. The kind is a Unity tag ("Player").
    /// The core's nodes never look into the scene themselves (invariant 7) — they ask this; a game with its own
    /// registry of characters gives the agent its own finder (AgentContext.Objects).
    /// </summary>
    public interface IObjectFinder
    {
        /// <summary>The nearest object of the kind within the radius, the asker's own entity aside; null — none.</summary>
        Transform FindNearest(string kind, Vector3 center, float radius, Transform self);

        /// <summary>Every object of the kind within the radius (results is cleared first), the asker's entity aside.</summary>
        void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results);

        /// <summary>
        /// Every object with a component of the type within the radius (results is cleared first), the asker's entity
        /// aside (step 91: a sensor looking by type). A finder that cannot look by type finds nothing.
        /// </summary>
        void FindAllOfType(System.Type type, Vector3 center, float radius, Transform self, List<Transform> results) =>
            results.Clear();
    }

    /// <summary>
    /// The finder every agent has by default: objects by tag, asked from the scene once per frame per tag. Simple and
    /// good for a handful of agents; a crowd wants a registry instead.
    /// </summary>
    public sealed class TagObjectFinder : IObjectFinder
    {
        public static readonly TagObjectFinder Shared = new();

        private sealed class Cache
        {
            public int Frame = -1;
            public GameObject[] Objects = System.Array.Empty<GameObject>();
        }

        private readonly Dictionary<string, Cache> _caches = new();
        private readonly HashSet<string> _unknownTags = new();

        /// <summary>
        /// Forgets what was asked this frame. An object made later in the same frame is seen only from the next one —
        /// nothing in a running game, but a test that builds a world and steps agents without frames passing calls this.
        /// </summary>
        public void Clear()
        {
            _caches.Clear();
            _typeCaches.Clear();
        }

        public Transform FindNearest(string kind, Vector3 center, float radius, Transform self)
        {
            if (string.IsNullOrEmpty(kind) || radius <= 0f)
                return null;

            GameObject[] objects = ObjectsOf(kind);
            Transform nearest = null;
            float best = radius * radius;
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || !candidate.activeInHierarchy)
                    continue;

                Transform transform = candidate.transform;
                if (self != null && (transform == self || transform.IsChildOf(self)))
                    continue;

                float distance = (transform.position - center).sqrMagnitude;
                if (distance > best)
                    continue;

                best = distance;
                nearest = transform;
            }

            return nearest;
        }

        public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
        {
            results.Clear();
            if (string.IsNullOrEmpty(kind) || radius <= 0f)
                return;

            GameObject[] objects = ObjectsOf(kind);
            bool anywhere = radius >= float.MaxValue * 0.5f;            // squaring it would overflow
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || !candidate.activeInHierarchy)
                    continue;

                Transform transform = candidate.transform;
                if (self != null && (transform == self || transform.IsChildOf(self)))
                    continue;

                if (anywhere || (transform.position - center).sqrMagnitude <= radius * radius)
                    results.Add(transform);
            }
        }

        private sealed class TypeCache
        {
            public int Frame = -1;
            public Object[] Objects = System.Array.Empty<Object>();
        }

        private readonly Dictionary<System.Type, TypeCache> _typeCaches = new();

        public void FindAllOfType(System.Type type, Vector3 center, float radius, Transform self, List<Transform> results)
        {
            results.Clear();
            if (type == null || radius <= 0f || !typeof(Component).IsAssignableFrom(type))
                return;

            if (!_typeCaches.TryGetValue(type, out TypeCache cache))
            {
                cache = new TypeCache();
                _typeCaches.Add(type, cache);
            }

            if (cache.Frame != Time.frameCount || !Application.isPlaying)
            {
                cache.Frame = Time.frameCount;
                cache.Objects = Object.FindObjectsByType(type, FindObjectsInactive.Exclude, FindObjectsSortMode.None); // PORTING: 6000.3 needs the sort mode
            }

            bool anywhere = radius >= float.MaxValue * 0.5f;
            for (int i = 0; i < cache.Objects.Length; i++)
            {
                if (cache.Objects[i] is not Component candidate || candidate == null || !candidate.gameObject.activeInHierarchy)
                    continue;

                Transform transform = candidate.transform;
                if (self != null && (transform == self || transform.IsChildOf(self)) || results.Contains(transform))
                    continue;

                if (anywhere || (transform.position - center).sqrMagnitude <= radius * radius)
                    results.Add(transform);
            }
        }

        private GameObject[] ObjectsOf(string tag)
        {
            if (!_caches.TryGetValue(tag, out Cache cache))
            {
                cache = new Cache();
                _caches.Add(tag, cache);
            }

            if (cache.Frame == Time.frameCount && Application.isPlaying)
                return cache.Objects;

            cache.Frame = Time.frameCount;
            try
            {
                cache.Objects = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                cache.Objects = System.Array.Empty<GameObject>();
                if (_unknownTags.Add(tag))
                    Debug.LogWarning($"AI Kit sensor: the tag '{tag}' is not defined (Project Settings → Tags and Layers)");
            }

            return cache.Objects;
        }
    }
}
