namespace Game.Scripts.PlayerController
{
    internal interface IMoveComponent
    {
        float Move(float moveInput, bool isGrounded, float currentXVelocity);
    }
}