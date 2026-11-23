using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Escopetazo letal a quemarropa: el zombie revienta. El cuerpo desaparece y en su lugar saltan
/// trozos con física que dejan estela y manchan donde caen, una nube de sangre, salpicaduras en
/// las paredes cercanas y un charco en el suelo.
/// </summary>
public static class EfectosGore
{
    static Material carne, hueso;
    static readonly Color Sangre = new Color(.34f, .01f, .01f, .85f);

    static Material Carne => carne != null ? carne : carne = Lit("Carne", new Color(.42f, .03f, .03f), .55f);
    static Material Hueso => hueso != null ? hueso : hueso = Lit("Hueso", new Color(.86f, .8f, .68f), .25f);

    static Material Lit(string nombre, Color color, float brillo)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var m = new Material(shader) { name = nombre, color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", brillo);
        return m;
    }

    /// <param name="punto">Donde entró el disparo.</param>
    /// <param name="direccion">Hacia donde iba la carga de perdigones.</param>
    public static void Reventar(ControladorVida vida, Vector3 punto, Vector3 direccion)
    {
        if (vida == null) return;
        if (direccion.sqrMagnitude < .01f) direccion = vida.transform.forward;
        direccion.Normalize();
        var cuerpo = vida.transform;
        Bounds limites = GeografiaIsla.Limites(cuerpo);
        Vector3 centro = limites.center;
        float alto = Mathf.Max(1.2f, limites.size.y);

        // Material de la ropa del zombie, para que algunos trozos se reconozcan como suyos.
        Material ropa = null;
        foreach (var r in cuerpo.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (ropa == null && r.enabled && r.sharedMaterial != null) ropa = r.sharedMaterial;
            r.enabled = false;
        }
        var anim = cuerpo.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;
        var agente = cuerpo.GetComponent<NavMeshAgent>();
        if (agente != null) agente.enabled = false;
        foreach (var col in cuerpo.GetComponentsInChildren<Collider>()) col.enabled = false;
        Object.Destroy(cuerpo.gameObject, .5f);

        // Trozos: carne, algún hueso y jirones de ropa, a lo largo de todo el cuerpo.
        for (int i = 0; i < 16; i++)
        {
            Material mat = i < 9 ? Carne : i < 12 ? Hueso : (ropa != null ? ropa : Carne);
            bool largo = mat == Hueso;
            Vector3 tam = largo
                ? new Vector3(Random.Range(.04f, .06f), Random.Range(.16f, .26f), Random.Range(.04f, .06f))
                : Vector3.one * Random.Range(.08f, .19f) + Random.insideUnitSphere * .03f;
            Vector3 origen = centro + new Vector3(Random.Range(-.18f, .18f), Random.Range(-.35f, .45f) * alto * .5f, Random.Range(-.18f, .18f));
            Vector3 vel = direccion * Random.Range(3f, 7.5f) + Random.insideUnitSphere * 3.5f + Vector3.up * Random.Range(2.5f, 5.5f);
            Trozo(origen, tam, mat, vel, i < 9);
        }

        // Nube y lluvia de sangre: mucho más que un impacto normal.
        EfectosPelea.Sangre(punto, direccion, true, true, 2.4f);
        EfectosPelea.Rafaga(centro, direccion, new Color(.45f, .02f, .02f, .65f), new Color(.7f, .05f, .04f, .5f), 22,
            new Vector2(.6f, 2.4f), new Vector2(.4f, 1f), new Vector2(.5f, 1.1f), .15f, 70f, 2f);
        EfectosPelea.Rafaga(centro, Vector3.up, new Color(.5f, .02f, .02f), new Color(.8f, .05f, .04f), 70,
            new Vector2(2f, 7f), new Vector2(.04f, .11f), new Vector2(.5f, 1.2f), 2f, 180f, 2.5f);

        // Salpicaduras en paredes y objetos cercanos, sobre todo detrás del zombie.
        int mascara = LayerMask.GetMask("Default");
        for (int i = 0; i < 10; i++)
        {
            Vector3 rayo = (direccion * 1.4f + Random.insideUnitSphere).normalized;
            if (Physics.Raycast(centro, rayo, out var hit, 3.2f, mascara, QueryTriggerInteraction.Ignore))
                EfectosPelea.Marca(hit.point, hit.normal, Random.Range(.18f, .45f), Sangre, 30f);
        }
        // Charco bajo los pies y regueros hacia donde salió todo.
        if (Physics.Raycast(centro + Vector3.up * .5f, Vector3.down, out var suelo, 4f, mascara, QueryTriggerInteraction.Ignore))
        {
            EfectosPelea.Marca(suelo.point, suelo.normal, Random.Range(.6f, .85f), Sangre, 35f);
            Vector3 plano = Vector3.ProjectOnPlane(direccion, suelo.normal).normalized;
            for (int i = 1; i <= 4; i++)
                EfectosPelea.Marca(suelo.point + plano * (i * .45f) + Random.insideUnitSphere * .2f, suelo.normal,
                    Random.Range(.2f, .4f), Sangre, 30f);
        }

        var banco = BancoSonidos.Actual;
        Sonido.Reproducir(banco != null ? banco.impacto : null, centro, 1f, "Efectos", .55f);
        Sonido.Reproducir(banco != null ? banco.explosion : null, centro, .45f, "Efectos", 1.6f);
    }

    static void Trozo(Vector3 posicion, Vector3 tam, Material material, Vector3 velocidad, bool estela)
    {
        var go = GameObject.CreatePrimitive(Random.value < .5f ? PrimitiveType.Cube : PrimitiveType.Sphere);
        go.name = "Trozo";
        int capa = LayerMask.NameToLayer("Ignore Raycast");
        if (capa >= 0) go.layer = capa;
        go.transform.SetPositionAndRotation(posicion, Random.rotation);
        go.transform.localScale = tam;
        var render = go.GetComponent<Renderer>();
        render.sharedMaterial = material;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var cuerpo = go.AddComponent<Rigidbody>();
        cuerpo.mass = .3f;
        cuerpo.linearVelocity = velocidad;
        cuerpo.angularVelocity = Random.insideUnitSphere * 14f;
        cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var jugador = GameObject.FindGameObjectWithTag("Player");
        var capsula = jugador != null ? jugador.GetComponent<CharacterController>() : null;
        if (capsula != null) Physics.IgnoreCollision(go.GetComponent<Collider>(), capsula);
        if (estela)
        {
            var rastro = go.AddComponent<TrailRenderer>();
            rastro.sharedMaterial = EfectosPelea.MaterialParticulas;
            rastro.time = .3f;
            rastro.startWidth = tam.x * .7f;
            rastro.endWidth = 0f;
            rastro.startColor = new Color(.55f, .02f, .02f, .9f);
            rastro.endColor = new Color(.4f, .01f, .01f, 0f);
            rastro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        go.AddComponent<TrozoGore>().vida = Random.Range(7f, 11f);
    }
}

/// <summary>Trozo que deja una mancha la primera vez que toca el suelo y se encoge al final.</summary>
public sealed class TrozoGore : MonoBehaviour
{
    public float vida = 9f;
    float t;
    bool mancho;
    Vector3 escala;

    void Start() => escala = transform.localScale;

    void OnCollisionEnter(Collision choque)
    {
        if (mancho || choque.contactCount == 0) return;
        mancho = true;
        var contacto = choque.GetContact(0);
        EfectosPelea.Marca(contacto.point, contacto.normal, Random.Range(.12f, .25f), new Color(.34f, .01f, .01f, .85f), 25f);
    }

    void Update()
    {
        t += Time.deltaTime;
        float resto = vida - t;
        if (resto < 1f) transform.localScale = escala * Mathf.Clamp01(resto);
        if (resto <= 0f) Destroy(gameObject);
    }
}
