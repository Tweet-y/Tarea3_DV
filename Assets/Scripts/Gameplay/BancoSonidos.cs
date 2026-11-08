using UnityEngine;

/// <summary>Clips compartidos. Se asignan en la escena.</summary>
public class BancoSonidos : MonoBehaviour
{
    public static BancoSonidos Actual { get; private set; }
    public AudioClip swoosh;
    public AudioClip impacto;
    public AudioClip explosion;
    public AudioClip buff;
    public AudioClip curar;
    public AudioClip beber;
    public AudioClip golpeJugador;
    public AudioClip muerteJugador;
    public AudioClip muerteEnemigo;
    public AudioClip grito;

    void Awake()
    {
        Actual = this;
    }

    void OnDestroy()
    {
        if (Actual == this) Actual = null;
    }
}
