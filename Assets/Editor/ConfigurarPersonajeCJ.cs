using System.IO;
using UnityEditor;
using UnityEngine;

public static class ConfigurarPersonajeCJ
{
    const string PrefabPath = "Assets/Prefabs/Personaje/cj/CJ.prefab";
    const string VisualPath = "Assets/Prefabs/Personaje/cj/Humanoid/CJVisual.prefab";

    [MenuItem("Tools/CJ/Reconstruir rig humanoide")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Deten Play antes de reconstruir el rig de CJ.");
            return;
        }
        RecrearRigCJ.GenerarVisual();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var old = root.transform.Find("CJModel");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath), root.transform);
            visual.name = "CJModel";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/cj-setup.txt", "CJ convertido a Humanoid con clips de Starter Assets.\n");
        Debug.Log("CJ usa un rig Humanoid y animaciones de Starter Assets.");
    }
}
