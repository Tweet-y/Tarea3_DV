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
    public const string EscenaJuego = "MapaV1";
    private static readonly Color Verde = new Color32(170, 198, 99, 255);
    private static readonly Color Tinta = new Color32(17, 21, 20, 255);
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
        var titulo = Rect(transform, "TituloPixel", new Vector2(.06f, .60f), new Vector2(.94f, .95f));
        var letras = titulo.gameObject.AddComponent<TextoPixelGraphic>();
        letras.Contenido = "SUPER CURAO\nMAN";
        letras.color = new Color32(57, 216, 182, 255);
        letras.ConSombra = true;
        letras.raycastTarget = false;
        var contenido = Rect(transform, "BotonesCentrados", new Vector2(.5f, .36f), new Vector2(.5f, .36f));
        contenido.sizeDelta = new Vector2(300, 124);
        primerBoton = BotonPixel(contenido, "Iniciar Juego", 32, () => Cargar(EscenaJuego));
        BotonPixel(contenido, "Salir", -32, Salir);
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
        Fondo(transform, "FondoPausa", new Color(.025f, .018f, .065f, .85f));
        var titulo = Rect(transform, "TituloPausa", new Vector2(.2f, .65f), new Vector2(.8f, .88f));
        var letras = titulo.gameObject.AddComponent<TextoPixelGraphic>();
        letras.Contenido = "PAUSA";
        letras.color = new Color32(57, 216, 182, 255);
        letras.ConSombra = true;
        letras.raycastTarget = false;
        var botones = Rect(transform, "BotonesPausa", new Vector2(.5f, .43f), new Vector2(.5f, .43f));
        botones.sizeDelta = new Vector2(300, 190);
        primerBoton = BotonPixel(botones, "Continuar", 64, ContinuarPartida);
        BotonPixel(botones, "Nuevo juego", 0, () => Cargar(EscenaJuego));
        BotonPixel(botones, "Salir", -64, Salir);
        var pista = Texto(botones, "Atajo", "ESC PARA CONTINUAR", 0, -119, 400, 24, 15, new Color32(207, 195, 212, 255));
        pista.alignment = TextAlignmentOptions.Center;
        CrearCarga();
        if (Application.isPlaying) StartCoroutine(Seleccionar(primerBoton));
    }

    private static void ContinuarPartida()
    {
        if (ControladorPartidaBotellas.Instancia != null) ControladorPartidaBotellas.Instancia.Continuar();
    }

    private Button BotonPixel(Transform padre, string valor, float y, UnityEngine.Events.UnityAction accion, float ancho = 300)
    {
        Caja(padre, valor + "Sombra", 4, y - 5, ancho, 56, new Color32(21, 22, 43, 255));
        var rect = Caja(padre, valor, 0, y, ancho, 56, Color.white);
        var imagen = rect.GetComponent<Image>();
        imagen.raycastTarget = true;
        Caja(rect, "BordeSuperior", 0, 26, ancho, 4, new Color32(250, 241, 235, 255));
        Caja(rect, "BordeInferior", 0, -26, ancho, 4, new Color32(109, 111, 129, 255));
        var etiqueta = Rect(rect, "Etiqueta", new Vector2(.07f, .16f), new Vector2(.93f, .84f));
        var texto = etiqueta.gameObject.AddComponent<TextoPixelGraphic>();
        texto.Contenido = valor.ToUpperInvariant();
        texto.MaximoPixel = 3;
        texto.color = new Color32(28, 44, 65, 255);
        texto.raycastTarget = false;
        var boton = rect.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;
        var colores = boton.colors;
        colores.normalColor = new Color32(202, 203, 212, 255);
        colores.highlightedColor = new Color32(163, 236, 208, 255);
        colores.selectedColor = colores.highlightedColor;
        colores.pressedColor = new Color32(98, 184, 165, 255);
        colores.fadeDuration = .08f;
        boton.colors = colores;
        boton.onClick.AddListener(() => EjecutarBoton(accion));
        return boton;
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
        fuente.clip = clip;
        fuente.Play();
        fuente.time = instante;
        Destroy(objeto, Mathf.Max(.05f, clip.length - instante));
    }

    public void ConstruirResultado(bool victoria, string resumen, bool animar = true)
    {
        PrepararCanvas();
        if (!victoria) FondoPixel();
        Fondo(transform, "Oscuridad", new Color(.025f, .018f, .065f, victoria ? .55f : .84f));
        var banda = Rect(transform, "BandaTitulo", new Vector2(0, .40f), new Vector2(1, .70f));
        banda.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .72f);
        Color color = victoria ? new Color32(255, 183, 36, 255) : (Color)new Color32(166, 42, 33, 255);
        var titulo = Texto(banda, "TituloResultado", victoria ? "mission passed!" : "Has muerto", 0, victoria ? 34 : 0, 1100, 100, victoria ? 78 : 82, color);
        titulo.alignment = TextAlignmentOptions.Center;
        titulo.characterSpacing = victoria ? -3 : 9;
        titulo.fontStyle = victoria ? FontStyles.Bold : FontStyles.Normal;
        AjustarAnchoTitulo(titulo);
        if (victoria)
        {
            EstiloSanAndreas(titulo);
            var respeto = Texto(banda, "RespetoVictoria", "RESPECT + 99", 0, -47, 1100, 72, 52, Color.white);
            respeto.alignment = TextAlignmentOptions.Center;
            respeto.fontStyle = FontStyles.Bold;
            AjustarAnchoTitulo(respeto);
            EstiloSanAndreas(respeto);
        }
        var tituloGrupo = banda.gameObject.AddComponent<CanvasGroup>();
        var rectOpciones = Rect(transform, "OpcionesResultado", new Vector2(.5f, .24f), new Vector2(.5f, .24f));
        rectOpciones.sizeDelta = new Vector2(600, 290);
        opciones = rectOpciones.gameObject.AddComponent<CanvasGroup>();
        var detalle = Texto(rectOpciones, "Resumen", resumen, 0, 58, 600, 96, 24, new Color32(207, 195, 212, 255));
        detalle.alignment = TextAlignmentOptions.Center;
        detalle.textWrappingMode = TextWrappingModes.Normal;
        primerBoton = BotonPixel(rectOpciones, "Volver a jugar", -26, () => Cargar(EscenaJuego), 440);
        BotonPixel(rectOpciones, "Ir al menú principal", -93, () => Cargar(EscenaMenu), 440);
        CrearCarga();
        if (animar && Application.isPlaying)
        {
            StartCoroutine(EntradaMuerte(tituloGrupo));
            ReproducirCierre(victoria);
        }
    }

    private static void AjustarAnchoTitulo(TMP_Text texto)
    {
        var rect = texto.rectTransform;
        rect.anchorMin = new Vector2(.04f, .5f);
        rect.anchorMax = new Vector2(.96f, .5f);
        rect.sizeDelta = new Vector2(0, rect.sizeDelta.y);
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 36;
        texto.fontSizeMax = texto.fontSize;
    }

    private void ReproducirCierre(bool victoria)
    {
        var clip = Resources.Load<AudioClip>(victoria ? "Sonido/Coins 10" : "Sonido/Explosion Gunshot_01");
        if (clip == null) return;
        var fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.clip = clip;
        fuente.Play();
        StartCoroutine(CortarSonido(fuente, 1.2f));
    }

    private static IEnumerator CortarSonido(AudioSource fuente, float segundos)
    {
        yield return new WaitForSecondsRealtime(segundos);
        if (fuente != null) fuente.Stop();
    }

    private void EstiloSanAndreas(TMP_Text texto)
    {
        var material = new Material(texto.fontSharedMaterial);
        texto.fontMaterial = material;
        texto.outlineColor = Color.black;
        texto.outlineWidth = .3f;
        materialesResultado.Add(material);
    }
    private readonly System.Collections.Generic.List<Material> materialesResultado = new System.Collections.Generic.List<Material>();
    private void OnDestroy()
    {
        foreach (var material in materialesResultado) Destroy(material);
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
        var rect = Fondo(transform, "PantallaCarga", Tinta);
        estadoCarga = Texto(rect, "EstadoCarga", "CARGANDO LA CIUDAD…", 0, 15, 800, 54, 36, Color.white);
        estadoCarga.alignment = TextAlignmentOptions.Center;
        Caja(rect, "Carril", 0, -50, 500, 4, new Color32(51, 61, 49, 255));
        var barra = Caja(rect, "Progreso", -250, -50, 500, 4, Verde);
        barra.pivot = new Vector2(0, .5f);
        progresoCarga = barra.GetComponent<Image>();
        carga = rect.gameObject;
        carga.SetActive(false);
    }

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
        text.font = TMP_Settings.defaultFontAsset;
        text.text = valor;
        text.fontSize = tamano;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    private static Button Boton(Transform padre, string valor, float x, float y, float ancho, float alto, Color acento, UnityEngine.Events.UnityAction accion)
    {
        var rect = Caja(padre, valor, x, y, ancho, alto, new Color32(33, 39, 33, 255));
        var fondo = rect.GetComponent<Image>();
        fondo.color = Color.white;
        fondo.raycastTarget = true;
        Caja(rect, "Acento", -ancho / 2 + 2, 0, 4, alto, acento);
        var texto = Texto(rect, "Etiqueta", valor, 12, 0, ancho - 48, alto, 24, Color.white);
        texto.fontStyle = FontStyles.Bold;
        var boton = rect.gameObject.AddComponent<Button>();
        boton.targetGraphic = fondo;
        var colores = boton.colors;
        colores.normalColor = new Color32(33, 39, 33, 255);
        colores.highlightedColor = acento;
        colores.selectedColor = acento;
        colores.pressedColor = new Color(.55f, .65f, .4f);
        colores.fadeDuration = .12f;
        boton.colors = colores;
        boton.onClick.AddListener(accion);
        return boton;
    }
}
