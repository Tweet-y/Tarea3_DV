using System.Collections.Generic;
using UnityEngine;

/// <summary>Hitbox que solo hace daño mientras el golpe está abierto.</summary>
[RequireComponent(typeof(Collider))]
public class GolpeMelee : MonoBehaviour
{
    public float danio = 40f;
    public float danioJugador = 0.12f;
    public AudioClip sonidoImpacto;
    public bool activo;

    Collider colision;
    readonly HashSet<int> golpeados = new HashSet<int>();

    void Awake()
    {
        colision = GetComponent<Collider>();
        if (colision == null) colision = gameObject.AddComponent<BoxCollider>();
        colision.isTrigger = true;
        var cuerpo = GetComponent<Rigidbody>();
        if (cuerpo == null) cuerpo = gameObject.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;
        colision.enabled = false;
    }

    public void InicioGolpe()
    {
        golpeados.Clear();
        activo = true;
        if (colision != null) colision.enabled = true;
    }

    public void FinGolpe()
    {
        activo = false;
        if (colision != null) colision.enabled = false;
    }

    void OnTriggerEnter(Collider other) => Pegar(other);

    public void Golpear()
    {
        Physics.SyncTransforms();
        InicioGolpe();
        var caja = colision as BoxCollider;
        Vector3 centro = caja != null ? transform.TransformPoint(caja.center) : transform.position;
        Vector3 mitad = caja != null ? Vector3.Scale(caja.size * .5f, Abs(transform.lossyScale)) : Vector3.one * .5f;
        int mascara = MascaraPropia();
        foreach (var otro in Physics.OverlapBox(centro, mitad, transform.rotation, mascara, QueryTriggerInteraction.Collide))
            Pegar(otro);
        foreach (var otro in Physics.OverlapSphere(centro, 1.2f, mascara, QueryTriggerInteraction.Collide))
            Pegar(otro);
        int jugadorCapa = LayerMask.NameToLayer(CapasJuego.Jugador);
        var jugador = GameObject.FindGameObjectWithTag("Player");
        float dist = jugador == null ? -1f : Vector3.Distance(centro, jugador.transform.position + Vector3.up * .9f);
        if (jugadorCapa >= 0 && !Physics.GetIgnoreLayerCollision(gameObject.layer, jugadorCapa))
        {
            if (jugador != null && dist < 1.6f)
            {
                var capsula = jugador.GetComponent<CharacterController>();
                if (capsula == null) capsula = jugador.GetComponentInChildren<CharacterController>();
                if (capsula != null) Pegar(capsula);
            }
        }
        int enemigos = LayerMask.NameToLayer(CapasJuego.Enemigos);
        if (enemigos >= 0 && !Physics.GetIgnoreLayerCollision(gameObject.layer, enemigos))
        {
            foreach (var vida in Object.FindObjectsByType<ControladorVida>(FindObjectsSortMode.None))
            {
                if (vida.gameObject == transform.root.gameObject || vida.Muerto) continue;
                if (Vector3.Distance(centro, vida.transform.position + Vector3.up) > 1.6f) continue;
                var col = vida.GetComponent<Collider>();
                if (col != null) Pegar(col);
            }
        }
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    int MascaraPropia()
    {
        int mascara = 0;
        for (int i = 0; i < 32; i++)
            if (!Physics.GetIgnoreLayerCollision(gameObject.layer, i)) mascara |= 1 << i;
        return mascara;
    }

    void Pegar(Collider other)
    {
        if (!activo || other == null || EsPropio(other)) return;
        var vida = other.GetComponentInParent<ControladorVida>();
        var capsula = other.GetComponentInParent<CharacterController>();
        int id = vida != null ? vida.GetInstanceID() : capsula != null ? capsula.GetInstanceID() : other.GetInstanceID();
        if (!golpeados.Add(id)) return;
        if (vida != null && (capsula == null || !capsula.CompareTag("Player")))
        {
            float golpe = danio;
            if (gameObject.layer == LayerMask.NameToLayer(CapasJuego.ArmaJugador) && InventarioJugador.Instancia != null)
                golpe *= InventarioJugador.Instancia.MultiplicadorDanio;
            vida.RecibirDanio(golpe, other.ClosestPoint(transform.position));
            Sonido.Reproducir(sonidoImpacto != null ? sonidoImpacto : BancoSonidos.Actual != null ? BancoSonidos.Actual.impacto : null, transform.position, .8f, "Efectos");
            return;
        }
            if (capsula != null && capsula.CompareTag("Player"))
            {
                var partida = ControladorPartidaBotellas.Instancia;
            if (partida != null) partida.RecibirDanio(danioJugador, ControladorPartidaBotellas.Causa.Enemigo, true);
            Sonido.Reproducir(sonidoImpacto != null ? sonidoImpacto : BancoSonidos.Actual != null ? BancoSonidos.Actual.impacto : null, transform.position, .8f, "Efectos");
        }
    }

    bool EsPropio(Collider other)
    {
        var enemigo = GetComponentInParent<EnemigoAcechador>();
        if (enemigo != null && other.transform.IsChildOf(enemigo.transform)) return true;
        var inventario = GetComponentInParent<InventarioJugador>();
        if (inventario != null && other.transform.IsChildOf(inventario.transform)) return true;
        return false;
    }
}
