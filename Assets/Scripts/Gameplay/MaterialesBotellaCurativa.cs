using UnityEngine;

/// <summary>Libera las copias de materiales usadas para teñir cada botella azul.</summary>
public sealed class MaterialesBotellaCurativa : MonoBehaviour
{
    private Material[] materiales;
    private void Awake()
    {
        var lista = new System.Collections.Generic.List<Material>();
        foreach (var renderer in GetComponentsInChildren<Renderer>()) lista.AddRange(renderer.sharedMaterials);
        materiales = lista.ToArray();
    }
    private void OnDestroy()
    {
        foreach (var material in materiales) if (material != null) Destroy(material);
    }
}
