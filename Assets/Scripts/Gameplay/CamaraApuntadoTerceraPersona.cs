using Unity.Cinemachine;
using UnityEngine;

/// <summary>Acercamiento al hombro sobre Cinemachine, conservando su deteccion de obstaculos.</summary>
[DefaultExecutionOrder(150)]
public sealed class CamaraApuntadoTerceraPersona : MonoBehaviour
{
    [Min(.5f)] public float distanciaApuntado = 1.8f;
    [Range(15f, 60f)] public float fovApuntado = 32f;
    [Min(.05f)] public float segundosTransicion = .22f;
    public Vector3 hombroApuntado = new Vector3(.65f, .10f, 0f);
    private ControladorArmaFuego arma;
    private CinemachineCamera virtualCam;
    private CinemachineThirdPersonFollow seguimiento;
    private Vector3 hombroOriginal, amortiguacionOriginal;
    private float distanciaOriginal, fovOriginal, ladoOriginal, brazoOriginal, mezcla;

    public void Inicializar(ControladorArmaFuego controlador) => arma = controlador;

    private void Update()
    {
        if (arma == null || IntroduccionIsla.Activa || Time.timeScale <= 0f) return;
        if (virtualCam == null || seguimiento == null)
        {
            var principal = Camera.main;
            var brain = principal != null ? principal.GetComponent<CinemachineBrain>() : null;
            var activa = brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;
            if (activa == null || activa.Target.TrackingTarget == null || !activa.Target.TrackingTarget.IsChildOf(transform)) return;
            var follow = activa.GetComponent<CinemachineThirdPersonFollow>();
            if (follow == null) return;
            // El personaje se mueve en Update; con SmartUpdate la camara alterna de metodo al correr y vibra.
            if (brain.UpdateMethod != CinemachineBrain.UpdateMethods.LateUpdate)
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            virtualCam = activa;
            seguimiento = follow;
            hombroOriginal = follow.ShoulderOffset;
            amortiguacionOriginal = follow.Damping;
            distanciaOriginal = follow.CameraDistance;
            ladoOriginal = follow.CameraSide;
            brazoOriginal = follow.VerticalArmLength;
            fovOriginal = activa.Lens.FieldOfView;
        }
        mezcla = Mathf.MoveTowards(mezcla, arma.SolicitaApuntar ? 1f : 0f, Time.deltaTime / segundosTransicion);
        float t = Mathf.SmoothStep(0f, 1f, mezcla);
        seguimiento.CameraDistance = Mathf.Lerp(distanciaOriginal, Mathf.Min(distanciaOriginal, distanciaApuntado), t);
        seguimiento.ShoulderOffset = Vector3.Lerp(hombroOriginal, hombroApuntado, t);
        seguimiento.CameraSide = Mathf.Lerp(ladoOriginal, 1f, t);
        seguimiento.VerticalArmLength = Mathf.Lerp(brazoOriginal, .08f, t);
        seguimiento.Damping = Vector3.Lerp(amortiguacionOriginal, new Vector3(.05f, .1f, .1f), t);
        virtualCam.Lens.FieldOfView = Mathf.Lerp(fovOriginal, Mathf.Min(fovOriginal, fovApuntado), t);
    }

    public void Restablecer()
    {
        mezcla = 0f;
        if (seguimiento != null)
        {
            seguimiento.CameraDistance = distanciaOriginal;
            seguimiento.ShoulderOffset = hombroOriginal;
            seguimiento.CameraSide = ladoOriginal;
            seguimiento.VerticalArmLength = brazoOriginal;
            seguimiento.Damping = amortiguacionOriginal;
        }
        if (virtualCam != null) virtualCam.Lens.FieldOfView = fovOriginal;
    }

    private void OnDisable() => Restablecer();
}
