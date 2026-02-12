namespace Inventories
{
    public class InventoryAdapter
    {
        public int Widht => _inventory.Width;
        public int Height => _inventory.Height;

        private readonly Inventory _inventory;

        public InventoryAdapter(Inventory inventory)
        {
            _inventory = inventory;
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }
    }
}