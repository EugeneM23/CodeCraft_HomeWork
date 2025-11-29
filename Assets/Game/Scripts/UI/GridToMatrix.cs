using UnityEngine;
using UnityEngine.UI;

public class GridToMatrix : MonoBehaviour
{
    private GridLayoutGroup grid;
    public CellView[,] matrix;   // теперь CellView!

    public int Rows { get; private set; }
    public int Columns { get; private set; }

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        BuildMatrix();
    }

    public void BuildMatrix()
    {
        int total = transform.childCount;

        // Вычисляем количество строк и столбцов
        switch (grid.constraint)
        {
            case GridLayoutGroup.Constraint.FixedColumnCount:
                Columns = grid.constraintCount;
                Rows = Mathf.CeilToInt((float)total / Columns);
                break;

            case GridLayoutGroup.Constraint.FixedRowCount:
                Rows = grid.constraintCount;
                Columns = Mathf.CeilToInt((float)total / Rows);
                break;

            default:
                Columns = Mathf.CeilToInt(Mathf.Sqrt(total));
                Rows = Mathf.CeilToInt((float)total / Columns);
                break;
        }

        matrix = new CellView[Rows, Columns];

        for (int i = 0; i < total; i++)
        {
            int row = i / Columns;
            int col = i % Columns;

            Transform child = transform.GetChild(i);
            CellView cell = child.GetComponent<CellView>();

            if (cell == null)
            {
                Debug.LogWarning($"Объект {child.name} не содержит CellView!");
                continue;
            }

            matrix[row, col] = cell;
        }
    }

    public CellView GetCell(int row, int col)
    {
        if (row < 0 || col < 0 || row >= Rows || col >= Columns)
            return null;

        return matrix[row, col];
    }

    public bool TryGetCellPosition(CellView cell, out int row, out int col)
    {
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Columns; c++)
            if (matrix[r, c] == cell)
            {
                row = r;
                col = c;
                return true;
            }

        row = col = -1;
        return false;
    }
}
