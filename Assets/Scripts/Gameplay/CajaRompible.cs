using UnityEngine;

/// <summary>Al quedarse sin vida suelta restos y un objeto al azar.</summary>
[DisallowMultipleComponent]
public class CajaRompible : MonoBehaviour
{
    public GameObject efectoImpacto;
    public AudioClip sonidoRotura;
    public TipoRecogible[] tabla = { TipoRecogible.Municion, TipoRecogible.Curacion, TipoRecogible.Buff };

    void OnEnable()
    {
        var vida = GetComponent<ControladorVida>();
        if (vida == null)
        {
            vida = gameObject.AddComponent<ControladorVida>();
            vida.vidaMaxima = 1f;
            vida.Reiniciar();
        }
        vida.segundosDestruir = .15f;
        vida.alMorir.AddListener(Romper);
    }

    void OnDisable()
    {
        var vida = GetComponent<ControladorVida>();
        if (vida != null) vida.alMorir.RemoveListener(Romper);
    }

    public void Romper()
    {
        Sonido.Reproducir(sonidoRotura != null ? sonidoRotura : BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, transform.position, .9f, "Efectos");
        if (efectoImpacto != null) Instantiate(efectoImpacto, transform.position, Quaternion.identity);
        for (int i = 0; i < 4; i++)
        {
            var resto = GameObject.CreatePrimitive(PrimitiveType.Cube);
            resto.name = "RestoCaja";
            resto.transform.position = transform.position + Random.insideUnitSphere * .2f;
            resto.transform.localScale = Vector3.one * Random.Range(.12f, .28f);
            int capa = LayerMask.NameToLayer(CapasJuego.Objetos);
            if (capa >= 0) resto.layer = capa;
            var cuerpo = resto.AddComponent<Rigidbody>();
            cuerpo.mass = .4f;
            cuerpo.AddForce(Random.onUnitSphere * 2.5f, ForceMode.Impulse);
            Destroy(resto, 4f);
        }
        var tipo = tabla != null && tabla.Length > 0 ? tabla[Random.Range(0, tabla.Length)] : TipoRecogible.Curacion;
        RecogibleItem.Crear(tipo, transform.position);
        foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
    }
}
