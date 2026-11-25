using UnityEngine;

/// <summary>
/// Trigger de la lancha de extracción (SubirAlBote, en muelle_final). Al llegar con todas las botellas
/// inicia la pelea final. Antes vivía dentro de FinalIsla.cs y Unity no podía guardarlo en la escena
/// (cada MonoBehaviour necesita su propio archivo con el mismo nombre), así que nunca se ejecutaba.
/// </summary>
public class DisparadorBote : MonoBehaviour
{
    public FinalIsla final;

    Collider zona;
    Transform jugador;

    void Awake() => zona = GetComponent<Collider>();

    void OnTriggerEnter(Collider other) => Tocar(other);

    // Si el jugador ya estaba dentro cuando se encendieron los fuegos (tomó la última botella en el
    // muelle) no hay un nuevo Enter: Stay lo detecta igual.
    void OnTriggerStay(Collider other) => Tocar(other);

    void Tocar(Collider other)
    {
        if (other == null) return;
        var capsula = other.GetComponentInParent<CharacterController>();
        bool esJugador = other.CompareTag("Player") || (capsula != null && capsula.CompareTag("Player"))
            || other.transform.root.CompareTag("Player");
        if (esJugador) Entrar();
    }

    void Update()
    {
        // Respaldo sin física: el jugador dentro de la zona del muelle.
        if (zona == null || !FinalIsla.FuegosEncendidos) return;
        if (jugador == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            jugador = go.transform;
        }
        if (zona.bounds.Contains(jugador.position + Vector3.up * .5f)) Entrar();
    }

    void Entrar()
    {
        if (final == null) final = FinalIsla.Instancia;
        if (final != null) final.EntrarAlBote();
    }
}
