using UnityEngine;

public class WeaponRecoil : MonoBehaviour
{
    public float recoilDistance = 0.1f;
    public float recoilDuration = 0.1f;
    
    private Vector3 originalPosition;
    private float recoilTimer = 0f;
    private bool isRecoiling = false;

    void Start()
    {
        originalPosition = transform.localPosition;
    }

    void Update()
    {
        if (isRecoiling)
        {
            recoilTimer += Time.deltaTime;
            float progress = recoilTimer / recoilDuration;

            if (progress < 0.5f)
            {
                // Отдача назад (первые 0.1 сек)
                float t = progress * 2f;
                transform.localPosition = Vector3.Lerp(originalPosition, originalPosition + Vector3.back * recoilDistance, t);
            }
            else
            {
                // Возврат на место (вторые 0.1 сек)
                float t = (progress - 0.5f) * 2f;
                transform.localPosition = Vector3.Lerp(originalPosition + Vector3.back * recoilDistance, originalPosition, t);
            }

            if (recoilTimer >= recoilDuration * 2f)
            {
                isRecoiling = false;
                transform.localPosition = originalPosition;
            }
        }
    }

    public void PlayRecoil()
    {
        recoilTimer = 0f;
        isRecoiling = true;
    }
}