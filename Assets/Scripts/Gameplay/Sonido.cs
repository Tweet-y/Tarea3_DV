using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>Reproduce clips por el Audio Mixer. PlayClipAtPoint no puede elegir grupo.</summary>
public static class Sonido
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Enganchar()
    {
        StarterAssets.PuenteSonido.Reproducir = (clip, posicion, volumen) => Reproducir(clip, posicion, volumen, "Efectos");
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
    }

    static void AlCargar(Scene escena, LoadSceneMode modo) => AplicarEscena();

    public static void AplicarEscena()
    {
        foreach (var fuente in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (fuente.outputAudioMixerGroup == null)
                Asignar(fuente, fuente.loop && fuente.spatialBlend < .5f ? "Musica" : "Efectos");
    }

    public static void Asignar(AudioSource fuente, string grupo)
    {
        if (fuente == null) return;
        var destino = MezcladorAudio.Grupo(grupo);
        if (destino != null) fuente.outputAudioMixerGroup = destino;
    }

    public static void Reproducir(AudioClip clip, Vector3 posicion, float volumen, string grupo, float tono = 1f)
    {
        if (clip == null) return;
        var objeto = new GameObject("Sonido");
        objeto.transform.position = posicion;
        var fuente = objeto.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.volume = volumen;
        fuente.pitch = tono;
        fuente.spatialBlend = 1f;
        fuente.playOnAwake = false;
        Asignar(fuente, grupo);
        fuente.Play();
        Object.Destroy(objeto, clip.length / Mathf.Max(.1f, tono) + .15f);
    }
}

public static class MezcladorAudio
{
    static AudioMixer mixer;

    public static AudioMixerGroup Grupo(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;
        if (mixer == null) mixer = Resources.Load<AudioMixer>("Audio/MezcladorPrincipal");
        if (mixer == null) return null;
        foreach (var grupo in mixer.FindMatchingGroups(nombre))
            if (grupo.name == nombre) return grupo;
        return null;
    }
}
