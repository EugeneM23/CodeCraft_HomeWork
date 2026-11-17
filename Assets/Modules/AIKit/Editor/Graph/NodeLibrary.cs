using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// The list with search the canvas opens (step 90: one list for a right click on an empty place, Space and a pin let
    /// go over an empty place — NodeMenu builds what it holds). The items are paths ("AI/Actions/Combat/Attack"), grouped by their
    /// folders, each with a tile of its kind's colour; what is picked goes back to the canvas. Also the searchable list of
    /// a setting with many values (an action, a tag).
    /// </summary>
    public sealed class NodeLibrary : ScriptableObject, ISearchWindowProvider
    {
        private string _title;
        private List<(string Path, string Id, Texture Icon)> _items;
        private Action<string> _picked;

        /// <summary>A list of plain items (a variable, a value of a setting): the id of the picked one goes back.</summary>
        public static NodeLibrary Of(string title, List<(string Path, string Id)> items, Action<string> picked)
        {
            var list = new List<(string Path, string Id, Texture Icon)>();
            foreach ((string path, string id) in items)
                list.Add((path, id, null));

            return Make(title, list, picked);
        }

        /// <summary>A list of nodes (NodeMenu): each with the tile of its kind; the picked item goes back.</summary>
        public static NodeLibrary Of(string title, List<NodeMenu.Item> items, Action<NodeMenu.Item> picked)
        {
            var list = new List<(string Path, string Id, Texture Icon)>();
            var byKey = new Dictionary<string, NodeMenu.Item>();
            for (int i = 0; i < items.Count; i++)
            {
                string key = i.ToString();
                byKey[key] = items[i];
                list.Add((items[i].Path, key, Tile(items[i].Tint)));
            }

            return Make(title, list, key =>
            {
                if (byKey.TryGetValue(key, out NodeMenu.Item item))
                    picked?.Invoke(item);
            });
        }

        private static NodeLibrary Make(string title, List<(string Path, string Id, Texture Icon)> items, Action<string> picked)
        {
            var library = CreateInstance<NodeLibrary>();
            library.hideFlags = HideFlags.HideAndDontSave;
            library._title = title;
            library._items = items;
            library._picked = picked;
            return library;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent(_title)) };
            var groups = new HashSet<string>();
            foreach ((string itemPath, string id, Texture icon) in _items)
            {
                string[] path = itemPath.Split('/');
                string group = "";
                for (int i = 0; i < path.Length - 1; i++)
                {
                    group += "/" + path[i];
                    if (groups.Add(group))
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(path[i]), i + 1));
                }

                tree.Add(new SearchTreeEntry(new GUIContent(path[^1], icon)) { level = path.Length, userData = id });
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            _picked?.Invoke((string)entry.userData);
            return true;
        }

        // A small square of one colour, made once per colour (the tile of a node's kind)
        private static readonly Dictionary<Color, Texture2D> Tiles = new();

        private static Texture2D Tile(Color color)
        {
            if (Tiles.TryGetValue(color, out Texture2D tile) && tile != null)
                return tile;

            const int SIZE = 12;
            tile = new Texture2D(SIZE, SIZE) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[SIZE * SIZE];
            for (int i = 0; i < pixels.Length; i++)
            {
                int x = i % SIZE;
                int y = i / SIZE;
                bool edge = x == 0 || y == 0 || x == SIZE - 1 || y == SIZE - 1;
                pixels[i] = edge ? Color.Lerp(color, Color.black, 0.4f) : Color.Lerp(color, Color.white, 0.15f);
            }

            tile.SetPixels(pixels);
            tile.Apply();
            Tiles[color] = tile;
            return tile;
        }
    }
}
