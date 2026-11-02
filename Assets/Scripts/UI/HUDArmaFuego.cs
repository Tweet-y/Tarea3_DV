using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Municion, interaccion, recarga y mira en el mismo espacio de pantalla que el HUD existente.</summary>
public sealed class HUDArmaFuego : MonoBehaviour
{
    private ControladorArmaFuego jugador;
    private Canvas canvas;
    private TMP_Text estado, controles, interaccion, mira;
    private Image progreso;

    public void Inicializar(ControladorArmaFuego fuente)
    {
        jugador = fuente;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        var panel = Rect("PanelMunicion", transform, new Vector2(1, 0), new Vector2(-28, 240), new Vector2(370, 140));
        panel.pivot = new Vector2(1, 0);
        panel.gameObject.AddComponent<Image>().color = new Color32(17, 21, 20, 225);
        panel.GetComponent<Image>().raycastTarget = false;
        estado = Texto("Municion", panel, new Vector2(.5f, 1), new Vector2(0, -32), new Vector2(345, 50), 27);
        controles = Texto("Controles", panel, new Vector2(.5f, 0), new Vector2(0, 46), new Vector2(345, 52), 17);
        controles.textWrappingMode = TextWrappingModes.Normal;
        var barra = Rect("Recarga", panel, new Vector2(0, 0), new Vector2(12, 10), new Vector2(346, 5));
        barra.pivot = Vector2.zero;
        progreso = barra.gameObject.AddComponent<Image>();
        progreso.color = new Color32(232, 180, 68, 255);
        progreso.raycastTarget = false;
        interaccion = Texto("Interaccion", transform, new Vector2(.5f, .5f), new Vector2(0, -110), new Vector2(720, 60), 24);
        mira = Texto("Mira", transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(80, 80), 28);
        mira.text = "+";
    }

    private void Update()
    {
        if (jugador == null) { Destroy(gameObject); return; }
        canvas.enabled = jugador.PuedeActuar;
        if (!canvas.enabled) return;
        bool mando = jugador.UsaGamepad;
        var municion = jugador.Municion;
        bool equipada = jugador.Equipada;
        mira.gameObject.SetActive(equipada && !municion.Recargando);
        mira.color = Time.time < jugador.ImpactoHasta ? new Color(1f, .3f, .22f) : Color.white;
        mira.text = Time.time < jugador.ImpactoHasta ? "×" : "+";
        mira.fontSize = jugador.Apuntando ? 22 : 28 + jugador.Retroceso * 5f;
        if (equipada)
        {
            estado.text = municion.Recargando ? $"RECARGANDO  {jugador.RecargaRestante:0.0}s" :
                $"RIFLE  <color=#E8B444>{municion.Cargador:00}</color> / {municion.Reserva:000}";
            controles.text = mando ? "RT: disparar · LB: apuntar\nY: recargar · ↓: soltar" :
                "Clic izq.: disparar · Clic der.: apuntar\nR: recargar · G: soltar";
            if (municion.Cargador == 0 && !municion.Recargando)
                controles.text = municion.Reserva > 0 ? (mando ? "Y: RECARGAR" : "R: RECARGAR") : "SIN MUNICION · Busca cargadores";
        }
        else
        {
            estado.text = "SIN ARMA";
            controles.text = mando ? "Acercate a un rifle y pulsa X" : "Acercate a un rifle y pulsa E";
        }
        progreso.gameObject.SetActive(equipada && municion.Recargando);
        if (equipada) progreso.rectTransform.localScale = new Vector3(municion.Recargando ?
            Mathf.Clamp01(1f - jugador.RecargaRestante / jugador.Definicion.segundosRecarga) : 0, 1, 1);
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
