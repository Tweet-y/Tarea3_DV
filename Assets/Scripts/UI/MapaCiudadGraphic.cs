using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Plano cenital del nivel con sectores de búsqueda, nunca posiciones exactas.</summary>
public sealed class MapaCiudadGraphic : MaskableGraphic
{
    private readonly List<Bounds> edificios = new List<Bounds>();
    private readonly List<Bounds> calles = new List<Bounds>();
    private readonly List<Bounds> parques = new List<Bounds>();
    private RectTransform marco;
    private readonly Dictionary<Vector2Int, Vector2> sectores = new Dictionary<Vector2Int, Vector2>();
    private readonly HashSet<Vector2Int> exploradas = new HashSet<Vector2Int>();
    [SerializeField, Min(1f)] private float zoomMinimapa = 3f;
    [SerializeField, Min(1f)] private float zoomAmpliado = 1.5f;
    [SerializeField, Min(1f)] private float radioExploracion = 22f;
    private readonly Vector2[] recorteA = new Vector2[8], recorteB = new Vector2[8];
    private ControladorPartidaBotellas partida;
    private Transform jugador;
    private Vector2 minimo;
    private float lado, celda;
    private float celdaExploracion;
    private Vector2 centroVista;
    private float LadoVisible => lado / Mathf.Max(1f, ampliado ? zoomAmpliado : zoomMinimapa);
    private Vector2 OrigenVista => centroVista - Vector2.one * LadoVisible * .5f;
    private float siguiente;
    private bool ampliado;

    public void Inicializar(ControladorPartidaBotellas fuente, RectTransform marcoExterior = null)
    {
        partida = fuente;
        marco = marcoExterior != null ? marcoExterior : (RectTransform)transform.parent;
        raycastTarget = false;
        var player = GameObject.FindGameObjectWithTag("Player");
        jugador = player != null ? player.transform : null;
        Bounds limites = new Bounds(jugador != null ? jugador.position : Vector3.zero, Vector3.one * 10f);
        foreach (var b in partida.Botellas) if (b != null) limites.Encapsulate(b.transform.position);
        // Excluir cielo, suelo y decoraciones enormes; usar las huellas de la ciudad real.
        foreach (var raiz in partida.gameObject.scene.GetRootGameObjects())
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                var nombre = r.name.ToLowerInvariant();
                // Conservar calles y parques del nivel en la cartografía, sin inventar rutas.
                if (b.size.x <= 100f && b.size.z <= 100f && b.size.x > 1f && b.size.z > 1f)
                {
                    bool calle = nombre.Contains("road") || nombre.Contains("street") || nombre.Contains("calle");
                    bool parque = nombre.Contains("grass") || nombre.Contains("park") || nombre.Contains("pasto");
                    foreach (var material in r.sharedMaterials)
                    {
                        if (material == null) continue;
                        var etiqueta = material.name.ToLowerInvariant();
                        calle |= etiqueta.Contains("road");
                        parque |= etiqueta.Contains("grass");
                    }
                    if (calle) { calles.Add(b); limites.Encapsulate(b); continue; }
                    if (parque) { parques.Add(b); limites.Encapsulate(b); continue; }
                }
                if (r.GetComponentInParent<ObjetoEspecialColeccionable>() != null ||
                    r.GetComponentInParent<CharacterController>() != null ||
                    b.size.y < 2f || b.size.x < 1f || b.size.z < 1f ||
                    b.size.x > 100f || b.size.z > 100f) continue;
                edificios.Add(b);
                limites.Encapsulate(b);
            }
        lado = Mathf.Max(limites.size.x, limites.size.z) + 20f;
        minimo = new Vector2(limites.center.x, limites.center.z) - Vector2.one * lado * .5f;
        celda = lado / 10f;
        celdaExploracion = lado / 64f;
        centroVista = jugador != null ? new Vector2(jugador.position.x, jugador.position.z) : minimo + Vector2.one * lado * .5f;
        RevelarEntorno();
        ActualizarSectores();
        partida.EstadoActualizado += ActualizarSectores;
    }

    protected override void OnDestroy()
    {
        if (partida != null) partida.EstadoActualizado -= ActualizarSectores;
        base.OnDestroy();
    }
    private void ActualizarSectores()
    {
        sectores.Clear();
        foreach (var b in partida.Botellas)
        {
            if (b == null || b.YaRecogido || !b.gameObject.activeInHierarchy) continue;
            var p = new Vector2(b.transform.position.x, b.transform.position.z) - minimo;
            var clave = new Vector2Int(Mathf.FloorToInt(p.x / celda), Mathf.FloorToInt(p.y / celda));
            sectores[clave] = minimo + new Vector2(clave.x + .5f, clave.y + .5f) * celda;
        }
        SetVerticesDirty();
    }
    private void Update()
    {
        if (partida == null) return;
        if (partida.Estado != ControladorPartidaBotellas.Resultado.EnCurso) { gameObject.SetActive(false); return; }
        if (!partida.EstaPausada && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            ampliado = !ampliado;
            marco.anchorMin = marco.anchorMax = ampliado ? new Vector2(.5f, .5f) : Vector2.zero;
            marco.pivot = ampliado ? new Vector2(.5f, .5f) : Vector2.zero;
            marco.anchoredPosition = ampliado ? Vector2.zero : new Vector2(24, 24);
            marco.sizeDelta = ampliado ? new Vector2(590, 630) : new Vector2(256, 296);
        }
        if (Time.unscaledTime < siguiente || partida.EstaPausada) return;
        siguiente = Time.unscaledTime + 1f / 30f;
        if (jugador != null)
            centroVista = new Vector2(jugador.position.x, jugador.position.z);
        RevelarEntorno();
        SetVerticesDirty();
    }
    private Vector2Int CeldaExplorada(Vector2 mundo)
    {
        var local = (mundo - minimo) / celdaExploracion;
        return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
    }
    private void RevelarEntorno()
    {
        if (jugador == null || celdaExploracion <= 0f) return;
        var centro = CeldaExplorada(centroVista);
        int alcance = Mathf.CeilToInt(radioExploracion / celdaExploracion);
        for (int x = centro.x - alcance; x <= centro.x + alcance; x++)
            for (int y = centro.y - alcance; y <= centro.y + alcance; y++)
            {
                var p = minimo + new Vector2(x + .5f, y + .5f) * celdaExploracion;
                if ((p - centroVista).sqrMagnitude <= radioExploracion * radioExploracion)
                    exploradas.Add(new Vector2Int(x, y));
            }
    }
    private Vector2 Plano(Vector3 mundo) => Plano(new Vector2(mundo.x, mundo.z));
    private Vector2 Plano(Vector2 mundo)
    {
        var r = rectTransform.rect;
        return new Vector2(r.xMin, r.yMin) + (mundo - OrigenVista) / LadoVisible * r.size;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        Quad(vh, r.min, r.max, new Color32(103, 124, 64, 255));
        foreach (var b in parques) Quad(vh, Plano(b.min), Plano(b.max), new Color32(51, 97, 39, 255));
        foreach (var b in calles) Quad(vh, Plano(b.min), Plano(b.max), new Color32(9, 12, 10, 255));
        foreach (var b in edificios)
        {
            var a = Plano(b.min); var z = Plano(b.max);
            a = Vector2.Max(a, r.min); z = Vector2.Min(z, r.max);
            if (z.x <= a.x || z.y <= a.y) continue;
            Quad(vh, a - Vector2.one * 2f, z + Vector2.one * 2f, new Color32(9, 12, 10, 255));
            Quad(vh, a, z, new Color32(234, 235, 220, 255));
            if (z.x - a.x > 12f && z.y - a.y > 12f)
                Quad(vh, a + Vector2.one * 3f, z - Vector2.one * 3f, new Color32(174, 177, 163, 255));
        }
        float radio = celda / LadoVisible * r.width * .72f;
        foreach (var centro in sectores.Values)
        {
            // La pista aparece sólo al descubrir el sector, incluso en el mapa ampliado.
            if (!exploradas.Contains(CeldaExplorada(centro))) continue;
            var p = Plano(centro);
            if (p.x + radio < r.xMin || p.x - radio > r.xMax || p.y + radio < r.yMin || p.y - radio > r.yMax) continue;
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2 / 40, b = (i + 1) * Mathf.PI * 2 / 40;
                var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var v = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Tri(vh, p, p + u * radio, p + v * radio, new Color32(242, 211, 69, 38));
                Tri(vh, p + u * radio, p + v * radio, p + u * (radio - 1.5f), new Color32(250, 215, 74, 210));
                Tri(vh, p + v * radio, p + v * (radio - 1.5f), p + u * (radio - 1.5f), new Color32(250, 215, 74, 210));
            }
        }
        DibujarNiebla(vh);
        if (jugador == null) return;
        var centroJugador = Plano(jugador.position);
        centroJugador = new Vector2(Mathf.Clamp(centroJugador.x, r.xMin + 10, r.xMax - 10), Mathf.Clamp(centroJugador.y, r.yMin + 10, r.yMax - 10));
        float yaw = jugador.eulerAngles.y * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
        var derecha = new Vector2(dir.y, -dir.x);
        Tri(vh, centroJugador + dir * 11, centroJugador - dir * 7 + derecha * 8, centroJugador - dir * 7 - derecha * 8, Color.black);
        Tri(vh, centroJugador + dir * 8, centroJugador - dir * 5 + derecha * 5, centroJugador - dir * 5 - derecha * 5, Color.white);
    }
    private void DibujarNiebla(VertexHelper vh)
    {
        if (celdaExploracion <= 0f) return;
        var inicio = CeldaExplorada(OrigenVista);
        var fin = CeldaExplorada(OrigenVista + Vector2.one * LadoVisible);
        for (int x = inicio.x; x <= fin.x; x++)
            for (int y = inicio.y; y <= fin.y; y++)
            {
                if (exploradas.Contains(new Vector2Int(x, y))) continue;
                var a = minimo + new Vector2(x, y) * celdaExploracion;
                Quad(vh, Plano(a), Plano(a + Vector2.one * celdaExploracion), new Color32(25, 30, 32, 255));
            }
    }
    private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Color c)
    {
        Tri(vh, a, new Vector2(a.x, b.y), b, c);
        Tri(vh, a, b, new Vector2(b.x, a.y), c);
    }
    private void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        // Recortar también círculos y niebla: el zoom nunca desborda el marco del HUD.
        var rect = rectTransform.rect;
        recorteA[0] = a; recorteA[1] = b; recorteA[2] = c;
        var entrada = recorteA; var salida = recorteB;
        int cantidad = 3;
        for (int borde = 0; borde < 4; borde++)
        {
            if (cantidad == 0) return;
            float limite = borde == 0 ? rect.xMin : borde == 1 ? rect.xMax : borde == 2 ? rect.yMin : rect.yMax;
            bool ejeX = borde < 2, mayor = borde == 0 || borde == 2;
            int escritos = 0;
            var anterior = entrada[cantidad - 1];
            float distanciaAnterior = (ejeX ? anterior.x : anterior.y) - limite;
            bool anteriorDentro = mayor ? distanciaAnterior >= 0f : distanciaAnterior <= 0f;
            for (int i = 0; i < cantidad; i++)
            {
                var actual = entrada[i];
                float distancia = (ejeX ? actual.x : actual.y) - limite;
                bool dentro = mayor ? distancia >= 0f : distancia <= 0f;
                if (dentro != anteriorDentro)
                    salida[escritos++] = Vector2.LerpUnclamped(anterior, actual, distanciaAnterior / (distanciaAnterior - distancia));
                if (dentro) salida[escritos++] = actual;
                anterior = actual; distanciaAnterior = distancia; anteriorDentro = dentro;
            }
            cantidad = escritos;
            var temporal = entrada; entrada = salida; salida = temporal;
        }
        if (cantidad < 3) return;
        int n = vh.currentVertCount;
        for (int i = 0; i < cantidad; i++) vh.AddVert(entrada[i], color, Vector2.zero);
        for (int i = 1; i < cantidad - 1; i++) vh.AddTriangle(n, n + i, n + i + 1);
    }
}

/// <summary>Disco vectorial para el borde y la máscara circular del radar.</summary>
public sealed class MapaDiscoGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        vh.AddVert(r.center, color, Vector2.zero);
        for (int i = 0; i < 96; i++)
        {
            float angulo = i * Mathf.PI * 2f / 96f;
            vh.AddVert(r.center + new Vector2(Mathf.Cos(angulo) * r.width * .5f,
                Mathf.Sin(angulo) * r.height * .5f), color, Vector2.zero);
        }
        for (int i = 0; i < 96; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % 96);
    }
}
