using Game.Scripts.Entities.Systems;
using Game.Scripts.UI.Entities.Components;
using Rukhanka;
using Rukhanka.Toolbox;
using Unity.Collections;
using Unity.Entities;

[UpdateAfter(typeof(RukhankaAnimationSystemGroup))]
public partial struct AnimationDamageEventSystem : ISystem
{
    private uint _damageHash;

    public void OnCreate(ref SystemState state)
    {
        _damageHash = FixedStringExtensions.CalculateHash32(new FixedString64Bytes("DealDamage"));
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = state.EntityManager;

        foreach (var (animEvents, entity) in SystemAPI.Query<DynamicBuffer<AnimationEventComponent>>()
                     .WithEntityAccess())
        {
            foreach (var evt in animEvents)
            {
                // Проверяем, что это событие урона
                if (evt.nameHash != _damageHash)
                    continue;

                // Проверяем наличие цели
                if (!manager.HasComponent<Target>(entity))
                    continue;

                Target target = manager.GetComponentData<Target>(entity);

                if (target.Value == Entity.Null || !manager.Exists(target.Value))
                    continue;

                // Проверяем наличие компонента урона у атакующего
                if (!manager.HasComponent<Damage>(entity))
                    continue;

                Damage damage = manager.GetComponentData<Damage>(entity);

                // Создаём запрос на нанесение урона
                Entity damageRequestEntity = ecb.CreateEntity();
                ecb.AddComponent(damageRequestEntity, new DamageRequest
                {
                    Target = target.Value,
                    Attacker = entity,
                    DamageAmount = damage.Value
                });

                Entity hitEffectRequestEntity = ecb.CreateEntity();
                ecb.AddComponent(hitEffectRequestEntity, new HitEffectRequest
                {
                    Target = target.Value
                });

                // Создаём запрос на звук
                Entity soundRequestEntity = ecb.CreateEntity();
                ecb.AddComponent(soundRequestEntity, new DamageSoundRequest
                {
                    Target = target.Value
                });
            }
        }

        ecb.Playback(manager);
        ecb.Dispose();
    }
}