using UnityEngine;
using UnityEngine.UI;

/// <summary>Silueta de rifle blanca con contorno negro para la esquina del HUD.</summary>
public sealed class IconoRifleGraphic : Graphic
{
    private static readonly Vector2[][] Piezas =
    {
        new[] { new Vector2(.03f,.58f), new Vector2(.22f,.58f), new Vector2(.25f,.46f), new Vector2(.03f,.32f) },
        new[] { new Vector2(.20f,.62f), new Vector2(.73f,.62f), new Vector2(.73f,.43f), new Vector2(.20f,.43f) },
        new[] { new Vector2(.70f,.56f), new Vector2(.98f,.56f), new Vector2(.98f,.48f), new Vector2(.70f,.48f) },
        new[] { new Vector2(.90f,.66f), new Vector2(.94f,.66f), new Vector2(.94f,.54f), new Vector2(.90f,.54f) },
        new[] { new Vector2(.31f,.45f), new Vector2(.41f,.43f), new Vector2(.37f,.23f), new Vector2(.28f,.25f) },
        new[] { new Vector2(.48f,.44f), new Vector2(.60f,.44f), new Vector2(.66f,.17f), new Vector2(.53f,.15f) },
        new[] { new Vector2(.27f,.68f), new Vector2(.54f,.68f), new Vector2(.54f,.61f), new Vector2(.27f,.61f) }
    };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (var pieza in Piezas) Dibujar(vh, pieza, Color.black, .018f);
        foreach (var pieza in Piezas) Dibujar(vh, pieza, Color.white, 0f);
    }

    private void Dibujar(VertexHelper vh, Vector2[] puntos, Color colorPieza, float borde)
    {
        Vector2 centro = (puntos[0] + puntos[1] + puntos[2] + puntos[3]) * .25f;
        Rect rect = rectTransform.rect;
        int inicio = vh.currentVertCount;
        foreach (var punto in puntos)
        {
            Vector2 p = punto + new Vector2(Mathf.Sign(punto.x - centro.x), Mathf.Sign(punto.y - centro.y)) * borde;
            vh.AddVert(rect.min + Vector2.Scale(p, rect.size), colorPieza, Vector2.zero);
        }
        vh.AddTriangle(inicio, inicio + 1, inicio + 2);
        vh.AddTriangle(inicio, inicio + 2, inicio + 3);
    }
}
