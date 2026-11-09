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

    void Start()
    {
        AjustarBateEnMano();
        AplicarArma();
    }

    // Distancia del pomo al centro del mango del bate Synty (pivote en el pomo, eje +Y hacia la hoja).
    const float AgarreBate = .15f;

    /// <summary>Coloca el mango en la palma derecha usando la geometria real de la mano, no valores fijos.</summary>
    void AjustarBateEnMano()
    {
        if (bate == null) return;
        var rig = GetComponentInChildren<Animator>();
        if (rig == null || !rig.isHuman) return;
        var mano = rig.GetBoneTransform(HumanBodyBones.RightHand);
        var medio = rig.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        var indice = rig.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        if (mano == null || medio == null || indice == null) return;
        var menique = rig.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        var pulgar = rig.GetBoneTransform(HumanBodyBones.RightThumbProximal);

        Vector3 dedos = mano.InverseTransformDirection(medio.position - mano.position).normalized;
        Vector3 centro = mano.InverseTransformPoint(Vector3.Lerp(mano.position, (medio.position + indice.position) * .5f, .5f));
        // El menique queda en el pomo y el indice/pulgar del lado de la hoja.
        Vector3 ancho = Vector3.zero;
        if (menique != null) ancho = mano.InverseTransformDirection(indice.position - menique.position);
        if (Vector3.ProjectOnPlane(ancho, dedos).sqrMagnitude < 1e-6f && pulgar != null)
            ancho = mano.InverseTransformDirection(pulgar.position - mano.position);
        ancho = Vector3.ProjectOnPlane(ancho, dedos);
        if (ancho.sqrMagnitude < 1e-6f) return;
        ancho.Normalize();

        // Inclinar la hoja hacia arriba: con el brazo caido los dedos apuntan abajo, asi que se aleja de ellos.
        Vector3 eje = (ancho * Mathf.Cos(40f * Mathf.Deg2Rad) - dedos * Mathf.Sin(40f * Mathf.Deg2Rad)).normalized;
        Quaternion giro = Quaternion.LookRotation(Vector3.Cross(eje, Vector3.Cross(dedos, eje)).normalized, eje);

        bate.transform.SetParent(mano, false);
        Vector3 escalaMano = mano.lossyScale;
        Vector3 escala = new Vector3(1f / Mathf.Max(.0001f, escalaMano.x), 1f / Mathf.Max(.0001f, escalaMano.y), 1f / Mathf.Max(.0001f, escalaMano.z));
        bate.transform.localScale = escala;
        bate.transform.localRotation = giro;
        bate.transform.localPosition = centro - giro * Vector3.Scale(Vector3.up * AgarreBate, escala);
    }

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
        var arma = GetComponent<ControladorArmaFuego>();
        // Sin rifle en las manos no hay nada que seleccionar: volver al bate.
        if (armaActiva == Arma.Rifle && (arma == null || !arma.Equipada)) armaActiva = Arma.Bate;
        if (bate != null) bate.SetActive(armaActiva == Arma.Bate);
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
