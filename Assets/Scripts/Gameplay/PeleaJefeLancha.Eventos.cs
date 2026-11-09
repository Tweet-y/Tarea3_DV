using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// Segunda parte de la pelea final: vida de la lancha y barras del HUD, animaciones procedurales
/// de Nemesis (lanzar, rugir, golpear el suelo, morir), quick time events y la carga de botellas.
/// </summary>
public sealed partial class PeleaJefeLancha
{
    // Lancha
    float vidaLancha = 1f, rastroLancha = 1f, rastroNemesis = 1f, desvioLateral;
    bool hundida;

    // Quick time events
    bool qteActivo, bloqueoDisparo;
    float proximoQte = 22f;
    int tipoQte;
    GameObject panelQte;
    TMP_Text teclaQte, textoQte;
    RectTransform barraQte;
    Image barraQteImg;

    // Barras
    RectTransform rellenoNemesis, rastroNemesisRt, rellenoLancha, rastroLanchaRt;
    Image rellenoLanchaImg;
    TMP_Text textoNemesis, textoLancha;

    AnimadorNemesis animador;

    // ---------------------------------------------------------------- Lancha

    void DanarLancha(float danio)
    {
        if (hundida || fase != Fase.Combate) return;
        vidaLancha = Mathf.Max(0f, vidaLancha - danio);
        danioHasta = Time.time + .6f;
        sacudida = Mathf.Max(sacudida, 1.4f);
        if (vidaLancha <= 0f) hundida = true;
    }

    IEnumerator Hundimiento()
    {
        fase = Fase.Final;
        IntroduccionIsla.Extra = true;
        if (canvas != null) canvas.gameObject.SetActive(false);
        foreach (var r in rocas.ToArray()) if (r != null) Destroy(r.gameObject);
        rocas.Clear();
        Vector3 inicio = lancha.position;
        Quaternion giro0 = lancha.rotation;
        Vector3 camPos = inicio + lancha.right * 10f - lancha.forward * 6f + Vector3.up * 4.5f;
        EfectosPelea.Explosion(inicio + Vector3.up, 2.2f, true);
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, inicio, 1f, "Efectos");
        Decir("¡Nos hundimos, weón! ¡Cagamos!");
        for (float s = 0f; s < 3.6f; s += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, s / 3.6f);
            lancha.position = inicio - Vector3.up * 3.5f * k;
            lancha.rotation = giro0 * Quaternion.Euler(-28f * k, 0f, 12f * k);
            if (Random.value < .2f)
                EfectosPelea.Rafaga(lancha.position + Random.insideUnitSphere * 2f + Vector3.up, Vector3.up, new Color(.15f, .15f, .15f, .8f),
                    new Color(.3f, .3f, .3f, .6f), 10, new Vector2(1f, 3f), new Vector2(1.5f, 3f), new Vector2(1.5f, 2.5f), -.1f, 30f);
            camara.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(lancha.position + Vector3.up - camPos));
            camara.fieldOfView = 50f;
            yield return null;
        }
        IntroduccionIsla.Extra = false;
        BarraVisible = false;
        partida.RecibirDanio(10f, ControladorPartidaBotellas.Causa.Enemigo, false);
    }

    // ---------------------------------------------------------------- HUD de barras y QTE

    void CrearHUDExtra(Transform raiz)
    {
        textoNemesis = Texto(raiz, "NombreNemesis", new Vector2(.5f, 1f), new Vector2(0, -22), new Vector2(640, 30), 24, new Color32(232, 180, 68, 255));
        textoNemesis.text = "NEMESIS";
        (rellenoNemesis, rastroNemesisRt, _) = BarraVida(raiz, new Vector2(.5f, 1f), new Vector2(0, -50), new Vector2(640, 22), new Color(.7f, .1f, .08f));
        // Marcas de fase al 66 % y 33 %.
        foreach (float f in new[] { .33f, .66f })
        {
            var marca = Panel(raiz, "MarcaFase", new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(-320 + 640 * f, -50), new Vector2(3, 22), new Color(0f, 0f, 0f, .8f));
            marca.transform.SetAsLastSibling();
        }

        textoLancha = Texto(raiz, "NombreLancha", new Vector2(0f, 0f), new Vector2(200, 72), new Vector2(340, 28), 20, Color.white);
        Image img;
        (rellenoLancha, rastroLanchaRt, img) = BarraVida(raiz, new Vector2(0f, 0f), new Vector2(200, 46), new Vector2(320, 20), new Color(.2f, .8f, .4f));
        rellenoLanchaImg = img;

        panelQte = Panel(raiz, "QTE", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -130), new Vector2(560, 150), new Color(0f, 0f, 0f, .6f)).gameObject;
        var caja = Panel(panelQte.transform, "Tecla", new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -48), new Vector2(150, 70), new Color(1f, .85f, .3f, .95f));
        teclaQte = Texto(caja.transform, "Letra", new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 70), 40, new Color32(25, 25, 25, 255));
        teclaQte.outlineWidth = 0f;
        textoQte = Texto(panelQte.transform, "Texto", new Vector2(.5f, 0f), new Vector2(0, 42), new Vector2(530, 34), 24, Color.white);
        textoQte.enableAutoSizing = true;
        textoQte.fontSizeMin = 14;
        textoQte.fontSizeMax = 24;
        Panel(panelQte.transform, "BarraFondo", new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0, 14), new Vector2(504, 14), new Color(0f, 0f, 0f, .7f));
        barraQteImg = Panel(panelQte.transform, "Barra", new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(-250, 14), new Vector2(500, 10), new Color(1f, .85f, .3f));
        barraQte = barraQteImg.rectTransform;
        barraQte.pivot = new Vector2(0f, .5f);
        panelQte.SetActive(false);
        CrearHUDMotor(raiz);
    }

    static (RectTransform relleno, RectTransform rastro, Image imagen) BarraVida(Transform raiz, Vector2 ancla, Vector2 pos, Vector2 tam, Color color)
    {
        var fondo = Panel(raiz, "BarraFondo", ancla, ancla, pos, tam + new Vector2(6, 6), new Color(0f, 0f, 0f, .7f));
        RectTransform Hijo(string nombre, Color c, out Image imagen)
        {
            imagen = Panel(fondo.transform, nombre, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(3, 0), tam, c);
            imagen.rectTransform.pivot = new Vector2(0f, .5f);
            return imagen.rectTransform;
        }
        var rastro = Hijo("Rastro", new Color(1f, .95f, .8f, .85f), out _);
        var relleno = Hijo("Relleno", color, out var img);
        return (relleno, rastro, img);
    }

    void ActualizarBarras()
    {
        float dt = Time.deltaTime;
        float fn = nemesis != null ? nemesis.Fraccion : 0f;
        rastroNemesis = Mathf.Max(fn, Mathf.MoveTowards(rastroNemesis, fn, dt * .25f));
        rellenoNemesis.localScale = new Vector3(fn, 1f, 1f);
        rastroNemesisRt.localScale = new Vector3(rastroNemesis, 1f, 1f);
        textoNemesis.text = $"NEMESIS  <size=70%>FASE {faseJefe}</size>";

        rastroLancha = Mathf.Max(vidaLancha, Mathf.MoveTowards(rastroLancha, vidaLancha, dt * .3f));
        rellenoLancha.localScale = new Vector3(vidaLancha, 1f, 1f);
        rastroLanchaRt.localScale = new Vector3(rastroLancha, 1f, 1f);
        var c = Color.Lerp(new Color(.9f, .15f, .1f), new Color(.2f, .8f, .4f), Mathf.InverseLerp(.2f, .7f, vidaLancha));
        if (vidaLancha < .3f && Mathf.Repeat(Time.time * 3f, 1f) < .5f) c = Color.white;
        rellenoLanchaImg.color = c;
        textoLancha.text = $"LANCHA DE CJ  {Mathf.CeilToInt(vidaLancha * 100f)}%";
    }

    void MostrarQte(string tecla, string texto, float progreso, Color color)
    {
        if (panelQte == null) return;
        panelQte.SetActive(true);
        teclaQte.text = tecla;
        textoQte.text = texto;
        barraQte.localScale = new Vector3(Mathf.Clamp01(progreso), 1f, 1f);
        barraQteImg.color = color;
        float pulso = 1f + Mathf.Sin(Time.time * 14f) * .06f;
        teclaQte.transform.parent.localScale = Vector3.one * pulso;
    }

    void OcultarQte()
    {
        if (panelQte != null) panelQte.SetActive(false);
    }

    // ---------------------------------------------------------------- Utilidades de animacion

    /// <summary>Corre f(k) con k suavizado de 0 a 1; se detiene si la pelea termina.</summary>
    IEnumerator Tween(float segundos, System.Action<float> f, System.Func<bool> abortar = null)
    {
        for (float s = 0f; s < segundos; s += Time.deltaTime)
        {
            if (abortar != null && abortar()) yield break;
            f(Mathf.SmoothStep(0f, 1f, s / segundos));
            yield return null;
        }
        f(1f);
    }

    bool PeleaInterrumpida() => nemesis == null || nemesis.Muerto || fase != Fase.Combate;

    Vector3 FrenteNemesis()
    {
        Vector3 f = GeografiaIsla.Plano(lancha.position - nemesis.transform.position);
        return f.sqrMagnitude > .01f ? f.normalized : nemesis.transform.forward;
    }

    IEnumerator Rugido()
    {
        if (animador == null || lanzando) yield break;
        Vector3 frente = FrenteNemesis();
        Vector3 lado = Vector3.Cross(Vector3.up, frente);
        float L = animador.LargoBrazo;
        Vector3 hD = animador.Hombro(true), hI = animador.Hombro(false);
        Vector3 manoD0 = animador.Mano(true), manoI0 = animador.Mano(false);
        yield return Tween(.5f, k =>
        {
            animador.inclinacion = Mathf.Lerp(0f, -22f, k);
            animador.pesoDer = animador.pesoIzq = k;
            animador.objetivoDer = Vector3.Lerp(manoD0, hD + lado * L * .8f + Vector3.up * L * .45f, k);
            animador.objetivoIzq = Vector3.Lerp(manoI0, hI - lado * L * .8f + Vector3.up * L * .45f, k);
        });
        yield return new WaitForSeconds(1f);
        yield return Tween(.6f, k =>
        {
            animador.inclinacion = Mathf.Lerp(-22f, 0f, k);
            animador.pesoDer = animador.pesoIzq = 1f - k;
        });
        animador.Soltar();
    }

    // ---------------------------------------------------------------- Lanzamientos

    IEnumerator Lanzar()
    {
        lanzando = true;
        int cantidad = faseJefe;
        float tamano = Mathf.Clamp(altura * .09f, 3f, 6.5f) * (faseJefe == 3 ? 1.15f : 1f);
        if (materialRoca == null)
        {
            materialRoca = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "RocaNemesis" };
            materialRoca.SetColor("_BaseColor", new Color(.36f, .33f, .3f));
            materialRoca.SetFloat("_Smoothness", .1f);
        }
        for (int i = 0; i < cantidad; i++)
        {
            if (PeleaInterrumpida()) break;
            GameObject modelo = ModeloAlAzar(false);
            bool explosivo = modelo != null && EsExplosivo(modelo.name);
            float vidaObjeto = (faseJefe == 3 ? 6f : 5f) * (modelo != null && EsPesado(modelo.name) ? 1.5f : 1f);
            yield return LanzarUno(modelo, tamano, vidaObjeto, faseJefe == 3 || explosivo, i == 0);
        }
        if (animador != null) animador.Soltar();
        lanzando = false;
        float pausa = faseJefe == 1 ? 4.2f : faseJefe == 2 ? 3.4f : 2.8f;
        proximoLanzamiento = tCombate + pausa;
    }

    GameObject ModeloAlAzar(bool pesado)
    {
        if (catalogo == null || catalogo.objetos == null || catalogo.objetos.Length == 0) return null;
        for (int intento = 0; intento < 12; intento++)
        {
            var m = catalogo.objetos[Random.Range(0, catalogo.objetos.Length)];
            if (m != null && (!pesado || EsPesado(m.name))) return m;
        }
        return catalogo.objetos[Random.Range(0, catalogo.objetos.Length)];
    }

    /// <summary>Agacharse, arrancar el objeto del suelo, levantarlo sobre la cabeza y lanzarlo.</summary>
    IEnumerator LanzarUno(GameObject modelo, float tamano, float vidaObjeto, bool llamas, bool comentar)
    {
        var t = nemesis.transform;
        Vector3 frente = FrenteNemesis();
        Vector3 lado = Vector3.Cross(Vector3.up, frente);
        float L = animador != null ? animador.LargoBrazo : altura * .35f;
        Vector3 hombro = animador != null ? animador.Hombro(true) : t.position + Vector3.up * altura * .8f;
        Vector3 mano0 = animador != null ? animador.Mano(true) : hombro;
        Vector3 agarre = GeografiaIsla.Suelo(t.position + frente * altura * .22f + lado * altura * .16f, altura * .4f);
        RocaNemesis roca = null;
        bool perdida = false;
        System.Func<bool> abortar = () => PeleaInterrumpida() || (roca == null && perdida);

        // 1) Agacharse hacia el objeto.
        yield return Tween(.55f, k =>
        {
            if (animador == null) return;
            animador.inclinacion = Mathf.Lerp(0f, 45f, k);
            animador.giro = Mathf.Lerp(0f, -15f, k);
            animador.pesoDer = k;
            animador.objetivoDer = Vector3.Lerp(mano0, agarre + Vector3.up * tamano * .5f, k);
        }, PeleaInterrumpida);
        if (PeleaInterrumpida()) yield break;

        // 2) Arrancarlo del suelo con polvo y escombros.
        roca = RocaNemesis.Crear(this, animador != null ? animador.ManoDer : null, agarre - Vector3.up * tamano, modelo, tamano, vidaObjeto, llamas, materialRoca);
        roca.SeguirMano = false;
        rocas.Add(roca);
        perdida = true;
        EfectosPelea.Rafaga(agarre, Vector3.up, new Color(.45f, .38f, .3f, .9f), new Color(.6f, .52f, .42f, .8f), 30,
            new Vector2(3f, 9f), new Vector2(.8f, 2.2f) * (tamano / 3f), new Vector2(.8f, 1.6f), 1.2f, 60f);
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.impacto : null, agarre, .9f, "Efectos");
        var r0 = roca;
        yield return Tween(.35f, k =>
        {
            if (r0 != null) r0.transform.position = Vector3.Lerp(agarre - Vector3.up * tamano, agarre + Vector3.up * tamano * .5f, k);
        }, abortar);
        if (roca == null) { yield return Aturdido(); yield break; }
        if (PeleaInterrumpida()) yield break;
        roca.SeguirMano = true;

        // 3) Levantarlo sobre la cabeza, inclinandose hacia atras para tomar impulso.
        Vector3 arriba = hombro + Vector3.up * L * .8f - frente * L * .45f + lado * L * .15f;
        Vector3 desde = animador != null ? animador.objetivoDer : agarre;
        yield return Tween(.6f, k =>
        {
            if (animador == null) return;
            animador.inclinacion = Mathf.Lerp(45f, -18f, k);
            animador.giro = Mathf.Lerp(-15f, -35f, k);
            animador.objetivoDer = Vector3.Lerp(desde, arriba, k);
        }, abortar);
        if (roca == null) { yield return Aturdido(); yield break; }
        if (PeleaInterrumpida()) yield break;
        yield return new WaitForSeconds(.15f);
        if (roca == null) { yield return Aturdido(); yield break; }

        // 4) Lanzarlo con todo el cuerpo.
        Vector3 suelta = hombro + frente * L * .95f + Vector3.up * L * .25f;
        bool lanzada = false;
        var r1 = roca;
        yield return Tween(.3f, k =>
        {
            if (animador != null)
            {
                animador.inclinacion = Mathf.Lerp(-18f, 30f, k);
                animador.giro = Mathf.Lerp(-35f, 20f, k);
                animador.objetivoDer = Vector3.Lerp(arriba, suelta, k);
            }
            if (!lanzada && k >= .55f && r1 != null)
            {
                lanzada = true;
                Soltar(r1);
            }
        }, PeleaInterrumpida);
        if (!lanzada && roca != null && !PeleaInterrumpida()) Soltar(roca);
        if (comentar && Random.value < .35f && modelo != null) Decir(Frase(modelo.name));

        // 5) Recuperar la postura.
        yield return Tween(.55f, k =>
        {
            if (animador == null) return;
            animador.inclinacion = Mathf.Lerp(30f, 0f, k);
            animador.giro = Mathf.Lerp(20f, 0f, k);
            animador.pesoDer = 1f - k;
        }, PeleaInterrumpida);
    }

    void Soltar(RocaNemesis roca)
    {
        float distancia = GeografiaIsla.Plano(lancha.position - roca.transform.position).magnitude;
        float vuelo = Mathf.Clamp(distancia / (faseJefe == 1 ? 38f : faseJefe == 2 ? 44f : 50f), 2.6f, 6f);
        float error = faseJefe == 1 ? 3f : 1.8f;
        Vector2 desvio = Random.insideUnitCircle * error;
        Vector3 destino = PosicionLancha(tCombate + vuelo) + new Vector3(desvio.x, 0f, desvio.y);
        destino.y = GeografiaIsla.NivelAgua;
        roca.Lanzar(destino, vuelo);
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.swoosh : null, roca.transform.position, 1f, "Efectos");
    }

    /// <summary>Le revientan el objeto en la mano: retrocede tambaleandose.</summary>
    IEnumerator Aturdido()
    {
        if (animador == null) yield break;
        float i0 = animador.inclinacion, g0 = animador.giro;
        yield return Tween(.25f, k =>
        {
            animador.inclinacion = Mathf.Lerp(i0, -30f, k);
            animador.giro = Mathf.Lerp(g0, 25f, k);
        }, PeleaInterrumpida);
        yield return Tween(.9f, k =>
        {
            animador.inclinacion = Mathf.Lerp(-30f, 0f, k);
            animador.giro = Mathf.Lerp(25f, 0f, k);
            animador.pesoDer = animador.pesoIzq = Mathf.Min(animador.pesoDer, 1f - k);
        }, PeleaInterrumpida);
        animador.Soltar();
    }

    // ---------------------------------------------------------------- Quick time events

    /// <summary>0 = pisoton con ola, 1 = objeto gigante.</summary>
    IEnumerator QuickTimeEvent(int tipo)
    {
        qteActivo = true;
        while (lanzando) yield return null;
        if (!PeleaInterrumpida())
        {
            if (tipo == 0) yield return QteOla();
            else yield return QteObjetoGigante();
        }
        OcultarQte();
        bloqueoDisparo = false;
        if (animador != null && !PeleaInterrumpida()) animador.Soltar();
        qteActivo = false;
        // Pisotones frecuentes; los demas eventos, mas espaciados.
        if (tipo == 0) proximoPisoton = tCombate + Random.Range(faseJefe == 3 ? 7f : 9f, faseJefe == 3 ? 10f : 13f);
        else proximoQte = tCombate + Random.Range(24f, 30f);
        proximoLanzamiento = Mathf.Max(proximoLanzamiento, tCombate + 1.5f);
    }

    static bool Tecla(KeyControl a, KeyControl b = null) => (a != null && a.wasPressedThisFrame) || (b != null && b.wasPressedThisFrame);

    /// <summary>Golpea el suelo y levanta una ola: elegir el lado correcto para que CJ esquive.</summary>
    IEnumerator QteOla()
    {
        var t = nemesis.transform;
        Vector3 frente = FrenteNemesis();
        Vector3 lado = Vector3.Cross(Vector3.up, frente);
        float L = animador != null ? animador.LargoBrazo : altura * .35f;
        Vector3 hD = animador != null ? animador.Hombro(true) : t.position + Vector3.up * altura * .8f;
        Vector3 hI = animador != null ? animador.Hombro(false) : hD;
        Vector3 mD = animador != null ? animador.Mano(true) : hD, mI = animador != null ? animador.Mano(false) : hI;
        Mensaje("¡PISOTÓN DE NEMESIS!", 1.5f);
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.grito : null, t.position, 1f, "Voces", .5f);
        // Puños arriba...
        yield return Tween(.7f, k =>
        {
            if (animador == null) return;
            animador.inclinacion = Mathf.Lerp(0f, -20f, k);
            animador.pesoDer = animador.pesoIzq = k;
            animador.objetivoDer = Vector3.Lerp(mD, hD + Vector3.up * L * .85f + lado * L * .15f, k);
            animador.objetivoIzq = Vector3.Lerp(mI, hI + Vector3.up * L * .85f - lado * L * .15f, k);
        }, PeleaInterrumpida);
        if (PeleaInterrumpida()) yield break;
        // ...y al suelo.
        Vector3 golpe = GeografiaIsla.Suelo(t.position + frente * altura * .3f, altura * .4f);
        Vector3 aD = animador != null ? animador.objetivoDer : golpe, aI = animador != null ? animador.objetivoIzq : golpe;
        yield return Tween(.22f, k =>
        {
            if (animador == null) return;
            animador.inclinacion = Mathf.Lerp(-20f, 55f, k);
            animador.objetivoDer = Vector3.Lerp(aD, golpe + lado * altura * .08f, k);
            animador.objetivoIzq = Vector3.Lerp(aI, golpe - lado * altura * .08f, k);
        }, PeleaInterrumpida);
        if (PeleaInterrumpida()) yield break;
        EfectosPelea.Explosion(golpe, altura / 12f, false);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * .2f;
            EfectosPelea.Rafaga(golpe + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * altura * .25f, Vector3.up, new Color(.45f, .38f, .3f, .9f),
                new Color(.6f, .52f, .42f, .8f), 14, new Vector2(4f, 10f), new Vector2(2f, 4f), new Vector2(1f, 2f), .4f, 40f);
        }
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, golpe, 1f, "Efectos");
        sacudida = 1.3f;
        StartCoroutine(Tween(.9f, k =>
        {
            if (animador == null) return;
            animador.inclinacion = Mathf.Lerp(55f, 0f, k);
            animador.pesoDer = animador.pesoIzq = 1f - k;
        }, PeleaInterrumpida));

        // La ola viaja por el agua hacia la lancha.
        bool izquierda = Random.value < .5f;
        float duracion = 2.2f;
        Vector3 centro = GeografiaIsla.Plano(t.position);
        float r0 = Mathf.Min(altura * .4f, GeografiaIsla.Plano(lancha.position - t.position).magnitude * .5f);
        var anillo = new GameObject("OlaNemesis").AddComponent<LineRenderer>();
        anillo.loop = true;
        anillo.positionCount = 96;
        anillo.useWorldSpace = true;
        anillo.widthMultiplier = 2.5f;
        anillo.sharedMaterial = EfectosPelea.MaterialParticulas;
        anillo.startColor = anillo.endColor = new Color(.85f, .95f, 1f, .85f);
        bool resuelto = false, exito = false;
        float espuma = 0f;
        for (float s = 0f; s < duracion; s += Time.deltaTime)
        {
            if (PeleaInterrumpida()) break;
            float distancia = GeografiaIsla.Plano(lancha.position - centro).magnitude;
            float radio = Mathf.Lerp(r0, distancia, s / duracion);
            float y = GeografiaIsla.NivelAgua + .4f;
            for (int i = 0; i < anillo.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / anillo.positionCount;
                anillo.SetPosition(i, new Vector3(centro.x + Mathf.Cos(a) * radio, y, centro.z + Mathf.Sin(a) * radio));
            }
            espuma -= Time.deltaTime;
            if (espuma <= 0f)
            {
                espuma = .12f;
                Vector3 dir = GeografiaIsla.Plano(lancha.position - centro).normalized;
                Vector3 p = centro + Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * dir * radio;
                p.y = GeografiaIsla.NivelAgua;
                EfectosPelea.Salpicadura(p, 1.1f);
            }
            if (!resuelto && !partida.EstaPausada)
            {
                MostrarQte(izquierda ? "◄ A" : "D ►", izquierda ? "¡OLA! ¡Grítale a CJ que gire a la IZQUIERDA!" : "¡OLA! ¡Grítale a CJ que gire a la DERECHA!",
                    1f - s / duracion, new Color(.4f, .8f, 1f));
                var kb = Keyboard.current;
                var pad = Gamepad.current;
                float x = pad != null ? pad.leftStick.ReadValue().x : 0f;
                bool izq = (kb != null && Tecla(kb.aKey, kb.leftArrowKey)) || x < -.6f || (pad != null && pad.dpad.left.wasPressedThisFrame);
                bool der = (kb != null && Tecla(kb.dKey, kb.rightArrowKey)) || x > .6f || (pad != null && pad.dpad.right.wasPressedThisFrame);
                if (izq || der)
                {
                    resuelto = true;
                    exito = izquierda ? izq && !der : der && !izq;
                    OcultarQte();
                    if (exito)
                    {
                        Mensaje("¡ESQUIVADA!", 1.5f);
                        Decir("¡Afírmate, weón!");
                        StartCoroutine(Esquivar(izquierda ? -1f : 1f));
                    }
                    else Mensaje("¡PARA EL OTRO LADO!", 1.2f);
                }
            }
            yield return null;
        }
        OcultarQte();
        Destroy(anillo.gameObject);
        if (PeleaInterrumpida()) yield break;
        Vector3 casco = lancha.position;
        if (exito)
            EfectosPelea.Salpicadura(casco - Vector3.Cross(Vector3.up, rumbo) * (izquierda ? -1f : 1f) * 8f + Vector3.down * (casco.y - GeografiaIsla.NivelAgua), 2f);
        else
        {
            EfectosPelea.Salpicadura(new Vector3(casco.x, GeografiaIsla.NivelAgua, casco.z), 2.5f);
            Mensaje("¡LA OLA NOS GOLPEÓ!", 1.5f);
            DanarLancha(.3f);
        }
    }

    IEnumerator Esquivar(float signo)
    {
        yield return Tween(.7f, k => desvioLateral = signo * 9f * k);
        yield return new WaitForSeconds(.6f);
        yield return Tween(1.2f, k => desvioLateral = signo * 9f * (1f - k));
        desvioLateral = 0f;
    }

    /// <summary>Levanta algo enorme e indestructible: machacar para sobrecargar la .50 y reventarlo en sus manos.</summary>
    IEnumerator QteObjetoGigante()
    {
        var t = nemesis.transform;
        Vector3 frente = FrenteNemesis();
        Vector3 lado = Vector3.Cross(Vector3.up, frente);
        float L = animador != null ? animador.LargoBrazo : altura * .35f;
        float tamano = Mathf.Clamp(altura * .22f, 8f, 16f);
        Vector3 hD = animador != null ? animador.Hombro(true) : t.position + Vector3.up * altura * .8f;
        Vector3 hI = animador != null ? animador.Hombro(false) : hD;
        Vector3 mD = animador != null ? animador.Mano(true) : hD, mI = animador != null ? animador.Mano(false) : hI;
        Vector3 agarre = GeografiaIsla.Suelo(t.position + frente * altura * .25f, altura * .4f);
        Mensaje("¡NEMESIS ARRANCA ALGO ENORME!", 2f);
        Decir("¡¿Qué es ESA WEA?! ¡Dale con todo a la punto cincuenta!");

        yield return Tween(.6f, k =>
        {
            if (animador == null) return;
            animador.inclinacion = Mathf.Lerp(0f, 50f, k);
            animador.pesoDer = animador.pesoIzq = k;
            animador.objetivoDer = Vector3.Lerp(mD, agarre + lado * tamano * .5f + Vector3.up * tamano * .4f, k);
            animador.objetivoIzq = Vector3.Lerp(mI, agarre - lado * tamano * .5f + Vector3.up * tamano * .4f, k);
        }, PeleaInterrumpida);
        if (PeleaInterrumpida()) yield break;

        var modelo = ModeloAlAzar(true);
        var gigante = RocaNemesis.Crear(this, null, agarre - Vector3.up * tamano, modelo, tamano, 99999f, false, materialRoca);
        gigante.SeguirMano = false;
        gigante.Danio = .35f;
        rocas.Add(gigante);
        EfectosPelea.Explosion(agarre, tamano / 5f, false);
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, agarre, 1f, "Efectos");
        sacudida = 1f;

        // Lo sostiene entre las dos manos sobre la cabeza.
        Vector3 encima = (hD + hI) * .5f + Vector3.up * L * .9f - frente * L * .3f;
        Vector3 dD = animador != null ? animador.objetivoDer : agarre, dI = animador != null ? animador.objetivoIzq : agarre;
        float subida = 0f;
        System.Action sostener = () =>
        {
            if (gigante == null) return;
            Vector3 entre = animador != null && animador.Valido ? (animador.Mano(true) + animador.Mano(false)) * .5f : encima;
            gigante.transform.position = Vector3.Lerp(agarre - Vector3.up * tamano, entre + Vector3.up * tamano * .35f, subida);
        };
        yield return Tween(1f, k =>
        {
            subida = Mathf.Clamp01(k * 1.6f);
            if (animador != null)
            {
                animador.inclinacion = Mathf.Lerp(50f, -15f, k);
                animador.objetivoDer = Vector3.Lerp(dD, encima + lado * tamano * .45f, k);
                animador.objetivoIzq = Vector3.Lerp(dI, encima - lado * tamano * .45f, k);
            }
            sostener();
        }, PeleaInterrumpida);
        if (PeleaInterrumpida()) { if (gigante != null) Destroy(gigante.gameObject); yield break; }

        // Machacar.
        bloqueoDisparo = true;
        float carga = 0f, limite = 3.4f;
        bool exito = false;
        for (float s = 0f; s < limite; s += Time.deltaTime)
        {
            if (PeleaInterrumpida()) break;
            sostener();
            if (!partida.EstaPausada)
            {
                var kb = Keyboard.current;
                var pad = Gamepad.current;
                bool golpe = (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                if (golpe && gigante != null)
                {
                    carga += .085f;
                    DisparoAutomatico(gigante.transform.position);
                }
                carga = Mathf.Max(0f, carga - Time.deltaTime * .22f);
                MostrarQte("ESPACIO", "¡MACHACA! Sobrecarga la .50 y revienta lo que levanta", carga,
                    Color.Lerp(new Color(1f, .85f, .3f), new Color(1f, .25f, .1f), carga));
                if (animador != null) animador.inclinacion = -15f + Mathf.Sin(s * 9f) * 3f * carga;
                if (carga >= 1f) { exito = true; break; }
            }
            yield return null;
        }
        OcultarQte();
        bloqueoDisparo = false;
        if (PeleaInterrumpida()) { if (gigante != null) Destroy(gigante.gameObject); yield break; }

        if (exito && gigante != null)
        {
            Vector3 p = gigante.transform.position;
            rocas.Remove(gigante);
            Destroy(gigante.gameObject);
            EfectosPelea.Explosion(p, tamano / 3f, true);
            EfectosPelea.Explosion(p + Random.insideUnitSphere * tamano * .4f, tamano / 4f, true);
            Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, p, 1f, "Efectos");
            sacudida = 1.6f;
            Mensaje("¡SE LO REVENTASTE ENCIMA!", 2f);
            Decir("¡JAJA! ¡Toma, weón! ¡Eso le dolió!");
            nemesis.RecibirDanio(320f, p, -frente);
            yield return Aturdido();
        }
        else if (gigante != null)
        {
            Mensaje("¡NO ALCANZÓ! ¡VIENE HACIA NOSOTROS!", 2f);
            Vector3 suelta = (hD + hI) * .5f + frente * L * .9f + Vector3.up * L * .3f;
            Vector3 dD2 = animador != null ? animador.objetivoDer : suelta, dI2 = animador != null ? animador.objetivoIzq : suelta;
            bool lanzada = false;
            yield return Tween(.35f, k =>
            {
                if (animador != null)
                {
                    animador.inclinacion = Mathf.Lerp(-15f, 35f, k);
                    animador.objetivoDer = Vector3.Lerp(dD2, suelta + lado * tamano * .3f, k);
                    animador.objetivoIzq = Vector3.Lerp(dI2, suelta - lado * tamano * .3f, k);
                }
                if (!lanzada && k > .5f && gigante != null)
                {
                    lanzada = true;
                    Soltar(gigante);
                }
                else if (!lanzada) sostener();
            }, PeleaInterrumpida);
            if (!lanzada && gigante != null && !PeleaInterrumpida()) Soltar(gigante);
            yield return Tween(.6f, k =>
            {
                if (animador == null) return;
                animador.inclinacion = Mathf.Lerp(35f, 0f, k);
                animador.pesoDer = animador.pesoIzq = 1f - k;
            }, PeleaInterrumpida);
        }
    }

    /// <summary>Cada pulsacion del QTE dispara la .50 sola hacia el objeto.</summary>
    void DisparoAutomatico(Vector3 objetivo)
    {
        Vector3 boca = pivote.TransformPoint(bocaLocal);
        var lr = trazas[traza];
        traza = (traza + 1) % trazas.Length;
        if (lr != null)
        {
            lr.SetPosition(0, boca);
            lr.SetPosition(1, objetivo + Random.insideUnitSphere * 1.5f);
            lr.enabled = true;
        }
        trazaHasta = Time.time + .05f;
        fogonazoHasta = Time.time + .04f;
        retroceso = Mathf.Min(retroceso + .4f, 2.2f);
        sacudida = Mathf.Max(sacudida, .25f);
        EfectosPelea.Rafaga(objetivo + Random.insideUnitSphere * 1.5f, Random.onUnitSphere, new Color(1f, .7f, .3f), new Color(1f, .4f, .1f), 8,
            new Vector2(3f, 8f), new Vector2(.2f, .5f), new Vector2(.2f, .5f), 1f, 180f, 1.5f);
        if (clipDisparo != null) audioArma.PlayOneShot(clipDisparo, .7f);
    }

    // ---------------------------------------------------------------- Muerte

    IEnumerator MuerteNemesis()
    {
        var t = nemesis.transform;
        Vector3 frente = FrenteNemesis();
        Vector3 lado = Vector3.Cross(Vector3.up, frente);
        // Cae hacia atras y un poco de lado, para verlo entero desde la lancha.
        Vector3 caida = Quaternion.Euler(0f, Random.value < .5f ? -35f : 35f, 0f) * -frente;
        Vector3 eje = Vector3.Cross(Vector3.up, caida);
        Quaternion giro0 = t.rotation;
        Vector3 base0 = t.position;
        if (animNemesis != null) animNemesis.speed = .4f;
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.grito : null, base0, 1f, "Voces", .4f);
        float L = animador != null ? animador.LargoBrazo : altura * .35f;
        Vector3 cara = animador != null && animador.Cabeza != null ? animador.Cabeza.position : base0 + Vector3.up * altura * .9f;

        // 1) Agonia en camara lenta: se lleva las manos a la cara y retrocede.
        Time.timeScale = .35f;
        Vector3 camA = base0 + frente * altura * 1.2f + lado * altura * .55f + Vector3.up * altura * .45f;
        for (float s = 0f; s < 1.8f; s += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, s / 1.8f);
            if (animador != null)
            {
                animador.inclinacion = Mathf.Lerp(0f, -22f, k);
                animador.giro = Mathf.Sin(s * 5f) * 8f;
                animador.pesoDer = animador.pesoIzq = Mathf.Clamp01(k * 2f) * .85f;
                animador.objetivoDer = cara + frente * L * .3f + lado * L * .12f;
                animador.objetivoIzq = cara + frente * L * .3f - lado * L * .12f;
            }
            t.position = base0 + caida * altura * .04f * k + Random.insideUnitSphere * .05f * altura * (1f - k);
            if (Random.value < .1f)
                EfectosPelea.Explosion(base0 + Vector3.up * altura * Random.Range(.4f, .85f) + Random.insideUnitSphere * altura * .1f, altura / 25f, true);
            Vector3 foco = base0 + Vector3.up * altura * .6f;
            camara.transform.SetPositionAndRotation(camA, Quaternion.LookRotation(foco - camA));
            camara.fieldOfView = 45f;
            yield return null;
        }
        Time.timeScale = 1f;

        // 2) Se desploma: rotacion con aceleracion creciente, como un arbol talado.
        Vector3 camB = base0 + lado * altura * 1.9f * (Vector3.Dot(lado, caida) > 0f ? -1f : 1f) + frente * altura * .4f + Vector3.up * altura * .5f;
        Vector3 pivoteCaida = t.position;
        float angulo = 0f, velocidad = 8f;
        bool impacto = false;
        float temblor = 0f, reposo = 0f;
        Quaternion giroInicio = t.rotation;
        while (reposo < 1.4f)
        {
            float dt = Time.deltaTime;
            if (!impacto)
            {
                velocidad += (30f + 160f * Mathf.Sin(angulo * Mathf.Deg2Rad)) * dt;
                angulo += velocidad * dt;
                if (angulo >= 86f)
                {
                    angulo = 86f;
                    impacto = true;
                    velocidad = -velocidad * .18f;
                    Vector3 cuerpo = pivoteCaida + caida * altura * .5f;
                    for (int i = 0; i < 12; i++)
                    {
                        Vector3 p = pivoteCaida + caida * altura * (i / 11f) + lado * Random.Range(-1f, 1f) * altura * .12f;
                        EfectosPelea.Rafaga(GeografiaIsla.Suelo(p, altura), Vector3.up, new Color(.45f, .38f, .3f, .9f), new Color(.62f, .55f, .45f, .8f),
                            22, new Vector2(4f, 14f) * (altura / 30f), new Vector2(3f, 7f) * (altura / 30f), new Vector2(1.5f, 3f), .3f, 70f, 5f);
                    }
                    EfectosPelea.Explosion(cuerpo, altura / 10f, false);
                    Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, cuerpo, 1f, "Efectos");
                    Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, cuerpo, .8f, "Efectos", .6f);
                    temblor = 2.2f;
                }
            }
            else
            {
                // Pequeño rebote al chocar contra el suelo.
                velocidad += 200f * dt;
                angulo = Mathf.Min(86f, angulo + velocidad * dt);
                reposo += dt;
            }
            t.rotation = Quaternion.AngleAxis(angulo, eje) * giroInicio;
            if (animador != null)
            {
                // Brazos al aire mientras cae.
                float agita = Mathf.Sin(Time.time * 7f) * .15f;
                animador.inclinacion = Mathf.Lerp(-22f, -8f, angulo / 86f);
                animador.giro = 0f;
                animador.pesoDer = animador.pesoIzq = impacto ? Mathf.MoveTowards(animador.pesoDer, 0f, dt) : .7f;
                Vector3 alto = t.position + t.up * altura;
                animador.objetivoDer = alto + t.right * L * (.9f + agita) + t.up * L * .2f;
                animador.objetivoIzq = alto - t.right * L * (.9f - agita) + t.up * L * .2f;
            }
            temblor = Mathf.MoveTowards(temblor, 0f, dt * 1.5f);
            Vector3 foco = pivoteCaida + caida * altura * .35f * (angulo / 86f) + Vector3.up * altura * .35f;
            Vector3 cam = camB + Random.insideUnitSphere * temblor * .6f;
            camara.transform.SetPositionAndRotation(cam, Quaternion.LookRotation(foco - camB));
            camara.fieldOfView = 50f;
            yield return null;
        }

        // 3) El cuerpo se hunde entre polvo.
        Vector3 hundido = t.position;
        for (float s = 0f; s < 2f; s += Time.deltaTime)
        {
            t.position = hundido - Vector3.up * altura * .25f * Mathf.SmoothStep(0f, 1f, s / 2f);
            if (Random.value < .3f)
                EfectosPelea.Rafaga(GeografiaIsla.Suelo(pivoteCaida + caida * altura * Random.value, altura), Vector3.up, new Color(.45f, .38f, .3f, .8f),
                    new Color(.6f, .52f, .42f, .7f), 10, new Vector2(1f, 4f), new Vector2(2f, 4f) * (altura / 30f), new Vector2(1.5f, 2.5f), .1f, 60f);
            Vector3 foco = pivoteCaida + caida * altura * .35f + Vector3.up * altura * .2f;
            camara.transform.SetPositionAndRotation(camB, Quaternion.LookRotation(foco - camB));
            yield return null;
        }
        if (animador != null) animador.Soltar();
        nemesis.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- Botellas del final

    /// <summary>Botellas de pie en la popa, detras de CJ y el protagonista, a la altura de la cubierta.</summary>
    void LlenarBote(int cantidad)
    {
        var carga = new GameObject("CargaBotellas").transform;
        carga.SetParent(lancha, false);
        Bounds b = GeografiaIsla.Limites(lancha, cj, jugador.transform, torreta, carga);
        Vector3 centro = lancha.InverseTransformPoint(b.center);
        float escala = Mathf.Max(.001f, lancha.lossyScale.x);
        float ancho = Vector3.Scale(b.size, Abs(lancha.right)).magnitude / escala;
        float largo = Vector3.Scale(b.size, Abs(lancha.forward)).magnitude / escala;
        float cubiertaMundo = cj != null ? cj.position.y : torreta.position.y - 1.25f;
        float zCj = cj != null ? lancha.InverseTransformPoint(cj.position).z : centro.z;
        float popa = centro.z - largo * .5f + largo * .1f;
        float tope = Mathf.Max(popa + .3f, zCj - .7f / escala);
        const float paso = .24f;
        float pasoLocal = paso / escala;
        int columnas = Mathf.Max(3, Mathf.FloorToInt(ancho * .6f / pasoLocal));
        int filas = Mathf.Max(1, Mathf.FloorToInt((tope - popa) / pasoLocal));
        for (int i = 0; i < cantidad; i++)
        {
            int capa = i / (columnas * filas);
            int k = i % (columnas * filas);
            float x = centro.x + ((k % columnas) - (columnas - 1) * .5f) * pasoLocal;
            float z = popa + (k / columnas + .5f) * pasoLocal;
            GameObject botella;
            if (plantillaBotella != null)
            {
                botella = Instantiate(plantillaBotella, carga);
                botella.SetActive(true);
            }
            else botella = BotellaSimple(carga);
            botella.name = "Botella_" + (i + 1);
            botella.transform.localPosition = new Vector3(x + Random.Range(-.03f, .03f) / escala, 0f, z + Random.Range(-.03f, .03f) / escala);
            bool tumbada = capa > 0;
            // Medir de pie para escalar a 30 cm, despues girar.
            botella.transform.localRotation = Quaternion.identity;
            float alto = GeografiaIsla.Limites(botella.transform).size.y;
            if (alto > .001f) botella.transform.localScale *= .3f / alto;
            botella.transform.localRotation = Quaternion.Euler(tumbada ? 90f : Random.Range(-5f, 5f), Random.Range(0f, 360f), tumbada ? 0f : Random.Range(-5f, 5f));
            // Apoyar la base exactamente sobre la cubierta, aunque el pivote de la botella este en el centro.
            Bounds bb = GeografiaIsla.Limites(botella.transform);
            botella.transform.position += Vector3.up * (cubiertaMundo + .01f + (tumbada ? .3f : 0f) - bb.min.y);
        }
    }
}
