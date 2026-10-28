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

    public bool Persiguiendo { get; private set; }

    Animator animator;
    NavMeshAgent agente;
    float escalaLocomocion = 1f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agente = GetComponent<NavMeshAgent>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.SetBool("Grounded", true);
        animator.SetFloat("Speed", 0f);
        animator.SetFloat("MotionSpeed", 1f);
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
            if (usarNavMesh && agente != null && agente.isOnNavMesh
                && NavMesh.SamplePosition(objetivo.position, out var destino, alcanceDestino, Filtro()))
                agente.SetDestination(destino.position);
            yield return espera;
        }
    }

    void Update()
    {
        if (!Persiguiendo || objetivo == null) return;
        float distancia = DistanciaPlana(transform.position, objetivo.position);
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

    void Animar(float velocidad)
    {
        // Velocidad equivalente a un humano. Por debajo del clip Walk el blend cae a idle y el cuerpo se desliza.
        float humano = velocidad / escalaLocomocion;
        bool lento = humano > 0.05f && humano < 2f;
        animator.SetFloat("Speed", lento ? 2f : humano);
        animator.SetFloat("MotionSpeed", lento ? humano / 2f : 1f);
        animator.SetBool("Grounded", true);
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
        AudioSource.PlayClipAtPoint(pisadaPesada, transform.position, volumenPisada);
    }

    public void OnLand(AnimationEvent animationEvent) { }
}
