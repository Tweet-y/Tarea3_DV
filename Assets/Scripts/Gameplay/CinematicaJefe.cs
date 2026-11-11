using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>Plano del jefe al entrar en su zona. Una sola vez.</summary>
[RequireComponent(typeof(Collider))]
public class CinematicaJefe : MonoBehaviour
{
    public Transform jefe;
    public string linea = "Esa botella es mía. Ven a buscarla.";
    public AudioClip grito;
    bool hecha;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hecha || !other.transform.root.CompareTag("Player")) return;
        hecha = true;
        StartCoroutine(Plano());
    }

    IEnumerator Plano()
    {
        IntroduccionIsla.Extra = true;
        var jugador = GameObject.FindGameObjectWithTag("Player");
        SuspensionCinematica.Tomar(jugador);
        Camera camara = null;
        CinemachineBrain cerebro = null;
        bool cerebroActivo = false;
        Vector3 posicion = default;
        Quaternion rotacion = Quaternion.identity;
        float fov = 60f;
        ControladorDialogo dialogo = null;
        try
        {
            camara = Camera.main;
            if (camara != null)
            {
                cerebro = camara.GetComponent<CinemachineBrain>();
                cerebroActivo = cerebro != null && cerebro.enabled;
                if (cerebro != null) cerebro.enabled = false;
                posicion = camara.transform.position;
                rotacion = camara.transform.rotation;
                fov = camara.fieldOfView;
                Vector3 foco = (jefe != null ? jefe.position : transform.position) + Vector3.up * 1.6f;
                Vector3 plano = foco + Vector3.back * 4.5f + Vector3.up * .6f;
                camara.transform.SetPositionAndRotation(plano, Quaternion.LookRotation(foco - plano));
            }
            var origen = jefe != null ? jefe.position : transform.position;
            Sonido.Reproducir(grito != null ? grito : BancoSonidos.Actual != null ? BancoSonidos.Actual.grito : null, origen, 1f, "Voces", .65f);
            dialogo = ControladorDialogo.Crear(false);
            dialogo.FijarNombre("NEMESIS");
            yield return dialogo.StartCoroutine(dialogo.Revelar(linea, 3.2f, "nemesis_zona"));
        }
        finally
        {
            if (dialogo != null) dialogo.Cerrar();
            if (camara != null)
            {
                camara.transform.SetPositionAndRotation(posicion, rotacion);
                camara.fieldOfView = fov;
            }
            if (cerebro != null) cerebro.enabled = cerebroActivo;
            SuspensionCinematica.Devolver();
            IntroduccionIsla.Extra = false;
        }
    }
}
