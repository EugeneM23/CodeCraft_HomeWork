namespace Gameplay
{
    public class JumpController : IInitializeble
    {
        public void Initialize()
        {
            ServiceLocator.Get<InputReader>(GameID.InpuReader).OnJump +=
                ServiceLocator.Get<JumpComponent>(PlayerId.JumpComponent).Jump;

            ServiceLocator.Get<CollisionComponent>(PlayerId.CollisionComponent).OnGrounded +=
                ServiceLocator.Get<JumpComponent>(PlayerId.JumpComponent).ResetJump;
        }

        private void OnDisable()
        {
            ServiceLocator.Get<InputReader>(GameID.InpuReader).OnJump -=
                ServiceLocator.Get<JumpComponent>(PlayerId.JumpComponent).Jump;

            ServiceLocator.Get<CollisionComponent>(PlayerId.CollisionComponent).OnGrounded -=
                ServiceLocator.Get<JumpComponent>(PlayerId.JumpComponent).ResetJump;
        }
    }
}