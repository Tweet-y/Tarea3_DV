using UnityEngine;

/// <summary>Roca lanzada por Nemesis: la sostiene en la mano, luego vuela en parabola hacia donde estara la lancha.</summary>
public sealed class RocaNemesis : MonoBehaviour
{
    public float Vida { get; private set; }
    public bool EnMano { get; private set; } = true;
    public bool EnLlamas { get; private set; }
    /// <summary>Con falso, quien la creo la mueve (sale del suelo, va en dos manos).</summary>
    public bool SeguirMano { get; set; } = true;
    /// <summary>Daño a la lancha al caer encima; negativo = el valor por defecto.</summary>
    public float Danio { get; set; } = -1f;
    public float Tamano => tamano;
    public float Progreso => tiempoVuelo <= 0f ? 0f : Mathf.Clamp01(t / tiempoVuelo);

    PeleaJefeLancha pelea;
    Transform mano;
    Vector3 origen, velocidad, giro, destino;
    float t, tiempoVuelo, tamano;
    TrailRenderer estela;
    LineRenderer marca;
    bool terminada;

    /// <param name="modelo">Prefab de la isla a lanzar; null = roca.</param>
    public static RocaNemesis Crear(PeleaJefeLancha pelea, Transform mano, Vector3 posicion, GameObject modelo,
        float tamano, float vida, bool llamas, Material material)
    {
        GameObject go;
        if (modelo != null)
        {
            go = new GameObject("Proyectil_" + modelo.name);
            go.transform.position = posicion;
            var visual = Instantiate(modelo, go.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Random.rotation;
            // Solo la forma: sin fisica, scripts, luces ni sonido del prefab original.
            foreach (var c in visual.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(c);
            foreach (var c in visual.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var c in visual.GetComponentsInChildren<Rigidbody>(true)) Destroy(c);
            foreach (var c in visual.GetComponentsInChildren<Light>(true)) Destroy(c);
            foreach (var c in visual.GetComponentsInChildren<AudioSource>(true)) Destroy(c);
            Bounds b = GeografiaIsla.Limites(visual.transform);
            float mayor = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (mayor > .01f) visual.transform.localScale *= tamano / mayor;
            b = GeografiaIsla.Limites(visual.transform);
            visual.transform.position += go.transform.position - b.center;
            var esfera = go.AddComponent<SphereCollider>();
            esfera.isTrigger = true;
            esfera.radius = tamano * .6f;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "RocaNemesis";
            go.transform.position = posicion;
            go.transform.rotation = Random.rotation;
            go.transform.localScale = new Vector3(tamano * Random.Range(.85f, 1.15f), tamano * Random.Range(.7f, .95f), tamano * Random.Range(.9f, 1.2f));
            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = .7f; // Un poco mas grande que la malla: apuntar en movimiento debe ser justo.
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
        var cuerpo = go.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;

        var roca = go.AddComponent<RocaNemesis>();
        roca.pelea = pelea;
        roca.mano = mano;
        roca.tamano = tamano;
        roca.Vida = vida;
        roca.EnLlamas = llamas;

        roca.estela = go.AddComponent<TrailRenderer>();
        roca.estela.time = .5f;
        roca.estela.startWidth = tamano * .7f;
        roca.estela.endWidth = 0f;
        roca.estela.sharedMaterial = EfectosPelea.MaterialParticulas;
        roca.estela.startColor = llamas ? new Color(1f, .55f, .15f, .9f) : new Color(.55f, .5f, .45f, .6f);
        roca.estela.endColor = llamas ? new Color(1f, .15f, .05f, 0f) : new Color(.55f, .5f, .45f, 0f);
        roca.estela.emitting = false;
        roca.estela.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (llamas)
        {
            var luzGo = new GameObject("Fuego");
            luzGo.transform.SetParent(go.transform, false);
            var luz = luzGo.AddComponent<Light>();
            luz.color = new Color(1f, .5f, .15f);
            luz.intensity = 4f;
            luz.range = tamano * 5f;
            var fuego = luzGo.AddComponent<ParticleSystem>();
            fuego.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fuego.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(tamano * .4f, tamano * .9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .6f, .15f), new Color(1f, .25f, .05f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = fuego.emission;
            em.rateOverTime = 45f;
            var forma = fuego.shape;
            forma.shapeType = ParticleSystemShapeType.Sphere;
            forma.radius = .5f;
            EfectosPelea.Desvanecer(fuego);
            var fr = luzGo.GetComponent<ParticleSystemRenderer>();
            fr.sharedMaterial = EfectosPelea.MaterialParticulas;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fuego.Play();
        }
        return roca;
    }

    public void Lanzar(Vector3 objetivo, float segundos)
    {
        EnMano = false;
        origen = transform.position;
        destino = objetivo;
        tiempoVuelo = Mathf.Max(.5f, segundos);
        t = 0f;
        velocidad = (destino - origen) / tiempoVuelo - .5f * Physics.gravity * tiempoVuelo;
        giro = Random.onUnitSphere * Random.Range(90f, 240f);
        estela.emitting = true;
        CrearMarca();
    }

    /// <summary>Resta vida; devuelve verdadero si la roca queda destruida.</summary>
    public bool Recibir(float danio)
    {
        Vida -= danio;
        EfectosPelea.Rafaga(transform.position, Random.onUnitSphere, new Color(.4f, .37f, .34f), new Color(.6f, .55f, .5f), 8,
            new Vector2(2f, 6f), new Vector2(.08f, .2f), new Vector2(.3f, .7f), 1.5f, 180f, 2f);
        return Vida <= 0f;
    }

    void Update()
    {
        if (terminada) return;
        if (EnMano)
        {
            if (SeguirMano && mano != null) transform.position = mano.position + Vector3.up * (tamano * .35f);
            return;
        }
        t += Time.deltaTime;
        transform.position = origen + velocidad * t + .5f * Physics.gravity * (t * t);
        transform.Rotate(giro * Time.deltaTime, Space.World);
        ActualizarMarca();
        if (t >= tiempoVuelo)
        {
            terminada = true;
            pelea.RocaLlego(this);
        }
    }

    void CrearMarca()
    {
        var go = new GameObject("MarcaImpacto");
        marca = go.AddComponent<LineRenderer>();
        marca.loop = true;
        marca.positionCount = 40;
        marca.useWorldSpace = true;
        marca.widthMultiplier = .35f;
        marca.sharedMaterial = EfectosPelea.MaterialParticulas;
        marca.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ActualizarMarca();
    }

    void ActualizarMarca()
    {
        if (marca == null) return;
        float radio = Mathf.Lerp(6f, 3.5f, Progreso);
        float pulso = .55f + .45f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 22f, Progreso));
        var color = new Color(1f, .15f, .1f, pulso);
        marca.startColor = marca.endColor = color;
        float y = GeografiaIsla.NivelAgua + .12f;
        for (int i = 0; i < marca.positionCount; i++)
        {
            float a = i * Mathf.PI * 2f / marca.positionCount;
            marca.SetPosition(i, new Vector3(destino.x + Mathf.Cos(a) * radio, y, destino.z + Mathf.Sin(a) * radio));
        }
    }

    void OnDestroy()
    {
        if (marca != null) Destroy(marca.gameObject);
    }
}
