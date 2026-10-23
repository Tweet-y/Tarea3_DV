using UnityEngine;

/// <summary>Una caída se mide desde su punto más alto y se cobra sólo al aterrizar.</summary>
[RequireComponent(typeof(StarterAssets.ThirdPersonController))]
public sealed class DanioCaida : MonoBehaviour
{
    [Min(0f)] public float alturaSegura = 4f;
    [Min(0f)] public float danioPorMetro = .045f;
    private StarterAssets.ThirdPersonController controlador;
    private bool cayendo;
    private float alturaMaxima;

    private void Awake() => controlador = GetComponent<StarterAssets.ThirdPersonController>();
    private void LateUpdate()
    {
        var partida = ControladorPartidaBotellas.Instancia;
        if (partida == null || partida.EstaPausada || partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso) return;
        if (!controlador.enabled) { cayendo = false; return; }
        float altura = transform.position.y;
        if (!controlador.Grounded)
        {
            if (!cayendo) { cayendo = true; alturaMaxima = altura; }
            alturaMaxima = Mathf.Max(alturaMaxima, altura);
        }
        else if (cayendo)
        {
            cayendo = false;
            partida.RecibirDanio(CalcularDanio(alturaMaxima - altura));
        }
    }
    public float CalcularDanio(float altura) => Mathf.Clamp01(Mathf.Max(0f, altura - alturaSegura) * danioPorMetro);
}
