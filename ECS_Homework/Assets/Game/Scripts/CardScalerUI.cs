using System;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CardScalerUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _image;

    public Vector3 normalScale = new Vector3(0.8f, 0.8f, 0.8f);
    public Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1.1f);
    public float scaleSpeed = 5f; // чем больше, тем быстрее

    private Coroutine scaleCoroutine;

    private void OnEnable()
    {
        _image.color = new Color(1f, 1f, 1f, 0);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // ставим объект на верхний слой UI
        gameObject.transform.SetAsLastSibling();
        _image.color = new Color(1f, 1f, 1f, 1f);

        // запускаем плавное увеличение
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleTo(hoverScale));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _image.color = new Color(1f, 1f, 1f, 0);


        // запускаем плавное уменьшение
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);


        scaleCoroutine = StartCoroutine(ScaleTo(normalScale));
    }

    private IEnumerator ScaleTo(Vector3 targetScale)
    {
        while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
            yield return null;
        }

        transform.localScale = targetScale; // чтобы точно установить финальный размер
    }
}