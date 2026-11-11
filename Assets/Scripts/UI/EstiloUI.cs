using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Identidad visual común de toda la interfaz: paleta de atardecer, tipografías y texto con contorno
/// grueso y sombra dura. HUD, menús, diálogos y la pelea del jefe piden aquí sus fuentes y colores.
/// </summary>
public static class EstiloUI
{
    public enum Fuente { Titulo, Hud, Cuerpo }

    public static readonly Color Oro = new Color32(255, 196, 64, 255);
    public static readonly Color Rosa = new Color32(247, 151, 173, 255);
    public static readonly Color Magenta = new Color32(214, 64, 130, 255);
    public static readonly Color Noche = new Color32(22, 14, 38, 255);
    public static readonly Color Verde = new Color32(98, 190, 96, 255);
    public static readonly Color Rojo = new Color32(206, 38, 48, 255);
    public static readonly Color Celeste = new Color32(110, 200, 240, 255);
    public static readonly Color Crema = new Color32(244, 236, 220, 255);
    public static readonly Color Gris = new Color32(190, 180, 200, 255);
    public const string HexOro = "#FFC440";
    public const string HexRosa = "#F797AD";
    public const string HexVerde = "#62BE60";

    private static readonly TMP_FontAsset[] fuentes = new TMP_FontAsset[3];
    private static readonly Dictionary<int, Material> materiales = new Dictionary<int, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        // Con la recarga de dominio desactivada, los estáticos sobreviven entre sesiones de Play.
        foreach (var material in materiales.Values) Liberar(material);
        materiales.Clear();
        for (int i = 0; i < fuentes.Length; i++)
        {
            if (fuentes[i] != null && fuentes[i] != TMP_Settings.defaultFontAsset) Liberar(fuentes[i]);
            fuentes[i] = null;
        }
    }

    private static void Liberar(Object objeto)
    {
        if (objeto == null) return;
        if (Application.isPlaying) Object.Destroy(objeto);
        else Object.DestroyImmediate(objeto);
    }

    /// <summary>Fuente TMP del rol pedido. Si falta el .ttf, cae a la fuente por defecto de TMP.</summary>
    public static TMP_FontAsset Obtener(Fuente fuente)
    {
        int i = (int)fuente;
        if (fuentes[i] != null) return fuentes[i];
        var porDefecto = TMP_Settings.defaultFontAsset;
        if (fuente == Fuente.Cuerpo) return fuentes[i] = porDefecto;
        var ttf = Resources.Load<Font>(fuente == Fuente.Titulo ? "Fuentes/Bangers-Regular" : "Fuentes/Anton-Regular");
        TMP_FontAsset creada = null;
        if (ttf != null)
        {
            try { creada = TMP_FontAsset.CreateFontAsset(ttf, 96, 12, GlyphRenderMode.SDFAA, 2048, 2048); }
            catch (System.Exception error) { Debug.LogWarning("EstiloUI: no se pudo crear la fuente " + ttf.name + ": " + error.Message); }
        }
        if (creada == null) return fuentes[i] = porDefecto;
        creada.name = "EstiloUI " + fuente;
        creada.hideFlags = HideFlags.HideAndDontSave;
        if (creada.material != null) creada.material.hideFlags = HideFlags.HideAndDontSave;
        if (creada.atlasTexture != null) creada.atlasTexture.hideFlags = HideFlags.HideAndDontSave;
        // Flechas, puntos medios y otros símbolos que la fuente decorativa no trae.
        if (porDefecto != null) creada.fallbackFontAssetTable = new List<TMP_FontAsset> { porDefecto };
        return fuentes[i] = creada;
    }

    /// <summary>Material compartido por rol, grosor de contorno y sombra: no se crea uno por texto.</summary>
    public static Material MaterialTexto(Fuente fuente, float contorno, bool sombra)
    {
        int clave = (int)fuente | (Mathf.RoundToInt(contorno * 100f) << 4) | (sombra ? 1 << 16 : 0);
        if (materiales.TryGetValue(clave, out var listo) && listo != null) return listo;
        var asset = Obtener(fuente);
        if (asset == null || asset.material == null) return null;
        var material = new Material(asset.material) { hideFlags = HideFlags.HideAndDontSave };
        material.name = $"EstiloUI {fuente} c{contorno:0.00}{(sombra ? " sombra" : "")}";
        if (contorno > 0f)
        {
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", contorno);
            material.SetColor("_OutlineColor", Color.black);
            // El contorno se reparte hacia dentro y hacia fuera: dilatar la cara conserva el grosor de la letra.
            material.SetFloat("_FaceDilate", contorno * .5f);
        }
        if (sombra)
        {
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, .9f));
            material.SetFloat("_UnderlayOffsetX", .7f);
            material.SetFloat("_UnderlayOffsetY", -.7f);
            material.SetFloat("_UnderlayDilate", Mathf.Min(1f, contorno));
            material.SetFloat("_UnderlaySoftness", .04f);
        }
        ShaderUtilities.UpdateShaderRatios(material);
        materiales[clave] = material;
        return material;
    }

    /// <summary>Aplica fuente, contorno negro y sombra dura a un texto ya creado.</summary>
    public static void Aplicar(TMP_Text texto, Fuente fuente, float contorno = .22f, bool sombra = true)
    {
        if (texto == null) return;
        var asset = Obtener(fuente);
        if (asset == null) return;
        texto.font = asset;
        var material = MaterialTexto(fuente, contorno, sombra);
        if (material != null) texto.fontSharedMaterial = material;
        // El contorno y la sombra necesitan margen extra en la malla para no cortarse.
        texto.extraPadding = true;
    }
}
