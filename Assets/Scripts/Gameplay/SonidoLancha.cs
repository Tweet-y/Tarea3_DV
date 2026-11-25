using UnityEngine;

/// <summary>
/// Motor y agua de la lancha. Mide cuánto se mueve el casco (sirve igual si la mueve un script,
/// una animación o la física): con más velocidad el motor sube de tono y el agua suena más.
/// Detenida un rato se apaga el motor; al volver a moverse arranca de nuevo.
/// </summary>
[DisallowMultipleComponent]
public sealed class SonidoLancha : MonoBehaviour
{
    const float VelocidadMaxima = 14f;
    const float SegundosParaApagar = 3f;

    AudioSource motor, agua;
    Vector3 posicionPrevia;
    float velocidad, quietaDesde, encendido;

    /// <summary>Agrega el sonido a la lancha una sola vez, aunque varios sistemas la usen.</summary>
    public static SonidoLancha Instalar(Transform lancha)
    {
        if (lancha == null) return null;
        var sonido = lancha.GetComponent<SonidoLancha>();
        return sonido != null ? sonido : lancha.gameObject.AddComponent<SonidoLancha>();
    }

    void Awake()
    {
        motor = Fuente("MotorLancha", SintesisAudio.Motor);
        agua = Fuente("AguaLancha", SintesisAudio.Agua);
        posicionPrevia = transform.position;
        // Arranca apagada: una lancha amarrada no suena hasta que se mueve.
        quietaDesde = float.NegativeInfinity;
    }

    AudioSource Fuente(string nombre, AudioClip clip)
    {
        var hijo = transform.Find(nombre);
        if (hijo == null)
        {
            hijo = new GameObject(nombre).transform;
            hijo.SetParent(transform, false);
        }
        var fuente = hijo.GetComponent<AudioSource>();
        if (fuente == null) fuente = hijo.gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.loop = true;
        fuente.playOnAwake = false;
        // Casi siempre se va arriba de la lancha: mayormente 3D, pero sin perderse al girar la cámara.
        fuente.spatialBlend = .8f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 6f;
        fuente.maxDistance = 70f;
        fuente.dopplerLevel = 0f;
        fuente.volume = 0f;
        Sonido.Asignar(fuente, "Efectos");
        return fuente;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 ahora = transform.position;
        Vector3 delta = ahora - posicionPrevia;
        posicionPrevia = ahora;
        delta.y = 0f;
        // Un salto de posición (UbicarEnMuelle, reinicio) no es velocidad real.
        float medida = delta.magnitude > 8f ? 0f : delta.magnitude / dt;
        velocidad = Mathf.Lerp(velocidad, medida, 1f - Mathf.Exp(-dt * 4f));

        if (velocidad > .4f) quietaDesde = Time.time;
        bool enMarcha = Time.time - quietaDesde < SegundosParaApagar;
        encendido = Mathf.MoveTowards(encendido, enMarcha ? 1f : 0f, dt * (enMarcha ? 2f : .6f));

        float k = Mathf.Clamp01(velocidad / VelocidadMaxima);
        Ajustar(motor, encendido * (.25f + .45f * k), .6f + .8f * k);
        Ajustar(agua, Mathf.Clamp01(velocidad / 3f) * (.15f + .45f * k), .85f + .3f * k);
    }

    static void Ajustar(AudioSource fuente, float volumen, float tono)
    {
        if (fuente == null || fuente.clip == null) return;
        fuente.volume = volumen;
        fuente.pitch = tono;
        if (volumen > .005f && !fuente.isPlaying) fuente.Play();
        else if (volumen <= .005f && fuente.isPlaying) fuente.Stop();
    }
}
