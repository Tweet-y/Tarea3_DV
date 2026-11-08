using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Caja de diálogo con máquina de escribir. Espacio o A adelantan.</summary>
public sealed class ControladorDialogo : MonoBehaviour
{
    public static bool Saltar;
    public Image Negro { get; private set; }
    CanvasGroup panel;
    TMP_Text texto;
    TMP_Text indicacion;

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
        var caja = Rect("Dialogo", objeto.transform, new Vector2(.08f, .06f), new Vector2(.92f, .32f));
        var fondo = caja.gameObject.AddComponent<Image>();
        fondo.color = new Color32(17, 21, 20, 240);
        fondo.raycastTarget = false;
        dialogo.panel = caja.gameObject.AddComponent<CanvasGroup>();
        dialogo.panel.alpha = 0f;
        var nombre = Texto("Nombre", caja, new Vector2(.025f, .73f), new Vector2(.975f, .98f), 28);
        nombre.text = "CJ";
        nombre.color = new Color32(232, 180, 68, 255);
        dialogo.texto = Texto("Texto", caja, new Vector2(.025f, .20f), new Vector2(.975f, .73f), 24);
        dialogo.texto.enableAutoSizing = true;
        dialogo.texto.fontSizeMin = 20;
        dialogo.texto.fontSizeMax = 24;
        dialogo.texto.textWrappingMode = TextWrappingModes.Normal;
        dialogo.indicacion = Texto("Continuar", caja, new Vector2(.025f, .025f), new Vector2(.975f, .19f), 16);
        dialogo.indicacion.text = "ESPACIO / A · Mostrar diálogo";
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
        panel.alpha = 1f;
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
        if (panel != null) panel.alpha = 0f;
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

    static TMP_Text Texto(string nombre, Transform padre, Vector2 minimo, Vector2 maximo, int tamano)
    {
        var etiqueta = Rect(nombre, padre, minimo, maximo).gameObject.AddComponent<TextMeshProUGUI>();
        etiqueta.fontSize = tamano;
        etiqueta.color = Color.white;
        etiqueta.alignment = TextAlignmentOptions.Left;
        etiqueta.raycastTarget = false;
        return etiqueta;
    }
}
