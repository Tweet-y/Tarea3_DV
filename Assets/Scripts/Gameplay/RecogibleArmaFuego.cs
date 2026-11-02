using System.Collections.Generic;
using UnityEngine;

/// <summary>Recogida explicita a corta distancia. El mismo estado viaja al soltar y recoger el rifle.</summary>
public sealed class RecogibleArmaFuego : MonoBehaviour
{
    public static readonly HashSet<RecogibleArmaFuego> Activos = new HashSet<RecogibleArmaFuego>();
    public ArmaFuegoDefinicion Definicion { get; private set; }
    public EstadoMunicion Municion { get; private set; }
    public bool SoloMunicion { get; private set; }
    public int Balas { get; private set; }
    private Transform visual;
    private float fase;

    public static RecogibleArmaFuego Crear(ArmaFuegoDefinicion datos, Vector3 suelo,
        EstadoMunicion estado = null, bool soloMunicion = false)
    {
        var go = new GameObject(soloMunicion ? "Municion_Rifle_60" : "Recoger_SM_Wep_Rifle_Assault_03");
        go.transform.position = suelo;
        var pickup = go.AddComponent<RecogibleArmaFuego>();
        pickup.Definicion = datos;
        pickup.Municion = estado ?? new EstadoMunicion(datos.capacidad, datos.reservaMaxima, datos.capacidad, datos.reservaInicial);
        pickup.SoloMunicion = soloMunicion;
        pickup.Balas = 60;
        var pivot = new GameObject("ModeloFlotante").transform;
        pivot.SetParent(go.transform, false);
        pickup.visual = pivot;
        var modelo = Instantiate(datos.modelo, pivot);
        PrepararModelo(modelo);
        modelo.transform.localScale = Vector3.one * (soloMunicion ? .6f : 1f);
        // Las cajas de municion muestran el cargador real del rifle, sin duplicar el arma completa.
        if (soloMunicion)
        {
            foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
                r.enabled = r.name.Contains("_Mag_");
        }
        return pickup;
    }

    public static void PrepararModelo(GameObject modelo)
    {
        foreach (var col in modelo.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var body in modelo.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("_Flash_")) t.gameObject.SetActive(false);
    }

    public bool Recoger(ControladorArmaFuego jugador)
    {
        if (!Activos.Contains(this) || !jugador.PuedeRecoger(this)) return false;
        if (SoloMunicion)
        {
            if (jugador.Municion == null) return false;
            int recibidas = jugador.Municion.AgregarReserva(Balas);
            if (recibidas == 0) return false;
            Balas -= recibidas;
            if (Balas > 0) return true;
        }
        else
        {
            if (jugador.Equipada) return false;
            jugador.Equipar(Definicion, Municion);
        }
        Activos.Remove(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    private void OnEnable() => Activos.Add(this);
    private void OnDisable() => Activos.Remove(this);
    private void Update()
    {
        if (Time.timeScale == 0f || visual == null) return;
        fase += Time.deltaTime;
        visual.localPosition = Vector3.up * (.65f + Mathf.Sin(fase * 2f) * .08f);
        visual.localRotation = Quaternion.Euler(0, fase * 35f, SoloMunicion ? 0 : -12f);
    }
}
