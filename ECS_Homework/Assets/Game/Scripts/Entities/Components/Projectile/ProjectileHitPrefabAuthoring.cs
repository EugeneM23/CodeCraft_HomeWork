using Unity.Entities;
using UnityEngine;

public class ProjectileHitPrefabAuthoring : MonoBehaviour
{
    public GameObject Prefab;

    private class ProjectileHitPrefabBaker : Baker<ProjectileHitPrefabAuthoring>
    {
        public override void Bake(ProjectileHitPrefabAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ProjectileHitPrefab { Value = GetEntity(authoring.Prefab) });
        }
    }
}