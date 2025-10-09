using System.Collections.Generic;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal class ProductionOrderSerializer : GameSerializer<ProductionOrder, ProductionOrderData>
    {
        protected override ProductionOrderData Serialize(ProductionOrder productionOrder)
        {
            var configNames = new List<string>();
            foreach (var config in productionOrder.Queue)
            {
                configNames.Add(config?.name ?? string.Empty);
            }
            return new(configNames);
        }
    }
}