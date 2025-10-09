using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal class MoveSpeedSerializer : GameSerializer<MoveSpeed, MoveSpeedData>
    {
        protected override MoveSpeedData Serialize(MoveSpeed moveSpeed) => new(moveSpeed.Current);
    }
}