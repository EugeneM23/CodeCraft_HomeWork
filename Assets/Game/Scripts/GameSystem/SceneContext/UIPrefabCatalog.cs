using System.Collections.Generic;
using UnityEngine;

namespace Inventories
{
    [CreateAssetMenu(fileName = "UIPrefabCatalog", menuName = "UI/Prefab Catalog")]
    public class UIPrefabCatalog : ScriptableObject
    {
        [SerializeField] private List<UIPrefabEntry> _prefabs = new();
    
        private Dictionary<UIPrefabs, GameObject> _prefabDictionary;

        private void OnEnable()
        {
            BuildDictionary();
        }

        private void BuildDictionary()
        {
            _prefabDictionary = new Dictionary<UIPrefabs, GameObject>();
        
            foreach (var entry in _prefabs)
            {
                if (entry.prefab != null)
                {
                    _prefabDictionary[entry.id] = entry.prefab;
                }
            }
        }

        public GameObject GetPrefab(UIPrefabs id)
        {
            if (_prefabDictionary == null)
                BuildDictionary();

            if (_prefabDictionary.TryGetValue(id, out var prefab))
            {
                return prefab;
            }

            Debug.LogError($"Prefab with id {id} not found in UIPrefabCatalog!");
            return null;
        }

        public T GetPrefabComponent<T>(UIPrefabs id) where T : Component
        {
            var prefab = GetPrefab(id);
        
            if (prefab == null)
                return null;

            var component = prefab.GetComponent<T>();
        
            if (component == null)
            {
                Debug.LogError($"Component {typeof(T).Name} not found on prefab {id}!");
            }

            return component;
        }
    }
}