using System.Collections;
using UnityEngine;

public enum TipoRecogible { Municion, Curacion, Buff }

/// <summary>Munición, botiquín o bebida. Sube y se desvanece al recogerse.</summary>
[RequireComponent(typeof(Collider))]
public class RecogibleItem : MonoBehaviour
{
    public TipoRecogible tipo = TipoRecogible.Curacion;
    public int cantidad = 1;
    public AudioClip sonido;
    bool recogido;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        int capa = LayerMask.NameToLayer(CapasJuego.Recogibles);
        if (capa >= 0) gameObject.layer = capa;
        var cuerpo = GetComponent<Rigidbody>();
        if (cuerpo == null) cuerpo = gameObject.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;
    }

    void OnTriggerEnter(Collider other) => Tocar(other);

    // Si el objeto aparece encima del jugador (lo soltó un zombie a sus pies) no hay Enter: sirve Stay.
    void OnTriggerStay(Collider other) => Tocar(other);

    void Tocar(Collider other)
    {
        if (recogido || other == null) return;
        // Antes solo valía si la raíz de la jerarquía tenía el tag Player; el jugador puede estar
        // dentro de otro objeto. Se acepta el collider, su CharacterController o la raíz, como las botellas.
        var capsula = other.GetComponentInParent<CharacterController>();
        if (other.CompareTag("Player")) Recoger(other.gameObject);
        else if (capsula != null && capsula.CompareTag("Player")) Recoger(capsula.gameObject);
        else if (other.transform.root.CompareTag("Player")) Recoger(other.transform.root.gameObject);
    }

    static Transform jugadorCache;

    static void Avisar(TipoRecogible tipo) => InventarioJugador.Avisar(
        tipo == TipoRecogible.Curacion ? "+1 BOTIQUÍN" : tipo == TipoRecogible.Buff ? "+1 BEBIDA" : "+MUNICIÓN");

    /// <summary>Respaldo por distancia (como la munición): recoge aunque la física no avise del contacto.</summary>
    void RecogerPorCercania()
    {
        if (jugadorCache == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            jugadorCache = go.transform;
        }
        Vector3 d = jugadorCache.position - transform.position;
        if (Mathf.Abs(d.y) < 1.6f && d.x * d.x + d.z * d.z < 1.1f * 1.1f) Recoger(jugadorCache.gameObject);
    }

    public void Recoger(GameObject jugador)
    {
        if (recogido || jugador == null || IntroduccionIsla.Activa) return;
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida != null && (partida.EstaPausada || partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso)) return;
        recogido = true;
        var inventario = jugador.GetComponentInParent<InventarioJugador>();
        if (inventario == null) inventario = InventarioJugador.Instancia;
        Avisar(tipo);
        if (inventario != null)
        {
            if (tipo == TipoRecogible.Municion) inventario.SumarMunicion(60 * Mathf.Max(1, cantidad));
            else if (tipo == TipoRecogible.Curacion) inventario.SumarBotiquin(cantidad);
            else inventario.SumarBuff(cantidad);
        }
        Sonido.Reproducir(sonido != null ? sonido : BancoSonidos.Actual != null ? BancoSonidos.Actual.beber : null, transform.position, .8f, "Efectos");
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (isActiveAndEnabled) StartCoroutine(Elevar());
        else Destroy(gameObject);
    }

    Transform modelo;
    float fase;

    void Update()
    {
        if (recogido) return;
        RecogerPorCercania();
        // Gira y flota para que se distinga de la utilería del mapa.
        if (recogido || modelo == null) return;
        modelo.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        modelo.localPosition = Vector3.up * (.08f * Mathf.Sin((Time.time + fase) * 2.2f));
    }

    public static RecogibleItem Crear(TipoRecogible tipo, Vector3 posicion)
    {
        var objeto = new GameObject("Item_" + tipo);
        objeto.transform.position = posicion + Vector3.up * .55f;
        // Zona de recogida generosa: basta con pasar al lado.
        var zona = objeto.AddComponent<BoxCollider>();
        zona.size = new Vector3(1.1f, 1.3f, 1.1f);
        var item = objeto.AddComponent<RecogibleItem>();
        item.tipo = tipo;
        item.fase = Random.Range(0f, 6f);
        item.modelo = new GameObject("Modelo").transform;
        item.modelo.SetParent(objeto.transform, false);
        if (tipo == TipoRecogible.Curacion) ModeloBotiquin(item.modelo);
        else if (tipo == TipoRecogible.Buff) ModeloBebida(item.modelo);
        else Pieza(item.modelo, PrimitiveType.Cube, Vector3.zero, new Vector3(.42f, .26f, .26f), new Color(.55f, .5f, .18f));
        return item;
    }

    /// <summary>Maletín blanco con cruz roja por ambos lados y asa.</summary>
    static void ModeloBotiquin(Transform padre)
    {
        Pieza(padre, PrimitiveType.Cube, Vector3.zero, new Vector3(.5f, .34f, .24f), new Color(.95f, .95f, .93f));
        Color rojo = new Color(.9f, .1f, .12f);
        foreach (float z in new[] { -.123f, .123f })
        {
            Pieza(padre, PrimitiveType.Cube, new Vector3(0f, 0f, z), new Vector3(.26f, .08f, .01f), rojo, brillo: .8f);
            Pieza(padre, PrimitiveType.Cube, new Vector3(0f, 0f, z), new Vector3(.08f, .26f, .01f), rojo, brillo: .8f);
        }
        Pieza(padre, PrimitiveType.Cube, new Vector3(0f, .2f, 0f), new Vector3(.2f, .05f, .06f), new Color(.2f, .2f, .22f));
    }

    /// <summary>Lata de bebida energética con franja y tapa metálica.</summary>
    static void ModeloBebida(Transform padre)
    {
        Pieza(padre, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.17f, .2f, .17f), new Color(.1f, .55f, 1f), brillo: .5f);
        Pieza(padre, PrimitiveType.Cylinder, new Vector3(0f, .02f, 0f), new Vector3(.175f, .05f, .175f), new Color(.75f, 1f, .2f), brillo: .9f);
        Pieza(padre, PrimitiveType.Cylinder, new Vector3(0f, .205f, 0f), new Vector3(.15f, .01f, .15f), new Color(.8f, .8f, .82f));
    }

    static readonly System.Collections.Generic.Dictionary<Color, Material> materiales = new System.Collections.Generic.Dictionary<Color, Material>();

    static void Pieza(Transform padre, PrimitiveType forma, Vector3 posicion, Vector3 escala, Color color, float brillo = .25f)
    {
        var pieza = GameObject.CreatePrimitive(forma);
        // Solo es dibujo: la zona de recogida es el BoxCollider de la raíz.
        DestroyImmediate(pieza.GetComponent<Collider>());
        pieza.transform.SetParent(padre, false);
        pieza.transform.localPosition = posicion;
        pieza.transform.localScale = escala;
        var renderer = pieza.GetComponent<Renderer>();
        if (!materiales.TryGetValue(color, out var material) || material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * brillo);
            materiales[color] = material;
        }
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// Deja botiquines y bebidas repartidos por la isla, junto a algunas botellas (así quedan en el
    /// recorrido) y un botiquín cerca del inicio.
    /// </summary>
    public static void RepartirEnIsla(Transform jugador)
    {
        if (jugador == null) return;
        var botellas = Object.FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsSortMode.InstanceID);
        int puestos = 0;
        for (int i = 1; i < botellas.Length; i += 2)
        {
            var botella = botellas[i];
            if (botella.tipo != ObjetoEspecialColeccionable.TipoBotella.Objetivo || botella.GetComponentInParent<PortadorBotella>(true) != null) continue;
            // A un costado de la botella, en una dirección distinta cada vez.
            Vector3 lado = Quaternion.Euler(0f, i * 137f, 0f) * Vector3.forward * 1.7f;
            var tipo = puestos++ % 2 == 0 ? TipoRecogible.Curacion : TipoRecogible.Buff;
            Crear(tipo, SistemaArmasFuego.BuscarSuelo(botella.transform.position + lado, jugador));
        }
        Crear(TipoRecogible.Curacion, SistemaArmasFuego.BuscarSuelo(jugador.position - jugador.forward * 2f + jugador.right * 1.5f, jugador));
    }

    /// <summary>De vez en cuando un zombie suelta un botiquín o una bebida al morir.</summary>
    public static void SoltarZombie(Vector3 posicion)
    {
        float azar = Random.value;
        if (azar > .2f) return;
        var tipo = azar < .11f ? TipoRecogible.Curacion : TipoRecogible.Buff;
        Vector3 punto = posicion + new Vector3(Random.Range(-.6f, .6f), 0f, Random.Range(-.6f, .6f));
        var jugador = GameObject.FindGameObjectWithTag("Player");
        Crear(tipo, jugador != null ? SistemaArmasFuego.BuscarSuelo(punto, jugador.transform) : punto);
    }

    IEnumerator Elevar()
    {
        Vector3 inicio = transform.position;
        Vector3 escala = transform.localScale;
        float t = 0f;
        while (t < .55f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / .55f);
            transform.position = inicio + Vector3.up * (.7f * k);
            transform.localScale = escala * Mathf.Lerp(1f, .05f, k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
