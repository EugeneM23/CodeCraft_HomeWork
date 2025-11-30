using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(GridLayoutGroup))]
public class GridToMatrix : MonoBehaviour
{
    private GridLayoutGroup grid;
    public CellView[,] matrix;

    public int Rows { get; private set; }
    public int Columns { get; private set; }

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
    }

    /// <summary>
    /// Строит матрицу только из реально существующих CellView в трансформе, учитывая настройки GridLayoutGroup.
    /// Возвращает массив [Columns, Rows].
    /// </summary>
    public CellView[,] BuildMatrix()
    {
        // Форсим layout чтобы дочерние RectTransform имели актуальные данные
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);

        // Собираем список реальных CellView (в порядке GetChild)
        List<CellView> cells = new List<CellView>(transform.childCount);
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var cell = child.GetComponent<CellView>();
            if (cell != null)
                cells.Add(cell);
        }

        int total = cells.Count;

        // Определяем Columns/Rows по constraint
        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            Columns = Mathf.Max(1, grid.constraintCount);
            Rows = Mathf.CeilToInt((float)total / Columns);
        }
        else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount)
        {
            Rows = Mathf.Max(1, grid.constraintCount);
            Columns = Mathf.CeilToInt((float)total / Rows);
        }
        else // Unconstrained — попробуем вычислить колонки по ширине, иначе sqrt fallback
        {
            var rect = (transform as RectTransform).rect;
            float cellW = grid.cellSize.x + grid.spacing.x;
            int guessCols = 0;
            if (cellW > 0f)
                guessCols = Mathf.FloorToInt(Mathf.Max(1f, rect.width / cellW));
            if (guessCols <= 0) guessCols = Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, total)));
            Columns = Mathf.Max(1, guessCols);
            Rows = Mathf.CeilToInt((float)total / Columns);
        }

        // Создаём матрицу точно под существующие ячейки
        matrix = new CellView[Columns, Rows];

        // Важно учитывать startAxis: Horizontal — заполнение по строкам (горизонтально), 
        // Vertical — заполнение по столбцам (снизу-верх/сверху-вниз затем следующий столбец).
        bool startAxisHorizontal = grid.startAxis == GridLayoutGroup.Axis.Horizontal;

        for (int i = 0; i < total; i++)
        {
            int x, y;

            if (startAxisHorizontal)
            {
                // стандартный случай: заполнение по строкам (лево→право, затем вниз)
                x = i % Columns;
                y = i / Columns;
            }
            else
            {
                // заполнение по колонкам (верх→низ, затем следующая колонка)
                // При этом Rows уже рассчитан как Ceil(total / Columns) или фиксированное значение
                x = i / Rows;
                y = i % Rows;
            }

            // Защита на случай, если вдруг вышли за границы
            if (x >= Columns || y >= Rows)
            {
                continue;
            }

            matrix[x, y] = cells[i];
        }

        return matrix;
    }
}