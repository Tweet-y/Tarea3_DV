using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tipografía bitmap de 5 × 7; conserva sus píxeles sin depender de fuentes externas.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TextoPixelGraphic : Graphic
{
    public string Contenido = "";
    public bool ConSombra;
    public int MaximoPixel;
    private static readonly Dictionary<char, string> Letras = new Dictionary<char, string>
    {
        ['A'] = "01110/10001/10001/11111/10001/10001/10001",
        ['C'] = "01111/10000/10000/10000/10000/10000/01111",
        ['E'] = "11111/10000/10000/11110/10000/10000/11111",
        ['I'] = "11111/00100/00100/00100/00100/00100/11111",
        ['J'] = "00111/00010/00010/00010/10010/10010/01100",
        ['L'] = "10000/10000/10000/10000/10000/10000/11111",
        ['M'] = "10001/11011/10101/10101/10001/10001/10001",
        ['N'] = "10001/11001/10101/10011/10001/10001/10001",
        ['O'] = "01110/10001/10001/10001/10001/10001/01110",
        ['P'] = "11110/10001/10001/11110/10000/10000/10000",
        ['R'] = "11110/10001/10001/11110/10100/10010/10001",
        ['S'] = "01111/10000/10000/01110/00001/00001/11110",
        ['T'] = "11111/00100/00100/00100/00100/00100/00100",
        ['U'] = "10001/10001/10001/10001/10001/10001/01110",
        ['Ú'] = "00010/00100/10001/10001/10001/10001/01110",
        ['V'] = "10001/10001/10001/10001/10001/01010/00100",
        ['G'] = "01111/10000/10000/10111/10001/10001/01111"
    };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var lineas = Contenido.ToUpperInvariant().Split('\n');
        int ancho = 1;
        foreach (var linea in lineas) ancho = Mathf.Max(ancho, linea.Length * 6 - 1);
        var rect = rectTransform.rect;
        float pixel = Mathf.Max(1, Mathf.Floor(Mathf.Min(rect.width / (ancho + 2), rect.height / (lineas.Length * 9))));
        if (MaximoPixel > 0) pixel = Mathf.Min(pixel, MaximoPixel);
        float arriba = rect.center.y + (lineas.Length * 9 - 2) * pixel / 2;
        if (ConSombra)
        {
            Dibujar(vh, lineas, pixel, arriba, new Vector2(pixel, -pixel), new Color32(28, 24, 46, 255));
            Dibujar(vh, lineas, pixel, arriba, new Vector2(-pixel * .3f, pixel * .3f), new Color32(247, 151, 173, 255));
        }
        Dibujar(vh, lineas, pixel, arriba, Vector2.zero, color);
    }

    private void Dibujar(VertexHelper vh, string[] lineas, float pixel, float arriba, Vector2 desplazamiento, Color tinta)
    {
        for (int fila = 0; fila < lineas.Length; fila++)
        {
            float izquierda = rectTransform.rect.center.x - (lineas[fila].Length * 6 - 1) * pixel / 2;
            for (int letra = 0; letra < lineas[fila].Length; letra++)
            {
                if (!Letras.TryGetValue(lineas[fila][letra], out var dibujo)) continue;
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 5; x++)
                    {
                        if (dibujo[y * 6 + x] != '1') continue;
                        float px = izquierda + (letra * 6 + x) * pixel + desplazamiento.x;
                        float py = arriba - (fila * 9 + y + 1) * pixel + desplazamiento.y;
                        int primero = vh.currentVertCount;
                        vh.AddVert(new Vector3(px, py), tinta, Vector2.zero);
                        vh.AddVert(new Vector3(px, py + pixel), tinta, Vector2.zero);
                        vh.AddVert(new Vector3(px + pixel, py + pixel), tinta, Vector2.zero);
                        vh.AddVert(new Vector3(px + pixel, py), tinta, Vector2.zero);
                        vh.AddTriangle(primero, primero + 1, primero + 2);
                        vh.AddTriangle(primero, primero + 2, primero + 3);
                    }
            }
        }
    }
}
