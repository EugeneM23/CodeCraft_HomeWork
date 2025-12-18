using System.Collections;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image _backGround;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Sprite _freeSprite;
    [SerializeField] private Sprite _errorSprite;
    [SerializeField] private float _fadeDuration = 0.2f;

    private Coroutine _fadeCoroutine;
    private Sprite _currentTargetSprite;

    public Vector2Int GridPosition { get; private set; }

    public InventoryItem InventoryItem { get; set; }

    public Inventory Inventory { get; private set; }

    public void Construct(Inventory inventory, Vector2Int gridPosition)
    {
        Inventory = inventory;
        GridPosition = gridPosition;
        _currentTargetSprite = _emptySprite;
    }

    public void Highlight(bool isCorrect)
    {
        Sprite targetSprite = isCorrect ? _freeSprite : _errorSprite;

        if (_currentTargetSprite == targetSprite)
            return;
        FadeToSprite(targetSprite);
    }

    public void UnHighlight()
    {
        if (_currentTargetSprite == _emptySprite)
            return;

        FadeToSprite(_emptySprite);
    }

    public void Clear()
    {
        InventoryItem = null;
    }

    private void FadeToSprite(Sprite newSprite)
    {
        _currentTargetSprite = newSprite;

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeRoutine(newSprite));
    }

    private IEnumerator FadeRoutine(Sprite newSprite)
    {
        Color color = _backGround.color;
        color.a = 0f;
        _backGround.color = color;

        _backGround.sprite = newSprite;

        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, elapsed / _fadeDuration);
            _backGround.color = color;
            yield return null;
        }

        color.a = 0.85f;
        _backGround.color = color;
        _fadeCoroutine = null;
    }
}