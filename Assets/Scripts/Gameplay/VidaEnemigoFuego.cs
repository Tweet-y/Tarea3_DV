using UnityEngine;
using UnityEngine.AI;

/// <summary>Daño de armas independiente del objetivo de botellas.</summary>
[DisallowMultipleComponent]
public sealed class VidaEnemigoFuego : MonoBehaviour
{
    [Min(1f)] public float vidaMaxima = 175f;
    public float Vida { get; private set; }
    public bool Muerto => Vida <= 0f;
    private Bounds volumen;

    private void Start()
    {
        Vida = vidaMaxima;
        var colliders = GetComponentsInChildren<Collider>();
        volumen = new Bounds(transform.position, Vector3.zero);
        foreach (var col in colliders) volumen.Encapsulate(col.bounds);
    }

    public bool RecibirDanio(float cantidad, Vector3 punto)
    {
        if (Muerto || cantidad <= 0f) return false;
        Vida = Mathf.Max(0f, Vida - cantidad);
        if (!Muerto) return true;
        var perseguidor = GetComponent<EnemigoAcechador>();
        if (perseguidor != null) perseguidor.enabled = false;
        foreach (var trampa in GetComponentsInChildren<TrampaDanio>()) trampa.enabled = false;
        var agente = GetComponent<NavMeshAgent>();
        if (agente != null) agente.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var anim in GetComponentsInChildren<Animator>()) anim.enabled = false;
        // Ocultar el cuerpo tras la derrota evita dejar un enemigo activo o una barrera invisible.
        gameObject.SetActive(false);
        return true;
    }

    public bool EsCabeza(Vector3 punto)
    {
        // El volumen se desplaza con el enemigo; no usar su posicion inicial como hitbox.
        return punto.y >= transform.position.y + volumen.size.y * .82f;
    }
}
