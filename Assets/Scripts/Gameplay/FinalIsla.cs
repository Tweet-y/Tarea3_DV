using System.Collections;
using UnityEngine;

/// <summary>Al juntar las 30 botellas enciende los fuegos y deja la lancha de CJ.</summary>
public class FinalIsla : MonoBehaviour
{
    public static FinalIsla Instancia { get; private set; }
    public static string Mensaje { get; private set; }
    public static bool FuegosEncendidos { get; private set; }

    public Transform puntoExtraccion;
    public Transform lancha;
    public Transform cj;
    public GameObject fuegos;
    public string linea = "Sube, nos vamos. La isla ya dio todo lo que tenía.";
    bool salida;

    void Awake() => Instancia = this;

    void OnEnable()
    {
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida != null)
        {
            partida.TodasRecogidas -= Encender;
            partida.TodasRecogidas += Encender;
        }
    }

    void Start()
    {
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida != null)
        {
            partida.TodasRecogidas -= Encender;
            partida.TodasRecogidas += Encender;
        }
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida != null) partida.TodasRecogidas -= Encender;
    }

    public void Encender()
    {
        if (FuegosEncendidos) return;
        FuegosEncendidos = true;
        Mensaje = "Ve al bote";
        if (puntoExtraccion == null) return;
        if (lancha != null)
        {
            lancha.gameObject.SetActive(true);
            lancha.position = puntoExtraccion.position;
            lancha.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(puntoExtraccion.forward, Vector3.up).sqrMagnitude > .01f ? puntoExtraccion.forward : Vector3.forward, Vector3.up);
        }
        if (cj != null && lancha != null)
        {
            cj.gameObject.SetActive(true);
            cj.SetParent(lancha, true);
        }
        if (fuegos != null)
        {
            fuegos.SetActive(true);
            fuegos.transform.position = puntoExtraccion.position + Vector3.up * 2f;
            foreach (var sistema in fuegos.GetComponentsInChildren<ParticleSystem>(true))
            {
                var principal = sistema.main;
                principal.loop = true;
                sistema.Play();
            }
        }
        Sonido.Reproducir(BancoSonidos.Actual != null ? BancoSonidos.Actual.explosion : null, puntoExtraccion.position, .6f, "Efectos");
    }

    public void EntrarAlBote()
    {
        if (!FuegosEncendidos || salida) return;
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida == null || partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso) return;
        salida = true;
        StartCoroutine(Salida());
    }

    IEnumerator Salida()
    {
        IntroduccionIsla.Extra = true;
        var jugador = GameObject.FindGameObjectWithTag("Player");
        SuspensionCinematica.Tomar(jugador);
        var dialogo = ControladorDialogo.Crear(false);
        dialogo.FijarNombre("CJ");
        yield return dialogo.StartCoroutine(dialogo.Revelar(linea, 3f));
        dialogo.Ocultar();
        if (!ControladorDialogo.Saltar && lancha != null)
        {
            Vector3 rumbo = lancha.forward;
            for (float t = 0f; t < 3.5f; t += Time.deltaTime)
            {
                lancha.position += rumbo * (8f * Time.deltaTime);
                yield return null;
            }
        }
        dialogo.Cerrar();
        SuspensionCinematica.Devolver();
        IntroduccionIsla.Extra = false;
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida != null) partida.FinalizarVictoria();
    }
}

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
