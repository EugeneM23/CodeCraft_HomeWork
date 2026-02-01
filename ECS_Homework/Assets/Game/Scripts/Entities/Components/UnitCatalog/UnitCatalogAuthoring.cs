using Unity.Entities;
using UnityEngine;

public class UnitCatalogAuthoring : MonoBehaviour
{
    public GameObject KnightRed;
    public GameObject NecromancerRed;
    public GameObject KnightBlue;
    public GameObject NecromancerBlue;

    private class UnitCatalogBaker : Baker<UnitCatalogAuthoring>
    {
        public override void Bake(UnitCatalogAuthoring authoring)
        {
            var catalog = GetEntity(TransformUsageFlags.None);

            var knightRed = GetEntity(authoring.KnightRed, TransformUsageFlags.Dynamic);
            var necromancerRed = GetEntity(authoring.NecromancerRed, TransformUsageFlags.Dynamic);
            var knightBlue = GetEntity(authoring.KnightBlue, TransformUsageFlags.Dynamic);
            var necromancerBlue = GetEntity(authoring.NecromancerBlue, TransformUsageFlags.Dynamic);

            AddComponent(catalog, new UnitEntityCatalog
            {
                KnightRed = knightRed,
                NecromancerRed = necromancerRed,
                KnightBlue = knightBlue,
                NecromancerBlue = necromancerBlue
            });
        }
    }
}

public struct UnitEntityCatalog : IComponentData
{
    public Entity KnightRed;
    public Entity NecromancerRed;
    public Entity KnightBlue;
    public Entity NecromancerBlue;
}