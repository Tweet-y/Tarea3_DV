using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

/// <summary>Presentacion de CJ en la lancha y salida al mar antes de entregar el control al jugador.</summary>
[DefaultExecutionOrder(-300)]
public sealed class IntroduccionIsla : MonoBehaviour
{
    public const string Dialogo = "Hoy nos tenemos que curar, ve y consigue todo el alcohol que haya en esta isla, te espero al otro lado";
    private static IntroduccionIsla instancia;
    public static bool Extra { get; set; }
    public static bool Activa => (instancia != null && instancia.enCurso) || Extra;

    [SerializeField] private Transform cj = null;
    [SerializeField] private Transform lancha = null;
    [SerializeField, Min(1f)] private float segundosLectura = 4.5f;
    [SerializeField, Min(1f)] private float segundosSalida = 5f;
    [SerializeField, Min(1f)] private float velocidadLancha = 12f;
    private bool enCurso, restaurada;
    private Camera camara;
    private CinemachineBrain brain;
    private bool brainActivo;
    private Vector3 posicionCamara;
    private Quaternion rotacionCamara;
    private float fov;
    private ControladorDialogo dialogo;
    private readonly List<(Behaviour componente, bool activo)> suspendidos = new List<(Behaviour, bool)>();
    private StarterAssets.StarterAssetsInputs entradas;
    private bool miradaActiva;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() => instancia = null;

    private void Awake()
    {
        instancia = this;
        enCurso = true;
        ResolverReferencias();
    }

    [ContextMenu("Vincular CJ y SM_Veh_Boat_01 (1)")]
    private void VincularReferencias() => ResolverReferencias();

    public bool ResolverReferencias()
    {
        // Reenlazar tambien instancias que Unity conserve abiertas sin las referencias serializadas.
        // Buscar exclusivamente en esta escena, incluidos los hijos de DemoScene y de la lancha.
        foreach (var raiz in gameObject.scene.GetRootGameObjects())
            foreach (var objeto in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (cj == null && objeto.name == "CJ") cj = objeto;
                if (lancha == null && objeto.name == "SM_Veh_Boat_01 (1)") lancha = objeto;
            }
        if (cj != null)
        {
            // CJ es un pasajero: no debe competir con curao_principal por el tag Player ni por el input.
            cj.gameObject.tag = "Untagged";
            var controlador = cj.GetComponent<StarterAssets.ThirdPersonController>();
            if (controlador != null) controlador.enabled = false;
            var input = cj.GetComponent<PlayerInput>();
            if (input != null) input.enabled = false;
            var capsula = cj.GetComponent<CharacterController>();
            if (capsula != null) capsula.enabled = false;
        }
        return cj != null && lancha != null;
    }

    private IEnumerator Start()
    {
        if (!ResolverReferencias())
        {
            yield return null;
            if (!ResolverReferencias())
            {
                enCurso = false;
                Debug.LogError($"IntroduccionIsla: no se encontró {(cj == null ? "CJ" : "SM_Veh_Boat_01 (1)")} en {gameObject.scene.name}.", this);
                yield break;
            }
        }
        var jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador == null) { enCurso = false; yield break; }
        try
        {
            Suspender(jugador.GetComponent<StarterAssets.ThirdPersonController>());
            Suspender(jugador.GetComponent<PlayerInput>());
            entradas = jugador.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (entradas != null)
            {
                miradaActiva = entradas.cursorInputForLook;
                entradas.cursorInputForLook = false;
                LimpiarEntradas();
            }
            foreach (var enemigo in FindObjectsByType<EnemigoAcechador>(FindObjectsSortMode.None))
            {
                Suspender(enemigo.GetComponent<NavMeshAgent>());
                Suspender(enemigo);
            }
            cj.SetParent(lancha, true);
            dialogo = ControladorDialogo.Crear(true);
            // Dejar que Cinemachine y los inicializadores de la escena terminen su primer frame.
            yield return null;
            camara = Camera.main;
            if (camara == null) { Debug.LogError("La introduccion necesita una MainCamera activa."); yield break; }
            brain = camara.GetComponent<CinemachineBrain>();
            brainActivo = brain != null && brain.enabled;
            if (brain != null) brain.enabled = false;
            posicionCamara = camara.transform.position;
            rotacionCamara = camara.transform.rotation;
            fov = camara.fieldOfView;
            Vector3 foco = cj.position + Vector3.up * 1.35f;
            Vector3 plano = foco + cj.forward * 3.8f + cj.right * 1.5f + Vector3.up * .45f;
            camara.transform.SetPositionAndRotation(plano, Quaternion.LookRotation(foco - plano));
            camara.fieldOfView = 48f;
            for (float t = 0; t < .3f; t += Time.deltaTime)
            {
                dialogo.Negro.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(t / .3f));
                yield return null;
            }
            dialogo.Negro.color = Color.clear;
            dialogo.FijarNombre("CJ");
            yield return dialogo.StartCoroutine(dialogo.Revelar(Dialogo, segundosLectura));
            dialogo.Ocultar();
            Vector3 salida = Vector3.ProjectOnPlane(lancha.position - jugador.transform.position, Vector3.up).normalized;
            if (salida.sqrMagnitude < .1f) salida = lancha.forward;
            Quaternion rumboInicial = lancha.rotation;
            Quaternion rumboFinal = Quaternion.LookRotation(salida, Vector3.up);
            float tiempo = 0f;
            for (; tiempo < segundosSalida; tiempo += Time.deltaTime)
            {
                MoverLancha(tiempo, salida, rumboInicial, rumboFinal);
                Vector3 mirar = cj.position + Vector3.up * 1.1f;
                camara.transform.position = Vector3.Lerp(plano, plano + Vector3.up * 1.6f, Mathf.Clamp01(tiempo / segundosSalida));
                camara.transform.rotation = Quaternion.LookRotation(mirar - camara.transform.position);
                yield return null;
            }
            Vector3 desde = camara.transform.position;
            Quaternion giroDesde = camara.transform.rotation;
            for (float t = 0; t < .55f; t += Time.deltaTime)
            {
                MoverLancha(tiempo, salida, rumboInicial, rumboFinal);
                tiempo += Time.deltaTime;
                float mezcla = Mathf.SmoothStep(0f, 1f, t / .55f);
                camara.transform.position = Vector3.Lerp(desde, posicionCamara, mezcla);
                camara.transform.rotation = Quaternion.Slerp(giroDesde, rotacionCamara, mezcla);
                camara.fieldOfView = Mathf.Lerp(48f, fov, mezcla);
                yield return null;
            }
            Restaurar();
            // La lancha sigue alejandose despues de devolver el control; CJ permanece como pasajero.
            while (lancha != null && tiempo < 18f)
            {
                MoverLancha(tiempo, salida, rumboInicial, rumboFinal);
                tiempo += Time.deltaTime;
                yield return null;
            }
            if (lancha != null) lancha.gameObject.SetActive(false);
        }
        finally { Restaurar(); }
    }

    private void MoverLancha(float tiempo, Vector3 direccion, Quaternion inicial, Quaternion final)
    {
        if (lancha == null) return;
        lancha.rotation = Quaternion.Slerp(inicial, final, Mathf.SmoothStep(0, 1, Mathf.Clamp01(tiempo / 1.2f)));
        lancha.position += direccion * (velocidadLancha * Mathf.SmoothStep(0, 1, Mathf.Clamp01(tiempo / 1.5f)) * Time.deltaTime);
    }

    private void Suspender(Behaviour componente)
    {
        if (componente == null) return;
        suspendidos.Add((componente, componente.enabled));
        componente.enabled = false;
    }

    private void LimpiarEntradas()
    {
        if (entradas == null) return;
        entradas.move = Vector2.zero;
        entradas.look = Vector2.zero;
        entradas.jump = false;
        entradas.sprint = false;
    }

    private void Restaurar()
    {
        if (restaurada) return;
        restaurada = true;
        enCurso = false;
        if (camara != null)
        {
            camara.transform.SetPositionAndRotation(posicionCamara, rotacionCamara);
            camara.fieldOfView = fov;
        }
        if (brain != null) brain.enabled = brainActivo;
        LimpiarEntradas();
        if (entradas != null) entradas.cursorInputForLook = miradaActiva;
        foreach (var estado in suspendidos)
            if (estado.componente != null) estado.componente.enabled = estado.activo;
        suspendidos.Clear();
        if (dialogo != null) dialogo.Cerrar();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        Restaurar();
        if (instancia == this) instancia = null;
    }
}
