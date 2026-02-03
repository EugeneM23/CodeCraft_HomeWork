using UnityEngine;
using System.Collections.Generic;

namespace Game.Scripts.UI
{
    /// <summary>
    /// Компонент для расположения UI карт веером (по дуге)
    /// </summary>
    public class CardFanLayout : MonoBehaviour
    {
        [Header("Fan Settings")]
        [Tooltip("Максимальный угол раскрытия веера (в градусах)")]
        [SerializeField] private float maxFanAngle = 30f;
        
        [Tooltip("Расстояние между картами (spacing)")]
        [SerializeField] private float cardSpacing = 100f;
        
        [Tooltip("Высота параболы (чем больше, тем выше центральная карта)")]
        [SerializeField] private float parabolaHeight = 100f;
        
        [Tooltip("Вертикальное смещение всего веера (выше/ниже)")]
        [SerializeField] private float verticalOffset = -50f;
        
        [Tooltip("Наклон карт (дополнительный поворот)")]
        [SerializeField] private float cardTilt = 0f;
        
        [Header("Animation")]
        [Tooltip("Скорость анимации расстановки")]
        [SerializeField] private float animationSpeed = 10f;
        
        [Tooltip("Использовать плавную анимацию")]
        [SerializeField] private bool useAnimation = true;
        
        private List<RectTransform> cards = new List<RectTransform>();
        private List<Vector3> targetPositions = new List<Vector3>();
        private List<Quaternion> targetRotations = new List<Quaternion>();

        private void Start()
        {
            CollectCards();
            CalculatePositions();
            
            if (!useAnimation)
            {
                ApplyPositionsImmediate();
            }
        }

        private void Update()
        {
            if (useAnimation)
            {
                AnimateCards();
            }
        }

        /// <summary>
        /// Собирает все дочерние карты
        /// </summary>
        public void CollectCards()
        {
            cards.Clear();
            
            foreach (Transform child in transform)
            {
                RectTransform rectTransform = child.GetComponent<RectTransform>();
                if (rectTransform != null && child.gameObject.activeSelf)
                {
                    cards.Add(rectTransform);
                }
            }
        }

        /// <summary>
        /// Рассчитывает позиции и повороты для всех карт
        /// </summary>
        public void CalculatePositions()
        {
            targetPositions.Clear();
            targetRotations.Clear();
            
            int cardCount = cards.Count;
            
            if (cardCount == 0)
                return;
            
            // Если одна карта - размещаем по центру
            if (cardCount == 1)
            {
                targetPositions.Add(new Vector3(0, verticalOffset, 0));
                targetRotations.Add(Quaternion.Euler(0, 0, cardTilt));
                return;
            }
            
            // Рассчитываем позиции по параболе
            float angleStep = maxFanAngle / (cardCount - 1);
            float startAngle = -(cardCount - 1) * angleStep / 2f;
            
            for (int i = 0; i < cardCount; i++)
            {
                // Горизонтальная позиция (с учетом spacing)
                float x = (i - (cardCount - 1) / 2f) * cardSpacing;
                
                // Вычисляем Y по формуле параболы: y = -a * x^2 + h
                // Нормализуем x для параболы
                float normalizedX = i / (float)(cardCount - 1); // от 0 до 1
                normalizedX = (normalizedX - 0.5f) * 2f; // от -1 до 1
                
                // Парабола (вершина в центре)
                float y = -parabolaHeight * normalizedX * normalizedX + parabolaHeight + verticalOffset;
                
                Vector3 position = new Vector3(x, y, 0);
                targetPositions.Add(position);
                
                // Угол карты - рассчитываем по касательной к параболе
                float angle = startAngle + (i * angleStep);
                
                // Дополнительно рассчитываем угол по производной параболы для более естественного вида
                float tangentAngle = Mathf.Atan(-2f * parabolaHeight * normalizedX / cardSpacing) * Mathf.Rad2Deg;
                
                Quaternion rotation = Quaternion.Euler(0, 0, angle + tangentAngle + cardTilt);
                targetRotations.Add(rotation);
            }
        }

        /// <summary>
        /// Применяет позиции мгновенно (без анимации)
        /// </summary>
        private void ApplyPositionsImmediate()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (i < targetPositions.Count)
                {
                    cards[i].localPosition = targetPositions[i];
                    cards[i].localRotation = targetRotations[i];
                }
            }
        }

        /// <summary>
        /// Плавная анимация карт к целевым позициям
        /// </summary>
        private void AnimateCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (i < targetPositions.Count)
                {
                    cards[i].localPosition = Vector3.Lerp(
                        cards[i].localPosition,
                        targetPositions[i],
                        Time.deltaTime * animationSpeed
                    );
                    
                    cards[i].localRotation = Quaternion.Lerp(
                        cards[i].localRotation,
                        targetRotations[i],
                        Time.deltaTime * animationSpeed
                    );
                }
            }
        }

        /// <summary>
        /// Обновляет расположение карт (например, при добавлении/удалении карты)
        /// </summary>
        public void UpdateLayout()
        {
            CollectCards();
            CalculatePositions();
            
            if (!useAnimation)
            {
                ApplyPositionsImmediate();
            }
        }

        /// <summary>
        /// Добавляет карту в веер
        /// </summary>
        public void AddCard(RectTransform card)
        {
            if (card != null)
            {
                card.SetParent(transform);
                UpdateLayout();
            }
        }

        /// <summary>
        /// Удаляет карту из веера
        /// </summary>
        public void RemoveCard(RectTransform card)
        {
            if (cards.Contains(card))
            {
                cards.Remove(card);
                UpdateLayout();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Автообновление в редакторе при изменении параметров
            if (Application.isPlaying)
            {
                UpdateLayout();
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Визуализация параболы в редакторе
            Gizmos.color = Color.yellow;
            
            if (cards.Count > 1)
            {
                int cardCount = cards.Count;
                Vector3 previousPoint = Vector3.zero;
                
                for (int i = 0; i <= 50; i++)
                {
                    float t = i / 50f;
                    
                    // Позиция по параболе
                    float x = ((t * (cardCount - 1)) - (cardCount - 1) / 2f) * cardSpacing;
                    float normalizedX = (t - 0.5f) * 2f;
                    float y = -parabolaHeight * normalizedX * normalizedX + parabolaHeight + verticalOffset;
                    
                    Vector3 point = transform.position + new Vector3(x, y, 0);
                    
                    if (i > 0)
                    {
                        Gizmos.DrawLine(previousPoint, point);
                    }
                    
                    previousPoint = point;
                }
            }
        }
#endif
    }
}