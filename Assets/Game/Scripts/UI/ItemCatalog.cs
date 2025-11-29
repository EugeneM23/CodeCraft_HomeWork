using System;
using System.Collections.Generic;
using Inventories;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Game.Scripts.UI
{
    public class ItemCatalog : SerializedMonoBehaviour
    {
        [OdinSerialize] public Dictionary<string, ItemView> items = new Dictionary<string, ItemView>();

        public ItemView GetItem(Item item)
        {
            return items[item.Name];
        }
    }
}