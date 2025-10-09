using SaveLoadSystem;

namespace SampleGame.Gameplay
{
    public class DamageSerializer : GameSerializer<Damage, DamageData>
    {
        protected override DamageData Serialize(Damage damage) => new(damage.Value);
    }
}