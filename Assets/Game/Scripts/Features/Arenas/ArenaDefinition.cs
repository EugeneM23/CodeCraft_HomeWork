using System;
using UnityEngine;

namespace Game.Arenas
{
    /// <summary>How the houses of an arena stand against the line from the entry to the gate.</summary>
    public enum ArenaLayout
    {
        /// <summary>Houses anywhere off the entry and the gate.</summary>
        Scattered,

        /// <summary>A house between the tank and the far burrows: Spitters hide behind it (arena 3).</summary>
        CoverForSpitters,

        /// <summary>A house in the middle, on the line from the entry to the gate (arena 4).</summary>
        HouseBlocksGate
    }

    /// <summary>Hints the HUD shows on an arena (the menu team draws them).</summary>
    [Flags]
    public enum ArenaHint
    {
        None = 0,
        Controls = 1,
        Crush = 2,
        DestroyCover = 4
    }

    /// <summary>
    /// One arena of a chapter: what the generator puts on it. The waves are the AI's (its director
    /// reads <see cref="Id"/>); here is only the ground and what stands on it.
    /// </summary>
    [CreateAssetMenu(fileName = "ArenaDefinition", menuName = "Game/Arenas/Arena")]
    public sealed class ArenaDefinition : ScriptableObject
    {
        [Tooltip("Stable id of the arena (ArenaId of the run contracts): saves and the AI's wave tables refer to it.")]
        [SerializeField] private ArenaId _id;

        [Tooltip("Place of the arena in its chapter, from 1.")]
        [Min(1)]
        [SerializeField] private int _number = 1;

        [SerializeField] private ArenaLayout _layout = ArenaLayout.Scattered;

        [Tooltip("Houses, fewest and most.")]
        [SerializeField] private Vector2Int _houses = new(1, 2);

        [Tooltip("Trees inside the arena, fewest and most (more stand outside its edge).")]
        [SerializeField] private Vector2Int _trees = new(3, 6);

        [Tooltip("Free-standing fence runs besides the yard fences round the houses.")]
        [SerializeField] private Vector2Int _fences = new(1, 2);

        [Tooltip("Height (m) from the lowest dip to the highest rise of the ground.")]
        [Range(0f, 1.6f)]
        [SerializeField] private float _relief = 0.8f;

        [Tooltip("Share of the arena (0..1) the generator aims to have under mud.")]
        [Range(0f, 0.8f)]
        [SerializeField] private float _mud = 0.2f;

        [Tooltip("Burrows round the edge the horde comes out of.")]
        [Range(4, 24)]
        [SerializeField] private int _burrows = 10;

        [SerializeField] private ArenaHint _hints;

        [Tooltip("An elite Regax fights here (the AI spawns it; the arena keeps more room in the middle).")]
        [SerializeField] private bool _elite;

        public ArenaId Id => _id;
        public int Number => _number;
        public ArenaLayout Layout => _layout;
        public Vector2Int Houses => _houses;
        public Vector2Int Trees => _trees;
        public Vector2Int Fences => _fences;
        public float Relief => _relief;
        public float Mud => _mud;
        public int Burrows => _burrows;
        public ArenaHint Hints => _hints;
        public bool Elite => _elite;

        /// <summary>Setup from code (tests and the editor builder); the asset sets the same in the Inspector.</summary>
        public void Configure(ArenaId id, int number, ArenaLayout layout, Vector2Int houses, Vector2Int trees,
            Vector2Int fences, float relief, float mud, int burrows, ArenaHint hints, bool elite)
        {
            _id = id;
            _number = Mathf.Max(1, number);
            _layout = layout;
            _houses = houses;
            _trees = trees;
            _fences = fences;
            _relief = relief;
            _mud = mud;
            _burrows = burrows;
            _hints = hints;
            _elite = elite;
        }
    }
}
