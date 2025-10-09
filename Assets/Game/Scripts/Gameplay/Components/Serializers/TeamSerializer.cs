using SampleGame.Common;
using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    public class TeamSerializer : GameSerializer<Team, TeamData>
    {
        protected override TeamData Serialize(Team entityWorld) => new TeamData(_service.Type);
    }
}