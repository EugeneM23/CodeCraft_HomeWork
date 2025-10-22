using System;
using UnityEngine;

public class GroundOffsetComponent
{
    private readonly GravityComponent _gravity;

    public GroundOffsetComponent(GravityComponent gravity)
    {
        _gravity = gravity;
    }

    public Vector3 GetOffset()

    {
        if (_gravity.IsGrounded && _gravity.CurrentGround != null)
            return _gravity.GetGroundOffset();

        return Vector3.zero;
    }
}