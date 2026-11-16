using UnityEngine;

/// <summary>
/// Efectos de la ametralladora .50: fogonazo visible, humo, casquillos, balas trazadoras que viajan,
/// golpe grave en el sonido, corredera que recula y vapor cuando el cañón se sobrecalienta.
/// Los sistemas de partículas se crean una vez y se reutilizan en cada disparo.
/// </summary>
public sealed partial class PeleaJefeLancha
{
    struct Trazadora
    {
        public LineRenderer linea;
        public Vector3 desde, dir;
        public float distancia, inicio;
        public bool activa;
    }

    const float VelocidadTrazadora = 520f, LargoTrazadora = 22f, VentajaTrazadora = 40f;

    readonly Trazadora[] balas = new Trazadora[10];
    int balaSiguiente;
    ParticleSystem psLlama, psHumo, psCasquillos;
    Transform corredera, cinta;
    Vector3 correderaBase;
    Quaternion cintaBase;
    AudioSource audioGrave;
    AudioClip clipGolpe, clipSiseo, clipClic;
    Vector3 velLancha, lanchaPrevia;
    float golpeArma, humoCalor;
    bool lanchaMedida, efectosApagados;
    static Material materialFogonazo, materialCasquillo;

    // ---------------------------------------------------------------- Construcción

    void CrearEfectosArma()
    {
        psLlama = Sistema("LlamaBoca", pivote, bocaLocal, MaterialFogonazo, ParticleSystemSimulationSpace.Local, 40);
        EfectosPelea.Desvanecer(psLlama);

        psHumo = Sistema("HumoBoca", pivote, bocaLocal, EfectosPelea.MaterialParticulas, ParticleSystemSimulationSpace.World, 160);
        var color = psHumo.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(g);
        var tam = psHumo.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .5f, 1f, 2.8f));

        // Casquillos: cilindros de latón con giro propio, expulsados por el costado derecho.
        psCasquillos = Sistema("Casquillos", pivote, Vector3.zero, MaterialCasquillo, ParticleSystemSimulationSpace.World, 40);
        var main = psCasquillos.main;
        main.gravityModifier = 1.6f;
        main.startSize3D = true;
        main.startRotation3D = true;
        var render = psCasquillos.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Mesh;
        var cilindro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        render.mesh = cilindro.GetComponent<MeshFilter>().sharedMesh;
        Destroy(cilindro);
        render.alignment = ParticleSystemRenderSpace.World;

        for (int i = 0; i < balas.Length; i++)
        {
            var go = new GameObject("Trazadora50_" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = EfectosPelea.MaterialParticulas;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = .025f;
            lr.endWidth = .1f;
            lr.startColor = new Color(1f, .45f, .1f, 0f);
            lr.endColor = new Color(1f, .82f, .5f, 1f);
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            balas[i].linea = lr;
        }

        if (arma != null)
            foreach (var t in arma.GetComponentsInChildren<Transform>())
            {
                if (corredera == null && t.name.Contains("Slide")) { corredera = t; correderaBase = t.localPosition; }
                if (cinta == null && t.name.Contains("Belt")) { cinta = t; cintaBase = t.localRotation; }
            }

        if (fogonazo != null)
        {
            fogonazo.range = 13f;
            fogonazo.shadows = LightShadows.None;
        }

        audioGrave = gameObject.AddComponent<AudioSource>();
        audioGrave.playOnAwake = false;
        audioGrave.spatialBlend = 0f;
        Sonido.Asignar(audioGrave, "Efectos");
        clipGolpe = CrearGolpeGrave();
        clipSiseo = CrearRuido("SiseoCanon", 1.3f, 2.6f, .22f, true);
        clipClic = CrearRuido("ClicSeco", .05f, 120f, .5f, false);
    }

    static ParticleSystem Sistema(string nombre, Transform padre, Vector3 local, Material material, ParticleSystemSimulationSpace espacio, int maximo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = espacio;
        main.maxParticles = maximo;
        main.startSpeed = 0f;
        // Solo se emite a mano, disparo a disparo.
        var emision = ps.emission;
        emision.enabled = false;
        var forma = ps.shape;
        forma.enabled = false;
        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    /// <summary>Estrella de seis puntas con centro blanco: la llamarada vista de frente.</summary>
    static Material MaterialFogonazo
    {
        get
        {
            if (materialFogonazo != null) return materialFogonazo;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Fogonazo50" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 p = new Vector2(x + .5f - n * .5f, y + .5f - n * .5f) / (n * .5f);
                    float r = p.magnitude;
                    float punta = Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(p.y, p.x) * 3f)), 5f);
                    float a = Mathf.Clamp01(1f - r / (.3f + .7f * punta));
                    float centro = Mathf.Clamp01(1f - r / .35f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a * a * 1.2f + centro * centro)));
                }
            tex.Apply();
            materialFogonazo = new Material(Shader.Find("Sprites/Default")) { name = "Fogonazo50", mainTexture = tex };
            return materialFogonazo;
        }
    }

    static Material MaterialCasquillo
    {
        get
        {
            if (materialCasquillo != null) return materialCasquillo;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            materialCasquillo = new Material(shader) { name = "Casquillo50" };
            var laton = new Color(.86f, .64f, .22f);
            materialCasquillo.color = laton;
            if (materialCasquillo.HasProperty("_BaseColor")) materialCasquillo.SetColor("_BaseColor", laton);
            if (materialCasquillo.HasProperty("_Metallic")) materialCasquillo.SetFloat("_Metallic", .85f);
            if (materialCasquillo.HasProperty("_Smoothness")) materialCasquillo.SetFloat("_Smoothness", .65f);
            return materialCasquillo;
        }
    }

    // ---------------------------------------------------------------- Disparo

    /// <summary>Todo lo que se ve y se oye en un disparo, desde la boca hasta el punto de llegada.</summary>
    void EfectosDisparo(Vector3 boca, Vector3 fin)
    {
        golpeArma = 1f;
        fogonazoHasta = Time.time + .045f;
        if (fogonazo != null) fogonazo.intensity = Random.Range(4.5f, 8f);

        if (psLlama != null)
        {
            // Apuntando, la llamarada se achica para no tapar el blanco.
            float escala = Mathf.Lerp(1f, .6f, zoom);
            var nucleo = new ParticleSystem.EmitParams
            {
                position = Vector3.zero,
                velocity = Vector3.zero,
                startSize = Random.Range(.34f, .5f) * escala,
                startLifetime = Random.Range(.035f, .05f),
                startColor = new Color(1f, .92f, .68f, .9f),
                rotation = Random.Range(0f, 360f)
            };
            psLlama.Emit(nucleo, 1);
            for (int i = 0; i < 3; i++)
            {
                var lengua = new ParticleSystem.EmitParams
                {
                    position = Vector3.forward * Random.Range(.03f, .12f),
                    velocity = Vector3.forward * Random.Range(7f, 15f) + (Vector3)Random.insideUnitCircle * 1.4f,
                    startSize = Random.Range(.13f, .22f) * escala,
                    startLifetime = Random.Range(.04f, .065f),
                    startColor = new Color(1f, Random.Range(.5f, .75f), .18f, .9f),
                    rotation = Random.Range(0f, 360f)
                };
                psLlama.Emit(lengua, 1);
            }
            // Escapes laterales del gas, uno por lado.
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var escape = new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    velocity = Vector3.right * lado * Random.Range(4f, 7f) + Vector3.up * Random.Range(-.5f, 1f),
                    startSize = Random.Range(.1f, .16f) * escala,
                    startLifetime = .04f,
                    startColor = new Color(1f, .7f, .3f, .8f),
                    rotation = Random.Range(0f, 360f)
                };
                psLlama.Emit(escape, 1);
            }
        }

        if (psHumo != null)
        {
            var humo = new ParticleSystem.EmitParams
            {
                position = boca + pivote.forward * .15f,
                velocity = pivote.forward * Random.Range(1.5f, 3f) + Vector3.up * .4f + velLancha * .6f,
                startSize = Random.Range(.22f, .36f),
                startLifetime = Random.Range(.45f, .8f),
                startColor = new Color(.78f, .76f, .74f, .2f),
                rotation = Random.Range(0f, 360f)
            };
            psHumo.Emit(humo, 1);
        }

        if (psCasquillos != null)
        {
            Vector3 ventana = pivote.TransformPoint(new Vector3(.09f, bocaLocal.y - .02f, armaBase.z + .3f));
            var casquillo = new ParticleSystem.EmitParams
            {
                position = ventana,
                velocity = pivote.right * Random.Range(2.2f, 3.6f) + pivote.up * Random.Range(1f, 2.2f) +
                    pivote.forward * Random.Range(-.6f, .4f) + velLancha,
                startSize3D = new Vector3(.02f, .048f, .02f),
                startLifetime = 1.1f,
                startColor = Color.white,
                rotation3D = Random.insideUnitSphere * 180f,
                angularVelocity3D = Random.insideUnitSphere * 900f
            };
            psCasquillos.Emit(casquillo, 1);
        }

        Vector3 tramo = fin - boca;
        if (tramo.sqrMagnitude > .01f)
        {
            ref var bala = ref balas[balaSiguiente];
            balaSiguiente = (balaSiguiente + 1) % balas.Length;
            bala.desde = boca;
            bala.distancia = tramo.magnitude;
            bala.dir = tramo / bala.distancia;
            bala.inicio = Time.time;
            bala.activa = true;
        }

        if (clipDisparo != null) audioArma.PlayOneShot(clipDisparo, Random.Range(.6f, .74f));
        if (audioGrave != null && clipGolpe != null)
        {
            audioGrave.pitch = Random.Range(.93f, 1.06f);
            audioGrave.PlayOneShot(clipGolpe, .85f);
        }
    }

    /// <summary>El cañón llegó al límite: siseo y bocanada de vapor.</summary>
    void EfectoSobrecalentada()
    {
        if (audioGrave != null && clipSiseo != null)
        {
            audioGrave.pitch = 1f;
            audioGrave.PlayOneShot(clipSiseo, .8f);
        }
        if (psHumo == null) return;
        for (int i = 0; i < 10; i++) Vapor(Random.Range(.25f, .45f), .34f);
    }

    /// <summary>Apretar el gatillo con el arma bloqueada solo hace un clic.</summary>
    void ClicSeco()
    {
        if (audioGrave == null || clipClic == null) return;
        audioGrave.pitch = 1f;
        audioGrave.PlayOneShot(clipClic, .6f);
    }

    void Vapor(float tamano, float alfa)
    {
        Vector3 punto = pivote.TransformPoint(bocaLocal - Vector3.forward * Random.Range(0f, .55f));
        var vapor = new ParticleSystem.EmitParams
        {
            position = punto,
            velocity = Vector3.up * Random.Range(.6f, 1.5f) + Random.insideUnitSphere * .3f + velLancha * .85f,
            startSize = tamano,
            startLifetime = Random.Range(.7f, 1.2f),
            startColor = new Color(.9f, .9f, .9f, alfa),
            rotation = Random.Range(0f, 360f)
        };
        psHumo.Emit(vapor, 1);
    }

    // ---------------------------------------------------------------- Cada cuadro

    void ActualizarEfectosArma(float dt)
    {
        if (!lanchaMedida) { lanchaPrevia = lancha.position; lanchaMedida = true; }
        if (dt > 0f) velLancha = (lancha.position - lanchaPrevia) / dt;
        lanchaPrevia = lancha.position;

        // Golpe mecánico: el arma vibra, la corredera recula y la cinta salta.
        golpeArma = Mathf.MoveTowards(golpeArma, 0f, dt * 13f);
        if (arma != null)
            arma.localRotation = Quaternion.Euler(-golpeArma * .7f, 0f, Random.Range(-1f, 1f) * golpeArma * .8f);
        if (corredera != null && corredera.parent != null)
        {
            Vector3 atras = corredera.parent.InverseTransformDirection(-pivote.forward);
            float escala = Mathf.Max(.0001f, corredera.parent.lossyScale.z);
            corredera.localPosition = correderaBase + atras * (golpeArma * .05f / escala);
        }
        if (cinta != null)
            cinta.localRotation = cintaBase * Quaternion.Euler(Random.Range(-1f, 1f) * golpeArma * 4f, 0f, Random.Range(-1f, 1f) * golpeArma * 3f);
        camara.fieldOfView += golpeArma * .9f;

        if (fogonazo != null) fogonazo.enabled = Time.time < fogonazoHasta;

        // El cañón humea al calentarse y echa vapor denso mientras está bloqueado.
        if (psHumo != null && (sobrecalentada || calor > .55f))
        {
            float ritmo = sobrecalentada ? 26f : Mathf.Lerp(0f, 9f, (calor - .55f) / .45f);
            humoCalor += ritmo * dt;
            while (humoCalor >= 1f)
            {
                humoCalor -= 1f;
                Vapor(Random.Range(.14f, .28f), sobrecalentada ? .3f : .16f);
            }
        }

        for (int i = 0; i < balas.Length; i++)
        {
            ref var bala = ref balas[i];
            if (bala.linea == null) continue;
            if (!bala.activa) { bala.linea.enabled = false; continue; }
            float cabeza = VentajaTrazadora + (Time.time - bala.inicio) * VelocidadTrazadora;
            float cola = Mathf.Max(0f, cabeza - LargoTrazadora);
            if (cola >= bala.distancia - .01f)
            {
                bala.activa = false;
                bala.linea.enabled = false;
                continue;
            }
            cabeza = Mathf.Min(cabeza, bala.distancia);
            bala.linea.SetPosition(0, bala.desde + bala.dir * cola);
            bala.linea.SetPosition(1, bala.desde + bala.dir * cabeza);
            bala.linea.enabled = true;
        }
    }

    /// <summary>Al terminar el combate no deben quedar luces ni trazadoras congeladas.</summary>
    void ApagarEfectosArma()
    {
        if (efectosApagados) return;
        efectosApagados = true;
        if (fogonazo != null) fogonazo.enabled = false;
        for (int i = 0; i < balas.Length; i++)
        {
            balas[i].activa = false;
            if (balas[i].linea != null) balas[i].linea.enabled = false;
        }
    }

    // ---------------------------------------------------------------- Sonido generado

    /// <summary>Golpe grave que le da cuerpo de calibre grueso al disparo del rifle.</summary>
    static AudioClip CrearGolpeGrave()
    {
        const int frecuencia = 22050;
        var muestras = new float[(int)(frecuencia * .2f)];
        var azar = new System.Random(50);
        float fase = 0f;
        for (int i = 0; i < muestras.Length; i++)
        {
            float t = (float)i / frecuencia;
            float tono = Mathf.Lerp(150f, 48f, Mathf.Clamp01(t / .12f));
            fase += tono * Mathf.PI * 2f / frecuencia;
            float grave = Mathf.Sin(fase) * Mathf.Exp(-t * 20f) * .8f;
            float chasquido = ((float)azar.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 85f) * .45f;
            muestras[i] = Mathf.Clamp(grave + chasquido, -1f, 1f);
        }
        var clip = AudioClip.Create("GolpeCalibre50", muestras.Length, 1, frecuencia, false);
        clip.SetData(muestras, 0);
        return clip;
    }

    static AudioClip CrearRuido(string nombre, float segundos, float caida, float volumen, bool agudo)
    {
        const int frecuencia = 22050;
        var muestras = new float[(int)(frecuencia * segundos)];
        var azar = new System.Random(nombre.Length);
        float previo = 0f;
        for (int i = 0; i < muestras.Length; i++)
        {
            float t = (float)i / frecuencia;
            float ruido = (float)azar.NextDouble() * 2f - 1f;
            // Restar la muestra anterior deja solo lo agudo: suena a siseo y no a viento.
            float valor = agudo ? (ruido - previo) * .7f : ruido;
            previo = ruido;
            muestras[i] = valor * Mathf.Exp(-t * caida) * volumen;
        }
        var clip = AudioClip.Create(nombre, muestras.Length, 1, frecuencia, false);
        clip.SetData(muestras, 0);
        return clip;
    }
}
