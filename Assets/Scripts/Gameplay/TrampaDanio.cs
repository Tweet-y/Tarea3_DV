using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Componente para trampas u obstáculos en el suelo (vidrios rotos, charcos electrificados, pinchos, etc.)
/// que dañan al jugador al entrar en contacto.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrampaDanio : MonoBehaviour
{
    [Header("--- Configuración de Daño ---")]
    [Tooltip("Tag del objeto jugador.")]
    public string tagJugador = "Player";

    [Tooltip("Cantidad de daño al entrar en contacto (0.1 = 10% de vida/ebriedad).")]
    [Range(0.01f, 1f)]
    public float cantidadDanio = 0.15f;

    [Tooltip("¿Aplica daño por segundo mientras el jugador permanezca dentro?")]
    public bool esDanioContinuo = false;

    [Tooltip("Daño por segundo si 'esDanioContinuo' está activado.")]
    public float danioPorSegundo = 0.1f;

    [Tooltip("Tiempo mínimo entre impactos sucesivos para evitar daño involuntario repetido.")]
    public float tiempoEnfriamiento = 1.0f;

    [Tooltip("¿La trampa se destruye tras activarse una vez (ej. cristal roto que se desintegra)?")]
    public bool destruirAlActivar = false;

    [Header("--- Físicas / Empuje ---")]
    [Tooltip("Fuerza de empujón aplicada al jugador al pisar la trampa.")]
    public float fuerzaEmpuje = 10f;

    [Tooltip("Duración en segundos del efecto de empuje sobre el jugador.")]
    public float duracionEmpuje = 0.25f;

    [Header("--- Efectos Audiovisuales ---")]
    [Tooltip("Sonido reproducido al dañarse el jugador.")]
    public AudioClip clipSonidoDanio;

    [Range(0f, 1f)]
    public float volumenSonido = 1f;

    [Tooltip("Prefab de partículas a instanciar al activarse la trampa.")]
    public GameObject efectoParticulas;

    [Header("--- Eventos ---")]
    public UnityEvent alDañarJugador;

    private float _ultimoTiempoDanio = -999f;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger && !esDanioContinuo)
        {
            // Se sugiere isTrigger en el editor para zonas de trampa
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (EsJugador(other, out GameObject jugadorObj))
        {
            ProcesarImpacto(jugadorObj);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (esDanioContinuo && EsJugador(other, out GameObject jugadorObj))
        {
            ProcesarDanioContinuo();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (EsJugador(collision.collider, out GameObject jugadorObj))
        {
            ProcesarImpacto(jugadorObj);
        }
    }

    private bool EsJugador(Collider col, out GameObject jugadorObj)
    {
        jugadorObj = null;
        if (col == null) return false;

        if (!string.IsNullOrEmpty(tagJugador) && col.CompareTag(tagJugador))
        {
            jugadorObj = col.gameObject;
            return true;
        }

        var cc = col.GetComponent<CharacterController>() ?? col.GetComponentInParent<CharacterController>();
        if (cc != null)
        {
            jugadorObj = cc.gameObject;
            return true;
        }

        string nombre = col.name.ToLower();
        if (nombre.Contains("player") || nombre.Contains("personaje") || nombre.Contains("hotdog"))
        {
            jugadorObj = col.gameObject;
            return true;
        }

        return false;
    }

    private void ProcesarImpacto(GameObject jugadorObj)
    {
        if (Time.time < _ultimoTiempoDanio + tiempoEnfriamiento) return;
        _ultimoTiempoDanio = Time.time;

        AplicarDanio(cantidadDanio);
        AplicarEfectos(jugadorObj);

        if (destruirAlActivar)
        {
            // Si tiene que destruir el objeto, deshabilitamos visual/collider y destruimos después del empuje
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = false;
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;
            Destroy(gameObject, duracionEmpuje + 0.1f);
        }
    }

    private void ProcesarDanioContinuo()
    {
        float danioFrame = danioPorSegundo * Time.deltaTime;
        AplicarDanio(danioFrame);
    }

    private void AplicarDanio(float danio)
    {
        if (ControladorPartidaBotellas.Instancia != null)
        {
            ControladorPartidaBotellas.Instancia.RecibirDanio(danio);
        }

        alDañarJugador?.Invoke();
    }

    private void AplicarEfectos(GameObject jugadorObj)
    {
        if (clipSonidoDanio != null)
        {
            AudioSource.PlayClipAtPoint(clipSonidoDanio, transform.position, volumenSonido);
        }

        if (efectoParticulas != null)
        {
            Instantiate(efectoParticulas, transform.position, Quaternion.identity);
        }

        if (fuerzaEmpuje > 0f && jugadorObj != null)
        {
            Vector3 direccionEmpuje = (jugadorObj.transform.position - transform.position);
            direccionEmpuje.y = 0f;

            // Si está justo encima de la trampa, empujar en dirección opuesta a su mirada
            if (direccionEmpuje.sqrMagnitude < 0.01f)
            {
                direccionEmpuje = -jugadorObj.transform.forward;
            }

            direccionEmpuje.Normalize();
            direccionEmpuje += Vector3.up * 0.3f; // Pequeño impulso hacia arriba
            direccionEmpuje.Normalize();

            // 1. Si el personaje tiene CharacterController (StarterAssets)
            CharacterController cc = jugadorObj.GetComponent<CharacterController>() ?? jugadorObj.GetComponentInParent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                StartCoroutine(CoEmpujarCharacterController(cc, direccionEmpuje, fuerzaEmpuje, duracionEmpuje));
            }
            // 2. Si usa Rigidbody estándar
            else
            {
                Rigidbody rb = jugadorObj.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddForce(direccionEmpuje * fuerzaEmpuje, ForceMode.Impulse);
                }
            }
        }
    }

    private System.Collections.IEnumerator CoEmpujarCharacterController(CharacterController cc, Vector3 direccion, float fuerza, float duracion)
    {
        float tiempo = 0f;
        while (tiempo < duracion && cc != null && cc.enabled)
        {
            float factor = Mathf.SmoothStep(1f, 0f, tiempo / duracion);
            Vector3 desplazamiento = direccion * (fuerza * factor * Time.deltaTime);
            cc.Move(desplazamiento);
            tiempo += Time.deltaTime;
            yield return null;
        }
    }
}
