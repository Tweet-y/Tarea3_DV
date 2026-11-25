using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>Zona de interaccion adaptada de DemoControlInteraccion del profesor: detecta al jugador y avisa por eventos al pulsar la tecla.</summary>
[DisallowMultipleComponent]
public class ControladorInteraccion : MonoBehaviour
{
    public InputActionReference inputInteraccion;
    public Eventos eventos;

    InputAction accion;
    bool propia;
    bool dentro;

    void Awake()
    {
        accion = inputInteraccion != null ? inputInteraccion.action : null;
        if (accion != null) return;
        // ponytail: sin accion en el Inspector se arma aqui, igual que InventarioJugador.
        accion = new InputAction("Interaccion", InputActionType.Button);
        accion.AddBinding("<Keyboard>/e");
        accion.AddBinding("<Gamepad>/buttonWest");
        propia = true;
    }

    void OnEnable()
    {
        accion.started += AlPulsar;
        accion.Enable();
    }

    void OnDisable()
    {
        accion.started -= AlPulsar;
        if (propia) accion.Disable();
    }

    void OnDestroy()
    {
        if (propia) accion.Dispose();
    }

    void AlPulsar(InputAction.CallbackContext contexto) => Interactuar();

    /// <summary>Lanza alInteractuar solo si el jugador esta dentro y la partida corre (la tecla llega a todas las zonas).</summary>
    public void Interactuar()
    {
        if (!dentro) return;
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida == null || partida.EstaPausada || partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso) return;
        if (IntroduccionIsla.Activa) return;
        eventos.alInteractuar?.Invoke();
    }

    public void EntrarDeteccion()
    {
        dentro = true;
        eventos.alEntrarDeteccion?.Invoke();
    }

    public void SalirDeteccion()
    {
        dentro = false;
        eventos.alSalirDeteccion?.Invoke();
    }

    void OnTriggerEnter(Collider other)
    {
        if (EsJugador(other)) EntrarDeteccion();
    }

    void OnTriggerExit(Collider other)
    {
        if (EsJugador(other)) SalirDeteccion();
    }

    static bool EsJugador(Collider other) => other != null && other.transform.root.CompareTag("Player");

    [System.Serializable]
    public struct Eventos
    {
        public UnityEvent alEntrarDeteccion;
        public UnityEvent alSalirDeteccion;
        public UnityEvent alInteractuar;
    }
}
