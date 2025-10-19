using UnityEngine;

public class RotationComponent 
{
    private Quaternion startRotation;

    public RotationComponent(Quaternion startRotation)
    {
        this.startRotation = startRotation;
    }

    public void ApplyRotation(bool grounded, Vector2 normal, Transform target)
    {
        Quaternion targetRot = grounded ? Quaternion.FromToRotation(Vector3.up, normal) : startRotation;
        target.rotation = Quaternion.RotateTowards(target.rotation, targetRot, 360f * Time.deltaTime);
    }
}