using Game.Animation;
using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;

[UpdateAfter(typeof(CheckDeathSystem))]
public partial struct HandleDeathSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = state.EntityManager;

        foreach (var (@event, entity) in SystemAPI.Query<DeathEvent>()
                     .WithEntityAccess())
        {
            // Удаляем цель
            if (manager.HasComponent<Target>(entity))
                ecb.RemoveComponent<Target>(entity);

            // Останавливаем навигацию
            if (manager.HasComponent<AgentBody>(entity))
                ecb.RemoveComponent<AgentBody>(entity);

            //Удаляем комануд
            if (manager.HasComponent<TeamMask>(entity))
                ecb.RemoveComponent<TeamMask>(entity);

            // Запускаем анимацию смерти
            ecb.AddComponent(entity, new AnimationEventRequest
            {
                Parameter = ACBase.Death.ToParameterName()
            });

            // Удаляем событие смерти
            ecb.RemoveComponent<DeathEvent>(entity);
        }

        ecb.Playback(manager);
        ecb.Dispose();
    }
}