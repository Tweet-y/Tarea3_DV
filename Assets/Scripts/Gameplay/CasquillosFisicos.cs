using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Casquillos y cartuchos vacíos con física real: saltan por la ventana de expulsión, rebotan con un
/// tintineo, quedan un rato en el suelo y se encogen al desaparecer. Van en la capa Ignore Raycast
/// para no tapar disparos ni la cámara, y no chocan con el jugador.
/// </summary>
public static class CasquillosFisicos
{
    const int Maximo = 45;
    const float Vida = 9f;
    static readonly Queue<GameObject> activos = new Queue<GameObject>();
    static AudioClip tintineo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => activos.Clear();

    public static void Expulsar(ArmaFuegoDefinicion datos, Vector3 posicion, Quaternion rotacion, Vector3 velocidad,
        Collider ignorar)
    {
        var go = Crear(datos);
        go.transform.SetPositionAndRotation(posicion, rotacion);
        var cuerpo = go.GetComponent<Rigidbody>();
        cuerpo.linearVelocity = velocidad;
        cuerpo.angularVelocity = Random.insideUnitSphere * 25f;
        if (ignorar != null)
            foreach (var col in go.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, ignorar);
        activos.Enqueue(go);
        while (activos.Count > Maximo)
        {
            var viejo = activos.Dequeue();
            if (viejo != null) Object.Destroy(viejo);
        }
    }

    static GameObject Crear(ArmaFuegoDefinicion datos)
    {
        GameObject go;
        if (datos.casquillo != null)
        {
            go = new GameObject("Casquillo_" + datos.casquillo.name);
            var modelo = Object.Instantiate(datos.casquillo, go.transform);
            modelo.transform.localPosition = Vector3.zero;
            modelo.transform.localRotation = Quaternion.identity;
            modelo.transform.localScale = Vector3.Scale(modelo.transform.localScale, datos.escalaCasquillo);
            foreach (var col in modelo.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);
            foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Colisionador a la medida del modelo, centrado en él.
            Bounds b = new Bounds();
            bool alguno = false;
            foreach (var mf in modelo.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var local = mf.sharedMesh.bounds;
                Vector3 centro = go.transform.InverseTransformPoint(mf.transform.TransformPoint(local.center));
                Vector3 tam = Vector3.Scale(local.size, mf.transform.lossyScale);
                var caja = new Bounds(centro, tam);
                if (!alguno) { b = caja; alguno = true; }
                else b.Encapsulate(caja);
            }
            var capsula = go.AddComponent<CapsuleCollider>();
            if (alguno)
            {
                int eje = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.y >= b.size.z ? 1 : 2;
                capsula.direction = eje;
                capsula.center = b.center;
                capsula.height = b.size[eje];
                capsula.radius = Mathf.Max(.004f, .5f * Mathf.Min(b.size[(eje + 1) % 3], b.size[(eje + 2) % 3]));
            }
        }
        else
        {
            // Casquillo de latón: el cilindro de Unity mide 2 de alto y 1 de diámetro.
            go = new GameObject("Casquillo_Laton");
            var visual = new GameObject("Modelo");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.Scale(new Vector3(.013f, .021f, .013f), datos.escalaCasquillo);
            visual.AddComponent<MeshFilter>().sharedMesh = EfectosPelea.Cilindro;
            var render = visual.AddComponent<MeshRenderer>();
            render.sharedMaterial = EfectosPelea.MaterialCasquillo;
            render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var capsula = go.AddComponent<CapsuleCollider>();
            capsula.direction = 1;
            capsula.height = .042f * datos.escalaCasquillo.y;
            capsula.radius = .0065f * datos.escalaCasquillo.x;
        }
        int capa = LayerMask.NameToLayer("Ignore Raycast");
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Mathf.Max(0, capa);
        var cuerpo = go.AddComponent<Rigidbody>();
        cuerpo.mass = .02f;
        cuerpo.linearDamping = .05f;
        cuerpo.angularDamping = .4f;
        cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
        cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        go.AddComponent<Casquillo>().Iniciar(Vida, Tintineo, datos.casquillo != null);
        return go;
    }

    /// <summary>Golpe metálico corto y agudo, generado una vez.</summary>
    static AudioClip Tintineo
    {
        get
        {
            if (tintineo != null) return tintineo;
            const int frecuencia = 22050;
            var muestras = new float[frecuencia / 6];
            for (int i = 0; i < muestras.Length; i++)
            {
                float t = (float)i / frecuencia;
                float env = Mathf.Exp(-t * 38f);
                muestras[i] = (Mathf.Sin(t * 5200f * Mathf.PI * 2f) * .55f + Mathf.Sin(t * 7900f * Mathf.PI * 2f) * .3f
                    + Mathf.Sin(t * 3100f * Mathf.PI * 2f) * .25f) * env * .35f;
            }
            tintineo = AudioClip.Create("TintineoCasquillo", muestras.Length, 1, frecuencia, false);
            tintineo.SetData(muestras, 0);
            return tintineo;
        }
    }
}

/// <summary>Vida de un casquillo: suena al rebotar, se queda quieto y se encoge al final.</summary>
public sealed class Casquillo : MonoBehaviour
{
    float vida, nacido;
    AudioClip sonido;
    bool plastico;
    int rebotes;
    Vector3 escala;

    public void Iniciar(float segundos, AudioClip clip, bool cartucho)
    {
        vida = segundos;
        sonido = clip;
        plastico = cartucho;
        nacido = Time.time;
        escala = transform.localScale;
    }

    void OnCollisionEnter(Collision choque)
    {
        if (rebotes > 2 || choque.relativeVelocity.magnitude < .6f) return;
        rebotes++;
        // El cartucho de escopeta es de plástico: suena más grave y apagado.
        float tono = plastico ? Random.Range(.45f, .6f) : Random.Range(.9f, 1.25f);
        Sonido.Reproducir(sonido, transform.position, (plastico ? .18f : .28f) / rebotes, "Efectos", tono);
    }

    void Update()
    {
        float edad = Time.time - nacido;
        float resto = vida - edad;
        if (resto < .6f) transform.localScale = escala * Mathf.Clamp01(resto / .6f);
        if (resto <= 0f) Destroy(gameObject);
    }
}
