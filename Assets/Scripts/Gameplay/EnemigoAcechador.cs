using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Persigue al jugador con NavMeshAgent, como MovimientoSimpleNPC del curso.
/// Nemesis usa el agente Humanoid; el titán usa su propio tipo de agente ("Titan").
/// Con modoHorda (zombies) el enemigo deambula hasta ver u oír al jugador, avisa a los que tiene cerca,
/// lo rodea en vez de ir en fila, corre y remata con una embestida.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class EnemigoAcechador : MonoBehaviour
{
    [Tooltip("Objetivo a perseguir. Si queda vacío se busca el objeto con tag Player.")]
    public Transform objetivo;
    public bool usarNavMesh = true;
    [Tooltip("Radio para llevar el destino al NavMesh del agente (el titán no cabe en calles angostas).")]
    public float alcanceDestino = 60f;
    public float velocidadCaminar = 2.2f;
    public float velocidadCorrer = 4.6f;
    public float umbralCorrer = 8f;
    public float retrasoAparicion = 5f;
    public AudioClip pisadaPesada;
    [Range(0f, 1f)] public float volumenPisada = 1f;
    public float radioDeteccion;
    public float rangoAtaque;
    public float cadenciaAtaque = 1.4f;
    public GolpeMelee golpe;
    public AudioClip sonidoAtaque;

    [Header("Eventos (como MobController del curso)")]
    public UnityEngine.Events.UnityEvent alAtacar;
    public UnityEngine.Events.UnityEvent<Transform> alDetectarObjetivo;
    public UnityEngine.Events.UnityEvent alMorir;

    [Header("Horda (zombies)")]
    public bool modoHorda;
    [Tooltip("A esta distancia lo detecta aunque esté de espaldas o detrás de un muro.")]
    public float radioOido = 7f;
    public float anguloVision = 140f;
    public float radioVagar = 9f;
    public float velocidadVagar = 1.1f;
    [Tooltip("Si el jugador pasa varios segundos más lejos que esto, vuelve a deambular.")]
    public float distanciaOlvido = 60f;
    [Tooltip("Velocidad del salto final al atacar.")]
    public float embestida = 5.5f;

    /// <summary>Enemigos activos, para avisarse entre ellos y reaccionar al ruido.</summary>
    public static readonly List<EnemigoAcechador> Activos = new List<EnemigoAcechador>();

    public bool Persiguiendo { get; private set; }
    public bool Atacando { get; private set; }
    public bool Alertado => detectado;
    bool detectado;
    float proximoAtaque, aturdidoHasta;
    // Horda
    Vector3 hogar, posicionObjetivoPrevia, velocidadObjetivo;
    float flanco, proximoVagar, lejosDesde = -1f, medidaPrevia, atascadoDesde = -1f;
    bool embistiendo, golpeAbierto, navMeshPedido;
    static float proximoGrunido;
    static int mascaraVision = -1;
    Coroutine ataque;

    Animator animator;
    NavMeshAgent agente;
    float escalaLocomocion = 1f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agente = GetComponent<NavMeshAgent>();
        navMeshPedido = usarNavMesh;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (TieneParametro("Grounded")) animator.SetBool("Grounded", true);
        if (TieneParametro("Speed")) animator.SetFloat("Speed", 0f);
        if (TieneParametro("MotionSpeed")) animator.SetFloat("MotionSpeed", 1f);
        escalaLocomocion = Mathf.Max(1f, Altura() / 1.8f);
        if (agente != null)
            agente.enabled = false;
        flanco = Random.Range(-1f, 1f);
        hogar = transform.position;
        if (modoHorda) AjustarAgenteHorda();
    }

    void OnEnable()
    {
        if (!Activos.Contains(this)) Activos.Add(this);
        // ControladorVida puede añadirse después de Awake: se engancha aquí y no se duplica.
        var vida = GetComponent<ControladorVida>();
        if (vida != null)
        {
            vida.alMorir.RemoveListener(AlMorirVida);
            vida.alMorir.AddListener(AlMorirVida);
        }
        StartCoroutine(Perseguir());
    }

    void AlMorirVida() => alMorir?.Invoke();

    void OnDisable()
    {
        var vida = GetComponent<ControladorVida>();
        if (vida != null) vida.alMorir.RemoveListener(AlMorirVida);
        Activos.Remove(this);
        StopAllCoroutines();
        Persiguiendo = false;
        Atacando = false;
        embistiendo = false;
        if (golpe != null) golpe.FinGolpe();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Activos.Clear();
        proximoGrunido = 0f;
    }

    // ---------------------------------------------------------------- Horda

    /// <summary>Activa el comportamiento de horda con la velocidad de carrera de este zombie.</summary>
    public void ConfigurarHorda(float correr, float vision, float oido)
    {
        modoHorda = true;
        velocidadCorrer = correr;
        radioDeteccion = vision;
        radioOido = oido;
        hogar = transform.position;
        AjustarAgenteHorda();
    }

    void AjustarAgenteHorda()
    {
        if (agente == null) return;
        agente.acceleration = 22f;
        agente.angularSpeed = 540f;
        agente.autoBraking = false;
        agente.stoppingDistance = .9f;
        // Prioridades distintas: se abren paso entre ellos en vez de trabarse en fila.
        agente.avoidancePriority = Random.Range(30, 70);
    }

    /// <summary>Mueve al enemigo a un punto del NavMesh (estaba bajo el mar o trabado) y retoma la persecución.</summary>
    public void Reubicar(Vector3 punto)
    {
        transform.position = punto;
        hogar = punto;
        atascadoDesde = -1f;
        if (agente == null) return;
        // Si al arrancar no encontró NavMesh había quedado en línea recta: ahora sí lo tiene.
        if (navMeshPedido && !usarNavMesh && Persiguiendo)
        {
            usarNavMesh = true;
            agente.enabled = true;
        }
        if (agente.enabled)
        {
            agente.Warp(punto);
            if (agente.isOnNavMesh) agente.ResetPath();
        }
    }

    /// <summary>Un ruido fuerte (disparos) despierta a los zombies que estén dentro del radio.</summary>
    public static void Ruido(Vector3 posicion, float radio)
    {
        float r2 = radio * radio;
        for (int i = Activos.Count - 1; i >= 0; i--)
        {
            var e = Activos[i];
            if (e == null || !e.modoHorda || e.detectado) continue;
            if ((e.transform.position - posicion).sqrMagnitude <= r2) e.Alertar(false);
        }
    }

    /// <summary>Pasa a perseguir. Con contagiar, también despierta a los zombies cercanos.</summary>
    public void Alertar(bool contagiar)
    {
        bool nuevo = !detectado;
        detectado = true;
        if (nuevo) alDetectarObjetivo?.Invoke(objetivo);
        lejosDesde = -1f;
        if (!nuevo || !modoHorda) return;
        proximoVagar = 0f;
        if (Time.time >= proximoGrunido && BancoSonidos.Actual != null && BancoSonidos.Actual.grito != null)
        {
            proximoGrunido = Time.time + 2.5f;
            Sonido.Reproducir(BancoSonidos.Actual.grito, transform.position, .3f, "Efectos", Random.Range(1.25f, 1.6f));
        }
        if (!contagiar) return;
        for (int i = Activos.Count - 1; i >= 0; i--)
        {
            var e = Activos[i];
            if (e == null || e == this || !e.modoHorda || e.detectado) continue;
            if ((e.transform.position - transform.position).sqrMagnitude <= 12f * 12f) e.Alertar(false);
        }
    }

    bool Percibe()
    {
        float d = DistanciaPlana(transform.position, objetivo.position);
        if (radioDeteccion > 0f && d > radioDeteccion) return false;
        if (d <= radioOido) return true;
        Vector3 hacia = objetivo.position - transform.position;
        hacia.y = 0f;
        if (Vector3.Angle(transform.forward, hacia) > anguloVision * .5f) return false;
        if (mascaraVision < 0) mascaraVision = LayerMask.GetMask("Default");
        Vector3 ojos = transform.position + Vector3.up * 1.6f;
        // Basta con ver el pecho o la cabeza: un desnivel o una baranda baja no deben dejarlo ciego.
        return Visible(ojos, objetivo.position + Vector3.up * 1.2f) || Visible(ojos, objetivo.position + Vector3.up * 1.75f);
    }

    bool Visible(Vector3 desde, Vector3 hasta)
    {
        return !Physics.Linecast(desde, hasta, out var tapa, mascaraVision, QueryTriggerInteraction.Ignore)
            || tapa.transform.IsChildOf(objetivo) || tapa.transform.IsChildOf(transform);
    }

    /// <summary>Decisiones lentas (cuatro veces por segundo): percibir, deambular o fijar el destino.</summary>
    void PensarHorda()
    {
        float ahora = Time.time;
        float dt = Mathf.Max(.05f, ahora - medidaPrevia);
        velocidadObjetivo = medidaPrevia > 0f ? (objetivo.position - posicionObjetivoPrevia) / dt : Vector3.zero;
        posicionObjetivoPrevia = objetivo.position;
        medidaPrevia = ahora;
        bool listo = agente != null && agente.enabled && agente.isOnNavMesh;

        if (!detectado)
        {
            if (Percibe()) { Alertar(true); return; }
            if (!listo || ahora < proximoVagar) return;
            proximoVagar = ahora + Random.Range(3f, 8f);
            Vector2 r = Random.insideUnitCircle * radioVagar;
            if (NavMesh.SamplePosition(hogar + new Vector3(r.x, 0f, r.y), out var paseo, 3f, Filtro()))
                agente.SetDestination(paseo.position);
            return;
        }

        float d = DistanciaPlana(transform.position, objetivo.position);
        if (d > distanciaOlvido)
        {
            if (lejosDesde < 0f) lejosDesde = ahora;
            else if (ahora - lejosDesde > 8f)
            {
                detectado = false;
                lejosDesde = -1f;
                hogar = transform.position;
                return;
            }
        }
        else lejosDesde = -1f;

        if (!listo || Atacando || ahora < aturdidoHasta) return;
        Vector3 meta = objetivo.position;
        if (d > 3.5f)
        {
            // Anticipar hacia dónde corre el jugador y abrirse a un costado: lo rodean en vez de hacer fila.
            float velocidad = Mathf.Max(1f, velocidadCorrer);
            meta += Vector3.ClampMagnitude(GeografiaIsla.Plano(velocidadObjetivo), 6f) * Mathf.Clamp(d / velocidad, 0f, 1.2f) * .6f;
            Vector3 lateral = Vector3.Cross(Vector3.up, GeografiaIsla.Plano(objetivo.position - transform.position).normalized);
            meta += lateral * flanco * Mathf.Clamp(d * .35f, 0f, 5f);
        }
        if (NavMesh.SamplePosition(meta, out var destino, 6f, Filtro())) agente.SetDestination(destino.position);
        else if (NavMesh.SamplePosition(objetivo.position, out destino, alcanceDestino, Filtro())) agente.SetDestination(destino.position);
    }

    /// <summary>Movimiento cuadro a cuadro del zombie: deambular, correr tras el jugador y embestir.</summary>
    void ActuarHorda(float distancia)
    {
        bool listo = agente != null && agente.enabled && agente.isOnNavMesh;
        if (!detectado)
        {
            if (listo)
            {
                agente.isStopped = false;
                agente.speed = velocidadVagar;
                Animar(agente.velocity.magnitude);
            }
            else Animar(0f);
            return;
        }
        if (Atacando)
        {
            if (listo) agente.isStopped = true;
            Encarar(420f);
            // Embestida: se abalanza el último tramo en vez de quedarse plantado.
            if (embistiendo && distancia > .95f)
            {
                Vector3 paso = transform.forward * embestida * Time.deltaTime;
                if (listo) agente.Move(paso);
                else transform.position += paso;
            }
            Animar(0f);
            return;
        }
        // Atascado (el jugador está sobre una roca, un muelle o fuera del NavMesh): alcanza un poco más lejos.
        bool atascado = atascadoDesde >= 0f && Time.time - atascadoDesde > .5f;
        float alcance = rangoAtaque + (atascado ? .8f : 0f);
        if (rangoAtaque > 0f && distancia <= alcance)
        {
            Encarar(420f);
            if (Time.time >= proximoAtaque)
            {
                atascadoDesde = -1f;
                ataque = StartCoroutine(Atacar());
                return;
            }
            if (distancia <= rangoAtaque * .65f || atascado)
            {
                if (listo) agente.isStopped = true;
                Animar(0f);
                return;
            }
        }
        if (listo)
        {
            agente.isStopped = false;
            agente.speed = velocidadCorrer;
            // Si el camino no lo acerca (ruta parcial o trabado contra algo) y el jugador está cerca,
            // deja la ruta y va directo hacia él; Move lo mantiene dentro del NavMesh.
            bool quieto = !agente.pathPending && agente.velocity.sqrMagnitude < .09f;
            if (distancia < 10f && (quieto || agente.pathStatus != NavMeshPathStatus.PathComplete))
            {
                if (atascadoDesde < 0f) atascadoDesde = Time.time;
            }
            else atascadoDesde = -1f;
            if (atascado)
            {
                Encarar(420f);
                agente.Move(transform.forward * velocidadCorrer * Time.deltaTime);
                Animar(velocidadCorrer);
                return;
            }
            Animar(agente.velocity.magnitude);
            return;
        }
        // Sin NavMesh: en línea recta.
        Encarar(240f);
        transform.position += transform.forward * velocidadCorrer * Time.deltaTime;
        Animar(velocidadCorrer);
    }

    void Encarar(float gradosPorSegundo)
    {
        Vector3 cara = objetivo.position - transform.position;
        cara.y = 0f;
        if (cara.sqrMagnitude > 0.04f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(cara), gradosPorSegundo * Time.deltaTime);
    }

    // ---------------------------------------------------------------- Persecución

    IEnumerator Perseguir()
    {
        yield return new WaitForSeconds(retrasoAparicion);
        while (objetivo == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) objetivo = go.transform;
            else yield return null;
        }

        Persiguiendo = true;
        if (usarNavMesh && agente != null)
        {
            agente.enabled = true;
            if (!agente.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 6f, Filtro()))
                agente.Warp(hit.position);
            if (!agente.isOnNavMesh)
            {
                Debug.LogWarning(name + " no quedó sobre el NavMesh; persigue en línea recta.");
                usarNavMesh = false;
                agente.enabled = false;
            }
        }
        hogar = transform.position;

        var espera = new WaitForSeconds(0.25f);
        // Desfase para que toda la horda no piense en el mismo cuadro.
        if (modoHorda) yield return new WaitForSeconds(Random.Range(0f, .25f));
        while (true)
        {
            if (modoHorda)
            {
                PensarHorda();
                yield return espera;
                continue;
            }
            // El destino se lleva al NavMesh de este tipo de agente: el titán va al punto de avenida más cercano.
            bool quieto = (radioDeteccion > 0f && !detectado) || Atacando;
            if (!quieto && usarNavMesh && agente != null && agente.isOnNavMesh
                && NavMesh.SamplePosition(objetivo.position, out var destino, alcanceDestino, Filtro()))
                agente.SetDestination(destino.position);
            yield return espera;
        }
    }

    void Update()
    {
        if (!Persiguiendo || objetivo == null) return;
        var vida = GetComponent<ControladorVida>();
        if (vida != null && vida.Muerto) return;
        if (Time.time < aturdidoHasta)
        {
            if (agente != null && agente.enabled) agente.isStopped = true;
            Animar(0f);
            return;
        }
        float distancia = DistanciaPlana(transform.position, objetivo.position);
        if (modoHorda)
        {
            ActuarHorda(distancia);
            return;
        }
        if (radioDeteccion > 0f && !detectado)
        {
            if (distancia > radioDeteccion)
            {
                Animar(0f);
                return;
            }
            detectado = true;
            alDetectarObjetivo?.Invoke(objetivo);
        }
        if (rangoAtaque > 0f && distancia <= rangoAtaque)
        {
            if (agente != null && agente.enabled) agente.isStopped = true;
            Vector3 cara = objetivo.position - transform.position;
            cara.y = 0f;
            if (cara.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(cara), 240f * Time.deltaTime);
            Animar(0f);
            if (!Atacando && Time.time >= proximoAtaque) ataque = StartCoroutine(Atacar());
            return;
        }
        if (agente != null && agente.enabled) agente.isStopped = false;
        float velocidad = distancia > umbralCorrer ? velocidadCorrer : velocidadCaminar;
        if (usarNavMesh && agente != null && agente.enabled)
        {
            agente.speed = velocidad;
            Animar(agente.velocity.magnitude);
            return;
        }

        Vector3 direccion = objetivo.position - transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude > 0.04f)
        {
            direccion.Normalize();
            transform.position += direccion * velocidad * Time.deltaTime;
            var mirar = Quaternion.LookRotation(direccion, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, mirar, 120f * Time.deltaTime);
        }
        Animar(velocidad);
    }

    /// <summary>Interrumpe el ataque y frena al enemigo, como al encajar un impacto.</summary>
    public void Aturdir(float segundos)
    {
        // Una horda no se detiene con cada bala: el tambaleo dura menos.
        if (modoHorda) segundos *= .6f;
        aturdidoHasta = Mathf.Max(aturdidoHasta, Time.time + segundos);
        if (ataque != null) StopCoroutine(ataque);
        ataque = null;
        if (golpe != null) golpe.FinGolpe();
        Atacando = false;
        embistiendo = false;
        proximoAtaque = Mathf.Max(proximoAtaque, aturdidoHasta);
        if (modoHorda) Alertar(true);
        else detectado = true;
    }

    IEnumerator Atacar()
    {
        Atacando = true;
        alAtacar?.Invoke();
        if (animator != null && TieneParametro("Atacar")) animator.SetTrigger("Atacar");
        var voz = GetComponent<SonidoZombie>();
        if (voz != null) voz.Ataque();
        Sonido.Reproducir(sonidoAtaque != null ? sonidoAtaque : BancoSonidos.Actual != null ? BancoSonidos.Actual.swoosh : null, transform.position, .7f, "Efectos");
        embistiendo = modoHorda;
        golpeAbierto = false;
        var clip = ClipConEventos(animator);
        if (clip != null)
        {
            // El clip abre y cierra el golpe con ActivaArma / DesactivaArma, como Ataque01 del curso.
            // Si el Animator no llegó a reproducirlo (venía de una reacción al impacto, transición
            // cortada...), el evento nunca llega y el zombie quedaba plantado: se pega igual a mitad del clip.
            float mitad = clip.length * .5f;
            yield return new WaitForSeconds(mitad);
            if (!golpeAbierto && golpe != null)
            {
                golpeAbierto = true;
                golpe.Golpear();
                yield return new WaitForSeconds(.2f);
                golpe.FinGolpe();
                mitad += .2f;
            }
            yield return new WaitForSeconds(Mathf.Max(0f, clip.length - mitad));
        }
        else
        {
            yield return new WaitForSeconds(.25f);
            if (golpe != null) golpe.InicioGolpe();
            yield return new WaitForSeconds(.22f);
        }
        // Por si un golpe recibido cortó el clip antes de DesactivaArma.
        if (golpe != null) golpe.FinGolpe();
        embistiendo = false;
        proximoAtaque = Time.time + cadenciaAtaque * (modoHorda ? Random.Range(.85f, 1.15f) : 1f);
        if (modoHorda) yield return new WaitForSeconds(.12f);
        Atacando = false;
    }

    // Eventos de animación del clip de ataque.
    public void ActivaArma()
    {
        if (Atacando && enabled && golpe != null && !golpeAbierto)
        {
            golpeAbierto = true;
            golpe.InicioGolpe();
        }
    }

    public void DesactivaArma()
    {
        if (golpe != null) golpe.FinGolpe();
    }

    /// <summary>Clip del Animator que trae el evento ActivaArma, o null si el golpe va por tiempo.</summary>
    public static AnimationClip ClipConEventos(Animator animador)
    {
        if (animador == null || animador.runtimeAnimatorController == null) return null;
        foreach (var clip in animador.runtimeAnimatorController.animationClips)
            if (clip != null)
                foreach (var evento in clip.events)
                    if (evento.functionName == "ActivaArma") return clip;
        return null;
    }

    void Animar(float velocidad)
    {
        // Velocidad equivalente a un humano. Por debajo del clip Walk el blend cae a idle y el cuerpo se desliza.
        float humano = velocidad / escalaLocomocion;
        bool lento = humano > 0.05f && humano < 2f;
        if (TieneParametro("Speed")) animator.SetFloat("Speed", lento ? 2f : humano);
        if (TieneParametro("MotionSpeed")) animator.SetFloat("MotionSpeed", lento ? humano / 2f : 1f);
        if (TieneParametro("Grounded")) animator.SetBool("Grounded", true);
    }

    bool TieneParametro(string nombre)
    {
        if (animator == null) return false;
        foreach (var parametro in animator.parameters)
            if (parametro.name == nombre) return true;
        return false;
    }

    NavMeshQueryFilter Filtro() => new NavMeshQueryFilter { agentTypeID = agente.agentTypeID, areaMask = NavMesh.AllAreas };

    float Altura()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 1.8f;
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds.size.y;
    }

    static float DistanciaPlana(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void OnFootstep(AnimationEvent animationEvent)
    {
        if (pisadaPesada == null || animationEvent.animatorClipInfo.weight <= 0.5f) return;
        Sonido.Reproducir(pisadaPesada, transform.position, volumenPisada, "Efectos");
    }

    public void OnLand(AnimationEvent animationEvent) { }
}
