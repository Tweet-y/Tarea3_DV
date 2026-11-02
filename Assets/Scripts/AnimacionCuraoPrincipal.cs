using UnityEngine;
using StarterAssets;

/// <summary>El rig Synty usa retargeting Humanoid nativo; el controlador mueve la capsula.</summary>
[RequireComponent(typeof(Animator))]
public sealed class AnimacionCuraoPrincipal : MonoBehaviour
{
    private ThirdPersonController movimiento;
    private CharacterController capsula;
    private AudioSource sonido;
    private float distanciaPaso, ultimoPaso = -10f, ultimoAterrizaje = -10f;
    private bool estabaEnSuelo;

    private void Awake()
    {
        movimiento = GetComponentInParent<ThirdPersonController>();
        capsula = GetComponentInParent<CharacterController>();
        sonido = gameObject.AddComponent<AudioSource>();
        sonido.playOnAwake = false;
        sonido.spatialBlend = 1f;
        sonido.minDistance = 2f;
        estabaEnSuelo = movimiento != null && movimiento.Grounded;
        var animador = GetComponent<Animator>();
        animador.applyRootMotion = false;
        animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    public void OnFootstep(AnimationEvent evento)
    {
        if (movimiento != null && evento.animatorClipInfo.weight > .5f) ReproducirPaso();
    }

    public void OnLand(AnimationEvent evento)
    {
        if (movimiento != null && evento.animatorClipInfo.weight > .5f) ReproducirAterrizaje();
    }

    private void Update()
    {
        if (movimiento == null || capsula == null || !movimiento.enabled || Time.deltaTime <= 0f) return;
        // Basic Motions no trae eventos de sonido. Conservar los pasos por distancia recorrida.
        bool suelo = movimiento.Grounded;
        if (suelo && !estabaEnSuelo) ReproducirAterrizaje();
        estabaEnSuelo = suelo;
        float velocidad = Vector3.ProjectOnPlane(capsula.velocity, Vector3.up).magnitude;
        if (!suelo || velocidad < .3f) { distanciaPaso = 0f; return; }
        distanciaPaso += velocidad * Time.deltaTime;
        float paso = Mathf.Lerp(.8f, 1.4f, Mathf.InverseLerp(2f, 5.335f, velocidad));
        if (distanciaPaso < paso) return;
        distanciaPaso %= paso;
        ReproducirPaso();
    }

    private void ReproducirPaso()
    {
        if (movimiento == null || !movimiento.Grounded || Time.time - ultimoPaso < .15f) return;
        var clips = movimiento.FootstepAudioClips;
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) sonido.PlayOneShot(clip, movimiento.FootstepAudioVolume);
        ultimoPaso = Time.time;
    }

    private void ReproducirAterrizaje()
    {
        if (movimiento == null || movimiento.LandingAudioClip == null || Time.time - ultimoAterrizaje < .2f) return;
        sonido.PlayOneShot(movimiento.LandingAudioClip, movimiento.FootstepAudioVolume);
        ultimoAterrizaje = Time.time;
    }
}
