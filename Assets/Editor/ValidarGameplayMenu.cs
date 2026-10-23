using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Comprueba movimiento, huesos, triggers y HUD antes y después de una recarga de scripts.</summary>
[InitializeOnLoad]
public static class ValidarGameplayMenu
{
    private const string Clave = "GameplayMenu.";
    private static int paso;
    private static double desde;
    private static string informe;
    private static Vector3 posicion;
    private static Quaternion rotacion;
    private static int esperadas, puntosEsperados;

    static ValidarGameplayMenu()
    {
        EditorApplication.update += RevisarSolicitud;
        if (SessionState.GetBool(Clave + "Activa", false))
        {
            paso = SessionState.GetInt(Clave + "Paso", 0);
            desde = SessionState.GetFloat(Clave + "Desde", 0);
            informe = SessionState.GetString(Clave + "Informe", "");
            esperadas = SessionState.GetInt(Clave + "Botellas", 0);
            puntosEsperados = SessionState.GetInt(Clave + "Puntos", 0);
            Registrar();
        }
        EditorApplication.delayCall += () =>
        {
            const string solicitud = "Logs/gameplay-menu-validar.solicitud";
            if (!File.Exists(solicitud) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(solicitud);
            Ejecutar();
        };
    }

    private static void RevisarSolicitud()
    {
        const string solicitud = "Logs/gameplay-menu-validar.solicitud";
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(solicitud)) return;
        File.Delete(solicitud);
        Ejecutar();
    }

    [MenuItem("Tools/Interfaz/Verificar mecanicas y recompilacion")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Logs");
        paso = 0;
        informe = "";
        desde = EditorApplication.timeSinceStartup;
        SessionState.SetBool(Clave + "Activa", true);
        SessionState.SetString(Clave + "Error", "");
        Guardar();
        Registrar();
        EditorApplication.isPlaying = true;
    }

    private static void Registrar()
    {
        EditorApplication.playModeStateChanged += Modo;
        EditorApplication.update += Avanzar;
        Application.logMessageReceived += CapturarError;
    }

    private static void CapturarError(string mensaje, string stack, LogType tipo)
    {
        if (tipo == LogType.Exception || tipo == LogType.Error)
            SessionState.SetString(Clave + "Error", mensaje + "\n" + stack);
    }

    private static void Modo(PlayModeStateChange modo)
    {
        if (modo == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            CambiarPaso(1);
        }
        if (modo != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= Modo;
        EditorApplication.update -= Avanzar;
        Application.logMessageReceived -= CapturarError;
        SessionState.SetBool(Clave + "Activa", false);
        Time.timeScale = 1;
        File.WriteAllText("Logs/gameplay-menu-verificado.txt", informe + (paso == 10 ? "OK: prueba completa.\n" : "ERROR: prueba interrumpida.\n"));
    }

    private static void Avanzar()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            string error = SessionState.GetString(Clave + "Error", "");
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            if (EditorApplication.timeSinceStartup - desde > 45) throw new Exception("Tiempo agotado, paso " + paso);
            var partida = ControladorPartidaBotellas.Instancia;
            var jugador = GameObject.FindGameObjectWithTag("Player");
            if ((paso == 0 || paso == 1) && SceneManager.GetActiveScene().name == ControladorMenus.EscenaMenu)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
                if (menu == null) return;
                menu.GetComponentsInChildren<Button>().First(b => b.name == "Iniciar Juego").onClick.Invoke();
                CambiarPaso(2);
            }
            else if ((paso == 2 || paso == 5) && partida != null && jugador != null && SceneManager.GetActiveScene().name == ControladorMenus.EscenaJuego)
            {
                if (paso == 5)
                    Exigir(partida.Recogidas == esperadas && partida.Puntos == puntosEsperados, "Recompilar conserva contador y puntuación.");
                Exigir(Time.timeScale == 1, "La partida avanza con tiempo normal.");
                var animacion = jugador.GetComponentInChildren<CJLocomotionAnimation>();
                Exigir(animacion != null && animacion.isActiveAndEnabled, "CJ tiene animación activa.");
                posicion = jugador.transform.position;
                rotacion = Pierna(jugador).localRotation;
                jugador.GetComponent<StarterAssets.StarterAssetsInputs>().MoveInput(Vector2.up);
                jugador.GetComponent<StarterAssets.StarterAssetsInputs>().SprintInput(true);
                CambiarPaso(paso == 2 ? 3 : 6);
            }
            else if ((paso == 3 || paso == 6) && EditorApplication.timeSinceStartup - desde > .9)
            {
                Exigir(Vector3.Distance(posicion, jugador.transform.position) > .15f, "El controlador mueve al personaje.");
                Exigir(Quaternion.Angle(rotacion, Pierna(jugador).localRotation) > .1f, "La pierna cambia de pose durante el movimiento.");
                var input = jugador.GetComponent<StarterAssets.StarterAssetsInputs>();
                input.MoveInput(Vector2.zero);
                input.SprintInput(false);
                PrepararRecogida(jugador, partida);
                CambiarPaso(paso == 3 ? 4 : 7);
            }
            else if (paso == 4 || paso == 7)
            {
                var objetivo = UnityEngine.Object.FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.GetInstanceID() == SessionState.GetInt(Clave + "Objetivo", 0));
                if (objetivo != null)
                    File.WriteAllText("Logs/gameplay-trigger-datos.txt", "Paso " + paso + "\nJugador " + jugador.transform.position +
                        "\nCapsula " + jugador.GetComponent<CharacterController>().bounds + "\nBotella " + objetivo.GetComponent<Collider>().bounds +
                        "\nTrigger " + objetivo.GetComponent<Collider>().isTrigger + "\nEnabled " + objetivo.GetComponent<Collider>().enabled +
                        "\nYa recogida " + objetivo.YaRecogido + "\nContador " + partida.Recogidas + " esperadas " + esperadas +
                        "\nDelta " + Time.unscaledDeltaTime);
                jugador.GetComponent<CharacterController>().Move(Vector3.right * 3f * Time.unscaledDeltaTime);
                // La primera cerveza comprueba el callback físico. Tras recargar,
                // comprueba por separado el evento de recogida y sus suscriptores.
                if (paso == 7 && objetivo != null && !objetivo.YaRecogido)
                    objetivo.Recoger(jugador);
                if (partida.Recogidas != esperadas) return;
                Exigir(partida.Puntos == puntosEsperados, paso == 4
                    ? "El trigger recoge una cerveza y suma sus puntos."
                    : "El evento de recogida vuelve a sumar puntos después de recargar scripts.");
                var textos = UnityEngine.Object.FindFirstObjectByType<ControladorGameplayHUD>().GetComponentsInChildren<TMP_Text>();
                Exigir(textos.First(t => t.name == "BottleCounter").text.StartsWith(esperadas.ToString("00")), "El HUD actualiza las botellas recogidas.");
                Exigir(textos.First(t => t.name == "Score").text == "PUNTOS " + puntosEsperados.ToString("0000"), "El HUD actualiza los puntos.");
                var controlador = jugador.GetComponent<CharacterController>();
                controlador.enabled = false;
                jugador.transform.position = JsonUtility.FromJson<Vector3>(SessionState.GetString(Clave + "PosicionJugador", "{}"));
                controlador.enabled = true;
                jugador.GetComponent<StarterAssets.ThirdPersonController>().enabled = true;
                if (paso == 4)
                {
                    CambiarPaso(5);
                    // Reproduce el fallo real: recargar ensamblados con la partida en curso.
                    EditorUtility.RequestScriptReload();
                }
                else
                {
                    CambiarPaso(8);
                    var resultado = new GameObject("ResultadoPrueba", typeof(RectTransform)).AddComponent<ControladorMenus>();
                    resultado.ConstruirResultado(false, "PRUEBA", false);
                    resultado.Cargar(ControladorMenus.EscenaMenu);
                }
            }
            else if (paso == 8 && SceneManager.GetActiveScene().name == ControladorMenus.EscenaMenu)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<ControladorMenus>();
                Exigir(menu != null, "El menú vuelve a construirse después de recompilar.");
                menu.GetComponentsInChildren<Button>().First(b => b.name == "Iniciar Juego").onClick.Invoke();
                CambiarPaso(9);
            }
            else if (paso == 9 && partida != null && SceneManager.GetActiveScene().name == ControladorMenus.EscenaJuego)
            {
                Exigir(partida.Recogidas == 0 && partida.Puntos == 0 && partida.Vida > .99f, "Nueva partida reinicia puntuación botellas y vida.");
                CambiarPaso(10);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception error)
        {
            informe += "ERROR: " + error + "\n";
            Guardar();
            Debug.LogException(error);
            EditorApplication.isPlaying = false;
        }
    }

    private static Transform Pierna(GameObject jugador) => jugador.GetComponentsInChildren<Transform>().First(t => t.name == "PIERNA_IZQ");

    private static void PrepararRecogida(GameObject jugador, ControladorPartidaBotellas partida)
    {
        var botella = UnityEngine.Object.FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsSortMode.None)
            .Where(b => !b.YaRecogido).OrderBy(b => Vector3.Distance(b.transform.position, jugador.transform.position)).First();
        var controlador = jugador.GetComponent<CharacterController>();
        SessionState.SetInt(Clave + "Objetivo", botella.GetInstanceID());
        SessionState.SetString(Clave + "PosicionJugador", JsonUtility.ToJson(jugador.transform.position));
        jugador.GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
        // Aísla el trigger de los coches, edificios y gravedad del mapa de prueba.
        botella.flotarEnElAire = false;
        botella.velocidadGiro = 0;
        botella.transform.position += new Vector3(0, 1000, 0) - botella.GetComponent<Collider>().bounds.center;
        Physics.SyncTransforms();
        var bounds = botella.GetComponent<Collider>().bounds;
        esperadas = partida.Recogidas + 1;
        puntosEsperados = partida.Puntos + Mathf.Max(0, botella.puntos);
        controlador.enabled = false;
        jugador.transform.position = bounds.center - jugador.transform.TransformVector(controlador.center)
            - Vector3.right * (bounds.extents.x + controlador.radius + .5f);
        controlador.enabled = true;
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception(mensaje);
        informe += "OK: " + mensaje + "\n";
        Guardar();
    }

    private static void CambiarPaso(int nuevo)
    {
        paso = nuevo;
        desde = EditorApplication.timeSinceStartup;
        Guardar();
    }

    private static void Guardar()
    {
        SessionState.SetInt(Clave + "Paso", paso);
        SessionState.SetFloat(Clave + "Desde", (float)desde);
        SessionState.SetString(Clave + "Informe", informe);
        SessionState.SetInt(Clave + "Botellas", esperadas);
        SessionState.SetInt(Clave + "Puntos", puntosEsperados);
        File.WriteAllText("Logs/gameplay-menu-en-curso.txt", "Paso " + paso + "\n" + informe);
    }
}
