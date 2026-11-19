using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Mantiene la presión de los zombies en la isla. Convierte a los zombies de la escena en horda,
/// repone a los que mueren fuera de la vista del jugador y cada cierto tiempo lanza una oleada.
/// Mientras más botellas lleva el jugador, más zombies hay, más rápidos son y más seguido llegan.
/// Se crea solo en cualquier escena que tenga zombies; no hay que configurar nada.
/// </summary>
public sealed class DirectorZombies : MonoBehaviour
{
    public static DirectorZombies Instancia { get; private set; }

    const int MaximoVivos = 30;
    const float DistanciaMinima = 20f, DistanciaMaxima = 36f, DistanciaReciclar = 75f;

    readonly List<GameObject> plantillas = new List<GameObject>();
    readonly List<EnemigoAcechador> vivos = new List<EnemigoAcechador>();
    readonly HashSet<EnemigoAcechador> generados = new HashSet<EnemigoAcechador>();
    Transform jugador;
    ControladorPartidaBotellas partida;
    NavMeshQueryFilter filtro;
    NavMeshPath ruta;
    int inicial;
    float proximaAparicion, proximaOleada;
    bool listo, detenido;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        Intentar();
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo) => Intentar();

    static void Intentar()
    {
        if (Instancia != null) return;
        foreach (var e in FindObjectsByType<EnemigoAcechador>(FindObjectsSortMode.None))
            if (EsZombie(e))
            {
                new GameObject("DirectorZombies").AddComponent<DirectorZombies>();
                return;
            }
    }

    static bool EsZombie(EnemigoAcechador e)
    {
        if (e == null || !e.name.StartsWith("Zombie")) return false;
        var vida = e.GetComponent<ControladorVida>();
        return vida == null || !vida.esJefe;
    }

    /// <summary>Corta las apariciones (empieza la pelea final).</summary>
    public static void Detener()
    {
        if (Instancia != null) Instancia.detenido = true;
    }

    void Awake() => Instancia = this;

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    float Progreso => partida != null && partida.Total > 0 ? Mathf.Clamp01((float)partida.Recogidas / partida.Total) : 0f;

    IEnumerator Start()
    {
        // Un cuadro de margen: la partida, el jugador y los agentes terminan de inicializarse.
        yield return null;
        partida = ControladorPartidaBotellas.Instancia;
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) { Destroy(gameObject); yield break; }
        jugador = go.transform;
        ruta = new NavMeshPath();

        foreach (var e in FindObjectsByType<EnemigoAcechador>(FindObjectsSortMode.None))
        {
            if (!EsZombie(e)) continue;
            vivos.Add(e);
            Afinar(e);
        }
        inicial = vivos.Count;
        if (inicial == 0) { Destroy(gameObject); yield break; }

        var agente = vivos[0].GetComponent<NavMeshAgent>();
        filtro = new NavMeshQueryFilter { agentTypeID = agente != null ? agente.agentTypeID : 0, areaMask = NavMesh.AllAreas };

        // Copias apagadas de los zombies de la escena: de ellas salen los refuerzos.
        for (int i = 0; i < vivos.Count && plantillas.Count < 8; i++)
        {
            var origen = vivos[i].gameObject;
            if (!origen.activeSelf) continue;
            origen.SetActive(false);
            var copia = Instantiate(origen, transform);
            origen.SetActive(true);
            copia.name = "Plantilla_" + origen.name;
            plantillas.Add(copia);
        }
        proximaAparicion = Time.time + 18f;
        proximaOleada = Time.time + 70f;
        listo = true;
    }

    /// <summary>Convierte al zombie en horda. Algunos corren casi tanto como el jugador; otros solo trotan.</summary>
    void Afinar(EnemigoAcechador e)
    {
        float p = Progreso;
        bool corredor = Random.value < Mathf.Lerp(.25f, .5f, p);
        float correr = corredor ? Random.Range(4.5f, 5.1f) : Random.Range(3.1f, 4f);
        correr = Mathf.Min(5.3f, correr * Mathf.Lerp(1f, 1.08f, p));
        e.ConfigurarHorda(correr, 26f, 7f);
        e.rangoAtaque = 1.9f;
        e.cadenciaAtaque = Random.Range(.95f, 1.25f);
    }

    void Update()
    {
        if (!listo || detenido || jugador == null) return;
        if (partida != null && (partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso || partida.EstaPausada)) return;
        if (IntroduccionIsla.Activa || Time.timeScale <= 0f) return;

        float p = Progreso;
        Limpiar();
        int objetivo = Mathf.Min(MaximoVivos, inicial + Mathf.RoundToInt(Mathf.Lerp(3f, 15f, p)));

        if (Time.time >= proximaAparicion)
        {
            proximaAparicion = Time.time + Mathf.Lerp(7f, 2.5f, p);
            if (vivos.Count < objetivo) Aparecer(Random.Range(0f, 360f), 360f, Random.value < Mathf.Lerp(.6f, .9f, p));
        }

        if (Time.time >= proximaOleada)
        {
            proximaOleada = Time.time + Mathf.Lerp(80f, 45f, p);
            // La oleada llega junta desde un mismo lado y ya sabe dónde está el jugador.
            float lado = Random.Range(0f, 360f);
            int cantidad = Mathf.Min(MaximoVivos - vivos.Count, 3 + Mathf.RoundToInt(4f * p));
            for (int i = 0; i < cantidad; i++) Aparecer(lado, 50f, true);
        }
    }

    void Limpiar()
    {
        for (int i = vivos.Count - 1; i >= 0; i--)
        {
            var e = vivos[i];
            var vida = e != null ? e.GetComponent<ControladorVida>() : null;
            if (e == null || (vida != null && vida.Muerto))
            {
                generados.Remove(e);
                vivos.RemoveAt(i);
                continue;
            }
            // Los refuerzos que quedaron muy atrás se retiran para volver a aparecer cerca.
            if (generados.Contains(e) && (e.transform.position - jugador.position).sqrMagnitude > DistanciaReciclar * DistanciaReciclar
                && !EnVista(e.transform.position))
            {
                generados.Remove(e);
                vivos.RemoveAt(i);
                Destroy(e.gameObject);
            }
        }
    }

    void Aparecer(float angulo, float abanico, bool cazador)
    {
        if (plantillas.Count == 0 || !BuscarPunto(angulo, abanico, out var punto)) return;
        var plantilla = plantillas[Random.Range(0, plantillas.Count)];
        Vector3 mirar = GeografiaIsla.Plano(jugador.position - punto);
        var zombie = Instantiate(plantilla, punto, mirar.sqrMagnitude > .01f ? Quaternion.LookRotation(mirar) : Quaternion.identity);
        zombie.name = "Zombie_Refuerzo";
        zombie.SetActive(true);
        var acecho = zombie.GetComponent<EnemigoAcechador>();
        if (acecho == null) { Destroy(zombie); return; }
        acecho.objetivo = jugador;
        Afinar(acecho);
        if (cazador) acecho.Alertar(false);
        vivos.Add(acecho);
        generados.Add(acecho);
    }

    /// <summary>Punto del NavMesh lejos del jugador, fuera de cámara y con camino hasta él.</summary>
    bool BuscarPunto(float angulo, float abanico, out Vector3 punto)
    {
        punto = default;
        if (!NavMesh.SamplePosition(jugador.position, out var pie, 8f, filtro)) return false;
        for (int intento = 0; intento < 14; intento++)
        {
            float a = (angulo + Random.Range(-abanico, abanico) * .5f) * Mathf.Deg2Rad;
            Vector3 candidato = jugador.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(DistanciaMinima, DistanciaMaxima);
            if (!NavMesh.SamplePosition(candidato, out var hit, 8f, filtro)) continue;
            if (GeografiaIsla.Plano(hit.position - jugador.position).magnitude < DistanciaMinima * .85f) continue;
            if (EnVista(hit.position)) continue;
            if (!NavMesh.CalculatePath(hit.position, pie.position, filtro, ruta) || ruta.status != NavMeshPathStatus.PathComplete) continue;
            punto = hit.position;
            return true;
        }
        return false;
    }

    /// <summary>Verdadero si la cámara puede ver ese punto: nadie debe aparecer frente al jugador.</summary>
    static bool EnVista(Vector3 punto)
    {
        var camara = Camera.main;
        if (camara == null) return false;
        Vector3 cabeza = punto + Vector3.up * 1.6f;
        Vector3 v = camara.WorldToViewportPoint(cabeza);
        if (v.z <= 0f || v.x < -.08f || v.x > 1.08f || v.y < -.15f || v.y > 1.15f) return false;
        return !Physics.Linecast(camara.transform.position, cabeza, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
    }
}
