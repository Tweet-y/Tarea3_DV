using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

/// <summary>Capas, NavMesh, zombies, cajas, botellas y jefe de MapaV2.</summary>
[InitializeOnLoad]
public static class ConfigurarIsla
{
    const string Escena = "Assets/Scenes/MapaV2.unity";
    const string NavMeshRuta = "Assets/Scenes/MapaV2/NavMesh-Isla.asset";
    const string Controlador = "Assets/Animaciones/Personajes/EnemigoCombate.controller";
    const string MezcladorRuta = "Assets/Resources/Audio/MezcladorPrincipal.mixer";
    const int Semilla = 20261108;

    static string RutaDisparo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "configurar-isla.txt"));

    static ConfigurarIsla() => EditorApplication.update += DispararSiEstaPendiente;

    static void DispararSiEstaPendiente()
    {
        if (!File.Exists(RutaDisparo)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EditorApplication.update -= DispararSiEstaPendiente;
        File.Delete(RutaDisparo);
        Ejecutar();
    }

    [MenuItem("Tools/Isla/Configurar")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Cierra Play antes de configurar la isla.");
        Directory.CreateDirectory("Logs");
        AsegurarCapas();
        CrearMezclador();
        PrepararClipsCombate();
        AvisarClipsFaltantes();
        var idle = Clip("Orc Idle", "Assets/Animaciones/Personajes/Mob/Characters@Orc Idle F.fbx") ?? Clip("Idle", "Assets/Animaciones/Personajes/Mob/Orco_Idle.anim");
        var correr = Clip("Zombie Run", "Assets/Animaciones/Personajes/Mob/Characters@Zombie Run.fbx") ?? Clip("Run", "Assets/Animaciones/Personajes/Mob/Orco_Run.anim");
        var ataqueZombie = BuscarCombate("Zombie Attack");
        var muerteZombie = BuscarCombate("Zombie Dying");
        var ataqueNemesis = BuscarCombate("Mutant Swiping");
        var muerteNemesis = BuscarCombate("Mutant Dying");
        // Respaldo: clips Humanoid de Kevin Iglesias (Basic Motions + Melee Warrior) si faltan los de Mixamo.
        var atacarZ = ataqueZombie ?? Paquete("Attack01", "Melee Warrior Animations/Animations/OneHanded/RightHand/RightHand@Attack01.fbx") ?? correr;
        var golpeZ = BuscarCombate("Zombie Reaction Hit") ?? Paquete("TakeDamage01", "Melee Warrior Animations/Animations/OneHanded/1H@TakeDamage01.fbx") ?? idle;
        var morirZ = muerteZombie ?? Paquete("Death01_A", "Melee Warrior Animations/Animations/MeleeWarrior@Death01.fbx") ?? idle;
        var idleZ = BuscarCombate("Zombie Idle") ?? Paquete("Idle01", "Basic Motions/Animations/Idles/BasicMotions@Idle01.fbx") ?? idle;
        var andarZ = BuscarCombate("Zombie Walk") ?? Paquete("Walk01 - Forwards", "Basic Motions/Animations/Movement/BasicMotions@Walk01.fbx") ?? correr;
        var correrZ = Paquete("Run01 - Forwards", "Basic Motions/Animations/Movement/BasicMotions@Run01.fbx") ?? andarZ;
        var controlador = CrearControladorEnemigo(idleZ, andarZ, correrZ, atacarZ, golpeZ, morirZ);
        var overrideZ = Override(controlador, "Assets/Animaciones/Personajes/EnemigoCombate_Zombie.overrideController");
        // Solo se reemplaza el clip propio del ataque o de la muerte. Si falta el del zombie,
        // ese estado comparte el de caminar o el de reposo y no se puede pisar sin romper la locomoción.
        var overrideN = Override(controlador, "Assets/Animaciones/Personajes/EnemigoCombate_Nemesis.overrideController", ataqueZombie, ataqueNemesis, muerteZombie, muerteNemesis);
        AgregarCapaJugador(
            BuscarCombate("Standing Melee Attack Horizontal") ?? Paquete("Attack02", "Melee Warrior Animations/Animations/OneHanded/RightHand/RightHand@Attack02.fbx"),
            BuscarCombate("Hit Reaction") ?? Paquete("TakeDamage02", "Melee Warrior Animations/Animations/OneHanded/1H@TakeDamage02.fbx"),
            BuscarCombate("Dying") ?? Paquete("Death02_A", "Melee Warrior Animations/Animations/MeleeWarrior@Death02.fbx"));
        EditorSceneManager.OpenScene(Escena);
        string[] origenes =
        {
            "Assets/PolygonZombies/Prefabs/Zombie_Hobo_Male_01.prefab",
            "Assets/PolygonZombies/Prefabs/Zombie_Roadworker_Male_01.prefab",
            "Assets/PolygonZombies/Prefabs/Zombie_Paramedic_Female_01.prefab",
            "Assets/PolygonZombies/Prefabs/Zombie_Jock_Male_01.prefab",
            "Assets/PolygonZombies/Prefabs/Zombie_Businessman_Male_01.prefab",
            "Assets/PolygonZombies/Prefabs/Zombie_Punk_Male_01.prefab",
            "Assets/PolygonZombies/Prefabs/Zombie_Police_Male_01.prefab",
            "Assets/PolygonZombies/Prefabs/Zombie_Hoodie_Male_01.prefab"
        };
        var prefabsZ = new GameObject[origenes.Length];
        for (int i = 0; i < origenes.Length; i++)
            prefabsZ[i] = CrearZombie(Path.GetFileNameWithoutExtension(origenes[i]), origenes[i], overrideZ);
        var jefe = CrearJefe(overrideN);
        IgnorarAgua();
        var superficie = Hornear();
        var jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador == null) throw new Exception("MapaV2 no tiene un objeto con tag Player.");
        PonerCapa(jugador, CapasJuego.Jugador);
        var cj = GameObject.Find("CJ");
        if (cj != null) PonerCapa(cj, CapasJuego.Aliados);
        QuitarAnterior();
        var raiz = new GameObject("ContenidoIsla");
        var puntos = PuntosNavMesh(400);
        if (puntos.Count < 40) throw new Exception("El NavMesh de la isla no tiene puntos suficientes (" + puntos.Count + ").");
        Vector3 spawn = jugador.transform.position;
        Vector3 lejos = MasLejos(puntos, spawn);
        Vector3 extraccion = MasLejos(puntos, spawn, lejos, 18f);
        Vector3 arena = MasLejos(puntos, extraccion, spawn, 12f);
        ColocarBotellas(raiz.transform, puntos, spawn, 29);
        ColocarCajas(raiz.transform, puntos, spawn);
        ColocarZombies(raiz.transform, prefabsZ, puntos, spawn);
        var jefeGo = (GameObject)PrefabUtility.InstantiatePrefab(jefe);
        jefeGo.name = "NemesisJefe";
        jefeGo.transform.SetParent(raiz.transform, true);
        jefeGo.transform.position = arena;
        var acecho = jefeGo.GetComponent<EnemigoAcechador>();
        acecho.objetivo = jugador.transform;
        var zona = new GameObject("ZonaJefe");
        zona.transform.SetParent(raiz.transform, false);
        zona.transform.position = arena;
        var caja = zona.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(16f, 4f, 16f);
        caja.center = new Vector3(0f, 2f, 0f);
        var cine = zona.AddComponent<CinematicaJefe>();
        cine.jefe = jefeGo.transform;
        cine.grito = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Profe/Debuff 1.wav");
        var isla = new GameObject("Isla");
        var banco = isla.AddComponent<BancoSonidos>();
        banco.swoosh = ClipAudio("Sword Swoosh 1.wav");
        banco.impacto = ClipAudio("Magical Impact 1.wav");
        banco.explosion = ClipAudio("Explosion 1.wav");
        banco.buff = ClipAudio("Buff 1.wav");
        banco.curar = banco.buff;
        banco.beber = banco.buff;
        banco.golpeJugador = banco.impacto;
        banco.muerteJugador = banco.explosion;
        banco.muerteEnemigo = banco.impacto;
        banco.grito = ClipAudio("Debuff 1.wav");
        var final = isla.AddComponent<FinalIsla>();
        var punto = new GameObject("PuntoExtraccion").transform;
        punto.SetParent(raiz.transform, false);
        punto.position = extraccion;
        punto.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(spawn - extraccion, Vector3.up).normalized, Vector3.up);
        final.puntoExtraccion = punto;
        final.lancha = Buscar("SM_Veh_Boat_01 (1)");
        final.cj = cj != null ? cj.transform : null;
        var fuegosPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Fireworks_Group_01.prefab");
        if (fuegosPrefab != null)
        {
            var fuegos = (GameObject)PrefabUtility.InstantiatePrefab(fuegosPrefab);
            fuegos.name = "FuegosExtraccion";
            fuegos.transform.SetParent(raiz.transform, true);
            fuegos.transform.position = extraccion + Vector3.up * 2f;
            fuegos.SetActive(false);
            final.fuegos = fuegos;
        }
        var subir = new GameObject("SubirAlBote");
        subir.transform.SetParent(punto, false);
        var trigger = subir.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(5f, 3f, 6f);
        trigger.center = new Vector3(0f, 1.2f, 0f);
        subir.AddComponent<DisparadorBote>().final = final;
        EquiparBate(jugador, banco.swoosh);
        foreach (var enemigo in UnityEngine.Object.FindObjectsByType<EnemigoAcechador>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (enemigo.GetComponent<ControladorVida>() == null) continue;
            enemigo.GetComponent<ControladorVida>().sonidoGolpe = banco.impacto;
            enemigo.GetComponent<ControladorVida>().sonidoMuerte = banco.muerteEnemigo;
            enemigo.sonidoAtaque = banco.swoosh;
            var melee = enemigo.GetComponentInChildren<GolpeMelee>();
            if (melee != null) melee.sonidoImpacto = banco.impacto;
        }
        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        AssetDatabase.ForceReserializeAssets(new[] { Escena, NavMeshRuta });
        if (superficie.navMeshData == null) throw new Exception("La superficie quedó sin NavMeshData.");
        Debug.Log("Isla configurada");
    }

    static void AsegurarCapas()
    {
        foreach (var nombre in new[] { CapasJuego.Jugador, CapasJuego.Aliados, CapasJuego.Enemigos, CapasJuego.ArmaJugador, CapasJuego.ArmaEnemigo, CapasJuego.Objetos, CapasJuego.Recogibles })
        {
            if (LayerMask.NameToLayer(nombre) >= 0) continue;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = nombre;
                tags.ApplyModifiedProperties();
                break;
            }
        }
    }

    static void CrearMezclador()
    {
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MezcladorRuta) != null) return;
        Directory.CreateDirectory("Assets/Resources/Audio");
        var tipo = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor.CoreModule");
        if (tipo == null)
        {
            foreach (var ensamblado in AppDomain.CurrentDomain.GetAssemblies())
            {
                tipo = ensamblado.GetType("UnityEditor.Audio.AudioMixerController");
                if (tipo != null) break;
            }
        }
        if (tipo == null) throw new Exception("No está AudioMixerController.");
        var crear = tipo.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var mixer = (AudioMixer)crear.Invoke(null, new object[] { MezcladorRuta });
        var master = mixer.FindMatchingGroups("Master")[0];
        var nuevo = tipo.GetMethod("CreateNewGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var anidar = tipo.GetMethod("AddChildToParent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var nombre in new[] { "Musica", "Efectos", "Voces" })
        {
            var grupo = nuevo.Invoke(mixer, new object[] { nombre, false });
            anidar.Invoke(mixer, new object[] { grupo, master });
        }
        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        if (MezcladorAudio.Grupo("Efectos") == null && mixer.FindMatchingGroups("Efectos").Length == 0)
            throw new Exception("El mezclador no creó el grupo Efectos.");
    }

    static AnimatorController CrearControladorEnemigo(AnimationClip idle, AnimationClip andar, AnimationClip correr, AnimationClip atacar, AnimationClip golpe, AnimationClip morir)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador) != null)
            AssetDatabase.DeleteAsset(Controlador);
        var controlador = AnimatorController.CreateAnimatorControllerAtPath(Controlador);
        controlador.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controlador.AddParameter("Atacar", AnimatorControllerParameterType.Trigger);
        controlador.AddParameter("Golpe", AnimatorControllerParameterType.Trigger);
        controlador.AddParameter("Morir", AnimatorControllerParameterType.Trigger);
        var maquina = controlador.layers[0].stateMachine;
        var sIdle = maquina.AddState("Idle");
        var sMover = maquina.AddState("Mover");
        var sAtacar = maquina.AddState("Atacar");
        var sGolpe = maquina.AddState("Golpe");
        var sMorir = maquina.AddState("Morir");
        sIdle.motion = idle;
        // Caminar y correr se mezclan por Speed (m/s del agente) para que los pies no patinen.
        var arbol = new BlendTree { name = "Caminar y correr", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(arbol, controlador);
        arbol.AddChild(andar, 1.8f);
        arbol.AddChild(correr, 4.5f);
        sMover.motion = arbol;
        sAtacar.motion = atacar;
        sGolpe.motion = golpe;
        sMorir.motion = morir;
        maquina.defaultState = sIdle;
        var aMover = sIdle.AddTransition(sMover);
        aMover.hasExitTime = false;
        aMover.duration = .1f;
        aMover.AddCondition(AnimatorConditionMode.Greater, .1f, "Speed");
        var aIdle = sMover.AddTransition(sIdle);
        aIdle.hasExitTime = false;
        aIdle.duration = .1f;
        aIdle.AddCondition(AnimatorConditionMode.Less, .1f, "Speed");
        Inmediata(maquina.AddAnyStateTransition(sAtacar), "Atacar");
        Inmediata(maquina.AddAnyStateTransition(sGolpe), "Golpe");
        Inmediata(maquina.AddAnyStateTransition(sMorir), "Morir");
        var volver = sAtacar.AddTransition(sIdle);
        volver.hasExitTime = true;
        volver.exitTime = .85f;
        volver.duration = .1f;
        var volverGolpe = sGolpe.AddTransition(sIdle);
        volverGolpe.hasExitTime = true;
        volverGolpe.exitTime = .8f;
        volverGolpe.duration = .1f;
        EditorUtility.SetDirty(controlador);
        return controlador;
    }

    static void Inmediata(AnimatorStateTransition transicion, string trigger)
    {
        transicion.hasExitTime = false;
        transicion.duration = 0f;
        transicion.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static AnimatorOverrideController Override(AnimatorController baseCtrl, string ruta, AnimationClip desdeA = null, AnimationClip haciaA = null, AnimationClip desdeM = null, AnimationClip haciaM = null)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ruta) != null)
            AssetDatabase.DeleteAsset(ruta);
        var over = new AnimatorOverrideController(baseCtrl);
        if (desdeA != null && haciaA != null) over[desdeA] = haciaA;
        if (desdeM != null && haciaM != null) over[desdeM] = haciaM;
        AssetDatabase.CreateAsset(over, ruta);
        return over;
    }

    static void AgregarCapaJugador(AnimationClip atacar, AnimationClip golpe, AnimationClip morir)
    {
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animaciones/Personajes/CuraoPrincipalLocomotion.controller");
        if (controlador == null) throw new Exception("Falta CuraoPrincipalLocomotion.");
        if (controlador.parameters.All(p => p.name != "Atacar")) controlador.AddParameter("Atacar", AnimatorControllerParameterType.Trigger);
        if (controlador.parameters.All(p => p.name != "Golpe")) controlador.AddParameter("Golpe", AnimatorControllerParameterType.Trigger);
        if (controlador.parameters.All(p => p.name != "Morir")) controlador.AddParameter("Morir", AnimatorControllerParameterType.Trigger);
        if (controlador.layers.Any(l => l.name == "Combate"))
        {
            AsignarClipsCombate(controlador, atacar, golpe, morir);
            return;
        }
        controlador.AddLayer("Combate");
        var capas = controlador.layers;
        int indice = capas.Length - 1;
        var maquina = capas[indice].stateMachine;
        var quieto = maquina.AddState("Quieto");
        var sAtacar = maquina.AddState("Atacar");
        var sGolpe = maquina.AddState("Golpe");
        var sMorir = maquina.AddState("Morir");
        maquina.defaultState = quieto;
        sAtacar.motion = atacar;
        sGolpe.motion = golpe;
        sMorir.motion = morir;
        Inmediata(maquina.AddAnyStateTransition(sAtacar), "Atacar");
        Inmediata(maquina.AddAnyStateTransition(sGolpe), "Golpe");
        Inmediata(maquina.AddAnyStateTransition(sMorir), "Morir");
        var vuelve = sAtacar.AddTransition(quieto);
        vuelve.hasExitTime = true;
        vuelve.exitTime = .9f;
        vuelve.duration = .05f;
        var vuelveG = sGolpe.AddTransition(quieto);
        vuelveG.hasExitTime = true;
        vuelveG.exitTime = .8f;
        vuelveG.duration = .05f;
        capas[indice].defaultWeight = 0f;
        capas[indice].blendingMode = AnimatorLayerBlendingMode.Override;
        controlador.layers = capas;
        EditorUtility.SetDirty(controlador);
    }

    static void AsignarClipsCombate(AnimatorController controlador, AnimationClip atacar, AnimationClip golpe, AnimationClip morir)
    {
        foreach (var capa in controlador.layers)
        {
            if (capa.name != "Combate" || capa.stateMachine == null) continue;
            foreach (var hijo in capa.stateMachine.states)
            {
                if (hijo.state == null) continue;
                if (hijo.state.name == "Atacar" && atacar != null) hijo.state.motion = atacar;
                else if (hijo.state.name == "Golpe" && golpe != null) hijo.state.motion = golpe;
                else if (hijo.state.name == "Morir" && morir != null) hijo.state.motion = morir;
            }
        }
        EditorUtility.SetDirty(controlador);
    }

    static GameObject CrearZombie(string nombre, string origen, RuntimeAnimatorController controlador)
    {
        Directory.CreateDirectory("Assets/Prefabs/Enemigos");
        var fuente = AssetDatabase.LoadAssetAtPath<GameObject>(origen);
        if (fuente == null) throw new Exception("Falta el personaje " + origen);
        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(fuente);
        if (PrefabUtility.IsPartOfPrefabInstance(instancia))
            PrefabUtility.UnpackPrefabInstance(instancia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instancia.name = nombre;
        var capsulaPersonaje = instancia.GetComponent<CharacterController>();
        if (capsulaPersonaje != null) UnityEngine.Object.DestroyImmediate(capsulaPersonaje);
        var agente = Obtener<NavMeshAgent>(instancia);
        agente.radius = .35f;
        agente.height = 1.8f;
        agente.speed = 1.8f;
        agente.angularSpeed = 360f;
        agente.acceleration = 10f;
        agente.stoppingDistance = 1.35f;
        var acecho = Obtener<EnemigoAcechador>(instancia);
        acecho.radioDeteccion = 12f;
        acecho.rangoAtaque = 1.7f;
        acecho.retrasoAparicion = 0f;
        acecho.velocidadCaminar = 1.8f;
        acecho.velocidadCorrer = 3.6f;
        acecho.umbralCorrer = 6f;
        acecho.usarNavMesh = true;
        var vida = Obtener<ControladorVida>(instancia);
        vida.vidaMaxima = 60f;
        vida.segundosDestruir = 4f;
        var cuerpo = Obtener<CapsuleCollider>(instancia);
        cuerpo.height = 1.8f;
        cuerpo.center = new Vector3(0f, .9f, 0f);
        cuerpo.radius = .35f;
        cuerpo.isTrigger = false;
        var mano = new GameObject("Golpe");
        mano.transform.SetParent(instancia.transform, false);
        mano.transform.localPosition = new Vector3(0f, 1.1f, .45f);
        var caja = mano.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(.9f, .7f, .9f);
        var golpe = mano.AddComponent<GolpeMelee>();
        golpe.danio = 20f;
        golpe.danioJugador = .12f;
        acecho.golpe = golpe;
        var anim = Obtener<Animator>(instancia);
        anim.runtimeAnimatorController = controlador;
        anim.applyRootMotion = false;
        PonerCapa(instancia, CapasJuego.Enemigos);
        int arma = LayerMask.NameToLayer(CapasJuego.ArmaEnemigo);
        if (arma >= 0) mano.layer = arma;
        string ruta = "Assets/Prefabs/Enemigos/" + nombre + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(instancia, ruta);
        UnityEngine.Object.DestroyImmediate(instancia);
        return prefab;
    }

    static T Obtener<T>(GameObject objeto) where T : Component
    {
        var componente = objeto.GetComponent<T>();
        if (componente == null) componente = objeto.AddComponent<T>();
        return componente;
    }

    static GameObject CrearJefe(RuntimeAnimatorController controlador)
    {
        var nemesis = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemigos/Nemesis.prefab");
        if (nemesis == null) throw new Exception("Falta Nemesis.prefab.");
        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(nemesis);
        foreach (var trampa in instancia.GetComponentsInChildren<TrampaDanio>(true))
            UnityEngine.Object.DestroyImmediate(trampa);
        var acecho = instancia.GetComponent<EnemigoAcechador>();
        acecho.radioDeteccion = 20f;
        acecho.rangoAtaque = 2.4f;
        acecho.cadenciaAtaque = 1.6f;
        acecho.retrasoAparicion = 0f;
        acecho.velocidadCaminar = 2.4f;
        acecho.velocidadCorrer = 4.2f;
        var vida = instancia.GetComponent<ControladorVida>() ?? instancia.AddComponent<ControladorVida>();
        vida.vidaMaxima = 600f;
        vida.esJefe = true;
        vida.segundosDestruir = 5f;
        var anim = instancia.GetComponent<Animator>();
        anim.runtimeAnimatorController = controlador;
        var mano = anim.GetBoneTransform(HumanBodyBones.RightHand);
        var golpeGo = new GameObject("Golpe");
        golpeGo.transform.SetParent(mano != null ? mano : instancia.transform, false);
        golpeGo.transform.localPosition = mano != null ? Vector3.zero : new Vector3(0f, 1.2f, .6f);
        var caja = golpeGo.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(1.1f, .8f, 1.1f);
        var golpe = golpeGo.AddComponent<GolpeMelee>();
        golpe.danioJugador = .16f;
        acecho.golpe = golpe;
        var botellaFuente = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SM_Prop_LargeSign_Bottle_01_Especial.prefab");
        var botella = (GameObject)PrefabUtility.InstantiatePrefab(botellaFuente);
        botella.name = "BotellaJefe";
        botella.transform.SetParent(mano != null ? mano : instancia.transform, false);
        botella.transform.localPosition = new Vector3(0f, .05f, .08f);
        AjustarAltura(botella, .35f);
        int recogible = LayerMask.NameToLayer(CapasJuego.Recogibles);
        var coleccion = botella.GetComponent<ObjetoEspecialColeccionable>();
        coleccion.tipo = ObjetoEspecialColeccionable.TipoBotella.Objetivo;
        var portador = instancia.GetComponent<PortadorBotella>() ?? instancia.AddComponent<PortadorBotella>();
        portador.botella = coleccion;
        PonerCapa(instancia, CapasJuego.Enemigos);
        int arma = LayerMask.NameToLayer(CapasJuego.ArmaEnemigo);
        if (arma >= 0) golpeGo.layer = arma;
        if (recogible >= 0) PonerCapa(botella, CapasJuego.Recogibles);
        const string ruta = "Assets/Prefabs/Enemigos/NemesisJefe.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(instancia, ruta, InteractionMode.AutomatedAction);
        UnityEngine.Object.DestroyImmediate(instancia);
        return prefab;
    }

    static void IgnorarAgua()
    {
        foreach (var col in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!col.isTrigger && col.GetComponent<AguaLetal>() == null && col.gameObject.layer != LayerMask.NameToLayer("Water") && col.name != "Water") continue;
            var modificador = col.GetComponent<NavMeshModifier>() ?? col.gameObject.AddComponent<NavMeshModifier>();
            modificador.ignoreFromBuild = true;
        }
    }

    static NavMeshSurface Hornear()
    {
        var objeto = GameObject.Find("NavMeshIsla");
        var superficie = objeto != null ? objeto.GetComponent<NavMeshSurface>() : new GameObject("NavMeshIsla").AddComponent<NavMeshSurface>();
        superficie.agentTypeID = 0;
        superficie.collectObjects = CollectObjects.All;
        superficie.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        int agua = LayerMask.NameToLayer("Water");
        int ui = LayerMask.NameToLayer("UI");
        superficie.layerMask = ~((agua >= 0 ? 1 << agua : 0) | (ui >= 0 ? 1 << ui : 0) | (1 << 2));
        superficie.navMeshData = null;
        superficie.BuildNavMesh();
        if (NavMesh.CalculateTriangulation().vertices.Length < 3)
        {
            superficie.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            superficie.BuildNavMesh();
        }
        if (NavMesh.CalculateTriangulation().vertices.Length < 3)
            throw new Exception("El NavMesh de la isla quedó vacío.");
        Directory.CreateDirectory("Assets/Scenes/MapaV2");
        var datos = superficie.navMeshData;
        var previo = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshRuta);
        if (previo != null && previo != datos) AssetDatabase.DeleteAsset(NavMeshRuta);
        if (AssetDatabase.GetAssetPath(datos) != NavMeshRuta) AssetDatabase.CreateAsset(datos, NavMeshRuta);
        superficie.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshRuta);
        return superficie;
    }

    static void QuitarAnterior()
    {
        var viejo = GameObject.Find("ContenidoIsla");
        if (viejo != null) UnityEngine.Object.DestroyImmediate(viejo);
        var isla = GameObject.Find("Isla");
        if (isla != null) UnityEngine.Object.DestroyImmediate(isla);
        foreach (var objeto in UnityEngine.Object.FindObjectsByType<ObjetoEspecialColeccionable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(objeto.gameObject);
    }

    static void ColocarBotellas(Transform raiz, List<Vector3> puntos, Vector3 spawn, int cantidad)
    {
        var fuente = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SM_Prop_LargeSign_Bottle_01_Especial.prefab");
        var elegidos = Separar(puntos, spawn, cantidad, 7f, 6f);
        for (int i = 0; i < elegidos.Count; i++)
        {
            var botella = (GameObject)PrefabUtility.InstantiatePrefab(fuente);
            if (PrefabUtility.IsPartOfPrefabInstance(botella))
                PrefabUtility.UnpackPrefabInstance(botella, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            botella.name = "Botella_" + (i + 1).ToString("00");
            botella.transform.SetParent(raiz, true);
            botella.transform.position = elegidos[i] + Vector3.up * .2f;
            AjustarAltura(botella, .45f);
            PonerCapa(botella, CapasJuego.Recogibles);
            var cuerpo = Obtener<Rigidbody>(botella);
            cuerpo.isKinematic = true;
            cuerpo.useGravity = false;
        }
    }

    static void ColocarCajas(Transform raiz, List<Vector3> puntos, Vector3 spawn)
    {
        var fuente = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Crate_01.prefab");
        var efecto = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Impact_Wood_01.prefab");
        var elegidos = Separar(puntos, spawn, 15, 8f, 5f);
        foreach (var punto in elegidos)
        {
            var caja = (GameObject)PrefabUtility.InstantiatePrefab(fuente);
            if (PrefabUtility.IsPartOfPrefabInstance(caja))
                PrefabUtility.UnpackPrefabInstance(caja, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            caja.name = "Caja";
            caja.transform.SetParent(raiz, true);
            caja.transform.position = punto;
            var malla = caja.GetComponent<MeshCollider>();
            if (malla != null) malla.convex = true;
            var cuerpo = Obtener<Rigidbody>(caja);
            cuerpo.mass = 8f;
            cuerpo.constraints = RigidbodyConstraints.FreezeRotation;
            var vida = caja.AddComponent<ControladorVida>();
            vida.vidaMaxima = 1f;
            vida.segundosDestruir = .2f;
            var rompible = caja.AddComponent<CajaRompible>();
            rompible.efectoImpacto = efecto;
            rompible.sonidoRotura = ClipAudio("Explosion 1.wav");
            PonerCapa(caja, CapasJuego.Objetos);
        }
    }

    static void ColocarZombies(Transform raiz, GameObject[] prefabs, List<Vector3> puntos, Vector3 spawn)
    {
        var elegidos = Separar(puntos, spawn, 15, 10f, 8f);
        for (int i = 0; i < elegidos.Count; i++)
        {
            var zombie = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i % prefabs.Length]);
            zombie.name = "Zombie_" + (i + 1).ToString("00");
            zombie.transform.SetParent(raiz, true);
            zombie.transform.position = elegidos[i];
            var jugador = GameObject.FindGameObjectWithTag("Player");
            zombie.GetComponent<EnemigoAcechador>().objetivo = jugador.transform;
        }
    }

    static void EquiparBate(GameObject jugador, AudioClip swoosh)
    {
        var previo = jugador.transform.Find("Bate");
        if (previo != null) UnityEngine.Object.DestroyImmediate(previo.gameObject);
        var fuente = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonBattleRoyale/Prefabs/Weapons/SM_Wep_Bat_01.prefab");
        var bate = (GameObject)PrefabUtility.InstantiatePrefab(fuente);
        bate.name = "Bate";
        var anim = jugador.GetComponentInChildren<Animator>();
        var mano = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        bate.transform.SetParent(mano != null ? mano : jugador.transform, false);
        bate.transform.localPosition = new Vector3(.02f, 0f, .05f);
        bate.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
        bate.transform.localScale = Vector3.one;
        var hoja = new GameObject("Hoja");
        hoja.transform.SetParent(bate.transform, false);
        hoja.transform.localPosition = new Vector3(0f, .45f, 0f);
        var caja = hoja.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(.25f, .9f, .25f);
        var golpe = hoja.AddComponent<GolpeMelee>();
        golpe.danio = 40f;
        int arma = LayerMask.NameToLayer(CapasJuego.ArmaJugador);
        if (arma >= 0) hoja.layer = arma;
        var ataque = Obtener<AtaqueMeleeJugador>(jugador);
        ataque.golpe = golpe;
        ataque.swoosh = swoosh;
        var inventario = Obtener<InventarioJugador>(jugador);
        inventario.bate = bate;
        inventario.armaActiva = InventarioJugador.Arma.Bate;
    }

    static void AjustarAltura(GameObject objeto, float altura)
    {
        var renderers = objeto.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y > .01f) objeto.transform.localScale *= altura / bounds.size.y;
    }

    static void PonerCapa(GameObject objeto, string capa)
    {
        int indice = LayerMask.NameToLayer(capa);
        if (indice < 0) return;
        foreach (var t in objeto.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = indice;
    }

    static List<Vector3> PuntosNavMesh(int cantidad)
    {
        var tri = NavMesh.CalculateTriangulation();
        var puntos = new List<Vector3>();
        if (tri.indices == null || tri.indices.Length < 3) return puntos;
        var azar = new System.Random(Semilla);
        var filtro = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        for (int i = 0; i < cantidad; i++)
        {
            int t = azar.Next(tri.indices.Length / 3) * 3;
            Vector3 a = tri.vertices[tri.indices[t]];
            Vector3 b = tri.vertices[tri.indices[t + 1]];
            Vector3 c = tri.vertices[tri.indices[t + 2]];
            float u = (float)azar.NextDouble();
            float v = (float)azar.NextDouble();
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            Vector3 p = a + u * (b - a) + v * (c - a);
            if (NavMesh.SamplePosition(p, out var hit, 2f, filtro)) puntos.Add(hit.position);
        }
        return puntos;
    }

    static List<Vector3> Separar(List<Vector3> puntos, Vector3 origen, int cantidad, float minimoOrigen, float minimoEntre)
    {
        var azar = new System.Random(Semilla + cantidad);
        var orden = puntos.OrderBy(_ => azar.Next()).ToList();
        var elegidos = new List<Vector3>();
        float entre = minimoEntre;
        float lejos = minimoOrigen;
        for (int intento = 0; intento < 4 && elegidos.Count < cantidad; intento++)
        {
            elegidos.Clear();
            foreach (var punto in orden)
            {
                if (Distancia(punto, origen) < lejos) continue;
                if (elegidos.Any(otro => Distancia(otro, punto) < entre)) continue;
                elegidos.Add(punto);
                if (elegidos.Count >= cantidad) break;
            }
            entre *= .6f;
            lejos *= .6f;
        }
        if (elegidos.Count < cantidad) throw new Exception("Solo hay " + elegidos.Count + " puntos separados; hacían falta " + cantidad + ".");
        return elegidos;
    }

    static Vector3 MasLejos(List<Vector3> puntos, Vector3 desde) =>
        puntos.OrderByDescending(p => Distancia(p, desde)).First();

    static Vector3 MasLejos(List<Vector3> puntos, Vector3 desde, Vector3 evitar, float separacion)
    {
        var candidato = puntos.Where(p => Distancia(p, evitar) >= separacion).OrderByDescending(p => Distancia(p, desde)).FirstOrDefault();
        if (candidato == Vector3.zero && !puntos.Contains(Vector3.zero))
            candidato = MasLejos(puntos, desde);
        return candidato == Vector3.zero ? MasLejos(puntos, desde) : candidato;
    }

    static float Distancia(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static Transform Buscar(string nombre)
    {
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == nombre) return t;
        return null;
    }

    static AudioClip ClipAudio(string archivo) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Profe/" + archivo);

    static AnimationClip Clip(string nombre, string ruta)
    {
        var directo = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        if (directo != null) return directo;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ruta))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__") && clip.name.IndexOf(nombre, StringComparison.OrdinalIgnoreCase) >= 0)
                return clip;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ruta))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__")) return clip;
        return null;
    }

    // Clip de un FBX de Kevin Iglesias, por nombre parcial del clip. Devuelve null si el paquete no está.
    static AnimationClip Paquete(string nombre, string rutaRelativa)
    {
        string ruta = "Assets/Kevin Iglesias/" + rutaRelativa;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ruta))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__") && clip.name.EndsWith(nombre, StringComparison.OrdinalIgnoreCase))
                return clip;
        return null;
    }

    const string CarpetaCombate = "Assets/Animaciones/Combate";

    static readonly string[] ClipsEsperados =
    {
        "Zombie Attack", "Zombie Reaction Hit", "Zombie Dying", "Zombie Idle", "Zombie Walk",
        "Mutant Swiping", "Mutant Dying",
        "Standing Melee Attack Horizontal", "Hit Reaction", "Dying"
    };

    static void AvisarClipsFaltantes()
    {
        var faltan = ClipsEsperados.Where(nombre => BuscarCombate(nombre) == null).ToArray();
        if (faltan.Length == 0) return;
        Debug.LogWarning("Combate: faltan " + string.Join(", ", faltan) + ". El FBX tiene que llamarse exactamente así, sin sufijos.");
    }

    static void PrepararClipsCombate()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaCombate)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CarpetaCombate }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!ruta.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            var importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (importer == null) continue;
            string archivo = Path.GetFileNameWithoutExtension(ruta);
            bool loop = archivo == "Zombie Idle" || archivo == "Zombie Walk";
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool clipsListos = clips != null && clips.Length > 0 && clips.All(c => ClipRaizHorneada(c, loop));
            bool humano = importer.animationType == ModelImporterAnimationType.Human
                && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel
                && importer.importAnimation;
            if (humano && clipsListos) continue;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            if (clips != null)
            {
                foreach (var clip in clips) HornearRaiz(clip, loop);
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
        }
    }

    static void HornearRaiz(ModelImporterClipAnimation clip, bool loop)
    {
        clip.loopTime = loop;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
    }

    static bool ClipRaizHorneada(ModelImporterClipAnimation clip, bool loop) =>
        clip.loopTime == loop
        && clip.keepOriginalOrientation
        && clip.keepOriginalPositionY
        && clip.keepOriginalPositionXZ
        && clip.lockRootRotation
        && clip.lockRootHeightY
        && clip.lockRootPositionXZ;

    static AnimationClip BuscarCombate(string texto)
    {
        if (string.IsNullOrEmpty(texto) || !AssetDatabase.IsValidFolder(CarpetaCombate)) return null;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CarpetaCombate }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileNameWithoutExtension(ruta), texto, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ruta))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__")) return clip;
        }
        return null;
    }
}
