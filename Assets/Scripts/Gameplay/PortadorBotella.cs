using UnityEngine;

/// <summary>La botella del jefe no se puede tomar hasta que muere.</summary>
public class PortadorBotella : MonoBehaviour
{
    public ObjetoEspecialColeccionable botella;

    void Start()
    {
        var vida = GetComponent<ControladorVida>();
        if (vida != null) vida.alMorir.AddListener(Soltar);
        if (botella == null) return;
        var col = botella.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    public void Soltar()
    {
        if (botella == null) return;
        botella.transform.SetParent(null, true);
        botella.gameObject.SetActive(true);
        botella.transform.position = transform.position + Vector3.up * 1.2f;
        var col = botella.GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }
}
