using UnityEngine;
using UnityEngine.Events;
using System;

[RequireComponent(typeof(Collider))]
public class ObjetoEspecialColeccionable : MonoBehaviour
{
    public enum TipoBotella { Objetivo, Curativa }
    public TipoBotella tipo;
    [Range(0f, .2f)] public float curacion = .08f;
    public static event Action<ObjetoEspecialColeccionable, GameObject> AlRecogerColeccionable;
    [Header("--- Puntuación y Eventos ---")]
    [Tooltip("Puntos que otorga este coleccionable")]
    public int puntos = 100;
    [Tooltip("Tag esperado del jugador (opcional, si está vacío detecta CharacterController/ThirdPersonController)")]
    public string tagJugador = "Player";
    [Tooltip("Evento que se dispara al ser recogido")]
    public UnityEvent alRecoger;

    [Header("--- Movimiento (Giro y Flotación) ---")]
    [Tooltip("Velocidad de rotación en grados por segundo")]
    public float velocidadGiro = 90f;
    [Tooltip("Eje sobre el cual rotará el objeto")]
    public Vector3 ejeRotacion = Vector3.up;
    [Tooltip("Habilitar movimiento de flotación vertical suave")]
    public bool flotarEnElAire = true;
    [Tooltip("Amplitud de la flotación vertical")]
    public float amplitudFlotacion = 0.25f;
    [Tooltip("Frecuencia de la flotación vertical")]
    public float frecuenciaFlotacion = 2f;

    [Header("--- Efecto de Brillo (Luz) ---")]
    [Tooltip("Luz puntual para el efecto de brillo (se crea automáticamente si no se asigna)")]
    public Light luzBrillo;
    [Tooltip("Color de la luz")]
    public Color colorBrillo = new Color(0.2f, 0.8f, 1f, 1f); // Azul brillante / cian neón
    [Tooltip("Intensidad base de la luz")]
    public float intensidadBase = 2f;
    [Tooltip("Amplitud del pulso de luz")]
    public float amplitudPulso = 0.8f;
    [Tooltip("Velocidad del pulso de luz")]
    public float velocidadPulso = 3f;

    [Header("--- Efectos al Recoger ---")]
    [Tooltip("Efecto de sonido al recoger")]
    public AudioClip clipSonido;
    [Range(0f, 1f)]
    public float volumenSonido = 1f;
    [Tooltip("Prefab de partículas a instanciar al ser recogido (opcional)")]
    public GameObject efectoParticulas;

    private Vector3 _posicionInicial;
    private bool _yaRecogido = false;

    public bool YaRecogido => _yaRecogido;

    void Awake()
    {
        // Tamaño absoluto desde el mesh: los clones curativos no vuelven a ampliarlo.
        var meshCollider = GetComponent<MeshCollider>();
        var mesh = meshCollider != null ? meshCollider.sharedMesh : GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh != null)
        {
            var caja = GetComponent<BoxCollider>();
            if (caja == null) caja = gameObject.AddComponent<BoxCollider>();
            caja.center = mesh.bounds.center;
            caja.size = mesh.bounds.size * 1.3f;
            caja.isTrigger = true;
            caja.enabled = true;
            if (meshCollider != null) meshCollider.enabled = false;
        }
        else
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        // Configurar la luz de brillo si no fue asignada en el inspector
        if (luzBrillo == null)
        {
            luzBrillo = GetComponentInChildren<Light>();
            if (luzBrillo == null)
            {
                GameObject luzObj = new GameObject("LuzBrillo");
                luzObj.transform.SetParent(transform, false);
                luzObj.transform.localPosition = Vector3.up * 0.5f;
                luzBrillo = luzObj.AddComponent<Light>();
                luzBrillo.type = LightType.Point;
                luzBrillo.range = 5f;
                luzBrillo.color = colorBrillo;
                luzBrillo.intensity = intensidadBase;
            }
        }
    }

    void Start()
    {
        _posicionInicial = transform.position;

        if (luzBrillo != null)
        {
            luzBrillo.color = colorBrillo;
        }
    }

    void Update()
    {
        // 1. Rotación continua
        transform.Rotate(ejeRotacion, velocidadGiro * Time.deltaTime, Space.Self);

        // 2. Flotación senoidal en el aire
        if (flotarEnElAire)
        {
            float nuevoY = _posicionInicial.y + Mathf.Sin(Time.time * frecuenciaFlotacion) * amplitudFlotacion;
            transform.position = new Vector3(_posicionInicial.x, nuevoY, _posicionInicial.z);
        }

        // 3. Brillo pulsante
        if (luzBrillo != null)
        {
            luzBrillo.intensity = intensidadBase + Mathf.Sin(Time.time * velocidadPulso) * amplitudPulso;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        IntentarRecoger(other);
    }

    // Si Unity recarga los scripts con un contacto existente, puede reanudarlo
    // como Stay en vez de emitir otro Enter. La recogida sigue siendo única.
    void OnTriggerStay(Collider other)
    {
        IntentarRecoger(other);
    }

    private void IntentarRecoger(Collider other)
    {
        if (_yaRecogido) return;

        // Verificar si es el jugador (por tag o por componentes de control habituales)
        bool esJugador = false;

        if (!string.IsNullOrEmpty(tagJugador) && other.CompareTag(tagJugador))
        {
            esJugador = true;
        }
        else if (other.GetComponent<CharacterController>() != null || other.GetComponentInParent<CharacterController>() != null)
        {
            esJugador = true;
        }
        else if (other.name.ToLower().Contains("player") || other.name.ToLower().Contains("personaje") || other.name.ToLower().Contains("hotdog"))
        {
            esJugador = true;
        }

        if (esJugador)
        {
            Recoger(other.gameObject);
        }
    }

    public void Recoger(GameObject jugador)
    {
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida != null && (partida.EstaPausada || partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso)) return;
        if (_yaRecogido || jugador == null) return;
        _yaRecogido = true;
        Debug.Log($"<color=yellow>¡Objeto Especial Recogido!</color> {gameObject.name} por {jugador.name} (+{puntos} puntos)");

        // 1. Reproducir sonido en la posición del objeto
        if (clipSonido != null)
        {
            AudioSource.PlayClipAtPoint(clipSonido, transform.position, volumenSonido);
        }

        // 2. Instanciar partículas de recogida si existen
        if (efectoParticulas != null)
        {
            Instantiate(efectoParticulas, transform.position, Quaternion.identity);
        }

        // 3. Disparar eventos configurados
        alRecoger?.Invoke();
        AlRecogerColeccionable?.Invoke(this, jugador);

        // 4. Destruir el objeto
        Destroy(gameObject);
    }

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }
}
