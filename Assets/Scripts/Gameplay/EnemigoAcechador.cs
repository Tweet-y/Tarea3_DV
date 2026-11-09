using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Persigue al jugador con NavMeshAgent, como MovimientoSimpleNPC del curso.
/// Nemesis usa el agente Humanoid; el titán usa su propio tipo de agente ("Titan").
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

    public bool Persiguiendo { get; private set; }
    public bool Atacando { get; private set; }
    bool detectado;
    float proximoAtaque, aturdidoHasta;
    Coroutine ataque;

    Animator animator;
    NavMeshAgent agente;
    float escalaLocomocion = 1f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agente = GetComponent<NavMeshAgent>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (TieneParametro("Grounded")) animator.SetBool("Grounded", true);
        if (TieneParametro("Speed")) animator.SetFloat("Speed", 0f);
        if (TieneParametro("MotionSpeed")) animator.SetFloat("MotionSpeed", 1f);
        escalaLocomocion = Mathf.Max(1f, Altura() / 1.8f);
        if (agente != null)
            agente.enabled = false;
    }

    void OnEnable()
    {
        StartCoroutine(Perseguir());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        Persiguiendo = false;
    }

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

        var espera = new WaitForSeconds(0.25f);
        while (true)
        {
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
        if (radioDeteccion > 0f && !detectado)
        {
            if (distancia > radioDeteccion)
            {
                Animar(0f);
                return;
            }
            detectado = true;
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
        aturdidoHasta = Mathf.Max(aturdidoHasta, Time.time + segundos);
        if (ataque != null) StopCoroutine(ataque);
        ataque = null;
        if (golpe != null) golpe.FinGolpe();
        Atacando = false;
        proximoAtaque = Mathf.Max(proximoAtaque, aturdidoHasta);
        detectado = true;
    }

    IEnumerator Atacar()
    {
        Atacando = true;
        if (animator != null && TieneParametro("Atacar")) animator.SetTrigger("Atacar");
        Sonido.Reproducir(sonidoAtaque != null ? sonidoAtaque : BancoSonidos.Actual != null ? BancoSonidos.Actual.swoosh : null, transform.position, .7f, "Efectos");
        yield return new WaitForSeconds(.25f);
        if (golpe != null) golpe.InicioGolpe();
        yield return new WaitForSeconds(.22f);
        if (golpe != null) golpe.FinGolpe();
        proximoAtaque = Time.time + cadenciaAtaque;
        Atacando = false;
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
