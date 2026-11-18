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
        private const float WALL_HOLE_RADIUS = 0.5f;

        /// <summary>Most chips one bullet bite throws: a hole, not a cloud of crumbs.</summary>
        private const int BULLET_DEBRIS = 40;

        /// <summary>How hard (m/s) a plain bullet's hit flings the chips it knocks out.</summary>
        private const float CHIP_FORCE = 9f;

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
        }

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

            if (!mud && !voxel && BulletHitUseCase.ShouldTryRicochet(_bullet, target) && TryRicochet(collision))
                return;

            // A blast marks the mud itself (ExplosionUseCase); a plain bullet leaves a dent here.
            if (mud && !ExplosionUseCase.IsExplosive(_bullet))
                MudImpactUseCase.Bullet(collision.collider, collision.GetContact(0).point,
                    _bullet.GetMoveDirection().Value);

            if (ExplosionUseCase.IsExplosive(_bullet))
                ExplosionUseCase.Explode(_bullet, collision.GetContact(0).point, collision.collider, _gameFactory);
            else if (target != null && target.HasDamageableTag())
            {
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));

                // A person shows the hit with blood; a tank's armour showed nothing at all.
                if (BulletHitUseCase.ShowsHitOnTarget(target))
                {
                    SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);
                    ChipVoxels(collision);
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
            if (body == null || body.IsTerrain)
                return false;

            _wallPierces++;
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = _bullet.GetMoveDirection().Value;
            Vector3 along = direction.sqrMagnitude > 1e-6f ? direction.normalized : -contact.normal;
            // One round hole at the entry (three bites along the path read as three small holes).
            VoxelDamage.Impact(contact.point, along, WALL_HOLE_RADIUS, CHIP_FORCE, BULLET_DEBRIS);
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

        /// <summary>A small fan of hot voxel sparks off the surface.</summary>
        private static void Sparks(Vector3 point, Vector3 normal)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null)
                return;

            Vector3 at = point + normal * 0.03f;
            for (int i = 0; i < SPARKS; i++)
            {
                Vector3 heading = (normal + Random.insideUnitSphere * 0.9f + Vector3.up * 0.3f).normalized;
                Color32 color = SparkColors[Random.Range(0, SparkColors.Length)];
                if (debris.Spawn(at, heading * Random.Range(5f, 14f), color, 0.04f * Random.Range(0.7f, 1.3f)) < 0)
                    break;
            }
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
                if (!BulletShotUseCase.TryPierce(_bullet))
                    return false;
                _pierced = target;
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));
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
        private void ChipVoxels(Collision collision) =>
            VoxelDamage.Impact(collision.GetContact(0).point, _bullet.GetMoveDirection().Value, VOXEL_CHIP_RADIUS,
                ExplosionUseCase.IsExplosive(_bullet) ? 2.5f : CHIP_FORCE,
                ExplosionUseCase.IsExplosive(_bullet) ? VoxelDamage.BLAST_DEBRIS : BULLET_DEBRIS);

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
