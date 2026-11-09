using System.Collections.Generic;
using UnityEngine;

/// <summary>Consultas de agua y tierra para colocar la lancha, a Nemesis y la ruta del combate final.</summary>
public static class GeografiaIsla
{
    static bool medido, hayAgua;
    static float nivel;
    static Bounds agua;

    /// <summary>Objetos que no cuentan como tierra (la lancha, el jugador, Nemesis...).</summary>
    public static readonly List<Transform> Ignorados = new List<Transform>();
    public static Vector3 Centro { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => Invalidar();

    public static void Invalidar()
    {
        medido = false;
        Ignorados.Clear();
    }

    public static float NivelAgua { get { Medir(); return nivel; } }

    static void Medir()
    {
        if (medido) return;
        medido = true;
        hayAgua = false;
        nivel = 0f;
        float mayor = -1f;
        foreach (var a in Object.FindObjectsByType<AguaLetal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var r = a.GetComponent<Renderer>();
            Vector3 escala = a.transform.lossyScale;
            Bounds b = r != null ? r.bounds : new Bounds(a.transform.position,
                new Vector3(10f * Mathf.Abs(escala.x), 0f, 10f * Mathf.Abs(escala.z)));
            float area = b.size.x * b.size.z;
            if (area <= mayor) continue;
            mayor = area;
            nivel = a.transform.position.y;
            agua = b;
            hayAgua = true;
        }
        if (hayAgua) return;
        var w = GameObject.Find("Water");
        if (w == null) return;
        nivel = w.transform.position.y;
        var rw = w.GetComponent<Renderer>();
        if (rw != null) { agua = rw.bounds; hayAgua = true; }
    }

    /// <summary>Verdadero si sobre ese punto no hay tierra por encima del nivel del mar.</summary>
    public static bool EsAgua(Vector3 p)
    {
        Medir();
        if (hayAgua && (p.x < agua.min.x + 4f || p.x > agua.max.x - 4f || p.z < agua.min.z + 4f || p.z > agua.max.z - 4f))
            return false;
        var origen = new Vector3(p.x, nivel + 300f, p.z);
        float menor = float.PositiveInfinity;
        float altura = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(origen, Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= menor || Ignorar(h.collider.transform)) continue;
            menor = h.distance;
            altura = h.point.y;
        }
        return float.IsNegativeInfinity(altura) || altura <= nivel + .35f;
    }

    public static bool EsAguaAmplia(Vector3 p, float radio)
    {
        if (!EsAgua(p)) return false;
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            if (!EsAgua(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radio)) return false;
        }
        return true;
    }

    /// <summary>Punto de suelo bajo p (o p si no hay nada).</summary>
    public static Vector3 Suelo(Vector3 p, float desde = 40f)
    {
        float menor = float.PositiveInfinity;
        Vector3 resultado = p;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * desde, Vector3.down, desde + 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= menor || Ignorar(h.collider.transform)) continue;
            menor = h.distance;
            resultado = h.point;
        }
        return resultado;
    }

    static bool Ignorar(Transform t)
    {
        foreach (var r in Ignorados)
            if (r != null && t.IsChildOf(r)) return true;
        return false;
    }

    public static Transform BuscarPorNombre(string nombre)
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, nombre, System.StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }

    /// <summary>Limites en mundo de los renderers (o colliders) bajo raiz, omitiendo los hijos de excluir.</summary>
    public static Bounds Limites(Transform raiz, params Transform[] excluir)
    {
        bool alguno = false;
        var b = new Bounds(raiz.position, Vector3.zero);
        foreach (var r in raiz.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || Excluido(r.transform, excluir)) continue;
            if (!alguno) { b = r.bounds; alguno = true; }
            else b.Encapsulate(r.bounds);
        }
        if (alguno) return b;
        foreach (var c in raiz.GetComponentsInChildren<Collider>())
        {
            if (Excluido(c.transform, excluir)) continue;
            if (!alguno) { b = c.bounds; alguno = true; }
            else b.Encapsulate(c.bounds);
        }
        return b;
    }

    static bool Excluido(Transform t, Transform[] excluir)
    {
        if (excluir == null) return false;
        foreach (var e in excluir)
            if (e != null && t.IsChildOf(e)) return true;
        return false;
    }

    public static Vector3 Plano(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
