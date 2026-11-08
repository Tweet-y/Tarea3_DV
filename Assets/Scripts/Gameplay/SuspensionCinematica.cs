using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>Apaga jugador y enemigos mientras corre un plano de cámara.</summary>
public static class SuspensionCinematica
{
    static readonly List<(Behaviour componente, bool activo)> lista = new List<(Behaviour, bool)>();

    public static void Tomar(GameObject jugador)
    {
        lista.Clear();
        if (jugador == null) return;
        Apagar(jugador.GetComponent<StarterAssets.ThirdPersonController>());
        Apagar(jugador.GetComponent<PlayerInput>());
        foreach (var enemigo in Object.FindObjectsByType<EnemigoAcechador>(FindObjectsSortMode.None))
        {
            Apagar(enemigo.GetComponent<NavMeshAgent>());
            Apagar(enemigo);
        }
    }

    public static void Devolver()
    {
        foreach (var estado in lista)
            if (estado.componente != null) estado.componente.enabled = estado.activo;
        lista.Clear();
    }

    static void Apagar(Behaviour componente)
    {
        if (componente == null) return;
        lista.Add((componente, componente.enabled));
        componente.enabled = false;
    }
}
