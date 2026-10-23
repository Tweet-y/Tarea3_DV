using StarterAssets;
using UnityEngine;

// CJ uses a Humanoid rig fitted to his original geometry. Poses come from imported clips;
// Rigid segments retain their authored dimensions while the clips rotate the joints.
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class CJLocomotionAnimation : MonoBehaviour
{
    Animator animator;
    ThirdPersonController controller;
    Transform[] joints;
    Vector3[] jointPositions, jointScales;
    Quaternion[] jointRotations;
    float[] rotationLimits;
    Transform hips;
    Transform spine, chest, upperChest;
    Quaternion chestRotation, upperChestRotation, spineFromUpperChest;

    public void Initialize()
    {
        animator = GetComponent<Animator>();
        controller = GetComponentInParent<ThirdPersonController>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;
        if (joints == null)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            chestRotation = chest.localRotation;
            upperChestRotation = upperChest.localRotation;
            spineFromUpperChest = Quaternion.Inverse(upperChest.rotation) * spine.rotation;
            joints = hips.GetComponentsInChildren<Transform>();
            jointPositions = new Vector3[joints.Length];
            jointScales = new Vector3[joints.Length];
            jointRotations = new Quaternion[joints.Length];
            rotationLimits = new float[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                jointPositions[i] = joints[i].localPosition;
                jointScales[i] = joints[i].localScale;
                jointRotations[i] = joints[i].localRotation;
            }
            SetLimit(HumanBodyBones.Hips, 25f);
            SetLimit(HumanBodyBones.Spine, 22f);
            SetLimit(HumanBodyBones.Neck, 10f);
            SetLimit(HumanBodyBones.Head, 15f);
            SetLimit(HumanBodyBones.LeftShoulder, 10f);
            SetLimit(HumanBodyBones.RightShoulder, 10f);
            SetLimit(HumanBodyBones.LeftUpperArm, 60f);
            SetLimit(HumanBodyBones.RightUpperArm, 60f);
            SetLimit(HumanBodyBones.LeftLowerArm, 90f);
            SetLimit(HumanBodyBones.RightLowerArm, 90f);
            SetLimit(HumanBodyBones.LeftHand, 20f);
            SetLimit(HumanBodyBones.RightHand, 20f);
            SetLimit(HumanBodyBones.LeftUpperLeg, 45f);
            SetLimit(HumanBodyBones.RightUpperLeg, 45f);
            SetLimit(HumanBodyBones.LeftLowerLeg, 80f);
            SetLimit(HumanBodyBones.RightLowerLeg, 80f);
            SetLimit(HumanBodyBones.LeftFoot, 25f);
            SetLimit(HumanBodyBones.RightFoot, 25f);
        }
    }

    void SetLimit(HumanBodyBones id, float degrees)
    {
        int index = System.Array.IndexOf(joints, animator.GetBoneTransform(id));
        if (index >= 0) rotationLimits[index] = degrees;
    }

    void Awake() { Initialize(); }

    void LateUpdate() { PreserveDimensions(); }

    void PreserveDimensions()
    {
        if (joints == null) return;
        // Apply the clip's combined torso lean at the waist, keeping the torso solid.
        Quaternion torsoRotation = upperChest.rotation * spineFromUpperChest;
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == hips)
            {
                // CharacterController owns travel and jump height. Keep only the
                // clip's small weight shifts, rather than its authored root travel.
                Vector3 shift = hips.localPosition - jointPositions[i];
                hips.localPosition = jointPositions[i] + new Vector3(
                    Mathf.Clamp(shift.x, -0.03f, 0.03f),
                    Mathf.Clamp(shift.y, -0.06f, 0.06f),
                    Mathf.Clamp(shift.z, -0.03f, 0.03f));
            }
            else joints[i].localPosition = jointPositions[i];
            joints[i].localScale = jointScales[i];
        }
        spine.rotation = torsoRotation;
        chest.localRotation = chestRotation;
        upperChest.localRotation = upperChestRotation;
        // The low-poly joint rings cannot accommodate the imported clips' deep
        // crouches and twists. Ease into their supported range without abrupt stops.
        for (int i = 0; i < joints.Length; i++)
        {
            float limit = rotationLimits[i];
            if (limit <= 0f) continue;
            float angle = Quaternion.Angle(jointRotations[i], joints[i].localRotation);
            float start = limit * 0.75f;
            if (angle <= start) continue;
            float t = (angle - start) / (limit - start);
            float bounded = start + (limit - start) * t / (1f + t);
            joints[i].localRotation = Quaternion.Slerp(jointRotations[i], joints[i].localRotation, bounded / angle);
        }
    }

    // Deterministic editor preview of the same clips used in gameplay.
    public void ApplyPose(float movementSpeed, float cycle, float jumpBlend)
    {
        if (animator == null) Initialize();
        animator.SetFloat("Speed", movementSpeed);
        animator.SetFloat("MotionSpeed", 1f);
        animator.SetBool("Grounded", jumpBlend < 0.5f);
        animator.SetBool("Jump", false);
        animator.SetBool("FreeFall", false);
        animator.Play(jumpBlend >= 0.5f ? "Jump" : "Locomotion", 0,
            Mathf.Repeat(cycle / (2f * Mathf.PI), 1f));
        animator.Update(0f);
        PreserveDimensions();
    }

    void OnFootstep(AnimationEvent animationEvent)
    {
        if (Application.isPlaying && controller != null) controller.OnFootstep(animationEvent);
    }

    void OnLand(AnimationEvent animationEvent)
    {
        if (Application.isPlaying && controller != null) controller.OnLand(animationEvent);
    }
}
