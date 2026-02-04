using Unity.Entities;
using UnityEngine;

public class ProjectilePrefabAuthoring : MonoBehaviour
{
    public GameObject Prefab;

    private class ProjectilePrefabBaker : Baker<ProjectilePrefabAuthoring>
    {
        public override void Bake(ProjectilePrefabAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ProjectilePrefab { Value = GetEntity(authoring.Prefab) });
        }
    }
}