using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// A line in the corner of the screen saying which grenade the throw key throws and how many are
    /// left: switching types is otherwise blind. Redrawn when the selection or any stock changes.
    /// </summary>
    public sealed class GrenadeHudBehaviour : IEntityInit, IEntityDispose
    {
        private const string SWITCH_HINT = "[Tab]";

        private readonly Text _text;
        private IEntity _character;
        private Subscription<GrenadeType> _selection;
        private Dictionary<GrenadeType, Ammo> _ammo;

        public GrenadeHudBehaviour(Text text) => _text = text;

        public void Init(IEntity entity)
        {
            _character = entity;
            _ammo = entity.GetGrenadeAmmo();

            foreach (Ammo stock in _ammo.Values)
                stock.OnStateChanged += Redraw;

            _selection = entity.GetGrenadeSelected().Observe(_ => Redraw());
        }

        public void Dispose(IEntity entity)
        {
            _selection.Dispose();

            foreach (Ammo stock in _ammo.Values)
                stock.OnStateChanged -= Redraw;
        }

        private void Redraw()
        {
            GrenadeType type = _character.GetGrenadeSelected().Value;
            int count = _ammo.TryGetValue(type, out Ammo stock) ? stock.GetCount() : 0;
            _text.text = $"Граната: {Title(type)} ×{count}  {SWITCH_HINT}";
        }

        private static string Title(GrenadeType type) => type switch
        {
            GrenadeType.Frag => "Осколочная",
            GrenadeType.Flash => "Флеш",
            _ => type.ToString()
        };
    }
}
