using Atomic.Contexts;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class DashController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private Ability _dashAbility;

        public void Init(IPlayerContext context)
        {
            _dashAbility = context.GetDashAbility();
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift)) 
                AbilityUseCase.Use(_dashAbility);
        }
    }
}