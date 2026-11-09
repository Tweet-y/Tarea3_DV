using UnityEngine;

/// <summary>
/// Capa procedural sobre la animacion de Nemesis: inclina y gira el torso y lleva cada mano
/// a un punto del mundo con IK de dos huesos. PeleaJefeLancha anima estos valores para
/// agacharse, arrancar objetos, lanzarlos, golpear el suelo, rugir y caer.
/// </summary>
[DefaultExecutionOrder(700)] // Despues del Animator y de ReaccionImpacto.
public sealed class AnimadorNemesis : MonoBehaviour
{
    /// <summary>Grados: positivo se inclina hacia delante.</summary>
    public float inclinacion;
    /// <summary>Grados de giro del torso alrededor del eje vertical.</summary>
    public float giro;
    [Range(0f, 1f)] public float pesoDer, pesoIzq;
    public Vector3 objetivoDer, objetivoIzq;

    Transform espina, pecho, cabeza, brazoD, antebrazoD, manoD, brazoI, antebrazoI, manoI;

    public Transform ManoDer => manoD;
    public Transform ManoIzq => manoI;
    public Transform Cabeza => cabeza;
    public bool Valido => brazoD != null && antebrazoD != null && manoD != null;
    public float LargoBrazo => Valido ? Vector3.Distance(brazoD.position, antebrazoD.position) + Vector3.Distance(antebrazoD.position, manoD.position) : 1f;

    public static AnimadorNemesis Instalar(GameObject nemesis)
    {
        var a = nemesis.GetComponent<AnimadorNemesis>();
        if (a == null) a = nemesis.AddComponent<AnimadorNemesis>();
        return a;
    }

    void Awake()
    {
        var rig = GetComponent<Animator>();
        if (rig == null || !rig.isHuman) return;
        espina = rig.GetBoneTransform(HumanBodyBones.Spine);
        pecho = rig.GetBoneTransform(HumanBodyBones.Chest);
        if (pecho == null) pecho = rig.GetBoneTransform(HumanBodyBones.UpperChest);
        cabeza = rig.GetBoneTransform(HumanBodyBones.Head);
        brazoD = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
        antebrazoD = rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
        manoD = rig.GetBoneTransform(HumanBodyBones.RightHand);
        brazoI = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        antebrazoI = rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        manoI = rig.GetBoneTransform(HumanBodyBones.LeftHand);
    }

    public Vector3 Hombro(bool derecho)
    {
        var h = derecho ? brazoD : brazoI;
        if (h != null) return h.position;
        float alto = GeografiaIsla.Limites(transform).size.y;
        return transform.position + Vector3.up * alto * .8f + transform.right * (derecho ? 1f : -1f) * alto * .12f;
    }

    public Vector3 Mano(bool derecho)
    {
        var m = derecho ? manoD : manoI;
        return m != null ? m.position : Hombro(derecho) + transform.forward * LargoBrazo * .5f;
    }

    /// <summary>Vuelve a la animacion base.</summary>
    public void Soltar()
    {
        inclinacion = giro = pesoDer = pesoIzq = 0f;
    }

    void LateUpdate()
    {
        Vector3 lado = transform.right;
        if (Mathf.Abs(inclinacion) > .01f || Mathf.Abs(giro) > .01f)
        {
            var torso = Quaternion.AngleAxis(giro * .5f, Vector3.up) * Quaternion.AngleAxis(inclinacion * .5f, lado);
            if (espina != null) espina.rotation = torso * espina.rotation;
            if (pecho != null) pecho.rotation = torso * pecho.rotation;
        }
        Brazo(brazoD, antebrazoD, manoD, objetivoDer, pesoDer, 1f);
        Brazo(brazoI, antebrazoI, manoI, objetivoIzq, pesoIzq, -1f);
    }

    void Brazo(Transform superior, Transform inferior, Transform mano, Vector3 objetivo, float peso, float signo)
    {
        if (superior == null || inferior == null || mano == null || peso <= .001f) return;
        Quaternion baseSup = superior.rotation, baseInf = inferior.rotation;
        float largo = LargoBrazo;
        // Codos hacia afuera y abajo, como un gorila al cargar peso.
        Vector3 polo = superior.position + transform.right * signo * largo * .7f - Vector3.up * largo * .4f - transform.forward * largo * .2f;
        PoseRifleProcedural.ResolverBrazo(superior, inferior, mano, objetivo, polo);
        Quaternion sup = superior.rotation, inf = inferior.rotation;
        superior.rotation = Quaternion.Slerp(baseSup, sup, peso);
        inferior.rotation = Quaternion.Slerp(baseInf, inf, peso);
    }
}
