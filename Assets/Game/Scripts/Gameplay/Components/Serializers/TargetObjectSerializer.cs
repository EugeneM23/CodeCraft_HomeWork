using Modules.Entities;
using SaveLoadSystem;
using UnityEngine;
using Zenject;

namespace SampleGame.Gameplay
{
    internal class TargetObjectSerializer : GameSerializer<TargetObject, TargetObjectData>
    {
        [Inject] private readonly EntityWorld _entityWorld;

        protected override TargetObjectData Serialize(TargetObject component)
        {
            if (!component.Value)
                return new TargetObjectData(-1);

            return new TargetObjectData(component.Value.Id);
        }

        protected override void Deserialize(TargetObject service, TargetObjectData data)
        {
            if (data.Id == -1)
            {
                service.Value = null;
                return;
            }

            service.Value = _entityWorld.Get(data.Id);
        }
    }
}