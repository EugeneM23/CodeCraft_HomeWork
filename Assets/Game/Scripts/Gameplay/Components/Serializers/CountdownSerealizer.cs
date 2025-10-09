using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    public class CountdownSerealizer : GameSerializer<Countdown, CountdownData>
    {
        protected override CountdownData Serialize(Countdown service) => new(service.Current, service.Duration);
    }
}