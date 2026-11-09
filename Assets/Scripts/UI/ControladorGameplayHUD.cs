using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>HUD compacto de cerveza, ebriedad y resistencia al desmayo.</summary>
public sealed class ControladorGameplayHUD : MonoBehaviour
{
    private ControladorPartidaBotellas partida;
    private RectTransform ebriedadFill, vidaFill, icono;
    private TMP_Text contador, restantes, aviso, feedback, puntos, vidaEtiqueta, etiquetaMedidor, inventarioHud, vidaJefe;
    private RectTransform vidaJefeFill;
    private TMP_Text municion, nombreArma;
    private GameObject imagenBotella, imagenArma, marcoRecarga;
    private RectTransform rellenoRecarga;
    private float objetivoEbriedad, objetivoVida = 1f, visualEbriedad, visualVida = 1f;
    private float tiempoPickup;
    private RectTransform marcoMapa, vidaMapa, ebriedadMapa;
    private RectTransform objetivoInicial;
    private TMP_Text indicadorAyuda;
    private MapaCiudadGraphic mapaCiudad;
    private CanvasGroup visibilidadMapa;
    private ControladorArmaFuego armaJugador;
    private const string ClaveAyuda = "HUD.IndicacionesVisibles";
    private static bool? ayudaVisible;
    public static bool IndicacionesVisibles => ayudaVisible ??= PlayerPrefs.GetInt(ClaveAyuda, 1) != 0;

    public static void AlternarIndicaciones()
    {
        ayudaVisible = !IndicacionesVisibles;
        PlayerPrefs.SetInt(ClaveAyuda, ayudaVisible.Value ? 1 : 0);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarAyuda() => ayudaVisible = null;
    private Canvas lienzo;
    private readonly List<Material> materialesTexto = new List<Material>();

    public void Inicializar(ControladorPartidaBotellas fuente)
    {
        partida = fuente;
        Construir();
        ConectarEventos();
        Actualizar();
    }

    private void OnEnable()
    {
        if (partida == null) return;
        ConectarEventos();
        Actualizar();
    }

    private void ConectarEventos()
    {
        DesconectarEventos();
        partida.EstadoActualizado += Actualizar;
        partida.BotellaRecogida += Recogida;
        partida.BotellaCurativaRecogida += CuracionRecogida;
        partida.PartidaTerminada += MostrarResultado;
    }

    private void OnDisable() => DesconectarEventos();

    private void DesconectarEventos()
    {
        if (partida == null) return;
        partida.EstadoActualizado -= Actualizar;
        partida.BotellaRecogida -= Recogida;
        partida.BotellaCurativaRecogida -= CuracionRecogida;
        partida.PartidaTerminada -= MostrarResultado;
    }

    private void OnDestroy()
    {
        if (partida != null)
        {
            partida.EstadoActualizado -= Actualizar;
            partida.BotellaRecogida -= Recogida;
            partida.PartidaTerminada -= MostrarResultado;
        }
        foreach (var material in materialesTexto) Destroy(material);
    }

    private void Construir()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        ConstruirMapa();

        var root = Nodo(transform, "GameplayHUD", new Vector2(1, 1), new Vector2(-30, -26), new Vector2(280, 332));
        root.pivot = new Vector2(1, 1);
        icono = Nodo(root, "BottleIcon", new Vector2(0, 1), Vector2.zero, new Vector2(88, 90));
        Caja(icono, "BlackFrame", Vector2.zero, new Vector2(88, 90), Color.black);
        Caja(icono, "WhiteInset", new Vector2(4, -4), new Vector2(80, 82), new Color32(210, 214, 209, 255));
        var botella = Nodo(icono, "BeerSilhouette", new Vector2(0, 1), new Vector2(13, -8), new Vector2(62, 74));
        botella.gameObject.AddComponent<BottleHudGraphic>().raycastTarget = false;
        imagenBotella = botella.gameObject;
        var rifle = Nodo(icono, "MiniaturaArma", new Vector2(0, 1), new Vector2(5, -5), new Vector2(78, 80));
        rifle.gameObject.AddComponent<IconoRifleGraphic>().raycastTarget = false;
        imagenArma = rifle.gameObject;
        imagenArma.SetActive(false);

        municion = Texto(root, "MunicionArma", "--", new Vector2(99, 0), new Vector2(181, 38), 30, Color.white);
        nombreArma = Texto(root, "NombreArma", "SIN ARMA", new Vector2(99, -44), new Vector2(181, 22), 17, Color.white);
        rellenoRecarga = Barra(root, "ProgresoRecarga", new Vector2(99, -76), new Vector2(181, 10), new Color32(232, 180, 68, 255));
        marcoRecarga = rellenoRecarga.parent.parent.gameObject;
        marcoRecarga.SetActive(false);
        contador = Texto(root, "BottleCounter", $"BOTELLAS 00 / {partida.Total:00}", new Vector2(0, -104), new Vector2(280, 28), 24, Color.white);
        vidaEtiqueta = Texto(root, "HealthLabel", "VIDA 100%", new Vector2(0, -142), new Vector2(280, 20), 17, Color.white);
        vidaFill = Barra(root, "PlayerHealth", new Vector2(0, -166), new Vector2(280, 20), new Color32(188, 37, 45, 255));
        etiquetaMedidor = Texto(root, "DrunkLabel", "EBRIEDAD", new Vector2(0, -198), new Vector2(280, 18), 17, Color.white);
        ebriedadFill = Barra(root, "DrunkMeter", new Vector2(0, -220), new Vector2(280, 16), new Color32(205, 205, 205, 255));
        restantes = Texto(root, "RemainingText", $"QUEDAN {partida.Total}", new Vector2(0, -246), new Vector2(280, 28), 24, new Color32(82, 142, 72, 255));
        aviso = Texto(root, "DangerStatus", "SOBRIO", new Vector2(0, -280), new Vector2(280, 20), 16, Color.white);
        puntos = Texto(root, "Score", "PUNTOS 0000", new Vector2(0, -306), new Vector2(280, 22), 17, Color.white);
        feedback = Texto(root, "BottlePickupFeedback", "+1", new Vector2(0, -104), new Vector2(40, 26), 20, Color.white);
        feedback.gameObject.SetActive(false);

        indicadorAyuda = Texto(transform, "AtajoIndicaciones", "", new Vector2(24, -24), new Vector2(450, 28), 16, new Color32(250, 215, 74, 255));
        indicadorAyuda.alignment = TextAlignmentOptions.Left;
        objetivoInicial = Nodo(transform, "ObjetivoInicial", new Vector2(0, 1),
            new Vector2(24, -66), new Vector2(430, 268));
        var fondoObjetivo = objetivoInicial.gameObject.AddComponent<Image>();
        fondoObjetivo.color = new Color32(17, 21, 20, 225);
        fondoObjetivo.raycastTarget = false;
        var objetivo = Texto(objetivoInicial, "TextoObjetivo",
            "OBJETIVO\nConsigue las 30 botellas de la isla.\nLuego ve a muelle_final con CJ.\n\nCONTROLES\nWASD: mover · Espacio: saltar · Shift: correr\n1 bate · 2 rifle · Clic izq.: golpear o disparar\nQ botiquín · F bebida · E recoger · Esc pausa",
            Vector2.zero, Vector2.zero, 18, Color.white);
        objetivo.rectTransform.anchorMin = Vector2.zero;
        objetivo.rectTransform.anchorMax = Vector2.one;
        objetivo.rectTransform.offsetMin = new Vector2(18, 16);
        objetivo.rectTransform.offsetMax = new Vector2(-18, -16);
        objetivo.alignment = TextAlignmentOptions.TopLeft;
        objetivo.characterSpacing = 0;
        objetivo.textWrappingMode = TextWrappingModes.Normal;

    }

    private void ConstruirMapa()
    {
        marcoMapa = Nodo(transform, "MapaCiudad", Vector2.zero, new Vector2(24, 24), new Vector2(256, 296));
        marcoMapa.pivot = Vector2.zero;
        visibilidadMapa = marcoMapa.gameObject.AddComponent<CanvasGroup>();
        visibilidadMapa.interactable = false;
        visibilidadMapa.blocksRaycasts = false;
        var borde = Nodo(marcoMapa, "BordeRadar", Vector2.zero, Vector2.zero, Vector2.zero);
        borde.anchorMin = Vector2.zero; borde.anchorMax = Vector2.one;
        borde.offsetMin = new Vector2(8, 44); borde.offsetMax = new Vector2(-8, -12);
        var disco = borde.gameObject.AddComponent<MapaDiscoGraphic>();
        disco.color = Color.black;
        disco.raycastTarget = false;
        var mascara = Nodo(borde, "MascaraCircular", Vector2.zero, Vector2.zero, Vector2.zero);
        mascara.anchorMin = Vector2.zero; mascara.anchorMax = Vector2.one;
        mascara.offsetMin = Vector2.one * 10f; mascara.offsetMax = -Vector2.one * 10f;
        mascara.gameObject.AddComponent<MapaDiscoGraphic>().raycastTarget = false;
        mascara.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var mapa = Nodo(mascara, "PlanoCiudad", Vector2.zero, Vector2.zero, Vector2.zero);
        mapa.anchorMin = Vector2.zero; mapa.anchorMax = Vector2.one;
        mapa.offsetMin = mapa.offsetMax = Vector2.zero;
        mapaCiudad = mapa.gameObject.AddComponent<MapaCiudadGraphic>();
        mapaCiudad.Inicializar(partida, marcoMapa);
        var cj = Texto(borde, "IdentificadorCJ", "CJ", Vector2.zero, new Vector2(42, 36), 27, Color.white);
        cj.rectTransform.anchorMin = cj.rectTransform.anchorMax = new Vector2(0, .5f);
        cj.rectTransform.pivot = new Vector2(.5f, .5f);
        cj.alignment = TextAlignmentOptions.Center;
        var norte = Texto(borde, "IndicadorNorte", "N", Vector2.zero, new Vector2(36, 36), 27, Color.white);
        norte.rectTransform.anchorMin = norte.rectTransform.anchorMax = new Vector2(.5f, 1);
        norte.rectTransform.pivot = new Vector2(.5f, .5f);
        norte.alignment = TextAlignmentOptions.Center;
        var leyenda = Texto(marcoMapa, "LeyendaMapa", "N ↑   EXPLORA · M: MAPA", new Vector2(8, -256), new Vector2(240, 16), 12, new Color32(250, 215, 74, 255));
        leyenda.rectTransform.anchorMin = leyenda.rectTransform.anchorMax = Vector2.zero;
        leyenda.rectTransform.anchoredPosition = new Vector2(8, 39);
        leyenda.alignment = TextAlignmentOptions.Center;
        vidaMapa = Barra(marcoMapa, "VidaMapa", Vector2.zero, new Vector2(118, 11), new Color32(198, 51, 56, 255));
        var barraVida = (RectTransform)vidaMapa.parent.parent;
        barraVida.anchorMin = barraVida.anchorMax = Vector2.zero;
        barraVida.anchoredPosition = new Vector2(8, 20);
        ebriedadMapa = Barra(marcoMapa, "EbriedadMapa", Vector2.zero, new Vector2(118, 11), new Color32(238, 207, 58, 255));
        var barraEbriedad = (RectTransform)ebriedadMapa.parent.parent;
        barraEbriedad.anchorMin = barraEbriedad.anchorMax = Vector2.zero;
        barraEbriedad.anchoredPosition = new Vector2(130, 20);
    }

    private void Actualizar()
    {
        bool isla = partida.gameObject.scene.name == "MapaV2";
        objetivoEbriedad = isla
            ? (partida.Total <= 0 ? 0f : (float)partida.Recogidas / partida.Total)
            : partida.EbriedadNormalizada;
        objetivoVida = partida.Vida;
        vidaEtiqueta.text = $"VIDA {Mathf.RoundToInt(partida.Vida * 100f)}%";
        contador.text = $"BOTELLAS {partida.Recogidas:00} / {partida.Total:00}";
        restantes.text = $"QUEDAN {partida.Restantes}";
        if (puntos != null) puntos.text = $"PUNTOS {partida.Puntos:0000}";
        if (isla)
        {
            etiquetaMedidor.text = $"BOTELLAS {partida.Recogidas}/{partida.Total}";
            aviso.text = string.IsNullOrEmpty(FinalIsla.Mensaje) ? "JUNTA LAS BOTELLAS" : FinalIsla.Mensaje;
        }
        else
        {
            etiquetaMedidor.text = "EBRIEDAD";
            aviso.text = objetivoEbriedad >= .85f ? "! PELIGRO DE DESMAYO !" :
                objetivoEbriedad >= .6f ? "MUY EBRIO" : objetivoEbriedad >= .3f ? "MAREADO" : "SOBRIO";
        }
        ActualizarJefe();
        ActualizarInventario();
    }

    private void Update()
    {
        if (partida == null) return;
        if (lienzo == null) lienzo = GetComponent<Canvas>();
        if (lienzo != null) lienzo.enabled = !IntroduccionIsla.Activa && !PeleaJefeLancha.Activa;
        bool jugando = partida.Estado == ControladorPartidaBotellas.Resultado.EnCurso;
        if (armaJugador == null) armaJugador = FindFirstObjectByType<ControladorArmaFuego>();
        ActualizarEquipo();
        // Ocultar solo la presentacion: la exploracion del radar sigue funcionando al caminar apuntando.
        if (visibilidadMapa != null)
            visibilidadMapa.alpha = jugando && !(armaJugador != null && armaJugador.SolicitaApuntar) ? 1f : 0f;
        if (jugando && !partida.EstaPausada && !IntroduccionIsla.Activa &&
            Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) AlternarIndicaciones();
        bool mapaAmpliado = mapaCiudad != null && mapaCiudad.Ampliado;
        objetivoInicial.gameObject.SetActive(jugando && IndicacionesVisibles && !partida.EstaPausada && !mapaAmpliado);
        indicadorAyuda.gameObject.SetActive(jugando && !partida.EstaPausada && !mapaAmpliado);
        indicadorAyuda.text = IndicacionesVisibles ? "H: ocultar indicaciones · ESC: opciones" : "H: mostrar indicaciones · ESC: opciones";
        visualEbriedad = Mathf.MoveTowards(visualEbriedad, objetivoEbriedad, Time.unscaledDeltaTime * 2f);
        visualVida = Mathf.MoveTowards(visualVida, objetivoVida, Time.unscaledDeltaTime * 2f);
        ebriedadFill.localScale = new Vector3(visualEbriedad, 1, 1);
        vidaFill.localScale = new Vector3(visualVida, 1, 1);
        vidaMapa.localScale = new Vector3(visualVida, 1, 1);
        ebriedadMapa.localScale = new Vector3(visualEbriedad, 1, 1);
        bool isla = partida.gameObject.scene.name == "MapaV2";
        aviso.color = !isla && objetivoEbriedad >= .85f
            ? Color.Lerp(Color.white, new Color32(255, 90, 75, 255), .5f + .5f * Mathf.Sin(Time.unscaledTime * 7))
            : Color.white;
        if (tiempoPickup > 0)
        {
            tiempoPickup = Mathf.Max(0, tiempoPickup - Time.unscaledDeltaTime);
            contador.rectTransform.localScale = Vector3.one * (1 + .04f * Mathf.Sin(tiempoPickup / .45f * Mathf.PI));
            feedback.gameObject.SetActive(tiempoPickup > 0);
        }
        else contador.rectTransform.localScale = Vector3.one;
    }

    private void ActualizarEquipo()
    {
        bool equipada = armaJugador != null && armaJugador.Equipada && armaJugador.Municion != null;
        imagenBotella.SetActive(!equipada);
        imagenArma.SetActive(equipada);
        var balas = equipada ? armaJugador.Municion : null;
        municion.text = equipada ? $"{balas.Cargador:00} - {balas.Reserva:000}" : "--";
        municion.color = equipada && balas.Cargador == 0 ? new Color32(238, 90, 75, 255) : Color.white;
        nombreArma.text = !equipada ? "SIN ARMA" : balas.Recargando ? $"RECARGANDO {armaJugador.RecargaRestante:0.0}s" : "RIFLE DE ASALTO";
        marcoRecarga.SetActive(equipada && balas.Recargando);
        if (equipada && balas.Recargando)
            rellenoRecarga.localScale = new Vector3(Mathf.Clamp01(1 - armaJugador.RecargaRestante / Mathf.Max(.1f, armaJugador.Definicion.segundosRecarga)), 1, 1);
    }

    void ActualizarInventario()
    {
        var inventario = InventarioJugador.Instancia;
        if (inventarioHud == null)
        {
            inventarioHud = Texto(transform, "Inventario", "", new Vector2(24, 78), new Vector2(520, 28), 18, Color.white);
            inventarioHud.alignment = TextAlignmentOptions.Left;
            inventarioHud.rectTransform.anchorMin = inventarioHud.rectTransform.anchorMax = Vector2.zero;
            inventarioHud.rectTransform.pivot = Vector2.zero;
        }
        if (inventario == null) { inventarioHud.text = ""; return; }
        string arma = inventario.armaActiva == InventarioJugador.Arma.Bate ? "BATE" : "RIFLE";
        inventarioHud.text = $"{arma}   BOTIQUÍN {inventario.botiquines}   BEBIDA {inventario.buffs}" + (inventario.BuffActivo ? "   +DAÑO" : "");
    }

    void ActualizarJefe()
    {
        ControladorVida jefe = null;
        foreach (var vida in FindObjectsByType<ControladorVida>(FindObjectsSortMode.None))
            if (vida.esJefe && !vida.Muerto && PeleaJefeLancha.BarraVisible) jefe = vida;
        if (jefe == null)
        {
            if (vidaJefe != null) vidaJefe.gameObject.SetActive(false);
            return;
        }
        if (vidaJefeFill == null)
        {
            vidaJefe = Texto(transform, "NombreJefe", "NEMESIS", new Vector2(0, -16), new Vector2(420, 24), 18, new Color32(232, 180, 68, 255));
            vidaJefe.alignment = TextAlignmentOptions.Center;
            vidaJefe.rectTransform.anchorMin = vidaJefe.rectTransform.anchorMax = new Vector2(.5f, 1f);
            vidaJefeFill = Barra(vidaJefe.rectTransform, "VidaJefe", new Vector2(0, -28), new Vector2(420, 16), new Color32(166, 42, 33, 255));
        }
        vidaJefe.gameObject.SetActive(true);
        vidaJefeFill.localScale = new Vector3(jefe.Fraccion, 1f, 1f);
    }

    private void Recogida()
    {
        tiempoPickup = .45f;
        feedback.text = "+1";
        feedback.color = Color.white;
    }
    private void CuracionRecogida()
    {
        tiempoPickup = .8f;
        feedback.text = "+VIDA";
        feedback.color = new Color32(65, 180, 255, 255);
        feedback.rectTransform.sizeDelta = new Vector2(90, 26);
    }

    private static string TextoCausa(ControladorPartidaBotellas.Causa causa)
    {
        switch (causa)
        {
            case ControladorPartidaBotellas.Causa.Botellas: return "Recogiste todas las botellas";
            case ControladorPartidaBotellas.Causa.Desmayo: return "Te desmayaste";
            case ControladorPartidaBotellas.Causa.Agua: return "Caíste al agua";
            case ControladorPartidaBotellas.Causa.Enemigo: return "Te atraparon";
            case ControladorPartidaBotellas.Causa.Ninguna: return "";
            default:
                throw new ArgumentOutOfRangeException(nameof(causa), causa, null);
        }
    }

    private void MostrarResultado(ControladorPartidaBotellas.Resultado estado)
    {
        Actualizar();
        transform.Find("GameplayHUD").gameObject.SetActive(false);
        marcoMapa.gameObject.SetActive(false);
        if (objetivoInicial != null) objetivoInicial.gameObject.SetActive(false);
        if (indicadorAyuda != null) indicadorAyuda.gameObject.SetActive(false);
        bool victoria = estado == ControladorPartidaBotellas.Resultado.Victoria;
        var menu = new GameObject("MenuResultado", typeof(RectTransform)).AddComponent<ControladorMenus>();
        menu.ConstruirResultado(victoria,
            $"{partida.Recogidas} / {partida.Total} BOTELLAS\nPUNTOS {partida.Puntos}\n{TextoCausa(partida.CausaFinal)}");
    }

    private RectTransform Barra(Transform parent, string nombre, Vector2 pos, Vector2 size, Color color)
    {
        var frame = Caja(parent, nombre, pos, size, Color.black);
        var inset = Caja(frame, "Empty", new Vector2(3, -3), size - new Vector2(6, 6), new Color32(46, 46, 46, 255));
        var fill = Caja(inset, "Fill", Vector2.zero, size - new Vector2(6, 6), color);
        return fill;
    }

    private static RectTransform Nodo(Transform parent, string nombre, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var obj = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Caja(Transform parent, string nombre, Vector2 pos, Vector2 size, Color color)
    {
        var rect = Nodo(parent, nombre, new Vector2(0, 1), pos, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private TMP_Text Texto(Transform parent, string nombre, string valor, Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var rect = Nodo(parent, nombre, new Vector2(0, 1), pos, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        var material = new Material(text.fontSharedMaterial);
        materialesTexto.Add(material);
        text.fontMaterial = material;
        text.text = valor;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = -1;
        text.color = color;
        text.outlineColor = Color.black;
        text.outlineWidth = .32f;
        text.alignment = TextAlignmentOptions.Right;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}

/// <summary>Icono vectorial original; no depende de caracteres o imágenes externas.</summary>
public sealed class BottleHudGraphic : Graphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var shape = new[] {
            new Vector2(.39f,.94f), new Vector2(.61f,.94f), new Vector2(.61f,.65f),
            new Vector2(.76f,.53f), new Vector2(.76f,.06f), new Vector2(.24f,.06f),
            new Vector2(.24f,.53f), new Vector2(.39f,.65f)
        };
        Polygon(vh, shape, Color.black);
        var inner = new Vector2[shape.Length];
        for (int i = 0; i < shape.Length; i++) inner[i] = new Vector2(.5f,.5f) + (shape[i] - new Vector2(.5f,.5f)) * .83f;
        Polygon(vh, inner, new Color32(55, 111, 53, 255));
        Polygon(vh, new[] {new Vector2(.35f,.23f), new Vector2(.65f,.23f), new Vector2(.65f,.45f), new Vector2(.35f,.45f)}, new Color32(249, 225, 153, 255));
        Polygon(vh, new[] {new Vector2(.36f,.91f), new Vector2(.64f,.91f), new Vector2(.64f,.99f), new Vector2(.36f,.99f)}, Color.black);
        Polygon(vh, new[] {new Vector2(.40f,.94f), new Vector2(.60f,.94f), new Vector2(.60f,.97f), new Vector2(.40f,.97f)}, new Color32(211, 164, 62, 255));
    }

    private void Polygon(VertexHelper vh, Vector2[] points, Color color)
    {
        int first = vh.currentVertCount;
        var rect = rectTransform.rect;
        // Concave bottle shoulder is triangulated around an interior point.
        var center = UIVertex.simpleVert;
        Vector2 average = Vector2.zero;
        foreach (var point in points) average += point;
        average /= points.Length;
        center.position = new Vector2(rect.xMin + rect.width * average.x, rect.yMin + rect.height * average.y);
        center.color = color;
        vh.AddVert(center);
        foreach (var point in points)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = new Vector2(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height);
            vertex.color = color;
            vh.AddVert(vertex);
        }
        for (int i = 0; i < points.Length; i++)
            vh.AddTriangle(first, first + i + 1, first + (i + 1) % points.Length + 1);
    }
}
