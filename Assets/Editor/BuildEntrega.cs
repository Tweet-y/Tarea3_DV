using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Compilación de entrega y adaptación batch del verificador existente.</summary>
[InitializeOnLoad]
public static class BuildEntrega
{
    private const string ClaveMenus = "Entrega.VerificandoMenus";
    private const string ClavePausa = "Entrega.VerificandoPausa";
    private const string ClaveInforme = "Entrega.InformePendiente";

    static BuildEntrega()
    {
        if (SessionState.GetBool(ClaveMenus, false) || SessionState.GetBool(ClavePausa, false))
            EditorApplication.playModeStateChanged += FinalizarVerificacion;
        if (!string.IsNullOrEmpty(SessionState.GetString(ClaveInforme, "")))
            EditorApplication.update += LeerVerificacion;
    }

    [MenuItem("Tools/Entrega/Compilar macOS")]
    public static void Generar()
    {
        const string salida = "Compilaciones/Entrega-2026-10-09/Super Curao Man.app";
        try
        {
            if (Directory.Exists(salida)) throw new InvalidOperationException("Ya existe la entrega: " + salida);
            Directory.CreateDirectory(Path.GetDirectoryName(salida));
            var informe = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = salida,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            if (informe.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Compilación falló: " + informe.summary.result + ", errores: " + informe.summary.totalErrors);
            Debug.Log("Entrega compilada: " + salida + ", bytes: " + informe.summary.totalSize);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    public static void VerificarMenus()
    {
        Directory.CreateDirectory("Logs");
        EditorSceneManager.OpenScene("Assets/Scenes/MenuInicio.unity");
        File.Delete("Logs/menu-flujo-verificado.txt");
        SessionState.SetBool(ClaveMenus, true);
        EditorApplication.playModeStateChanged += FinalizarVerificacion;
        if (!Application.isBatchMode) EditorApplication.ExecuteMenuItem("Window/General/Game");
        ValidarMenus.ProbarFlujo();
    }

    public static void VerificarPausa()
    {
        Directory.CreateDirectory("Logs");
        EditorSceneManager.OpenScene("Assets/Scenes/MenuInicio.unity");
        File.Delete("Logs/pausa-verificada.txt");
        SessionState.SetBool(ClavePausa, true);
        EditorApplication.playModeStateChanged += FinalizarVerificacion;
        if (!Application.isBatchMode) EditorApplication.ExecuteMenuItem("Window/General/Game");
        ValidarPausa.Ejecutar();
    }

    private static void FinalizarVerificacion(PlayModeStateChange cambio)
    {
        if (cambio != PlayModeStateChange.EnteredEditMode) return;
        string archivo = SessionState.GetBool(ClavePausa, false)
            ? "Logs/pausa-verificada.txt" : "Logs/menu-flujo-verificado.txt";
        SessionState.SetString(ClaveInforme, archivo);
        SessionState.SetBool(ClaveMenus, false);
        SessionState.SetBool(ClavePausa, false);
        EditorApplication.playModeStateChanged -= FinalizarVerificacion;
        EditorApplication.update -= LeerVerificacion;
        EditorApplication.update += LeerVerificacion;
    }

    private static void LeerVerificacion()
    {
        string archivo = SessionState.GetString(ClaveInforme, "");
        EditorApplication.update -= LeerVerificacion;
        SessionState.SetString(ClaveInforme, "");
        string informe = File.Exists(archivo) ? File.ReadAllText(archivo) : "";
        bool valido = !informe.Contains("ERROR:") && informe.Contains("OK: Salir detuvo Play.");
        Debug.Log("Verificacion entrega: " + (valido ? "PASS" : "FAIL") + " archivo=" + archivo);
        var argumentos = Environment.GetCommandLineArgs();
        if (Application.isBatchMode || argumentos.Contains("BuildEntrega.VerificarMenus")
            || argumentos.Contains("BuildEntrega.VerificarPausa"))
            EditorApplication.Exit(valido ? 0 : 1);
    }
}
