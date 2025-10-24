using System.Collections.Generic;
using UnityEngine;

public static class VectorDebug
{
    private class Arrow
    {
        public GameObject obj;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public Mesh mesh;
        public MaterialPropertyBlock block;
    }

    private static Dictionary<int, Arrow> arrows = new Dictionary<int, Arrow>();

    public static void DrawArrow(int id, Vector2 start, Vector2 dir, Color color,
        float shaftWidth = 0.03f, float headWidth = 5f, float headLength = 0.5f)
    {
        Arrow arrow;

        if (!arrows.TryGetValue(id, out arrow))
        {
            arrow = new Arrow();
            arrow.obj = new GameObject("Arrow2D_" + id);
            arrow.filter = arrow.obj.AddComponent<MeshFilter>();
            arrow.renderer = arrow.obj.AddComponent<MeshRenderer>();
            arrow.renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            arrow.mesh = new Mesh();
            arrow.filter.mesh = arrow.mesh;
            arrow.block = new MaterialPropertyBlock();
            arrows[id] = arrow;
        }

        arrow.block.SetColor("_Color", color);
        arrow.renderer.SetPropertyBlock(arrow.block);

        float length = dir.magnitude;
        if (length < 0.001f) return;

        float shaftLength = Mathf.Max(0.01f, length - headLength);
        Vector2 dirNorm = dir.normalized;
        Vector2 perp = new Vector2(-dirNorm.y, dirNorm.x) * shaftWidth;

        Vector3[] vertices = new Vector3[]
        {
            start + perp,
            start - perp,
            start + perp + dirNorm * shaftLength,
            start - perp + dirNorm * shaftLength,
            start + dirNorm * shaftLength + perp * headWidth,
            start + dirNorm * shaftLength - perp * headWidth,
            start + dirNorm * length
        };

        int[] triangles = new int[]
        {
            0,2,1,
            2,3,1,
            4,6,5
        };

        arrow.mesh.Clear();
        arrow.mesh.vertices = vertices;
        arrow.mesh.triangles = triangles;
        arrow.mesh.RecalculateNormals();
        arrow.mesh.RecalculateBounds();
    }

    public static void ClearAll()
    {
        foreach (var arrow in arrows.Values)
        {
            if (arrow.obj != null)
                Object.Destroy(arrow.obj);
        }
        arrows.Clear();
    }
}
