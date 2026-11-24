using UnityEngine;

/// <summary>Trigger de la lancha de extracción.</summary>
public class DisparadorBote : MonoBehaviour
{
    public FinalIsla final;

    void OnTriggerEnter(Collider other)
    {
        if (!other.transform.root.CompareTag("Player")) return;
        if (final == null) final = FinalIsla.Instancia;
        if (final != null) final.EntrarAlBote();
    }
}
