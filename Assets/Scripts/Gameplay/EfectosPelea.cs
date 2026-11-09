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
