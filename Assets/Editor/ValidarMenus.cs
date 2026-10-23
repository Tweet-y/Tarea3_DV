using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Previsualización y prueba real del ciclo de partida sin modificar las escenas del editor.</summary>
[InitializeOnLoad]
public static class ValidarMenus
{
    private static int paso;
    private static double inicio, desde;
    private static string informe;
    private static double menuVisibleDesde;

    static ValidarMenus()
    {
        if (File.Exists("Logs/menu-redisenio-captura.solicitud")) EditorApplication.update += CapturarRediseno;
        if (SessionState.GetBool("Menus.PruebaActiva", false))
        {
            paso = SessionState.GetInt("Menus.Paso", 0);
            informe = SessionState.GetString("Menus.Informe", "");
            inicio = SessionState.GetFloat("Menus.Inicio", 0);
            EditorApplication.playModeStateChanged += CambiarModo;
            EditorApplication.update += Avanzar;
        }
    }

    private static void CapturarRediseno()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != ControladorMenus.EscenaMenu)
        {
            menuVisibleDesde = 0;
            return;
        }
        if (menuVisibleDesde == 0) menuVisibleDesde = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - menuVisibleDesde < 1) return;
        ScreenCapture.CaptureScreenshot("Logs/menu-pixel-art-play.png");
        File.Delete("Logs/menu-redisenio-captura.solicitud");
        EditorApplication.update -= CapturarRediseno;
    }

    [MenuItem("Tools/Interfaz/Probar inicio muerte reinicio y salida")]
    public static void ProbarFlujo()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        paso = 0;
        informe = "";
        inicio = EditorApplication.timeSinceStartup;
        SessionState.SetBool("Menus.PruebaActiva", true);
        SessionState.SetInt("Menus.Paso", 0);
        SessionState.SetString("Menus.Informe", "");
        SessionState.SetFloat("Menus.Inicio", (float)inicio);
        EditorApplication.playModeStateChanged += CambiarModo;
        EditorApplication.update += Avanzar;
        EditorApplication.isPlaying = true;
    }

    private static void CambiarModo(PlayModeStateChange cambio)
    {
        if (cambio == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            paso = 10;
            SessionState.SetInt("Menus.Paso", paso);
        }
        if (cambio != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= CambiarModo;
        EditorApplication.update -= Avanzar;
        Time.timeScale = 1f;
        SessionState.SetBool("Menus.PruebaActiva", false);
        File.WriteAllText("Logs/menu-flujo-verificado.txt", informe + (paso == 8 ? "OK: Salir detuvo Play.\n" : "ERROR: prueba interrumpida.\n"));
    }

    private static void Avanzar()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - inicio > 90) throw new Exception("Tiempo agotado probando los menús.");
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            var partida = ControladorPartidaBotellas.Instancia;
            var menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
            if (paso == 10)
            {
                SceneManager.LoadScene(ControladorMenus.EscenaMenu);
                paso = 1;
            }
            else if (paso == 1 && SceneManager.GetActiveScene().name == ControladorMenus.EscenaMenu && menu != null)
            {
                Exigir(Time.timeScale == 1 && Cursor.lockState == CursorLockMode.None, "Menú principal desbloquea cursor y tiempo.");
                Pulsar(menu, "Iniciar Juego");
                paso = 2;
            }
            else if (paso == 2 && partida != null && partida.Estado == ControladorPartidaBotellas.Resultado.EnCurso)
            {
                Exigir(partida.Vida > .99f && partida.Recogidas == 0, "Inicio crea una partida limpia.");
                partida.RecibirDanio(1);
                Exigir(partida.Vida == 0 && partida.Estado == ControladorPartidaBotellas.Resultado.Derrota && Time.timeScale == 0, "Vida cero dispara muerte y pausa el mundo.");
                desde = EditorApplication.timeSinceStartup;
                paso = 3;
            }
            else if (paso == 3 && EditorApplication.timeSinceStartup - desde > 2.8 && menu != null)
            {
                TMP_Text resumen = null;
                foreach (var texto in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                    if (texto.name == "Resumen") resumen = texto;
                if (resumen == null) throw new Exception("Falta el resumen de derrota.");
                resumen.ForceMeshUpdate();
                Exigir(!resumen.isTextOverflowing && resumen.fontSize == 24, "Resumen de derrota completo y legible a 24 puntos.");
                Exigir(EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null, "Opciones de muerte tienen selección de teclado.");
                ScreenCapture.CaptureScreenshot("Logs/menu-muerte-play.png");
                desde = EditorApplication.timeSinceStartup;
                paso = 31;
            }
            else if (paso == 31 && EditorApplication.timeSinceStartup - desde > .5 && menu != null)
            {
                Pulsar(menu, "Volver a jugar");
                paso = 4;
            }
            else if (paso == 4 && partida != null && partida.Estado == ControladorPartidaBotellas.Resultado.EnCurso)
            {
                Exigir(partida.Vida > .99f && partida.Recogidas == 0 && Time.timeScale == 1, "Reintento restaura vida botellas y tiempo.");
                partida.RecibirDanio(1);
                desde = EditorApplication.timeSinceStartup;
                paso = 5;
            }
            else if (paso == 5 && EditorApplication.timeSinceStartup - desde > 2.8 && menu != null)
            {
                Pulsar(menu, "Ir al menú principal");
                paso = 6;
            }
            else if (paso == 6 && SceneManager.GetActiveScene().name == ControladorMenus.EscenaMenu && menu != null)
            {
                Exigir(partida == null && Time.timeScale == 1 && Cursor.lockState == CursorLockMode.None, "Regreso al menú elimina la partida y libera cursor.");
                ScreenCapture.CaptureScreenshot("Logs/menu-inicio-play.png");
                desde = EditorApplication.timeSinceStartup;
                paso = 7;
            }
            else if (paso == 7 && EditorApplication.timeSinceStartup - desde > .5 && menu != null)
            {
                paso = 8;
                Pulsar(menu, "Salir");
            }
        }
        catch (Exception error)
        {
            informe += "ERROR: " + error + "\n";
            Debug.LogException(error);
            EditorApplication.isPlaying = false;
        }
        finally
        {
            SessionState.SetInt("Menus.Paso", paso);
            SessionState.SetString("Menus.Informe", informe);
            File.WriteAllText("Logs/menu-flujo-en-curso.txt", "Paso " + paso + "\n" + informe);
        }
    }

    private static void Pulsar(ControladorMenus menu, string nombre)
    {
        foreach (var boton in menu.GetComponentsInChildren<Button>())
        {
            if (boton.name != nombre) continue;
            if (!boton.IsInteractable()) throw new Exception("Botón bloqueado: " + nombre);
            boton.onClick.Invoke();
            informe += "OK: botón " + nombre + ".\n";
            return;
        }
        throw new Exception("Falta botón: " + nombre);
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception(mensaje);
        informe += "OK: " + mensaje + "\n";
    }
}

