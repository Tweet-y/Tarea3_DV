using UnityEngine;

/// <summary>Pose superior Humanoid con agarres de palma, compatible con manos simplificadas de Synty.</summary>
public sealed class PoseRifleProcedural
{
    private sealed class Brazo
    {
        public Transform superior, inferior, mano;
        public Quaternion baseSuperior, baseInferior, baseMano;
        public Vector3 ejeDedos, normalPalma;
        public Vector3 centroPalma;
        public bool orientarPalma;
        private readonly Transform[] dedos = new Transform[12];
        private readonly Quaternion[] baseDedos = new Quaternion[12];

        public Brazo(Animator rig, bool derecho)
        {
            if (rig == null || !rig.isHuman) return;
            superior = rig.GetBoneTransform(derecho ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            inferior = rig.GetBoneTransform(derecho ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            mano = rig.GetBoneTransform(derecho ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var ids = derecho ? new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate,
                HumanBodyBones.RightLittleDistal } : new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.LeftLittleDistal };
            for (int i = 0; i < dedos.Length; i++) dedos[i] = rig.GetBoneTransform(ids[i]);
            var medio = rig.GetBoneTransform(derecho ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            var indice = rig.GetBoneTransform(derecho ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var menique = rig.GetBoneTransform(derecho ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            // Polygon usa un dedo central compartido y no siempre incluye Little/Ring.
            if (mano == null || medio == null || indice == null) return;
            ejeDedos = mano.InverseTransformDirection(medio.position - mano.position).normalized;
            Vector3 ancho = menique != null ? indice.position - menique.position : indice.position - medio.position;
            normalPalma = mano.InverseTransformDirection(Vector3.Cross(medio.position - mano.position,
                ancho)).normalized * (derecho ? 1f : -1f);
            centroPalma = mano.InverseTransformPoint(Vector3.Lerp(mano.position, (medio.position + indice.position) * .5f, .5f));
            orientarPalma = normalPalma.sqrMagnitude > .5f && ejeDedos.sqrMagnitude > .5f;
        }

        public void Aplicar(Vector3 objetivo, Vector3 polo, Quaternion arma, float peso, bool derecho)
        {
            if (superior == null || inferior == null || mano == null) return;
            baseSuperior = superior.rotation;
            baseInferior = inferior.rotation;
            baseMano = mano.rotation;
            Quaternion giroMano = mano.rotation;
            if (orientarPalma)
            {
                // Derecha al pistol grip; izquierda en cuna, con la palma debajo del guardamanos.
                Vector3 dedos = arma * (derecho ? new Vector3(0, -.94f, .34f).normalized : Vector3.right);
                Vector3 normal = arma * (derecho ? Vector3.left : Vector3.up);
                giroMano = Quaternion.LookRotation(normal, dedos) *
                    Quaternion.Inverse(Quaternion.LookRotation(normalPalma, ejeDedos));
            }
            // Los agarres representan el centro de contacto de la palma, no la articulacion de la muñeca.
            Vector3 desplazamiento = Vector3.Scale(centroPalma, mano.lossyScale);
            Vector3 objetivoMuneca = objetivo - giroMano * desplazamiento;
            ResolverBrazo(superior, inferior, mano, objetivoMuneca, polo);
            Quaternion giroSuperior = superior.rotation;
            Quaternion giroInferior = inferior.rotation;
            if (!orientarPalma) giroMano = mano.rotation;
            // Interpolar en orden de padre a hijo para conservar la pose mundial del antebrazo.
            superior.rotation = Quaternion.Slerp(baseSuperior, giroSuperior, peso);
            inferior.rotation = Quaternion.Slerp(baseInferior, giroInferior, peso);
            mano.rotation = Quaternion.Slerp(baseMano, giroMano, peso);
            for (int i = 0; i < dedos.Length; i++)
            {
                var dedo = dedos[i];
                if (dedo == null) continue;
                baseDedos[i] = dedo.localRotation;
                if (!orientarPalma || dedo.childCount == 0) continue;
                Vector3 direccion = dedo.GetChild(0).position - dedo.position;
                Vector3 eje = Vector3.Cross(direccion, mano.TransformDirection(normalPalma)).normalized;
                float flexion = derecho && i < 3 ? 18f : (i % 3 == 1 ? 45f : 28f);
                dedo.rotation = Quaternion.AngleAxis(flexion * peso, eje) * dedo.rotation;
            }
        }

        public void AcercarArma(Transform arma, Vector3 agarre)
        {
            if (superior == null || inferior == null || mano == null) return;
            float alcance = (Vector3.Distance(superior.position, inferior.position) + Vector3.Distance(inferior.position, mano.position)) * .97f;
            Vector3 distancia = arma.TransformPoint(agarre) - superior.position;
            if (distancia.magnitude > alcance) arma.position -= distancia.normalized * (distancia.magnitude - alcance);
        }

        public void AjustarAlturaAlcance(Transform arma, Vector3 agarre, bool derecho)
        {
            if (superior == null || inferior == null || mano == null) return;
            float alcance = (Vector3.Distance(superior.position, inferior.position) + Vector3.Distance(inferior.position, mano.position)) * .98f;
            Vector3 dedosObjetivo = arma.rotation * (derecho ? new Vector3(0, -.94f, .34f).normalized : Vector3.right);
            Vector3 normalObjetivo = arma.rotation * (derecho ? Vector3.left : Vector3.up);
            Quaternion giro = orientarPalma ? Quaternion.LookRotation(normalObjetivo, dedosObjetivo) *
                Quaternion.Inverse(Quaternion.LookRotation(normalPalma, ejeDedos)) : mano.rotation;
            Vector3 objetivo = arma.TransformPoint(agarre) - giro * Vector3.Scale(centroPalma, mano.lossyScale);
            Vector3 diferencia = objetivo - superior.position;
            float horizontal = Vector3.ProjectOnPlane(diferencia, Vector3.up).sqrMagnitude;
            if (horizontal >= alcance * alcance) return;
            float alturaMaxima = Mathf.Sqrt(alcance * alcance - horizontal);
            float altura = Mathf.Clamp(diferencia.y, -alturaMaxima, alturaMaxima);
            arma.position += Vector3.up * (altura - diferencia.y);
        }

        public void Restaurar()
        {
            if (superior == null || inferior == null || mano == null) return;
            superior.rotation = baseSuperior;
            inferior.rotation = baseInferior;
            mano.rotation = baseMano;
            for (int i = 0; i < dedos.Length; i++) if (dedos[i] != null) dedos[i].localRotation = baseDedos[i];
        }
    }

    private readonly Transform cuerpo, modelo, cargador, pecho, hombroDerecho;
    private readonly Brazo derecho, izquierdo;
    private readonly CharacterController movimiento;
    private readonly Vector3 origenCargador;
    private readonly Quaternion giroCargador;
    private Quaternion basePecho;
    private Quaternion orientacionSuave;
    private float peso, apuntado, carrera, velocidad, ciclo, retroceso;
    private float alturaFiltrada, velocidadAltura, pesoEstabilizacion;
    private bool alturaInicializada;
    private bool aplicada;

    public PoseRifleProcedural(Transform cuerpo, Animator rig, Transform modelo, Transform cargador)
    {
        this.cuerpo = cuerpo;
        this.modelo = modelo;
        this.cargador = cargador;
        orientacionSuave = cuerpo.rotation;
        movimiento = cuerpo.GetComponent<CharacterController>();
        derecho = new Brazo(rig, true);
        izquierdo = new Brazo(rig, false);
        if (rig != null && rig.isHuman)
        {
            pecho = rig.GetBoneTransform(HumanBodyBones.Chest) ?? rig.GetBoneTransform(HumanBodyBones.Spine);
            hombroDerecho = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }
        if (cargador != null)
        {
            origenCargador = cargador.localPosition;
            giroCargador = cargador.localRotation;
        }
    }

    public void Aplicar(ArmaFuegoDefinicion datos, Vector3 direccion, bool apuntar, bool disparando,
        bool recargando, float progresoRecarga, float impulso, float dt)
    {
        if (modelo == null || dt <= 0f) return;
        float suavizado = 1f - Mathf.Exp(-12f * dt);
        peso = Mathf.MoveTowards(peso, 1f, dt / .24f);
        apuntado = Mathf.Lerp(apuntado, apuntar ? 1f : 0f, suavizado);
        float rapidez = movimiento != null ? Vector3.ProjectOnPlane(movimiento.velocity, Vector3.up).magnitude : 0f;
        velocidad = Mathf.Lerp(velocidad, rapidez, suavizado);
        carrera = Mathf.Lerp(carrera, !apuntar && !disparando && !recargando && velocidad > 3.5f ? 1f : 0f, suavizado);
        retroceso = Mathf.Lerp(retroceso, impulso, 1f - Mathf.Exp(-30f * dt));
        ciclo += dt * Mathf.Lerp(1.8f, 9f, Mathf.Clamp01(velocidad / 5.3f));
        float respiracion = Mathf.Sin(ciclo) * Mathf.Lerp(.004f, .012f, Mathf.Clamp01(velocidad / 5.3f));
        Vector3 frente = Vector3.ProjectOnPlane(direccion, Vector3.up).normalized;
        if (frente.sqrMagnitude < .001f) frente = cuerpo.forward;
        float inclinacion = Mathf.Clamp(Vector3.SignedAngle(frente, direccion, cuerpo.right), -55f, 55f);
        float activo = Mathf.Max(apuntado, disparando ? 1f : 0f);
        Quaternion orientacion = Quaternion.LookRotation(frente, Vector3.up) *
            Quaternion.Euler(inclinacion * activo, 0, 0);
        orientacionSuave = Quaternion.Slerp(orientacionSuave, orientacion, suavizado);
        orientacion = orientacionSuave;

        // La culata sube al hombro al apuntar, y baja al correr; el agarre sigue la respiracion.
        Vector3 soporte = datos.posicionEnCuerpo + new Vector3(-.025f * apuntado,
            .10f * apuntado - .12f * carrera + respiracion, .015f * apuntado);
        soporte.y -= .16f * (1f - Mathf.SmoothStep(0f, 1f, peso));
        float recarga = recargando ? Ventana(progresoRecarga, 0f, .15f) * (1f - Ventana(progresoRecarga, .85f, 1f)) : 0f;
        modelo.position = cuerpo.TransformPoint(soporte) - orientacion * Vector3.forward * (retroceso * .045f);
        modelo.rotation = orientacion * Quaternion.Euler(7f * (1f - apuntado) + 24f * carrera - retroceso * 3f,
            -10f * recarga, -23f * recarga);

        if (pecho != null)
        {
            basePecho = pecho.rotation;
            pecho.rotation = Quaternion.AngleAxis(inclinacion * activo * .22f * peso, cuerpo.right) * pecho.rotation;
        }
        // Colocar la culata respecto al hombro del avatar, no a la altura fija del modelo anterior.
        if (hombroDerecho != null)
        {
            Vector3 anclaje = hombroDerecho.position - cuerpo.right * .025f + cuerpo.forward * datos.separacionHombro +
                Vector3.up * (Mathf.Lerp(-.10f, .03f, apuntado) - .08f * carrera + respiracion);
            Vector3 culata = modelo.TransformPoint(datos.apoyoCulata);
            modelo.position += anclaje - culata;
            modelo.position -= modelo.forward * (retroceso * .025f);
        }
        // Mantener el rifle alcanzable por ambos brazos antes de resolver el contacto de las palmas.
        for (int i = 0; i < 3; i++)
        {
            derecho.AcercarArma(modelo, datos.agarreDerecho);
            izquierdo.AcercarArma(modelo, datos.agarreIzquierdo);
        }
        // La restriccion de cuerpo tiene prioridad sobre IK, tambien durante retroceso y recarga.
        // Medir la parte trasera real de la culata, no el pivote que esta junto al gatillo.
        if (hombroDerecho != null)
        {
            Vector3 apoyo = modelo.TransformPoint(datos.apoyoCulata);
            float distancia = Vector3.Dot(apoyo - hombroDerecho.position, cuerpo.forward);
            if (distancia < datos.separacionHombro)
                modelo.position += cuerpo.forward * (datos.separacionHombro - distancia);
        }
        // Al bajar el rifle para correr, corregir su altura antes que estirar los brazos o perder el agarre.
        for (int i = 0; i < 3; i++)
        {
            derecho.AjustarAlturaAlcance(modelo, datos.agarreDerecho, true);
            izquierdo.AjustarAlturaAlcance(modelo, datos.agarreIzquierdo, false);
        }
        EstabilizarCaminata(recargando, dt);
        Vector3 manoIzquierda = modelo.TransformPoint(datos.agarreIzquierdo);
        if (cargador != null)
        {
            float retirar = recargando ? Ventana(progresoRecarga, .18f, .4f) * (1f - Ventana(progresoRecarga, .55f, .8f)) : 0f;
            cargador.localPosition = origenCargador + new Vector3(-.12f, -.32f, -.04f) * retirar;
            cargador.localRotation = giroCargador * Quaternion.Euler(0, 0, -15f * retirar);
            if (recargando)
            {
                float tomar = Ventana(progresoRecarga, .03f, .18f);
                manoIzquierda = Vector3.Lerp(manoIzquierda, cargador.position, tomar);
                float cerrojo = Ventana(progresoRecarga, .80f, .88f);
                manoIzquierda = Vector3.Lerp(manoIzquierda, modelo.TransformPoint(new Vector3(-.035f, .12f, .17f)), cerrojo);
                manoIzquierda = Vector3.Lerp(manoIzquierda, modelo.TransformPoint(datos.agarreIzquierdo),
                    Ventana(progresoRecarga, .91f, 1f));
            }
        }
        Vector3 centro = hombroDerecho != null ? hombroDerecho.position - cuerpo.right * .18f - Vector3.up * .22f :
            cuerpo.position + Vector3.up * 1.15f;
        derecho.Aplicar(modelo.TransformPoint(datos.agarreDerecho), centro + cuerpo.right * .8f - cuerpo.forward * .25f,
            modelo.rotation, peso, true);
        izquierdo.Aplicar(manoIzquierda, centro - cuerpo.right * .8f - cuerpo.forward * .15f,
            modelo.rotation, peso, false);
        aplicada = true;
    }

    private void EstabilizarCaminata(bool recargando, float dt)
    {
        // Filtrar despues de todas las restricciones: el ajuste de alcance tambien puede producir saltos.
        // Usar altura relativa a la capsula para seguir escaleras y saltos sin retrasar el desplazamiento real.
        Vector3 local = cuerpo.InverseTransformPoint(modelo.position);
        if (!alturaInicializada)
        {
            alturaFiltrada = local.y;
            alturaInicializada = true;
        }
        float andando = Mathf.InverseLerp(.15f, .6f, velocidad) * (1f - Mathf.InverseLerp(2.6f, 3.5f, velocidad));
        float objetivo = !recargando && movimiento != null && movimiento.isGrounded ? andando : 0f;
        pesoEstabilizacion = Mathf.MoveTowards(pesoEstabilizacion, objetivo, dt * 10f);
        if (pesoEstabilizacion <= 0f)
        {
            alturaFiltrada = local.y;
            velocidadAltura = 0f;
            return;
        }
        alturaFiltrada = Mathf.SmoothDamp(alturaFiltrada, local.y, ref velocidadAltura, .12f, Mathf.Infinity, dt);
        local.y = Mathf.Lerp(local.y, alturaFiltrada, pesoEstabilizacion);
        modelo.position = cuerpo.TransformPoint(local);
    }

    public void Restaurar()
    {
        if (!aplicada) return;
        if (pecho != null) pecho.rotation = basePecho;
        derecho.Restaurar();
        izquierdo.Restaurar();
        if (cargador != null) cargador.SetLocalPositionAndRotation(origenCargador, giroCargador);
        aplicada = false;
    }

    private static float Ventana(float t, float inicio, float fin) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inicio, fin, t));

    public static Vector3 CentroDePalma(Animator rig, bool derecho)
    {
        var mano = rig.GetBoneTransform(derecho ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        var medio = rig.GetBoneTransform(derecho ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        var indice = rig.GetBoneTransform(derecho ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        if (mano == null) return rig.transform.position;
        return medio != null && indice != null ? Vector3.Lerp(mano.position, (medio.position + indice.position) * .5f, .5f) : mano.position;
    }

    /// <summary>IK analitica de dos segmentos con polo estable y limites para objetivos inalcanzables.</summary>
    public static void ResolverBrazo(Transform superior, Transform inferior, Transform mano, Vector3 objetivo, Vector3 polo)
    {
        Vector3 origen = superior.position;
        float a = Vector3.Distance(origen, inferior.position);
        float b = Vector3.Distance(inferior.position, mano.position);
        if (a < .0001f || b < .0001f) return;
        Vector3 diferencia = objetivo - origen;
        Vector3 direccion = diferencia.sqrMagnitude > .000001f ? diferencia.normalized : (mano.position - origen).normalized;
        if (direccion.sqrMagnitude < .5f) direccion = Vector3.forward;
        float distancia = Mathf.Clamp(diferencia.magnitude, Mathf.Abs(a - b) + .0001f, (a + b) * .995f);
        Vector3 normal = Vector3.ProjectOnPlane(polo - origen, direccion).normalized;
        if (normal.sqrMagnitude < .5f) normal = Vector3.ProjectOnPlane(Vector3.up, direccion).normalized;
        if (normal.sqrMagnitude < .5f) normal = Vector3.ProjectOnPlane(Vector3.right, direccion).normalized;
        float avance = (a * a - b * b + distancia * distancia) / (2f * distancia);
        float altura = Mathf.Sqrt(Mathf.Max(0f, a * a - avance * avance));
        Vector3 codo = origen + direccion * avance + normal * altura;
        Vector3 destino = origen + direccion * distancia;
        superior.rotation = Quaternion.FromToRotation(inferior.position - origen, codo - origen) * superior.rotation;
        inferior.rotation = Quaternion.FromToRotation(mano.position - inferior.position, destino - inferior.position) * inferior.rotation;
    }
}
