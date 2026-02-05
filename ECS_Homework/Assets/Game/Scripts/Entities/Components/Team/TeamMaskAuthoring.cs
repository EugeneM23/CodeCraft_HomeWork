using Unity.Entities;
using UnityEngine;

public class TeamMaskAuthoring : MonoBehaviour
{
    public TeamType Team;

    private class TeamMaskBaker : Baker<TeamMaskAuthoring>
    {
        public override void Bake(TeamMaskAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new TeamMask { Team = authoring.Team });
        }
    }
}