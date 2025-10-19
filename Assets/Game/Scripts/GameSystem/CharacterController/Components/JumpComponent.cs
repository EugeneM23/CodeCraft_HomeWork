using UnityEngine;

public class JumpComponent
{
    private readonly GravityComponent gravityComponent;
    private float jumpForce;
    private bool jumpRequested = false;

    public JumpComponent(float jumpForce, GravityComponent gravityComponent)
    {
        this.jumpForce = jumpForce;
        this.gravityComponent = gravityComponent;
    }

    public void RequestJump()
    {
        gravityComponent.AddImpulse(jumpForce);
    }

    public bool ConsumeJumpRequest()
    {
        if (!jumpRequested) return false;
        jumpRequested = false;
        return true;
    }

    public float GetJumpForce() => jumpForce;
}