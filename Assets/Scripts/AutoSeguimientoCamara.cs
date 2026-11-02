using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Componente que asegura automáticamente que la cámara de Cinemachine siga al personaje
/// incluso si las referencias se desvincularon al arrastrar los prefabs a la escena.
/// </summary>
public class AutoSeguimientoCamara : MonoBehaviour
{
    [Tooltip("Transform objetivo que seguirá la cámara (si se deja vacío, busca PlayerCameraRoot o el objeto con tag Player)")]
    public Transform objetivoSeguimiento;

    void Awake()
    {
        VincularObjetivo();
    }

    void Start()
    {
        VincularObjetivo();
    }

    [ContextMenu("Vincular Objetivo")]
    public void VincularObjetivo()
    {
        // Resolver desde el jugador activo: un modelo de prueba o CJ desactivado no debe capturar la camara.
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var movimiento = player.GetComponent<StarterAssets.ThirdPersonController>();
            if (movimiento != null && movimiento.CinemachineCameraTarget != null)
            {
                objetivoSeguimiento = movimiento.CinemachineCameraTarget.transform;
            }
            else
            {
                objetivoSeguimiento = player.transform.Find("PlayerCameraRoot");
                if (objetivoSeguimiento == null)
                {
                    var root = new GameObject("PlayerCameraRoot");
                    root.transform.SetParent(player.transform, false);
                    root.transform.localPosition = new Vector3(0, 1.575f, 0);
                    objetivoSeguimiento = root.transform;
                }
                if (movimiento != null) movimiento.CinemachineCameraTarget = objetivoSeguimiento.gameObject;
            }
        }

        if (objetivoSeguimiento == null)
        {
            return;
        }

        // 2. Asignar al componente CinemachineCamera
        CinemachineCamera cineCam = GetComponent<CinemachineCamera>();
        if (cineCam != null)
        {
            cineCam.Target.TrackingTarget = objetivoSeguimiento;
            cineCam.Target.LookAtTarget = objetivoSeguimiento;
        }
    }
}
