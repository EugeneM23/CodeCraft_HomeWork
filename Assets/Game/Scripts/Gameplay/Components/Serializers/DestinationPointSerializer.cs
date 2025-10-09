using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    internal class DestinationPointSerializer : GameSerializer<DestinationPoint, DestinationPointData>
    {
        protected override DestinationPointData Serialize(DestinationPoint destinationPoint) => 
            new(destinationPoint.Value);
    }
}