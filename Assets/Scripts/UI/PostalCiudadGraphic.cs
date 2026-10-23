using UnityEngine;
using UnityEngine.UI;

/// <summary>Postal vectorial de la ciudad: sin texturas ni descargas adicionales.</summary>
public sealed class PostalCiudadGraphic : Graphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var negro = new Color32(23, 32, 27, 255);
        Quad(vh, 0, 0, 1, 1, new Color32(169, 189, 120, 255));
        Circulo(vh, .69f, .72f, .20f, new Color32(224, 214, 161, 255));
        for (int i = 0; i < 14; i++)
        {
            float x = i / 13f;
            float altura = .22f + .15f * Mathf.Abs(Mathf.Sin(i * 2.3f));
            Quad(vh, x, .32f, .085f, altura, new Color32(113, 138, 88, 255));
            Quad(vh, x + .03f, .32f + altura, .025f, .04f, new Color32(113, 138, 88, 255));
        }
        for (int i = 0; i < 7; i++)
        {
            float x = i * .17f - .05f;
            float h = .17f + .11f * Mathf.Abs(Mathf.Cos(i * 1.7f));
            Quad(vh, x, .22f, .13f, h, negro);
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                    Quad(vh, x + .02f + col * .034f, .24f + row * .04f, .012f, .018f, new Color32(153, 168, 105, 255));
        }
        Poligono(vh, negro, new Vector2(0, .04f), new Vector2(1, .04f), new Vector2(.69f, .30f), new Vector2(.47f, .30f));
        for (int i = 0; i < 4; i++)
            Quad(vh, .575f - i * .012f, .075f + i * .048f, .01f, .025f, new Color32(215, 209, 148, 255));
        Palma(vh, .17f, .36f, .50f, negro);
        Palma(vh, .91f, .38f, .39f, negro);
        // Botella protagonista, con sombra, etiqueta y contornos gruesos.
        Botella(vh, .46f, .23f, .31f, .44f, negro);
        Botella(vh, .477f, .242f, .276f, .411f, new Color32(67, 91, 51, 255));
        Quad(vh, .505f, .31f, .22f, .135f, new Color32(224, 214, 161, 255));
        Quad(vh, .522f, .323f, .186f, .108f, negro);
        Quad(vh, .606f, .343f, .018f, .064f, new Color32(224, 214, 161, 255));
        Quad(vh, .576f, .366f, .078f, .018f, new Color32(224, 214, 161, 255));
        Quad(vh, .555f, .67f, .12f, .021f, negro);
        Quad(vh, .58f, .52f, .012f, .12f, new Color32(116, 147, 73, 255));
        Quad(vh, 0, .97f, 1, .008f, negro);
        Quad(vh, 0, .025f, 1, .008f, negro);
    }

    private void Botella(VertexHelper vh, float x, float y, float w, float h, Color c)
    {
        Poligono(vh, c, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h * .67f),
            new Vector2(x + w * .66f, y + h * .80f), new Vector2(x + w * .66f, y + h),
            new Vector2(x + w * .34f, y + h), new Vector2(x + w * .34f, y + h * .80f), new Vector2(x, y + h * .67f));
    }

    private void Palma(VertexHelper vh, float x, float y, float altura, Color c)
    {
        var copa = new Vector2(x + .04f, y + altura);
        Poligono(vh, c, new Vector2(x, y), new Vector2(x + .027f, y), copa + new Vector2(.013f, 0), copa);
        for (int i = 0; i < 7; i++)
        {
            float a = .12f + i * .48f;
            var fin = copa + new Vector2(Mathf.Cos(a) * .20f, Mathf.Sin(a) * .10f - .06f);
            Poligono(vh, c, copa, Vector2.Lerp(copa, fin, .55f) + Vector2.up * .033f, fin, Vector2.Lerp(copa, fin, .55f) - Vector2.up * .016f);
        }
    }

    private void Circulo(VertexHelper vh, float x, float y, float radio, Color c)
    {
        var puntos = new Vector2[48];
        for (int i = 0; i < puntos.Length; i++)
        {
            float a = i * Mathf.PI * 2 / puntos.Length;
            puntos[i] = new Vector2(x + Mathf.Cos(a) * radio, y + Mathf.Sin(a) * radio * rectTransform.rect.width / rectTransform.rect.height);
        }
        Poligono(vh, c, puntos);
    }

    private void Quad(VertexHelper vh, float x, float y, float w, float h, Color c) =>
        Poligono(vh, c, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h));

    private void Poligono(VertexHelper vh, Color c, params Vector2[] puntos)
    {
        int inicio = vh.currentVertCount;
        var rect = rectTransform.rect;
        Vector2 centro = Vector2.zero;
        foreach (var p in puntos) centro += p;
        centro /= puntos.Length;
        Vertice(vh, centro, rect, c);
        foreach (var p in puntos) Vertice(vh, p, rect, c);
        for (int i = 0; i < puntos.Length; i++) vh.AddTriangle(inicio, inicio + i + 1, inicio + (i + 1) % puntos.Length + 1);
    }

    private static void Vertice(VertexHelper vh, Vector2 p, Rect rect, Color c)
    {
        var v = UIVertex.simpleVert;
        v.position = new Vector2(rect.xMin + p.x * rect.width, rect.yMin + p.y * rect.height);
        v.color = c;
        vh.AddVert(v);
    }
}
