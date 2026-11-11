using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Diálogo con franjas de cine y máquina de escribir. Espacio o A adelantan.</summary>
public sealed class ControladorDialogo : MonoBehaviour
{
    public static bool Saltar;
    public Image Negro { get; private set; }
    CanvasGroup panel;
    TMP_Text texto;
    TMP_Text indicacion;
    float alfaObjetivo;

    public static ControladorDialogo Crear(bool conFundido)
    {
        var objeto = new GameObject("Dialogo", typeof(RectTransform));
        var dialogo = objeto.AddComponent<ControladorDialogo>();
        var canvas = objeto.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var escala = objeto.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1280, 720);
        escala.matchWidthOrHeight = .5f;
        if (conFundido)
        {
            dialogo.Negro = Rect("Entrada", objeto.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            dialogo.Negro.color = Color.black;
            dialogo.Negro.raycastTarget = false;
        }
        // Franjas de cine: quien habla y su línea van sobre la franja inferior, sin caja flotante.
        var caja = Rect("Dialogo", objeto.transform, Vector2.zero, Vector2.one);
        dialogo.panel = caja.gameObject.AddComponent<CanvasGroup>();
        dialogo.panel.alpha = 0f;
        dialogo.panel.interactable = dialogo.panel.blocksRaycasts = false;
        Franja("FranjaSuperior", caja, new Vector2(0, .915f), Vector2.one, 0f);
        Franja("FranjaInferior", caja, Vector2.zero, new Vector2(1, .26f), 1f);
        var nombre = Texto("Nombre", caja, new Vector2(.1f, .185f), new Vector2(.9f, .245f), 28, EstiloUI.Fuente.Hud);
        nombre.text = "CJ";
        nombre.color = EstiloUI.Oro;
        nombre.characterSpacing = 5;
        dialogo.texto = Texto("Texto", caja, new Vector2(.12f, .06f), new Vector2(.88f, .185f), 25, EstiloUI.Fuente.Cuerpo);
        dialogo.texto.fontStyle = FontStyles.Bold;
        dialogo.texto.enableAutoSizing = true;
        dialogo.texto.fontSizeMin = 19;
        dialogo.texto.fontSizeMax = 25;
        dialogo.texto.textWrappingMode = TextWrappingModes.Normal;
        dialogo.indicacion = Texto("Continuar", caja, new Vector2(.1f, .012f), new Vector2(.975f, .055f), 14, EstiloUI.Fuente.Hud);
        dialogo.indicacion.text = "ESPACIO / A · Mostrar diálogo";
        dialogo.indicacion.color = EstiloUI.Gris;
        dialogo.indicacion.characterSpacing = 3;
        dialogo.indicacion.alignment = TextAlignmentOptions.Right;
        return dialogo;
    }

    public void FijarNombre(string nombre)
    {
        var etiqueta = panel.transform.Find("Nombre").GetComponent<TMP_Text>();
        etiqueta.text = nombre;
    }

    public IEnumerator Revelar(string linea, float segundosLectura)
    {
        alfaObjetivo = 1f;
        if (Saltar) panel.alpha = 1f;
        texto.text = linea;
        texto.maxVisibleCharacters = 0;
        texto.ForceMeshUpdate();
        int caracteres = texto.textInfo.characterCount;
        if (Saltar)
        {
            texto.maxVisibleCharacters = caracteres;
            yield break;
        }
        float reveladas = 0f;
        while (reveladas < caracteres)
        {
            if (Avanzar()) reveladas = caracteres;
            else reveladas += Time.deltaTime * 38f;
            texto.maxVisibleCharacters = Mathf.Min(caracteres, Mathf.FloorToInt(reveladas));
            yield return null;
        }
        indicacion.text = "ESPACIO / A · Continuar";
        for (float t = 0f; t < segundosLectura; t += Time.deltaTime)
        {
            if (t > .25f && Avanzar()) break;
            yield return null;
        }
    }

    public void Ocultar()
    {
        alfaObjetivo = 0f;
        if (panel != null && Saltar) panel.alpha = 0f;
    }

    public void Cerrar()
    {
        if (this != null) Destroy(gameObject);
    }

    public static bool Avanzar()
    {
        if (Saltar) return true;
        var partida = ControladorPartidaBotellas.Instancia;
        if (Time.timeScale <= 0f || (partida != null && partida.EstaPausada)) return false;
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    void Update()
    {
        // Las franjas entran y salen con un fundido corto en vez de aparecer de golpe.
        if (panel != null) panel.alpha = Mathf.MoveTowards(panel.alpha, alfaObjetivo, Time.unscaledDeltaTime * 5f);
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        var partida = ControladorPartidaBotellas.Instancia;
        canvas.enabled = partida == null || !partida.EstaPausada;
    }

    static RectTransform Rect(string nombre, Transform padre, Vector2 minimo, Vector2 maximo)
    {
        var rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = minimo;
        rect.anchorMax = maximo;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static void Franja(string nombre, Transform padre, Vector2 minimo, Vector2 maximo, float borde)
    {
        var franja = Rect(nombre, padre, minimo, maximo);
        var fondo = franja.gameObject.AddComponent<Image>();
        fondo.color = new Color(.03f, .02f, .06f, .93f);
        fondo.raycastTarget = false;
        // Filo dorado en el lado que mira a la imagen.
        var filo = Rect("Filo", franja, new Vector2(0, borde), new Vector2(1, borde));
        filo.sizeDelta = new Vector2(0, 3);
        var color = filo.gameObject.AddComponent<Image>();
        color.color = EstiloUI.Oro;
        color.raycastTarget = false;
    }

    static TMP_Text Texto(string nombre, Transform padre, Vector2 minimo, Vector2 maximo, int tamano, EstiloUI.Fuente fuente)
    {
        var etiqueta = Rect(nombre, padre, minimo, maximo).gameObject.AddComponent<TextMeshProUGUI>();
        EstiloUI.Aplicar(etiqueta, fuente, .22f);
        etiqueta.fontSize = tamano;
        etiqueta.color = Color.white;
        etiqueta.alignment = TextAlignmentOptions.Center;
        etiqueta.raycastTarget = false;
        return etiqueta;
    }
}
