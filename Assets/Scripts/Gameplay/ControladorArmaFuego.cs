using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Armas de fuego hitscan en tercera persona: cámara para apuntar, cañón para resolver cobertura.
/// Lleva una ranura por tipo (rifle y escopeta); la selección la decide InventarioJugador.
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class ControladorArmaFuego : MonoBehaviour
{
    /// <summary>Botellas que hay que juntar para recibir la escopeta.</summary>
    public const int BotellasEscopeta = 10;

    sealed class Ranura
    {
        public ArmaFuegoDefinicion datos;
        public EstadoMunicion municion;
        public Transform modelo, fogonazo, cargador, corredera;
        public Vector3 origenCorredera;
        public PoseRifleProcedural pose;
        public Light luz;
        public float ultimoDisparo = -10f, calor;
        public bool casquilloPendiente, bombeoSonado;
    }

    sealed class Danio
    {
        public float total;
        public Vector3 punto, direccion;
        public int perdigones;
    }

    readonly Dictionary<ArmaFuegoDefinicion.Tipo, Ranura> ranuras = new Dictionary<ArmaFuegoDefinicion.Tipo, Ranura>();
    readonly Dictionary<ControladorVida, Danio> danios = new Dictionary<ControladorVida, Danio>();
    Ranura activa;

    public ArmaFuegoDefinicion Definicion => activa?.datos;
    public EstadoMunicion Municion => activa?.municion;
    /// <summary>Hay un arma de fuego seleccionada (no el bate).</summary>
    public bool Equipada => activa != null;
    public bool EnMano => Equipada && !DisparoBloqueado;
    public bool Apuntando { get; private set; }
    public bool SolicitaApuntar => PuedeActuar && EnMano && Municion != null && !Municion.Recargando && apuntar != null && apuntar.IsPressed();
    public float RecargaRestante { get; private set; }
    public float ImpactoHasta { get; private set; }
    public float Retroceso { get; private set; }
    public RecogibleArmaFuego Cercana { get; private set; }
    public bool DisparoBloqueado { get; set; }
    /// <summary>Mensaje grande de la última arma recibida (la escopeta al juntar botellas).</summary>
    public string Aviso { get; private set; }
    public float AvisoHasta { get; private set; }
    public bool UsaGamepad => GetComponent<PlayerInput>()?.currentControlScheme == "Gamepad";
    public bool PuedeActuar
    {
        get
        {
            var partida = ControladorPartidaBotellas.Instancia;
            return enabled && !IntroduccionIsla.Activa && Time.timeScale > 0f && partida != null &&
                partida.Estado == ControladorPartidaBotellas.Resultado.EnCurso && !partida.EstaPausada;
        }
    }

    public bool Tiene(ArmaFuegoDefinicion.Tipo tipo) => ranuras.ContainsKey(tipo);
    public EstadoMunicion MunicionDe(ArmaFuegoDefinicion.Tipo tipo) => ranuras.TryGetValue(tipo, out var r) ? r.municion : null;
    public ArmaFuegoDefinicion DefinicionDe(ArmaFuegoDefinicion.Tipo tipo) => ranuras.TryGetValue(tipo, out var r) ? r.datos : null;

    private InputActionMap acciones;
    private InputAction disparar, apuntar, recoger, recargar, soltar;
    private Quaternion orientacionCuerpo;
    private bool siguiendoCamara;
    private AudioSource audioArma;
    private AudioClip click, bombeo;
    private Camera camara;
    private Animator animador;
    private StarterAssets.ThirdPersonController locomocion;
    private CharacterController capsula;
    private CamaraApuntadoTerceraPersona camaraApuntado;
    private EfectosRifle efectos;
    private float proximoDisparo, efectoHasta, proximaBusqueda;
    private bool estabaDisponible, escopetaEntregada;
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
        capsula = GetComponent<CharacterController>();
        camaraApuntado = GetComponent<CamaraApuntadoTerceraPersona>();
        if (camaraApuntado == null) camaraApuntado = gameObject.AddComponent<CamaraApuntadoTerceraPersona>();
        camaraApuntado.Inicializar(this);
        click = CrearClick();
        bombeo = CrearBombeo();
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
        foreach (var r in ranuras.Values) r.pose?.Restaurar();
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
        if (click != null) Destroy(click);
        if (bombeo != null) Destroy(bombeo);
    }

    public void Equipar(ArmaFuegoDefinicion datos, EstadoMunicion estado)
    {
        if (datos == null || datos.modelo == null || estado == null || Tiene(datos.tipo)) return;
        var r = new Ranura { datos = datos, municion = estado };
        estado.CancelarRecarga();
        r.modelo = Instantiate(datos.modelo, transform).transform;
        r.modelo.localScale *= datos.escalaEquipada;
        r.modelo.name = "Equipada_" + datos.modelo.name;
        RecogibleArmaFuego.PrepararModelo(r.modelo.gameObject);
        foreach (var t in r.modelo.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Contains("_Flash_")) r.fogonazo = t;
            if (t.name.Contains("_Mag_")) r.cargador = t;
            if (!string.IsNullOrEmpty(datos.corredera) && t.name.Contains(datos.corredera)) r.corredera = t;
        }
        if (r.corredera != null) r.origenCorredera = r.corredera.localPosition;
        r.pose = new PoseRifleProcedural(transform, animador, r.modelo, r.cargador);
        var luzGo = new GameObject("LuzDisparo");
        luzGo.transform.SetParent(r.modelo, false);
        luzGo.transform.localPosition = datos.bocaCanon + Vector3.forward * .08f;
        r.luz = luzGo.AddComponent<Light>();
        r.luz.color = new Color(1f, .62f, .22f);
        r.luz.intensity = 3f * datos.escalaFogonazo;
        r.luz.range = 4f + 2.5f * datos.escalaFogonazo;
        r.luz.shadows = LightShadows.None;
        r.luz.enabled = false;
        r.modelo.gameObject.SetActive(false);
        ranuras[datos.tipo] = r;
        if (efectos == null) efectos = EfectosRifle.Crear(transform);
        gatilloBloqueado = true;
        audioArma.PlayOneShot(click, .5f);
        // Recoger un arma la selecciona; antes el bate la dejaba enfundada e imposible de usar.
        var inventario = InventarioJugador.Instancia;
        if (inventario != null)
        {
            inventario.armaActiva = datos.tipo == ArmaFuegoDefinicion.Tipo.Escopeta ? InventarioJugador.Arma.Escopeta : InventarioJugador.Arma.Rifle;
            inventario.AplicarArma();
        }
        Seleccionar();
    }

    /// <summary>La ranura activa sigue al inventario: 2 rifle, 3 escopeta, 1 bate (ninguna).</summary>
    private void Seleccionar()
    {
        Ranura nueva = null;
        var inventario = InventarioJugador.Instancia;
        if (inventario != null)
        {
            if (inventario.armaActiva == InventarioJugador.Arma.Rifle) ranuras.TryGetValue(ArmaFuegoDefinicion.Tipo.Rifle, out nueva);
            else if (inventario.armaActiva == InventarioJugador.Arma.Escopeta) ranuras.TryGetValue(ArmaFuegoDefinicion.Tipo.Escopeta, out nueva);
        }
        else
        {
            foreach (var r in ranuras.Values) { nueva = r; break; }
        }
        if (nueva == activa) return;
        if (activa != null)
        {
            activa.pose?.Restaurar();
            activa.municion.CancelarRecarga();
            activa.modelo.gameObject.SetActive(false);
        }
        OcultarEfectos();
        RecargaRestante = 0f;
        Apuntando = false;
        siguiendoCamara = false;
        activa = nueva;
        gatilloBloqueado = true;
        // Cambiar de arma no debe permitir disparar en el mismo instante.
        proximoDisparo = Mathf.Max(proximoDisparo, Time.time + .25f);
        if (activa != null) audioArma.PlayOneShot(click, .7f);
    }

    private void Update()
    {
        bool disponible = PuedeActuar;
        foreach (var r in ranuras.Values) if (disponible) r.pose?.Restaurar();
        Seleccionar();
        foreach (var r in ranuras.Values)
        {
            bool visible = r == activa && !DisparoBloqueado;
            if (r.modelo.gameObject.activeSelf != visible) r.modelo.gameObject.SetActive(visible);
        }
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
        EntregarEscopeta();
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
        foreach (var r in ranuras.Values) r.calor = Mathf.MoveTowards(r.calor, 0f, Time.deltaTime * .3f);
        if (activa == null || DisparoBloqueado)
        {
            Apuntando = false;
            siguiendoCamara = false;
            return;
        }
        if (soltar.WasPressedThisFrame()) { Soltar(); return; }
        if (recargar.WasPressedThisFrame()) IniciarRecarga();
        if (Municion.Recargando)
        {
            RecargaRestante = Mathf.Max(0f, RecargaRestante - Time.deltaTime);
            if (RecargaRestante <= 0f)
            {
                if (Definicion.recargaPorCartucho)
                {
                    Municion.CargarUno();
                    audioArma.PlayOneShot(click, .7f);
                    if (Municion.Recargando) RecargaRestante = Definicion.segundosRecarga;
                    else audioArma.PlayOneShot(bombeo, .7f);
                }
                else
                {
                    Municion.TerminarRecarga();
                    audioArma.PlayOneShot(click, .8f);
                }
            }
        }
        Apuntando = SolicitaApuntar;
        Retroceso = Mathf.MoveTowards(Retroceso, 0f, Time.deltaTime * 6f);
        if (Time.time >= efectoHasta) OcultarEfectos();
    }

    /// <summary>Al juntar suficientes botellas el personaje recibe la escopeta, cargada y en la mano.</summary>
    private void EntregarEscopeta()
    {
        if (escopetaEntregada) return;
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida == null || partida.Recogidas < BotellasEscopeta) return;
        escopetaEntregada = true;
        if (Tiene(ArmaFuegoDefinicion.Tipo.Escopeta)) return;
        var datos = Resources.Load<ArmaFuegoDefinicion>("Armas/Escopeta");
        if (datos == null || datos.modelo == null)
        {
            Debug.LogError("Falta Resources/Armas/Escopeta o su modelo.");
            return;
        }
        Equipar(datos, new EstadoMunicion(datos.capacidad, datos.reservaMaxima, datos.capacidad, datos.reservaInicial));
        audioArma.PlayOneShot(bombeo, 1f);
        Aviso = "¡CONSEGUISTE UNA ESCOPETA!";
        AvisoHasta = Time.time + 3.5f;
    }

    private void LateUpdate()
    {
        if (!PuedeActuar || !EnMano || camara == null) return;
        var r = activa;
        var datos = r.datos;
        bool fuego = disparar.IsPressed() && !gatilloBloqueado;
        // La escopeta se puede disparar a mitad de la recarga si ya tiene algún cartucho.
        if (fuego && Municion.Recargando && datos.recargaPorCartucho && Municion.Cargador > 0)
        {
            Municion.CancelarRecarga();
            RecargaRestante = 0f;
        }
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
        float progreso = Municion.Recargando ? Mathf.Clamp01(1f - RecargaRestante / Mathf.Max(.1f, datos.segundosRecarga)) : 0f;
        AnimarCorredera(r);
        r.pose?.Aplicar(datos, direccion, Apuntando, fuego && !Municion.Recargando,
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
        // El cañón caliente sigue humeando un rato después de la ráfaga.
        if (efectos != null && r.calor > .05f && Time.time - r.ultimoDisparo > .15f)
            efectos.HumoResidual(r.modelo.TransformPoint(datos.bocaCanon), r.modelo.forward, r.calor);
    }

    /// <summary>Escopeta: tras cada tiro la corredera va atrás (sale el cartucho vacío) y vuelve.</summary>
    private void AnimarCorredera(Ranura r)
    {
        var datos = r.datos;
        float t = Time.time - r.ultimoDisparo;
        float inicio = Mathf.Max(.05f, datos.retrasoCasquillo - .14f);
        float fase = Mathf.Clamp01((t - inicio) / .32f);
        float atras = r.corredera != null && fase > 0f && fase < 1f ? Mathf.Sin(fase * Mathf.PI) : 0f;
        if (r.corredera != null)
        {
            r.corredera.localPosition = r.origenCorredera - Vector3.forward * (.1f * atras);
            r.pose.DesplazamientoIzquierdo = -Vector3.forward * (.1f * atras);
            if (fase > 0f && !r.bombeoSonado)
            {
                r.bombeoSonado = true;
                audioArma.PlayOneShot(bombeo, .85f);
            }
        }
        if (r.casquilloPendiente && t >= datos.retrasoCasquillo)
        {
            r.casquilloPendiente = false;
            ExpulsarCasquillo(r);
        }
    }

    private void ExpulsarCasquillo(Ranura r)
    {
        var datos = r.datos;
        Vector3 derecha = r.modelo.right, arriba = r.modelo.up, frente = r.modelo.forward;
        Vector3 inercia = capsula != null ? capsula.velocity : Vector3.zero;
        Vector3 velocidad = derecha * Random.Range(1.8f, 2.9f) + arriba * Random.Range(1.3f, 2.2f)
            - frente * Random.Range(0f, .6f) + inercia;
        Quaternion giro = r.modelo.rotation * Quaternion.Euler(Random.Range(-20f, 20f), 0f, 90f);
        CasquillosFisicos.Expulsar(datos, r.modelo.TransformPoint(datos.expulsion) + derecha * .02f, giro, velocidad, capsula);
    }

    public bool IniciarRecarga()
    {
        if (!PuedeActuar || !EnMano || Municion == null || !Municion.IniciarRecarga()) return false;
        RecargaRestante = Mathf.Max(.1f, Definicion.segundosRecarga);
        gatilloBloqueado = true;
        audioArma.PlayOneShot(click, .65f);
        return true;
    }

    public void Soltar()
    {
        if (!PuedeActuar || !EnMano) return;
        var r = activa;
        r.municion.CancelarRecarga();
        RecargaRestante = 0f;
        var suelo = SistemaArmasFuego.BuscarSuelo(transform.position + transform.right * 1.1f, transform);
        RecogibleArmaFuego.Crear(r.datos, suelo, r.municion);
        OcultarEfectos();
        r.pose?.Restaurar();
        siguiendoCamara = false;
        Destroy(r.modelo.gameObject);
        ranuras.Remove(r.datos.tipo);
        activa = null;
        Apuntando = false;
        var inventario = InventarioJugador.Instancia;
        if (inventario != null)
        {
            // Pasar a la otra arma de fuego si queda una; si no, al bate.
            inventario.armaActiva = Tiene(ArmaFuegoDefinicion.Tipo.Rifle) ? InventarioJugador.Arma.Rifle :
                Tiene(ArmaFuegoDefinicion.Tipo.Escopeta) ? InventarioJugador.Arma.Escopeta : InventarioJugador.Arma.Bate;
            inventario.AplicarArma();
        }
        Seleccionar();
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
        if (!PuedeActuar || item == null || item.Definicion == null) return false;
        var tipo = item.Definicion.tipo;
        if (!item.SoloMunicion && Tiene(tipo)) return false;
        if (item.SoloMunicion)
        {
            var municion = MunicionDe(tipo);
            if (municion == null || municion.Reserva >= municion.ReservaMaxima) return false;
        }
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

    /// <summary>Fracción de daño según la distancia: los perdigones pierden fuerza lejos.</summary>
    private static float Caida(ArmaFuegoDefinicion datos, float distancia)
    {
        if (datos.alcanceEfectivo <= 0f || distancia <= datos.alcanceEfectivo) return 1f;
        return Mathf.Lerp(1f, .35f, Mathf.InverseLerp(datos.alcanceEfectivo, datos.alcance, distancia));
    }

    public bool IntentarDisparar()
    {
        if (!PuedeActuar || !EnMano || Municion == null || Time.time < proximoDisparo) return false;
        camara = Camera.main;
        if (camara == null || !Municion.Disparar()) return false;
        var r = activa;
        var datos = r.datos;
        proximoDisparo = Time.time + 1f / Mathf.Max(.1f, datos.disparosPorSegundo);
        float dispersion = Apuntando ? datos.dispersionApuntado : datos.dispersionCadera;
        Vector3 boca = r.modelo.TransformPoint(datos.bocaCanon);
        Vector3 origen = transform.position + Vector3.up * datos.posicionEnCuerpo.y;
        // Con el cañón metido en una pared, todo el disparo da en ella.
        bool tapado = Impactar(origen, boca - origen, Vector3.Distance(origen, boca), out var tapa);
        danios.Clear();
        int marcas = 0;
        Vector3 vista0 = camara.transform.forward;
        for (int i = 0; i < Mathf.Max(1, datos.perdigones); i++)
        {
            Vector2 error = Random.insideUnitCircle * Mathf.Tan(dispersion * Mathf.Deg2Rad);
            Vector3 direccion = (camara.transform.forward + camara.transform.right * error.x + camara.transform.up * error.y).normalized;
            Vector3 objetivo = camara.transform.position + direccion * datos.alcance;
            if (Impactar(camara.transform.position, direccion, datos.alcance, out var vista)) objetivo = vista.point;
            if (i == 0) vista0 = objetivo - boca;
            RaycastHit hit = tapa;
            bool golpe = tapado;
            // Cruzar ligeramente el punto de vista evita perder impactos por redondeo justo en la superficie.
            if (!golpe) golpe = Impactar(boca, objetivo - boca, Mathf.Min(datos.alcance, Vector3.Distance(boca, objetivo) + .05f), out hit);
            Vector3 fin = golpe ? hit.point : boca + (objetivo - boca).normalized * Mathf.Min(datos.alcance, Vector3.Distance(boca, objetivo));
            if (golpe)
            {
                var enemigo = hit.collider.GetComponentInParent<ControladorVida>();
                if (enemigo != null && !enemigo.Muerto)
                {
                    float danio = datos.danio * Caida(datos, Vector3.Distance(boca, hit.point)) *
                        (enemigo.EsCabeza(hit.point) ? datos.multiplicadorCabeza : 1f);
                    if (!danios.TryGetValue(enemigo, out var suma)) danios[enemigo] = suma = new Danio { punto = hit.point };
                    suma.total += danio;
                    suma.perdigones++;
                    suma.direccion += (hit.point - boca).normalized;
                }
                // Los enemigos sangran (ReaccionImpacto); el resto recibe chispas, polvo y el agujero.
                if (efectos != null && (enemigo == null || enemigo.GetComponent<EnemigoAcechador>() == null) && marcas++ < 6)
                    efectos.Impacto(hit);
            }
            // La trazadora viaja hasta el blanco en vez de dibujarse de golpe; de la escopeta, solo algunos perdigones.
            if (efectos != null && i < 3) efectos.Trazadora(boca, fin);
        }
        float multiplicador = InventarioJugador.Instancia != null ? InventarioJugador.Instancia.MultiplicadorDanio : 1f;
        foreach (var par in danios)
        {
            var enemigo = par.Key;
            var suma = par.Value;
            Vector3 dir = suma.direccion.sqrMagnitude > .001f ? suma.direccion.normalized : vista0.normalized;
            if (enemigo.RecibirDanio(suma.total * multiplicador, suma.punto, dir)) ImpactoHasta = Time.time + .15f;
            // Un escopetazo que mata de cerca (o con casi toda la carga) revienta al zombie.
            if (enemigo.Muerto && datos.tipo == ArmaFuegoDefinicion.Tipo.Escopeta && !enemigo.esJefe &&
                enemigo.GetComponent<EnemigoAcechador>() != null &&
                (suma.perdigones >= 3 || Vector3.Distance(boca, suma.punto) < 9f))
                EfectosGore.Reventar(enemigo, suma.punto, dir);
        }
        if (efectos != null) efectos.Disparo(boca, vista0, Apuntando, datos.escalaFogonazo);
        // El disparo se oye lejos: los zombies cercanos vienen a ver.
        EnemigoAcechador.Ruido(transform.position, datos.ruido);
        if (r.fogonazo != null) r.fogonazo.gameObject.SetActive(true);
        if (r.luz != null) r.luz.enabled = true;
        efectoHasta = Time.time + .05f;
        Retroceso = Mathf.Min(Mathf.Max(1.5f, datos.retrocesoArma), Retroceso + datos.retrocesoArma);
        if (locomocion != null)
            locomocion.AgregarRetroceso(datos.retrocesoCamara * (Apuntando ? .75f : 1f) * Random.Range(.85f, 1.15f),
                Random.Range(-.35f, .35f) * datos.retrocesoCamara);
        if (datos.disparo != null) audioArma.PlayOneShot(datos.disparo, datos.tipo == ArmaFuegoDefinicion.Tipo.Escopeta ? .85f : .65f);
        r.ultimoDisparo = Time.time;
        r.bombeoSonado = false;
        r.calor = Mathf.Min(1f, r.calor + (datos.perdigones > 1 ? .7f : .1f));
        r.casquilloPendiente = true;
        if (datos.retrasoCasquillo <= 0f) AnimarCorredera(r);
        return true;
    }

    private void OcultarEfectos()
    {
        foreach (var r in ranuras.Values)
        {
            if (r.fogonazo != null) r.fogonazo.gameObject.SetActive(false);
            if (r.luz != null) r.luz.enabled = false;
        }
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

    /// <summary>"Chac-chac" de la corredera: dos golpes secos con ruido metálico.</summary>
    private static AudioClip CrearBombeo()
    {
        const int frecuencia = 22050;
        var muestras = new float[(int)(frecuencia * .32f)];
        var azar = new System.Random(11);
        for (int i = 0; i < muestras.Length; i++)
        {
            float t = (float)i / frecuencia;
            float golpe1 = Mathf.Exp(-t * 60f);
            float t2 = t - .17f;
            float golpe2 = t2 > 0f ? Mathf.Exp(-t2 * 55f) : 0f;
            float ruido = (float)azar.NextDouble() * 2f - 1f;
            float tono = Mathf.Sin(t * 900f * Mathf.PI * 2f) * .5f + Mathf.Sin(t * 1700f * Mathf.PI * 2f) * .3f;
            muestras[i] = (ruido * .6f + tono) * (golpe1 + golpe2 * 1.1f) * .22f;
        }
        var clip = AudioClip.Create("BombeoEscopeta", muestras.Length, 1, frecuencia, false);
        clip.SetData(muestras, 0);
        return clip;
    }
}
