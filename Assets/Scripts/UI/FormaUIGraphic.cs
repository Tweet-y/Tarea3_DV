using UnityEngine;
using UnityEngine.UI;

/// <summary>Cuadrilátero de interfaz con inclinación y degradado opcionales; no necesita sprites.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class FormaUIGraphic : MaskableGraphic
{
    /// <summary>Píxeles que el borde superior se desplaza a la derecha respecto del inferior.</summary>
    public float Inclinacion;
    public bool Degradado;
    /// <summary>Con degradado: de izquierda (color) a derecha (ColorB). Sin esto, de abajo hacia arriba.</summary>
    public bool Horizontal = true;
    public Color ColorB = Color.clear;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        Color a = color, b = Degradado ? ColorB : color;
        Color abajoIzq = a, abajoDer = Horizontal ? b : a, arribaDer = b, arribaIzq = Horizontal ? a : b;
        vh.AddVert(new Vector3(r.xMin, r.yMin), abajoIzq, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin + Inclinacion, r.yMax), arribaIzq, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax + Inclinacion, r.yMax), arribaDer, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin), abajoDer, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }
}
