using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Rifle hitscan en tercera persona: camara para apuntar, cañon para resolver cobertura.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class ControladorArmaFuego : MonoBehaviour
{
    public ArmaFuegoDefinicion Definicion { get; private set; }
    public EstadoMunicion Municion { get; private set; }
    public bool Equipada => modelo != null;
    public bool Apuntando { get; private set; }
    public bool SolicitaApuntar => PuedeActuar && Equipada && Municion != null && !Municion.Recargando && apuntar != null && apuntar.IsPressed();
    public float RecargaRestante { get; private set; }
    public float ImpactoHasta { get; private set; }
    public float Retroceso { get; private set; }
    public RecogibleArmaFuego Cercana { get; private set; }
    public bool DisparoBloqueado { get; set; }
    public bool UsaGamepad => GetComponent<PlayerInput>()?.currentControlScheme == "Gamepad";
    public bool PuedeActuar
    {
        get
        {
            var partida = ControladorPartidaBotellas.Instancia;
            return enabled && !DisparoBloqueado && !IntroduccionIsla.Activa && Time.timeScale > 0f && partida != null &&
                partida.Estado == ControladorPartidaBotellas.Resultado.EnCurso && !partida.EstaPausada;
        }
    }

    private InputActionMap acciones;
    private InputAction disparar, apuntar, recoger, recargar, soltar;
    private Transform modelo, fogonazo, cargador;
    private PoseRifleProcedural poseRifle;
    private Quaternion orientacionCuerpo;
    private bool siguiendoCamara;
    private AudioSource audioArma;
    private AudioClip click;
    private Camera camara;
    private Animator animador;
    private StarterAssets.ThirdPersonController locomocion;
    private CamaraApuntadoTerceraPersona camaraApuntado;
    private LineRenderer traza;
    private Material materialTraza;
    private Light luz;
    private float proximoDisparo, efectoHasta, proximaBusqueda;
    private bool estabaDisponible;
    // Tras pausa o recarga exigir soltar el gatillo para evitar un disparo involuntario.
    private bool gatilloBloqueado;

    private void Awake()
    {
        acciones = new InputActionMap("Armas");
        disparar = Accion("Disparar", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
        apuntar = Accion("Apuntar", "<Mouse>/rightButton", "<Gamepad>/leftShoulder");
        recoger = Accion("Recoger", "<Keyboard>/e", "<Gamepad>/buttonWest");
        recargar = Accion("Recargar", "<Keyboard>/r", "<Gamepad>/buttonNorth");
        soltar = Accion("Soltar", "<Keyboard>/g", "<Gamepad>/dpad/down");
        audioArma = gameObject.AddComponent<AudioSource>();
        audioArma.playOnAwake = false;
        audioArma.spatialBlend = 0f;
        audioArma.volume = .5f;
        Sonido.Asignar(audioArma, "Efectos");
        animador = GetComponentInChildren<Animator>();
        locomocion = GetComponent<StarterAssets.ThirdPersonController>();
        camaraApuntado = GetComponent<CamaraApuntadoTerceraPersona>();
        if (camaraApuntado == null) camaraApuntado = gameObject.AddComponent<CamaraApuntadoTerceraPersona>();
        camaraApuntado.Inicializar(this);
        click = CrearClick();
    }

    private InputAction Accion(string nombre, string teclado, string mando)
    {
        var accion = acciones.AddAction(nombre, InputActionType.Button);
        accion.AddBinding(teclado);
        accion.AddBinding(mando);
        return accion;
    }

    private bool ConsultarApuntado() => SolicitaApuntar;
    private void OnEnable()
    {
        acciones?.Enable();
        if (locomocion != null) locomocion.ConsultaApuntado = ConsultarApuntado;
    }
    private void OnDisable()
    {
        poseRifle?.Restaurar();
        acciones?.Disable();
        if (locomocion != null) locomocion.ConsultaApuntado = null;
        camaraApuntado?.Restablecer();
        Municion?.CancelarRecarga();
        RecargaRestante = 0f;
        Apuntando = false;
        OcultarEfectos();
        if (audioArma != null) audioArma.Stop();
    }

    private void OnDestroy()
    {
        acciones?.Dispose();
        if (materialTraza != null) Destroy(materialTraza);
        if (click != null) Destroy(click);
    }

    public void Equipar(ArmaFuegoDefinicion datos, EstadoMunicion estado)
    {
        if (Equipada || datos == null || datos.modelo == null || estado == null) return;
        Definicion = datos;
        Municion = estado;
        Municion.CancelarRecarga();
        modelo = Instantiate(datos.modelo, transform).transform;
        modelo.localScale *= datos.escalaEquipada;
        modelo.name = "RifleEquipado_SM_Wep_Rifle_Assault_03";
        RecogibleArmaFuego.PrepararModelo(modelo.gameObject);
        foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Contains("_Flash_")) fogonazo = t;
            if (t.name.Contains("_Mag_")) cargador = t;
        }
        poseRifle = new PoseRifleProcedural(transform, animador, modelo, cargador);
        var luzGo = new GameObject("LuzDisparo");
        luzGo.transform.SetParent(modelo, false);
        luzGo.transform.localPosition = datos.bocaCanon;
        luz = luzGo.AddComponent<Light>();
        luz.color = new Color(1f, .65f, .2f);
        luz.intensity = 2f;
        luz.range = 3f;
        luz.enabled = false;
        if (traza == null)
        {
            var go = new GameObject("TrazaDisparo");
            go.transform.SetParent(transform, false);
            traza = go.AddComponent<LineRenderer>();
            materialTraza = datos.materialTraza != null ? new Material(datos.materialTraza) :
                new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            materialTraza.SetColor("_BaseColor", new Color(1f, .76f, .22f));
            traza.sharedMaterial = materialTraza;
            traza.useWorldSpace = true;
            traza.positionCount = 2;
            traza.startWidth = .016f;
            traza.endWidth = .006f;
            traza.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            traza.enabled = false;
        }
        gatilloBloqueado = true;
        audioArma.PlayOneShot(click, .5f);
    }

    private void Update()
    {
        bool disponible = PuedeActuar;
        if (disponible) poseRifle?.Restaurar();
        if (!disponible)
        {
            if (estabaDisponible) audioArma.Stop();
            estabaDisponible = false;
            gatilloBloqueado = true;
            Apuntando = false;
            Cercana = null;
            OcultarEfectos();
            return;
        }
        estabaDisponible = true;
        camara = Camera.main;
        if (!disparar.IsPressed()) gatilloBloqueado = false;
        if (Time.time >= proximaBusqueda)
        {
            proximaBusqueda = Time.time + .1f;
            BuscarRecogible();
        }
        if (recoger.WasPressedThisFrame() && Cercana != null)
        {
            // Revalidar distancia y cobertura en el mismo frame de la interaccion.
            BuscarRecogible();
            if (Cercana != null && Cercana.Recoger(this)) audioArma.PlayOneShot(click, .6f);
        }
        if (!Equipada) return;
        if (soltar.WasPressedThisFrame()) { Soltar(); return; }
        if (recargar.WasPressedThisFrame()) IniciarRecarga();
        if (Municion.Recargando)
        {
            RecargaRestante = Mathf.Max(0f, RecargaRestante - Time.deltaTime);
            if (RecargaRestante <= 0f)
            {
                Municion.TerminarRecarga();
                audioArma.PlayOneShot(click, .8f);
            }
        }
        Apuntando = SolicitaApuntar;
        Retroceso = Mathf.MoveTowards(Retroceso, 0f, Time.deltaTime * 6f);
        if (Time.time >= efectoHasta) OcultarEfectos();
    }

    private void LateUpdate()
    {
        if (!PuedeActuar || !Equipada || camara == null) return;
        bool fuego = disparar.IsPressed() && !gatilloBloqueado;
        Vector3 frente = Vector3.ProjectOnPlane(camara.transform.forward, Vector3.up).normalized;
        bool orientar = (Apuntando || fuego) && !Municion.Recargando && frente.sqrMagnitude > .01f;
        if (orientar)
        {
            if (Apuntando && locomocion != null && locomocion.EnModoApuntado)
                orientacionCuerpo = transform.rotation;
            else
            {
                if (!siguiendoCamara) orientacionCuerpo = transform.rotation;
                orientacionCuerpo = Quaternion.RotateTowards(orientacionCuerpo, Quaternion.LookRotation(frente), 540f * Time.deltaTime);
                transform.rotation = orientacionCuerpo;
            }
        }
        siguiendoCamara = orientar;
        Vector3 direccion = orientar ? camara.transform.forward : transform.forward;
        float progreso = Municion.Recargando ? Mathf.Clamp01(1f - RecargaRestante / Mathf.Max(.1f, Definicion.segundosRecarga)) : 0f;
        poseRifle?.Aplicar(Definicion, direccion, Apuntando, fuego && !Municion.Recargando,
            Municion.Recargando, progreso, Retroceso, Time.deltaTime);
        if (fuego && !Municion.Recargando && Time.time >= proximoDisparo)
        {
            if (Municion.Cargador == 0)
            {
                audioArma.PlayOneShot(click, .6f);
                proximoDisparo = Time.time + .3f;
                IniciarRecarga();
            }
            else IntentarDisparar();
        }
    }

    public bool IniciarRecarga()
    {
        if (!PuedeActuar || Municion == null || !Municion.IniciarRecarga()) return false;
        RecargaRestante = Mathf.Max(.1f, Definicion.segundosRecarga);
        gatilloBloqueado = true;
        audioArma.PlayOneShot(click, .65f);
        return true;
    }

    public void Soltar()
    {
        if (!PuedeActuar || !Equipada) return;
        Municion.CancelarRecarga();
        RecargaRestante = 0f;
        var suelo = SistemaArmasFuego.BuscarSuelo(transform.position + transform.right * 1.1f, transform);
        RecogibleArmaFuego.Crear(Definicion, suelo, Municion);
        OcultarEfectos();
        poseRifle?.Restaurar();
        poseRifle = null;
        siguiendoCamara = false;
        Destroy(modelo.gameObject);
        modelo = null;
        fogonazo = null;
        cargador = null;
        luz = null;
        Municion = null;
        Apuntando = false;
        BuscarRecogible();
    }

    private void BuscarRecogible()
    {
        Cercana = null;
        float menor = 2.5f * 2.5f;
        Vector3 origen = transform.position + Vector3.up;
        foreach (var item in RecogibleArmaFuego.Activos)
        {
            if (!PuedeRecoger(item)) continue;
            Vector3 destino = item.transform.position + Vector3.up * .65f;
            float distancia = (destino - origen).sqrMagnitude;
            if (distancia >= menor) continue;
            menor = distancia;
            Cercana = item;
        }
    }

    public bool PuedeRecoger(RecogibleArmaFuego item)
    {
        if (!PuedeActuar || item == null || item.Definicion == null || (Equipada && !item.SoloMunicion)) return false;
        if (item.SoloMunicion && (Municion == null || item.Definicion != Definicion || Municion.Reserva >= Municion.ReservaMaxima)) return false;
        Vector3 origen = transform.position + Vector3.up;
        Vector3 trayecto = item.transform.position + Vector3.up * .65f - origen;
        return trayecto.sqrMagnitude <= 2.5f * 2.5f && !Impactar(origen, trayecto, trayecto.magnitude, out _);
    }

    // Incluir triggers de enemigos, pero omitir botellas, agua y zonas de daño.
    public bool Impactar(Vector3 origen, Vector3 direccion, float alcance, out RaycastHit impacto)
    {
        impacto = default;
        float menor = float.PositiveInfinity;
        int mascara = LayerMask.GetMask("Default", CapasJuego.Enemigos, CapasJuego.Objetos);
        if (mascara == 0) mascara = ~0;
        foreach (var hit in Physics.RaycastAll(origen, direccion.normalized, alcance, mascara, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.isTrigger && hit.collider.GetComponentInParent<ControladorVida>() == null) continue;
            if (hit.distance >= menor) continue;
            menor = hit.distance;
            impacto = hit;
        }
        return menor < float.PositiveInfinity;
    }

    public bool IntentarDisparar()
    {
        if (!PuedeActuar || !Equipada || Municion == null || Time.time < proximoDisparo) return false;
        camara = Camera.main;
        if (camara == null || !Municion.Disparar()) return false;
        proximoDisparo = Time.time + 1f / Mathf.Max(1f, Definicion.disparosPorSegundo);
        float dispersion = Apuntando ? Definicion.dispersionApuntado : Definicion.dispersionCadera;
        Vector2 error = Random.insideUnitCircle * Mathf.Tan(dispersion * Mathf.Deg2Rad);
        Vector3 direccion = (camara.transform.forward + camara.transform.right * error.x + camara.transform.up * error.y).normalized;
        Vector3 objetivo = camara.transform.position + direccion * Definicion.alcance;
        if (Impactar(camara.transform.position, direccion, Definicion.alcance, out var vista)) objetivo = vista.point;
        Vector3 boca = modelo.TransformPoint(Definicion.bocaCanon);
        Vector3 origen = transform.position + Vector3.up * Definicion.posicionEnCuerpo.y;
        bool golpe = Impactar(origen, boca - origen, Vector3.Distance(origen, boca), out var hit);
        // Cruzar ligeramente el punto de vista evita perder impactos por redondeo justo en la superficie.
        if (!golpe) golpe = Impactar(boca, objetivo - boca, Mathf.Min(Definicion.alcance, Vector3.Distance(boca, objetivo) + .05f), out hit);
        Vector3 fin = golpe ? hit.point : boca + (objetivo - boca).normalized * Mathf.Min(Definicion.alcance, Vector3.Distance(boca, objetivo));
        if (golpe)
        {
            var enemigo = hit.collider.GetComponentInParent<ControladorVida>();
            if (enemigo != null)
            {
                float multiplicador = InventarioJugador.Instancia != null ? InventarioJugador.Instancia.MultiplicadorDanio : 1f;
                float danio = Definicion.danio * multiplicador * (enemigo.EsCabeza(hit.point) ? Definicion.multiplicadorCabeza : 1f);
                if (enemigo.RecibirDanio(danio, hit.point)) ImpactoHasta = Time.time + .15f;
            }
        }
        traza.SetPosition(0, boca);
        traza.SetPosition(1, fin);
        traza.enabled = true;
        if (fogonazo != null) fogonazo.gameObject.SetActive(true);
        if (luz != null) luz.enabled = true;
        efectoHasta = Time.time + .045f;
        Retroceso = Mathf.Min(1.5f, Retroceso + .55f);
        if (Definicion.disparo != null) audioArma.PlayOneShot(Definicion.disparo, .65f);
        return true;
    }

    private void OcultarEfectos()
    {
        if (traza != null) traza.enabled = false;
        if (fogonazo != null) fogonazo.gameObject.SetActive(false);
        if (luz != null) luz.enabled = false;
    }

    private static AudioClip CrearClick()
    {
        const int frecuencia = 22050;
        var muestras = new float[1102];
        for (int i = 0; i < muestras.Length; i++)
        {
            float t = (float)i / frecuencia;
            muestras[i] = Mathf.Sin(t * 2100f * Mathf.PI * 2f) * Mathf.Exp(-t * 110f) * .18f;
        }
        var clip = AudioClip.Create("MecanismoRifle", muestras.Length, 1, frecuencia, false);
        clip.SetData(muestras, 0);
        return clip;
    }
}
