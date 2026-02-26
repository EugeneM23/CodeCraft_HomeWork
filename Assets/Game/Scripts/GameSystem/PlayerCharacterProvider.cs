namespace Inventories.Scripts
{
    public class PlayerCharacterProvider
    {
        private readonly Entity _character;

        public Entity GetCharacterEntity() => _character;

        public PlayerCharacterProvider(Entity character)
        {
            _character = character;
        }
    }
}