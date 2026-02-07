using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.UI.Entities.Components.Health
{
    public class HitEffectAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        private class HitEffectBaker : Baker<HitEffectAuthoring>
        {
            public override void Bake(HitEffectAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new HitEffect { Prefab = GetEntity(authoring.Prefab) });
            }
        }
    }
}