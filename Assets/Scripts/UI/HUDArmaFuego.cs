using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Municion, interaccion, recarga y mira en el mismo espacio de pantalla que el HUD existente.</summary>
public sealed class HUDArmaFuego : MonoBehaviour
{
    private ControladorArmaFuego jugador;
    private Canvas canvas;
    private TMP_Text controles, interaccion, mira;

    public void Inicializar(ControladorArmaFuego fuente)
    {
        jugador = fuente;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;

        controles = Texto("Controles", transform, new Vector2(1, 0), new Vector2(-164, 58), new Vector2(280, 60), 15);
        controles.outlineColor = Color.black;
        controles.outlineWidth = .2f;
        controles.textWrappingMode = TextWrappingModes.Normal;
        interaccion = Texto("Interaccion", transform, new Vector2(.5f, .5f), new Vector2(0, -110), new Vector2(600, 60), 20);
        mira = Texto("Mira", transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(80, 80), 20);
        mira.text = "+";
    }

    private void Update()
    {
        if (jugador == null) { Destroy(gameObject); return; }
        canvas.enabled = jugador.PuedeActuar;
        if (!canvas.enabled) return;
        bool mando = jugador.UsaGamepad;
        var municion = jugador.Municion;
        bool equipada = jugador.EnMano;
        bool ayuda = ControladorGameplayHUD.IndicacionesVisibles;
        controles.gameObject.SetActive(ayuda);
        mira.gameObject.SetActive(equipada && !municion.Recargando);
        mira.color = Time.time < jugador.ImpactoHasta ? new Color(1f, .3f, .22f) : Color.white;
        mira.text = Time.time < jugador.ImpactoHasta ? "×" : "+";
        mira.fontSize = jugador.Apuntando ? 16 : 20 + jugador.Retroceso * 3f;
        if (equipada)
        {
            controles.text = mando ? "RT: disparar · LB: apuntar\nY: recargar · ↓: soltar" :
                "Clic izq.: disparar · Clic der.: apuntar\nR: recargar · G: soltar";
            if (municion.Cargador == 0 && !municion.Recargando)
                controles.text = municion.Reserva > 0 ? (mando ? "Y: RECARGAR" : "R: RECARGAR") : "SIN MUNICION · Busca cargadores";
        }
        else
        {
            controles.text = mando ? "Acercate a un rifle y pulsa X" : "Acercate a un rifle y pulsa E";
        }
        var item = jugador.Cercana;
        interaccion.text = item == null ? "" : (mando ? "[X] " : "[E] ") +
            (item.SoloMunicion ? $"Municion de rifle +{item.Balas}" : "Recoger rifle de asalto");
    }

    private static RectTransform Rect(string nombre, Transform padre, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        var rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = rect.anchorMax = ancla;
        rect.anchoredPosition = posicion;
        rect.sizeDelta = tamano;
        return rect;
    }

    private static TMP_Text Texto(string nombre, Transform padre, Vector2 ancla, Vector2 posicion, Vector2 tamano, float puntos)
    {
        var rect = Rect(nombre, padre, ancla, posicion, tamano);
        var texto = rect.gameObject.AddComponent<TextMeshProUGUI>();
        texto.fontSize = puntos;
        texto.color = Color.white;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.raycastTarget = false;
        return texto;
    }
}
