using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Objects
{
    /// <summary>
    /// Keys of a Saveable's components: the path of the component's object from the Saveable and the type name
    /// ("Transform", "Visual/SpriteRenderer", "BoxCollider2D#1"). A path is object names joined by '/', a repeated
    /// name among siblings gets "#1", "#2". The walk stops at a nested Saveable: it saves itself.
    /// </summary>
    internal static class ComponentKeys
    {
        public static List<KeyedComponent> Collect(Saveable root)
        {
            var result = new List<KeyedComponent>();
            // A whole-scene object of the scene answers for itself only: its children are objects of the scene too
            Walk(root.transform, string.Empty, result, deep: !root.Baseline.HasValue);
            return result;
        }

        public static Transform FindChild(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                current = FindAmong(ChildrenOf(current), segment);
                if (current == null)
                    return null;
            }

            return current;
        }

        public static string PathOf(Transform root, Transform node)
        {
            var segments = new List<string>();
            for (Transform current = node; current != null && current != root; current = current.parent)
                segments.Add(SegmentOf(current));

            segments.Reverse();
            return string.Join("/", segments);
        }

        /// <summary>The path of an object from its scene root, for parents that are not under a Saveable.</summary>
        public static string ScenePathOf(Transform node) => PathOf(null, node);

        public static Transform FindInScene(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path) || !scene.IsValid())
                return null;

            int slash = path.IndexOf('/');
            string first = slash < 0 ? path : path.Substring(0, slash);
            var roots = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
                roots.Add(root.transform);

            Transform top = FindAmong(roots, first);
            return top == null || slash < 0 ? top : FindChild(top, path.Substring(slash + 1));
        }

        /// <summary>The component type name in a key: "Visual/BoxCollider2D#1" -> "BoxCollider2D".</summary>
        public static string TypeNameOf(string key)
        {
            string last = key.Substring(key.LastIndexOf('/') + 1);
            int hash = last.IndexOf('#');
            return hash < 0 ? last : last.Substring(0, hash);
        }

        private static void Walk(Transform node, string path, List<KeyedComponent> result, bool deep = true)
        {
            var counts = new Dictionary<string, int>();
            foreach (Component component in node.GetComponents<Component>())
            {
                // A missing script is null; the Saveable itself is not a saved component
                if (component == null || component is Saveable)
                    continue;

                string type = component.GetType().Name;
                counts.TryGetValue(type, out int index);
                counts[type] = index + 1;
                string name = index == 0 ? type : $"{type}#{index}";
                result.Add(new KeyedComponent(path.Length == 0 ? name : $"{path}/{name}", path, component));
            }

            if (!deep)
                return;

            foreach (Transform child in node)
            {
                if (child.GetComponent<Saveable>() != null)
                    continue;

                string segment = SegmentOf(child);
                Walk(child, path.Length == 0 ? segment : $"{path}/{segment}", result, deep);
            }
        }

        private static string SegmentOf(Transform node)
        {
            int index = 0;
            foreach (Transform sibling in SiblingsOf(node))
            {
                if (sibling == node)
                    break;

                if (sibling.name == node.name)
                    index++;
            }

            return index == 0 ? node.name : $"{node.name}#{index}";
        }

        private static Transform FindAmong(IEnumerable<Transform> candidates, string segment)
        {
            int hash = segment.LastIndexOf('#');
            string name = segment;
            int wanted = 0;
            if (hash > 0 && int.TryParse(segment.Substring(hash + 1), out int number))
            {
                name = segment.Substring(0, hash);
                wanted = number;
            }

            int seen = 0;
            foreach (Transform candidate in candidates)
            {
                if (candidate.name != name)
                    continue;

                if (seen == wanted)
                    return candidate;

                seen++;
            }

            return null;
        }

        private static IEnumerable<Transform> ChildrenOf(Transform node)
        {
            foreach (Transform child in node)
                yield return child;
        }

        private static IEnumerable<Transform> SiblingsOf(Transform node)
        {
            if (node.parent != null)
                return ChildrenOf(node.parent);

            var roots = new List<Transform>();
            foreach (GameObject root in node.gameObject.scene.GetRootGameObjects())
                roots.Add(root.transform);

            return roots;
        }
    }
}
