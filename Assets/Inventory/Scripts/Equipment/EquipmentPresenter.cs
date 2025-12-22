namespace Inventories
{
    public class EquipmentPresenter
    {
        private readonly Equipment _equipment;

        public EquipmentPresenter(Equipment equipment)
        {
            _equipment = equipment;
        }

        public void Toggle()
        {
            _equipment.gameObject.SetActive(!_equipment.gameObject.activeSelf);
        }
    }
}