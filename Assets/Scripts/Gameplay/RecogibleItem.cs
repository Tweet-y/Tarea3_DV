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

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.CompareTag("Player")) Recoger(other.transform.root.gameObject);
    }

    public void Recoger(GameObject jugador)
    {
        if (recogido || jugador == null || IntroduccionIsla.Activa) return;
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida != null && (partida.EstaPausada || partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso)) return;
        recogido = true;
        var inventario = jugador.GetComponent<InventarioJugador>();
        if (inventario == null) inventario = InventarioJugador.Instancia;
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

    public static RecogibleItem Crear(TipoRecogible tipo, Vector3 posicion)
    {
        var objeto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        objeto.name = "Item_" + tipo;
        objeto.transform.position = posicion + Vector3.up * .45f;
        objeto.transform.localScale = Vector3.one * .35f;
        var item = objeto.AddComponent<RecogibleItem>();
        item.tipo = tipo;
        var renderer = objeto.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = tipo == TipoRecogible.Municion ? new Color(.85f, .7f, .2f) :
                tipo == TipoRecogible.Curacion ? new Color(.3f, .85f, .45f) : new Color(.35f, .7f, 1f);
            renderer.sharedMaterial = material;
        }
        return item;
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
