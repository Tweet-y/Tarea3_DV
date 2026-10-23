using UnityEngine;

/// <summary>Detecta el contacto con la superficie del agua y usa la derrota existente.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class AguaLetal : MonoBehaviour
{
    private void Awake()
    {
        // El plano de Unity mide 10 x 10 unidades locales. El volumen empieza
        // en su superficie y se extiende hacia abajo para detectar las caidas.
        var zona = GetComponent<BoxCollider>();
        float profundidadLocal = 10f / Mathf.Max(Mathf.Abs(transform.lossyScale.y), .001f);
        zona.isTrigger = true;
        zona.center = new Vector3(0f, -profundidadLocal * .5f, 0f);
        zona.size = new Vector3(10f, profundidadLocal, 10f);
    }

    private void OnTriggerEnter(Collider other) => DetectarJugador(other);
    private void OnTriggerStay(Collider other) => DetectarJugador(other);

    private void DetectarJugador(Collider other)
    {
        var personaje = other.GetComponentInParent<StarterAssets.ThirdPersonController>();
        if (personaje != null && personaje.CompareTag("Player"))
            ControladorPartidaBotellas.Instancia?.Morir();
    }
}
