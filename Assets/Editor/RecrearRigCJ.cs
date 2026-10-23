using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Fits a Humanoid skeleton to CJ without changing the original mesh geometry.
// Bind-pose conversion happens once, in the editor; gameplay uses native GPU skinning.
public static class RecrearRigCJ
{
    const string Folder = "Assets/Prefabs/Personaje/cj/Humanoid";
    const string Character = "Assets/Prefabs/Personaje/cj/CJ.fbx";
    const string Models = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Models/Armature.fbx";
    const string Clips = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/";
    static readonly HumanBodyBones[] BoneIds = {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
        HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };

    public static void GenerarVisual()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var original = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character));
        original.transform.position = new Vector3(0f,1.5485042f,2.9860067f);
        var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Models));
        try
        {
            source.name = "CJModel";
            source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = source.GetComponent<Animator>();
            animator.enabled = false;
            animator.runtimeAnimatorController = null;
            var bones = BoneIds.Select(animator.GetBoneTransform).ToArray();
            var leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            var rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            var leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            var rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            var description = animator.avatar.humanDescription;
            // The imported Animator can restore its donor bind pose when queried.
            // Build the fitted skeleton independently of that Animator.
            UnityEngine.Object.DestroyImmediate(animator);
            var virtualPositions = new Vector3[bones.Length];
            var virtualRotations = bones.Select(b => b.rotation).ToArray();
            for (int i = 0; i < bones.Length; i++)
                virtualPositions[i] = new Vector3(bones[i].position.x, MapHeight(bones[i].position.y), bones[i].position.z);

            // CJ's original names have the opposite left/right convention to the Humanoid avatar.
            SetLimb(6, new Vector3(-0.2145f,1.4661f,-0.0903f), new Vector3(-0.2411f,1.2027f,-0.0903f), new Vector3(-0.2633f,0.9384f,-0.0137f));
            SetLimb(9, new Vector3(0.1977f,1.4644f,-0.0514f), new Vector3(0.2245f,1.1997f,-0.0583f), new Vector3(0.2509f,0.9383f,-0.0137f));
            SetLimb(12, new Vector3(-0.12f,1.0772f,0f), new Vector3(-0.1501f,0.5717f,0f), new Vector3(-0.1392f,0.1113f,-0.0847f));
            SetLimb(15, new Vector3(0.10f,1.0772f,0f), new Vector3(0.1009f,0.5729f,-0.0185f), new Vector3(0.0984f,0.1021f,-0.0695f));
            virtualPositions[5] = new Vector3(0f, 1.64f, 0.03f);

            void SetLimb(int start, Vector3 upper, Vector3 lower, Vector3 end)
            {
                virtualPositions[start] = upper;
                virtualPositions[start+1] = lower;
                virtualPositions[start+2] = end;
                virtualRotations[start] = Quaternion.FromToRotation(bones[start+1].position-bones[start].position,lower-upper) * bones[start].rotation;
                virtualRotations[start+1] = Quaternion.FromToRotation(bones[start+2].position-bones[start+1].position,end-lower) * bones[start+1].rotation;
                // Feet keep their original orientation; hands follow the forearm's bind orientation.
                if (start < 12)
                    virtualRotations[start+2] = virtualRotations[start+1] * Quaternion.Inverse(bones[start+1].rotation) * bones[start+2].rotation;
            }

            // Fit the skeleton to CJ, retaining every original vertex in model space.
            // Hierarchy order matters: position parents before their children.
            for (int i = 0; i < bones.Length; i++)
            {
                if (i == 6 || i == 9)
                {
                    var shoulder = i == 6 ? leftShoulder : rightShoulder;
                    shoulder.position = new Vector3(i == 6 ? -0.0014f : 0.0014f, 1.4691f, -0.0514f);
                }
                bones[i].SetPositionAndRotation(virtualPositions[i], virtualRotations[i]);
            }
            leftToes.position = new Vector3(-0.1622f, -0.0200f, 0.0780f);
            rightToes.position = new Vector3(0.1090f, 0.0003f, 0.1104f);
            // Calibrate Humanoid axes in a T-pose, then restore CJ's original bind pose.
            var transforms = source.GetComponentsInChildren<Transform>();
            var restPositions = transforms.Select(t => t.localPosition).ToArray();
            var restRotations = transforms.Select(t => t.localRotation).ToArray();
            StraightenArm(6, Vector3.left);
            StraightenArm(9, Vector3.right);
            StraightenLeg(12);
            StraightenLeg(15);
            void StraightenArm(int start, Vector3 direction)
            {
                bones[start].rotation = Quaternion.FromToRotation(bones[start+1].position-bones[start].position, direction) * bones[start].rotation;
                bones[start+1].rotation = Quaternion.FromToRotation(bones[start+2].position-bones[start+1].position, direction) * bones[start+1].rotation;
            }
            void StraightenLeg(int start)
            {
                var footRotation = bones[start+2].rotation;
                bones[start].rotation = Quaternion.FromToRotation(bones[start+1].position-bones[start].position, Vector3.down) * bones[start].rotation;
                bones[start+1].rotation = Quaternion.FromToRotation(bones[start+2].position-bones[start+1].position, Vector3.down) * bones[start+1].rotation;
                bones[start+2].rotation = footRotation;
            }
            description.armStretch = 0f;
            description.legStretch = 0f;
            description.skeleton = source.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone {
                name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
            }).ToArray();
            var avatar = AvatarBuilder.BuildHumanAvatar(source, description);
            if (!avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("No se pudo crear el avatar con las proporciones originales de CJ.");
            avatar.name = "CJHumanoidAvatar";
            string avatarPath = Folder + "/CJHumanoidAvatar.asset";
            string avatarMeta = File.Exists(avatarPath + ".meta") ? File.ReadAllText(avatarPath + ".meta") : null;
            if (AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath) != null) AssetDatabase.DeleteAsset(avatarPath);
            AssetDatabase.CreateAsset(avatar, avatarPath);
            if (avatarMeta != null)
            {
                File.WriteAllText(avatarPath + ".meta", avatarMeta);
                AssetDatabase.ImportAsset(avatarPath, ImportAssetOptions.ForceUpdate);
                avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            }
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].localPosition = restPositions[i];
                transforms[i].localRotation = restRotations[i];
            }
            animator = source.AddComponent<Animator>();
            animator.enabled = false;
            animator.avatar = avatar;
            var bindposes = bones.Select(b => b.worldToLocalMatrix * source.transform.localToWorldMatrix).ToArray();
            foreach(var renderer in source.GetComponentsInChildren<Renderer>())
                UnityEngine.Object.DestroyImmediate(renderer.gameObject);

            foreach(var renderer in original.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh;
                if(renderer is SkinnedMeshRenderer oldSkin)
                {
                    mesh = new Mesh();
                    oldSkin.BakeMesh(mesh);
                }
                else mesh = UnityEngine.Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                var originalSkin = renderer as SkinnedMeshRenderer;
                mesh = BindOriginalMesh(mesh, renderer.name, renderer.transform.localToWorldMatrix, originalSkin);
                mesh.name = "CJ_" + renderer.name + "_Humanoid";
                mesh.bindposes = bindposes;
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                string path=Folder+"/"+mesh.name+".asset";
                mesh = SaveMesh(mesh, path);
                var go=new GameObject(renderer.name);
                go.transform.SetParent(source.transform,false);
                var skin=go.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh=mesh;
                string materialName=renderer.name=="TORSO"?"torso":renderer.name=="PIERNAS"?"pants":renderer.name=="PIES"?"sneakers":"face";
                skin.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Personaje/cj/Materials/"+materialName+".mat");
                skin.quality=SkinQuality.Bone4;
                skin.bones=bones;
                skin.rootBone=bones[0];
                // Includes the full jump/stride envelope for offscreen culling.
                skin.localBounds=new Bounds(new Vector3(0f,0.9f,0f),new Vector3(2.6f,2.6f,2.6f));
            }
            animator.runtimeAnimatorController = CrearController();
            animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.enabled=true;
            source.AddComponent<CJLocomotionAnimation>();
            PrefabUtility.SaveAsPrefabAsset(source,Folder+"/CJVisual.prefab");
            AssetDatabase.SaveAssets();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        // Rebuild native buffers explicitly when the topology changes.
        existing.Clear();
        existing.vertices = mesh.vertices;
        existing.normals = mesh.normals;
        existing.uv = mesh.uv;
        existing.tangents = mesh.tangents;
        existing.boneWeights = mesh.boneWeights;
        existing.bindposes = mesh.bindposes;
        existing.subMeshCount = mesh.subMeshCount;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            existing.SetTriangles(mesh.GetTriangles(submesh), submesh);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(mesh);
        return existing;
    }

    static float MapHeight(float y)
    {
        if(y<0.9811f) return Mathf.LerpUnclamped(0.105f,1.0772f,(y-0.102f)/(0.9811f-0.102f));
        return Mathf.LerpUnclamped(1.0772f,1.64f,(y-0.9811f)/(1.6237f-0.9811f));
    }

    static Mesh BindOriginalMesh(Mesh mesh, string part, Matrix4x4 toWorld, SkinnedMeshRenderer originalSkin)
    {
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var normalMatrix = toWorld.inverse.transpose;
        var weights = new BoneWeight[vertices.Length];
        var authoredWeights = originalSkin != null ? originalSkin.sharedMesh.boneWeights : null;
        var armMembership = part == "TORSO" ? TorsoArmMembership(mesh, toWorld) : null;
        for (int v = 0; v < vertices.Length; v++)
        {
            Vector3 p = toWorld.MultiplyPoint3x4(vertices[v]);
            // Loose trouser cuffs cross the centre line. Use their authored bone
            // membership instead of choosing a leg from the vertex's X coordinate.
            int side = p.x < 0f ? -1 : 1;
            if (part == "PIERNAS" && originalSkin != null)
            {
                var authored = authoredWeights[v];
                string bone = originalSkin.bones[authored.boneIndex0].name;
                side = bone == "PIERNA_DER" || bone == "Junta_1_2" ? -1 : 1;
            }
            var w = JointWeight(part, p, side, armMembership == null ? 0f : armMembership[v]);
            if (part == "PIERNAS" && p.y > 0.45f && originalSkin != null)
            {
                var authored = authoredWeights[v];
                float left = Left(authored.boneIndex0, authored.weight0) + Left(authored.boneIndex1, authored.weight1)
                    + Left(authored.boneIndex2, authored.weight2) + Left(authored.boneIndex3, authored.weight3);
                left = Mathf.Lerp(side < 0 ? 1f : 0f, left, JointBlend(0.62f, 0.72f, p.y));
                float pelvis = Mathf.Max(JointBlend(0.85f, 1.04f, p.y),
                    (1f - JointBlend(0.03f, 0.21f, Mathf.Abs(p.x + 0.015f))) * JointBlend(0.57f, 0.83f, p.y));
                float thigh = JointBlend(0.45f, 0.70f, p.y);
                w = new BoneWeight { boneIndex0 = 0, weight0 = pelvis,
                    boneIndex1 = 12, weight1 = (1f - pelvis) * thigh * left,
                    boneIndex2 = 15, weight2 = (1f - pelvis) * thigh * (1f - left),
                    boneIndex3 = side < 0 ? 13 : 16, weight3 = (1f - pelvis) * (1f - thigh) };
                float Left(int index, float weight)
                {
                    string name = originalSkin.bones[index].name;
                    return name == "PIERNA_DER" || name == "Junta_1_2" ? weight : 0f;
                }
            }
            vertices[v] = p;
            if (normals.Length == vertices.Length) normals[v] = normalMatrix.MultiplyVector(normals[v]).normalized;
            weights[v] = w;
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.boneWeights = weights;
        return mesh;
    }

    static BoneWeight Pair(int first, int second, float blend)
        => new BoneWeight { boneIndex0 = first, boneIndex1 = second, weight0 = 1f - blend, weight1 = blend };

    static float JointBlend(float low, float high, float value)
        => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(low, high, value));

    // Identify the three separate surfaces below the armpits. An X cutoff would
    // assign the inner forearm to the chest, stretching it when the arm swings.
    static float[] TorsoArmMembership(Mesh mesh, Matrix4x4 toWorld)
    {
        var points = mesh.vertices.Select(toWorld.MultiplyPoint3x4).ToArray();
        int count = points.Length;
        var canonical = new int[count];
        var welded = new Dictionary<Vector3Int, int>();
        var parent = Enumerable.Range(0, count).ToArray();
        var neighbours = Enumerable.Range(0, count).Select(_ => new HashSet<int>()).ToArray();
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        for (int i = 0; i < count; i++)
        {
            var key = Vector3Int.RoundToInt(points[i] * 10000f);
            if (!welded.TryGetValue(key, out int index)) welded[key] = index = i;
            canonical[i] = index;
        }
        var triangles = mesh.triangles;
        for (int t = 0; t < triangles.Length; t += 3)
        for (int edge = 0; edge < 3; edge++)
        {
            int a = canonical[triangles[t + edge]], b = canonical[triangles[t + (edge + 1) % 3]];
            if (a == b) continue;
            neighbours[a].Add(b); neighbours[b].Add(a);
            if (points[a].y < 1.30f && points[b].y < 1.30f) parent[Find(a)] = Find(b);
        }
        var minima = new Dictionary<int, float>();
        foreach (int i in welded.Values)
        {
            int group = Find(i);
            minima[group] = minima.TryGetValue(group, out float y) ? Mathf.Min(y, points[i].y) : points[i].y;
        }
        var value = new float[count];
        var fixedValue = new bool[count];
        foreach (int i in welded.Values)
        {
            if (points[i].y < 1.30f)
            {
                fixedValue[i] = true;
                value[i] = minima[Find(i)] < 1f ? 1f : 0f;
            }
            else if (Mathf.Abs(points[i].x) < 0.12f || points[i].y > 1.50f) fixedValue[i] = true;
            else value[i] = JointBlend(0.15f, 0.20f, Mathf.Abs(points[i].x));
        }
        // Blend only across the connected shoulder region, keeping the lower
        // shirt and the complete elbow/forearm rings assigned to their own part.
        for (int iteration = 0; iteration < 120; iteration++)
        foreach (int i in welded.Values)
            if (!fixedValue[i] && neighbours[i].Count > 0)
                value[i] = neighbours[i].Average(n => value[n]);
        return canonical.Select(i => value[i]).ToArray();
    }

    static BoneWeight JointWeight(string part, Vector3 p, int side, float sleeve)
    {
        int arm = p.x < 0f ? 6 : 9;
        int leg = side < 0 ? 12 : 15;
        // The original head includes the neck. Share its collar weights with the
        // shirt so head rotation cannot pull the neck away from the torso.
        float neck = JointBlend(1.49f, 1.56f, p.y);
        if (part == "CABEZA") return Pair(1, 5, neck);
        float wrist = 1f - JointBlend(0.92f, 0.98f, p.y);
        if (part == "MANOS") return Pair(arm + 1, arm + 2, wrist);
        if (part == "PIES") return Pair(leg + 2, leg + 2, 0f);
        if (part == "PIERNAS")
        {
            if (p.y > 0.45f) return Pair(leg + 1, leg, JointBlend(0.45f, 0.70f, p.y));
            return Pair(leg + 2, leg + 1, JointBlend(0.07f, 0.22f, p.y));
        }
        if (p.y > 1.49f && Mathf.Abs(p.x) < 0.14f) return Pair(1, 5, neck);
        // Blend the connected joint rings; never blend the inner forearm with
        // the chest just because their coordinates are close in the rest pose.
        float forearm = 1f - JointBlend(1.17f, 1.23f, p.y);
        return new BoneWeight { boneIndex0 = 1, weight0 = 1f - sleeve,
            boneIndex1 = arm, weight1 = sleeve * (1f - forearm),
            boneIndex2 = arm + 1, weight2 = sleeve * forearm * (1f - wrist),
            boneIndex3 = arm + 2, weight3 = sleeve * forearm * wrist };
    }

    static AnimationClip Clip(string filename,string name)
        => AssetDatabase.LoadAllAssetsAtPath(Clips+filename).OfType<AnimationClip>().First(c=>c.name==name);

    static AnimatorController CrearController()
    {
        string path=Folder+"/CJLocomotion.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller!=null) return controller;
        controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
        controller.AddParameter("MotionSpeed",AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded",AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump",AnimatorControllerParameterType.Bool);
        controller.AddParameter("FreeFall",AnimatorControllerParameterType.Bool);
        var machine=controller.layers[0].stateMachine;
        var locomotion=machine.AddState("Locomotion");
        var tree=new BlendTree { name="Reposo, caminar y correr",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false };
        AssetDatabase.AddObjectToAsset(tree,controller);
        tree.AddChild(Clip("Stand--Idle.anim.fbx","Idle"),0f);
        tree.AddChild(Clip("Locomotion--Walk_N.anim.fbx","Walk_N"),2f);
        tree.AddChild(Clip("Locomotion--Run_N.anim.fbx","Run_N"),5.335f);
        var children=tree.children;
        children[2].timeScale=5.335f/6f;
        tree.children=children;
        locomotion.motion=tree;
        locomotion.iKOnFeet=true;
        var jump=machine.AddState("Jump");jump.motion=Clip("Jump--Jump.anim.fbx","JumpStart");
        var fall=machine.AddState("Fall");fall.motion=Clip("Jump--InAir.anim.fbx","InAir");
        var land=machine.AddState("Land");land.motion=Clip("Jump--Jump.anim.fbx","JumpLand");
        machine.defaultState=locomotion;
        Transition(locomotion,jump,"Jump",true,0.06f);
        Transition(locomotion,fall,"FreeFall",true,0.12f);
        var toFall=jump.AddTransition(fall);toFall.hasExitTime=true;toFall.exitTime=0.85f;toFall.duration=0.08f;
        Transition(jump,land,"Grounded",true,0.06f);
        Transition(fall,land,"Grounded",true,0.08f);
        var toMove=land.AddTransition(locomotion);toMove.hasExitTime=true;toMove.exitTime=0.65f;toMove.duration=0.1f;
        Transition(land,jump,"Jump",true,0.06f);
        return controller;
    }

    static void Transition(AnimatorState from,AnimatorState to,string parameter,bool value,float duration)
    {
        var t=from.AddTransition(to);t.hasExitTime=false;t.duration=duration;t.hasFixedDuration=true;
        t.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0f,parameter);
    }
}
