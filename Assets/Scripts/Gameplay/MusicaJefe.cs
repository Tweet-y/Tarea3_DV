using System.Collections;
using UnityEngine;

/// <summary>
/// Música de la pelea contra Nemesis ("Mechanized Trinity", Nick Tzios, sin copyright).
/// Al empezar baja y pausa la música de la isla (MusicManager) y entra la del jefe;
/// al terminar la pelea hace lo inverso. Si se destruye a medias, deja la música de la isla como estaba.
/// </summary>
public sealed class MusicaJefe : MonoBehaviour
{
    const string Ruta = "MusicaJefe/MechanizedTrinity";
    // Por debajo de la voz de CJ, que habla durante toda la pelea. El oído es logarítmico:
    // .2 suena a ~40% de lo que sonaba a .85 (unos -13 dB), no a una cuarta parte.
    const float Volumen = .2f;

    AudioSource fuente;
    Behaviour ambiente;
    AudioSource fuenteAmbiente;
    float volumenAmbiente = 1f;
    bool ambienteDevuelto = true;
    Coroutine fundido;

    public static MusicaJefe Iniciar(GameObject anfitrion)
    {
        var musica = anfitrion.GetComponent<MusicaJefe>();
        if (musica == null) musica = anfitrion.AddComponent<MusicaJefe>();
        musica.Empezar();
        return musica;
    }

    void Empezar()
    {
        var clip = Resources.Load<AudioClip>(Ruta);
        if (clip == null)
        {
            Debug.LogWarning("MusicaJefe: falta Resources/" + Ruta + ".");
            return;
        }
        if (fuente == null)
        {
            fuente = gameObject.AddComponent<AudioSource>();
            // Prioridad máxima: la metralleta lanza muchas voces por segundo y no debe apagar la música.
            fuente.priority = 0;
            fuente.playOnAwake = false;
            fuente.loop = true;
            fuente.spatialBlend = 0f;
            Sonido.Asignar(fuente, "Musica");
        }
        fuente.clip = clip;
        fuente.volume = 0f;
        fuente.Play();

        // Música de la isla (Don Cheto) o, en mapas viejos, el MusicManager de las cumbias.
        var isla = FindFirstObjectByType<MusicaIsla>();
        if (isla != null && isla.Fuente != null)
        {
            ambiente = isla;
            fuenteAmbiente = isla.Fuente;
        }
        else
        {
            var cumbias = FindFirstObjectByType<MusicManager>();
            ambiente = cumbias;
            fuenteAmbiente = cumbias != null ? cumbias.GetComponent<AudioSource>() : null;
        }
        if (fuenteAmbiente != null)
        {
            volumenAmbiente = fuenteAmbiente.volume;
            // Apagado, MusicManager deja de poner otra cumbia cuando la actual se pausa.
            ambiente.enabled = false;
            ambienteDevuelto = false;
        }
        Fundir(entrar: true, segundos: 2f);
    }

    /// <summary>Baja la música del jefe y devuelve la de la isla.</summary>
    public void Terminar(float segundos = 3f) => Fundir(entrar: false, segundos: segundos);

    void Fundir(bool entrar, float segundos)
    {
        if (fundido != null) StopCoroutine(fundido);
        fundido = StartCoroutine(Fundido(entrar, segundos));
    }

    IEnumerator Fundido(bool entrar, float segundos)
    {
        float inicioJefe = fuente != null ? fuente.volume : 0f;
        float inicioAmbiente = fuenteAmbiente != null ? fuenteAmbiente.volume : 0f;
        if (!entrar && fuenteAmbiente != null && !ambienteDevuelto)
        {
            fuenteAmbiente.volume = 0f;
            inicioAmbiente = 0f;
            fuenteAmbiente.UnPause();
        }
        // Tiempo sin escalar: el fundido sigue aunque la pelea ralentice el juego.
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            float k = t / segundos;
            if (fuente != null) fuente.volume = Mathf.Lerp(inicioJefe, entrar ? Volumen : 0f, k);
            if (fuenteAmbiente != null) fuenteAmbiente.volume = Mathf.Lerp(inicioAmbiente, entrar ? 0f : volumenAmbiente, k);
            yield return null;
        }
        if (entrar)
        {
            if (fuente != null) fuente.volume = Volumen;
            if (fuenteAmbiente != null) fuenteAmbiente.Pause();
        }
        else
        {
            if (fuente != null) fuente.Stop();
            DevolverAmbiente();
        }
        fundido = null;
    }

    void DevolverAmbiente()
    {
        if (ambienteDevuelto) return;
        ambienteDevuelto = true;
        if (fuenteAmbiente != null)
        {
            fuenteAmbiente.volume = volumenAmbiente;
            fuenteAmbiente.UnPause();
        }
        if (ambiente != null) ambiente.enabled = true;
    }

    void OnDestroy() => DevolverAmbiente();
}
