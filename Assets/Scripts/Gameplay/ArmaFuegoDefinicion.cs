using UnityEngine;

[CreateAssetMenu(menuName = "Gameplay/Arma de fuego")]
public sealed class ArmaFuegoDefinicion : ScriptableObject
{
    /// <summary>Ranura que ocupa en el inventario: se puede llevar una de cada tipo.</summary>
    public enum Tipo { Rifle, Escopeta }

    public string nombre = "RIFLE DE ASALTO";
    public Tipo tipo = Tipo.Rifle;
    public GameObject modelo;
    public AudioClip disparo;
    public Material materialTraza;
    [Min(1)] public int capacidad = 30;
    [Min(0)] public int reservaInicial = 120;
    [Min(0)] public int reservaMaxima = 240;
    [Tooltip("Con recargaPorCartucho es el tiempo de cada cartucho, no el de la recarga completa.")]
    [Min(.1f)] public float segundosRecarga = 2.2f;
    [Tooltip("Escopeta: mete los cartuchos de a uno y se puede disparar a mitad de la recarga.")]
    public bool recargaPorCartucho;
    [Min(1f)] public float disparosPorSegundo = 8f;
    [Min(1f)] public float alcance = 150f;
    [Tooltip("Daño de cada proyectil; la escopeta suma el de todos los perdigones que acierten.")]
    [Min(1f)] public float danio = 25f;
    [Min(1)] public int perdigones = 1;
    [Tooltip("Desde esta distancia el daño cae hasta un 35% en el alcance máximo. 0 = sin caída.")]
    [Min(0f)] public float alcanceEfectivo;
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
    [Tooltip("Ventana de expulsión, en el espacio del modelo.")]
    public Vector3 expulsion = new Vector3(.03f, .095f, .17f);
    [Tooltip("Por donde entra cada cartucho al recargar de a uno. Cero = la mano no se mueve.")]
    public Vector3 puertoCarga;

    [Header("Sensación del disparo")]
    [Tooltip("Grados que sube la cámara en cada tiro.")]
    [Min(0f)] public float retrocesoCamara = .55f;
    [Tooltip("Golpe del arma contra el hombro en cada tiro.")]
    [Min(0f)] public float retrocesoArma = .55f;
    [Min(.2f)] public float escalaFogonazo = 1f;
    [Tooltip("Metros a la redonda en que los zombies oyen el disparo.")]
    [Min(1f)] public float ruido = 40f;

    [Header("Casquillos y munición")]
    [Tooltip("Modelo del casquillo expulsado. Vacío = casquillo de latón.")]
    public GameObject casquillo;
    public Vector3 escalaCasquillo = Vector3.one;
    [Tooltip("Escopeta: el cartucho vacío sale al bombear, no al disparar.")]
    [Min(0f)] public float retrasoCasquillo;
    [Tooltip("Nombre (parcial) de la pieza que se bombea tras cada tiro. Vacío = no hay corredera.")]
    public string corredera = "";
    [Tooltip("Caja que se ve en el suelo al soltar munición. Vacío = el cargador del arma.")]
    public GameObject modeloMunicion;
    [Min(1)] public int balasPorCaja = 60;
}
