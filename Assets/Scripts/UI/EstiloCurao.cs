using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Identidad visual de Super Curao Man: paleta de atardecer en la isla (morado, turquesa, rosa)
/// y la tipografía pixel VT323. Menús, HUD y diálogos la comparten para no verse genéricos.
/// </summary>
public static class EstiloCurao
{
    // Paleta tomada del fondo pixel del menú principal.
    public static readonly Color32 Noche = new Color32(22, 14, 40, 255);
    public static readonly Color32 Panel = new Color32(30, 19, 56, 232);
    public static readonly Color32 PanelSuave = new Color32(30, 19, 56, 178);
    public static readonly Color32 Turquesa = new Color32(57, 216, 182, 255);
    public static readonly Color32 TurquesaOscuro = new Color32(22, 96, 92, 255);
    public static readonly Color32 Rosa = new Color32(247, 151, 173, 255);
    public static readonly Color32 Durazno = new Color32(255, 196, 140, 255);
    public static readonly Color32 Ambar = new Color32(255, 200, 87, 255);
    public static readonly Color32 Rojo = new Color32(226, 58, 82, 255);
    public static readonly Color32 Verde = new Color32(122, 201, 92, 255);
    public static readonly Color32 Texto = new Color32(244, 236, 255, 255);
    public static readonly Color32 TextoTenue = new Color32(190, 174, 214, 255);
    public static readonly Color32 Sombra = new Color32(10, 6, 20, 255);

    private static TMP_FontAsset fuente;
    private static bool fuenteIntentada;

    /// <summary>VT323 creada en tiempo de ejecución desde Resources; si falta, la fuente TMP por defecto.</summary>
    public static TMP_FontAsset Fuente
    {
        get
        {
            if (fuente != null) return fuente;
            if (!fuenteIntentada)
            {
                fuenteIntentada = true;
                var ttf = Resources.Load<Font>("Fuentes/VT323-Regular");
                if (ttf != null)
                {
                    fuente = TMP_FontAsset.CreateFontAsset(ttf, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024);
                    if (fuente != null)
                    {
                        fuente.name = "VT323 (dinamica)";
                        // Símbolos que VT323 no trae (flechas, ×) salen de la fuente por defecto.
                        if (TMP_Settings.defaultFontAsset != null)
                            fuente.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
                    }
                }
                if (fuente == null) Debug.LogWarning("Falta Resources/Fuentes/VT323-Regular; se usa la fuente TMP por defecto.");
            }
            return fuente != null ? fuente : TMP_Settings.defaultFontAsset;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        fuente = null;
        fuenteIntentada = false;
    }

    /// <summary>Aplica la tipografía y un contorno oscuro que se lee sobre el escenario 3D.</summary>
    public static void Aplicar(TMP_Text texto, float tamano, Color color, float contorno = .22f)
    {
        texto.font = Fuente;
        texto.fontSize = tamano;
        texto.color = color;
        texto.fontStyle = FontStyles.Normal;
        texto.characterSpacing = 0;
        texto.raycastTarget = false;
        if (contorno > 0f)
        {
            texto.outlineColor = Sombra;
            texto.outlineWidth = contorno;
        }
    }

    /// <summary>Panel con fondo, borde pixel y sombra desplazada. El contenido agregado después queda encima.</summary>
    public static Image Marco(RectTransform rect, Color fondo, Color borde, float grosor = 3f, bool sombra = true)
    {
        var imagen = rect.GetComponent<Image>();
        if (imagen == null) imagen = rect.gameObject.AddComponent<Image>();
        imagen.color = fondo;
        imagen.raycastTarget = false;
        if (sombra)
        {
            var efecto = rect.gameObject.AddComponent<Shadow>();
            efecto.effectColor = new Color(0f, 0f, 0f, .55f);
            efecto.effectDistance = new Vector2(5f, -5f);
        }
        Linea(rect, "BordeArriba", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -grosor), Vector2.zero, borde);
        Color bajo = Color.Lerp(borde, Sombra, .45f);
        Linea(rect, "BordeAbajo", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, grosor), bajo);
        Linea(rect, "BordeIzq", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(grosor, 0), bajo);
        Linea(rect, "BordeDer", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-grosor, 0), Vector2.zero, bajo);
        return imagen;
    }

    private static void Linea(RectTransform padre, string nombre, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax, Color color)
    {
        var rect = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offMin;
        rect.offsetMax = offMax;
        var imagen = rect.gameObject.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
    }

    /// <summary>Color de la etiqueta de cada personaje en los diálogos.</summary>
    public static Color ColorPersonaje(string nombre)
    {
        switch ((nombre ?? "").ToUpperInvariant())
        {
            case "CJ": return Ambar;
            case "NEMESIS": return Rojo;
            default: return Turquesa;
        }
    }
}
