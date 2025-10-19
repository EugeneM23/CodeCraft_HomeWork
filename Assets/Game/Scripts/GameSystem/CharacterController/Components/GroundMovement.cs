using System;
using UnityEngine;

public class GroundMovement 
{
    private GravityComponent gravity;

    public GroundMovement(GravityComponent gravity)
    {
        this.gravity = gravity;
    }

    public Vector3 GetOffset(bool grounded)
    {
        if (grounded && gravity.CurrentGround != null)
            return gravity.GetGroundOffset();

        return Vector3.zero;
    }
}