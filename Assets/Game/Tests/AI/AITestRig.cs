using System;
using System.Collections.Generic;
using AIKit;
using AIKit.Entities;
using Atomic.Elements;
using Atomic.Entities;
using Game.Horde;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>
    /// A level without a scene for the AI tests: a flat synthetic grid (0.5 m cells over <see cref="HALF"/> m each way,
    /// searched at once), a level entity with the AIKit world (AIKit.Atomic's <see cref="AIWorldInstaller"/>) and the
    /// player's vehicle, a box "tank" on the Character layer, and skinned zombies made of plain Atomic entities.
    /// <see cref="Step"/> plays one frame the way the game does: the level steps the agents, the entity's walk moves the
    /// body, the zombie's behaviour (its motor) puts it on the ground.
    /// </summary>
    internal sealed class AITestRig : IDisposable
    {
        public const float HALF = 20f;
        public const float CELL = 0.5f;
        public const float SPEED = 5f;

        public readonly HordeNavGrid Grid;
        public readonly LevelNav Nav;
        public readonly AIWorld World = new(7);
        public readonly AIAgentList Agents;
        public readonly PlayerTank Tank = new();
        public readonly Entity Level = new("Level");
        public readonly List<Zombie> Zombies = new();

        private readonly List<GameObject> _objects = new();
        private GameObject _tank;

        /// <summary>A skinned zombie: a plain Atomic walker with <see cref="ZombieChaseBehaviour"/>.</summary>
        public sealed class Zombie
        {
            public GameObject GameObject;
            public Transform Transform;
            public Entity Entity;
            public ZombieChaseBehaviour Behaviour;
            public IReactiveVariable<Vector3> MoveDirection;
            public Health Health;
            public int Fired;
        }

        public AITestRig()
        {
            HordeNavGridSettings settings = HordeNavGridSettings.Cover(new float2(-HALF, -HALF), new float2(HALF, HALF), CELL);
            Grid = new HordeNavGrid(settings);
            using (var heights = new NativeArray<float>(Grid.CellCount, Allocator.Temp))
                Grid.SetHeights(heights);
            Nav = new LevelNav(Grid);
            Nav.Paths.Synchronous = true;
            World.Scheduler.MaxDecisionsPerStep = LevelAIInstaller.MAX_DECISIONS_PER_STEP;
            Level.AddLevelNavigation(Nav);
            Agents = AIWorldInstaller.Install(Level, World);
            Level.AddPlayerVehicle(Tank);
        }

        /// <summary>Raises the cells of the world rect <paramref name="min"/>..<paramref name="max"/> (XZ) to a wall.</summary>
        public void Wall(Vector2 min, Vector2 max, float height = 5f)
        {
            int2 a = Grid.ClampCell(new float3(min.x, 0f, min.y));
            int2 b = Grid.ClampCell(new float3(max.x, 0f, max.y));
            for (int z = a.y; z <= b.y; z++)
            for (int x = a.x; x <= b.x; x++)
                Grid.SetHeight(new int2(x, z), height);
        }

        /// <summary>A 3.5 × 2 × 6.5 m box tank with a body, on the Character layer, bound as the player's vehicle.</summary>
        public Entity AddTank(Vector3 position, int health = 400)
        {
            _tank = new GameObject("Tank") { layer = PhysicsLayers.Character };
            _objects.Add(_tank);
            _tank.transform.position = position;
            var box = _tank.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(3.5f, 2f, 6.5f);
            var body = _tank.AddComponent<Rigidbody>();
            body.isKinematic = true;
            Physics.SyncTransforms();

            var entity = new Entity("Tank");
            entity.AddTransform(_tank.transform);
            entity.AddHealth(new Health(health, health));
            Tank.Bind(entity);
            return entity;
        }

        public Transform TankTransform => _tank != null ? _tank.transform : null;

        /// <summary>A zombie at <paramref name="position"/>, joined to the level.</summary>
        public Zombie AddZombie(Vector3 position, int health = 60)
        {
            var go = new GameObject("Zombie") { layer = PhysicsLayers.Enemy };
            _objects.Add(go);
            go.transform.position = position;

            var zombie = new Zombie { GameObject = go, Transform = go.transform };
            var entity = new Entity("Zombie");
            entity.AddTransform(go.transform);
            zombie.MoveDirection = new ReactiveVariable<Vector3>();
            entity.AddMoveDirection(zombie.MoveDirection);
            entity.AddRotateDirection(new ReactiveVariable<Vector3>());
            entity.AddMoveSpeed(new ReactiveFloat(SPEED));
            zombie.Health = new Health(health, health);
            entity.AddHealth(zombie.Health);
            entity.AddFireAction(new InlineAction(() => zombie.Fired++));
            zombie.Entity = entity;

            zombie.Behaviour = new ZombieChaseBehaviour(Level);
            zombie.Behaviour.Init(entity);
            Zombies.Add(zombie);
            return zombie;
        }

        /// <summary>One frame: the level steps the agents, each entity walks (MoveDirection × speed), the motor grounds it.</summary>
        public void Step(float dt)
        {
            Agents.Step(dt);
            ZombieRing.Invalidate();                       // EditMode: the frame count stands still
            for (int i = 0; i < Zombies.Count; i++)
            {
                Zombie zombie = Zombies[i];
                if (!zombie.Health.IsEmpty())
                    zombie.Transform.position += zombie.MoveDirection.Value * (SPEED * dt);
                zombie.Behaviour.Tick(zombie.Entity, dt);
            }
        }

        public void Run(float seconds, float dt = 1f / 30f)
        {
            for (float t = 0f; t < seconds; t += dt)
                Step(dt);
        }

        public void Dispose()
        {
            foreach (Zombie zombie in Zombies)
                zombie.Behaviour.Dispose(zombie.Entity);
            Zombies.Clear();
            foreach (GameObject go in _objects)
                if (go != null)
                    Object.DestroyImmediate(go);
            _objects.Clear();
            Nav.Dispose();
            Grid.Dispose();
        }
    }
}
