using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using Inventories;

public class ItemDescriptionView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [SerializeField] private RectTransform _descriptionImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _showDelay = 0.3f;
    [SerializeField] private float _fadeDuration = 0.2f;
    [SerializeField] private InventoryItem _item;
    private bool _isPointerOver = false;
    private Coroutine _showCoroutine;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _descriptionImage.gameObject.SetActive(false);

        if (_canvasGroup == null)
            _canvasGroup = _descriptionImage.GetComponent<CanvasGroup>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;

        _descriptionImage.gameObject.transform.SetParent(gameObject.transform.parent);
        _descriptionImage.transform.SetAsLastSibling();

        if (_showCoroutine != null)
            StopCoroutine(_showCoroutine);

        _showCoroutine = StartCoroutine(ShowWithDelay());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOver = false;

        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        HideInstant();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (_descriptionImage != null && _descriptionImage.gameObject.activeSelf)
        {
            _descriptionImage.position = Input.mousePosition;
        }
    }

    private IEnumerator ShowWithDelay()
    {
        yield return new WaitForSeconds(_showDelay);

        if (_descriptionImage != null && _isPointerOver)
        {
            _descriptionImage.gameObject.SetActive(true);
            _descriptionImage.position = Input.mousePosition;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeIn());
        }
    }

    private IEnumerator FadeIn()
    {
        if (_canvasGroup == null)
            yield break;

        float elapsed = 0f;
        _canvasGroup.alpha = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / _fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
    }

    private void HideInstant()
    {
        if (_descriptionImage != null)
        {
            _descriptionImage.gameObject.SetActive(false);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
    }
}