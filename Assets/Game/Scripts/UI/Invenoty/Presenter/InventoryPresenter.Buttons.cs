using Sirenix.OdinInspector;

public partial class InventoryPresenter
{
    [Button]
    public void Reorganize()
    {
        _inventory.ReorganizeSpace();
        UpdateView();
    }
}