using UnityEngine;
using UnityEngine.UI;

/// <summary>Silueta blanca con contorno negro del arma en la mano: rifle, escopeta o bate. Sin sprites.</summary>
public sealed class IconoArmaGraphic : MaskableGraphic
{
    public enum Forma { Rifle, Escopeta, Bate }

    private static readonly Vector2[][] Rifle =
    {
        new[] { new Vector2(.03f,.58f), new Vector2(.22f,.58f), new Vector2(.25f,.46f), new Vector2(.03f,.32f) },
        new[] { new Vector2(.20f,.62f), new Vector2(.73f,.62f), new Vector2(.73f,.43f), new Vector2(.20f,.43f) },
        new[] { new Vector2(.70f,.56f), new Vector2(.98f,.56f), new Vector2(.98f,.48f), new Vector2(.70f,.48f) },
        new[] { new Vector2(.90f,.66f), new Vector2(.94f,.66f), new Vector2(.94f,.54f), new Vector2(.90f,.54f) },
        new[] { new Vector2(.31f,.45f), new Vector2(.41f,.43f), new Vector2(.37f,.23f), new Vector2(.28f,.25f) },
        new[] { new Vector2(.48f,.44f), new Vector2(.60f,.44f), new Vector2(.66f,.17f), new Vector2(.53f,.15f) },
        new[] { new Vector2(.27f,.68f), new Vector2(.54f,.68f), new Vector2(.54f,.61f), new Vector2(.27f,.61f) }
    };

    private static readonly Vector2[][] Escopeta =
    {
        // Culata de madera que baja hacia atrás.
        new[] { new Vector2(.02f,.56f), new Vector2(.27f,.60f), new Vector2(.29f,.48f), new Vector2(.02f,.36f) },
        // Cajón de mecanismos.
        new[] { new Vector2(.26f,.63f), new Vector2(.46f,.63f), new Vector2(.46f,.47f), new Vector2(.27f,.47f) },
        // Cañón largo.
        new[] { new Vector2(.44f,.63f), new Vector2(.99f,.63f), new Vector2(.99f,.57f), new Vector2(.44f,.57f) },
        // Tubo del cargador bajo el cañón.
        new[] { new Vector2(.44f,.56f), new Vector2(.90f,.56f), new Vector2(.90f,.51f), new Vector2(.44f,.51f) },
        // Corredera estriada.
        new[] { new Vector2(.56f,.58f), new Vector2(.78f,.58f), new Vector2(.78f,.46f), new Vector2(.56f,.46f) },
        // Guardamonte.
        new[] { new Vector2(.30f,.48f), new Vector2(.40f,.48f), new Vector2(.38f,.38f), new Vector2(.31f,.38f) },
        // Mira delantera.
        new[] { new Vector2(.95f,.67f), new Vector2(.98f,.67f), new Vector2(.98f,.62f), new Vector2(.95f,.62f) }
    };

    private static Vector2[][] bate;
    private static Vector2[][] Bate
    {
        get
        {
            if (bate != null) return bate;
            // Bate en diagonal: pomo abajo a la izquierda, barril grueso arriba a la derecha.
            Vector2 a = new Vector2(.14f, .14f), b = new Vector2(.90f, .86f);
            bate = new[]
            {
                Segmento(a, b, .015f, .04f, .095f),   // mango
                Segmento(a, b, .38f, .095f, .14f),    // cono
                Segmento(a, b, .62f, .14f, .145f),    // barril
                Segmento(a, b, -.025f, .09f, .09f, .04f), // pomo
            };
            return bate;
        }
    }

    [SerializeField] private Forma forma;
    public Forma Actual
    {
        get => forma;
        set
        {
            if (forma == value) return;
            forma = value;
            SetVerticesDirty();
        }
    }

    /// <summary>Tramo del bate entre las fracciones t y t+largo del eje a-b, con ancho inicial y final.</summary>
    private static Vector2[] Segmento(Vector2 a, Vector2 b, float t, float anchoA, float anchoB, float largo = -1f)
    {
        Vector2 eje = b - a;
        Vector2 n = new Vector2(-eje.y, eje.x).normalized;
        float fin = largo > 0f ? t + largo : (t < .3f ? .38f : t < .5f ? .62f : 1f);
        Vector2 p0 = a + eje * t, p1 = a + eje * fin;
        return new[] { p0 + n * anchoA * .5f, p1 + n * anchoB * .5f, p1 - n * anchoB * .5f, p0 - n * anchoA * .5f };
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var piezas = forma == Forma.Escopeta ? Escopeta : forma == Forma.Bate ? Bate : Rifle;
        foreach (var pieza in piezas) Dibujar(vh, pieza, Color.black, .02f);
        foreach (var pieza in piezas) Dibujar(vh, pieza, color, 0f);
    }

    /// <summary>Cuadrilátero convexo; con borde, cada lado se desplaza hacia afuera para el contorno.</summary>
    private void Dibujar(VertexHelper vh, Vector2[] puntos, Color colorPieza, float borde)
    {
        Rect rect = rectTransform.rect;
        int n = puntos.Length;
        // Sentido del polígono, para saber hacia dónde queda "afuera".
        float area = 0f;
        for (int i = 0; i < n; i++) area += puntos[i].x * puntos[(i + 1) % n].y - puntos[(i + 1) % n].x * puntos[i].y;
        float signo = area >= 0f ? 1f : -1f;
        int inicio = vh.currentVertCount;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = puntos[i];
            if (borde > 0f)
            {
                Vector2 previo = puntos[(i - 1 + n) % n], siguiente = puntos[(i + 1) % n];
                Vector2 e1 = (p - previo).normalized, e2 = (siguiente - p).normalized;
                Vector2 n1 = new Vector2(e1.y, -e1.x) * signo, n2 = new Vector2(e2.y, -e2.x) * signo;
                Vector2 media = n1 + n2;
                float d = 1f + Vector2.Dot(n1, n2);
                if (d > .05f) p += media / d * borde;
            }
            vh.AddVert(rect.min + Vector2.Scale(p, rect.size), colorPieza, Vector2.zero);
        }
        for (int i = 1; i < n - 1; i++) vh.AddTriangle(inicio, inicio + i, inicio + i + 1);
    }
}
