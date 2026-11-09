using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Reaccion procedural a los impactos, encima de la animacion: la columna se dobla en la direccion
/// del disparo con un muelle amortiguado, la cabeza se sacude en tiros a la cabeza, el enemigo se
/// tambalea (deja de avanzar y de atacar un instante), es empujado y salpica sangre.
/// Se agrega sola desde ControladorVida; no requiere configurar prefabs.
/// </summary>
[DisallowMultipleComponent]
public sealed class ReaccionImpacto : MonoBehaviour
{
    const float Rigidez = 150f, Amortiguacion = 15f;

    ControladorVida vida;
    EnemigoAcechador acecho;
    NavMeshAgent agente;
    Transform espina, pecho, cabeza;
    Renderer[] renderers;
    MaterialPropertyBlock bloque;
    static Material materialParticula;

    Vector3 eje = Vector3.right;
    float giroLateral;
    float anguloTronco, velocidadTronco, anguloCabeza, velocidadCabeza;
    Vector3 empuje;
    float empujeHasta, destelloHasta;

    void Awake()
    {
        vida = GetComponent<ControladorVida>();
        acecho = GetComponent<EnemigoAcechador>();
        agente = GetComponent<NavMeshAgent>();
        BuscarHuesos();
        renderers = GetComponentsInChildren<Renderer>();
        bloque = new MaterialPropertyBlock();
    }

    void BuscarHuesos()
    {
        var rig = GetComponentInChildren<Animator>();
        if (rig != null && rig.isHuman)
        {
            espina = rig.GetBoneTransform(HumanBodyBones.Spine);
            pecho = rig.GetBoneTransform(HumanBodyBones.Chest);
            if (pecho == null) pecho = rig.GetBoneTransform(HumanBodyBones.UpperChest);
            cabeza = rig.GetBoneTransform(HumanBodyBones.Head);
        }
        if (espina != null && pecho != null && cabeza != null) return;
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLowerInvariant();
            if (cabeza == null && n.EndsWith("head")) cabeza = t;
            else if (pecho == null && (n.EndsWith("chest") || n.EndsWith("spine2") || n.EndsWith("spine_02"))) pecho = t;
            else if (espina == null && (n.EndsWith("spine") || n.EndsWith("spine1") || n.EndsWith("spine_01"))) espina = t;
        }
    }

    /// <param name="direccion">Direccion en que viaja el proyectil o golpe; vacia = desde el jugador hacia el enemigo.</param>
    public void Impactar(Vector3 punto, Vector3 direccion, float danio, bool cabezazo, bool murio)
    {
        Vector3 plano = Vector3.ProjectOnPlane(direccion, Vector3.up);
        if (plano.sqrMagnitude < .01f)
        {
            var jugador = GameObject.FindGameObjectWithTag("Player");
            plano = jugador != null ? Vector3.ProjectOnPlane(transform.position - jugador.transform.position, Vector3.up) : transform.forward;
        }
        if (plano.sqrMagnitude < .0001f) plano = transform.forward;
        plano.Normalize();
        if (direccion.sqrMagnitude < .01f) direccion = plano;
        direccion.Normalize();

        bool pesado = vida != null && vida.esJefe;
        float fuerza = Mathf.Clamp(.55f + danio / 60f, .55f, 1.8f) * (cabezazo ? 1.5f : 1f) * (murio ? 1.3f : 1f);
        if (pesado) fuerza *= .3f;

        // Doblar el tronco hacia donde va el disparo, con un poco de giro para que no se vea mecanico.
        eje = Vector3.Cross(Vector3.up, plano);
        giroLateral = Random.Range(-1f, 1f);
        velocidadTronco += 260f * fuerza;
        velocidadCabeza += (cabezazo ? 520f : 140f) * fuerza;

        // Tambalear y empujar. Un jefe apenas se inmuta.
        if (!pesado)
        {
            if (acecho != null && !murio) acecho.Aturdir(cabezazo ? .7f : .3f);
            float distancia = murio ? (cabezazo ? 1.1f : .75f) : (cabezazo ? .35f : .18f);
            if (Physics.Raycast(transform.position + Vector3.up, plano, distancia + .4f, ~0, QueryTriggerInteraction.Ignore)) distancia = 0f;
            empuje = plano * (distancia / .18f);
            empujeHasta = Time.time + .18f;
        }
        destelloHasta = Time.time + .09f;
        Salpicar(punto, direccion, cabezazo, murio);
    }

    void Update()
    {
        if (Time.time < empujeHasta && empuje.sqrMagnitude > 0f)
        {
            Vector3 paso = empuje * Time.deltaTime;
            if (agente != null && agente.enabled && agente.isOnNavMesh) agente.Move(paso);
            else transform.position += paso;
        }
        if (renderers != null && destelloHasta > 0f)
        {
            bool activo = Time.time < destelloHasta;
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (activo)
                {
                    r.GetPropertyBlock(bloque);
                    bloque.SetColor("_BaseColor", new Color(1f, .35f, .3f));
                    bloque.SetColor("_Color", new Color(1f, .35f, .3f));
                    r.SetPropertyBlock(bloque);
                }
                else r.SetPropertyBlock(null);
            }
            if (!activo) destelloHasta = 0f;
        }
    }

    // Despues del Animator: el muelle se suma a la pose del clip.
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Muelle(ref anguloTronco, ref velocidadTronco, dt);
        Muelle(ref anguloCabeza, ref velocidadCabeza, dt);
        if (Mathf.Abs(anguloTronco) < .01f && Mathf.Abs(anguloCabeza) < .01f) return;
        var giro = Quaternion.AngleAxis(giroLateral * anguloTronco * .25f, Vector3.up);
        if (espina != null) espina.rotation = giro * Quaternion.AngleAxis(anguloTronco * .45f, eje) * espina.rotation;
        if (pecho != null) pecho.rotation = Quaternion.AngleAxis(anguloTronco * .55f, eje) * pecho.rotation;
        if (cabeza != null) cabeza.rotation = Quaternion.AngleAxis(anguloCabeza, eje) * cabeza.rotation;
    }

    static void Muelle(ref float x, ref float v, float dt)
    {
        v += (-Rigidez * x - Amortiguacion * v) * dt;
        x = Mathf.Clamp(x + v * dt, -50f, 50f);
    }

    static void Salpicar(Vector3 punto, Vector3 direccion, bool cabezazo, bool murio)
    {
        if (materialParticula == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            materialParticula = new Material(shader) { name = "SangreImpacto" };
        }
        var go = new GameObject("SangreImpacto");
        go.transform.position = punto;
        go.transform.rotation = Quaternion.LookRotation(direccion);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = .2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, cabezazo ? 5f : 3.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(.025f, .075f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.55f, .02f, .02f), new Color(.8f, .05f, .04f));
        main.gravityModifier = 1.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBurst(0, new ParticleSystem.Burst(0f, (short)(murio ? 34 : cabezazo ? 26 : 14)));
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 28f;
        forma.radius = .02f;
        var tamano = ps.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .2f));
        var render = go.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = materialParticula;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        Destroy(go, 1.5f);
    }
}
