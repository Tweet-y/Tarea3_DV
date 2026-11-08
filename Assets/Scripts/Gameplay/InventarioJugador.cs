using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Bate o rifle, botiquines y bebida energética.</summary>
[DisallowMultipleComponent]
public class InventarioJugador : MonoBehaviour
{
    public enum Arma { Bate, Rifle }
    public static InventarioJugador Instancia { get; private set; }
    public Arma armaActiva = Arma.Bate;
    public int botiquines;
    public int buffs;
    public int municionPendiente;
    public float multiplicadorDanio = 1f;
    public float MultiplicadorDanio => multiplicadorDanio;
    public bool BuffActivo { get; private set; }
    public GameObject bate;

    InputAction botiquin, bebida, elegirBate, elegirRifle;
    StarterAssets.ThirdPersonController locomocion;
    float velocidadBase, sprintBase;
    bool velocidadesGuardadas;

    void Awake()
    {
        Instancia = this;
        locomocion = GetComponent<StarterAssets.ThirdPersonController>();
        botiquin = Boton("Botiquin", "<Keyboard>/q");
        bebida = Boton("Buff", "<Keyboard>/f");
        elegirBate = Boton("Bate", "<Keyboard>/1", "<Gamepad>/dpad/left");
        elegirRifle = Boton("Rifle", "<Keyboard>/2", "<Gamepad>/dpad/right");
    }

    InputAction Boton(string nombre, params string[] rutas)
    {
        var accion = new InputAction(nombre, InputActionType.Button);
        foreach (var ruta in rutas) accion.AddBinding(ruta);
        return accion;
    }

    void OnEnable()
    {
        botiquin.Enable();
        bebida.Enable();
        elegirBate.Enable();
        elegirRifle.Enable();
    }

    void OnDisable()
    {
        botiquin.Disable();
        bebida.Disable();
        elegirBate.Disable();
        elegirRifle.Disable();
    }

    void OnDestroy()
    {
        botiquin.Dispose();
        bebida.Dispose();
        elegirBate.Dispose();
        elegirRifle.Dispose();
        if (Instancia == this) Instancia = null;
    }

    void Start() => AplicarArma();

    void Update()
    {
        var partida = ControladorPartidaBotellas.Instancia;
        bool puede = partida != null && !partida.EstaPausada && partida.Estado == ControladorPartidaBotellas.Resultado.EnCurso && !IntroduccionIsla.Activa;
        if (puede && elegirBate.WasPressedThisFrame()) armaActiva = Arma.Bate;
        if (puede && elegirRifle.WasPressedThisFrame() && TieneRifle()) armaActiva = Arma.Rifle;
        if (puede && botiquin.WasPressedThisFrame()) UsarBotiquin();
        if (puede && bebida.WasPressedThisFrame()) UsarBuff();
        EntregarMunicion();
        AplicarArma();
    }

    bool TieneRifle()
    {
        var arma = GetComponent<ControladorArmaFuego>();
        return arma != null && arma.Equipada;
    }

    public void AplicarArma()
    {
        if (bate != null) bate.SetActive(armaActiva == Arma.Bate);
        var arma = GetComponent<ControladorArmaFuego>();
        if (arma != null) arma.DisparoBloqueado = armaActiva != Arma.Rifle;
    }

    public void SumarMunicion(int cantidad)
    {
        municionPendiente += Mathf.Max(0, cantidad);
        EntregarMunicion();
    }

    void EntregarMunicion()
    {
        if (municionPendiente <= 0) return;
        var arma = GetComponent<ControladorArmaFuego>();
        if (arma == null || arma.Municion == null) return;
        arma.Municion.AgregarReserva(municionPendiente);
        municionPendiente = 0;
    }

    public void SumarBotiquin(int cantidad = 1) => botiquines += Mathf.Max(0, cantidad);
    public void SumarBuff(int cantidad = 1) => buffs += Mathf.Max(0, cantidad);

    public bool UsarBotiquin()
    {
        var partida = ControladorPartidaBotellas.Instancia;
        if (botiquines <= 0 || partida == null || partida.Vida >= .999f) return false;
        botiquines--;
        partida.Curar(.3f);
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.curar : null, transform.position, .8f, "Efectos");
        return true;
    }

    public bool UsarBuff()
    {
        if (buffs <= 0 || BuffActivo) return false;
        buffs--;
        StartCoroutine(Buff());
        return true;
    }

    IEnumerator Buff()
    {
        BuffActivo = true;
        multiplicadorDanio = 1.5f;
        if (locomocion != null)
        {
            if (!velocidadesGuardadas)
            {
                velocidadBase = locomocion.MoveSpeed;
                sprintBase = locomocion.SprintSpeed;
                velocidadesGuardadas = true;
            }
            locomocion.MoveSpeed = velocidadBase * 1.5f;
            locomocion.SprintSpeed = sprintBase * 1.5f;
        }
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.buff : null, transform.position, .8f, "Efectos");
        yield return new WaitForSeconds(15f);
        multiplicadorDanio = 1f;
        if (locomocion != null && velocidadesGuardadas)
        {
            locomocion.MoveSpeed = velocidadBase;
            locomocion.SprintSpeed = sprintBase;
        }
        BuffActivo = false;
    }
}
