using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    public class BulletCollisionBehaviour : IEntityInit, IEntityEnable, IEntityDispose
    {
        /// <summary>Radius (m) of the bite a bullet takes out of voxels: one or two 10 cm voxels.</summary>
        private const float VOXEL_CHIP_RADIUS = 0.13f;

        /// <summary>How deep (m) a bullet's tunnel through a wall goes; bites every this much / 3.</summary>
        private const float WALL_TUNNEL_DEPTH = 0.9f;

        /// <summary>Radius (m) of the hole a plain bullet bores through a wall (tough walls, armour, shrink it).</summary>
        private const float WALL_HOLE_RADIUS = 0.6f;

        /// <summary>Most chips one bullet bite throws: a hole, not a cloud of crumbs.</summary>
        private const int BULLET_DEBRIS = 40;

        /// <summary>Voxel bodies tougher than this (lower toughness: armour) are not bored through.</summary>
        private const float ARMOUR_TOUGHNESS = 0.7f;

        /// <summary>How hard (m/s) a plain bullet's hit flings the chips it knocks out.</summary>
        private const float CHIP_FORCE = 14f;

        private const int SPARKS = 0;

        /// <summary>Walls one bullet goes through before it stops in the next.</summary>
        private const int MAX_WALL_PIERCES = 3;

        private static readonly Color32[] SparkColors =
        {
            new(255, 214, 90, 255), new(255, 160, 40, 255), new(255, 240, 170, 255)
        };

        private int _wallPierces;

        private CollisionEventReceiver _collisionEventReceiver;
        private IEntity _bullet;
        private GameFactory _gameFactory;
        private Collider _own;
        private Rigidbody _body;

        /// <summary>The enemy a piercing shell last went through: its other colliders let the shell by for free.</summary>
        private IEntity _pierced;

        public void Init(IEntity entity)
        {
            _gameFactory = GameContext.Instance.GetGameFactory();
            _collisionEventReceiver = entity.GetCollisionReceiver();
            _bullet = entity;
            _collisionEventReceiver.OnEntered += OnCollision;

            GameObject go = entity.GetGameObject();
            _own = go.GetComponent<Collider>();
            _body = go.GetComponent<Rigidbody>();
        }

        /// <summary>Rented from the pool: a new shot has pierced nothing yet.</summary>
        public void Enable(IEntity entity)
        {
            _pierced = null;
            _wallPierces = 0;
            _armour = ShellArmourUseCase.State.Fresh;
        }

        /// <summary>An armour-piercing shell's way through a tank's armour (ShellArmourUseCase).</summary>
        private ShellArmourUseCase.State _armour = ShellArmourUseCase.State.Fresh;

        public void Dispose(IEntity entity) => _collisionEventReceiver.OnEntered -= OnCollision;

        private void OnCollision(Collision collision)
        {
            // A bullet touching several colliders gets one OnCollisionEnter per collider in the same
            // physics step, even after the first one returned it to the pool. Only the first counts:
            // otherwise one bullet deals damage several times and is returned to the pool twice.
            if (!_bullet.GetGameObject().activeInHierarchy)
                return;

            collision.gameObject.TryGetComponent(out IEntity target);

            // The armour-piercing shell goes through enemies while it has pierce left, dealing its damage.
            if (target != null && TryPierce(target, collision.collider))
                return;

            // An armour-piercing shell on a tank's armour: ricochet, stuck, or through it (TankArmour zones).
            if (TryArmour(collision))
                return;

            // Mud swallows a hit: nothing bounces off it, it only gets a dent.
            bool mud = MudImpactUseCase.IsMud(collision.collider);

            // Ricochet comes first, for explosive projectiles too: the tank shell bounces off walls
            // exactly as bullets do and goes off once it runs out of ricochets. It used to explode
            // before ricochet was even looked at.
            // Voxels take every hit: a shell digs into voxel ground or brick instead of skipping
            // off it, the way it went off on the ground the cursor pointed at.
            bool voxel = VoxelDamage.IsVoxel(collision.collider);
            bool explosive = ExplosionUseCase.IsExplosive(_bullet);

            // A plain bullet (the machine gun, the turrets) goes through voxel walls: it tunnels on, chips flying.
            if (voxel && !explosive && TryPierceWall(collision))
                return;

            // A cannon shell of a type (AmmoUseCase) glances off any surface by its angle, voxel ground and walls
            // too: a ricochet by its type's rule, with a scrape. Other projectiles keep the prefab's rule off solids.
            if (AmmoUseCase.IsShell(_bullet))
            {
                if (!mud && (target == null || !target.HasDamageableTag()) && TryShellRicochet(collision))
                    return;
                // An armour-piercing shell goes on through a house's walls (ShellHouseUseCase).
                if (voxel && !mud && TryShellThroughHouse(collision))
                    return;
            }
            else if (!mud && !voxel && BulletHitUseCase.ShouldTryRicochet(_bullet, target) && TryRicochet(collision))
                return;

            // A blast marks the mud itself (ExplosionUseCase); a plain bullet leaves a dent here.
            if (mud && !ExplosionUseCase.IsExplosive(_bullet))
                MudImpactUseCase.Bullet(collision.collider, collision.GetContact(0).point,
                    _bullet.GetMoveDirection().Value);

            if (ExplosionUseCase.IsExplosive(_bullet))
            {
                // No area damage (WeaponSettings.shellAreaDamage off): the enemy hit takes the shell's damage itself.
                if (!ExplosionUseCase.AreaDamage && target != null && target.HasDamageableTag())
                    DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));
                ContactPoint contact = collision.GetContact(0);
                ExplosionUseCase.Explode(_bullet, contact.point, collision.collider, _gameFactory, contact.normal,
                    target != null && target.HasEnemyTag());
            }
            else if (target != null && target.HasDamageableTag())
            {
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));

                // A person shows the hit with blood; a tank's armour showed nothing at all.
                if (BulletHitUseCase.ShowsHitOnTarget(target))
                {
                    // On a vehicle's steel: sparks, not the particle burst.
                    Sparks(collision.GetContact(0).point, collision.GetContact(0).normal);
                    ChipVoxels(collision);
                }
                else
                {
                    // Flesh bleeds by itself; the slug still glows a moment where it went in.
                    ContactPoint contact = collision.GetContact(0);
                    BulletHotSpots.Emit(contact.point, contact.normal);
                }
            }
            else
            {
                // A plain bullet's hit is voxel: sparks and chips, no particle burst.
                if (explosive)
                    SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);
                else
                    Sparks(collision.GetContact(0).point, collision.GetContact(0).normal);
                ChipVoxels(collision);
            }

            SpawnUseCase.UnSpawnEntity(_gameFactory, _bullet);
        }

        /// <summary>
        /// The bullet bores through a voxel wall (not the ground) and flies on: bites along its path, sparks at the
        /// hole, the wall's collider let by. False when it has gone through its last wall or this is the ground.
        /// </summary>
        private bool TryPierceWall(Collision collision)
        {
            if (_wallPierces >= MAX_WALL_PIERCES)
                return false;

            VoxelBody body = collision.collider.GetComponentInParent<VoxelBody>();
            // Not the ground, and not armour: a tank's steel (toughness well under brick's 1) stops the bullet.
            if (body == null || body.IsTerrain || body.Toughness < ARMOUR_TOUGHNESS)
                return false;

            _wallPierces++;
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = _bullet.GetMoveDirection().Value;
            Vector3 along = direction.sqrMagnitude > 1e-6f ? direction.normalized : -contact.normal;
            // One round hole at the entry (three bites along the path read as three small holes).
            VoxelDamage.Punch(contact.point, along, WALL_HOLE_RADIUS, CHIP_FORCE, BULLET_DEBRIS, body);
            Sparks(contact.point, contact.normal);

            if (_own != null)
                Physics.IgnoreCollision(_own, collision.collider, true);
            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            return true;
        }

        /// <summary>
        /// A juicy spray of glowing voxel sparks and a flash of light (VoxelSparks), and the slug's hot point cooling
        /// where it struck (BulletHotSpots).
        /// </summary>
        private void Sparks(Vector3 point, Vector3 normal)
        {
            VoxelSparks.Emit(point, normal, _bullet.GetMoveDirection().Value);
            BulletHotSpots.Emit(point, normal);
        }

        /// <summary>
        /// True when the shell flies on through <paramref name="target"/>: an enemy, and the shell either has a pierce
        /// left (used up here, with the hit) or just went through this same enemy (another of its colliders). The
        /// pair is ignored from now on, so the sweep and the solver let the shell by; the pool's deactivation
        /// forgets the pair again.
        /// </summary>
        private bool TryPierce(IEntity target, Collider collider)
        {
            if (!target.HasEnemyTag() || !target.HasDamageableTag())
                return false;

            if (!ReferenceEquals(target, _pierced))
            {
                // A plain bullet (the machine gun) always goes through a monster, blood bursting out along the shot;
                // a shell only as far as its pierce count lets it.
                bool plain = !ExplosionUseCase.IsExplosive(_bullet) && !AmmoUseCase.IsShell(_bullet);
                if (!plain && !BulletShotUseCase.TryPierce(_bullet))
                    return false;
                _pierced = target;
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));
                if (plain)
                    BulletEnemyHitUseCase.SprayThrough(collider != null ? collider.ClosestPoint(_bullet.GetTransform().position)
                        : _bullet.GetTransform().position, _bullet.GetMoveDirection().Value);
            }

            if (_own != null && collider != null)
                Physics.IgnoreCollision(_own, collider, true);

            // The solver has already pushed the dynamic body off the enemy: the shell flies by its transform.
            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            return true;
        }

        /// <summary>A bullet bites a small hole out of a voxel wall, or out of a vehicle's voxel skin.</summary>
        private void ChipVoxels(Collision collision)
        {
            Vector3 point = collision.GetContact(0).point;
            Vector3 direction = _bullet.GetMoveDirection().Value;
            if (ExplosionUseCase.IsExplosive(_bullet))
            {
                VoxelDamage.Impact(point, direction, VOXEL_CHIP_RADIUS, 2.5f, VoxelDamage.BLAST_DEBRIS);
                return;
            }

            // A plain bullet stopped by armour still punches a hole the size of the one it bores through a wall,
            // chips flying in through it; on the ground it only chips.
            VoxelBody body = collision.collider.GetComponentInParent<VoxelBody>();
            if (body != null && !body.IsTerrain)
                VoxelDamage.Punch(point, direction, WALL_HOLE_RADIUS, CHIP_FORCE, BULLET_DEBRIS, body);
            else
                VoxelDamage.Impact(point, direction, VOXEL_CHIP_RADIUS, CHIP_FORCE, BULLET_DEBRIS);
        }

        /// <summary>A typed shell's ricochet by angle off any surface: true when it flies on.</summary>
        private bool TryShellRicochet(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = _bullet.GetMoveDirection().Value;
            if (RicochetUseCase.IsMovingAway(direction, contact.normal))
                return true;
            Vector3 normal = AmmoUseCase.SurfaceNormal(collision.collider, contact.point, direction, contact.normal);
            if (!AmmoUseCase.ShouldRicochet(_bullet, direction, normal))
                return false;
            AmmoUseCase.Ricochet(_bullet, contact.point, normal, collision.collider);
            StopSolverPush();
            return true;
        }

        /// <summary>An armour-piercing shell on a tank's armour: true when it is dealt with here.</summary>
        private bool TryArmour(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            switch (ShellArmourUseCase.Resolve(_bullet, collision.collider, contact.point, contact.normal, ref _armour))
            {
                case ShellArmourUseCase.Step.Ricocheted:
                    StopSolverPush();
                    return true;
                case ShellArmourUseCase.Step.PassedOn:
                    if (_own != null)
                        Physics.IgnoreCollision(_own, collision.collider, true);
                    StopSolverPush();
                    return true;
                case ShellArmourUseCase.Step.Stopped:
                    SpawnUseCase.UnSpawnEntity(_gameFactory, _bullet);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// An armour-piercing shell through a house's wall: put just past its far face to fly on (true). False when it
        /// is spent in this wall (its entry hole is punched; the small burst follows) or this is not a house.
        /// </summary>
        private bool TryShellThroughHouse(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            switch (ShellHouseUseCase.Resolve(_bullet, collision.collider, contact.point, ref _armour.Penetration,
                        out Vector3 exit))
            {
                case ShellHouseUseCase.Step.PassedThrough:
                    PutPast(exit);
                    return true;
                case ShellHouseUseCase.Step.Hollow:
                    // A hole's box the colliders have not caught up with, ahead: past its far face. One behind: nothing.
                    Vector3 dir = _bullet.GetMoveDirection().Value.normalized;
                    Transform shell = _bullet.GetTransform();
                    var back = new Ray(contact.point + dir * 3f, -dir);
                    if (Vector3.Dot(contact.point - shell.position, dir) > -0.15f &&
                        collision.collider.Raycast(back, out RaycastHit far, 3f))
                        PutPast(far.point);
                    else
                        StopSolverPush();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The shell put just past <paramref name="face"/> along its flight, to fly on from there.</summary>
        private void PutPast(Vector3 face)
        {
            Vector3 dir = _bullet.GetMoveDirection().Value.normalized;
            float skin = _own != null ? _own.bounds.extents.x + 0.05f : 0.1f;
            Vector3 at = face + dir * skin;
            _bullet.GetTransform().position = at;
            if (_body != null)
                _body.position = at;
            StopSolverPush();
        }

        /// <summary>The shell flies by its transform: the solver's push off what it touched is dropped.</summary>
        private void StopSolverPush()
        {
            if (_body == null || _body.isKinematic)
                return;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        /// <summary>True when the bullet flies on: it bounced off the surface, or already flies away from it.</summary>
        private bool TryRicochet(Collision collision)
        {
            Vector3 normal = collision.GetContact(0).normal;

            if (RicochetUseCase.IsMovingAway(_bullet.GetMoveDirection().Value, normal))
                return true;

            if (!RicochetUseCase.CanRicochet(_bullet, normal))
                return false;

            SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);
            RicochetUseCase.Ricochet(_bullet, normal);
            return true;
        }
    }
}
