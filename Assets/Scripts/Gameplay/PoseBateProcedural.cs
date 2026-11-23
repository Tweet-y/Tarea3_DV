using UnityEngine;

/// <summary>
/// Bate tomado con las dos manos, encima de la animación. En guardia va bajo y adelante, lejos de la
/// cabeza (también al correr); el golpe es un swing horizontal pesado: carga atrás a la derecha, barre
/// por delante a la altura del pecho y remata a la izquierda, girando el torso. El bate ya no cuelga de
/// la mano derecha (eso lo hacía atravesar la cabeza): se coloca en el mundo y las manos lo siguen con IK.
/// </summary>
[DefaultExecutionOrder(210)]
[DisallowMultipleComponent]
public sealed class PoseBateProcedural : MonoBehaviour
{
    // Medidas del bate Synty: pivote en el pomo, eje +Y hacia la punta, 0,99 m.
    const float Agarre = .125f, ManoIzquierda = .07f, ManoDerecha = .18f, RadioMango = .033f;
    /// <summary>Fracción del golpe en que el bate cruza por delante.</summary>
    public const float MomentoImpacto = .47f;

    struct Clave
    {
        public float rumbo, elevacion, rumboManos, radio, bajo, torso;
        public Clave(float rumbo, float elevacion, float rumboManos, float radio, float bajo, float torso)
        {
            this.rumbo = rumbo; this.elevacion = elevacion; this.rumboManos = rumboManos;
            this.radio = radio; this.bajo = bajo; this.torso = torso;
        }

        public static Clave Lerp(Clave a, Clave b, float t) => new Clave(
            Mathf.Lerp(a.rumbo, b.rumbo, t), Mathf.Lerp(a.elevacion, b.elevacion, t), Mathf.Lerp(a.rumboManos, b.rumboManos, t),
            Mathf.Lerp(a.radio, b.radio, t), Mathf.Lerp(a.bajo, b.bajo, t), Mathf.Lerp(a.torso, b.torso, t));
    }

    // Rumbo: grados desde el frente del cuerpo, positivo a la derecha. Radio y bajo en metros desde el pecho.
    static readonly Clave Guardia = new Clave(-30f, 35f, -10f, .30f, .24f, 0f);
    static readonly Clave Corriendo = new Clave(-35f, 20f, -12f, .27f, .30f, 0f);
    static readonly Clave Carga = new Clave(150f, 40f, 70f, .18f, .06f, 32f);
    static readonly Clave Impacto = new Clave(-12f, 4f, -6f, .40f, .22f, -12f);
    static readonly Clave Remate = new Clave(-120f, 22f, -60f, .25f, .14f, -38f);

    sealed class Mano
    {
        readonly Transform superior, inferior, mano;
        readonly Transform[] dedos = new Transform[15];
        readonly Vector3 ejeDedos, normalPalma, centroPalma, pulgar;
        public readonly bool lista;
        public Transform Hombro => superior;

        public Mano(Animator rig, bool derecho)
        {
            superior = rig.GetBoneTransform(derecho ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            inferior = rig.GetBoneTransform(derecho ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            mano = rig.GetBoneTransform(derecho ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            int primero = (int)(derecho ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            for (int i = 0; i < dedos.Length; i++) dedos[i] = rig.GetBoneTransform((HumanBodyBones)(primero + i));
            var medio = rig.GetBoneTransform(derecho ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            var indice = rig.GetBoneTransform(derecho ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var menique = rig.GetBoneTransform(derecho ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            var dedoGordo = rig.GetBoneTransform(derecho ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            if (superior == null || inferior == null || mano == null || medio == null || indice == null) return;
            // Mismas convenciones que PoseRifleProcedural: normal de la palma hacia lo que agarra.
            ejeDedos = mano.InverseTransformDirection(medio.position - mano.position).normalized;
            Vector3 ancho = menique != null ? indice.position - menique.position : indice.position - medio.position;
            normalPalma = mano.InverseTransformDirection(Vector3.Cross(medio.position - mano.position, ancho)).normalized * (derecho ? 1f : -1f);
            centroPalma = mano.InverseTransformPoint(Vector3.Lerp(mano.position, (medio.position + indice.position) * .5f, .5f));
            pulgar = dedoGordo != null ? mano.InverseTransformDirection(dedoGordo.position - mano.position).normalized
                : mano.InverseTransformDirection(indice.position - (menique != null ? menique.position : medio.position)).normalized;
            lista = normalPalma.sqrMagnitude > .5f && ejeDedos.sqrMagnitude > .5f;
        }

        /// <summary>Lleva la palma al mango, rodeándolo, con el pulgar hacia la punta del bate.</summary>
        public void Agarrar(Vector3 punto, Vector3 eje, Vector3 polo, float peso)
        {
            if (!lista) return;
            Quaternion baseSuperior = superior.rotation, baseInferior = inferior.rotation, baseMano = mano.rotation;
            // La palma mira al mango desde el lado del hombro.
            Vector3 normal = Vector3.ProjectOnPlane(punto - superior.position, eje);
            if (normal.sqrMagnitude < .0001f) normal = Vector3.ProjectOnPlane(Vector3.down, eje);
            normal.Normalize();
            Vector3 cruce = Vector3.Cross(eje, normal).normalized;
            Quaternion inversa = Quaternion.Inverse(Quaternion.LookRotation(normalPalma, ejeDedos));
            Quaternion a = Quaternion.LookRotation(normal, cruce) * inversa;
            Quaternion b = Quaternion.LookRotation(normal, -cruce) * inversa;
            Quaternion giro = Vector3.Dot(a * pulgar, eje) >= Vector3.Dot(b * pulgar, eje) ? a : b;
            Vector3 palma = punto - normal * RadioMango;
            Vector3 muneca = palma - giro * Vector3.Scale(centroPalma, mano.lossyScale);
            PoseRifleProcedural.ResolverBrazo(superior, inferior, mano, muneca, polo);
            Quaternion giroSuperior = superior.rotation, giroInferior = inferior.rotation;
            superior.rotation = Quaternion.Slerp(baseSuperior, giroSuperior, peso);
            inferior.rotation = Quaternion.Slerp(baseInferior, giroInferior, peso);
            mano.rotation = Quaternion.Slerp(baseMano, giro, peso);
            // Puño cerrado alrededor del mango.
            Vector3 ejeFlexion = Vector3.Cross(mano.TransformDirection(ejeDedos), mano.TransformDirection(normalPalma)).normalized;
            for (int i = 0; i < dedos.Length; i++)
            {
                var dedo = dedos[i];
                if (dedo == null) continue;
                bool gordo = i < 3;
                float flexion = gordo ? 25f : (i % 3 == 0 ? 55f : 65f);
                dedo.rotation = Quaternion.AngleAxis(flexion * peso, gordo ? -eje : ejeFlexion) * dedo.rotation;
            }
        }
    }

    Transform bate, espina, pecho;
    Mano derecha, izquierda;
    CharacterController movimiento;
    Camera camara;
    float peso, carrera, ciclo, inicioGolpe = -10f, duracion = .85f;
    bool lista;

    public bool Golpeando => Time.time - inicioGolpe < duracion;

    public void Inicializar(Transform modeloBate, Animator rig)
    {
        bate = modeloBate;
        movimiento = GetComponent<CharacterController>();
        derecha = new Mano(rig, true);
        izquierda = new Mano(rig, false);
        espina = rig.GetBoneTransform(HumanBodyBones.Spine);
        pecho = rig.GetBoneTransform(HumanBodyBones.Chest);
        if (pecho == null) pecho = rig.GetBoneTransform(HumanBodyBones.UpperChest);
        lista = derecha.lista && izquierda.lista;
        if (!lista) return;
        // Fuera de la mano y a escala real: la pose lo coloca en el mundo cada cuadro.
        bate.SetParent(transform, false);
        Vector3 escala = transform.lossyScale;
        bate.localScale = new Vector3(1f / Mathf.Max(.0001f, escala.x), 1f / Mathf.Max(.0001f, escala.y), 1f / Mathf.Max(.0001f, escala.z));
    }

    /// <summary>Empieza un swing que dura los segundos indicados.</summary>
    public void Golpe(float segundos)
    {
        inicioGolpe = Time.time;
        duracion = Mathf.Max(.3f, segundos);
        camara = Camera.main;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (!lista || bate == null || !bate.gameObject.activeInHierarchy)
        {
            peso = 0f;
            return;
        }
        peso = Mathf.MoveTowards(peso, 1f, dt / .2f);
        float rapidez = movimiento != null ? GeografiaIsla.Plano(movimiento.velocity).magnitude : 0f;
        carrera = Mathf.MoveTowards(carrera, rapidez > 3.5f ? 1f : 0f, dt * 4f);
        ciclo += dt * Mathf.Lerp(2f, 10f, Mathf.Clamp01(rapidez / 5.3f));

        Clave pose = Clave.Lerp(Guardia, Corriendo, carrera);
        pose.bajo += Mathf.Sin(ciclo) * .012f * (.3f + carrera);
        float t = (Time.time - inicioGolpe) / duracion;
        if (t >= 0f && t < 1f)
        {
            pose = Swing(pose, t);
            // Al empezar el golpe el cuerpo encara hacia donde mira la cámara.
            if (t < MomentoImpacto && camara != null)
            {
                Vector3 frente = GeografiaIsla.Plano(camara.transform.forward);
                if (frente.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(frente), 720f * dt);
            }
        }

        Vector3 arriba = Vector3.up, adelante = GeografiaIsla.Plano(transform.forward).normalized, derechaCuerpo = Vector3.Cross(arriba, adelante);
        // El torso acompaña el golpe: gira a la derecha al cargar y a la izquierda al rematar.
        if (espina != null) espina.rotation = Quaternion.AngleAxis(pose.torso * .4f * peso, arriba) * espina.rotation;
        if (pecho != null) pecho.rotation = Quaternion.AngleAxis(pose.torso * .6f * peso, arriba) * pecho.rotation;

        Vector3 ancla = (derecha.Hombro.position + izquierda.Hombro.position) * .5f - arriba * .1f;
        float rumbo = pose.rumbo * Mathf.Deg2Rad, elevacion = pose.elevacion * Mathf.Deg2Rad, manos = pose.rumboManos * Mathf.Deg2Rad;
        Vector3 eje = (derechaCuerpo * Mathf.Sin(rumbo) + adelante * Mathf.Cos(rumbo)) * Mathf.Cos(elevacion) + arriba * Mathf.Sin(elevacion);
        Vector3 agarre = ancla + (derechaCuerpo * Mathf.Sin(manos) + adelante * Mathf.Cos(manos)) * pose.radio - arriba * pose.bajo;
        Vector3 pomo = agarre - eje * Agarre;
        // +Y del bate sobre el eje; el frente solo fija el giro sobre sí mismo, que en un bate no se nota.
        Vector3 frenteBate = Vector3.ProjectOnPlane(adelante, eje);
        if (frenteBate.sqrMagnitude < .01f) frenteBate = Vector3.ProjectOnPlane(arriba, eje);
        bate.SetPositionAndRotation(pomo, Quaternion.LookRotation(frenteBate, eje));

        Vector3 codos = ancla - arriba * .45f - adelante * .15f;
        izquierda.Agarrar(pomo + eje * ManoIzquierda, eje, codos - derechaCuerpo * .55f, peso);
        derecha.Agarrar(pomo + eje * ManoDerecha, eje, codos + derechaCuerpo * .55f, peso);
    }

    /// <summary>Carga lenta, barrido rápido, remate y vuelta a la guardia.</summary>
    static Clave Swing(Clave guardia, float t)
    {
        if (t < .38f) return Clave.Lerp(guardia, Carga, Mathf.SmoothStep(0f, 1f, t / .38f));
        if (t < .52f)
        {
            float u = (t - .38f) / .14f;
            return Clave.Lerp(Carga, Impacto, u * u);
        }
        if (t < .70f)
        {
            float u = (t - .52f) / .18f;
            return Clave.Lerp(Impacto, Remate, 1f - (1f - u) * (1f - u));
        }
        return Clave.Lerp(Remate, guardia, Mathf.SmoothStep(0f, 1f, (t - .70f) / .30f));
    }
}
