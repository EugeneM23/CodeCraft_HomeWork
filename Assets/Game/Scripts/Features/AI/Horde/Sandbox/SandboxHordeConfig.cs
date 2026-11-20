using Game.Horde;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The voxel sandbox's live horde (<see cref="SandboxHorde"/>): its art, the group that comes at the tank and where,
    /// how soon a new group follows a wiped-out one, and the map the level grid must cover. Loaded from Resources
    /// (<c>SandboxHordeConfig</c>), so the sandbox scenes need no edit.
    /// </summary>
    [CreateAssetMenu(fileName = "SandboxHordeConfig", menuName = "Game/Horde/Sandbox Horde Config")]
    public sealed class SandboxHordeConfig : ScriptableObject
    {
        public const string RESOURCE = "SandboxHordeConfig";

        [Tooltip("Baked crowd art (Tools/Horde/Bake Zombie VAT).")]
        public HordeArtConfig art;

        [Header("Group")]
        [Tooltip("Crawlers in one group.")]
        public int groupSize = 20;

        [Tooltip("The first group comes this long after the sandbox starts, s.")]
        public float firstDelay = 3f;

        [Tooltip("A new group comes this long after the last one is wiped out, s.")]
        public float respawnDelay = 12f;

        [Tooltip("Where the group rises out of the ground: open grass a short drive from the tank's start (x −34, facing +X).")]
        public Vector3 spawnCenter = new(-16f, 0f, 14f);

        [Tooltip("The group is spread over a disc this wide round the centre, m.")]
        public float spawnRadius = 4.5f;

        [Header("Level")]
        [Tooltip("XZ corners of the sandbox map: the level grid is kept over all of it (the run's arena would shrink it to the town's middle and leave the tank's start off the grid).")]
        public Vector2 mapMin = new(-40f, -40f);

        public Vector2 mapMax = new(40f, 40f);

        [Tooltip("Lowest and highest ground of the map, m (the grid's rays).")]
        public float groundBottom = -12f;

        public float groundTop = 16f;

        [Header("Horde")]
        [Tooltip("The sandbox horde: tankDamageScale 0.25 — twenty crawlers with six riders on the hull would kill the 1000-HP Tiger in under ten seconds and end the run.")]
        public HordeOptions horde = new()
        {
            capacity = 256, population = 0, castShadows = false, tankDamageScale = 0.25f,
            // No leaping onto the tank for now (the user): they walk up and attack from the ground.
            riders = new HordeRiderOptions { enabled = false }
        };
    }
}
