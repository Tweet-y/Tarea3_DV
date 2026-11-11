using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Menú de inicio y resultado; cada nueva partida recarga completamente el mapa.</summary>
public sealed class ControladorMenus : MonoBehaviour
{
    public const string EscenaMenu = "MenuInicio";
    public const string EscenaJuego = "MapaV2";
    private static readonly Color Tinta = EstiloCurao.Noche;
    private CanvasGroup opciones;
    private Button primerBoton;
    private TMP_Text estadoCarga;
    private Image progresoCarga;
    private GameObject carga;
    private bool cambiandoEscena;
    private bool accionEnCurso;
    private AudioSource sonidoBotones;

    private void OnEnable()
    {
        Registrar();
        // Los listeners creados por código se pierden cuando Unity recompila en Play.
        foreach (var boton in GetComponentsInChildren<Button>(true))
        {
            boton.onClick.RemoveAllListeners();
            switch (boton.name)
            {
                case "Iniciar Juego":
                case "Nuevo juego":
                case "Volver a jugar": boton.onClick.AddListener(() => EjecutarBoton(() => Cargar(EscenaJuego))); break;
                case "Continuar": boton.onClick.AddListener(() => EjecutarBoton(ContinuarPartida)); break;
                case "Ir al menú principal": boton.onClick.AddListener(() => EjecutarBoton(() => Cargar(EscenaMenu))); break;
                case "Salir": boton.onClick.AddListener(() => EjecutarBoton(Salir)); break;
                case "AyudaHUD": boton.onClick.AddListener(() => EjecutarBoton(AlternarAyudaHUD)); break;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Registrar()
    {
        SceneManager.sceneLoaded -= EscenaCargada;
        SceneManager.sceneLoaded += EscenaCargada;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AbrirMenuAlIniciar()
    {
        // También al pulsar Play con el mapa abierto en el editor.
        if (SceneManager.GetActiveScene().name == EscenaJuego)
            SceneManager.LoadScene(EscenaMenu);
    }

    private static void EscenaCargada(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != EscenaMenu) return;
        Time.timeScale = 1f;
        PeleaJefeLancha.ModoPrueba = false;
        // El menú anterior permanece en el archivo de escena, pero no se superpone al nuevo.
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.gameObject.scene == scene) canvas.gameObject.SetActive(false);
        var menu = new GameObject("MenuPrincipal", typeof(RectTransform)).AddComponent<ControladorMenus>();
        menu.ConstruirInicio();
    }

    public void ConstruirInicio()
    {
        PrepararCanvas();
        Fondo(transform, "Fondo", Tinta);
        FondoPixel();
        // Oscurecer apenas el arte para que los botones y textos resalten.
        Fondo(transform, "Velo", new Color(.086f, .055f, .157f, .22f)).GetComponent<Image>().raycastTarget = false;
        var titulo = Rect(transform, "TituloPixel", new Vector2(.06f, .60f), new Vector2(.94f, .95f));
        var letras = titulo.gameObject.AddComponent<TextoPixelGraphic>();
        letras.Contenido = "SUPER CURAO\nMAN";
        letras.color = EstiloCurao.Turquesa;
        letras.ConSombra = true;
        letras.raycastTarget = false;
        var lema = Texto(transform, "Lema", "30 BOTELLAS  ·  1 ISLA  ·  0 SOBRIEDAD", 0, 0, 900, 40, 34, EstiloCurao.Durazno);
        lema.rectTransform.anchorMin = lema.rectTransform.anchorMax = new Vector2(.5f, .565f);
        lema.alignment = TextAlignmentOptions.Center;
        lema.characterSpacing = 4;
        var contenido = Rect(transform, "BotonesCentrados", new Vector2(.5f, .36f), new Vector2(.5f, .36f));
        contenido.sizeDelta = new Vector2(300, 188);
        primerBoton = BotonPixel(contenido, "Iniciar Juego", 64, () => Cargar(EscenaJuego));
        BotonPixel(contenido, "Probar Jefe", 0, () =>
        {
            PeleaJefeLancha.ModoPrueba = true;
            Cargar(EscenaJuego);
        });
        BotonPixel(contenido, "Salir", -64, Salir);
        var pie = Texto(transform, "Controles", "FLECHAS / MOUSE: ELEGIR     ENTER / A: ACEPTAR", 0, 0, 900, 30, 24, EstiloCurao.TextoTenue);
        pie.rectTransform.anchorMin = pie.rectTransform.anchorMax = new Vector2(.5f, 0f);
        pie.rectTransform.anchoredPosition = new Vector2(0, 26);
        pie.alignment = TextAlignmentOptions.Center;
        CrearCarga();
        if (Application.isPlaying) StartCoroutine(Seleccionar(primerBoton));
    }

    private void FondoPixel()
    {
        var arte = Rect(transform, "CiudadPixelArt", Vector2.zero, Vector2.one);
        var imagen = arte.gameObject.AddComponent<RawImage>();
        var textura = Resources.Load<Texture2D>("Interfaz/MenuCiudadPixel");
        imagen.texture = textura;
        imagen.raycastTarget = false;
        if (textura != null)
        {
            textura.filterMode = FilterMode.Point;
            var ajuste = arte.gameObject.AddComponent<AspectRatioFitter>();
            ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            ajuste.aspectRatio = (float)textura.width / textura.height;
        }
    }

    public void ConstruirPausa()
    {
        PrepararCanvas();
        Fondo(transform, "FondoPausa", new Color(.086f, .055f, .157f, .82f));
        var titulo = Rect(transform, "TituloPausa", new Vector2(.2f, .68f), new Vector2(.8f, .9f));
        var letras = titulo.gameObject.AddComponent<TextoPixelGraphic>();
        letras.Contenido = "PAUSA";
        letras.color = EstiloCurao.Turquesa;
        letras.ConSombra = true;
        letras.raycastTarget = false;
        var marco = Rect(transform, "MarcoPausa", new Vector2(.5f, .43f), new Vector2(.5f, .43f));
        marco.sizeDelta = new Vector2(440, 372);
        EstiloCurao.Marco(marco, EstiloCurao.PanelSuave, EstiloCurao.Turquesa);
        var botones = Rect(transform, "BotonesPausa", new Vector2(.5f, .43f), new Vector2(.5f, .43f));
        botones.sizeDelta = new Vector2(300, 254);
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida != null)
        {
            var estado = Texto(botones, "EstadoPartida",
                $"BOTELLAS {partida.Recogidas}/{partida.Total}     VIDA {Mathf.RoundToInt(partida.Vida * 100f)}%",
                0, 152, 420, 30, 26, EstiloCurao.Ambar);
            estado.alignment = TextAlignmentOptions.Center;
        }
        primerBoton = BotonPixel(botones, "Continuar", 96, ContinuarPartida);
        var ayuda = BotonPixel(botones, TextoAyudaHUD(), 32, AlternarAyudaHUD, 360);
        ayuda.name = "AyudaHUD";
        BotonPixel(botones, "Nuevo juego", -32, () => Cargar(EscenaJuego));
        BotonPixel(botones, "Salir", -96, Salir);
        var pista = Texto(botones, "Atajo", "ESC PARA CONTINUAR", 0, -154, 400, 26, 24, EstiloCurao.TextoTenue);
        pista.alignment = TextAlignmentOptions.Center;
        CrearCarga();
        if (Application.isPlaying) StartCoroutine(Seleccionar(primerBoton));
    }

    private static void ContinuarPartida()
    {
        if (ControladorPartidaBotellas.Instancia != null) ControladorPartidaBotellas.Instancia.Continuar();
    }

    private static string TextoAyudaHUD() => ControladorGameplayHUD.IndicacionesVisibles ? "Ocultar indicaciones" : "Mostrar indicaciones";

    private void AlternarAyudaHUD()
    {
        ControladorGameplayHUD.AlternarIndicaciones();
        var botones = GetComponentsInChildren<Button>(true);
        foreach (var boton in botones)
            if (boton.name == "AyudaHUD")
            {
                var visual = boton.GetComponent<BotonCurao>();
                if (visual != null && visual.etiqueta != null) visual.etiqueta.Contenido = TextoAyudaHUD().ToUpperInvariant();
            }
    }

    private Button BotonPixel(Transform padre, string valor, float y, UnityEngine.Events.UnityAction accion, float ancho = 300)
    {
        Caja(padre, valor + "Sombra", 5, y - 5, ancho, 52, new Color(.04f, .02f, .08f, .85f));
        var rect = Caja(padre, valor, 0, y, ancho, 52, EstiloCurao.Panel);
        var imagen = EstiloCurao.Marco(rect, EstiloCurao.Panel, EstiloCurao.Turquesa, 3f, false);
        imagen.raycastTarget = true;
        var etiqueta = Rect(rect, "Etiqueta", new Vector2(.1f, .2f), new Vector2(.9f, .8f));
        var texto = etiqueta.gameObject.AddComponent<TextoPixelGraphic>();
        texto.Contenido = valor.ToUpperInvariant();
        texto.MaximoPixel = 3;
        texto.color = EstiloCurao.Texto;
        texto.raycastTarget = false;
        var izquierda = Flecha(rect, "FlechaIzq", ">", new Vector2(0f, .2f), new Vector2(.1f, .8f));
        var derecha = Flecha(rect, "FlechaDer", "<", new Vector2(.9f, .2f), new Vector2(1f, .8f));
        var boton = rect.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;
        boton.transition = Selectable.Transition.None;
        var visual = rect.gameObject.AddComponent<BotonCurao>();
        visual.fondo = imagen;
        visual.etiqueta = texto;
        visual.flechas = new[] { izquierda, derecha };
        boton.onClick.AddListener(() => EjecutarBoton(accion));
        return boton;
    }

    private static GameObject Flecha(Transform padre, string nombre, string simbolo, Vector2 min, Vector2 max)
    {
        var rect = Rect(padre, nombre, min, max);
        var flecha = rect.gameObject.AddComponent<TextoPixelGraphic>();
        flecha.Contenido = simbolo;
        flecha.MaximoPixel = 3;
        flecha.color = EstiloCurao.Noche;
        flecha.raycastTarget = false;
        rect.gameObject.SetActive(false);
        return rect.gameObject;
    }

    private void EjecutarBoton(UnityEngine.Events.UnityAction accion)
    {
        if (accionEnCurso || cambiandoEscena) return;
        accionEnCurso = true;
        StartCoroutine(SonarYActuar(accion));
    }

    private IEnumerator SonarYActuar(UnityEngine.Events.UnityAction accion)
    {
        if (sonidoBotones == null)
        {
            sonidoBotones = gameObject.AddComponent<AudioSource>();
            sonidoBotones.playOnAwake = false;
            sonidoBotones.spatialBlend = 0f;
            sonidoBotones.ignoreListenerPause = true;
            sonidoBotones.volume = .45f;
            sonidoBotones.clip = Resources.Load<AudioClip>("Sonido/Coins 10");
            Sonido.Asignar(sonidoBotones, "Efectos");
        }
        if (sonidoBotones.clip != null)
        {
            sonidoBotones.Play();
            // El clic arranca en pausa; el resto sigue en otro objeto para que el cambio de escena no lo corte.
            yield return new WaitForSecondsRealtime(.18f);
            ConservarClic();
        }
        accion.Invoke();
        accionEnCurso = false;
    }

    private void ConservarClic()
    {
        if (sonidoBotones == null || sonidoBotones.clip == null || !sonidoBotones.isPlaying) return;
        float instante = sonidoBotones.time;
        var clip = sonidoBotones.clip;
        float volumen = sonidoBotones.volume;
        sonidoBotones.Stop();
        var objeto = new GameObject("ClicBoton");
        DontDestroyOnLoad(objeto);
        var fuente = objeto.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.ignoreListenerPause = true;
        fuente.volume = volumen;
        Sonido.Asignar(fuente, "Efectos");
        fuente.clip = clip;
        fuente.Play();
        fuente.time = instante;
        Destroy(objeto, Mathf.Max(.05f, clip.length - instante));
    }

    public void ConstruirResultado(bool victoria, string resumen, bool animar = true)
    {
        PrepararCanvas();
        if (!victoria) FondoPixel();
        Fondo(transform, "Oscuridad", new Color(.086f, .055f, .157f, victoria ? .55f : .84f));
        var banda = Rect(transform, "BandaTitulo", new Vector2(0, .42f), new Vector2(1, .72f));
        Color acento = victoria ? (Color)EstiloCurao.Turquesa : EstiloCurao.Rojo;
        EstiloCurao.Marco(banda, new Color(.086f, .055f, .157f, .86f), acento, 4f, false);
        var titulo = Rect(banda, "TituloResultado", new Vector2(.05f, .36f), new Vector2(.95f, .92f));
        var letras = titulo.gameObject.AddComponent<TextoPixelGraphic>();
        letras.Contenido = victoria ? TituloVictoria : TituloDerrota;
        letras.color = acento;
        letras.ConSombra = true;
        if (!victoria) letras.ColorBrillo = new Color32(110, 18, 40, 255);
        letras.raycastTarget = false;
        var bajada = Texto(banda, "BajadaResultado", victoria ? "LA ISLA QUEDÓ SIN UNA GOTA DE COPETE" : "Y LA NOCHE RECIÉN EMPEZABA...",
            0, 0, 1100, 44, 38, victoria ? EstiloCurao.Ambar : EstiloCurao.Durazno);
        bajada.rectTransform.anchorMin = bajada.rectTransform.anchorMax = new Vector2(.5f, .2f);
        bajada.alignment = TextAlignmentOptions.Center;
        bajada.characterSpacing = 3;
        var tituloGrupo = banda.gameObject.AddComponent<CanvasGroup>();
        var rectOpciones = Rect(transform, "OpcionesResultado", new Vector2(.5f, .22f), new Vector2(.5f, .22f));
        rectOpciones.sizeDelta = new Vector2(600, 290);
        opciones = rectOpciones.gameObject.AddComponent<CanvasGroup>();
        var marcoResumen = Caja(rectOpciones, "MarcoResumen", 0, 62, 560, 100, EstiloCurao.PanelSuave);
        EstiloCurao.Marco(marcoResumen, EstiloCurao.PanelSuave, acento, 2f);
        var detalle = Texto(rectOpciones, "Resumen", resumen, 0, 62, 540, 92, 28, EstiloCurao.Texto);
        detalle.alignment = TextAlignmentOptions.Center;
        detalle.textWrappingMode = TextWrappingModes.Normal;
        detalle.lineSpacing = -8;
        primerBoton = BotonPixel(rectOpciones, "Volver a jugar", -26, () => Cargar(EscenaJuego), 440);
        BotonPixel(rectOpciones, "Ir al menú principal", -93, () => Cargar(EscenaMenu), 440);
        CrearCarga();
        if (animar && Application.isPlaying)
        {
            StartCoroutine(EntradaMuerte(tituloGrupo));
            ReproducirCierre(victoria);
        }
    }

    public const string TituloVictoria = "¡MISIÓN CUMPLIDA!";
    public const string TituloDerrota = "QUEDASTE EN NEGRO";

    private void ReproducirCierre(bool victoria)
    {
        var clip = Resources.Load<AudioClip>(victoria ? "Sonido/Coins 10" : "Sonido/Explosion Gunshot_01");
        if (clip == null) return;
        var fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.clip = clip;
        Sonido.Asignar(fuente, "Efectos");
        fuente.Play();
        StartCoroutine(CortarSonido(fuente, 1.2f));
    }

    private static IEnumerator CortarSonido(AudioSource fuente, float segundos)
    {
        yield return new WaitForSecondsRealtime(segundos);
        if (fuente != null) fuente.Stop();
    }

    private void PrepararCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();
        if (!Application.isPlaying) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        var sistema = EventSystem.current;
        if (sistema == null) sistema = new GameObject("EventSystemMenus", typeof(EventSystem)).GetComponent<EventSystem>();
        foreach (var module in sistema.GetComponents<BaseInputModule>())
            if (!(module is InputSystemUIInputModule)) module.enabled = false;
        var inputUI = sistema.GetComponent<InputSystemUIInputModule>();
        if (inputUI == null) inputUI = sistema.gameObject.AddComponent<InputSystemUIInputModule>();
        inputUI.enabled = true;
        if (inputUI.actionsAsset == null) inputUI.AssignDefaultActions();
    }

    private IEnumerator EntradaMuerte(CanvasGroup titulo)
    {
        var fondo = GetComponent<Canvas>().transform.Find("Oscuridad").GetComponent<Image>();
        var colorFinal = fondo.color;
        opciones.alpha = 0;
        opciones.interactable = opciones.blocksRaycasts = false;
        titulo.alpha = 0;
        float tiempo = 0;
        while (tiempo < 2.2f)
        {
            tiempo += Time.unscaledDeltaTime;
            titulo.alpha = Mathf.SmoothStep(0, 1, tiempo / 1.4f);
            fondo.color = new Color(colorFinal.r, colorFinal.g, colorFinal.b, Mathf.Lerp(.2f, colorFinal.a, tiempo / 1.4f));
            opciones.alpha = Mathf.Clamp01((tiempo - 1.6f) / .6f);
            yield return null;
        }
        opciones.alpha = 1;
        opciones.interactable = opciones.blocksRaycasts = true;
        yield return Seleccionar(primerBoton);
    }

    private IEnumerator Seleccionar(Button boton)
    {
        yield return null;
        if (EventSystem.current != null && boton != null) EventSystem.current.SetSelectedGameObject(boton.gameObject);
    }

    private void CrearCarga()
    {
        var rect = Fondo(transform, "PantallaCarga", EstiloCurao.Noche);
        var titulo = Rect(rect, "TituloCarga", new Vector2(.2f, .55f), new Vector2(.8f, .7f));
        var letras = titulo.gameObject.AddComponent<TextoPixelGraphic>();
        letras.Contenido = "RUMBO A LA ISLA";
        letras.color = EstiloCurao.Turquesa;
        letras.ConSombra = true;
        letras.MaximoPixel = 6;
        letras.raycastTarget = false;
        estadoCarga = Texto(rect, "EstadoCarga", "CARGANDO… 0%", 0, -6, 800, 40, 34, EstiloCurao.Texto);
        estadoCarga.alignment = TextAlignmentOptions.Center;
        var carril = Caja(rect, "Carril", 0, -50, 512, 20, EstiloCurao.Panel);
        EstiloCurao.Marco(carril, EstiloCurao.Panel, EstiloCurao.Turquesa, 3f, false);
        var barra = Caja(rect, "Progreso", -250, -50, 500, 8, EstiloCurao.Turquesa);
        barra.pivot = new Vector2(0, .5f);
        progresoCarga = barra.GetComponent<Image>();
        var consejo = Texto(rect, "Consejo", Consejos[Random.Range(0, Consejos.Length)], 0, -120, 820, 70, 28, EstiloCurao.Durazno);
        consejo.alignment = TextAlignmentOptions.Center;
        consejo.textWrappingMode = TextWrappingModes.Normal;
        carga = rect.gameObject;
        carga.SetActive(false);
    }

    private static readonly string[] Consejos =
    {
        "CONSEJO: el bate no se queda sin balas. El rifle sí.",
        "CONSEJO: Q usa un botiquín. No esperís a estar con un pie en la tumba.",
        "CONSEJO: la bebida energética (F) te deja más rápido y pegando más fuerte por 15 segundos.",
        "CONSEJO: el mar no perdona. Si te caís al agua, se acabó la fiesta.",
        "CONSEJO: apúntale a la cabeza, que el rifle pega mucho más fuerte ahí.",
        "CONSEJO: las botellas azules curan, pero no cuentan para las 30.",
        "CONSEJO: M abre el mapa grande. Las botellas no se buscan solas, po.",
        "CONSEJO: E recoge armas y munición. G suelta el rifle.",
    };

    public void Cargar(string escena)
    {
        if (cambiandoEscena) return;
        cambiandoEscena = true;
        StartCoroutine(CargarEscena(escena));
    }

    private IEnumerator CargarEscena(string escena)
    {
        carga.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        // La pantalla de carga sigue respondiendo aunque el resultado haya pausado el mundo.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        var operacion = SceneManager.LoadSceneAsync(escena, LoadSceneMode.Single);
        while (!operacion.isDone)
        {
            float avance = Mathf.Clamp01(operacion.progress / .9f);
            progresoCarga.rectTransform.localScale = new Vector3(avance, 1, 1);
            estadoCarga.text = "CARGANDO… " + Mathf.RoundToInt(avance * 100) + "%";
            yield return null;
        }
    }

    public void Salir()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnApplicationFocus(bool foco)
    {
        if (!foco) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static RectTransform Rect(Transform padre, string nombre, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform Fondo(Transform padre, string nombre, Color color)
    {
        var rect = Rect(padre, nombre, Vector2.zero, Vector2.one);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private static RectTransform Caja(Transform padre, string nombre, float x, float y, float ancho, float alto, Color color)
    {
        var rect = Rect(padre, nombre, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(ancho, alto);
        var imagen = rect.gameObject.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
        return rect;
    }

    private static TMP_Text Texto(Transform padre, string nombre, string valor, float x, float y, float ancho, float alto, float tamano, Color color)
    {
        var rect = Rect(padre, nombre, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(ancho, alto);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        EstiloCurao.Aplicar(text, tamano, color);
        text.text = valor;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
}
