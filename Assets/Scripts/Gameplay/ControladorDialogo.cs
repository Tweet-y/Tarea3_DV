using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Caja de diálogo con máquina de escribir. Espacio o A adelantan.
/// Si existe Resources/Voces/&lt;voz&gt; (un clip grabado o generado por TTS), se reproduce con la línea;
/// si no, cada personaje "balbucea" con un pitido de tono propio mientras se escribe el texto.
/// </summary>
public sealed class ControladorDialogo : MonoBehaviour
{
    public static bool Saltar;
    public Image Negro { get; private set; }
    CanvasGroup panel;
    TMP_Text texto;
    TMP_Text indicacion;
    TMP_Text nombre;
    Image fichaNombre, bordeCaja;
    AudioSource fuente;
    AudioClip pitido;
    float tonoVoz = 1f;
    bool esperando;

    public static ControladorDialogo Crear(bool conFundido)
    {
        var objeto = new GameObject("Dialogo", typeof(RectTransform));
        var dialogo = objeto.AddComponent<ControladorDialogo>();
        var canvas = objeto.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var escala = objeto.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1280, 720);
        escala.matchWidthOrHeight = .5f;
        if (conFundido)
        {
            dialogo.Negro = Rect("Entrada", objeto.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            dialogo.Negro.color = Color.black;
            dialogo.Negro.raycastTarget = false;
        }
        // Grupo que se desvanece: caja + ficha con el nombre encima del borde superior.
        var grupo = Rect("Dialogo", objeto.transform, new Vector2(.1f, .05f), new Vector2(.9f, .29f));
        dialogo.panel = grupo.gameObject.AddComponent<CanvasGroup>();
        dialogo.panel.alpha = 0f;
        var caja = Rect("Caja", grupo, Vector2.zero, Vector2.one);
        EstiloCurao.Marco(caja, EstiloCurao.Panel, EstiloCurao.Ambar, 4f);
        dialogo.bordeCaja = caja.Find("BordeArriba").GetComponent<Image>();

        var ficha = Rect("Ficha", grupo, new Vector2(0f, 1f), new Vector2(0f, 1f));
        ficha.pivot = new Vector2(0f, .5f);
        ficha.anchoredPosition = new Vector2(26f, 0f);
        ficha.sizeDelta = new Vector2(180f, 40f);
        dialogo.fichaNombre = EstiloCurao.Marco(ficha, EstiloCurao.Ambar, EstiloCurao.Noche, 3f);
        dialogo.nombre = Texto("Nombre", ficha, Vector2.zero, Vector2.one, 34, false);
        dialogo.nombre.alignment = TextAlignmentOptions.Center;
        dialogo.nombre.color = EstiloCurao.Noche;
        dialogo.nombre.text = "CJ";

        dialogo.texto = Texto("Texto", caja, new Vector2(.03f, .2f), new Vector2(.97f, .84f), 34, true);
        dialogo.texto.enableAutoSizing = true;
        dialogo.texto.fontSizeMin = 24;
        dialogo.texto.fontSizeMax = 34;
        dialogo.texto.textWrappingMode = TextWrappingModes.Normal;
        dialogo.texto.alignment = TextAlignmentOptions.TopLeft;
        dialogo.indicacion = Texto("Continuar", caja, new Vector2(.03f, .04f), new Vector2(.97f, .2f), 22, true);
        dialogo.indicacion.text = "ESPACIO / A  ·  SALTAR";
        dialogo.indicacion.alignment = TextAlignmentOptions.Right;
        dialogo.indicacion.color = EstiloCurao.TextoTenue;

        dialogo.fuente = objeto.AddComponent<AudioSource>();
        dialogo.fuente.playOnAwake = false;
        dialogo.fuente.spatialBlend = 0f;
        Sonido.Asignar(dialogo.fuente, "Voces");
        dialogo.pitido = CrearPitido();
        dialogo.FijarNombre("CJ");
        return dialogo;
    }

    public void FijarNombre(string quien)
    {
        quien = (quien ?? "").ToUpperInvariant();
        nombre.text = quien;
        Color color = EstiloCurao.ColorPersonaje(quien);
        fichaNombre.color = color;
        bordeCaja.color = color;
        // Tono de "balbuceo" distinto por personaje: Nemesis grave, el resto más agudo.
        tonoVoz = quien == "NEMESIS" ? .55f : quien == "CJ" ? 1.05f : 1.25f;
        var rect = fichaNombre.rectTransform;
        rect.sizeDelta = new Vector2(Mathf.Max(140f, 40f + quien.Length * 20f), rect.sizeDelta.y);
    }

    /// <param name="voz">Nombre del clip en Resources/Voces (sin extensión). Opcional.</param>
    public IEnumerator Revelar(string linea, float segundosLectura, string voz = null)
    {
        panel.alpha = 1f;
        texto.text = linea;
        texto.maxVisibleCharacters = 0;
        texto.ForceMeshUpdate();
        int caracteres = texto.textInfo.characterCount;
        if (Saltar)
        {
            texto.maxVisibleCharacters = caracteres;
            yield break;
        }
        var clip = string.IsNullOrEmpty(voz) ? null : Resources.Load<AudioClip>("Voces/" + voz);
        if (clip != null)
        {
            fuente.pitch = 1f;
            fuente.clip = clip;
            fuente.Play();
        }
        esperando = false;
        indicacion.text = "ESPACIO / A  ·  SALTAR";
        float reveladas = 0f;
        int ultimoSonido = -1;
        while (reveladas < caracteres)
        {
            if (Avanzar()) reveladas = caracteres;
            else reveladas += Time.deltaTime * 38f;
            int visibles = Mathf.Min(caracteres, Mathf.FloorToInt(reveladas));
            texto.maxVisibleCharacters = visibles;
            if (clip == null && visibles / 3 != ultimoSonido && visibles < caracteres)
            {
                ultimoSonido = visibles / 3;
                fuente.pitch = tonoVoz * Random.Range(.92f, 1.08f);
                fuente.PlayOneShot(pitido, .22f);
            }
            yield return null;
        }
        esperando = true;
        indicacion.text = "ESPACIO / A  ·  CONTINUAR  >";
        // Con voz, darle tiempo a terminar la frase antes de pasar sola a la siguiente.
        float espera = clip != null ? Mathf.Max(segundosLectura, clip.length - caracteres / 38f + .4f) : segundosLectura;
        for (float t = 0f; t < espera; t += Time.deltaTime)
        {
            if (t > .25f && Avanzar()) break;
            yield return null;
        }
        esperando = false;
        if (clip != null && fuente.clip == clip) fuente.Stop();
    }

    public void Ocultar()
    {
        if (panel != null) panel.alpha = 0f;
        if (fuente != null) fuente.Stop();
    }

    public void Cerrar()
    {
        if (this != null) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (pitido != null) Destroy(pitido);
    }

    public static bool Avanzar()
    {
        if (Saltar) return true;
        var partida = ControladorPartidaBotellas.Instancia;
        if (Time.timeScale <= 0f || (partida != null && partida.EstaPausada)) return false;
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    void Update()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        var partida = ControladorPartidaBotellas.Instancia;
        canvas.enabled = partida == null || !partida.EstaPausada;
        // Parpadeo suave del aviso cuando la línea ya terminó de escribirse.
        if (indicacion != null)
            indicacion.alpha = esperando ? .55f + .45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) : .7f;
    }

    static AudioClip CrearPitido()
    {
        const int frecuencia = 22050;
        var muestras = new float[frecuencia / 25];
        for (int i = 0; i < muestras.Length; i++)
        {
            float t = (float)i / frecuencia;
            float onda = Mathf.Sign(Mathf.Sin(t * 420f * Mathf.PI * 2f)) * .5f + Mathf.Sin(t * 840f * Mathf.PI * 2f) * .25f;
            muestras[i] = onda * Mathf.Exp(-t * 55f) * .5f;
        }
        var clip = AudioClip.Create("BalbuceoDialogo", muestras.Length, 1, frecuencia, false);
        clip.SetData(muestras, 0);
        return clip;
    }

    static RectTransform Rect(string nombre, Transform padre, Vector2 minimo, Vector2 maximo)
    {
        var rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = minimo;
        rect.anchorMax = maximo;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static TMP_Text Texto(string nombre, Transform padre, Vector2 minimo, Vector2 maximo, int tamano, bool contorno)
    {
        var etiqueta = Rect(nombre, padre, minimo, maximo).gameObject.AddComponent<TextMeshProUGUI>();
        EstiloCurao.Aplicar(etiqueta, tamano, EstiloCurao.Texto, contorno ? .18f : 0f);
        etiqueta.alignment = TextAlignmentOptions.Left;
        return etiqueta;
    }
}
