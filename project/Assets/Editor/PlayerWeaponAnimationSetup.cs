using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>세피리아 대검의 위치·회전 키를 현재 고정 검의 독립 Animator 에셋으로 준비</summary>
public static class PlayerWeaponAnimationSetup
{
    private const string Folder = "Assets/Art/Player/Animations/Weapon"; // 현재 프로젝트에서만 사용하는 검 모션 에셋 경로
    private const string MotionPath = "WeaponMotion"; // 조준 부모의 Animator가 움직일 단일 자식 경로

    /// <summary>없는 클립·컨트롤러만 만들고 기존 조정과 GUID를 보존한 컨트롤러 반환</summary>
    public static RuntimeAnimatorController EnsureAssets()
    {
        // 프로젝트 내부 폴더와 누락된 애니메이션만 최초 생성
        EnsureFolder(Folder);
        AnimationClip[] clips = { EnsureIdle(), EnsureAttack1(), EnsureAttack2(), EnsureAttack3() };
        string controllerPath = Folder + "/PlayerWeapon.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller != null) return controller;

        // 판정·콤보는 AttackController가 소유하며 Animator에는 전환과 외부 이벤트 없이 네 자세만 구성
        controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimatorState state = machine.AddState(clips[i].name, new Vector3(260f, 60f + i * 80f, 0f));
            state.motion = clips[i];
            state.writeDefaultValues = false;
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    /// <summary>취소와 공격 종료 때 모션 자식만 원점으로 돌리는 대기 클립 준비</summary>
    private static AnimationClip EnsureIdle()
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Idle.anim");
        if (existing != null) return existing;
        var keys = new[] { new Keyframe(0f, 0f), new Keyframe(1f / 60f, 0f) };
        return CreateClip("Idle", keys, keys, keys);
    }

    /// <summary>원작 GreatSword_Attack1의 시간·접선을 보존하고 +Y 조준 위치를 +X 기준으로 변환</summary>
    private static AnimationClip EnsureAttack1()
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Attack1.anim");
        if (existing != null) return existing;

        // 위치 (x, y)를 (y, -x)로 회전하며 현재 검 Sprite의 +90도 보정과 원작 Z 회전 유지
        return CreateClip("Attack1", new[]
        {
            new Keyframe(0f, 0.145f, 0f, 0f),
            new Keyframe(0.06666667f, 0.114728f, -0.60544014f, -0.6054401f),
            new Keyframe(0.2f, -0.11f, -0.000001067104f, 0f),
            new Keyframe(0.3f, -0.03f, 1.5524999f, 1.5524999f),
            new Keyframe(0.33333334f, 0.097f, -0.0000017804897f, 0f),
            new Keyframe(0.36666667f, -0.089f, -10.005001f, -10.005f),
            new Keyframe(0.4f, -0.57f, -1.371432f, -1.3714283f),
            new Keyframe(0.6f, -0.65f, 1.6961782e-7f, 0f)
        }, new[]
        {
            new Keyframe(0f, -0.051f, 0f, 0f),
            new Keyframe(0.06666667f, -0.047831997f, 0.06336005f, 0.06336004f),
            new Keyframe(0.2f, 0.15f, -5.20141e-7f, 0f),
            new Keyframe(0.3f, 0.1f, -1.5f, -1.5000001f),
            new Keyframe(0.33333334f, -0.126f, -7.56f, -7.5600004f),
            new Keyframe(0.36666667f, -0.404f, -4.56f, -4.559999f),
            new Keyframe(0.4f, -0.48f, -1.764297e-7f, 0f),
            new Keyframe(0.6f, -0.4f, -2.3841757e-8f, 0f)
        }, new[]
        {
            new Keyframe(0f, 159.3f, 0f, 0f),
            new Keyframe(0.06666667f, 163.7704f, 89.40797f, 89.40797f),
            new Keyframe(0.2f, 182.46f, -0.00006477345f, 0f),
            new Keyframe(0.3f, 179.309f, -94.530014f, -94.53002f),
            new Keyframe(0.33333334f, 22.5f, -3906.4495f, -3906.4502f),
            new Keyframe(0.36666667f, -81.121f, -1833.1807f, -1833.1807f),
            new Keyframe(0.4f, -111.674f, -194.03572f, -194.0357f),
            new Keyframe(0.6f, -126.396f, 0.000012115411f, 0f)
        });
    }

    /// <summary>원작 GreatSword_Attack2의 시간·접선을 보존하고 +Y 조준 위치를 +X 기준으로 변환</summary>
    private static AnimationClip EnsureAttack2()
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Attack2.anim");
        if (existing != null) return existing;

        // 위치 (x, y)를 (y, -x)로 회전하며 현재 검 Sprite의 +90도 보정과 원작 Z 회전 유지
        return CreateClip("Attack2", new[]
        {
            new Keyframe(0f, -0.54499996f, 0f, 0f),
            new Keyframe(0.13333334f, -0.23f, -9.427342e-7f, 0f),
            new Keyframe(0.23333333f, -0.31f, 4.005431e-7f, 0f),
            new Keyframe(0.26666668f, 0.58f, 0.0000062931313f, 0f),
            new Keyframe(0.3f, 0.11f, -14.549994f, -14.55f),
            new Keyframe(0.33333334f, -0.39f, -1.1999905f, -1.2000003f),
            new Keyframe(0.6f, -0.48f, -1.5307221e-7f, 0f),
            new Keyframe(0.6666667f, -0.38f, 3.0000002f, 3.0000002f),
            new Keyframe(0.73333335f, 0.145f, -0.0000038133057f, 0f)
        }, new[]
        {
            new Keyframe(0f, -0.42000002f, 0f, 0f),
            new Keyframe(0.13333334f, -0.48f, -0.17142904f, -0.17142893f),
            new Keyframe(0.23333333f, -0.49f, 1.6263519e-8f, 0f),
            new Keyframe(0.26666668f, -0.13f, 12.450001f, 12.449999f),
            new Keyframe(0.3f, 0.34f, 2.3999937f, 2.3999994f),
            new Keyframe(0.33333334f, 0.38f, -1.3013694e-7f, 0f),
            new Keyframe(0.6f, 0.33f, -0.39f, -0.39f),
            new Keyframe(0.6666667f, 0.25f, -2.4000008f, -2.4000006f),
            new Keyframe(0.73333335f, -0.051f, -6.800891e-7f, 0f)
        }, new[]
        {
            new Keyframe(0f, -131.60126f, 0f, 0f),
            new Keyframe(0.13333334f, -147.217f, -128.1203f, -128.12033f),
            new Keyframe(0.23333333f, -161.496f, 0.000043553115f, 0f),
            new Keyframe(0.26666668f, -9.117f, 3410.1892f, 3410.1895f),
            new Keyframe(0.3f, 65.85f, 1853.7452f, 1853.7451f),
            new Keyframe(0.33333334f, 114.466f, 166.10667f, 166.10666f),
            new Keyframe(0.6f, 126.924f, 104.77502f, 104.77501f),
            new Keyframe(0.6666667f, 149.391f, 242.8199f, 242.82f),
            new Keyframe(0.73333335f, 159.3f, -0.000012411489f, 0f)
        });
    }

    /// <summary>원작 GreatSword_Attack3의 시간·접선을 보존하고 +Y 조준 위치를 +X 기준으로 변환</summary>
    private static AnimationClip EnsureAttack3()
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Attack3.anim");
        if (existing != null) return existing;

        // 위치 (x, y)를 (y, -x)로 회전하며 현재 검 Sprite의 +90도 보정과 원작 Z 회전 유지
        return CreateClip("Attack3", new[]
        {
            new Keyframe(0f, 0.145f, 0f, 0f),
            new Keyframe(0.13333334f, 0.265f, -1.799066e-7f, 0f),
            new Keyframe(0.16666667f, -0.111f, -9.391251f, -9.391251f),
            new Keyframe(0.2f, -0.36108333f, -5.6250033f, -5.6250005f),
            new Keyframe(0.23333333f, -0.486f, -1.971922e-7f, 0f),
            new Keyframe(0.36666667f, -0.269f, 5.2079997f, 5.208f),
            new Keyframe(0.4f, 0.422f, -8.366094e-7f, 0f),
            new Keyframe(0.43333334f, -0.407f, -0.64364153f, -0.6436366f),
            new Keyframe(0.76666665f, -0.466f, 5.316378e-8f, float.NegativeInfinity),
            new Keyframe(0.8f, -0.466f, 0f, 0f),
            new Keyframe(0.93333334f, 0.145f, -0.0000016163781f, 0f)
        }, new[]
        {
            new Keyframe(0f, -0.051f, 0f, 0f),
            new Keyframe(0.13333334f, -0.199f, -1.596f, -1.596f),
            new Keyframe(0.16666667f, -0.317f, 0.0000023860928f, 0f),
            new Keyframe(0.2f, -0.2873f, 1.4850001f, 1.4850001f),
            new Keyframe(0.23333333f, -0.218f, 1.6631993f, 1.6631998f),
            new Keyframe(0.36666667f, 0.167f, 4.031993e-7f, 0f),
            new Keyframe(0.4f, 0.019f, -8.879999f, -8.88f),
            new Keyframe(0.43333334f, -0.445f, -0.109086856f, -0.10909124f),
            new Keyframe(0.76666665f, -0.455f, -2.6912215e-9f, float.PositiveInfinity),
            new Keyframe(0.8f, -0.455f, 0f, 0f),
            new Keyframe(0.93333334f, -0.051f, -9.508924e-7f, 0f)
        }, new[]
        {
            new Keyframe(0f, 159.3f, 0f, 0f),
            new Keyframe(0.13333334f, 142.883f, -394.00815f, -394.00815f),
            new Keyframe(0.16666667f, 32.331f, -1888.0695f, -1888.0706f),
            new Keyframe(0.2f, 0.8631573f, -870.8552f, -870.85504f),
            new Keyframe(0.23333333f, -25.726f, -638.1398f, -638.1398f),
            new Keyframe(0.36666667f, -208.331f, -1710.3662f, -1710.3658f),
            new Keyframe(0.4f, -310.787f, -4072.3203f, -4072.3203f),
            new Keyframe(0.43333334f, -479.819f, -106.659325f, -106.65828f),
            new Keyframe(0.76666665f, -489.596f, 0.0000073113292f, float.PositiveInfinity),
            new Keyframe(0.8f, 230.404f, 0f, 0f),
            new Keyframe(0.93333334f, 159.3f, -0.00008654917f, 0f)
        });
    }

    /// <summary>외부 이벤트·이동 판정·Sprite 참조 없이 모션 자식의 위치와 Euler 곡선만 저장</summary>
    private static AnimationClip CreateClip(string clipName, Keyframe[] x, Keyframe[] y, Keyframe[] angle)
    {
        // 원작의 연속 키는 그대로 사용하고 편집 눈금만 프로젝트의 60fps 기준으로 지정
        var clip = new AnimationClip { name = clipName, frameRate = 60f, legacy = false };
        float duration = angle[angle.Length - 1].time;
        var zero = new[] { new Keyframe(0f, 0f), new Keyframe(duration, 0f) };
        SetCurve(clip, "m_LocalPosition.x", x);
        SetCurve(clip, "m_LocalPosition.y", y);
        SetCurve(clip, "m_LocalPosition.z", zero);
        SetCurve(clip, "localEulerAnglesRaw.x", zero);
        SetCurve(clip, "localEulerAnglesRaw.y", zero);
        SetCurve(clip, "localEulerAnglesRaw.z", angle);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

        // 한 번의 공격은 마지막 키에 머무르며 종료·취소 복귀는 AttackController에서 결정
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, Folder + "/" + clipName + ".anim");
        return clip;
    }

    /// <summary>원본의 자유 접선과 계단형 무한 접선을 유지한 단일 Transform 곡선 등록</summary>
    private static void SetCurve(AnimationClip clip, string property, Keyframe[] keys)
    {
        var curve = new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(MotionPath, typeof(Transform), property), curve);
    }

    /// <summary>Unity의 에셋 GUID를 만들며 현재 프로젝트 아래 누락된 폴더만 준비</summary>
    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        int separator = folder.LastIndexOf('/');
        string parent = folder.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(separator + 1));
    }
}

