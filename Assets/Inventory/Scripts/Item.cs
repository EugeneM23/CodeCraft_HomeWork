using UnityEngine;

namespace Inventories
{
    public sealed class Item
    {
        public ItemID ItemID;
        private static int ID_GEN;
        public string Name { get; }
        public Vector2Int Size { get; }
        public ItemType ItemType { get; }

        private readonly int id;

        public Item(string name, Vector2Int size, ItemType itemType = default)
            : this(itemType)
        {
            this.Name = name;
            this.Size = size;
        }

        public Item(string name, int width, int height, ItemType itemType = default)
            : this(itemType)
        {
            this.Name = name;
            this.Size = new Vector2Int(width, height);
        }

        public Item(Vector2Int size, ItemType itemType = default)
            : this(itemType)
        {
            this.Name = string.Empty;
            this.Size = size;
        }

        public Item(int width, int height, ItemType itemType = default)
            : this(itemType)
        {
            this.Name = string.Empty;
            this.Size = new Vector2Int(width, height);
        }

        private Item(ItemType itemType)
        {
            ItemType = itemType;
            this.id = ID_GEN++;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Item)obj);
        }

        public bool Equals(Item other)
        {
            return this.id == other.id;
        }

        public override int GetHashCode()
        {
            return this.id;
        }

        public override string ToString()
        {
            return $"{this.Name}";
        }
    }
}