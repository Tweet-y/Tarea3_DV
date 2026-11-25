using System.Collections.Generic;
using UnityEngine;

/// <summary>Recogida explicita a corta distancia. El mismo estado viaja al soltar y recoger el arma.</summary>
public sealed class RecogibleArmaFuego : MonoBehaviour
{
    public static readonly HashSet<RecogibleArmaFuego> Activos = new HashSet<RecogibleArmaFuego>();
    public ArmaFuegoDefinicion Definicion { get; private set; }
    public EstadoMunicion Municion { get; private set; }
    public bool SoloMunicion { get; private set; }
    public int Balas { get; private set; }
    /// <summary>Munición soltada por un zombie: se recoge sola al pasar encima.</summary>
    public bool AutoRecoger { get; private set; }
    private Transform visual;
    private float fase, expira;
    private ControladorArmaFuego jugador;

    /// <param name="balas">Munición que trae; menor que 1 = la caja estándar del arma.</param>
    /// <param name="duracion">Segundos antes de desaparecer; 0 = queda para siempre.</param>
    public static RecogibleArmaFuego Crear(ArmaFuegoDefinicion datos, Vector3 suelo,
        EstadoMunicion estado = null, bool soloMunicion = false, int balas = 0, bool autoRecoger = false, float duracion = 0f)
    {
        string nombre = datos.tipo == ArmaFuegoDefinicion.Tipo.Escopeta ? "Escopeta" : "Rifle";
        int cantidad = balas > 0 ? balas : datos.balasPorCaja;
        var go = new GameObject(soloMunicion ? $"Municion_{nombre}_{cantidad}" : "Recoger_" + nombre);
        go.transform.position = suelo;
        var pickup = go.AddComponent<RecogibleArmaFuego>();
        pickup.Definicion = datos;
        pickup.Municion = estado ?? new EstadoMunicion(datos.capacidad, datos.reservaMaxima, datos.capacidad, datos.reservaInicial);
        pickup.SoloMunicion = soloMunicion;
        pickup.Balas = cantidad;
        pickup.AutoRecoger = autoRecoger;
        pickup.expira = duracion > 0f ? Time.time + duracion : 0f;
        var pivot = new GameObject("ModeloFlotante").transform;
        pivot.SetParent(go.transform, false);
        pickup.visual = pivot;
        if (soloMunicion && datos.modeloMunicion != null)
        {
            // Caja de cartuchos real, un poco más grande para que se vea en el suelo.
            var caja = Instantiate(datos.modeloMunicion, pivot);
            PrepararModelo(caja);
            caja.transform.localScale *= 1.6f;
            return pickup;
        }
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
            var municion = jugador.MunicionDe(Definicion.tipo);
            if (municion == null) return false;
            int recibidas = municion.AgregarReserva(Balas);
            if (recibidas == 0) return false;
            Balas -= recibidas;
            if (Balas > 0) return true;
        }
        else
        {
            if (jugador.Tiene(Definicion.tipo)) return false;
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
        if (expira > 0f)
        {
            float resto = expira - Time.time;
            // Parpadea los últimos segundos antes de desaparecer.
            visual.gameObject.SetActive(resto > 4f || Mathf.Repeat(resto, .4f) > .15f);
            if (resto <= 0f) { Destroy(gameObject); return; }
        }
        if (!AutoRecoger) return;
        if (jugador == null) jugador = FindFirstObjectByType<ControladorArmaFuego>();
        if (jugador == null) return;
        Vector3 d = jugador.transform.position - transform.position;
        if (Mathf.Abs(d.y) < 1.6f && GeografiaIsla.Plano(d).sqrMagnitude < 1.5f * 1.5f && Recoger(jugador))
            Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.buff : null, transform.position, .35f, "Efectos", 1.6f);
    }
}
