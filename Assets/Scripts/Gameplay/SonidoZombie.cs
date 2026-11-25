using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voz y pisadas de un zombie. Deambulando gime de vez en cuando; persiguiendo gruñe más seguido
/// y ruge al atacar. Las pisadas siguen la distancia recorrida, así que se aceleran al correr.
/// DirectorZombies lo agrega solo a cada zombie.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemigoAcechador))]
public sealed class SonidoZombie : MonoBehaviour
{
    // Con 30 zombies sonando a la vez sería ruido: solo los más cercanos hablan.
    const int MaximoVoces = 4;
    const float DistanciaAudible = 32f;

    static readonly List<SonidoZombie> activos = new List<SonidoZombie>();

    EnemigoAcechador acecho;
    ControladorVida vida;
    AudioSource voz, pies;
    Vector3 posicionPrevia;
    float recorrido, proximaVoz, zancada, tono;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => activos.Clear();

    void Awake()
    {
        acecho = GetComponent<EnemigoAcechador>();
        vida = GetComponent<ControladorVida>();
        voz = Fuente("VozZombie", new Vector3(0f, 1.6f, 0f), 1.5f, DistanciaAudible);
        pies = Fuente("PiesZombie", Vector3.zero, 1f, 16f);
        // Cada zombie tiene su propio registro de voz y largo de paso.
        tono = Random.Range(.85f, 1.15f);
        zancada = Random.Range(.7f, .85f);
        proximaVoz = Time.time + Random.Range(1f, 6f);
    }

    /// <summary>Fuente en un hijo con nombre: las copias que hace DirectorZombies la traen y se reutiliza.</summary>
    AudioSource Fuente(string nombre, Vector3 posicion, float minimo, float maximo)
    {
        var hijo = transform.Find(nombre);
        if (hijo == null)
        {
            hijo = new GameObject(nombre).transform;
            hijo.SetParent(transform, false);
            hijo.localPosition = posicion;
        }
        var fuente = hijo.GetComponent<AudioSource>();
        if (fuente == null) fuente = hijo.gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = minimo;
        fuente.maxDistance = maximo;
        fuente.dopplerLevel = 0f;
        Sonido.Asignar(fuente, "Efectos");
        return fuente;
    }

    void OnEnable()
    {
        if (!activos.Contains(this)) activos.Add(this);
        posicionPrevia = transform.position;
        if (vida != null)
        {
            vida.alMorir.RemoveListener(AlMorir);
            vida.alMorir.AddListener(AlMorir);
        }
    }

    void OnDisable()
    {
        activos.Remove(this);
        if (vida != null) vida.alMorir.RemoveListener(AlMorir);
    }

    void Update()
    {
        if (Time.deltaTime <= 0f || (vida != null && vida.Muerto)) return;
        Pisadas();
        if (Time.time >= proximaVoz && acecho.Persiguiendo)
        {
            bool alerta = acecho.Alertado;
            proximaVoz = Time.time + (alerta ? Random.Range(2.2f, 4.5f) : Random.Range(5f, 11f));
            if (alerta && Random.value < .55f) Hablar(SintesisAudio.Rugidos, .75f, 1f);
            else Hablar(SintesisAudio.Gemidos, alerta ? .8f : .6f, 1f);
        }
    }

    void Pisadas()
    {
        Vector3 ahora = transform.position;
        Vector3 paso = ahora - posicionPrevia;
        posicionPrevia = ahora;
        paso.y = 0f;
        // Un salto grande es un Warp o una reubicación, no pasos.
        if (paso.sqrMagnitude > 4f) return;
        recorrido += paso.magnitude;
        if (recorrido < zancada) return;
        recorrido = 0f;
        if (!Cerca(18f)) return;
        var clips = SintesisAudio.Pasos;
        if (clips.Length == 0) return;
        float velocidad = paso.magnitude / Time.deltaTime;
        pies.pitch = Random.Range(.72f, .88f);
        pies.PlayOneShot(clips[Random.Range(0, clips.Length)], Mathf.Lerp(.25f, .5f, velocidad / 5f));
    }

    /// <summary>Lo llama EnemigoAcechador al lanzar un golpe.</summary>
    public void Ataque()
    {
        if (vida != null && vida.Muerto) return;
        proximaVoz = Mathf.Max(proximaVoz, Time.time + 1.5f);
        Hablar(SintesisAudio.Rugidos, .95f, 1.12f, forzar: true);
    }

    void AlMorir()
    {
        // Último estertor, más grave y largo.
        Hablar(SintesisAudio.Gemidos, .9f, .75f, forzar: true);
    }

    void Hablar(AudioClip[] clips, float volumen, float multiplicadorTono, bool forzar = false)
    {
        if (clips == null || clips.Length == 0 || voz == null) return;
        if (!Cerca(DistanciaAudible)) return;
        if (!forzar && VocesSonando() >= MaximoVoces) return;
        voz.Stop();
        voz.clip = clips[Random.Range(0, clips.Length)];
        voz.pitch = tono * multiplicadorTono * Random.Range(.94f, 1.06f);
        voz.volume = volumen;
        voz.Play();
    }

    bool Cerca(float distancia)
    {
        var oyente = acecho.objetivo != null ? acecho.objetivo : Camera.main != null ? Camera.main.transform : null;
        return oyente == null || (oyente.position - transform.position).sqrMagnitude <= distancia * distancia;
    }

    static int VocesSonando()
    {
        int cuenta = 0;
        foreach (var s in activos)
            if (s != null && s.voz != null && s.voz.isPlaying) cuenta++;
        return cuenta;
    }
}
