using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// QTE del motor: la lancha se detiene y hay que acertar 3 chequeos de habilidad seguidos
/// (aguja giratoria, como en Dead by Daylight). Si fallas, Nemesis sigue lanzando objetos
/// a la lancha detenida y el intento se repite tras unos segundos.
/// </summary>
public sealed partial class PeleaJefeLancha
{
    // Motor y avance por la ruta
    bool motorApagado;
    float factorMotor = 1f, avance, proximoPisoton = 10f, humo;

    // Chequeo de habilidad
    GameObject panelSkill;
    RectTransform agujaSkill, zonaBuenaSkill, zonaGenialSkill;
    Image anilloSkill;
    TMP_Text textoSkill, contadorSkill;
    AudioSource audioUI;
    AudioClip pitido;

    const int ChequeosMotor = 3;

    void ActualizarMotor(float dt)
    {
        factorMotor = Mathf.MoveTowards(factorMotor, motorApagado ? 0f : 1f, dt / 2.2f);
        avance += VelocidadLancha * factorMotor * Mathf.Clamp01(tCombate / Arranque) * dt;
        if (!motorApagado) return;
        // Humo negro saliendo del motor mientras esta muerto.
        humo -= dt;
        if (humo > 0f) return;
        humo = .25f;
        Vector3 popa = lancha.position - lancha.forward * 2.5f + Vector3.up * 1f;
        EfectosPelea.Rafaga(popa, Vector3.up, new Color(.1f, .1f, .1f, .8f), new Color(.25f, .25f, .25f, .6f), 4,
            new Vector2(1f, 2.5f), new Vector2(.8f, 1.6f), new Vector2(1.2f, 2f), -.05f, 20f, 3f);
    }

    void CrearHUDMotor(Transform raiz)
    {
        var rt = new GameObject("SkillCheck", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(raiz, false);
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        // Bajo la mira (se puede seguir disparando) y sobre la barra de calor, sin tocar ninguna.
        rt.anchoredPosition = new Vector2(0f, -115f);
        rt.sizeDelta = new Vector2(170f, 170f);
        panelSkill = rt.gameObject;

        anilloSkill = Radial(rt, "Anillo", new Color(1f, 1f, 1f, .55f), 1f);
        zonaBuenaSkill = Radial(rt, "ZonaBuena", new Color(1f, 1f, 1f, .95f), 0f).rectTransform;
        zonaGenialSkill = Radial(rt, "ZonaGenial", new Color(1f, .85f, .2f, 1f), 0f).rectTransform;

        var aguja = new GameObject("Aguja", typeof(RectTransform)).AddComponent<Image>();
        aguja.transform.SetParent(rt, false);
        aguja.color = new Color(1f, .2f, .15f);
        aguja.raycastTarget = false;
        agujaSkill = aguja.rectTransform;
        agujaSkill.anchorMin = agujaSkill.anchorMax = new Vector2(.5f, .5f);
        agujaSkill.pivot = new Vector2(.5f, 0f);
        agujaSkill.sizeDelta = new Vector2(6f, 100f);
        agujaSkill.anchoredPosition = Vector2.zero;

        textoSkill = Texto(rt, "Tecla", new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(110, 30), 20, Color.white);
        contadorSkill = Texto(rt, "Contador", new Vector2(.5f, .5f), new Vector2(0, -18), new Vector2(110, 22), 14, new Color32(255, 214, 102, 255));
        panelSkill.SetActive(false);

        audioUI = gameObject.AddComponent<AudioSource>();
        audioUI.playOnAwake = false;
        audioUI.spatialBlend = 0f;
        Sonido.Asignar(audioUI, "Efectos");
        pitido = CrearPitido();
    }

    static Image Radial(Transform padre, string nombre, Color color, float relleno)
    {
        var img = new GameObject(nombre, typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(padre, false);
        img.sprite = Anillo;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.fillClockwise = true;
        img.fillAmount = relleno;
        img.color = color;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return img;
    }

    static AudioClip CrearPitido()
    {
        const int frecuencia = 22050;
        var muestras = new float[frecuencia / 8];
        for (int i = 0; i < muestras.Length; i++)
        {
            float t = (float)i / frecuencia;
            muestras[i] = Mathf.Sin(t * 1320f * Mathf.PI * 2f) * Mathf.Exp(-t * 18f) * .35f;
        }
        var clip = AudioClip.Create("PitidoSkillCheck", muestras.Length, 1, frecuencia, false);
        clip.SetData(muestras, 0);
        return clip;
    }

    IEnumerator Esperar(float segundos)
    {
        for (float s = 0f; s < segundos && !PeleaInterrumpida(); s += Time.deltaTime) yield return null;
    }

    IEnumerator QteMotor()
    {
        motorApagado = true;
        Mensaje("¡SE APAGÓ EL MOTOR!", 2f);
        Decir("¡Chucha, se apagó el motor! ¡Ayúdame a hacerlo partir, weón!");
        EfectosPelea.Explosion(lancha.position - lancha.forward * 2.5f + Vector3.up, .6f, false);
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.impacto : null, lancha.position, 1f, "Efectos");
        sacudida = Mathf.Max(sacudida, .8f);
        bool encendido = false;
        while (!encendido && !PeleaInterrumpida())
        {
            yield return Esperar(1.2f);
            int aciertos = 0;
            for (int i = 0; i < ChequeosMotor && !PeleaInterrumpida(); i++)
            {
                bool ok = false;
                yield return ChequeoHabilidad(i, r => ok = r);
                if (!ok) break;
                aciertos++;
                if (i < ChequeosMotor - 1) yield return Esperar(.55f);
            }
            panelSkill.SetActive(false);
            if (aciertos >= ChequeosMotor) encendido = true;
            else if (!PeleaInterrumpida())
            {
                Mensaje("¡EL MOTOR NO PARTE!", 1.5f);
                Decir(Random.value < .5f ? "¡Noo, weón! ¡Otra vez!" : "¡Puta la wea, no parte! ¡Inténtalo de nuevo!");
                // Mientras tanto Nemesis sigue tirando cosas a la lancha detenida.
                yield return Esperar(3f);
            }
        }
        panelSkill.SetActive(false);
        motorApagado = false;
        if (PeleaInterrumpida()) yield break;
        Mensaje("¡PARTIÓ EL MOTOR!", 1.5f);
        Decir("¡Partió, weón! ¡Vámonos!");
        proximoQte = tCombate + Random.Range(24f, 30f);
        proximoPisoton = Mathf.Max(proximoPisoton, tCombate + 4f);
    }

    /// <summary>Una vuelta de aguja: pulsar ESPACIO dentro de la zona blanca (dorada = genial).</summary>
    IEnumerator ChequeoHabilidad(int indice, System.Action<bool> resultado)
    {
        float vuelta = 1.3f - indice * .15f;           // segundos por vuelta: 1,3 · 1,15 · 1,0
        float ancho = 58f - indice * 8f;               // grados de la zona buena
        const float genial = 12f;
        float inicio = Random.Range(110f, 290f - ancho);
        var zonaB = zonaBuenaSkill.GetComponent<Image>();
        var zonaG = zonaGenialSkill.GetComponent<Image>();
        zonaB.fillAmount = ancho / 360f;
        zonaG.fillAmount = genial / 360f;
        zonaBuenaSkill.localEulerAngles = new Vector3(0f, 0f, -inicio);
        zonaGenialSkill.localEulerAngles = new Vector3(0f, 0f, -inicio);
        anilloSkill.color = new Color(1f, 1f, 1f, .55f);
        textoSkill.text = "ESPACIO";
        contadorSkill.text = $"MOTOR {indice + 1}/{ChequeosMotor}";
        agujaSkill.localEulerAngles = Vector3.zero;
        panelSkill.SetActive(true);
        if (pitido != null) audioUI.PlayOneShot(pitido, .8f);
        // Aviso breve, como el "ding" previo de Dead by Daylight.
        yield return Esperar(.35f);
        float angulo = 0f;
        bool acierto = false, decidido = false;
        while (!decidido && !PeleaInterrumpida())
        {
            if (partida.EstaPausada || Time.timeScale <= 0f) { yield return null; continue; }
            angulo += 360f / vuelta * Time.deltaTime;
            agujaSkill.localEulerAngles = new Vector3(0f, 0f, -angulo);
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool pulsa = (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (pulsa)
            {
                decidido = true;
                acierto = angulo >= inicio && angulo <= inicio + ancho;
                if (acierto && angulo <= inicio + genial)
                {
                    Mensaje("¡GENIAL!", .8f);
                    vidaLancha = Mathf.Min(1f, vidaLancha + .03f);
                }
            }
            else if (angulo > inicio + ancho + 8f) decidido = true; // Se paso de la zona sin pulsar.
            yield return null;
        }
        anilloSkill.color = acierto ? new Color(.3f, 1f, .4f, .9f) : new Color(1f, .2f, .15f, .9f);
        if (!acierto) sacudida = Mathf.Max(sacudida, .5f);
        resultado(acierto);
        yield return Esperar(.25f);
    }
}
