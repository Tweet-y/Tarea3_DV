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
    // Los refuerzos aparecen lejos y fuera de cámara: hay que caminar hacia ellos, no salen encima.
    const float DistanciaMinima = 34f, DistanciaMaxima = 52f, DistanciaReciclar = 95f;

    readonly List<GameObject> plantillas = new List<GameObject>();
    readonly List<EnemigoAcechador> vivos = new List<EnemigoAcechador>();
    readonly HashSet<EnemigoAcechador> generados = new HashSet<EnemigoAcechador>();
    readonly Dictionary<EnemigoAcechador, (Vector3 pos, float desde)> rastro = new Dictionary<EnemigoAcechador, (Vector3 pos, float desde)>();
    Transform jugador;
    ControladorPartidaBotellas partida;
    NavMeshQueryFilter filtro;
    NavMeshPath ruta;
    int inicial;
    float proximaAparicion, proximaOleada, proximaRevision;
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
            if (EsZombie(e)) vivos.Add(e);
        if (vivos.Count == 0) { Destroy(gameObject); yield break; }

        var agente = vivos[0].GetComponent<NavMeshAgent>();
        filtro = new NavMeshQueryFilter { agentTypeID = agente != null ? agente.agentTypeID : 0, areaMask = NavMesh.AllAreas };

        // Algunos zombies de la escena quedaron bajo el mar o en un trozo de NavMesh aislado y nunca se movían:
        // se llevan a tierra firme conectada con el jugador antes de empezar.
        for (int i = vivos.Count - 1; i >= 0; i--)
        {
            if (Reubicar(vivos[i])) continue;
            Destroy(vivos[i].gameObject);
            vivos.RemoveAt(i);
        }
        foreach (var e in vivos) Afinar(e);
        inicial = vivos.Count;
        if (inicial == 0) { Destroy(gameObject); yield break; }

        // Copias apagadas de los zombies de la escena: de ellas salen los refuerzos.
        for (int i = 0; i < vivos.Count && plantillas.Count < 8; i++)
        {
            var origen = vivos[i].gameObject;
            if (!origen.activeSelf) continue;
            origen.SetActive(false);
            var copia = Instantiate(origen, transform);
            origen.SetActive(true);
            copia.name = "Plantilla_" + origen.name;
            // La intro apaga el EnemigoAcechador y el NavMeshAgent de los zombies de la escena; la copia
            // heredaba ese estado y los refuerzos aparecían sin IA, quietos y flotando sobre el NavMesh.
            var cerebro = copia.GetComponent<EnemigoAcechador>();
            if (cerebro != null) cerebro.enabled = true;
            var agenteCopia = copia.GetComponent<NavMeshAgent>();
            if (agenteCopia != null) agenteCopia.enabled = true;
            plantillas.Add(copia);
        }
        proximaAparicion = Time.time + 35f;
        proximaOleada = Time.time + 120f;
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
        if (e.GetComponent<SonidoZombie>() == null) e.gameObject.AddComponent<SonidoZombie>();
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
        RevisarAtascados();
        int objetivo = Mathf.Min(MaximoVivos, inicial + Mathf.RoundToInt(Mathf.Lerp(2f, 8f, p)));

        if (Time.time >= proximaAparicion)
        {
            proximaAparicion = Time.time + Mathf.Lerp(15f, 7f, p);
            // Si ya lo están acosando varios, no se suman más: primero tiene que sacárselos de encima.
            if (vivos.Count < objetivo && Acosando() < 5) Aparecer(Random.Range(0f, 360f), 360f, Random.value < Mathf.Lerp(.25f, .5f, p));
        }

        if (Time.time >= proximaOleada)
        {
            proximaOleada = Time.time + Mathf.Lerp(130f, 80f, p);
            // La oleada llega junta desde un mismo lado y ya sabe dónde está el jugador.
            float lado = Random.Range(0f, 360f);
            int cantidad = Mathf.Min(MaximoVivos - vivos.Count, 2 + Mathf.RoundToInt(3f * p));
            for (int i = 0; i < cantidad; i++) Aparecer(lado, 50f, true);
        }
    }

    /// <summary>El NavMesh horneado también cubre el fondo del mar: eso no es isla.</summary>
    static bool EnTierra(Vector3 p) => p.y > GeografiaIsla.NivelAgua + .3f;

    bool Conectado(Vector3 p, bool hayPie, Vector3 pie)
    {
        if (!EnTierra(p)) return false;
        return !hayPie || (NavMesh.CalculatePath(p, pie, filtro, ruta) && ruta.status == NavMeshPathStatus.PathComplete);
    }

    /// <summary>Deja al zombie sobre tierra con camino hasta el jugador. Falso si no hay dónde ponerlo.</summary>
    bool Reubicar(EnemigoAcechador e)
    {
        bool hayPie = NavMesh.SamplePosition(jugador.position, out var pieHit, 10f, filtro);
        Vector3 pie = pieHit.position, origen = e.transform.position;
        if (NavMesh.SamplePosition(origen, out var hit, 3f, filtro) && Conectado(hit.position, hayPie, pie))
            return true;
        // Primero cerca de donde estaba, cada vez más lejos.
        float[] radios = { 6f, 12f, 25f, 45f };
        foreach (float r in radios)
            for (int intento = 0; intento < 10; intento++)
            {
                Vector2 c = Random.insideUnitCircle * r;
                if (!NavMesh.SamplePosition(origen + new Vector3(c.x, 0f, c.y), out hit, 6f, filtro)) continue;
                if (!Conectado(hit.position, hayPie, pie)) continue;
                e.Reubicar(hit.position);
                return true;
            }
        // Mar adentro: aparece como un refuerzo más, lejos y fuera de cámara.
        if (BuscarPunto(Random.Range(0f, 360f), 360f, out var punto))
        {
            e.Reubicar(punto);
            return true;
        }
        return false;
    }

    /// <summary>Un zombie que persigue sin avanzar varios segundos (trabado en el terreno) se reubica fuera de cámara.</summary>
    void RevisarAtascados()
    {
        if (Time.time < proximaRevision) return;
        proximaRevision = Time.time + 1f;
        foreach (var e in vivos)
        {
            if (e == null) continue;
            Vector3 p = e.transform.position;
            bool persigue = e.Persiguiendo && e.Alertado && !e.Atacando
                && GeografiaIsla.Plano(p - jugador.position).sqrMagnitude > 6f * 6f;
            if (!persigue || !rastro.TryGetValue(e, out var previo) || (p - previo.pos).sqrMagnitude > 1f)
            {
                rastro[e] = (p, Time.time);
                continue;
            }
            if (Time.time - previo.desde < 6f || EnVista(p)) continue;
            if (BuscarPunto(Random.Range(0f, 360f), 360f, out var punto)) e.Reubicar(punto);
            rastro[e] = (e.transform.position, Time.time);
        }
    }

    /// <summary>Zombies que ya persiguen al jugador y están a menos de 25 m.</summary>
    int Acosando()
    {
        int cuenta = 0;
        foreach (var e in vivos)
            if (e != null && e.Alertado && (e.transform.position - jugador.position).sqrMagnitude < 25f * 25f) cuenta++;
        return cuenta;
    }

    void Limpiar()
    {
        for (int i = vivos.Count - 1; i >= 0; i--)
        {
            var e = vivos[i];
            var vida = e != null ? e.GetComponent<ControladorVida>() : null;
            if (e == null || (vida != null && vida.Muerto))
            {
                rastro.Remove(e);
                generados.Remove(e);
                vivos.RemoveAt(i);
                continue;
            }
            // Los refuerzos que quedaron muy atrás se retiran para volver a aparecer cerca.
            if (generados.Contains(e) && (e.transform.position - jugador.position).sqrMagnitude > DistanciaReciclar * DistanciaReciclar
                && !EnVista(e.transform.position))
            {
                generados.Remove(e);
                rastro.Remove(e);
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
            if (!EnTierra(hit.position)) continue;
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
