using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Música de fondo mientras se recorre la isla ("El Tatuado", Don Cheto), en bucle y bajita para no
/// tapar zombies, diálogos ni efectos. Se crea sola al cargar el nivel. MusicaJefe la pausa durante
/// la pelea final y la devuelve al terminar.
/// </summary>
public sealed class MusicaIsla : MonoBehaviour
{
    const string Ruta = "MusicaIsla/DonCheto_ElTatuado";
    public const float Volumen = .12f;

    public AudioSource Fuente { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
    }

    static void AlCargar(Scene escena, LoadSceneMode modo)
    {
        if (escena.name != ControladorMenus.EscenaJuego) return;
        if (FindFirstObjectByType<MusicaIsla>() != null) return;
        var clip = Resources.Load<AudioClip>(Ruta);
        if (clip == null)
        {
            Debug.LogWarning("MusicaIsla: falta Resources/" + Ruta + ".");
            return;
        }
        // Objeto de la escena (no persistente): al volver al menú o reintentar se crea de nuevo.
        var musica = new GameObject("MusicaIsla").AddComponent<MusicaIsla>();
        SceneManager.MoveGameObjectToScene(musica.gameObject, escena);
        musica.Tocar(clip);
    }

    const float SegundosEntrada = 4f;
    bool empezo;

    void Tocar(AudioClip clip)
    {
        Fuente = gameObject.AddComponent<AudioSource>();
        // Prioridad máxima: con muchos sonidos a la vez Unity apaga primero los de prioridad baja, nunca la música.
        Fuente.priority = 0;
        Fuente.clip = clip;
        Fuente.loop = true;
        Fuente.playOnAwake = false;
        Fuente.spatialBlend = 0f;
        Fuente.volume = 0f;
        Sonido.Asignar(Fuente, "Musica");
        // No suena todavía: arranca cuando el jugador toma el rifle, ya terminada la intro con CJ.
    }

    void Update()
    {
        // MusicaJefe desactiva este componente durante la pelea, así que ahí no arranca ni sube.
        if (Fuente == null) return;
        if (!empezo)
        {
            var inventario = InventarioJugador.Instancia;
            if (IntroduccionIsla.Activa || inventario == null || !inventario.Tiene(InventarioJugador.Arma.Rifle)) return;
            empezo = true;
            Fuente.volume = 0f;
            Fuente.Play();
        }
        // Entrada suave hasta el volumen de fondo.
        if (Fuente.isPlaying && Fuente.volume < Volumen)
            Fuente.volume = Mathf.MoveTowards(Fuente.volume, Volumen, Volumen / SegundosEntrada * Time.unscaledDeltaTime);
    }
}
