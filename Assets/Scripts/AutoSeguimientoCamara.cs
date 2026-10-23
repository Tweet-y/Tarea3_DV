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
        // 1. Si no se asignó un objetivo, buscar PlayerCameraRoot en la escena
        if (objetivoSeguimiento == null)
        {
            GameObject rootObj = GameObject.Find("PlayerCameraRoot");
            if (rootObj != null)
            {
                objetivoSeguimiento = rootObj.transform;
            }
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Transform rootChild = player.transform.Find("PlayerCameraRoot");
                    objetivoSeguimiento = rootChild != null ? rootChild : player.transform;
                }
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
