using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>Vida compartida por zombis, el jefe y las cajas.</summary>
[DisallowMultipleComponent]
public class ControladorVida : MonoBehaviour
{
    public float vidaMaxima = 60f;
    public UnityEvent<float> alCambiarVida;
    public UnityEvent alMorir;
    public string triggerGolpe = "Golpe";
    public string triggerMorir = "Morir";
    public AudioClip sonidoGolpe;
    public AudioClip sonidoMuerte;
    public bool esJefe;
    public float segundosDestruir = 4f;
    public float Vida { get; private set; }
    public bool Muerto => Vida <= 0f;
    public float Fraccion => vidaMaxima <= 0f ? 0f : Vida / vidaMaxima;

    Bounds volumen;
    bool volumenListo;

    void Awake() => Reiniciar();

    public void Reiniciar()
    {
        Vida = vidaMaxima;
        Medir();
        alCambiarVida?.Invoke(Fraccion);
    }

    void Medir()
    {
        volumen = new Bounds(transform.position, Vector3.one);
        bool alguno = false;
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (!alguno) { volumen = col.bounds; alguno = true; }
            else volumen.Encapsulate(col.bounds);
        }
        volumenListo = alguno;
    }

    public bool EsCabeza(Vector3 punto)
    {
        if (!volumenListo) Medir();
        return punto.y >= transform.position.y + volumen.size.y * .82f;
    }

    float proximaAnimGolpe;

    /// <param name="direccion">Direccion del golpe o proyectil; vacia = se deduce desde el jugador.</param>
    public bool RecibirDanio(float cantidad, Vector3 punto, Vector3 direccion = default)
    {
        if (Muerto || cantidad <= 0f) return false;
        Vida = Mathf.Max(0f, Vida - cantidad);
        alCambiarVida?.Invoke(Fraccion);
        var anim = GetComponent<Animator>();
        // Solo enemigos con cuerpo animado: las cajas no reaccionan.
        if (anim != null && GetComponent<EnemigoAcechador>() != null)
        {
            var reaccion = GetComponent<ReaccionImpacto>();
            if (reaccion == null) reaccion = gameObject.AddComponent<ReaccionImpacto>();
            reaccion.Impactar(punto, direccion, cantidad, EsCabeza(punto), Muerto);
        }
        if (Muerto)
        {
            if (anim != null) anim.SetTrigger(triggerMorir);
            Sonido.Reproducir(sonidoMuerte != null ? sonidoMuerte : BancoSonidos.Actual != null ? BancoSonidos.Actual.muerteEnemigo : null, transform.position, 1f, "Efectos");
            Morir();
            return true;
        }
        // En rafaga, relanzar la animacion en cada bala la deja congelada en el primer fotograma.
        if (anim != null && Time.time >= proximaAnimGolpe)
        {
            anim.SetTrigger(triggerGolpe);
            proximaAnimGolpe = Time.time + (esJefe ? 1.5f : .5f);
        }
        Sonido.Reproducir(sonidoGolpe != null ? sonidoGolpe : BancoSonidos.Actual != null ? BancoSonidos.Actual.impacto : null, punto, .9f, "Efectos");
        return true;
    }

    void Morir()
    {
        alMorir?.Invoke();
        var acecho = GetComponent<EnemigoAcechador>();
        if (acecho != null) acecho.enabled = false;
        var agente = GetComponent<NavMeshAgent>();
        if (agente != null) agente.enabled = false;
        foreach (var golpe in GetComponentsInChildren<GolpeMelee>()) golpe.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        if (segundosDestruir >= 0f) Destroy(gameObject, segundosDestruir);
    }
}
