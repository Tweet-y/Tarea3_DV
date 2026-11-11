using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Estado visual de los botones de menú: oscuro en reposo, turquesa con flechas al seleccionarse.
/// Pasar el mouse por encima también selecciona, así teclado, mando y mouse se ven igual.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class BotonCurao : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image fondo;
    public TextoPixelGraphic etiqueta;
    public GameObject[] flechas;
    private bool seleccionado, presionado;

    private void OnEnable()
    {
        seleccionado = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        Pintar();
    }

    public void OnSelect(BaseEventData datos) { seleccionado = true; Pintar(); }
    public void OnDeselect(BaseEventData datos) { seleccionado = false; presionado = false; Pintar(); }
    public void OnPointerDown(PointerEventData datos) { presionado = true; Pintar(); }
    public void OnPointerUp(PointerEventData datos) { presionado = false; Pintar(); }

    public void OnPointerEnter(PointerEventData datos)
    {
        var boton = GetComponent<Button>();
        if (EventSystem.current != null && boton != null && boton.IsInteractable())
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    private void Pintar()
    {
        if (fondo != null)
            fondo.color = presionado ? (Color)EstiloCurao.TurquesaOscuro : seleccionado ? (Color)EstiloCurao.Turquesa : (Color)EstiloCurao.Panel;
        if (etiqueta != null) etiqueta.color = seleccionado && !presionado ? EstiloCurao.Noche : EstiloCurao.Texto;
        if (flechas == null) return;
        foreach (var flecha in flechas)
            if (flecha != null) flecha.SetActive(seleccionado);
    }
}
