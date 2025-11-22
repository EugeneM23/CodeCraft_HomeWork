using UnityEngine;

public class TestAnimIK : StateMachineBehaviour
{
    [Header("Отключить IK на этой анимации")]
    public bool disableRightHand = false;

    public bool disableLeftHand = false;

    [Header("Плавный переход")] public float fadeSpeed = 5f;

    private float currentRightWeight = 1f;
    private float currentLeftWeight = 1f;

    override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Плавно меняем веса
        if (disableRightHand)
        {
            currentRightWeight = Mathf.Lerp(currentRightWeight, 0f, Time.deltaTime * fadeSpeed);
        }
        else
        {
            currentRightWeight = Mathf.Lerp(currentRightWeight, 1f, Time.deltaTime * fadeSpeed);
        }

        if (disableLeftHand)
        {
            currentLeftWeight = Mathf.Lerp(currentLeftWeight, 0f, Time.deltaTime * fadeSpeed);
        }
        else
        {
            currentLeftWeight = Mathf.Lerp(currentLeftWeight, 1f, Time.deltaTime * fadeSpeed);
        }

        // Применяем веса
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, currentRightWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, currentRightWeight);

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, currentLeftWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, currentLeftWeight);
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Возвращаем веса к 1 при выходе
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
    }
}