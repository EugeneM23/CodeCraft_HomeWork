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
        // Если не на земле — возвращаем в исходный поворот
        if (!grounded)
        {
            RotateToStart(target);
            return;
        }

        // Вычисляем угол наклона поверхности
        float angle = Vector2.Angle(normal, Vector2.up);

        // Если поверхность слишком крутая — не вращаем по нормали
        if (angle > 55f)
        {
            RotateToStart(target);
            return;
        }

        // Иначе — плавно вращаем по нормали
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, normal);
        target.rotation = Quaternion.RotateTowards(target.rotation, targetRot, 360f * Time.deltaTime);
    }

    private void RotateToStart(Transform target)
    {
        target.rotation = Quaternion.RotateTowards(target.rotation, startRotation, 360f * Time.deltaTime);
    }
}