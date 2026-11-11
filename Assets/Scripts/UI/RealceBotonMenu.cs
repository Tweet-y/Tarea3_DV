using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Opción de menú de solo texto: al seleccionarla se tiñe de oro, avanza y enciende su franja.</summary>
public sealed class RealceBotonMenu : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    private TMP_Text texto;
    private Graphic franja;
    private Vector2 posicionBase;
    private float avance = 12f;
    private bool activo;
    private float mezcla;

    public void Configurar(TMP_Text etiqueta, Graphic fondo, float desplazamiento)
    {
        texto = etiqueta;
        franja = fondo;
        avance = desplazamiento;
        posicionBase = etiqueta.rectTransform.anchoredPosition;
        Pintar();
    }

    public void OnSelect(BaseEventData datos) => activo = true;
    public void OnDeselect(BaseEventData datos) => activo = false;

    public void OnPointerEnter(PointerEventData datos)
    {
        // Ratón, teclado y mando comparten una sola opción resaltada.
        var seleccionable = GetComponent<Selectable>();
        if (seleccionable != null && seleccionable.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    private void OnDisable()
    {
        activo = false;
        mezcla = 0f;
        Pintar();
    }

    private void Update()
    {
        // Los menús de pausa y resultado funcionan con el tiempo detenido.
        mezcla = Mathf.MoveTowards(mezcla, activo ? 1f : 0f, Time.unscaledDeltaTime * 9f);
        Pintar();
    }

    private void Pintar()
    {
        if (texto == null) return;
        texto.color = Color.Lerp(EstiloUI.Crema, EstiloUI.Oro, mezcla);
        texto.rectTransform.anchoredPosition = posicionBase + new Vector2(avance * mezcla, 0f);
        if (franja != null) franja.canvasRenderer.SetAlpha(mezcla);
    }
}
