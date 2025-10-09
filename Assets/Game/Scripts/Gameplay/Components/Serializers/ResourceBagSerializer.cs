using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal class ResourceBagSerializer : GameSerializer<ResourceBag, ResourceBagData>
    {
        protected override ResourceBagData Serialize(ResourceBag resourceBag) => 
            new(resourceBag.Type, resourceBag.Current, resourceBag.Capacity);
    }
}