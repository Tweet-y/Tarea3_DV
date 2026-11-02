using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instala el equipo de combate al cargar el nivel, tambien al reintentar desde el menu.</summary>
public static class SistemaArmasFuego
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        RecogibleArmaFuego.Activos.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Registrar() => SceneManager.sceneLoaded += AlCargar;

    private static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        if (escena.name != "MapaV1") return;
        Instalar();
    }

    public static void Instalar()
    {
        var jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador == null || jugador.GetComponent<ControladorArmaFuego>() != null) return;
        var datos = Resources.Load<ArmaFuegoDefinicion>("Armas/RifleAsalto03");
        if (datos == null || datos.modelo == null)
        {
            Debug.LogError("Falta Resources/Armas/RifleAsalto03 o su modelo Synty.");
            return;
        }
        var controlador = jugador.AddComponent<ControladorArmaFuego>();
        var hud = new GameObject("HUD_ArmaFuego").AddComponent<HUDArmaFuego>();
        hud.Inicializar(controlador);
        Vector3 origen = jugador.transform.position;
        // El arma queda a distancia de interaccion del punto inicial.
        RecogibleArmaFuego.Crear(datos, BuscarSuelo(origen + jugador.transform.forward * 1.8f, jugador.transform));
        RecogibleArmaFuego.Crear(datos, BuscarSuelo(origen + jugador.transform.right * 2.2f, jugador.transform), soloMunicion: true);
        // Reabastecimiento en sectores ya existentes del recorrido, sin cambiar el objetivo.
        var botellas = Object.FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < botellas.Length; i += Mathf.Max(1, botellas.Length / 4))
        {
            Vector3 punto = botellas[i].transform.position + Vector3.right;
            RecogibleArmaFuego.Crear(datos, BuscarSuelo(punto, jugador.transform), soloMunicion: true);
        }
        foreach (var enemigo in Object.FindObjectsByType<EnemigoAcechador>(FindObjectsSortMode.None))
        {
            var vida = enemigo.GetComponent<VidaEnemigoFuego>();
            if (vida == null) vida = enemigo.gameObject.AddComponent<VidaEnemigoFuego>();
            vida.vidaMaxima = enemigo.transform.lossyScale.y > 5f ? 1200f : 175f;
        }
    }

    public static Vector3 BuscarSuelo(Vector3 cerca, Transform jugador)
    {
        // Partir apenas sobre el punto evita colocar el arma en un techo situado muchos metros mas arriba.
        Vector3 origen = cerca + Vector3.up * 1f;
        float menor = float.PositiveInfinity;
        Vector3 resultado = cerca;
        foreach (var hit in Physics.RaycastAll(origen, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(jugador) || hit.normal.y < .6f || hit.distance >= menor) continue;
            menor = hit.distance;
            resultado = hit.point + Vector3.up * .04f;
        }
        return resultado;
    }
}
