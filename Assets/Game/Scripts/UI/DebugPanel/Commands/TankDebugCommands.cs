using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The debug panel's «Танк» tab: god mode, full health, reload, a cannon without reload, teleport to the aim point.
    /// God mode has no flag in the health code: the tank's DamageTakenEvent (raised inside Health.Reduce, before the
    /// empty check) puts the health back to max, so the killing blow never empties it; a tick keeps it full and follows
    /// a new tank.
    /// </summary>
    public static class TankDebugCommands
    {
        public const string CATEGORY = "Танк";
        private static readonly string[] HealthButtons = { "Полное здоровье", "Перезарядить" };
        private static readonly string[] MoveButtons = { "Телепорт к прицелу" };

        private static bool s_god;
        private static bool s_noReload;
        private static IEntity s_guarded;
        private static IEvent<TakeDamageArgs> s_guardedEvent;
        private static Action<TakeDamageArgs> s_onDamage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_god = false;
            s_noReload = false;
            s_guarded = null;
            s_guardedEvent = null;
        }

        public static void Register()
        {
            s_onDamage = OnDamage;
            DebugPanelRegistry.AddInfo(CATEGORY, HealthLine);
            DebugPanelRegistry.AddToggle(CATEGORY, "Бессмертие", () => s_god, SetGod);
            DebugPanelRegistry.AddToggle(CATEGORY, "Пушка без перезарядки", () => s_noReload, v => s_noReload = v);
            DebugPanelRegistry.AddButtons(CATEGORY, null, HealthButtons, PressHealth);
            DebugPanelRegistry.AddButtons(CATEGORY, null, MoveButtons, _ => TeleportToAim());
            DebugPanelRegistry.Ticked += Tick;
        }

        private static string HealthLine()
        {
            if (!DebugLookup.TryGetTank(out IEntity tank) || !tank.TryGetHealth(out Health health))
                return "Танка нет.";

            Transform root = DebugLookup.RootOf(tank);
            string at = root != null ? $"{root.position.x:0}, {root.position.y:0}, {root.position.z:0}" : "-";
            return $"Здоровье {health.GetCurrent()}/{health.GetMax()}, позиция {at}";
        }

        private static void SetGod(bool on)
        {
            s_god = on;
            if (!on)
                Unguard();
            else
                Tick();
        }

        private static void PressHealth(int button)
        {
            if (!DebugLookup.TryGetTank(out IEntity tank))
                return;

            if (button == 0)
            {
                if (tank.TryGetHealth(out Health health))
                    health.SetCurrent(health.GetMax());
                return;
            }

            Reload(tank, true);
        }

        private static void Tick()
        {
            if (!s_god && !s_noReload)
                return;

            if (!DebugLookup.TryGetTank(out IEntity tank))
                return;

            if (s_god)
            {
                if (!ReferenceEquals(tank, s_guarded))
                    Guard(tank);
                if (tank.TryGetHealth(out Health health) && health.GetCurrent() > 0 && !health.IsFull())
                    health.SetCurrent(health.GetMax());
            }

            if (s_noReload)
                Reload(tank, false);
        }

        private static void Guard(IEntity tank)
        {
            Unguard();
            if (!tank.TryGetDamageTakenEvent(out IEvent<TakeDamageArgs> damage))
                return;

            s_guarded = tank;
            s_guardedEvent = damage;
            damage.Subscribe(s_onDamage);
        }

        private static void Unguard()
        {
            s_guardedEvent?.Unsubscribe(s_onDamage);
            s_guarded = null;
            s_guardedEvent = null;
        }

        private static void OnDamage(TakeDamageArgs _)
        {
            if (s_god && s_guarded != null && s_guarded.TryGetHealth(out Health health))
                health.SetCurrent(health.GetMax());
        }

        // The cannon (and the machine gun on a press) ready to fire now
        private static void Reload(IEntity tank, bool both)
        {
            ReadyWeapon(tank.TryGetWeapon(out IReactiveVariable<IEntity> cannon) ? cannon.Value : null);
            if (both)
                ReadyWeapon(tank.TryGetSecondaryWeapon(out IReactiveVariable<IEntity> gun) ? gun.Value : null);
        }

        private static void ReadyWeapon(IEntity weapon)
        {
            if (weapon != null && weapon.TryGetWeaponCooldown(out CooldownTimer cooldown) && cooldown != null
                && !cooldown.IsExpired())
                cooldown.Tick(cooldown.Duration);
        }

        // Like the arena's PlaceTank: velocities zeroed, body and transform moved, the camera rig carried along
        private static void TeleportToAim()
        {
            if (!DebugLookup.TryGetTank(out IEntity tank) || !DebugLookup.TryGetAimPoint(out Vector3 point))
                return;

            Transform root = DebugLookup.RootOf(tank);
            if (root == null)
                return;

            Vector3 at = DebugLookup.Ground(point) + Vector3.up * 1f;
            Vector3 moved = at - root.position;
            Vector3 forward = root.forward;
            forward.y = 0f;
            Quaternion facing = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward);

            if (tank.TryGetRiggedBody(out Rigidbody body) && body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.transform.SetPositionAndRotation(body.transform.position + moved, facing);
                body.position = body.transform.position;
                body.rotation = facing;
            }
            else
                root.SetPositionAndRotation(at, facing);

            Physics.SyncTransforms();
            if (GameContext.TryGetInstance(out GameContext context) && context.TryGetPlayerContext(out PlayerContext player)
                && player != null && player.TryGetCameraRoot(out Transform rig) && rig != null)
                rig.position += moved;
        }
    }
}
