using UnityEngine;

/// <summary>
/// Lo que se ve al disparar: llamarada en la boca, chispas, humo que sigue saliendo del cañón después
/// de la ráfaga, balas trazadoras que viajan hasta el blanco y la marca del impacto. Los casquillos
/// son objetos con física (CasquillosFisicos). Los sistemas se crean una vez y se reutilizan.
/// </summary>
public sealed class EfectosRifle : MonoBehaviour
{
    struct Bala
    {
        public LineRenderer linea;
        public Vector3 desde, dir;
        public float distancia, inicio;
        public bool activa;
    }

    const float Velocidad = 240f, Largo = 6f, Ventaja = 1.2f;

    readonly Bala[] balas = new Bala[16];
    int siguiente;
    ParticleSystem llama, chispas, humo, residual;
    float proximoResidual;
    CharacterController cuerpo;
    bool hayAgua;

    public static EfectosRifle Crear(Transform jugador)
    {
        var go = new GameObject("EfectosRifle");
        go.transform.SetParent(jugador, false);
        var efectos = go.AddComponent<EfectosRifle>();
        efectos.cuerpo = jugador.GetComponent<CharacterController>();
        return efectos;
    }

    void Awake()
    {
        hayAgua = FindFirstObjectByType<AguaLetal>() != null;
        llama = EfectosPelea.SistemaManual("LlamaBoca", transform, EfectosPelea.MaterialFogonazo, 30);
        EfectosPelea.Desvanecer(llama);

        chispas = EfectosPelea.SistemaManual("ChispasBoca", transform, EfectosPelea.MaterialParticulas, 80);
        var mainChispas = chispas.main;
        mainChispas.gravityModifier = .5f;
        EfectosPelea.Desvanecer(chispas);
        var estela = chispas.GetComponent<ParticleSystemRenderer>();
        estela.renderMode = ParticleSystemRenderMode.Stretch;
        estela.velocityScale = .03f;
        estela.lengthScale = 1.5f;

        humo = EfectosPelea.SistemaManual("HumoBoca", transform, EfectosPelea.MaterialParticulas, 80);
        var color = humo.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(g);
        var tam = humo.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .5f, 1f, 2.6f));

        // Hilo de humo que sigue saliendo del cañón caliente después de disparar.
        residual = EfectosPelea.SistemaManual("HumoResidual", transform, EfectosPelea.MaterialParticulas, 120);
        var colorResidual = residual.colorOverLifetime;
        colorResidual.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(0f, 1f) });
        colorResidual.color = new ParticleSystem.MinMaxGradient(gr);
        var tamResidual = residual.sizeOverLifetime;
        tamResidual.enabled = true;
        tamResidual.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .4f, 1f, 3f));
        var ruidoResidual = residual.noise;
        ruidoResidual.enabled = true;
        ruidoResidual.strength = .35f;
        ruidoResidual.frequency = .8f;

        for (int i = 0; i < balas.Length; i++)
        {
            var go = new GameObject("Trazadora_" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = EfectosPelea.MaterialParticulas;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, .008f), new Keyframe(.8f, .028f), new Keyframe(1f, .05f));
            var tinte = new Gradient();
            tinte.SetKeys(new[] { new GradientColorKey(new Color(1f, .45f, .1f), 0f), new GradientColorKey(new Color(1f, .78f, .35f), .7f), new GradientColorKey(new Color(1f, .97f, .85f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(1f, 1f) });
            lr.colorGradient = tinte;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            balas[i].linea = lr;
        }
    }

    /// <param name="boca">Boca del cañón en el mundo.</param>
    /// <param name="dir">Dirección en que sale la bala.</param>
    /// <param name="potencia">1 = fusil; la escopeta escupe una llamarada y una nube mucho mayores.</param>
    public void Disparo(Vector3 boca, Vector3 dir, bool apuntando, float potencia = 1f)
    {
        if (dir.sqrMagnitude < .0001f) dir = transform.forward;
        dir.Normalize();
        Vector3 derecha = Vector3.Cross(Vector3.up, dir);
        if (derecha.sqrMagnitude < .0001f) derecha = transform.right;
        derecha.Normalize();
        Vector3 arriba = Vector3.Cross(dir, derecha);
        Vector3 inercia = cuerpo != null ? cuerpo.velocity : Vector3.zero;
        // Apuntando, la llamarada se achica para no tapar el blanco.
        float escala = (apuntando ? .65f : 1f) * potencia;

        llama.Emit(new ParticleSystem.EmitParams
        {
            position = boca + dir * .05f,
            velocity = inercia,
            startSize = Random.Range(.17f, .26f) * escala,
            startLifetime = Random.Range(.03f, .045f),
            startColor = new Color(1f, .93f, .7f, .95f),
            rotation = Random.Range(0f, 360f)
        }, 1);
        for (int i = 0; i < 2 + Mathf.RoundToInt(potencia * 2f); i++)
            llama.Emit(new ParticleSystem.EmitParams
            {
                position = boca + dir * Random.Range(.07f, .17f),
                velocity = dir * Random.Range(5f, 10f) + (derecha * Random.Range(-1f, 1f) + arriba * Random.Range(-1f, 1f)) + inercia,
                startSize = Random.Range(.08f, .13f) * escala,
                startLifetime = Random.Range(.035f, .055f),
                startColor = new Color(1f, Random.Range(.5f, .75f), .18f, .9f),
                rotation = Random.Range(0f, 360f)
            }, 1);

        for (int i = 0; i < Mathf.RoundToInt(4 * potencia * potencia); i++)
            chispas.Emit(new ParticleSystem.EmitParams
            {
                position = boca,
                velocity = dir * Random.Range(12f, 28f) + derecha * Random.Range(-3.5f, 3.5f) + arriba * Random.Range(-3f, 3.5f) + inercia,
                startSize = Random.Range(.012f, .024f),
                startLifetime = Random.Range(.07f, .18f),
                startColor = new Color(1f, Random.Range(.65f, .9f), .3f, 1f)
            }, 1);

        // Bocanada de humo: unas pocas nubes que se abren y suben despacio.
        int nubes = Mathf.RoundToInt(1 + 3 * (potencia - 1f) + Random.value);
        for (int i = 0; i < Mathf.Max(1, nubes); i++)
            humo.Emit(new ParticleSystem.EmitParams
            {
                position = boca + dir * Random.Range(.08f, .25f) * potencia,
                velocity = dir * Random.Range(.6f, 1.8f) * potencia + Vector3.up * .35f + Random.insideUnitSphere * .25f + inercia * .5f,
                startSize = Random.Range(.12f, .2f) * Mathf.Sqrt(potencia),
                startLifetime = Random.Range(.6f, 1.1f) * Mathf.Sqrt(potencia),
                startColor = new Color(.8f, .78f, .75f, .18f + .06f * potencia),
                rotation = Random.Range(0f, 360f)
            }, 1);
    }

    /// <summary>Bala trazadora que viaja de la boca al punto final.</summary>
    public void Trazadora(Vector3 boca, Vector3 fin)
    {
        Vector3 tramo = fin - boca;
        if (tramo.sqrMagnitude <= 4f) return;
        ref var bala = ref balas[siguiente];
        siguiente = (siguiente + 1) % balas.Length;
        bala.desde = boca;
        bala.distancia = tramo.magnitude;
        bala.dir = tramo / bala.distancia;
        bala.inicio = Time.time;
        bala.activa = true;
    }

    /// <summary>Llamar cada cuadro con el cañón caliente: el humo sale con intensidad 0..1.</summary>
    public void HumoResidual(Vector3 boca, Vector3 dir, float intensidad)
    {
        if (intensidad <= 0f || Time.time < proximoResidual) return;
        proximoResidual = Time.time + Mathf.Lerp(.12f, .035f, intensidad);
        residual.Emit(new ParticleSystem.EmitParams
        {
            position = boca + dir * .02f,
            velocity = Vector3.up * Random.Range(.25f, .55f) + dir * .05f + Random.insideUnitSphere * .04f,
            startSize = Random.Range(.025f, .05f),
            startLifetime = Random.Range(1.1f, 1.8f),
            startColor = new Color(.85f, .84f, .82f, .09f + .1f * intensidad),
            rotation = Random.Range(0f, 360f)
        }, 1);
    }

    /// <summary>Marca del tiro sobre lo que no sangra: chispas, polvo, agujero o salpicadura en el agua.</summary>
    public void Impacto(RaycastHit hit)
    {
        if (hit.collider == null) return;
        if (hayAgua && hit.point.y <= GeografiaIsla.NivelAgua + .12f)
        {
            EfectosPelea.Salpicadura(hit.point, .12f);
            return;
        }
        // El agujero solo se deja en superficies que no se mueven ni se rompen.
        bool fijo = hit.rigidbody == null && hit.collider.GetComponentInParent<ControladorVida>() == null;
        EfectosPelea.ImpactoBala(hit.point, hit.normal, 1f, fijo);
    }

    void LateUpdate()
    {
        for (int i = 0; i < balas.Length; i++)
        {
            ref var bala = ref balas[i];
            if (bala.linea == null) continue;
            if (!bala.activa) { bala.linea.enabled = false; continue; }
            float cabeza = Ventaja + (Time.time - bala.inicio) * Velocidad;
            float cola = Mathf.Max(0f, cabeza - Largo);
            if (cola >= bala.distancia - .01f)
            {
                bala.activa = false;
                bala.linea.enabled = false;
                continue;
            }
            cabeza = Mathf.Min(cabeza, bala.distancia);
            bala.linea.SetPosition(0, bala.desde + bala.dir * cola);
            bala.linea.SetPosition(1, bala.desde + bala.dir * cabeza);
            bala.linea.enabled = true;
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < balas.Length; i++)
        {
            balas[i].activa = false;
            if (balas[i].linea != null) balas[i].linea.enabled = false;
        }
    }
}
