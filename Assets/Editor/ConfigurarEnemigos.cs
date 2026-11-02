using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Prepara el rig de Nemesis, los prefabs y el NavMesh de MapaV1.</summary>
[InitializeOnLoad]
public static class ConfigurarEnemigos
{
    static string RutaRehacer => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "rehacer-nemesis.txt"));

    static ConfigurarEnemigos()
    {
        // Cada compilación vuelve a escuchar: el método se desuscribe al correr.
        EditorApplication.update += RehacerSiEstaPendiente;
    }

    static void RehacerSiEstaPendiente()
    {
        if (!File.Exists(RutaRehacer)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.update -= RehacerSiEstaPendiente;
        File.Delete(RutaRehacer);
        Ejecutar();
    }

    const string Fbx = "Assets/Prefabs/Personaje/nemesis/Nemesis.fbx";
    const string Biker = "Assets/Synty/PolygonCityCharacters/Prefabs/Character_Biker_01.prefab";
    const string PrefabNemesis = "Assets/Prefabs/Enemigos/Nemesis.prefab";
    const string PrefabTitan = "Assets/Prefabs/Enemigos/Titan.prefab";
    const string Escena = "Assets/Scenes/MapaV1.unity";
    const string NavMeshAsset = "Assets/Scenes/MapaV1/NavMesh-Ciudad.asset";
    const string NavMeshTitanAsset = "Assets/Scenes/MapaV1/NavMesh-Titan.asset";
    // Tipo de agente propio para el jefe grande, como JefeHumanoideGrande en la demo del curso.
    const string AgenteTitan = "Titan";
    const float RadioTitan = 3f;
    const float AlturaTitan = 8f;
    const float EscalaTitan = 20f;
    const string Controller = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
    const string Pisada = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Sfx/Player_Footstep_01.wav";
    const float AlturaNemesis = 2.6f;
    const string Texturas = "Assets/Prefabs/Personaje/nemesis/Texturas/";

    [System.Serializable] class MaterialGltf { public string nombre, color, normal, emision; public float[] factor; }
    [System.Serializable] class MaterialesGltf { public MaterialGltf[] materiales; }

    static readonly (string humano, string hueso)[] Huesos =
    {
        ("Hips", "Bip01_Pelvis"),
        ("Spine", "Bip01_Spine1"),
        ("Chest", "Bip01_Spine2"),
        ("UpperChest", "Bip01_Spine4"),
        ("Neck", "Bip01_Neck1"),
        ("Head", "Bip01_Head1"),
        ("LeftShoulder", "Bip01_L_Clavicle"),
        ("LeftUpperArm", "Bip01_L_UpperArm"),
        ("LeftLowerArm", "Bip01_L_Forearm"),
        ("LeftHand", "Bip01_L_Hand"),
        ("RightShoulder", "Bip01_R_Clavicle"),
        ("RightUpperArm", "Bip01_R_UpperArm"),
        ("RightLowerArm", "Bip01_R_Forearm"),
        ("RightHand", "Bip01_R_Hand"),
        ("LeftUpperLeg", "Bip01_L_Thigh"),
        ("LeftLowerLeg", "Bip01_L_Calf"),
        ("LeftFoot", "Bip01_L_Foot"),
        ("LeftToes", "Bip01_L_Toe0"),
        ("RightUpperLeg", "Bip01_R_Thigh"),
        ("RightLowerLeg", "Bip01_R_Calf"),
        ("RightFoot", "Bip01_R_Foot"),
        ("RightToes", "Bip01_R_Toe0"),
    };

    [MenuItem("Tools/Enemigos/Configurar")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.Exception("Detén Play antes de configurar enemigos.");
        if (!File.Exists(Fbx))
            throw new System.Exception("Falta " + Fbx + ". Corre Tools/convertir_nemesis.py con Blender.");

        ConfigurarHumanoide();
        int tipoTitan = TipoAgenteTitan();
        var escena = EditorSceneManager.OpenScene(Escena);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
        var pisada = AssetDatabase.LoadAssetAtPath<AudioClip>(Pisada);
        if (controller == null) throw new System.Exception("No está el controller de Starter Assets.");

        Directory.CreateDirectory("Assets/Prefabs/Enemigos");
        var nemesis = CrearNemesis(controller, pisada);
        var titan = CrearTitan(controller, pisada, tipoTitan);
        PrefabUtility.SaveAsPrefabAsset(nemesis, PrefabNemesis);
        PrefabUtility.SaveAsPrefabAsset(titan, PrefabTitan);
        Object.DestroyImmediate(nemesis);
        Object.DestroyImmediate(titan);

        QuitarInstanciasPrevias();
        IgnorarTriggersDelHorneado();
        var superficie = HornearNavMesh("NavMeshCiudad", 0, NavMeshAsset);
        HornearNavMesh("NavMeshTitan", tipoTitan, NavMeshTitanAsset);
        var jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador == null) throw new System.Exception("MapaV1 no tiene un objeto con tag Player.");

        var puntoNemesis = PuntoEnNavMesh(jugador.transform.position, 0, 8f, 40f, 30f, 55f);
        var instanciaNemesis = Instanciar(PrefabNemesis, puntoNemesis, jugador.transform);
        var puntoTitan = PuntoEnNavMesh(jugador.transform.position, tipoTitan, 60f, 120f, 80f, 170f);
        var instanciaTitan = Instanciar(PrefabTitan, puntoTitan, jugador.transform);

        EditorSceneManager.MarkSceneDirty(escena);
        int triangulos = NavMesh.CalculateTriangulation().indices.Length / 3;
        string informe = "Nemesis humanoid en " + puntoNemesis + "\nTitan (agente " + tipoTitan + ") en " + puntoTitan
            + "\nNavMesh triangulos=" + triangulos + " superficie=" + superficie.name + "\n";
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        AssetDatabase.ForceReserializeAssets(new[] { Escena, NavMeshAsset, NavMeshTitanAsset });
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/enemigos-setup.txt", informe);
        Debug.Log("Enemigos configurados. NavMesh triángulos: " + triangulos);
    }

    static void ConfigurarHumanoide()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        importer.globalScale = 1f;
        importer.optimizeBones = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.SaveAndReimport();

        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var descripcion = DescripcionHumana(modelo);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.autoGenerateAvatarMappingIfUnspecified = false;
        importer.humanDescription = descripcion;
        importer.SaveAndReimport();

        var avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault(a => a.isHuman);
        if (avatar == null || !avatar.isValid)
            throw new System.Exception("El avatar Humanoid de Nemesis no es válido. Revisa el mapeo Bip01.");
    }

    static HumanDescription DescripcionHumana(GameObject asset)
    {
        // El bind pose de Nemesis es una A-pose (brazos 45° abajo). Mecanim toma el esqueleto
        // de la descripción como T-pose: sin enderezar los brazos, las animaciones los bajan 45°
        // de más y atraviesan el torso. Mismo arreglo que RecrearRigCJ.
        var modelo = Object.Instantiate(asset);
        modelo.name = asset.name;
        try { return DescripcionEnTPose(modelo); }
        finally { Object.DestroyImmediate(modelo); }
    }

    static HumanDescription DescripcionEnTPose(GameObject modelo)
    {
        var porNombre = modelo.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
        var humanos = new List<HumanBone>();
        foreach (var (humano, hueso) in Huesos)
        {
            if (!porNombre.ContainsKey(hueso))
                throw new System.Exception("Falta el hueso " + hueso + " en Nemesis.fbx.");
            humanos.Add(new HumanBone
            {
                humanName = humano,
                boneName = hueso,
                limit = new HumanLimit { useDefaultValues = true }
            });
        }

        var pelvis = porNombre["Bip01_Pelvis"];
        foreach (var lado in new[] { "L", "R" })
        {
            var brazo = porNombre["Bip01_" + lado + "_UpperArm"];
            var antebrazo = porNombre["Bip01_" + lado + "_Forearm"];
            var mano = porNombre["Bip01_" + lado + "_Hand"];
            var afuera = Vector3.Project(brazo.position - pelvis.position, modelo.transform.right).normalized;
            Enderezar(brazo, antebrazo.position, afuera);
            Enderezar(antebrazo, mano.position, afuera);
        }

        var esqueleto = new List<SkeletonBone>();
        foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
        {
            esqueleto.Add(new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            });
        }

        return new HumanDescription
        {
            human = humanos.ToArray(),
            skeleton = esqueleto.ToArray(),
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
            feetSpacing = 0f,
            hasTranslationDoF = false
        };
    }

    static void Enderezar(Transform hueso, Vector3 hijo, Vector3 direccion)
    {
        hueso.rotation = Quaternion.FromToRotation(hijo - hueso.position, direccion) * hueso.rotation;
    }

    static GameObject CrearNemesis(RuntimeAnimatorController controller, AudioClip pisada)
    {
        // El Animator tiene que vivir en la raíz del esqueleto. Si el padre
        // tiene otra escala, el humanoid aplasta la malla.
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
        root.name = "Nemesis";
        NormalizarAltura(root, AlturaNemesis);
        ApoyarPies(root);
        var animator = root.GetComponent<Animator>();
        if (animator == null) animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        AplicarUrp(root);

        var acecho = root.AddComponent<EnemigoAcechador>();
        acecho.usarNavMesh = true;
        acecho.pisadaPesada = pisada;
        var agente = root.AddComponent<NavMeshAgent>();
        agente.speed = 2.2f;
        agente.angularSpeed = 240f;
        agente.acceleration = 16f;
        agente.stoppingDistance = 0.5f;
        agente.radius = 0.4f;
        agente.height = 2f;
        agente.baseOffset = 0f;
        agente.enabled = false;
        CuerpoFisico(root, 0.20f, 1.5f, 12f);
        return root;
    }

    static GameObject CrearTitan(RuntimeAnimatorController controller, AudioClip pisada, int tipoTitan)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Biker));
        root.name = "Titan";
        root.transform.localScale = Vector3.one * EscalaTitan;
        foreach (var colision in root.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(colision);
        var animator = root.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var acecho = root.AddComponent<EnemigoAcechador>();
        acecho.usarNavMesh = true;
        acecho.velocidadCaminar = 1.5f;
        acecho.velocidadCorrer = 3.2f;
        acecho.umbralCorrer = 40f;
        acecho.pisadaPesada = pisada;
        acecho.volumenPisada = 1f;
        var agente = root.AddComponent<NavMeshAgent>();
        agente.agentTypeID = tipoTitan;
        // Radio y altura del NavMeshAgent son locales: la escala ×20 los multiplica.
        agente.radius = RadioTitan / EscalaTitan;
        agente.height = AlturaTitan / EscalaTitan;
        agente.speed = 1.5f;
        agente.angularSpeed = 90f;
        agente.acceleration = 4f;
        agente.stoppingDistance = 2f;
        agente.baseOffset = 0f;
        agente.enabled = false;
        CuerpoFisico(root, 0.35f, 2f, 25f);
        return root;
    }

    static void CuerpoFisico(GameObject root, float danio, float enfriamiento, float empuje)
    {
        var cuerpo = root.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = false;
        var contacto = new GameObject("Contacto");
        contacto.transform.SetParent(root.transform, false);
        var capsula = contacto.AddComponent<CapsuleCollider>();
        capsula.isTrigger = true;
        EncajarCapsula(root, capsula);
        var trampa = contacto.AddComponent<TrampaDanio>();
        trampa.cantidadDanio = danio;
        trampa.tiempoEnfriamiento = enfriamiento;
        trampa.fuerzaEmpuje = empuje;
        trampa.causa = ControladorPartidaBotellas.Causa.Enemigo;
        trampa.repetirMientrasContacto = true;
        trampa.clipSonidoDanio = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Sfx/Player_Land.wav");
    }

    static void EncajarCapsula(GameObject root, CapsuleCollider capsula)
    {
        var mundo = BoundsDe(root);
        float escala = Mathf.Max(0.001f, root.transform.lossyScale.y);
        float escalaXZ = Mathf.Max(0.001f, root.transform.lossyScale.x);
        capsula.height = mundo.size.y / escala;
        capsula.radius = Mathf.Max(mundo.extents.x, mundo.extents.z) / escalaXZ * 0.55f;
        capsula.center = root.transform.InverseTransformPoint(mundo.center);
        capsula.direction = 1;
    }

    static void NormalizarAltura(GameObject root, float metros)
    {
        root.transform.localScale = Vector3.one;
        float altura = BoundsDe(root).size.y;
        if (altura < 0.01f) throw new System.Exception(root.name + " no tiene renderers.");
        float escala = metros / altura;
        if (escala < 0.5f || escala > 2f)
            throw new System.Exception("FBX con escala rota, rehacer con convertir_nemesis.py");
        root.transform.localScale = Vector3.one * escala;
    }

    static void ApoyarPies(GameObject root)
    {
        float delta = root.transform.position.y - BoundsDe(root).min.y;
        foreach (Transform hijo in root.transform)
            hijo.position += Vector3.up * delta;
    }

    static Bounds BoundsDe(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void AplicarUrp(GameObject root)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new System.Exception("No está el shader URP/Lit.");
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Texturas + "materiales.json");
        if (json == null) throw new System.Exception("Falta " + Texturas + "materiales.json. Corre Tools/extraer_texturas_nemesis.py.");
        var gltf = JsonUtility.FromJson<MaterialesGltf>(json.text).materiales.ToDictionary(m => m.nombre);

        Directory.CreateDirectory("Assets/Prefabs/Personaje/nemesis/Materiales");
        int sinTextura = 0;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            var materiales = renderer.sharedMaterials;
            for (int i = 0; i < materiales.Length; i++)
            {
                var origen = materiales[i];
                if (origen == null) continue;
                string ruta = "Assets/Prefabs/Personaje/nemesis/Materiales/" + origen.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (material == null)
                {
                    material = new Material(shader) { name = origen.name };
                    AssetDatabase.CreateAsset(material, ruta);
                }
                material.shader = shader;
                if (gltf.TryGetValue(origen.name, out var datos)) Texturizar(material, datos);
                else sinTextura++;
                EditorUtility.SetDirty(material);
                materiales[i] = material;
            }
            renderer.sharedMaterials = materiales;
        }
        if (sinTextura > 0) Debug.LogWarning(sinTextura + " materiales de Nemesis sin entrada en materiales.json.");
    }

    static void Texturizar(Material material, MaterialGltf datos)
    {
        var f = datos.factor != null && datos.factor.Length == 4 ? datos.factor : new[] { 1f, 1f, 1f, 1f };
        material.SetColor("_BaseColor", new Color(f[0], f[1], f[2], f[3]));
        material.SetTexture("_BaseMap", Textura(datos.color, false));
        var normal = Textura(datos.normal, true);
        material.SetTexture("_BumpMap", normal);
        if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
        var emision = Textura(datos.emision, false);
        material.SetTexture("_EmissionMap", emision);
        material.SetColor("_EmissionColor", emision != null ? Color.white : Color.black);
        if (emision != null) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
    }

    static Texture2D Textura(string archivo, bool esNormal)
    {
        if (string.IsNullOrEmpty(archivo)) return null;
        string ruta = Texturas + archivo;
        var importer = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (importer == null) throw new System.Exception("Falta la textura " + ruta + ".");
        var tipo = esNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != tipo)
        {
            importer.textureType = tipo;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    static int TipoAgenteTitan()
    {
        int indice = -1;
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            if (NavMesh.GetSettingsNameFromID(NavMesh.GetSettingsByIndex(i).agentTypeID) == AgenteTitan) indice = i;
        if (indice < 0)
        {
            // Mismo flujo que el botón + de Navigation > Agents.
            NavMesh.CreateSettings();
            indice = NavMesh.GetSettingsCount() - 1;
        }

        var ajustes = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));
        var nombres = ajustes.FindProperty("m_SettingNames");
        if (nombres.arraySize <= indice) nombres.arraySize = indice + 1;
        nombres.GetArrayElementAtIndex(indice).stringValue = AgenteTitan;
        var agente = ajustes.FindProperty("m_Settings").GetArrayElementAtIndex(indice);
        agente.FindPropertyRelative("agentRadius").floatValue = RadioTitan;
        agente.FindPropertyRelative("agentHeight").floatValue = AlturaTitan;
        agente.FindPropertyRelative("agentSlope").floatValue = 45f;
        agente.FindPropertyRelative("agentClimb").floatValue = 2f;
        agente.FindPropertyRelative("minRegionArea").floatValue = 20f;
        ajustes.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        int id = agente.FindPropertyRelative("agentTypeID").intValue;
        if (NavMesh.GetSettingsNameFromID(id) != AgenteTitan)
            throw new System.Exception("No se pudo registrar el tipo de agente " + AgenteTitan + ".");
        return id;
    }

    static NavMeshSurface HornearNavMesh(string nombre, int agentTypeID, string ruta)
    {
        var objeto = GameObject.Find(nombre);
        var superficie = objeto != null ? objeto.GetComponent<NavMeshSurface>() : new GameObject(nombre).AddComponent<NavMeshSurface>();
        superficie.agentTypeID = agentTypeID;
        superficie.collectObjects = CollectObjects.All;
        superficie.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        superficie.navMeshData = null;
        superficie.BuildNavMesh();
        if (NavMesh.CalculateTriangulation().vertices.Length == 0)
            throw new System.Exception("El NavMesh quedó vacío. Las calles no aportaron colliders.");
        GuardarNavMesh(superficie, ruta);
        return superficie;
    }

    static void IgnorarTriggersDelHorneado()
    {
        foreach (var colision in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!colision.isTrigger && colision.GetComponent<AguaLetal>() == null) continue;
            IgnorarDelNavMesh(colision.gameObject);
        }
        foreach (var agua in Object.FindObjectsByType<AguaLetal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            IgnorarDelNavMesh(agua.gameObject);
    }

    static void IgnorarDelNavMesh(GameObject objetivo)
    {
        var modificador = objetivo.GetComponent<NavMeshModifier>();
        if (modificador == null) modificador = objetivo.AddComponent<NavMeshModifier>();
        modificador.ignoreFromBuild = true;
    }

    static void GuardarNavMesh(NavMeshSurface superficie, string ruta)
    {
        Directory.CreateDirectory("Assets/Scenes/MapaV1");
        var datos = superficie.navMeshData;
        if (datos == null) throw new System.Exception("BuildNavMesh no produjo NavMeshData.");
        var previo = AssetDatabase.LoadAssetAtPath<NavMeshData>(ruta);
        if (previo != null && previo != datos)
            AssetDatabase.DeleteAsset(ruta);
        if (AssetDatabase.GetAssetPath(datos) != ruta)
            AssetDatabase.CreateAsset(datos, ruta);
        superficie.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(ruta);
    }

    static GameObject Instanciar(string prefab, Vector3 posicion, Transform objetivo)
    {
        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
        instancia.transform.position = posicion;
        // Objetivo asignado en la escena, como en la demo del curso.
        instancia.GetComponent<EnemigoAcechador>().objetivo = objetivo;
        return instancia;
    }

    static void QuitarInstanciasPrevias()
    {
        foreach (var nombre in new[] { "Nemesis", "Titan" })
        {
            var existente = GameObject.Find(nombre);
            if (existente != null) Object.DestroyImmediate(existente);
        }
    }

    static Vector3 PuntoEnNavMesh(Vector3 origen, int agentTypeID, float busqueda, float distancia, float minimo, float maximo)
    {
        var filtro = new NavMeshQueryFilter { agentTypeID = agentTypeID, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(origen, out var suelo, busqueda, filtro))
            throw new System.Exception("El jugador no está cerca del NavMesh del agente " + agentTypeID + ".");
        for (int i = 0; i < 36; i++)
        {
            float angulo = i * Mathf.PI * 2f / 36f;
            var candidato = suelo.position + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * distancia;
            if (!NavMesh.SamplePosition(candidato, out var hit, 20f, filtro)) continue;
            float separacion = Vector3.Distance(Plano(hit.position), Plano(suelo.position));
            if (separacion < minimo || separacion > maximo) continue;
            var camino = new NavMeshPath();
            if (NavMesh.CalculatePath(hit.position, suelo.position, filtro, camino)
                && camino.status == NavMeshPathStatus.PathComplete)
                return hit.position;
        }
        throw new System.Exception("No hay punto del NavMesh (agente " + agentTypeID + ") a ~" + distancia + " m del jugador con camino.");
    }

    static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
