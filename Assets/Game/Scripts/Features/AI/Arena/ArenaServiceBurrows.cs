using System.Collections.Generic;
using Game.Arenas;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>The Arenas team's <see cref="IArenaService"/> as the director's <see cref="IArenaBurrows"/>.</summary>
    public sealed class ArenaServiceBurrows : IArenaBurrows
    {
        private static readonly Vector3[] None = new Vector3[0];

        public ArenaServiceBurrows(IArenaService service) => Service = service;

        public IArenaService Service { get; }

        public Bounds Bounds => Service.Bounds;

        public IReadOnlyList<Vector3> BurrowPoints => Service.BurrowPoints ?? None;

        public Vector3 OpenBurrow(int index) => Service.OpenBurrow(index);
    }
}
