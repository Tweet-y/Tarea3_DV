using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Comprueba en Play que Nemesis acorta distancia, el titán avanza por su NavMesh y el contacto baja la vida.</summary>
[InitializeOnLoad]
public static class ValidarEnemigos
{
    const string Clave = "Enemigos.Validando";
    static int paso;
    static bool cargaPedida;
    static double desde;
    static float vidaAntes;
    static float distanciaAntes;
    static Vector3 titanAntes;
    static string informe = "";
    static bool posturaRevisada;

    static string RutaValidar => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "validar-enemigos.txt"));

    static ValidarEnemigos()
    {
        if (SessionState.GetBool(Clave, false)) Registrar();
        else EditorApplication.update += ValidarSiEstaPendiente;
    }

    // Igual que ConfigurarEnemigos: con el editor abierto, crear Logs/validar-enemigos.txt lanza la verificación.
    static void ValidarSiEstaPendiente()
    {
        if (!File.Exists(RutaValidar)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.update -= ValidarSiEstaPendiente;
        File.Delete(RutaValidar);
        Ejecutar();
    }

    [MenuItem("Tools/Enemigos/Verificar")]
    public static void Ejecutar()
    {
        Directory.CreateDirectory("Logs");
        paso = 0;
        informe = "";
        posturaRevisada = false;
        SessionState.SetBool(Clave, true);
        // Play directo sobre MapaV1 vuelve al menú (AbrirMenuAlIniciar). El flujo real entra por el menú.
        EditorSceneManager.OpenScene("Assets/Scenes/MenuInicio.unity");
        if (!Application.isBatchMode) EditorApplication.ExecuteMenuItem("Window/General/Game");
        Registrar();
        EditorApplication.isPlaying = true;
    }

    static void Registrar()
    {
        desde = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Avanzar;
        EditorApplication.update += Avanzar;
    }

    static void Exigir(bool valor, string mensaje)
    {
        if (!valor) throw new Exception(mensaje);
        informe += "OK: " + mensaje + "\n";
        File.WriteAllText("Logs/enemigos-validados.txt", informe);
    }

    static void Avanzar()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - desde > 180) throw new Exception("Tiempo agotado en paso " + paso);
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            var partida = ControladorPartidaBotellas.Instancia;
            var jugador = GameObject.FindGameObjectWithTag("Player");
            var nemesis = GameObject.Find("Nemesis");
            var titan = GameObject.Find("Titan");
            if (paso == 0)
            {
                if (SceneManager.GetActiveScene().name == "MenuInicio")
                {
                    if (cargaPedida) return;
                    var menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
                    if (menu == null) return;
                    menu.Cargar("MapaV1");
                    cargaPedida = true;
                    return;
                }
                if (SceneManager.GetActiveScene().name != "MapaV1" || partida == null || jugador == null || nemesis == null || titan == null)
                    return;
                var acecho = nemesis.GetComponent<EnemigoAcechador>();
                var acechoTitan = titan.GetComponent<EnemigoAcechador>();
                if (acecho == null || !acecho.Persiguiendo || acechoTitan == null || !acechoTitan.Persiguiendo) return;
                distanciaAntes = Distancia(nemesis.transform.position, jugador.transform.position);
                titanAntes = titan.transform.position;
                foreach (var malla in nemesis.GetComponentsInChildren<SkinnedMeshRenderer>())
                    malla.updateWhenOffscreen = true;
                desde = EditorApplication.timeSinceStartup;
                paso = 1;
            }
            else if (paso == 1 && !posturaRevisada && EditorApplication.timeSinceStartup - desde > 1)
            {
                posturaRevisada = true;
                ExigirPostura(nemesis);
            }
            else if (paso == 1 && EditorApplication.timeSinceStartup - desde > 4)
            {
                float distancia = Distancia(nemesis.transform.position, jugador.transform.position);
                float avanceTitan = Distancia(titanAntes, titan.transform.position);
                float acercamiento = Distancia(titanAntes, jugador.transform.position) - Distancia(titan.transform.position, jugador.transform.position);
                Exigir(distancia < distanciaAntes - 1.5f, "Nemesis se acerca al jugador (" + distanciaAntes.ToString("0.0") + " → " + distancia.ToString("0.0") + ").");
                Exigir(avanceTitan > 2f && acercamiento > 1f, "El titán avanza hacia el jugador.");
                var agenteTitan = titan.GetComponent<NavMeshAgent>();
                Exigir(agenteTitan != null && agenteTitan.isOnNavMesh
                    && NavMesh.GetSettingsNameFromID(agenteTitan.agentTypeID) == "Titan", "El titán camina sobre su NavMesh (agente Titan).");
                Exigir(nemesis.GetComponent<EnemigoAcechador>().objetivo == jugador.transform
                    && titan.GetComponent<EnemigoAcechador>().objetivo == jugador.transform, "Ambos enemigos tienen al jugador como objetivo.");
                Exigir(titan.GetComponent<Animator>().GetFloat("Speed") >= 2f, "El titán camina (Speed >= 2) mientras avanza.");
                var agua = UnityEngine.Object.FindFirstObjectByType<AguaLetal>();
                Exigir(agua != null, "Hay agua letal en el mapa.");
                var centroAgua = agua.GetComponent<Collider>() != null ? agua.GetComponent<Collider>().bounds.center : agua.transform.position;
                Exigir(!NavMesh.SamplePosition(centroAgua, out _, 1f, NavMesh.AllAreas), "El agua no forma parte del NavMesh.");
                var agente = nemesis.GetComponent<NavMeshAgent>();
                if (agente != null) agente.enabled = false;
                vidaAntes = partida.Vida;
                nemesis.transform.position = jugador.transform.position;
                Physics.SyncTransforms();
                desde = EditorApplication.timeSinceStartup;
                paso = 2;
            }
            else if (paso == 2 && EditorApplication.timeSinceStartup - desde > 1.5)
            {
                Exigir(vidaAntes - partida.Vida > 0.1f, "El contacto con Nemesis baja la vida.");
                Terminar(0);
            }
        }
        catch (Exception e)
        {
            File.WriteAllText("Logs/enemigos-validados.txt", informe + "ERROR: " + e + "\n");
            Debug.LogException(e);
            Terminar(1);
        }
    }

    static void ExigirPostura(GameObject nemesis)
    {
        bool alguno = false;
        var bounds = new Bounds();
        foreach (var malla in nemesis.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            malla.updateWhenOffscreen = true;
            if (!alguno) { bounds = malla.bounds; alguno = true; }
            else bounds.Encapsulate(malla.bounds);
        }
        float altura = alguno ? bounds.size.y : 0f;
        Exigir(altura >= 2.2f && altura <= 3f, "Nemesis mide entre 2,2 y 3,0 m (" + altura.ToString("0.00") + " m).");

        var caderas = nemesis.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        float separacion = caderas == null ? 99f : Distancia(caderas.position, nemesis.transform.position);
        Exigir(caderas != null && separacion < 0.5f, "Las caderas quedan sobre la raíz (" + separacion.ToString("0.00") + " m).");

        var agente = nemesis.GetComponent<NavMeshAgent>();
        Exigir(agente != null && agente.isOnNavMesh, "Nemesis está sobre el NavMesh.");

        var materiales = nemesis.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
        var blancos = materiales.Where(m => m.GetTexture("_BaseMap") == null && m.GetColor("_BaseColor") == Color.white).Select(m => m.name).ToArray();
        Exigir(materiales.Length > 0 && blancos.Length == 0, "Los materiales de Nemesis tienen textura o color (blancos: " + string.Join(", ", blancos) + ").");

        // Con el avatar calibrado en A-pose los brazos bajan 45° de más y las manos cruzan el torso.
        var animator = nemesis.GetComponent<Animator>();
        var derecha = nemesis.transform.right;
        float izquierda = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.LeftHand).position - caderas.position, -derecha);
        float diestra = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.RightHand).position - caderas.position, derecha);
        Exigir(izquierda > 0.2f && diestra > 0.2f, "Las manos quedan a los costados del torso (" + izquierda.ToString("0.00") + " / " + diestra.ToString("0.00") + " m).");
    }

    static float Distancia(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static void Terminar(int codigo)
    {
        SessionState.SetBool(Clave, false);
        EditorApplication.update -= Avanzar;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode || Environment.GetCommandLineArgs().Contains("ValidarEnemigos.Ejecutar"))
            EditorApplication.Exit(codigo);
    }
}
