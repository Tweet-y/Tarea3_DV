using UnityEngine;

/// <summary>Particulas y destellos creados por codigo para el combate final (no dependen de prefabs).</summary>
public static class EfectosPelea
{
    static Material particulas;
    static Texture2D circulo;

    /// <summary>Material de particula con un punto suave, para que no se vean cuadrados.</summary>
    public static Material MaterialParticulas
    {
        get
        {
            if (particulas != null) return particulas;
            particulas = new Material(Shader.Find("Sprites/Default")) { name = "ParticulasPelea" };
            particulas.mainTexture = Circulo;
            return particulas;
        }
    }

    public static Texture2D Circulo
    {
        get
        {
            if (circulo != null) return circulo;
            const int n = 32;
            circulo = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PuntoSuave" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n * .5f, n * .5f)) / (n * .5f);
                    float a = Mathf.Clamp01(1f - d);
                    circulo.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            circulo.Apply();
            return circulo;
        }
    }

    static Material fogonazo, casquillo;
    static Mesh cilindro, cuadro;
    static readonly System.Collections.Generic.Queue<GameObject> marcas = new System.Collections.Generic.Queue<GameObject>();
    const int MaximoMarcas = 70;

    /// <summary>Estrella de seis puntas con centro blanco: la llamarada vista de frente.</summary>
    public static Material MaterialFogonazo
    {
        get
        {
            if (fogonazo != null) return fogonazo;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Fogonazo" };
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
            fogonazo = new Material(Shader.Find("Sprites/Default")) { name = "Fogonazo", mainTexture = tex };
            return fogonazo;
        }
    }

    /// <summary>Latón pulido para los casquillos expulsados.</summary>
    public static Material MaterialCasquillo
    {
        get
        {
            if (casquillo != null) return casquillo;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            casquillo = new Material(shader) { name = "Casquillo" };
            var laton = new Color(.86f, .64f, .22f);
            casquillo.color = laton;
            if (casquillo.HasProperty("_BaseColor")) casquillo.SetColor("_BaseColor", laton);
            if (casquillo.HasProperty("_Metallic")) casquillo.SetFloat("_Metallic", .85f);
            if (casquillo.HasProperty("_Smoothness")) casquillo.SetFloat("_Smoothness", .65f);
            return casquillo;
        }
    }

    public static Mesh Cilindro
    {
        get
        {
            if (cilindro != null) return cilindro;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cilindro = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return cilindro;
        }
    }

    static Mesh Cuadro
    {
        get
        {
            if (cuadro != null) return cuadro;
            cuadro = new Mesh { name = "CuadroMarca" };
            cuadro.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) };
            cuadro.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            cuadro.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            cuadro.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            cuadro.RecalculateBounds();
            return cuadro;
        }
    }

    /// <summary>Sistema de partículas en espacio de mundo que solo emite a mano (Emit), disparo a disparo.</summary>
    public static ParticleSystem SistemaManual(string nombre, Transform padre, Material material, int maximo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maximo;
        main.startSpeed = 0f;
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

    /// <summary>Marca plana pegada a una superficie (agujero de bala, charco de sangre) que se desvanece sola.</summary>
    public static void Marca(Vector3 punto, Vector3 normal, float radio, Color color, float segundos)
    {
        if (normal.sqrMagnitude < .01f) normal = Vector3.up;
        var go = new GameObject("Marca");
        go.transform.position = punto + normal.normalized * .02f;
        go.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        go.transform.localScale = new Vector3(radio * 2f * Random.Range(.8f, 1.25f), radio * 2f * Random.Range(.8f, 1.25f), 1f);
        go.AddComponent<MeshFilter>().sharedMesh = Cuadro;
        var render = go.AddComponent<MeshRenderer>();
        render.sharedMaterial = MaterialParticulas;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        go.AddComponent<MarcaFugaz>().Iniciar(color, segundos);
        marcas.Enqueue(go);
        while (marcas.Count > MaximoMarcas)
        {
            var vieja = marcas.Dequeue();
            if (vieja != null) Object.Destroy(vieja);
        }
    }

    /// <summary>Bala contra una superficie dura: chispas, polvo y, si la superficie no se mueve, el agujero.</summary>
    public static void ImpactoBala(Vector3 punto, Vector3 normal, float escala = 1f, bool marca = true)
    {
        if (normal.sqrMagnitude < .01f) normal = Vector3.up;
        Rafaga(punto, normal, new Color(1f, .92f, .6f), new Color(1f, .6f, .2f), 5 + (int)(5 * escala), new Vector2(3f, 9f),
            new Vector2(.02f, .05f) * escala, new Vector2(.1f, .28f), 1.6f, 55f, 1f);
        Rafaga(punto, normal, new Color(.55f, .5f, .44f, .55f), new Color(.72f, .68f, .6f, .45f), 5, new Vector2(.6f, 2.2f),
            new Vector2(.14f, .34f) * escala, new Vector2(.35f, .75f), .1f, 35f, 1.5f);
        if (marca) Marca(punto, normal, .055f * escala, new Color(.04f, .04f, .04f, .9f), 12f);
    }

    /// <summary>Sangre al herir a un enemigo: gotas hacia donde sigue la bala, salpicón de vuelta, bruma y mancha en el suelo.</summary>
    public static void Sangre(Vector3 punto, Vector3 direccion, bool cabezazo, bool murio, float escala = 1f)
    {
        if (direccion.sqrMagnitude < .01f) direccion = Vector3.up;
        direccion.Normalize();
        var oscuro = new Color(.5f, .02f, .02f);
        var vivo = new Color(.82f, .05f, .04f);
        int gotas = murio ? 34 : cabezazo ? 26 : 16;
        Rafaga(punto, direccion, oscuro, vivo, gotas, new Vector2(1.4f, cabezazo ? 6.5f : 4.4f) * Mathf.Sqrt(escala),
            new Vector2(.045f, .12f) * escala, new Vector2(.35f, .85f), 1.8f, 30f, 1.6f);
        Rafaga(punto, -direccion, oscuro, vivo, 7, new Vector2(.8f, 2.6f) * Mathf.Sqrt(escala),
            new Vector2(.035f, .08f) * escala, new Vector2(.3f, .6f), 1.8f, 50f, 1.2f);
        Rafaga(punto, direccion, new Color(.5f, .02f, .02f, .5f), new Color(.65f, .05f, .05f, .35f), cabezazo ? 6 : 4, new Vector2(.3f, 1.1f),
            new Vector2(.25f, .5f) * escala, new Vector2(.22f, .45f), 0f, 60f, 1f);
        if (!murio && Random.value > .4f) return;
        // Mancha donde cae la sangre: un poco más allá del impacto, en el suelo.
        Vector3 desde = punto + Vector3.ProjectOnPlane(direccion, Vector3.up) * Random.Range(.3f, .9f) + Vector3.up * .3f;
        if (!Physics.Raycast(desde, Vector3.down, out var suelo, 4f * Mathf.Max(1f, escala), LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) return;
        int manchas = murio ? 4 : 2;
        for (int i = 0; i < manchas; i++)
        {
            Vector2 d = Random.insideUnitCircle * (murio ? .45f : .2f) * escala;
            Vector3 lado = Vector3.Cross(suelo.normal, Vector3.forward).normalized;
            Vector3 frente = Vector3.Cross(lado, suelo.normal);
            Marca(suelo.point + lado * d.x + frente * d.y + suelo.normal * (.004f * i), suelo.normal,
                Random.Range(.14f, murio ? .42f : .26f) * escala, new Color(.34f, .01f, .01f, .82f), murio ? 28f : 18f);
        }
    }

    public static ParticleSystem Rafaga(Vector3 posicion, Vector3 direccion, Color a, Color b, int cantidad,
        Vector2 velocidad, Vector2 tamano, Vector2 vida, float gravedad, float angulo, float segundos = 4f)
    {
        var go = new GameObject("FX_Pelea");
        go.transform.position = posicion;
        go.transform.rotation = direccion.sqrMagnitude > .0001f ? Quaternion.LookRotation(direccion) : Quaternion.identity;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = .2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida.x, vida.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velocidad.x, velocidad.y);
        main.startSize = new ParticleSystem.MinMaxCurve(tamano.x, tamano.y);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.gravityModifier = gravedad;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(100, cantidad);
        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBurst(0, new ParticleSystem.Burst(0f, (short)cantidad));
        var forma = ps.shape;
        forma.shapeType = angulo >= 180f ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
        forma.angle = Mathf.Min(angulo, 89f);
        forma.radius = .15f;
        Desvanecer(ps);
        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = MaterialParticulas;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        Object.Destroy(go, segundos);
        return ps;
    }

    public static void Desvanecer(ParticleSystem ps)
    {
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(g);
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .35f));
    }

    public static void Destello(Vector3 posicion, Color color, float intensidad, float rango, float segundos)
    {
        var go = new GameObject("Destello");
        go.transform.position = posicion;
        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.intensity = intensidad;
        luz.range = rango;
        luz.shadows = LightShadows.None;
        go.AddComponent<LuzFugaz>().segundos = segundos;
    }

    public static void Explosion(Vector3 p, float escala, bool fuego)
    {
        Rafaga(p, Vector3.up, new Color(.35f, .32f, .3f), new Color(.55f, .5f, .45f), 45, new Vector2(4f, 11f) * escala,
            new Vector2(.15f, .45f) * escala, new Vector2(.6f, 1.4f), 1.6f, 180f);
        Rafaga(p, Vector3.up, new Color(.5f, .46f, .42f, .7f), new Color(.7f, .66f, .6f, .6f), 18, new Vector2(1f, 3f) * escala,
            new Vector2(1.2f, 2.6f) * escala, new Vector2(1f, 2f), -.05f, 180f);
        if (fuego)
            Rafaga(p, Vector3.up, new Color(1f, .55f, .1f), new Color(1f, .2f, .05f), 35, new Vector2(3f, 8f) * escala,
                new Vector2(.4f, 1f) * escala, new Vector2(.3f, .7f), -.2f, 180f);
        Destello(p, fuego ? new Color(1f, .5f, .15f) : new Color(1f, .8f, .55f), 6f, 14f * escala, .25f);
    }

    public static void Salpicadura(Vector3 p, float escala)
    {
        Rafaga(p, Vector3.up, new Color(.85f, .93f, 1f, .9f), new Color(.6f, .78f, .9f, .8f), (int)(55 * escala),
            new Vector2(4f, 12f) * Mathf.Sqrt(escala), new Vector2(.25f, .7f) * escala, new Vector2(.7f, 1.4f), 2.2f, 22f);
        Rafaga(p, Vector3.up, new Color(1f, 1f, 1f, .6f), new Color(.8f, .9f, 1f, .5f), (int)(14 * escala),
            new Vector2(.5f, 2f), new Vector2(1.5f, 3f) * escala, new Vector2(1f, 1.8f), 0f, 80f);
    }
}

/// <summary>Tiñe una marca y la desvanece al final de su vida.</summary>
public sealed class MarcaFugaz : MonoBehaviour
{
    Color color;
    float segundos, t;
    Renderer render;
    MaterialPropertyBlock bloque;

    public void Iniciar(Color tinte, float duracion)
    {
        color = tinte;
        segundos = Mathf.Max(.1f, duracion);
        render = GetComponent<Renderer>();
        bloque = new MaterialPropertyBlock();
        Pintar(1f);
    }

    void Update()
    {
        t += Time.deltaTime;
        float resto = 1f - t / segundos;
        if (resto < .3f) Pintar(Mathf.Clamp01(resto / .3f));
        if (t >= segundos) Destroy(gameObject);
    }

    void Pintar(float alfa)
    {
        if (render == null) return;
        bloque.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * alfa));
        render.SetPropertyBlock(bloque);
    }
}

/// <summary>Apaga y destruye una luz breve.</summary>
public sealed class LuzFugaz : MonoBehaviour
{
    public float segundos = .2f;
    Light luz;
    float inicial, t;

    void Start()
    {
        luz = GetComponent<Light>();
        inicial = luz != null ? luz.intensity : 0f;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (luz != null) luz.intensity = Mathf.Lerp(inicial, 0f, t / Mathf.Max(.01f, segundos));
        if (t >= segundos) Destroy(gameObject);
    }
}
