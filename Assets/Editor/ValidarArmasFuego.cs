using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>Prueba real de recogida, impactos, cobertura, pausa, recarga y conservacion de balas.</summary>
[InitializeOnLoad]
public static class ValidarArmasFuego
{
    private const string Clave = "ArmasFuego.Validando";
    private static int paso;
    private static double inicio, desde;
    private static string informe = "";
    private static ControladorArmaFuego arma;
    private static VidaEnemigoFuego blanco;
    private static GameObject pared;
    private static Vector3 origen;
    private static float vidaAntes, recargaAntes;
    private static int balasAntes, reservaAntes;

    static ValidarArmasFuego()
    {
        if (SessionState.GetBool(Clave, false)) Registrar();
    }

    [MenuItem("Tools/Armas/Verificar sistema completo")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Deten Play antes de iniciar la verificacion.");
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Guarda las escenas antes de ejecutar esta prueba.");
        Directory.CreateDirectory("Logs");
        ComprobarIK();
        var datos = AssetDatabase.LoadAssetAtPath<ArmaFuegoDefinicion>("Assets/Resources/Armas/RifleAsalto03.asset");
        Exigir(datos != null && datos.modelo != null && datos.modelo.name == "rifle_asalto_1",
            "Definicion enlazada al modelo solicitado.");
        // El proyecto vuelve al menu al entrar en Play: probar la misma ruta de carga que el jugador.
        EditorSceneManager.OpenScene("Assets/Scenes/MenuInicio.unity");
        SessionState.SetBool(Clave, true);
        Registrar();
        EditorApplication.isPlaying = true;
    }

    private static void Registrar()
    {
        paso = -1;
        informe = "";
        inicio = desde = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Avanzar;
        EditorApplication.update += Avanzar;
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception(mensaje);
        informe += "OK: " + mensaje + "\n";
        File.WriteAllText("Logs/armas-fuego-verificadas.txt", informe);
    }

    private static void Avanzar()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - inicio > 90) throw new Exception("Tiempo agotado en paso " + paso);
            if (!EditorApplication.isPlaying) return;
            var partida = ControladorPartidaBotellas.Instancia;
            if (paso == -1)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
                if (menu == null) return;
                menu.Cargar(ControladorMenus.EscenaJuego);
                paso = 0;
                return;
            }
            if (paso == 0)
            {
                if (IntroduccionIsla.Activa) return;
                arma = UnityEngine.Object.FindFirstObjectByType<ControladorArmaFuego>();
                if (arma == null || partida == null || EditorApplication.timeSinceStartup - inicio < 1f) return;
                var personaje = arma.GetComponentInChildren<Animator>();
                Exigir(arma.name == "curao_principal" && personaje != null && personaje.avatar != null && personaje.avatar.isValid && personaje.isHuman,
                    "curao_principal es el jugador activo con avatar Humanoid valido.");
                Exigir(!personaje.applyRootMotion && personaje.runtimeAnimatorController.animationClips.All(c =>
                    AssetDatabase.GetAssetPath(c).StartsWith("Assets/Kevin Iglesias/Basic Motions/")),
                    "Locomocion usa los clips Basic Motions y la capsula controla el desplazamiento.");
                var camaraJugador = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None)
                    .FirstOrDefault(c => c.Target.TrackingTarget != null && c.Target.TrackingTarget.IsChildOf(arma.transform));
                Exigir(camaraJugador != null, "La camara Cinemachine sigue al nuevo jugador.");
                Exigir(!arma.Equipada && RecogibleArmaFuego.Activos.Any(p => !p.SoloMunicion), "El jugador inicia desarmado y existe un rifle recogible.");
                Exigir(UnityEngine.Object.FindObjectsByType<EnemigoAcechador>(FindObjectsSortMode.None)
                    .All(e => e.GetComponent<VidaEnemigoFuego>() != null), "Los enemigos existentes reciben dano de armas.");
                foreach (var enemigo in UnityEngine.Object.FindObjectsByType<EnemigoAcechador>(FindObjectsSortMode.None))
                    enemigo.gameObject.SetActive(false);
                origen = new Vector3(5000, 1000, 5000);
                var cc = arma.GetComponent<CharacterController>();
                cc.enabled = false;
                arma.GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
                arma.transform.SetPositionAndRotation(origen, Quaternion.identity);
                cc.enabled = true;
                var camara = Camera.main;
                var brain = camara.GetComponent<CinemachineBrain>();
                if (brain != null) brain.enabled = false;
                camara.transform.SetPositionAndRotation(origen + new Vector3(0, 1.5f, -4f), Quaternion.identity);
                var pickup = RecogibleArmaFuego.Activos.First(p => !p.SoloMunicion);
                pickup.transform.position = origen + Vector3.forward;
                Exigir(pickup.Recoger(arma) && arma.Equipada && arma.Municion.Cargador == 30, "Recoger equipa el rifle con treinta balas.");
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "BlancoPruebaArmas";
                go.transform.position = origen + new Vector3(0, 1.3f, 12);
                go.transform.localScale = new Vector3(2, 2, 1);
                blanco = go.AddComponent<VidaEnemigoFuego>();
                desde = EditorApplication.timeSinceStartup;
                paso = 1;
            }
            else if (paso == 1 && EditorApplication.timeSinceStartup - desde > .3)
            {
                Physics.SyncTransforms();
                var rig = arma.GetComponentInChildren<Animator>();
                var modelo = arma.transform.Find("RifleEquipado_SM_Wep_Rifle_Assault_03");
                Exigir(rig != null && rig.isHuman && modelo != null, "Rifle equipado sobre el rig humanoide del jugador.");
                Vector3 culata = modelo.TransformPoint(arma.Definicion.apoyoCulata);
                Vector3 hombro = rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                Exigir(Vector3.Dot(culata - hombro, arma.transform.forward) >= arma.Definicion.separacionHombro - .001f,
                    "La culata permanece delante del hombro del jugador.");
                Exigir(Vector3.Distance(PoseRifleProcedural.CentroDePalma(rig, true),
                    modelo.TransformPoint(arma.Definicion.agarreDerecho)) < .04f &&
                    Vector3.Distance(PoseRifleProcedural.CentroDePalma(rig, false),
                    modelo.TransformPoint(arma.Definicion.agarreIzquierdo)) < .04f,
                    "Las palmas contactan sus agarres, tambien en las manos simplificadas de Synty.");
                vidaAntes = blanco.Vida;
                Exigir(arma.IntentarDisparar() && blanco.Vida < vidaAntes && arma.Municion.Cargador == 29,
                    "El disparo real impacta y consume exactamente una bala.");
                Exigir(!arma.IntentarDisparar() && arma.Municion.Cargador == 29, "La cadencia impide disparar dos veces en el mismo frame.");
                vidaAntes = blanco.Vida;
                pared = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pared.transform.position = origen + new Vector3(0, 1.3f, 6);
                pared.transform.localScale = new Vector3(4, 4, 1);
                desde = EditorApplication.timeSinceStartup;
                paso = 2;
            }
            else if (paso == 2 && EditorApplication.timeSinceStartup - desde > .2)
            {
                Physics.SyncTransforms();
                Exigir(arma.IntentarDisparar() && blanco.Vida == vidaAntes, "Una pared bloquea el dano al blanco.");
                UnityEngine.Object.Destroy(pared);
                Exigir(arma.IniciarRecarga(), "La recarga puede iniciarse tras disparar.");
                balasAntes = arma.Municion.Cargador;
                reservaAntes = arma.Municion.Reserva;
                partida.AlternarPausa();
                recargaAntes = arma.RecargaRestante;
                Exigir(!arma.IntentarDisparar() && !arma.IniciarRecarga(), "En pausa no dispara ni inicia otra recarga.");
                desde = EditorApplication.timeSinceStartup;
                paso = 3;
            }
            else if (paso == 3 && EditorApplication.timeSinceStartup - desde > .4)
            {
                Exigir(arma.RecargaRestante == recargaAntes && arma.Municion.Cargador == balasAntes && arma.Municion.Reserva == reservaAntes,
                    "La pausa congela recarga y municion.");
                partida.Continuar();
                desde = EditorApplication.timeSinceStartup;
                paso = 4;
            }
            else if (paso == 4 && EditorApplication.timeSinceStartup - desde > 2.7)
            {
                Exigir(!arma.Municion.Recargando && arma.Municion.Cargador == 30 && arma.Municion.Reserva == reservaAntes - (30 - balasAntes),
                    "La recarga termina en tiempo de juego y descuenta solo las balas transferidas.");
                var datos = arma.Definicion;
                var caja = RecogibleArmaFuego.Crear(datos, origen + Vector3.right, soloMunicion: true);
                reservaAntes = arma.Municion.Reserva;
                Exigir(caja.Recoger(arma) && arma.Municion.Reserva == reservaAntes + 60, "El cargador recogible agrega sesenta balas de reserva.");
                reservaAntes = arma.Municion.Reserva;
                arma.Soltar();
                Exigir(!arma.Equipada, "Soltar retira el rifle de las manos.");
                var caida = RecogibleArmaFuego.Activos.First(p => !p.SoloMunicion && (p.transform.position - origen).sqrMagnitude < 9);
                Exigir(caida.Recoger(arma) && arma.Municion.Cargador == 30 && arma.Municion.Reserva == reservaAntes,
                    "Volver a recoger conserva cargador y reserva sin duplicarlos.");
                blanco.RecibirDanio(10000, blanco.transform.position);
                Exigir(blanco.Muerto && !blanco.gameObject.activeSelf, "El dano letal desactiva el blanco.");
                informe += "PASS: sistema de armas verificado en Play.\n";
                Terminar();
            }
        }
        catch (Exception error)
        {
            informe += "ERROR: " + error + "\n";
            Debug.LogException(error);
            Terminar();
        }
    }

    private static void Terminar()
    {
        File.WriteAllText("Logs/armas-fuego-verificadas.txt", informe);
        SessionState.SetBool(Clave, false);
        EditorApplication.update -= Avanzar;
        if (ControladorPartidaBotellas.Instancia != null && ControladorPartidaBotellas.Instancia.EstaPausada)
            ControladorPartidaBotellas.Instancia.Continuar();
        EditorApplication.isPlaying = false;
        Debug.Log(informe);
    }

    [MenuItem("Tools/Armas/Verificar IK de brazos")]
    public static void ComprobarIK()
    {
        Directory.CreateDirectory("Logs");
        var raiz = new GameObject("PruebaTemporalIK") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var superior = raiz.transform;
            var inferior = new GameObject("Codo").transform;
            inferior.SetParent(superior, false);
            var mano = new GameObject("Mano").transform;
            mano.SetParent(inferior, false);
            foreach (var objetivo in new[] { new Vector3(.1f, .1f, .4f), new Vector3(-.2f, -.25f, .3f),
                new Vector3(0, 0, 4), Vector3.zero, new Vector3(0, .599f, 0) })
            {
                superior.rotation = Quaternion.identity;
                inferior.localPosition = new Vector3(0, -.32f, 0);
                inferior.localRotation = Quaternion.identity;
                mano.localPosition = new Vector3(0, -.28f, 0);
                mano.localRotation = Quaternion.identity;
                PoseRifleProcedural.ResolverBrazo(superior, inferior, mano, objetivo, new Vector3(.8f, -.2f, -.2f));
                Exigir(Mathf.Abs(Vector3.Distance(superior.position, inferior.position) - .32f) < .0001f &&
                    Mathf.Abs(Vector3.Distance(inferior.position, mano.position) - .28f) < .0001f,
                    "IK conserva ambas longitudes para " + objetivo);
                Exigir(!float.IsNaN(mano.position.sqrMagnitude) && !float.IsInfinity(mano.position.sqrMagnitude),
                    "IK produce una pose finita incluso ante objetivos degenerados.");
                if (objetivo.magnitude > .041f && objetivo.magnitude < .597f)
                    Exigir(Vector3.Distance(mano.position, objetivo) < .001f, "IK alcanza el objetivo accesible.");
            }
            Debug.Log("IK del rifle verificada: longitudes, alcance y objetivos degenerados.");
        }
        finally { UnityEngine.Object.DestroyImmediate(raiz); }
    }
}
