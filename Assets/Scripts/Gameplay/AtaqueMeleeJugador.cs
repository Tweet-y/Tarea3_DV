using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Bate: clic izquierdo o RT cuando está seleccionado.</summary>
[DisallowMultipleComponent]
public class AtaqueMeleeJugador : MonoBehaviour
{
    public GolpeMelee golpe;
    public float esperaGolpe = 0.15f;
    public float duracionGolpe = 0.28f;
    public float cadencia = 0.55f;
    public AudioClip swoosh;

    InputAction atacar;
    Animator animador;
    float proximo;
    bool golpeando;

    void Awake()
    {
        animador = GetComponentInChildren<Animator>();
        atacar = new InputAction("AtacarBate", InputActionType.Button);
        atacar.AddBinding("<Mouse>/leftButton");
        atacar.AddBinding("<Gamepad>/rightTrigger");
        if (golpe == null) golpe = GetComponentInChildren<GolpeMelee>();
    }

    void OnEnable() => atacar.Enable();
    void OnDisable() => atacar.Disable();
    void OnDestroy() => atacar.Dispose();

    void Update()
    {
        if (golpeando || Time.time < proximo || !Puede()) return;
        if (!atacar.WasPressedThisFrame()) return;
        StartCoroutine(Swing());
    }

    bool Puede()
    {
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida == null || partida.EstaPausada || partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso) return false;
        if (IntroduccionIsla.Activa || Time.timeScale <= 0f) return false;
        var inventario = InventarioJugador.Instancia;
        return inventario == null || inventario.armaActiva == InventarioJugador.Arma.Bate;
    }

    IEnumerator Swing()
    {
        golpeando = true;
        proximo = Time.time + cadencia;
        if (animador != null)
        {
            int capa = animador.GetLayerIndex("Combate");
            if (capa >= 0) animador.SetLayerWeight(capa, TieneClip(animador) ? 1f : 0f);
            animador.SetTrigger("Atacar");
        }
        Sonido.Reproducir(swoosh != null ? swoosh : BancoSonidos.Actual != null ? BancoSonidos.Actual.swoosh : null, transform.position, .7f, "Efectos");
        var clip = EnemigoAcechador.ClipConEventos(animador);
        if (clip != null)
        {
            // El clip abre y cierra el golpe con ActivaArma / DesactivaArma (AnimacionCuraoPrincipal los reenvía).
            yield return new WaitForSeconds(clip.length * .9f);
        }
        else
        {
            yield return new WaitForSeconds(esperaGolpe);
            if (golpe != null) golpe.InicioGolpe();
            yield return new WaitForSeconds(duracionGolpe);
        }
        // Por si un golpe recibido cortó el clip antes de DesactivaArma.
        if (golpe != null) golpe.FinGolpe();
        golpeando = false;
    }

    // Eventos de animación del clip de ataque, como ControladorJugador del curso.
    public void ActivaArma()
    {
        if (golpeando && golpe != null) golpe.InicioGolpe();
    }

    public void DesactivaArma()
    {
        if (golpe != null) golpe.FinGolpe();
    }

    public static bool TieneClip(Animator animador)
    {
        if (animador == null || animador.runtimeAnimatorController == null) return false;
        foreach (var clip in animador.runtimeAnimatorController.animationClips)
            if (clip != null && (clip.name.Contains("Melee") || clip.name.Contains("Attack") || clip.name.Contains("Hit") || clip.name.Contains("Dying") || clip.name.Contains("Reaction")))
                return true;
        return false;
    }
}
