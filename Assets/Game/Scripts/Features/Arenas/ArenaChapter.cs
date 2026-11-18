using System.Collections.Generic;
using UnityEngine;

namespace Game.Arenas
{
    /// <summary>The arenas of one chapter, in the order a run goes through them.</summary>
    [CreateAssetMenu(fileName = "ArenaChapter", menuName = "Game/Arenas/Chapter")]
    public sealed class ArenaChapter : ScriptableObject
    {
        [Tooltip("Stable id of the chapter (ChapterId of the run contracts).")]
        [SerializeField] private ChapterId _id;

        [SerializeField] private List<ArenaDefinition> _arenas = new();

        public ChapterId Id => _id;
        public IReadOnlyList<ArenaDefinition> Arenas => _arenas;

        /// <summary>The arena with this id, or null.</summary>
        public ArenaDefinition Find(ArenaId arenaId)
        {
            for (int i = 0; i < _arenas.Count; i++)
                if (_arenas[i] != null && _arenas[i].Id == arenaId)
                    return _arenas[i];
            return null;
        }

        /// <summary>Arena number <paramref name="number"/> (from 1), or null past the end.</summary>
        public ArenaDefinition At(int number) =>
            number >= 1 && number <= _arenas.Count ? _arenas[number - 1] : null;

        /// <summary>Setup from code (tests and the editor builder).</summary>
        public void Configure(ChapterId id, IEnumerable<ArenaDefinition> arenas)
        {
            _id = id;
            _arenas = new List<ArenaDefinition>(arenas);
        }
    }
}
