using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Selects the next grenade type the character carries, wrapping round. An empty type can be
    /// selected too: the stock shows on the HUD, and a pickup may fill it.
    /// </summary>
    public sealed class GrenadeSwitchAction : IAction
    {
        private static readonly GrenadeType[] Types = (GrenadeType[])System.Enum.GetValues(typeof(GrenadeType));

        private readonly IEntity _character;

        public GrenadeSwitchAction(IEntity character) => _character = character;

        public void Invoke()
        {
            IReactiveVariable<GrenadeType> selected = _character.GetGrenadeSelected();
            var prefabs = _character.GetGrenadePrefabs();
            int start = System.Array.IndexOf(Types, selected.Value);

            for (int step = 1; step <= Types.Length; step++)
            {
                GrenadeType next = Types[(start + step) % Types.Length];

                if (!prefabs.ContainsKey(next))
                    continue;

                selected.Value = next;
                return;
            }
        }
    }
}
