using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Comprueba botellas, capas, golpes, cajas, inventario, mixer, NavMesh y el final.</summary>
[InitializeOnLoad]
public static class ValidarIsla
{
    const string Clave = "Isla.Validando";
    static int paso;
    static bool cargaPedida;
    static double desde;
    static string informe = "";
    static float vidaAntes;

    static string Ruta => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "validar-isla.txt"));

    static ValidarIsla()
    {
        if (SessionState.GetBool(Clave, false)) Registrar();
        else EditorApplication.update += Pendiente;
    }

    static void Pendiente()
    {
        if (!File.Exists(Ruta)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.update -= Pendiente;
        File.Delete(Ruta);
        Ejecutar();
    }

    [MenuItem("Tools/Isla/Verificar")]
    public static void Ejecutar()
    {
        Directory.CreateDirectory("Logs");
        paso = 0;
        cargaPedida = false;
        informe = "";
        SessionState.SetBool(Clave, true);
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
        File.WriteAllText("Logs/isla-validada.txt", informe);
    }

    static void Avanzar()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - desde > 180) throw new Exception("Tiempo agotado en paso " + paso);
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            if (paso == 0)
            {
                if (SceneManager.GetActiveScene().name == "MenuInicio")
                {
                    if (cargaPedida) return;
                    var menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
                    if (menu == null) return;
                    menu.Cargar(ControladorMenus.EscenaJuego);
                    cargaPedida = true;
                    return;
                }
                if (SceneManager.GetActiveScene().name != "MapaV2") return;
                var intro = UnityEngine.Object.FindFirstObjectByType<IntroduccionIsla>();
                if (intro != null) intro.enabled = false;
                var partida = ControladorPartidaBotellas.Instancia;
                var jugador = GameObject.FindGameObjectWithTag("Player");
                if (partida == null || jugador == null) return;
                Sonido.AplicarEscena();
                int botellas = UnityEngine.Object.FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Count(b => b.tipo == ObjetoEspecialColeccionable.TipoBotella.Objetivo);
                Exigir(botellas == 30, "Hay 30 botellas objetivo (" + botellas + ").");
                int armaEnemigo = LayerMask.NameToLayer(CapasJuego.ArmaEnemigo);
                int enemigos = LayerMask.NameToLayer(CapasJuego.Enemigos);
                int jugadorCapa = LayerMask.NameToLayer(CapasJuego.Jugador);
                int armaJugador = LayerMask.NameToLayer(CapasJuego.ArmaJugador);
                int objetos = LayerMask.NameToLayer(CapasJuego.Objetos);
                int recogibles = LayerMask.NameToLayer(CapasJuego.Recogibles);
                Exigir(Physics.GetIgnoreLayerCollision(armaEnemigo, enemigos) && !Physics.GetIgnoreLayerCollision(armaEnemigo, jugadorCapa),
                    "ArmaEnemigo solo choca con el jugador.");
                Exigir(!Physics.GetIgnoreLayerCollision(armaJugador, enemigos) && !Physics.GetIgnoreLayerCollision(armaJugador, objetos),
                    "ArmaJugador choca con enemigos y cajas.");
                Exigir(Physics.GetIgnoreLayerCollision(recogibles, enemigos) && !Physics.GetIgnoreLayerCollision(recogibles, jugadorCapa),
                    "Los recogibles solo chocan con el jugador.");
                Exigir(File.Exists("Assets/Scenes/MapaV2/NavMesh-Isla.asset"), "El NavMesh está guardado como asset.");
                Exigir(NavMesh.SamplePosition(jugador.transform.position, out _, 6f, NavMesh.AllAreas), "El jugador está junto al NavMesh.");
                var zombies = UnityEngine.Object.FindObjectsByType<EnemigoAcechador>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(e => e.name.StartsWith("Zombie_")).Take(2).ToArray();
                Exigir(zombies.Length == 2, "Hay al menos dos zombies.");
                var golpeA = zombies[0].GetComponentInChildren<GolpeMelee>();
                var vidaB = zombies[1].GetComponent<ControladorVida>();
                float antes = vidaB.Vida;
                Vector3 sitio = golpeA.transform.position;
                golpeA.transform.position = zombies[1].transform.position + Vector3.up;
                golpeA.Golpear();
                golpeA.FinGolpe();
                golpeA.transform.position = sitio;
                Exigir(Mathf.Approximately(vidaB.Vida, antes), "El golpe de un zombie no daña a otro zombie.");
                var bate = jugador.GetComponentInChildren<GolpeMelee>();
                Exigir(bate != null, "El jugador tiene el bate.");
                vidaAntes = partida.Vida;
                Vector3 sitioJugador = golpeA.transform.position;
                golpeA.transform.position = jugador.transform.position + Vector3.up;
                golpeA.danioJugador = .12f;
                golpeA.Golpear();
                golpeA.FinGolpe();
                golpeA.transform.position = sitioJugador;
                var animJ = jugador.GetComponentInChildren<Animator>();
                Exigir(partida.Vida < vidaAntes - .05f, "Un zombie baja la vida del jugador.");
                Exigir(EnEstado(animJ, "Golpe"), "El jugador entra en Golpe.");
                bate.danio = 10000f;
                bate.transform.position = zombies[0].transform.position + Vector3.up * .8f;
                bate.Golpear();
                var animZ = zombies[0].GetComponent<Animator>();
                Exigir(EnEstado(animZ, "Morir"), "El bate mata a un zombie y llega a Morir.");
                var caja = UnityEngine.Object.FindFirstObjectByType<CajaRompible>();
                int items = UnityEngine.Object.FindObjectsByType<RecogibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                caja.GetComponent<ControladorVida>().RecibirDanio(5f, caja.transform.position);
                int itemsAhora = UnityEngine.Object.FindObjectsByType<RecogibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                Exigir(itemsAhora > items, "La caja se rompe y suelta un objeto.");
                var inventario = jugador.GetComponent<InventarioJugador>();
                partida.RecibirDanio(.4f);
                float danada = partida.Vida;
                inventario.SumarBotiquin();
                Exigir(inventario.UsarBotiquin() && partida.Vida > danada + .2f, "El botiquín recupera vida.");
                inventario.SumarBuff();
                Exigir(inventario.UsarBuff() && inventario.MultiplicadorDanio > 1.2f, "La bebida sube el daño.");
                var sinGrupo = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(s => s.outputAudioMixerGroup == null).Select(s => s.name).ToArray();
                Exigir(sinGrupo.Length == 0, "Todos los AudioSource usan el mixer" + (sinGrupo.Length == 0 ? "." : ": " + string.Join(", ", sinGrupo) + "."));
                var jefe = GameObject.Find("NemesisJefe");
                var portador = jefe.GetComponent<PortadorBotella>();
                Exigir(portador != null && portador.botella != null && !portador.botella.GetComponent<Collider>().enabled, "El jefe lleva la botella sin poder recogerla.");
                jefe.GetComponent<ControladorVida>().RecibirDanio(100000f, jefe.transform.position);
                Exigir(portador.botella.transform.parent == null && portador.botella.GetComponent<Collider>().enabled, "Al morir, el jefe suelta la botella.");
                foreach (var botella in UnityEngine.Object.FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (botella.tipo == ObjetoEspecialColeccionable.TipoBotella.Objetivo && !botella.YaRecogido)
                        botella.Recoger(jugador);
                Exigir(FinalIsla.FuegosEncendidos && partida.Recogidas >= 30, "La botella 30 enciende los fuegos artificiales.");
                ControladorDialogo.Saltar = true;
                FinalIsla.Instancia.EntrarAlBote();
                desde = EditorApplication.timeSinceStartup;
                paso = 1;
            }
            else if (paso == 1)
            {
                var partida = ControladorPartidaBotellas.Instancia;
                if (partida == null || partida.Estado != ControladorPartidaBotellas.Resultado.Victoria) return;
                Exigir(Time.timeScale == 0f, "Subir a la lancha termina en victoria.");
                Terminar(0);
            }
        }
        catch (Exception e)
        {
            File.WriteAllText("Logs/isla-validada.txt", informe + "ERROR: " + e + "\n");
            Debug.LogException(e);
            Terminar(1);
        }
    }

    static bool EnEstado(Animator anim, string nombre)
    {
        if (anim == null) return false;
        for (int i = 0; i < 6; i++)
        {
            anim.Update(.05f);
            for (int capa = 0; capa < anim.layerCount; capa++)
                if (anim.GetCurrentAnimatorStateInfo(capa).IsName(nombre)) return true;
        }
        return false;
    }

    static void Terminar(int codigo)
    {
        SessionState.SetBool(Clave, false);
        EditorApplication.update -= Avanzar;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode || Environment.GetCommandLineArgs().Contains("ValidarIsla.Ejecutar"))
            EditorApplication.Exit(codigo);
    }
}
