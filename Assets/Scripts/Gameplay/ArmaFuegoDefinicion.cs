using UnityEngine;

[CreateAssetMenu(menuName = "Gameplay/Arma de fuego")]
public sealed class ArmaFuegoDefinicion : ScriptableObject
{
    public string nombre = "RIFLE DE ASALTO";
    public GameObject modelo;
    public AudioClip disparo;
    public Material materialTraza;
    [Min(1)] public int capacidad = 30;
    [Min(0)] public int reservaInicial = 120;
    [Min(0)] public int reservaMaxima = 240;
    [Min(.1f)] public float segundosRecarga = 2.2f;
    [Min(1f)] public float disparosPorSegundo = 8f;
    [Min(1f)] public float alcance = 150f;
    [Min(1f)] public float danio = 25f;
    [Min(1f)] public float multiplicadorCabeza = 2f;
    [Min(0f)] public float dispersionCadera = 1.2f;
    [Min(0f)] public float dispersionApuntado = .12f;
    [Range(.5f, 1.5f)] public float escalaEquipada = .8f;
    public Vector3 posicionEnCuerpo = new Vector3(.14f, 1.38f, .25f);
    public Vector3 apoyoCulata = new Vector3(0f, .07f, -.296f);
    [Min(0f)] public float separacionHombro = .06f;
    public Vector3 bocaCanon = new Vector3(0f, .072f, .70f);
    public Vector3 agarreDerecho = new Vector3(.015f, -.035f, .055f);
    public Vector3 agarreIzquierdo = new Vector3(-.015f, -.025f, .23f);
}
