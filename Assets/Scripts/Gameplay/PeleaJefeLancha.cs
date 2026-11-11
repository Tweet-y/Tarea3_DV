using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Final del juego: al subir a la lancha de CJ en muelle_final aparece Nemesis gigante en la orilla.
/// CJ conduce la lancha en circuito mientras el jugador usa la .50 montada en primera persona;
/// Nemesis lanza rocas que hay que reventar en el aire. Al morir, CJ y el protagonista se van
/// con la lancha llena de botellas hacia el horizonte. Todo se construye en tiempo de ejecucion.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed partial class PeleaJefeLancha : MonoBehaviour
{
    public const string NombreMuelle = "muelle_final";
    public static PeleaJefeLancha Instancia { get; private set; }
    /// <summary>Atajo del menu: empieza directamente en la pelea.</summary>
    public static bool ModoPrueba;
    /// <summary>La barra de vida de Nemesis solo se muestra desde su aparicion.</summary>
    public static bool BarraVisible { get; private set; }
    public static bool Activa => Instancia != null && Instancia.fase != Fase.Espera;

    enum Fase { Espera, Intro, Combate, Final }
    Fase fase = Fase.Espera;

    // Balance
    public const string NombreSpawn = "SPAWN_NEMESIS";
    const float VidaNemesis = 4800f, DanioBala = 22f, Cadencia = 8f, AlcanceBala = 1200f, VelocidadLancha = 13f;
    const float CalorPorDisparo = .042f, Enfriamiento = .45f, DanioRocaEnMano = 160f;

    ControladorPartidaBotellas partida;
    Transform lancha, cj;
    GameObject jugador;
    Renderer[] renderersJugador;
    ControladorVida nemesis;
    Animator animNemesis;
    Transform manoNemesis;
    GameObject plantillaBotella;

    Camera camara;
    CinemachineBrain brain;

    // Lancha y ruta alrededor de toda la isla
    Vector3 muelleLancha, rumbo;
    float baseY, tCombate, largoRuta, inicioRuta, radioMedio = 80f;
    readonly List<Vector3> ruta = new List<Vector3>();
    readonly List<float> acumulado = new List<float>();
    const float Arranque = 5f;
    ConductorLancha conductor;
    CatalogoObjetosIsla catalogo;

    // Torreta
    Transform torreta, pivote, arma;
    Vector3 bocaLocal, camaraLocal, armaBase;
    AudioSource audioArma;
    AudioClip clipDisparo;
    Light fogonazo;
    float yaw, pitch, zoom, calor, retroceso, sacudida, proximoDisparo, fogonazoHasta;
    bool sobrecalentada;

    // Nemesis
    Vector3 posNemesis;
    float altura = 45f;
    int faseJefe = 1;
    bool lanzando;
    float proximoLanzamiento;
    readonly List<RocaNemesis> rocas = new List<RocaNemesis>();
    Material materialRoca;

    // HUD
    Canvas canvas;
    TMP_Text mira, marcaGolpe, etiquetaCalor, mensaje, lineaCJ, ayuda;
    GameObject radioCJ;
    CanvasGroup grupoAyuda;
    RectTransform rellenoCalor;
    Image rellenoCalorImg, danioPantalla;
    readonly List<RectTransform> marcadores = new List<RectTransform>();
    float mensajeHasta, lineaHasta, golpeHasta, danioHasta;
    static Sprite anillo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        Instancia = null;
        BarraVisible = false;
        ModoPrueba = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar() => SceneManager.sceneLoaded += AlCargar;

    static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        BarraVisible = false;
        if (escena.name != ControladorMenus.EscenaJuego) return;
        new GameObject("PeleaJefeLancha").AddComponent<PeleaJefeLancha>();
    }

    // sceneLoaded corre antes de los Start de la escena: aqui se esconde el Nemesis de la isla
    // y se suelta su botella antes de que ObjetoEspecialColeccionable guarde su posicion inicial.
    void Awake()
    {
        Instancia = this;
        GeografiaIsla.Invalidar();
        Vector3 suma = Vector3.zero;
        int cuenta = 0;
        ObjetoEspecialColeccionable modelo = null;
        foreach (var b in FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.tipo != ObjetoEspecialColeccionable.TipoBotella.Objetivo) continue;
            if (b.GetComponentInParent<PortadorBotella>(true) == null)
            {
                suma += b.transform.position;
                cuenta++;
                if (modelo == null) modelo = b;
            }
        }
        GeografiaIsla.Centro = cuenta > 0 ? suma / cuenta : Vector3.zero;
        if (modelo != null) plantillaBotella = CrearPlantilla(modelo);

        foreach (var vida in FindObjectsByType<ControladorVida>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (vida.esJefe) { nemesis = vida; break; }
        if (nemesis != null)
        {
            var portador = nemesis.GetComponent<PortadorBotella>();
            if (portador != null && portador.botella != null)
            {
                LiberarBotella(portador.botella, nemesis.transform.position, modelo);
                portador.botella = null;
            }
            nemesis.gameObject.SetActive(false);
        }
        foreach (var cine in FindObjectsByType<CinematicaJefe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            cine.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
        BarraVisible = false;
        if (materialRoca != null) Destroy(materialRoca);
    }

    IEnumerator Start()
    {
        yield return null;
        yield return null;
        if (!ModoPrueba) yield break;
        partida = ControladorPartidaBotellas.Instancia;
        if (partida != null) partida.CompletarParaPrueba();
        var final = FinalIsla.Instancia;
        if (final != null && !FinalIsla.FuegosEncendidos) final.Encender();
        yield return null;
        Iniciar();
    }

    static GameObject CrearPlantilla(ObjetoEspecialColeccionable fuente)
    {
        var go = fuente.gameObject;
        bool activo = go.activeSelf;
        go.SetActive(false);
        var copia = Instantiate(go);
        go.SetActive(activo);
        copia.name = "PlantillaBotella";
        // Primero los scripts (pueden exigir otros componentes), despues el resto.
        foreach (var c in copia.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);
        foreach (var c in copia.GetComponentsInChildren<Component>(true))
        {
            if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer) continue;
            DestroyImmediate(c);
        }
        return copia;
    }

    static void LiberarBotella(ObjetoEspecialColeccionable botella, Vector3 donde, ObjetoEspecialColeccionable referencia)
    {
        botella.transform.SetParent(null, true);
        if (referencia != null)
        {
            float alto = GeografiaIsla.Limites(botella.transform).size.y;
            float objetivo = GeografiaIsla.Limites(referencia.transform).size.y;
            if (alto > .001f && objetivo > .001f) botella.transform.localScale *= objetivo / alto;
        }
        botella.transform.rotation = Quaternion.identity;
        botella.transform.position = GeografiaIsla.Suelo(donde + Vector3.up * 2f, 4f) + Vector3.up * 1.1f;
        botella.gameObject.SetActive(true);
        foreach (var col in botella.GetComponentsInChildren<Collider>(true)) col.enabled = true;
    }

    /// <summary>Lleva la lancha al agua junto a muelle_final y el punto de abordaje al borde del muelle.</summary>
    public static bool UbicarEnMuelle(Transform lancha, Transform puntoExtraccion, Transform cj)
    {
        var muelle = GeografiaIsla.BuscarPorNombre(NombreMuelle);
        if (muelle == null || lancha == null)
        {
            Debug.LogWarning("No se encontró '" + NombreMuelle + "'; la lancha queda en el punto de extracción.");
            return false;
        }
        GeografiaIsla.Ignorados.Clear();
        GeografiaIsla.Ignorados.Add(lancha);
        if (cj != null) GeografiaIsla.Ignorados.Add(cj);
        var jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador != null) GeografiaIsla.Ignorados.Add(jugador.transform);
        Bounds b = GeografiaIsla.Limites(muelle);
        Vector3 fuera = GeografiaIsla.Plano(b.center - GeografiaIsla.Centro);
        if (fuera.sqrMagnitude < .01f) fuera = GeografiaIsla.Plano(muelle.forward);
        fuera.Normalize();
        float y = lancha.position.y;
        Vector3 lugar = b.center + fuera * (Mathf.Max(b.extents.x, b.extents.z) + 4f);
        bool hallado = false;
        // Buscar agua libre alrededor del muelle, primero hacia afuera y luego a los lados.
        Vector3 lado = Vector3.Cross(Vector3.up, fuera);
        foreach (var dir in new[] { fuera, (fuera + lado).normalized, (fuera - lado).normalized, lado, -lado })
        {
            for (float d = 2f; d <= 80f && !hallado; d += 1.5f)
            {
                Vector3 p = b.center + dir * d;
                if (!GeografiaIsla.EsAguaAmplia(p, 3.5f)) continue;
                lugar = p;
                hallado = true;
            }
            if (hallado) break;
        }
        lugar.y = y;
        lancha.gameObject.SetActive(true);
        lancha.position = lugar;
        Vector3 haciaMuelle = GeografiaIsla.Plano(b.center - lugar);
        Vector3 costado = Vector3.Cross(Vector3.up, haciaMuelle.sqrMagnitude > .01f ? haciaMuelle.normalized : fuera);
        lancha.rotation = Quaternion.LookRotation(costado, Vector3.up);

        if (puntoExtraccion != null)
        {
            Vector3 borde = b.ClosestPoint(lugar);
            borde.y = b.max.y;
            puntoExtraccion.position = GeografiaIsla.Suelo(borde, 3f);
            puntoExtraccion.rotation = Quaternion.LookRotation(GeografiaIsla.Plano(lugar - borde).sqrMagnitude > .01f ? GeografiaIsla.Plano(lugar - borde) : fuera);
        }
        // Tocar la lancha tambien cuenta como subir.
        if (lancha.Find("SubirLancha") == null)
        {
            var subir = new GameObject("SubirLancha");
            subir.transform.SetParent(lancha, false);
            var caja = subir.AddComponent<BoxCollider>();
            caja.isTrigger = true;
            Bounds bl = GeografiaIsla.Limites(lancha, cj);
            caja.center = lancha.InverseTransformPoint(bl.center);
            caja.size = new Vector3(bl.size.x + 3f, bl.size.y + 3f, bl.size.z + 3f);
            subir.AddComponent<DisparadorBote>();
        }
        return true;
    }

    public void Iniciar()
    {
        if (fase != Fase.Espera) return;
        var final = FinalIsla.Instancia;
        partida = ControladorPartidaBotellas.Instancia;
        jugador = GameObject.FindGameObjectWithTag("Player");
        if (final == null || final.lancha == null || jugador == null || nemesis == null || partida == null)
        {
            Debug.LogError("PeleaJefeLancha: faltan la lancha, el jugador, Nemesis o la partida.");
            if (partida != null) partida.FinalizarVictoria();
            return;
        }
        lancha = final.lancha;
        cj = final.cj;
        StartCoroutine(Secuencia());
    }

    IEnumerator Secuencia()
    {
        fase = Fase.Intro;
        IntroduccionIsla.Extra = true;
        PrepararJugador();
        PrepararCamara();
        DesactivarZombis();
        ConstruirTorreta();
        conductor = ConductorLancha.Instalar(cj, lancha);
        catalogo = Resources.Load<CatalogoObjetosIsla>("Armas/CatalogoObjetosIsla");
        UbicarSpawn();
        CalcularRuta();
        PrepararNemesis();
        CrearEstela();
        yield return Intro();

        BarraVisible = true;
        partida.Curar(1f);
        CrearHUD();
        fase = Fase.Combate;
        IntroduccionIsla.Extra = false;
        proximoLanzamiento = 3f;
        Mensaje("¡REVIENTA LO QUE TE LANCE ANTES DE QUE CAIGA!", 3f);
        while (!nemesis.Muerto && !hundida && partida.Estado == ControladorPartidaBotellas.Resultado.EnCurso) yield return null;
        if (hundida) { yield return Hundimiento(); yield break; }
        if (partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso) yield break;

        fase = Fase.Final;
        IntroduccionIsla.Extra = true;
        foreach (var r in rocas.ToArray())
            if (r != null) RocaDestruida(r, false);
        rocas.Clear();
        if (canvas != null) canvas.gameObject.SetActive(false);
        yield return Final();
        IntroduccionIsla.Extra = false;
        BarraVisible = false;
        partida.FinalizarVictoria();
    }

    // ---------------------------------------------------------------- Preparacion

    void PrepararJugador()
    {
        Apagar(jugador.GetComponent<StarterAssets.ThirdPersonController>());
        Apagar(jugador.GetComponent<PlayerInput>());
        Apagar(jugador.GetComponent<AtaqueMeleeJugador>());
        Apagar(jugador.GetComponent<ControladorArmaFuego>());
        Apagar(jugador.GetComponent<DanioCaida>());
        var capsula = jugador.GetComponent<CharacterController>();
        if (capsula != null) capsula.enabled = false;
        var entradas = jugador.GetComponent<StarterAssets.StarterAssetsInputs>();
        if (entradas != null)
        {
            entradas.MoveInput(Vector2.zero);
            entradas.LookInput(Vector2.zero);
            entradas.SprintInput(false);
            entradas.JumpInput(false);
            entradas.cursorInputForLook = false;
        }
        renderersJugador = jugador.GetComponentsInChildren<Renderer>(true);
        MostrarJugador(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static void Apagar(Behaviour b)
    {
        if (b != null) b.enabled = false;
    }

    void MostrarJugador(bool visible)
    {
        if (renderersJugador == null) return;
        foreach (var r in renderersJugador)
            if (r != null && !(r is ParticleSystemRenderer)) r.enabled = visible;
    }

    void PrepararCamara()
    {
        camara = Camera.main;
        brain = camara != null ? camara.GetComponent<CinemachineBrain>() : null;
        if (brain != null) brain.enabled = false;
        if (camara != null) camara.nearClipPlane = .05f;
    }

    void DesactivarZombis()
    {
        foreach (var e in FindObjectsByType<EnemigoAcechador>(FindObjectsSortMode.None))
            if (nemesis == null || e.gameObject != nemesis.gameObject) e.gameObject.SetActive(false);
    }

    void ConstruirTorreta()
    {
        baseY = lancha.position.y;
        rumbo = GeografiaIsla.Plano(lancha.forward).normalized;
        // Casco en espacio local, sin CJ ni el jugador.
        Bounds mundo = GeografiaIsla.Limites(lancha, cj, jugador.transform);
        Vector3 centro = lancha.InverseTransformPoint(mundo.center);
        Vector3 tam = new Vector3(
            Vector3.Scale(mundo.size, Abs(lancha.right)).magnitude,
            mundo.size.y,
            Vector3.Scale(mundo.size, Abs(lancha.forward)).magnitude);
        Vector3 local = new Vector3(centro.x, 0f, centro.z + tam.z * .3f);
        Vector3 arriba = lancha.TransformPoint(local) + Vector3.up * (mundo.size.y + 3f);
        float cubierta = mundo.min.y + mundo.size.y * .4f;
        float mejor = float.PositiveInfinity;
        foreach (var col in lancha.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || (cj != null && col.transform.IsChildOf(cj))) continue;
            if (col.Raycast(new Ray(arriba, Vector3.down), out var hit, mundo.size.y + 6f) && hit.distance < mejor)
            {
                mejor = hit.distance;
                cubierta = hit.point.y;
            }
        }
        const float alturaArma = 1.25f;
        torreta = new GameObject("Torreta50").transform;
        torreta.SetParent(lancha, false);
        torreta.localScale = Vector3.one / Mathf.Max(.001f, lancha.lossyScale.x);
        torreta.localPosition = local;
        torreta.position = new Vector3(torreta.position.x, cubierta + alturaArma, torreta.position.z);
        torreta.rotation = lancha.rotation;

        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "Pedestal";
        Destroy(pedestal.GetComponent<Collider>());
        pedestal.transform.SetParent(torreta, false);
        pedestal.transform.localScale = new Vector3(.16f, alturaArma * .5f, .16f);
        pedestal.transform.localPosition = new Vector3(0f, -alturaArma * .5f, 0f);
        var metal = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MetalTorreta" };
        metal.SetColor("_BaseColor", new Color(.16f, .17f, .18f));
        pedestal.GetComponent<Renderer>().sharedMaterial = metal;

        pivote = new GameObject("Pivote").transform;
        pivote.SetParent(torreta, false);
        pivote.rotation = Quaternion.identity; // Alineado con el mundo para medir el arma con precision.

        var prefab = Resources.Load<GameObject>("Armas/50cal_bossfight");
        if (prefab != null)
        {
            arma = Instantiate(prefab, pivote).transform;
            arma.name = "Arma50";
            arma.localRotation = Quaternion.identity;
            arma.localPosition = Vector3.zero;
            foreach (var c in arma.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var rb in arma.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            Bounds ba = BoundsLocal(arma, pivote);
            float escala = 1.6f / Mathf.Max(.1f, ba.size.z);
            arma.localScale *= escala;
            ba = BoundsLocal(arma, pivote);
            arma.localPosition += new Vector3(-ba.center.x, -ba.center.y, .55f - ba.center.z);
            ba = BoundsLocal(arma, pivote);
            bocaLocal = new Vector3(0f, ba.center.y + ba.extents.y * .25f, ba.max.z);
            camaraLocal = new Vector3(0f, ba.max.y + .2f, ba.min.z + .05f);
        }
        else
        {
            Debug.LogError("Falta Resources/Armas/50cal_bossfight.");
            bocaLocal = new Vector3(0f, 0f, 1.2f);
            camaraLocal = new Vector3(0f, .35f, -.2f);
        }
        armaBase = arma != null ? arma.localPosition : Vector3.zero;
        pivote.rotation = torreta.rotation;

        var luzGo = new GameObject("Fogonazo");
        luzGo.transform.SetParent(pivote, false);
        luzGo.transform.localPosition = bocaLocal;
        fogonazo = luzGo.AddComponent<Light>();
        fogonazo.color = new Color(1f, .7f, .3f);
        fogonazo.range = 9f;
        fogonazo.intensity = 5f;
        fogonazo.enabled = false;

        CrearEfectosArma();

        audioArma = gameObject.AddComponent<AudioSource>();
        audioArma.playOnAwake = false;
        audioArma.spatialBlend = 0f;
        audioArma.pitch = .72f;
        Sonido.Asignar(audioArma, "Efectos");
        var rifle = Resources.Load<ArmaFuegoDefinicion>("Armas/RifleAsalto03");
        clipDisparo = rifle != null ? rifle.disparo : null;

        // El jugador queda de pie tras la ametralladora.
        jugador.transform.SetParent(lancha, true);
        jugador.transform.position = new Vector3(torreta.position.x, cubierta, torreta.position.z) - lancha.forward * .7f;
        jugador.transform.rotation = lancha.rotation;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static Bounds BoundsLocal(Transform objeto, Transform espacio)
    {
        bool alguno = false;
        var b = new Bounds();
        foreach (var r in objeto.GetComponentsInChildren<Renderer>())
        {
            var w = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var esquina = new Vector3(i % 2 == 0 ? w.min.x : w.max.x, (i / 2) % 2 == 0 ? w.min.y : w.max.y, i / 4 == 0 ? w.min.z : w.max.z);
                var local = espacio.InverseTransformPoint(esquina);
                if (!alguno) { b = new Bounds(local, Vector3.zero); alguno = true; }
                else b.Encapsulate(local);
            }
        }
        return b;
    }

    void PrepararNemesis()
    {
        var t = nemesis.transform;
        t.SetParent(null, true);
        nemesis.gameObject.SetActive(true);
        Apagar(nemesis.GetComponent<EnemigoAcechador>());
        Apagar(nemesis.GetComponent<NavMeshAgent>());
        animNemesis = nemesis.GetComponent<Animator>();
        if (animNemesis != null)
        {
            animNemesis.applyRootMotion = false;
            animNemesis.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Parametro("Speed", 0f);
            manoNemesis = animNemesis.isHuman ? animNemesis.GetBoneTransform(HumanBodyBones.RightHand) : null;
        }
        // Gigante: proporcional a la isla para que se vea desde toda la vuelta.
        altura = Mathf.Clamp(radioMedio * .4f, 35f, 80f);
        float alto = GeografiaIsla.Limites(t).size.y;
        if (alto > .1f) t.localScale *= altura / alto;
        float escala = Mathf.Max(.001f, t.lossyScale.y);
        var caja = new GameObject("CuerpoNemesis");
        caja.layer = nemesis.gameObject.layer;
        caja.transform.SetParent(t, false);
        var capsula = caja.AddComponent<CapsuleCollider>();
        capsula.isTrigger = true;
        capsula.height = altura / escala;
        capsula.radius = altura * .17f / escala;
        capsula.center = new Vector3(0f, altura * .5f / escala, 0f);
        nemesis.vidaMaxima = VidaNemesis;
        nemesis.segundosDestruir = -1f;
        nemesis.esJefe = true;
        nemesis.triggerMorir = nemesis.triggerGolpe;
        animador = AnimadorNemesis.Instalar(nemesis.gameObject);

        t.position = posNemesis;
        t.rotation = Quaternion.LookRotation(GeografiaIsla.Plano(lancha.position - posNemesis).normalized, Vector3.up);
        nemesis.Reiniciar();
    }

    void UbicarSpawn()
    {
        GeografiaIsla.Ignorados.Clear();
        GeografiaIsla.Ignorados.Add(lancha);
        GeografiaIsla.Ignorados.Add(jugador.transform);
        GeografiaIsla.Ignorados.Add(nemesis.transform);
        if (cj != null) GeografiaIsla.Ignorados.Add(cj);
        var spawn = GeografiaIsla.BuscarPorNombre(NombreSpawn);
        if (spawn == null)
        {
            Debug.LogWarning("No se encontró '" + NombreSpawn + "'; Nemesis surge en el centro de la isla.");
            posNemesis = GeografiaIsla.Suelo(GeografiaIsla.Centro);
            return;
        }
        Bounds b = GeografiaIsla.Limites(spawn);
        Vector3 centro = b.size.sqrMagnitude > .01f ? b.center : spawn.position;
        // Sobre la calle/losa marcada: el gigante surge desde ahi.
        posNemesis = GeografiaIsla.Suelo(new Vector3(centro.x, Mathf.Max(b.max.y, spawn.position.y) + 1f, centro.z), 3f);
    }

    /// <summary>Circuito cerrado por el agua alrededor de toda la isla, centrado en el spawn de Nemesis.</summary>
    void CalcularRuta()
    {
        muelleLancha = lancha.position;
        Vector3 c = GeografiaIsla.Plano(posNemesis);
        const int n = 72;
        const float margen = 16f;
        float alcanceMuelle = GeografiaIsla.Plano(muelleLancha - c).magnitude;
        var radios = new float[n];
        float maximo = Mathf.Max(400f, alcanceMuelle * 3f);
        for (int i = 0; i < n; i++)
        {
            Vector3 dir = Direccion(i, n);
            float r = -1f;
            // La primera franja de agua amplia (dos muestras seguidas) marca la costa en esa direccion.
            for (float d = 10f; d <= maximo; d += 4f)
            {
                Vector3 p = c + dir * d;
                if (!GeografiaIsla.EsAgua(p) || !GeografiaIsla.EsAgua(p + dir * 8f)) continue;
                if (!GeografiaIsla.EsAguaAmplia(p + dir * 4f, 5f)) continue;
                r = d;
                break;
            }
            radios[i] = r;
        }
        // Direcciones sin agua encontrada: usar el mayor radio valido.
        float mayor = alcanceMuelle;
        foreach (var r in radios) mayor = Mathf.Max(mayor, r);
        for (int i = 0; i < n; i++) if (radios[i] < 0f) radios[i] = mayor;
        // Maximo en una ventana (no recortar cabos entre muestras) y luego suavizado.
        var abiertos = new float[n];
        for (int i = 0; i < n; i++)
        {
            float m = 0f;
            for (int k = -3; k <= 3; k++) m = Mathf.Max(m, radios[(i + k + n) % n]);
            abiertos[i] = m + margen;
        }
        var suaves = new float[n];
        for (int i = 0; i < n; i++)
        {
            float suma = 0f;
            for (int k = -2; k <= 2; k++) suma += abiertos[(i + k + n) % n];
            suaves[i] = Mathf.Max(abiertos[i] - margen * .5f, suma / 5f);
        }
        // Empujar hacia afuera cualquier tramo que todavia toque tierra.
        for (int i = 0; i < n; i++)
            for (int intento = 0; intento < 12; intento++)
            {
                Vector3 p = c + Direccion(i, n) * suaves[i];
                Vector3 medio = c + Direccion(i + .5f, n) * (suaves[i] + suaves[(i + 1) % n]) * .5f;
                if (GeografiaIsla.EsAguaAmplia(p, 4f) && GeografiaIsla.EsAgua(medio)) break;
                suaves[i] += 6f;
            }
        float suma2 = 0f;
        foreach (var r in suaves) suma2 += r;
        radioMedio = suma2 / n;

        // Curva cerrada Catmull-Rom muestreada densamente, con longitud acumulada.
        var control = new Vector3[n];
        for (int i = 0; i < n; i++) control[i] = c + Direccion(i, n) * suaves[i];
        ruta.Clear();
        acumulado.Clear();
        const int pasos = 8;
        for (int i = 0; i < n; i++)
            for (int k = 0; k < pasos; k++)
            {
                float u = k / (float)pasos;
                ruta.Add(CatmullRom(control[(i - 1 + n) % n], control[i], control[(i + 1) % n], control[(i + 2) % n], u));
            }
        largoRuta = 0f;
        for (int i = 0; i < ruta.Count; i++)
        {
            acumulado.Add(largoRuta);
            largoRuta += Vector3.Distance(ruta[i], ruta[(i + 1) % ruta.Count]);
        }
        // Entrar al circuito por el punto mas cercano al muelle.
        float menor = float.PositiveInfinity;
        for (int i = 0; i < ruta.Count; i++)
        {
            float d = (ruta[i] - GeografiaIsla.Plano(muelleLancha)).sqrMagnitude;
            if (d >= menor) continue;
            menor = d;
            inicioRuta = acumulado[i];
        }
    }

    static Vector3 Direccion(float i, int n)
    {
        float a = i * Mathf.PI * 2f / n;
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return .5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    Vector3 PuntoRuta(float distancia)
    {
        if (ruta.Count == 0 || largoRuta <= 0f) return muelleLancha;
        distancia = Mathf.Repeat(distancia, largoRuta);
        int bajo = 0, alto = acumulado.Count - 1;
        while (bajo < alto)
        {
            int medio = (bajo + alto + 1) / 2;
            if (acumulado[medio] <= distancia) bajo = medio;
            else alto = medio - 1;
        }
        int sig = (bajo + 1) % ruta.Count;
        float tramo = (sig == 0 ? largoRuta : acumulado[sig]) - acumulado[bajo];
        return Vector3.Lerp(ruta[bajo], ruta[sig], tramo > .0001f ? (distancia - acumulado[bajo]) / tramo : 0f);
    }

    /// <summary>Posicion en el instante t; el futuro se estima con la velocidad actual (motor encendido o no).</summary>
    Vector3 PosicionLancha(float t)
    {
        float extra = Mathf.Max(0f, t - tCombate) * VelocidadLancha * factorMotor * Mathf.Clamp01(t / Arranque);
        Vector3 enRuta = PuntoRuta(inicioRuta + avance + extra);
        Vector3 p = Vector3.Lerp(GeografiaIsla.Plano(muelleLancha), enRuta, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Arranque)));
        p.y = baseY;
        return p;
    }

    void CrearEstela()
    {
        Bounds b = GeografiaIsla.Limites(lancha, cj, jugador.transform, torreta);
        var go = new GameObject("EstelaLancha");
        go.transform.SetParent(lancha, false);
        go.transform.position = new Vector3(b.center.x, GeografiaIsla.NivelAgua + .1f, b.center.z) - lancha.forward * (b.extents.magnitude * .55f);
        go.transform.rotation = Quaternion.LookRotation(Vector3.up);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(.6f, 1.6f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, .55f), new Color(.85f, .93f, 1f, .4f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.rateOverDistance = 5f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 30f;
        forma.radius = .8f;
        EfectosPelea.Desvanecer(ps);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = EfectosPelea.MaterialParticulas;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    // ---------------------------------------------------------------- Cinematicas

    IEnumerator Intro()
    {
        var t = nemesis.transform;
        Vector3 suelo = posNemesis;
        Vector3 haciaLancha = GeografiaIsla.Plano(lancha.position - suelo).normalized;
        Vector3 lado = Vector3.Cross(Vector3.up, haciaLancha);
        t.position = suelo - Vector3.up * altura * .95f;
        var dialogo = ControladorDialogo.Crear(false);

        // Plano 1: desde la lancha, la orilla tiembla y Nemesis emerge.
        Vector3 camPos = lancha.position + haciaLancha * 8f + Vector3.up * 4f + lado * 2.5f;
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, suelo, 1f, "Efectos");
        float polvo = 0f;
        for (float s = 0f; s < 4.5f; s += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - .4f) / 3.8f));
            t.position = Vector3.Lerp(suelo - Vector3.up * altura * .95f, suelo, k);
            polvo -= Time.deltaTime;
            if (polvo <= 0f)
            {
                polvo = .22f;
                EfectosPelea.Rafaga(suelo + Random.insideUnitSphere * altura * .2f, Vector3.up, new Color(.45f, .38f, .3f, .8f), new Color(.6f, .52f, .42f, .7f),
                    25, new Vector2(3f, 12f) * (altura / 20f), new Vector2(3f, 7f) * (altura / 20f), new Vector2(1.5f, 3f), .3f, 50f);
            }
            sacudida = Mathf.Lerp(.9f, .4f, k);
            Vector3 foco = suelo + Vector3.up * altura * .45f;
            Vector3 temblor = Random.insideUnitSphere * sacudida * .25f;
            camara.transform.SetPositionAndRotation(camPos + temblor, Quaternion.LookRotation(foco - camPos));
            camara.fieldOfView = Mathf.Clamp(Mathf.Atan(altura * .8f / Mathf.Max(1f, Vector3.Distance(camPos, foco))) * Mathf.Rad2Deg * 2f, 30f, 60f);
            yield return null;
        }
        t.position = suelo;
        sacudida = 0f;

        // Plano 2: contrapicado al gigante, rugido y amenaza. Desde aqui se ve su barra de vida.
        BarraVisible = true;
        StartCoroutine(Rugido());
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.grito : null, suelo, 1f, "Voces", .55f);
        Vector3 desde = suelo + haciaLancha * altura * .9f + Vector3.up * altura * .08f + lado * altura * .3f;
        Vector3 hasta = desde - haciaLancha * altura * .25f;
        yield return Plano(dialogo, "NEMESIS", "¿Mis botellas...? ¡Voy a hundir esa lancha con la isla entera!", 3f,
            s => (Vector3.Lerp(desde, hasta, s), suelo + Vector3.up * altura * .78f), 42f);

        // Plano 3: CJ al timon.
        Vector3 cabezaCJ = (cj != null ? cj.position : lancha.position) + Vector3.up * 1.55f;
        Vector3 frenteCJ = cj != null ? cj.forward : lancha.forward;
        Vector3 ladoCJ = cj != null ? cj.right : lancha.right;
        yield return Plano(dialogo, "CJ", "¡Chucha, se vino el grandote! Yo manejo y le damos la vuelta a la isla. ¡Tú agárrate de la punto cincuenta y revienta toda la wea que nos tire!", 4f,
            s => (cabezaCJ + frenteCJ * 2.4f + ladoCJ * (.7f - .3f * s) + Vector3.up * .15f, cabezaCJ), 45f);
        dialogo.Cerrar();

        // Transicion a primera persona apuntando a Nemesis.
        Vector3 objetivo = suelo + Vector3.up * altura * .6f;
        var mirar = Quaternion.LookRotation(objetivo - torreta.position);
        yaw = mirar.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, mirar.eulerAngles.x);
        Vector3 p0 = camara.transform.position;
        Quaternion r0 = camara.transform.rotation;
        for (float s = 0f; s < .7f; s += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, s / .7f);
            pivote.rotation = Quaternion.Euler(pitch, yaw, 0f);
            camara.transform.position = Vector3.Lerp(p0, pivote.TransformPoint(camaraLocal), k);
            camara.transform.rotation = Quaternion.Slerp(r0, pivote.rotation, k);
            camara.fieldOfView = Mathf.Lerp(45f, 62f, k);
            yield return null;
        }
    }

    IEnumerator Plano(ControladorDialogo dialogo, string nombre, string linea, float lectura,
        System.Func<float, (Vector3 pos, Vector3 foco)> camino, float fov)
    {
        dialogo.FijarNombre(nombre);
        bool hecho = false;
        dialogo.StartCoroutine(Envolver(dialogo.Revelar(linea, lectura), () => hecho = true));
        float s = 0f;
        float largo = lectura + 1.5f;
        while (!hecho)
        {
            s += Time.deltaTime;
            var (pos, foco) = camino(Mathf.Clamp01(s / largo));
            camara.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(foco - pos));
            camara.fieldOfView = fov;
            yield return null;
        }
        dialogo.Ocultar();
    }

    static IEnumerator Envolver(IEnumerator rutina, System.Action alTerminar)
    {
        yield return rutina;
        alTerminar();
    }

    IEnumerator Final()
    {
        yield return MuerteNemesis();

        // La lancha se llena con las botellas y aparece el protagonista junto a CJ.
        LlenarBote(partida.Total > 0 ? partida.Total : 30);
        if (arma != null) pivote.rotation = torreta.rotation;
        Vector3 junto = cj != null ? cj.position + lancha.right * .9f - lancha.forward * .2f : torreta.position - Vector3.up * 1.25f;
        jugador.transform.SetPositionAndRotation(junto, lancha.rotation);
        MostrarJugador(true);
        var animJugador = jugador.GetComponentInChildren<Animator>();
        if (animJugador != null)
            foreach (var p in animJugador.parameters)
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) animJugador.SetFloat("Speed", 0f);

        var dialogo = ControladorDialogo.Crear(true);
        dialogo.Negro.color = Color.clear;
        Vector3 salida = GeografiaIsla.Plano(lancha.position - GeografiaIsla.Centro);
        if (salida.sqrMagnitude < .01f) salida = lancha.forward;
        salida.Normalize();
        Quaternion giroInicial = lancha.rotation;
        Quaternion giroFinal = Quaternion.LookRotation(salida, Vector3.up);
        float velocidad = 0f;
        float reloj = 0f;

        // Plano lateral: la carga de botellas, CJ y el protagonista.
        System.Func<(Vector3, Vector3)> lateral = () =>
        {
            Vector3 personajes = cj != null ? (cj.position + jugador.transform.position) * .5f : lancha.position;
            Vector3 foco = personajes + Vector3.up * 1.2f - lancha.forward * .6f;
            return (personajes + lancha.forward * 5f + lancha.right * 3.2f + Vector3.up * 2.3f, foco);
        };
        bool hecho = false;
        dialogo.FijarNombre("CJ");
        dialogo.StartCoroutine(Envolver(dialogo.Revelar("¡Lo botaste, weón! Treinta botellas y un gigante menos... ¡Ahora sí que nos vamos a curar como la gente!", 3.5f), () => hecho = true));
        while (!hecho || reloj < 3f)
        {
            reloj += Time.deltaTime;
            Navegar(ref velocidad, reloj, giroInicial, giroFinal, salida, 6f);
            var (pos, foco) = lateral();
            camara.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(foco - pos));
            camara.fieldOfView = 50f;
            yield return null;
        }
        dialogo.Ocultar();

        // Plano fijo: la lancha se aleja hacia el horizonte, al atardecer.
        Vector3 fija = lancha.position - salida * 10f + Vector3.up * 3.5f + Vector3.Cross(Vector3.up, salida) * 3f;
        for (float s = 0f; s < 8f; s += Time.deltaTime)
        {
            reloj += Time.deltaTime;
            Navegar(ref velocidad, reloj, giroInicial, giroFinal, salida, 16f);
            Vector3 foco = Vector3.Lerp(lancha.position + Vector3.up, lancha.position + salida * 40f + Vector3.up * 6f, Mathf.Clamp01(s / 8f) * .5f);
            camara.transform.SetPositionAndRotation(fija + Vector3.up * s * .15f, Quaternion.LookRotation(foco - fija));
            if (s > 6f) dialogo.Negro.color = new Color(0f, 0f, 0f, Mathf.Clamp01((s - 6f) / 2f));
            yield return null;
        }
        dialogo.Negro.color = Color.black;
        yield return new WaitForSeconds(.4f);
        dialogo.Cerrar();
    }

    void Navegar(ref float velocidad, float reloj, Quaternion desde, Quaternion hasta, Vector3 salida, float maxima)
    {
        velocidad = Mathf.MoveTowards(velocidad, maxima, Time.deltaTime * 3f);
        Quaternion giro = Quaternion.Slerp(desde, hasta, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reloj / 3f)));
        lancha.rotation = giro * Quaternion.Euler(-velocidad * .25f + Mathf.Sin(reloj * 1.6f) * 1.2f, 0f, Mathf.Sin(reloj * 1.1f) * 1.5f);
        Vector3 p = lancha.position + GeografiaIsla.Plano(lancha.forward).normalized * velocidad * Time.deltaTime;
        p.y = baseY + Mathf.Sin(reloj * 2f) * .1f;
        lancha.position = p;
    }

    static GameObject BotellaSimple(Transform padre)
    {
        var raiz = new GameObject("Botella");
        raiz.transform.SetParent(padre, false);
        var vidrio = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        vidrio.SetColor("_BaseColor", new Color(.35f, .2f, .06f));
        vidrio.SetFloat("_Smoothness", .85f);
        foreach (var (pos, esc) in new[] { (new Vector3(0, .15f, 0), new Vector3(.12f, .15f, .12f)), (new Vector3(0, .36f, 0), new Vector3(.05f, .07f, .05f)) })
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(raiz.transform, false);
            p.transform.localPosition = pos;
            p.transform.localScale = esc;
            p.GetComponent<Renderer>().sharedMaterial = vidrio;
        }
        return raiz;
    }

    // ---------------------------------------------------------------- Combate

    void Update()
    {
        if (fase != Fase.Combate || partida == null) return;
        if (partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso || partida.EstaPausada || Time.timeScale <= 0f) return;
        float dt = Time.deltaTime;
        tCombate += dt;
        ActualizarMotor(dt);
        MoverLancha(dt);
        Apuntar(dt);
        Gatillo(dt);
        ActualizarNemesis(dt);
    }

    void LateUpdate()
    {
        if (fase == Fase.Final) ApagarEfectosArma();
        if (fase != Fase.Combate || camara == null || partida == null || partida.EstaPausada || Time.timeScale <= 0f) return;
        float dt = Time.deltaTime;
        var aim = Quaternion.Euler(pitch - retroceso, yaw, 0f);
        pivote.rotation = aim;
        if (arma != null) arma.localPosition = armaBase - Vector3.forward * (retroceso * .05f);
        float balanceo = Mathf.DeltaAngle(0f, lancha.eulerAngles.z) * .4f;
        Vector3 temblor = Random.insideUnitSphere * sacudida * .12f;
        sacudida = Mathf.MoveTowards(sacudida, 0f, dt * 2.2f);
        camara.transform.SetPositionAndRotation(pivote.TransformPoint(camaraLocal) + temblor, aim * Quaternion.Euler(0f, 0f, balanceo));
        camara.fieldOfView = Mathf.Lerp(62f, 34f, zoom);
        ActualizarEfectosArma(dt);
        ActualizarHUD();
    }

    void MoverLancha(float dt)
    {
        Vector3 p = PosicionLancha(tCombate);
        Vector3 siguiente = PosicionLancha(tCombate + .25f);
        Vector3 d = GeografiaIsla.Plano(siguiente - p);
        if (d.sqrMagnitude > .0004f) rumbo = d.normalized;
        float giro = Vector3.SignedAngle(GeografiaIsla.Plano(lancha.forward), rumbo, Vector3.up);
        var objetivo = Quaternion.LookRotation(rumbo, Vector3.up) *
            Quaternion.Euler(Mathf.Sin(tCombate * 1.7f) * 1.5f, 0f, Mathf.Clamp(-giro * .6f, -10f, 10f) + Mathf.Sin(tCombate * 1.3f) * 2f);
        lancha.rotation = Quaternion.Slerp(lancha.rotation, objetivo, 1f - Mathf.Exp(-3f * dt));
        if (conductor != null) conductor.giro = Mathf.Clamp(giro * 4f, -110f, 110f);
        lancha.position = p + Vector3.Cross(Vector3.up, rumbo) * desvioLateral + Vector3.up * (Mathf.Sin(tCombate * 2.1f) * .12f);
    }

    void Apuntar(float dt)
    {
        var mouse = Mouse.current;
        var pad = Gamepad.current;
        bool apunta = (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftTrigger.isPressed);
        zoom = Mathf.MoveTowards(zoom, apunta ? 1f : 0f, dt * 5f);
        float sens = Mathf.Lerp(1f, .5f, zoom);
        Vector2 d = Vector2.zero;
        if (mouse != null) d += mouse.delta.ReadValue() * .09f;
        if (pad != null) d += pad.rightStick.ReadValue() * 140f * dt;
        yaw += d.x * sens;
        pitch = Mathf.Clamp(pitch - d.y * sens, -60f, 25f);
    }

    void Gatillo(float dt)
    {
        var mouse = Mouse.current;
        var pad = Gamepad.current;
        bool gatillo = (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);
        retroceso = Mathf.MoveTowards(retroceso, 0f, dt * 8f);
        if (sobrecalentada)
        {
            calor = Mathf.Max(0f, calor - dt * Enfriamiento * 1.1f);
            if (calor <= .3f) sobrecalentada = false;
            bool pulsado = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            if (pulsado) ClicSeco();
            return;
        }
        if (gatillo && !bloqueoDisparo)
        {
            if (Time.time >= proximoDisparo) Disparo();
        }
        else calor = Mathf.Max(0f, calor - dt * Enfriamiento);
    }

    void Disparo()
    {
        proximoDisparo = Time.time + 1f / Cadencia;
        calor += CalorPorDisparo;
        if (calor >= 1f)
        {
            calor = 1f;
            sobrecalentada = true;
            Mensaje("¡SOBRECALENTADA!", 1.2f);
            EfectoSobrecalentada();
        }
        retroceso = Mathf.Min(retroceso + .55f, 2.2f);
        sacudida = Mathf.Max(sacudida, .22f);
        // El cañón se va hacia arriba y baila hacia los lados en ráfagas largas; apuntando se controla mejor.
        float control = Mathf.Lerp(1f, .5f, zoom);
        yaw += Random.Range(-.1f, .1f) * control;
        pitch = Mathf.Clamp(pitch - .04f * control, -60f, 25f);
        var cam = camara.transform;
        float dispersion = Mathf.Lerp(.9f, .3f, zoom) * Mathf.Deg2Rad;
        Vector2 e = Random.insideUnitCircle * Mathf.Tan(dispersion);
        Vector3 dir = (cam.forward + cam.right * e.x + cam.up * e.y).normalized;
        Vector3 boca = pivote.TransformPoint(bocaLocal);
        Vector3 fin = cam.position + dir * AlcanceBala;
        if (Impactar(cam.position, dir, out var hit))
        {
            fin = hit.point;
            var roca = hit.collider.GetComponent<RocaNemesis>();
            var vida = roca == null ? hit.collider.GetComponentInParent<ControladorVida>() : null;
            if (roca != null)
            {
                golpeHasta = Time.time + .12f;
                if (roca.Recibir(1f)) RocaDestruida(roca, true);
            }
            else if (vida != null && !vida.Muerto)
            {
                bool cabeza = vida.EsCabeza(hit.point);
                float multiplicador = InventarioJugador.Instancia != null ? InventarioJugador.Instancia.MultiplicadorDanio : 1f;
                vida.RecibirDanio(DanioBala * multiplicador * (cabeza ? 2f : 1f), hit.point, dir);
                golpeHasta = Time.time + (cabeza ? .2f : .12f);
            }
            else if (hit.point.y <= GeografiaIsla.NivelAgua + .3f) EfectosPelea.Salpicadura(hit.point, .35f);
            else EfectosPelea.Rafaga(hit.point, hit.normal, new Color(.5f, .45f, .38f), new Color(.7f, .65f, .55f), 10,
                new Vector2(2f, 5f), new Vector2(.1f, .3f), new Vector2(.3f, .7f), 1.2f, 40f, 2f);
        }
        else if (dir.y < -.001f)
        {
            float t = (GeografiaIsla.NivelAgua - cam.position.y) / dir.y;
            if (t > 0f && t < AlcanceBala)
            {
                fin = cam.position + dir * t;
                EfectosPelea.Salpicadura(fin, .35f);
            }
        }
        EfectosDisparo(boca, fin);
    }

    bool Impactar(Vector3 origen, Vector3 dir, out RaycastHit resultado)
    {
        resultado = default;
        float menor = float.PositiveInfinity;
        foreach (var h in Physics.RaycastAll(origen, dir, AlcanceBala, ~0, QueryTriggerInteraction.Collide))
        {
            var t = h.collider.transform;
            if (h.distance >= menor || t.IsChildOf(lancha) || t.IsChildOf(jugador.transform) || (cj != null && t.IsChildOf(cj))) continue;
            if (h.collider.isTrigger && h.collider.GetComponent<RocaNemesis>() == null && h.collider.GetComponentInParent<ControladorVida>() == null) continue;
            menor = h.distance;
            resultado = h;
        }
        return menor < float.PositiveInfinity;
    }

    void ActualizarNemesis(float dt)
    {
        if (nemesis.Muerto) return;
        var t = nemesis.transform;
        Vector3 mirar = GeografiaIsla.Plano(lancha.position - t.position);
        if (mirar.sqrMagnitude > .01f)
            t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(mirar.normalized, Vector3.up), 70f * dt);
        Parametro("Speed", 0f);
        float f = nemesis.Fraccion;
        int nueva = f > .66f ? 1 : f > .33f ? 2 : 3;
        if (nueva > faseJefe)
        {
            faseJefe = nueva;
            StartCoroutine(Enfurecer());
        }
        if (lanzando || qteActivo) return;
        // Con el motor muerto no hay otros QTE: solo sigue lanzando objetos a la lancha detenida.
        if (!motorApagado && tCombate >= proximoPisoton) StartCoroutine(QuickTimeEvent(0));
        else if (!motorApagado && tCombate >= proximoQte)
        {
            tipoQte = tipoQte == 1 ? 2 : 1;
            if (tipoQte == 2) StartCoroutine(QteMotor());
            else StartCoroutine(QuickTimeEvent(1));
        }
        else if (tCombate >= proximoLanzamiento) StartCoroutine(Lanzar());
    }

    IEnumerator Enfurecer()
    {
        StartCoroutine(Rugido());
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.grito : null, nemesis.transform.position, 1f, "Voces", .5f);
        sacudida = 1.2f;
        proximoLanzamiento = Mathf.Max(proximoLanzamiento, tCombate + 2.2f);
        if (faseJefe == 2)
        {
            Mensaje("¡NEMESIS ESTÁ FURIOSO!", 2.5f);
            Decir("¡Puta, ahora tira de a dos! ¡No dejí que lleguen, weón!");
        }
        else
        {
            Mensaje("¡LANZA TODO EN LLAMAS!", 2.5f);
            Decir("¡Está más choreado que la cresta! ¡Dale en la cabeza, que ya cae!");
        }
        yield break;
    }

    static bool EsExplosivo(string nombre) =>
        nombre.Contains("Barrel") || nombre.Contains("Propane") || nombre.Contains("Generator") || nombre.Contains("Transformer") ||
        nombre.Contains("Veh_") || nombre.Contains("Container");

    static bool EsPesado(string nombre) => nombre.Contains("Veh_") || nombre.Contains("Container") || nombre.Contains("Rock");

    static string Frase(string nombre)
    {
        if (nombre.Contains("Tank")) return "¿¡Un TANQUE, weón!? ¡Revienta esa wea!";
        if (nombre.Contains("Veh_")) return "¡Chucha, nos tiró un auto!";
        if (nombre.Contains("Portaloo")) return "¡¿Un baño químico?! ¡Qué asco, weón, dispárale al tiro!";
        if (nombre.Contains("Barrel") || nombre.Contains("Propane")) return "¡Ojo con ese barril, que va a reventar!";
        if (nombre.Contains("Container")) return "¡Un contenedor! ¡Este weón está más loco que una cabra!";
        if (nombre.Contains("Rock")) return "¡Piedra grande, piedra grande, weón!";
        return "¡Está desarmando la isla entera, el desgraciado!";
    }

    Vector3 PosicionMano()
    {
        if (manoNemesis != null) return manoNemesis.position;
        var t = nemesis.transform;
        return t.position + Vector3.up * altura * .8f + t.forward * altura * .15f + t.right * altura * .2f;
    }

    public void RocaLlego(RocaNemesis roca)
    {
        if (roca == null) return;
        Vector3 p = roca.transform.position;
        float d = GeografiaIsla.Plano(p - lancha.position).magnitude;
        rocas.Remove(roca);
        if (d < 5.5f && fase == Fase.Combate)
        {
            EfectosPelea.Explosion(p, 1.3f, roca.EnLlamas);
            EfectosPelea.Salpicadura(new Vector3(p.x, GeografiaIsla.NivelAgua, p.z), 1.2f);
            Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, p, 1f, "Efectos");
            sacudida = 1.5f;
            danioHasta = Time.time + .5f;
            DanarLancha(roca.Danio > 0f ? roca.Danio : roca.EnLlamas ? .2f : .14f);
            string[] quejas = { "¡Nos dieron, weón! ¡Revienta esas weas!", "¡Puta la wea! ¡Así nos vamos a hundir!", "¡Dispárale antes de que caigan, po!" };
            Decir(quejas[Random.Range(0, quejas.Length)]);
        }
        else
        {
            EfectosPelea.Salpicadura(new Vector3(p.x, GeografiaIsla.NivelAgua, p.z), 1.4f);
            if (d < 16f) sacudida = Mathf.Max(sacudida, .6f);
        }
        Destroy(roca.gameObject);
    }

    public void RocaDestruida(RocaNemesis roca, bool porJugador)
    {
        if (roca == null) return;
        Vector3 p = roca.transform.position;
        rocas.Remove(roca);
        EfectosPelea.Explosion(p, roca.EnMano ? 1.6f : 1.1f, roca.EnLlamas);
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, p, .75f, "Efectos");
        if (porJugador && roca.EnMano && !nemesis.Muerto)
        {
            nemesis.RecibirDanio(DanioRocaEnMano, p, GeografiaIsla.Plano(nemesis.transform.position - lancha.position).normalized);
            Mensaje("¡SE LO REVENTASTE EN LA MANO!", 2f);
            sacudida = Mathf.Max(sacudida, .5f);
        }
        else if (porJugador && GeografiaIsla.Plano(p - lancha.position).magnitude < 20f && Random.value < .5f)
            Decir(Random.value < .5f ? "¡Esa estuvo cerca, weón!" : "¡Bien ahí, la raja!");
        Destroy(roca.gameObject);
    }

    void Disparar(string trigger)
    {
        if (animNemesis == null) return;
        foreach (var p in animNemesis.parameters)
            if (p.name == trigger && p.type == AnimatorControllerParameterType.Trigger) { animNemesis.SetTrigger(trigger); return; }
    }

    void Parametro(string nombre, float valor)
    {
        if (animNemesis == null) return;
        foreach (var p in animNemesis.parameters)
            if (p.name == nombre && p.type == AnimatorControllerParameterType.Float) { animNemesis.SetFloat(nombre, valor); return; }
    }

    // ---------------------------------------------------------------- HUD

    void Mensaje(string texto, float segundos)
    {
        if (mensaje == null) return;
        mensaje.text = texto;
        mensajeHasta = Time.time + segundos;
    }

    void Decir(string texto)
    {
        if (lineaCJ == null || Time.time < lineaHasta - 1.5f) return;
        lineaCJ.text = texto;
        lineaHasta = Time.time + 3f;
    }

    void CrearHUD()
    {
        var go = new GameObject("HUD_PeleaJefe", typeof(RectTransform));
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var escala = go.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1280, 720);
        escala.matchWidthOrHeight = .5f;

        danioPantalla = Panel(go.transform, "Danio", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.8f, 0f, 0f, 0f));
        mira = Texto(go.transform, "Mira", new Vector2(.5f, .5f), Vector2.zero, new Vector2(80, 80), 40, Color.white);
        mira.text = "+";
        marcaGolpe = Texto(go.transform, "Golpe", new Vector2(.5f, .5f), Vector2.zero, new Vector2(80, 80), 46, EstiloUI.Rojo);
        marcaGolpe.text = "×";
        // Mira y marca de impacto conservan el trazo fino de la fuente de cuerpo.
        EstiloUI.Aplicar(mira, EstiloUI.Fuente.Cuerpo, .2f, false);
        EstiloUI.Aplicar(marcaGolpe, EstiloUI.Fuente.Cuerpo, .2f, false);
        mira.fontStyle = marcaGolpe.fontStyle = FontStyles.Bold;
        mensaje = Texto(go.transform, "Mensaje", new Vector2(.5f, 1f), new Vector2(0, -120), new Vector2(900, 60), 44, EstiloUI.Oro);
        mensaje.enableAutoSizing = true;
        mensaje.fontSizeMin = 22;
        mensaje.fontSizeMax = 44;

        // Las frases de CJ van en su propia "radio" a la izquierda, lejos de la mira, los QTE y las barras.
        var radio = Panel(go.transform, "RadioCJ", new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(190, -20), new Vector2(340, 104), new Color(.05f, .03f, .1f, .84f));
        radioCJ = radio.gameObject;
        Panel(radio.transform, "Filo", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2.5f, 0), new Vector2(5, 0), EstiloUI.Oro);
        var nombreCJ = Texto(radio.transform, "Nombre", new Vector2(0f, 1f), new Vector2(44, -17), new Vector2(60, 28), 22, EstiloUI.Oro);
        nombreCJ.text = "CJ";
        lineaCJ = Texto(radio.transform, "Linea", new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(316, 70), 22, Color.white);
        EstiloUI.Aplicar(lineaCJ, EstiloUI.Fuente.Cuerpo, .2f);
        lineaCJ.fontStyle = FontStyles.Bold;
        lineaCJ.alignment = TextAlignmentOptions.TopLeft;
        lineaCJ.textWrappingMode = TextWrappingModes.Normal;
        lineaCJ.enableAutoSizing = true;
        lineaCJ.fontSizeMin = 14;
        lineaCJ.fontSizeMax = 22;
        radioCJ.SetActive(false);

        ayuda = Texto(go.transform, "Ayuda", new Vector2(.5f, 0f), new Vector2(0, 26), new Vector2(560, 44), 16, new Color(1f, 1f, 1f, .85f));
        ayuda.textWrappingMode = TextWrappingModes.Normal;
        ayuda.enableAutoSizing = true;
        ayuda.fontSizeMin = 12;
        ayuda.fontSizeMax = 16;
        ayuda.text = "Clic izq. / RT: disparar · Clic der. / LT: apuntar\nDestruye lo que lanza antes de que caiga";
        grupoAyuda = ayuda.gameObject.AddComponent<CanvasGroup>();

        Panel(go.transform, "CalorFondo", new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0, 92), new Vector2(306, 20), Color.black);
        rellenoCalorImg = Panel(go.transform, "CalorRelleno", new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(-150, 92), new Vector2(300, 14), new Color(1f, .75f, .2f));
        rellenoCalor = rellenoCalorImg.rectTransform;
        rellenoCalor.pivot = new Vector2(0f, .5f);
        etiquetaCalor = Texto(go.transform, "CalorEtiqueta", new Vector2(.5f, 0f), new Vector2(0, 116), new Vector2(400, 28), 20, Color.white);
        CrearHUDExtra(go.transform);
    }

    static TMP_Text Texto(Transform padre, string nombre, Vector2 ancla, Vector2 pos, Vector2 tam, float size, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = ancla;
        r.anchoredPosition = pos;
        r.sizeDelta = tam;
        EstiloUI.Aplicar(t, EstiloUI.Fuente.Hud, .22f);
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static Image Panel(Transform padre, string nombre, Vector2 min, Vector2 max, Vector2 pos, Vector2 tam, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.anchoredPosition = pos;
        r.sizeDelta = tam;
        return img;
    }

    static Sprite Anillo
    {
        get
        {
            if (anillo != null) return anillo;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n * .5f, n * .5f)) / (n * .5f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - .8f) / .12f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            anillo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
            return anillo;
        }
    }

    RectTransform Marcador(int i)
    {
        while (marcadores.Count <= i)
        {
            var img = Panel(canvas.transform, "AvisoRoca", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(56, 56), new Color(1f, .2f, .15f, .9f));
            img.sprite = Anillo;
            var signo = Texto(img.transform, "Signo", new Vector2(.5f, .5f), Vector2.zero, new Vector2(56, 56), 30, new Color(1f, .25f, .2f));
            signo.text = "!";
            img.transform.SetSiblingIndex(1);
            marcadores.Add(img.rectTransform);
        }
        return marcadores[i];
    }

    void ActualizarHUD()
    {
        if (canvas == null) return;
        ActualizarBarras();
        bool golpe = Time.time < golpeHasta;
        marcaGolpe.gameObject.SetActive(golpe);
        mira.fontSize = Mathf.Lerp(40f, 30f, zoom) + retroceso * 4f;
        mira.color = sobrecalentada ? new Color(1f, .4f, .2f, .6f) : Color.white;
        mensaje.gameObject.SetActive(Time.time < mensajeHasta);
        radioCJ.SetActive(Time.time < lineaHasta);
        // La ayuda de controles solo al comienzo del combate.
        grupoAyuda.alpha = Mathf.Clamp01((14f - tCombate) / 2f);
        float a = Mathf.Clamp01((danioHasta - Time.time) / .5f) * .35f;
        danioPantalla.color = new Color(.8f, 0f, 0f, a);
        rellenoCalor.localScale = new Vector3(calor, 1f, 1f);
        rellenoCalorImg.color = sobrecalentada ? new Color(1f, .2f, .1f) : Color.Lerp(new Color(1f, .8f, .3f), new Color(1f, .3f, .1f), calor);
        etiquetaCalor.text = sobrecalentada ? "<color=#FF5533>SOBRECALENTADA</color>" : ".50 CAL";

        // Avisos sobre las rocas: en pantalla, o pegados al borde si estan fuera de vista.
        int usados = 0;
        float margen = 40f;
        foreach (var roca in rocas)
        {
            if (roca == null) continue;
            var m = Marcador(usados++);
            m.gameObject.SetActive(true);
            Vector3 s = camara.WorldToScreenPoint(roca.transform.position);
            bool detras = s.z < 0f;
            if (detras) s = new Vector3(Screen.width - s.x, Screen.height - s.y, 0f);
            bool fuera = detras || s.x < margen || s.x > Screen.width - margen || s.y < margen || s.y > Screen.height - margen;
            s.x = Mathf.Clamp(s.x, margen, Screen.width - margen);
            s.y = Mathf.Clamp(s.y, margen, Screen.height - margen);
            m.position = new Vector3(s.x, s.y, 0f);
            float urgencia = roca.EnMano ? .3f : roca.Progreso;
            m.localScale = Vector3.one * (fuera ? 1.1f : Mathf.Lerp(.8f, 1.4f, urgencia));
            var img = m.GetComponent<Image>();
            img.color = Color.Lerp(new Color(1f, .75f, .2f, .9f), new Color(1f, .1f, .1f, 1f), urgencia);
        }
        for (int i = usados; i < marcadores.Count; i++) marcadores[i].gameObject.SetActive(false);
    }
}
