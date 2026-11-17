using System;
using Atomic.Entities;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Gives the entities of the level's world their sounds, so the tank and Regax prefabs need none of their own.
    /// Looks at every entity in the world once and at every one added later:
    ///   the tank (has Suspension) — engine, tracks, turret, hits, its guns;
    ///   an enemy (Enemy tag) — <see cref="MonsterAudioBehaviour"/>.
    /// An entity that already has its sound behaviours (a pooled one coming back) is left alone.
    /// </summary>
    public sealed class EntityAudioDirector : IDisposable
    {
        private readonly IEntityWorld<IEntity> _world;
        private readonly Action<IEntity> _onAdded;

        public EntityAudioDirector(IEntityWorld<IEntity> world)
        {
            _world = world;
            _onAdded = Attach;
            _world.OnAdded += _onAdded;

            // Copied first: attaching must not run inside the world's own enumeration. Once per level.
            IEntity[] present = new IEntity[_world.Count];
            _world.CopyTo(present, 0);
            for (int i = 0; i < present.Length; i++)
                Attach(present[i]);
        }

        public void Dispose() => _world.OnAdded -= _onAdded;

        public static void Attach(IEntity entity)
        {
            if (entity == null || !entity.IsValid)
                return;

            if (entity.HasSuspension())
                AttachTank(entity);
            else if (entity.HasEnemyTag())
                AttachMonster(entity);
        }

        private static void AttachTank(IEntity tank)
        {
            if (tank.HasBehaviour<TankTracksAudioBehaviour>() || !tank.HasTransform())
                return;

            if (tank.TryGetBehaviour(out TankEngine engine))
                tank.AddBehaviour(new TankEngineAudioBehaviour(engine));

            tank.TryGetBehaviour(out TankWheelSpinBehaviour wheelSpin);
            tank.AddBehaviour(new TankTracksAudioBehaviour(wheelSpin));

            if (tank.HasTurretPivot() && tank.HasTurretRotationSpeed())
                tank.AddBehaviour(new TankTurretAudioBehaviour());

            if (tank.HasHealth())
                tank.AddBehaviour(new TankHealthAudioBehaviour());

            if (tank.HasWeapon() || tank.HasSecondaryWeapon())
                tank.AddBehaviour(new TankWeaponsAudioBehaviour());
        }

        private static void AttachMonster(IEntity monster)
        {
            if (monster.HasBehaviour<MonsterAudioBehaviour>() || !monster.HasTransform())
                return;

            monster.AddBehaviour(new MonsterAudioBehaviour());
        }
    }
}
