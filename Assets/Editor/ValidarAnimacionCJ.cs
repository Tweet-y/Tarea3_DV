using System;
using System.Linq;
using System.Text;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ValidarAnimacionCJ
{
    [MenuItem("Tools/CJ/Verificar animaciones")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Deten Play antes de verificar las animaciones de CJ.");
            return;
        }
        var preview = new PreviewRenderUtility();
        var materials = new List<Material>();
        GameObject root = null;
        try
        {
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Personaje/cj/CJ.prefab"));
            var animation = root.GetComponentInChildren<CJLocomotionAnimation>();
            string modelReport = ValidarModeloCJ.Comprobar(root);
            animation.Initialize();
            var animator = animation.GetComponent<Animator>();
            if (!animator.avatar.isValid || !animator.avatar.isHuman)
                throw new System.InvalidOperationException("CJ necesita un avatar Humanoid valido.");
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            if (shader != null)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var old = renderer.sharedMaterials;
                    var replacements = new Material[old.Length];
                    for (var i = 0; i < old.Length; i++)
                    {
                        var m = new Material(shader);
                        if (old[i].HasProperty("_BaseMap"))
                        {
                            m.mainTexture = old[i].GetTexture("_BaseMap");
                            m.mainTextureScale = old[i].GetTextureScale("_BaseMap");
                            m.mainTextureOffset = old[i].GetTextureOffset("_BaseMap");
                        }
                        else m.mainTexture = old[i].mainTexture;
                        replacements[i] = m;
                        materials.Add(m);
                    }
                    renderer.sharedMaterials = replacements;
                }
            }
            preview.camera.transform.position = new Vector3(2.2f, 1.4f, 3.8f);
            preview.camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 30f;
            preview.camera.fieldOfView = 30f;
            Directory.CreateDirectory("Logs");
            var distances = new System.Text.StringBuilder();
            distances.Append(modelReport);
            foreach (var pose in new[] {
                (name: "idle", speed: 0f, phase: 0f, jump: 0f),
                (name: "walk", speed: 2f, phase: 0.8f, jump: 0f),
                (name: "walk2", speed: 2f, phase: 2.4f, jump: 0f),
                (name: "run", speed: 5.335f, phase: 0.8f, jump: 0f),
                (name: "run2", speed: 5.335f, phase: 2.4f, jump: 0f),
                (name: "run-extended", speed: 5.335f, phase: 19f * 2f * Mathf.PI / 30f, jump: 0f),
                (name: "jump-mid", speed: 0f, phase: 7f * 2f * Mathf.PI / 30f, jump: 1f),
                (name: "jump", speed: 0f, phase: 0f, jump: 1f) })
            {
                animation.ApplyPose(pose.speed, pose.phase, pose.jump);
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices)
                    {
                        if (float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || vertex.magnitude > 5f)
                            throw new System.InvalidOperationException("Deformacion invalida: " + skin.name);
                    }
                    distances.AppendLine(pose.name + " " + skin.name + " bounds=" + mesh.bounds);
                    Object.DestroyImmediate(mesh);
                }
                // Render a frozen copy so the preview scene cannot evaluate its Animator.
                var frozen = Object.Instantiate(root);
                var transforms = frozen.GetComponentsInChildren<Transform>();
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();
                Object.DestroyImmediate(frozen.GetComponentInChildren<CJLocomotionAnimation>());
                Object.DestroyImmediate(frozen.GetComponentInChildren<Animator>());
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].localPosition = positions[i];
                    transforms[i].localRotation = rotations[i];
                }
                preview.AddSingleGO(frozen);
                foreach (var view in new[] { (name: "front", position: new Vector3(2.2f, 1.4f, 3.8f)),
                    (name: "back", position: new Vector3(0f, 1.4f, -4.4f)),
                    (name: "side", position: new Vector3(4.4f, 1.4f, 0f)) })
                {
                    preview.camera.transform.position = view.position;
                    preview.camera.transform.LookAt(new Vector3(0f, 0.9f, 0f));
                    preview.BeginPreview(new Rect(0f, 0f, 600f, 600f), GUIStyle.none);
                    preview.Render(true);
                    var texture = (RenderTexture)preview.EndPreview();
                    var previous = RenderTexture.active;
                    RenderTexture.active = texture;
                    var image = new Texture2D(600, 600, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0f, 0f, 600f, 600f), 0, 0);
                    image.Apply();
                    File.WriteAllBytes("Logs/cj-humanoid-" + pose.name + "-" + view.name + ".png", image.EncodeToPNG());
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(image);
                    }
                Object.DestroyImmediate(frozen);
            }
            File.WriteAllText("Logs/cj-humanoid-verificado.txt", distances.ToString());
        }
        finally
        {
            preview.Cleanup();
            if (root != null) Object.DestroyImmediate(root);
            foreach (var m in materials) Object.DestroyImmediate(m);
        }
    }
}

public static class ValidarModeloCJ
{
    public static string Comprobar(GameObject root)
    {
        var visual = root.GetComponentInChildren<CJLocomotionAnimation>();
        var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>().ToDictionary(s => s.name);
        var report = new StringBuilder();
        var original = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Personaje/cj/CJ.fbx"));
        original.transform.position = new Vector3(0f, 1.5485042f, 2.9860067f);
        try
        {
            foreach (var renderer in original.GetComponentsInChildren<Renderer>())
            {
                Mesh reference;
                if (renderer is SkinnedMeshRenderer source)
                {
                    reference = new Mesh();
                    source.BakeMesh(reference);
                }
                else reference = Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                try
                {
                    var skin = skins[renderer.name];
                    var saved = skin.sharedMesh;
                    if (reference.vertexCount != saved.vertexCount || !reference.triangles.SequenceEqual(saved.triangles) || !reference.uv.SequenceEqual(saved.uv))
                        throw new InvalidOperationException("Se modifico la topologia o las UV originales de " + renderer.name);
                    var points = reference.vertices;
                    var actual = saved.vertices;
                    var baked = new Mesh();
                    skin.BakeMesh(baked);
                    var rendered = baked.vertices;
                    Object.DestroyImmediate(baked);
                    float error = 0f;
                    for (int v = 0; v < points.Length; v++)
                    {
                        error = Mathf.Max(error, Vector3.Distance(renderer.transform.TransformPoint(points[v]), actual[v]));
                        error = Mathf.Max(error, Vector3.Distance(actual[v], visual.transform.InverseTransformPoint(skin.transform.TransformPoint(rendered[v]))));
                    }
                    if (error > 0.00001f) throw new InvalidOperationException("Se alteraron las proporciones originales de " + renderer.name);
                    report.AppendLine(renderer.name + ": geometria y UV originales; error=" + error + " m");
                }
                finally { Object.DestroyImmediate(reference); }
            }
        }
        finally { Object.DestroyImmediate(original); }

        // Match the original touching surfaces, then follow those same vertices.
        // This catches detached parts even when each individual mesh has valid bounds.
        var seams = new List<(string a, int va, string b, int vb)>();
        foreach (var parts in new[] { ("CABEZA", "TORSO"), ("MANOS", "TORSO"), ("PIES", "PIERNAS") })
        {
            var a = skins[parts.Item1].sharedMesh.vertices;
            var b = skins[parts.Item2].sharedMesh.vertices;
            for (int i = 0; i < a.Length; i++)
            {
                float distance = float.MaxValue;
                int nearest = 0;
                for (int j = 0; j < b.Length; j++)
                {
                    float d = (a[i] - b[j]).sqrMagnitude;
                    if (d < distance) { distance = d; nearest = j; }
                }
                if (distance < 0.025f * 0.025f) seams.Add((parts.Item1, i, parts.Item2, nearest));
            }
            if (!seams.Any(s => s.a == parts.Item1)) throw new InvalidOperationException("Falta la union original de " + parts.Item1);
        }
        visual.Initialize();
        var animator = visual.GetComponent<Animator>();
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var joints = hips.GetComponentsInChildren<Transform>();
        var positions = joints.Select(t => t.localPosition).ToArray();
        var scales = joints.Select(t => t.localScale).ToArray();
        var maxSeams = new Dictionary<string, float>();
        var maxStretch = skins.Keys.ToDictionary(name => name, _ => 1f);
        var edges = new Dictionary<string, List<(int a, int b, float length)>>();
        foreach (var skin in skins.Values)
        {
            var mesh = skin.sharedMesh;
            var points = mesh.vertices;
            var indices = mesh.triangles;
            var unique = new HashSet<(int, int)>();
            var list = new List<(int, int, float)>();
            for (int t = 0; t < indices.Length; t += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = indices[t + e], b = indices[t + (e + 1) % 3];
                if (a > b) (a, b) = (b, a);
                float length = Vector3.Distance(points[a], points[b]);
                if (length >= 0.01f && unique.Add((a, b))) list.Add((a, b, length));
            }
            edges[skin.name] = list;
        }
        int poses = 0;
        foreach (float speed in new[] { 0f, 0.5f, 2f, 5.335f, -1f })
        {
            for (int phase = 0; phase < 30; phase++)
            {
                visual.ApplyPose(Mathf.Max(0f, speed), phase * 2f * Mathf.PI / 30f, speed < 0f ? 1f : 0f);
                for (int j = 0; j < joints.Length; j++)
                {
                    if ((joints[j] != hips && Vector3.Distance(joints[j].localPosition, positions[j]) > 0.00001f) ||
                        Vector3.Distance(joints[j].localScale, scales[j]) > 0.00001f)
                        throw new InvalidOperationException("Cambia la longitud o escala de " + joints[j].name);
                }
                var vertices = new Dictionary<string, Vector3[]>();
                foreach (var skin in skins.Values)
                {
                    var baked = new Mesh();
                    skin.BakeMesh(baked);
                    vertices[skin.name] = baked.vertices.Select(v => visual.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
                    Object.DestroyImmediate(baked);
                    // Check joint faces as well as rigid segments: fixed bone
                    // lengths alone do not catch vertices attached to the wrong limb.
                    var posed = vertices[skin.name];
                    foreach (var edge in edges[skin.name])
                    {
                        float stretch = Vector3.Distance(posed[edge.a], posed[edge.b]) / edge.length;
                        maxStretch[skin.name] = Mathf.Max(maxStretch[skin.name], stretch);
                        if (stretch > 3f)
                            throw new InvalidOperationException("Superficie estirada: " + skin.name + " factor=" + stretch + " velocidad=" + speed + " fase=" + phase);
                    }
                }
                foreach (var seam in seams)
                {
                    float d = Vector3.Distance(vertices[seam.a][seam.va], vertices[seam.b][seam.vb]);
                    maxSeams[seam.a] = Mathf.Max(maxSeams.TryGetValue(seam.a, out float max) ? max : 0f, d);
                    if (d > 0.04f) throw new InvalidOperationException("Union separada: " + seam.a + " distancia=" + d + " velocidad=" + speed + " fase=" + phase);
                }
                poses++;
            }
        }
        report.AppendLine(poses + " poses: longitudes y escalas constantes.");
        foreach (var seam in maxSeams) report.AppendLine("Union " + seam.Key + ": distancia maxima=" + seam.Value + " m");
        foreach (var part in maxStretch) report.AppendLine("Superficie " + part.Key + ": factor maximo de estiramiento en articulaciones=" + part.Value);
        return report.ToString();
    }
}
