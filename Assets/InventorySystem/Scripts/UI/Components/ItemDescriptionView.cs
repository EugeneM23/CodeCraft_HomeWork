// using System.Collections;
// using Inventories;
// using TMPro;
// using UnityEngine;
//
// public class ItemDescriptionView : MonoBehaviour
// {
//     [SerializeField] private RectTransform _descriptionImage;
//     [SerializeField] private CanvasGroup _canvasGroup;
//     [SerializeField] private float _showDelay = 0.3f;
//     [SerializeField] private float _fadeDuration = 0.2f;
//     [SerializeField] private TMP_Text _descriptionText;
//     [SerializeField] private TMP_Text _nameText;
//     [SerializeField] private DragFSM _dragFSM;
//
//     private InventoryItem _lastItem;
//     private Coroutine _showCoroutine;
//     private Coroutine _fadeCoroutine;
//
//     private void Awake()
//     {
//         _descriptionImage.gameObject.SetActive(false);
//
//         if (_canvasGroup == null)
//             _canvasGroup = _descriptionImage.GetComponent<CanvasGroup>();
//     }
//
//     private void Update()
//     {
//         InventoryItem currentItem = _dragFSM.Context.CurrentItemUnderMouse;
//
//         if (Input.GetMouseButtonDown(0))
//         {
//             HideDescription();
//             return;
//         }
//
//         if (currentItem != _lastItem)
//         {
//             if (currentItem != null)
//                 ShowDescription(currentItem, _lastItem != null);
//             else
//                 HideDescription();
//
//             _lastItem = currentItem;
//         }
//
//         if (_descriptionImage.gameObject.activeSelf)
//             _descriptionImage.position = Input.mousePosition;
//     }
//
//     private void ShowDescription(InventoryItem item, bool instant)
//     {
//         if (_showCoroutine != null)
//             StopCoroutine(_showCoroutine);
//
//         if (_fadeCoroutine != null)
//             StopCoroutine(_fadeCoroutine);
//
//         //_nameText.text = item.Item.itemData.Name;
//         //_descriptionText.text = item.Item.itemData.Description;
//
//         if (instant)
//         {
//             _descriptionImage.gameObject.SetActive(true);
//             _descriptionImage.position = Input.mousePosition;
//             _canvasGroup.alpha = 1f;
//         }
//         else
//         {
//             _showCoroutine = StartCoroutine(ShowWithDelay());
//         }
//     }
//
//     private void HideDescription()
//     {
//         if (_showCoroutine != null)
//             StopCoroutine(_showCoroutine);
//
//         if (_fadeCoroutine != null)
//             StopCoroutine(_fadeCoroutine);
//
//         _fadeCoroutine = StartCoroutine(FadeOut());
//     }
//
//     private IEnumerator ShowWithDelay()
//     {
//         yield return new WaitForSeconds(_showDelay);
//
//         _descriptionImage.gameObject.SetActive(true);
//         _descriptionImage.position = Input.mousePosition;
//
//         if (_fadeCoroutine != null)
//             StopCoroutine(_fadeCoroutine);
//
//         _fadeCoroutine = StartCoroutine(FadeIn());
//     }
//
//     private IEnumerator FadeIn()
//     {
//         if (_canvasGroup == null)
//             yield break;
//
//         float elapsed = 0f;
//         _canvasGroup.alpha = 0f;
//
//         while (elapsed < _fadeDuration)
//         {
//             elapsed += Time.deltaTime;
//             _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / _fadeDuration);
//             yield return null;
//         }
//
//         _canvasGroup.alpha = 1f;
//     }
//
//     private IEnumerator FadeOut()
//     {
//         if (_canvasGroup == null)
//             yield break;
//
//         float elapsed = 0f;
//         float startAlpha = _canvasGroup.alpha;
//
//         while (elapsed < _fadeDuration)
//         {
//             elapsed += Time.deltaTime;
//             _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _fadeDuration);
//             yield return null;
//         }
//
//         _canvasGroup.alpha = 0f;
//         _descriptionImage.gameObject.SetActive(false);
//     }
//
//     private void OnDisable()
//     {
//         _descriptionImage.gameObject.SetActive(false);
//     }
// }