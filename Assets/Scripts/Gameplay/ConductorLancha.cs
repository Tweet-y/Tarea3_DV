using UnityEngine;

/// <summary>
/// CJ al timon: lo coloca detras del volante de la lancha, gira el volante segun las curvas
/// y lleva sus manos al aro con IK de dos huesos, encima de la animacion de reposo.
/// </summary>
[DefaultExecutionOrder(600)] // Despues de CJLocomotionAnimation, que corrige los huesos en LateUpdate.
public sealed class ConductorLancha : MonoBehaviour
{
    /// <summary>Giro del volante en grados; lo fija quien conduce la lancha.</summary>
    public float giro;

    Transform lancha, volante;
    Animator rig;
    Transform brazoDer, antebrazoDer, manoDer, brazoIzq, antebrazoIzq, manoIzq;
    Quaternion baseVolante;
    Vector3 ejeLocal, centroLocal, ladoLocal;
    float radio, giroSuave;
    bool listo;

    public static ConductorLancha Instalar(Transform cj, Transform lancha)
    {
        if (cj == null || lancha == null) return null;
        var conductor = cj.GetComponent<ConductorLancha>();
        if (conductor == null) conductor = cj.gameObject.AddComponent<ConductorLancha>();
        conductor.Configurar(lancha);
        return conductor;
    }

    void Configurar(Transform barco)
    {
        lancha = barco;
        volante = null;
        foreach (var t in lancha.GetComponentsInChildren<Transform>(true))
            if (t.name.IndexOf("SteeringW", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.name.IndexOf("Volante", System.StringComparison.OrdinalIgnoreCase) >= 0) { volante = t; break; }
        rig = GetComponentInChildren<Animator>();
        if (rig != null && rig.isHuman)
        {
            brazoDer = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
            antebrazoDer = rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
            manoDer = rig.GetBoneTransform(HumanBodyBones.RightHand);
            brazoIzq = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            antebrazoIzq = rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            manoIzq = rig.GetBoneTransform(HumanBodyBones.LeftHand);
            foreach (var p in rig.parameters)
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) rig.SetFloat("Speed", 0f);
        }
        if (volante == null)
        {
            Debug.LogWarning("ConductorLancha: la lancha no tiene volante (SteeringW); CJ queda de pie en su lugar.");
            return;
        }

        // El eje de giro es la dimension mas delgada de la malla del volante.
        var filtro = volante.GetComponent<MeshFilter>();
        Bounds b = filtro != null && filtro.sharedMesh != null ? filtro.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * .4f);
        Vector3 e = b.extents;
        int eje = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
        ejeLocal = eje == 0 ? Vector3.right : eje == 1 ? Vector3.up : Vector3.forward;
        radio = Mathf.Max(eje == 0 ? 0f : e.x, eje == 1 ? 0f : e.y, eje == 2 ? 0f : e.z) * .85f;
        centroLocal = b.center;
        baseVolante = volante.localRotation;

        // El conductor queda del lado del volante que mira hacia la popa.
        Vector3 ejeMundo = volante.TransformDirection(ejeLocal);
        if (Vector3.Dot(ejeMundo, -lancha.forward) < 0f) ejeLocal = -ejeLocal;
        ejeMundo = volante.TransformDirection(ejeLocal).normalized;
        Vector3 lado = Vector3.ProjectOnPlane(lancha.right, ejeMundo).normalized;
        ladoLocal = volante.InverseTransformDirection(lado);

        Vector3 centro = volante.TransformPoint(centroLocal);
        Vector3 atras = Vector3.ProjectOnPlane(ejeMundo, Vector3.up);
        if (atras.sqrMagnitude < .01f) atras = -lancha.forward;
        atras.Normalize();
        // Hombros ~0.45 m detras del aro y a ~1.4 m del suelo: los pies a la altura correspondiente.
        float hombro = brazoDer != null ? Mathf.Max(.8f, brazoDer.position.y - transform.position.y) : 1.4f;
        Vector3 pies = centro + atras * .5f;
        pies.y = centro.y - hombro + .25f;
        transform.SetParent(lancha, true);
        transform.SetPositionAndRotation(pies, Quaternion.LookRotation(-atras, Vector3.up));
        listo = true;
    }

    void LateUpdate()
    {
        if (!listo || volante == null) return;
        giroSuave = Mathf.Lerp(giroSuave, Mathf.Clamp(giro, -120f, 120f), 1f - Mathf.Exp(-6f * Time.deltaTime));
        volante.localRotation = baseVolante * Quaternion.AngleAxis(giroSuave, ejeLocal);
        Vector3 centro = volante.TransformPoint(centroLocal);
        Vector3 eje = volante.TransformDirection(ejeLocal).normalized;
        Vector3 lado = volante.TransformDirection(ladoLocal).normalized;
        Vector3 arriba = Vector3.Cross(eje, lado).normalized;
        if (Vector3.Dot(arriba, Vector3.up) < 0f) arriba = -arriba;
        float escala = volante.lossyScale.x;
        float r = radio * escala;
        // Manos a las "diez y diez" del aro.
        Vector3 der = centro + (lado * .8f + arriba * .6f).normalized * r;
        Vector3 izq = centro + (-lado * .8f + arriba * .6f).normalized * r;
        Mover(brazoDer, antebrazoDer, manoDer, der, transform.right);
        Mover(brazoIzq, antebrazoIzq, manoIzq, izq, -transform.right);
    }

    void Mover(Transform superior, Transform inferior, Transform mano, Vector3 objetivo, Vector3 afuera)
    {
        if (superior == null || inferior == null || mano == null) return;
        Vector3 polo = superior.position + afuera * .4f - Vector3.up * .6f - transform.forward * .2f;
        PoseRifleProcedural.ResolverBrazo(superior, inferior, mano, objetivo, polo);
    }
}
