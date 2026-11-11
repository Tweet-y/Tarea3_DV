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
    private static readonly Color Tinta = EstiloUI.Noche;
    private static readonly string[] Consejos =
    {
        "Las botellas azules devuelven vida. Guárdalas para cuando la cosa se ponga fea.",
        "El radar solo muestra lo que ya exploraste. Camina la isla completa.",
        "Con el rifle en la mano, apunta antes de disparar: la mira se cierra.",
        "Una caída de más de cuatro metros duele. Mira antes de saltar.",
        "Nemesis guarda la última botella. Llega con munición.",
        "H esconde las indicaciones cuando ya te sabes los controles."
    };
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
        PanelLateral(.94f);
        var titulo = Titulo("TituloPixel", $"SUPER CURAO\n<size=125%><color={EstiloUI.HexRosa}>MAN</color></size>",
            new Vector2(.06f, .56f), new Vector2(.62f, .95f), 118);
        titulo.lineSpacing = -28;
        var lema = TextoAnclado(transform, "Lema", "LA ISLA TIENE 30 BOTELLAS. TÚ TIENES SED.",
            new Vector2(.07f, .50f), new Vector2(560, 28), 20, EstiloUI.Rosa);
        lema.characterSpacing = 6;
        var contenido = Rect(transform, "BotonesCentrados", new Vector2(.07f, .29f), new Vector2(.07f, .29f));
        contenido.pivot = new Vector2(0, .5f);
        contenido.sizeDelta = new Vector2(420, 188);
        primerBoton = BotonPixel(contenido, "Iniciar Juego", 64, () => Cargar(EscenaJuego));
        BotonPixel(contenido, "Probar Jefe", 0, () =>
        {
            PeleaJefeLancha.ModoPrueba = true;
            Cargar(EscenaJuego);
        });
        BotonPixel(contenido, "Salir", -64, Salir);
        PieDeAyuda();
        CrearCarga();
        if (Application.isPlaying) StartCoroutine(Seleccionar(primerBoton));
    }

    /// <summary>Sombra lateral: el texto del menú se lee sobre cualquier fondo sin taparlo entero.</summary>
    private void PanelLateral(float opacidad)
    {
        var panel = Rect(transform, "PanelLateral", Vector2.zero, new Vector2(.7f, 1));
        var forma = panel.gameObject.AddComponent<FormaUIGraphic>();
        forma.color = new Color(Tinta.r, Tinta.g, Tinta.b, opacidad);
        forma.Degradado = true;
        forma.ColorB = new Color(Tinta.r, Tinta.g, Tinta.b, 0);
        forma.raycastTarget = false;
    }

    private TMP_Text Titulo(string nombre, string valor, Vector2 min, Vector2 max, float tamano)
    {
        var rect = Rect(transform, nombre, min, max);
        rect.localRotation = Quaternion.Euler(0, 0, 3f);
        var texto = rect.gameObject.AddComponent<TextMeshProUGUI>();
        EstiloUI.Aplicar(texto, EstiloUI.Fuente.Titulo, .3f);
        texto.text = valor;
        texto.color = EstiloUI.Oro;
        texto.alignment = TextAlignmentOptions.BottomLeft;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 40;
        texto.fontSizeMax = tamano;
        texto.raycastTarget = false;
        return texto;
    }

    private void PieDeAyuda()
    {
        var pie = TextoAnclado(transform, "PieAyuda", "FLECHAS / STICK: ELEGIR  ·  ENTER / A: ACEPTAR",
            new Vector2(.07f, .06f), new Vector2(640, 24), 15, EstiloUI.Gris);
        pie.characterSpacing = 3;
    }

    /// <summary>Texto alineado a la izquierda cuyo borde izquierdo queda en el ancla dada.</summary>
    private static TMP_Text TextoAnclado(Transform padre, string nombre, string valor, Vector2 ancla, Vector2 tamano, float puntos, Color color)
    {
        var texto = Texto(padre, nombre, valor, 0, 0, tamano.x, tamano.y, puntos, color);
        var rect = texto.rectTransform;
        rect.anchorMin = rect.anchorMax = ancla;
        rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = Vector2.zero;
        texto.alignment = TextAlignmentOptions.Left;
        return texto;
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
        Fondo(transform, "FondoPausa", new Color(Tinta.r, Tinta.g, Tinta.b, .78f));
        PanelLateral(.9f);
        Titulo("TituloPausa", "PAUSA", new Vector2(.06f, .70f), new Vector2(.6f, .93f), 130);
        var botones = Rect(transform, "BotonesPausa", new Vector2(.07f, .42f), new Vector2(.07f, .42f));
        botones.pivot = new Vector2(0, .5f);
        botones.sizeDelta = new Vector2(420, 254);
        primerBoton = BotonPixel(botones, "Continuar", 96, ContinuarPartida);
        var ayuda = BotonPixel(botones, TextoAyudaHUD(), 32, AlternarAyudaHUD);
        ayuda.name = "AyudaHUD";
        BotonPixel(botones, "Nuevo juego", -32, () => Cargar(EscenaJuego));
        BotonPixel(botones, "Salir", -96, Salir);
        var pista = TextoAnclado(transform, "Atajo", "ESC PARA CONTINUAR", new Vector2(.07f, .06f), new Vector2(400, 24), 15, EstiloUI.Gris);
        pista.characterSpacing = 3;
        EstadisticasPausa();
        CrearCarga();
        if (Application.isPlaying) StartCoroutine(Seleccionar(primerBoton));
    }

    /// <summary>Resumen de la partida en curso, a la derecha del menú de pausa.</summary>
    private void EstadisticasPausa()
    {
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida == null) return;
        var bloque = Rect(transform, "Estadisticas", new Vector2(1, .5f), new Vector2(1, .5f));
        bloque.pivot = new Vector2(1, .5f);
        bloque.anchoredPosition = new Vector2(-70, 0);
        bloque.sizeDelta = new Vector2(360, 250);
        var encabezado = Texto(bloque, "Encabezado", "ESTADÍSTICAS", 0, 96, 360, 34, 24, EstiloUI.Rosa);
        encabezado.alignment = TextAlignmentOptions.Right;
        encabezado.characterSpacing = 4;
        Caja(bloque, "Regla", 0, 72, 360, 4, Color.black);
        Caja(bloque, "ReglaOro", 0, 72, 356, 2, EstiloUI.Oro);
        Fila(bloque, "BOTELLAS", $"{partida.Recogidas:00} / {partida.Total:00}", 34, EstiloUI.Oro);
        Fila(bloque, "PUNTOS", partida.Puntos.ToString("0000"), -18, EstiloUI.Verde);
        Fila(bloque, "VIDA", Mathf.RoundToInt(partida.Vida * 100f) + "%", -70, EstiloUI.Rojo);
    }

    private static void Fila(Transform padre, string etiqueta, string valor, float y, Color color)
    {
        var nombre = Texto(padre, "Etiqueta" + etiqueta, etiqueta, 0, y, 360, 40, 22, EstiloUI.Crema);
        nombre.alignment = TextAlignmentOptions.Left;
        var cifra = Texto(padre, "Valor" + etiqueta, valor, 0, y, 360, 44, 34, color);
        cifra.alignment = TextAlignmentOptions.Right;
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
                boton.GetComponentInChildren<TMP_Text>().text = TextoAyudaHUD().ToUpperInvariant();
    }

    /// <summary>Opción de solo texto con franja inclinada; el realce lo anima RealceBotonMenu.</summary>
    private Button BotonPixel(Transform padre, string valor, float y, UnityEngine.Events.UnityAction accion, float ancho = 420, bool centrado = false)
    {
        var rect = Caja(padre, valor, 0, y, ancho, 54, new Color(0, 0, 0, 0));
        var imagen = rect.GetComponent<Image>();
        imagen.raycastTarget = true;
        var franja = Rect(rect, "Franja", Vector2.zero, Vector2.one);
        franja.offsetMin = new Vector2(-18, 3);
        franja.offsetMax = new Vector2(-6, -3);
        var forma = franja.gameObject.AddComponent<FormaUIGraphic>();
        forma.Inclinacion = 14;
        forma.color = EstiloUI.Magenta;
        forma.Degradado = !centrado;
        forma.ColorB = new Color(EstiloUI.Magenta.r, EstiloUI.Magenta.g, EstiloUI.Magenta.b, 0);
        forma.raycastTarget = false;
        var etiqueta = Rect(rect, "Etiqueta", Vector2.zero, Vector2.one);
        if (!centrado) etiqueta.offsetMin = new Vector2(8, 0);
        var texto = etiqueta.gameObject.AddComponent<TextMeshProUGUI>();
        EstiloUI.Aplicar(texto, EstiloUI.Fuente.Hud, .25f);
        texto.text = valor.ToUpperInvariant();
        texto.alignment = centrado ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 20;
        texto.fontSizeMax = 34;
        texto.raycastTarget = false;
        var boton = rect.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;
        boton.transition = Selectable.Transition.None;
        rect.gameObject.AddComponent<RealceBotonMenu>().Configurar(texto, forma, centrado ? 0f : 14f);
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
        Fondo(transform, "Oscuridad", new Color(Tinta.r, Tinta.g, Tinta.b, victoria ? .55f : .84f));
        var banda = Rect(transform, "BandaTitulo", new Vector2(0, .40f), new Vector2(1, .70f));
        banda.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .74f);
        Color color = victoria ? EstiloUI.Oro : EstiloUI.Rojo;
        // Filos de color que enmarcan la banda del título.
        foreach (float borde in new[] { 0f, 1f })
        {
            var filo = Rect(banda, "Filo", new Vector2(0, borde), new Vector2(1, borde));
            filo.sizeDelta = new Vector2(0, 5);
            var imagenFilo = filo.gameObject.AddComponent<Image>();
            imagenFilo.color = color;
            imagenFilo.raycastTarget = false;
        }
        var titulo = Texto(banda, "TituloResultado", victoria ? "mission passed!" : "Has muerto", 0, victoria ? 34 : 0, 1100, 110, victoria ? 92 : 104, color);
        titulo.alignment = TextAlignmentOptions.Center;
        titulo.characterSpacing = victoria ? 0 : 6;
        AjustarAnchoTitulo(titulo);
        EstiloSanAndreas(titulo);
        if (victoria)
        {
            var respeto = Texto(banda, "RespetoVictoria", "RESPECT + 99", 0, -52, 1100, 72, 56, Color.white);
            respeto.alignment = TextAlignmentOptions.Center;
            AjustarAnchoTitulo(respeto);
            EstiloSanAndreas(respeto);
        }
        var tituloGrupo = banda.gameObject.AddComponent<CanvasGroup>();
        var rectOpciones = Rect(transform, "OpcionesResultado", new Vector2(.5f, .24f), new Vector2(.5f, .24f));
        rectOpciones.sizeDelta = new Vector2(600, 290);
        opciones = rectOpciones.gameObject.AddComponent<CanvasGroup>();
        var detalle = Texto(rectOpciones, "Resumen", resumen, 0, 76, 600, 132, 24, EstiloUI.Crema);
        detalle.alignment = TextAlignmentOptions.Center;
        detalle.textWrappingMode = TextWrappingModes.Normal;
        detalle.lineSpacing = -12;
        primerBoton = BotonPixel(rectOpciones, "Volver a jugar", -26, () => Cargar(EscenaJuego), 440, true);
        BotonPixel(rectOpciones, "Ir al menú principal", -93, () => Cargar(EscenaMenu), 440, true);
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
        Sonido.Asignar(fuente, "Efectos");
        fuente.Play();
        StartCoroutine(CortarSonido(fuente, 1.2f));
    }

    private static IEnumerator CortarSonido(AudioSource fuente, float segundos)
    {
        yield return new WaitForSecondsRealtime(segundos);
        if (fuente != null) fuente.Stop();
    }

    /// <summary>Rótulo de cierre de misión: fuente de títulos con contorno grueso y sombra.</summary>
    private static void EstiloSanAndreas(TMP_Text texto)
    {
        texto.fontStyle = FontStyles.Normal;
        EstiloUI.Aplicar(texto, EstiloUI.Fuente.Titulo, .3f);
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
        var marca = Rect(rect, "Marca", new Vector2(0, 1), new Vector2(0, 1));
        marca.pivot = new Vector2(0, 1);
        marca.anchoredPosition = new Vector2(56, -36);
        marca.sizeDelta = new Vector2(700, 70);
        marca.localRotation = Quaternion.Euler(0, 0, 2f);
        var textoMarca = marca.gameObject.AddComponent<TextMeshProUGUI>();
        EstiloUI.Aplicar(textoMarca, EstiloUI.Fuente.Titulo, .3f);
        textoMarca.text = $"SUPER CURAO <color={EstiloUI.HexRosa}>MAN</color>";
        textoMarca.fontSize = 54;
        textoMarca.color = EstiloUI.Oro;
        textoMarca.alignment = TextAlignmentOptions.Left;
        textoMarca.raycastTarget = false;

        var consejo = Texto(rect, "Consejo", $"<color={EstiloUI.HexOro}>CONSEJO</color>\n" + Consejos[Random.Range(0, Consejos.Length)],
            0, 10, 860, 150, 26, EstiloUI.Crema);
        EstiloUI.Aplicar(consejo, EstiloUI.Fuente.Cuerpo, .2f);
        consejo.fontStyle = FontStyles.Bold;
        consejo.alignment = TextAlignmentOptions.Center;
        consejo.textWrappingMode = TextWrappingModes.Normal;

        estadoCarga = Texto(rect, "EstadoCarga", "CARGANDO…", 0, 0, 800, 56, 40, Color.white);
        var rectEstado = estadoCarga.rectTransform;
        rectEstado.anchorMin = rectEstado.anchorMax = rectEstado.pivot = Vector2.zero;
        rectEstado.anchoredPosition = new Vector2(56, 74);
        estadoCarga.alignment = TextAlignmentOptions.Left;

        var carril = Rect(rect, "Carril", Vector2.zero, new Vector2(1, 0));
        carril.offsetMin = new Vector2(56, 48);
        carril.offsetMax = new Vector2(-56, 66);
        var fondoCarril = carril.gameObject.AddComponent<Image>();
        fondoCarril.color = Color.black;
        fondoCarril.raycastTarget = false;
        var vacio = Rect(carril, "Vacio", Vector2.zero, Vector2.one);
        vacio.offsetMin = Vector2.one * 3f;
        vacio.offsetMax = -Vector2.one * 3f;
        var fondoVacio = vacio.gameObject.AddComponent<Image>();
        fondoVacio.color = Color.Lerp(EstiloUI.Oro, Color.black, .72f);
        fondoVacio.raycastTarget = false;
        var barra = Rect(vacio, "Progreso", Vector2.zero, Vector2.one);
        barra.pivot = new Vector2(0, .5f);
        barra.localScale = new Vector3(0, 1, 1);
        progresoCarga = barra.gameObject.AddComponent<Image>();
        progresoCarga.color = EstiloUI.Oro;
        progresoCarga.raycastTarget = false;
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
        EstiloUI.Aplicar(text, EstiloUI.Fuente.Hud, .22f);
        text.text = valor;
        text.fontSize = tamano;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Left;
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
