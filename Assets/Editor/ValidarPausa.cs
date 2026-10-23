using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ValidarPausa
{
    private const string Clave = "Pausa.Prueba";
    private static int paso;
    private static string informe;
    private static double desde;
    private static float vida, tiempo;
    private static ControladorPartidaBotellas partidaAntesDelClic;
    private static Keyboard teclado;
    private static InputSettings ajustesOriginales, ajustesPrueba;

    static ValidarPausa()
    {
        if (SessionState.GetBool(Clave, false))
        {
            paso = SessionState.GetInt(Clave + "Paso", 0);
            informe = SessionState.GetString(Clave + "Informe", "");
            Registrar();
        }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Logs/pausa-validar.solicitud")) return;
            File.Delete("Logs/pausa-validar.solicitud");
            Ejecutar();
        };
    }

    [MenuItem("Tools/Interfaz/Verificar pausa con Esc")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Logs");
        paso = 0;
        informe = "";
        SessionState.SetBool(Clave, true);
        Guardar();
        Registrar();
        EditorApplication.isPlaying = true;
    }

    private static void Registrar()
    {
        desde = EditorApplication.timeSinceStartup;
        EditorApplication.update += Avanzar;
        EditorApplication.playModeStateChanged += Modo;
    }

    private static void Modo(PlayModeStateChange modo)
    {
        if (modo != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.update -= Avanzar;
        EditorApplication.playModeStateChanged -= Modo;
        if (teclado != null && teclado.added) InputSystem.RemoveDevice(teclado);
        RestaurarInput();
        SessionState.SetBool(Clave, false);
        Time.timeScale = 1;
        AudioListener.pause = false;
        File.WriteAllText("Logs/pausa-verificada.txt", informe + (paso == 9 ? "OK: Salir detuvo Play.\n" : "ERROR: prueba interrumpida.\n"));
    }

    private static void Avanzar()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (EditorApplication.timeSinceStartup - desde > 45) throw new Exception("Tiempo agotado probando pausa, paso " + paso);
            Application.runInBackground = true;
            var partida = ControladorPartidaBotellas.Instancia;
            var menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
            if (paso == 0 && SceneManager.GetActiveScene().name == ControladorMenus.EscenaMenu && menu != null)
            {
                Pulsar(menu, "Iniciar Juego");
                Cambiar(1);
            }
            else if (paso == 1 && partida != null && SceneManager.GetActiveScene().name == ControladorMenus.EscenaJuego)
            {
                ajustesOriginales = InputSystem.settings;
                SessionState.SetString(Clave + "Ajustes", AssetDatabase.GetAssetPath(ajustesOriginales));
                ajustesPrueba = UnityEngine.Object.Instantiate(ajustesOriginales);
                ajustesPrueba.hideFlags = HideFlags.HideAndDontSave;
                ajustesPrueba.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                ajustesPrueba.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings = ajustesPrueba;
                teclado = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(teclado, new KeyboardState(Key.Escape));
                Cambiar(2);
            }
            else if (paso == 2 && partida != null && partida.EstaPausada)
            {
                Exigir(Time.timeScale == 0 && AudioListener.pause && Cursor.lockState == CursorLockMode.None, "Esc pausa mundo y audio y libera cursor.");
                vida = partida.Vida;
                tiempo = Time.time;
                partida.RecibirDanio(1);
                Exigir(partida.Vida == vida, "La pausa bloquea daño.");
                var botella = UnityEngine.Object.FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsSortMode.None).First(b => !b.YaRecogido);
                botella.Recoger(GameObject.FindGameObjectWithTag("Player"));
                Exigir(!botella.YaRecogido, "La pausa bloquea recogidas sin perder botellas.");
                InputSystem.QueueStateEvent(teclado, new KeyboardState());
                Cambiar(3);
            }
            else if (paso == 3 && EditorApplication.timeSinceStartup - desde > .5)
            {
                Exigir(Time.time == tiempo && partida.Vida == vida, "El tiempo y la vida permanecen detenidos.");
                ScreenCapture.CaptureScreenshot("Logs/menu-pausa-play.png");
                Cambiar(31);
            }
            else if (paso == 31 && EditorApplication.timeSinceStartup - desde > .25)
            {
                InputSystem.QueueStateEvent(teclado, new KeyboardState(Key.Escape));
                Cambiar(4);
            }
            else if (paso == 4 && !partida.EstaPausada)
            {
                Exigir(Time.timeScale == 1 && !AudioListener.pause && Cursor.lockState == CursorLockMode.Locked, "Segundo Esc reanuda la partida.");
                InputSystem.QueueStateEvent(teclado, new KeyboardState());
                partida.AlternarPausa();
                menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
                partidaAntesDelClic = partida;
                Pulsar(menu, "Continuar");
                ExigirSonidoEnPausa(menu);
                Pulsar(menu, "Continuar");
                Pulsar(menu, "Nuevo juego");
                ExigirSonidoEnPausa(menu);
                Exigir(partida.EstaPausada && Time.timeScale == 0 && AudioListener.pause,
                    "El clic espera en tiempo real sin levantar prematuramente la pausa.");
                Cambiar(41);
            }
            else if (paso == 41 && EditorApplication.timeSinceStartup - desde > .5)
            {
                Exigir(partida == partidaAntesDelClic && !partida.EstaPausada &&
                    GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerInput>().enabled,
                    "Continuar restaura el control; clics repetidos y otro botón durante la espera no reinician la partida.");
                Cambiar(5);
            }
            else if (paso == 5 && EditorApplication.timeSinceStartup - desde > .2)
            {
                partida.AlternarPausa();
                menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
                partidaAntesDelClic = partida;
                Pulsar(menu, "Nuevo juego");
                ExigirSonidoEnPausa(menu);
                Cambiar(6);
            }
            else if (paso == 6 && partida != null && partida != partidaAntesDelClic && !partida.EstaPausada && SceneManager.GetActiveScene().name == ControladorMenus.EscenaJuego)
            {
                Exigir(Time.timeScale == 1 && !AudioListener.pause && partida.Recogidas == 0 && partida.Puntos == 0 && partida.Vida > .99f, "Nuevo juego reinicia la partida y sale de pausa.");
                partida.AlternarPausa();
                Cambiar(7);
            }
            else if (paso == 7 && EditorApplication.timeSinceStartup - desde > .5)
            {
                RestaurarInput();
                Pulsar(menu, "Salir");
                Cambiar(9);
            }
        }
        catch (Exception error)
        {
            informe += "ERROR: " + error + "\n";
            Guardar();
            Debug.LogException(error);
            RestaurarInput();
            EditorApplication.isPlaying = false;
        }
    }

    private static void RestaurarInput()
    {
        if (teclado != null && teclado.added) InputSystem.RemoveDevice(teclado);
        if (ajustesOriginales == null)
            ajustesOriginales = AssetDatabase.LoadAssetAtPath<InputSettings>(SessionState.GetString(Clave + "Ajustes", ""));
        if (ajustesOriginales != null) InputSystem.settings = ajustesOriginales;
        if (ajustesPrueba != null) UnityEngine.Object.DestroyImmediate(ajustesPrueba);
    }

    private static void Pulsar(ControladorMenus menu, string nombre)
    {
        menu.GetComponentsInChildren<Button>().First(b => b.name == nombre).onClick.Invoke();
        informe += "OK: botón " + nombre + ".\n";
        Guardar();
    }

    private static void ExigirSonidoEnPausa(ControladorMenus menu)
    {
        var fuentes = menu.GetComponents<AudioSource>().Where(f => f.ignoreListenerPause).ToArray();
        Exigir(fuentes.Length == 1 && fuentes[0].clip != null && fuentes[0].isPlaying && AudioListener.pause,
            "El sonido del botón se reproduce aunque AudioListener esté pausado.");
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception(mensaje);
        informe += "OK: " + mensaje + "\n";
        Guardar();
    }

    private static void Cambiar(int nuevo)
    {
        paso = nuevo;
        desde = EditorApplication.timeSinceStartup;
        Guardar();
    }

    private static void Guardar()
    {
        SessionState.SetInt(Clave + "Paso", paso);
        SessionState.SetString(Clave + "Informe", informe);
        File.WriteAllText("Logs/pausa-en-curso.txt", "Paso " + paso + "\n" + informe);
    }
}
