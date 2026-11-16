using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>세피리아 대검의 위치·회전 키를 현재 고정 검의 독립 Animator 에셋으로 준비</summary>
public static class PlayerWeaponAnimationSetup
{
    private const string Folder = "Assets/Art/Player/Animations/Weapon"; // 현재 프로젝트에서만 사용하는 검 모션 에셋 경로
    private const string MotionPath = "WeaponMotion"; // 조준 부모의 Animator가 움직일 단일 자식 경로

    /// <summary>클립 곡선을 확정 사양으로 갱신하고 기존 GUID를 보존한 컨트롤러 반환</summary>
    public static RuntimeAnimatorController EnsureAssets()
    {
        // 프로젝트 내부 폴더를 준비하고 같은 에셋의 곡선만 갱신
        EnsureFolder(Folder);
        AnimationClip[] clips = { EnsureIdle(), EnsureAttack1(), EnsureAttack2(), EnsureAttack3(), EnsureWait2(), EnsureWait3() };
        string controllerPath = Folder + "/PlayerWeapon.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        // 기존 상태와 GUID를 유지하고 원작의 콤보 대기 자세 두 개만 누락 시 추가
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimatorState state = null;
            foreach (ChildAnimatorState child in machine.states)
                if (child.state.name == clips[i].name) state = child.state;
            if (state == null) state = machine.AddState(clips[i].name, new Vector3(260f, 60f + i * 80f, 0f));
            state.motion = clips[i];
            state.writeDefaultValues = false;
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    /// <summary>취소와 공격 종료 때 원작 대검의 호흡 대기 자세를 재생할 클립 준비</summary>
    private static AnimationClip EnsureIdle()
    {
        // 원작 대검 대기의 같은 위치·각도와 1초 호흡 곡선을 +X 조준 좌표로 변환
        AnimationClip clip = CreateClip("Idle", new[]
        {
            new Keyframe(0f, 0.145f), new Keyframe(0.5f, 0.147f), new Keyframe(1f, 0.145f)
        }, new[]
        {
            new Keyframe(0f, -0.051f), new Keyframe(0.5f, -0.037f), new Keyframe(1f, -0.051f)
        }, new[] { new Keyframe(0f, 159.3f), new Keyframe(1f, 159.3f) });

        // 공격 종료 후 원작처럼 대기 호흡을 반복하며 자기 피벗만 갱신
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    /// <summary>원작 GreatSword_Attack1의 시간·접선을 보존하고 +Y 조준 위치를 +X 기준으로 변환</summary>
    private static AnimationClip EnsureAttack1()
    {
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

    /// <summary>확정 R14의 원작1·2타 표본을 같은 포즈·순서로 연결한3타 클립 준비</summary>
    private static AnimationClip EnsureAttack3()
    {
        // R14의 1타1~24와 2타16~45를 추가 대기 없이 54개 포즈로 직접 연결
        AnimationClip first = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Attack1.anim");
        AnimationClip second = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Attack2.anim");
        return CreateClip("Attack3", SplicePoses(first, second, "m_LocalPosition.x"),
            SplicePoses(first, second, "m_LocalPosition.y"), SplicePoses(first, second, "localEulerAnglesRaw.z"));
    }

    /// <summary>원작 GreatSword_New_WaitForAttack2의 1타 종료 후 오른쪽 대기 전환 준비</summary>
    private static AnimationClip EnsureWait2()
    {
        // 원작 1/6초 전환과 자유 접선을 유지하며 위치만 +X 조준 기준으로 회전
        return CreateClip("Wait2", new[]
        {
            new Keyframe(0f, -0.65f, 0f, 0f), new Keyframe(0.16666667f, -0.54499996f, -1.126528e-7f, 0f)
        }, new[]
        {
            new Keyframe(0f, -0.4f, 0f, 0f), new Keyframe(0.16666667f, -0.42000002f, 2.1457685e-8f, 0f)
        }, new[]
        {
            new Keyframe(0f, -126.396f, 0f, 0f), new Keyframe(0.16666667f, -131.60126f, 5.5846313e-6f, 0f)
        });
    }

    /// <summary>원작 GreatSword_New_WaitForAttack3의 2타 종료 후 왼쪽 대기 전환 준비</summary>
    private static AnimationClip EnsureWait3()
    {
        // 원작 1/6초 전환과 자유 접선을 유지하며 위치만 +X 조준 기준으로 회전
        return CreateClip("Wait3", new[]
        {
            new Keyframe(0f, 0.145f, 0f, 0f), new Keyframe(0.16666667f, 0.149f, -4.291543e-9f, 0f)
        }, new[]
        {
            new Keyframe(0f, -0.051f, 0f, 0f), new Keyframe(0.16666667f, -0.077f, 2.7894975e-8f, 0f)
        }, new[]
        {
            new Keyframe(0f, 159.3f, 0f, 0f), new Keyframe(0.16666667f, 153.565f, 6.152988e-6f, 0f)
        });
    }

    /// <summary>원작1타24개·2타30개의 표본과 사이의 선형 보간으로3타 곡선 생성</summary>
    private static Keyframe[] SplicePoses(AnimationClip first, AnimationClip second, string property)
    {
        // 확정 프레임 표와 같은 시각에서 원작 자유 접선 곡선의 포즈를 조회
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(MotionPath, typeof(Transform), property);
        AnimationCurve firstCurve = AnimationUtility.GetEditorCurve(first, binding);
        AnimationCurve secondCurve = AnimationUtility.GetEditorCurve(second, binding);
        var keys = new Keyframe[54];
        for (int i = 0; i < keys.Length; i++)
        {
            float sourceTime = (i < 24 ? i : i - 24 + 15) / 60f;
            float value = (i < 24 ? firstCurve : secondCurve).Evaluate(sourceTime);
            keys[i] = new Keyframe(i / 60f, value);
        }

        // 각 표본은 그대로 통과하고 고주사율에서는 두 포즈 사이를 시간 기준으로 연결
        for (int i = 0; i < keys.Length; i++)
        {
            Keyframe key = keys[i];
            key.inTangent = i == 0 ? 0f : (keys[i].value - keys[i - 1].value) * 60f;
            key.outTangent = i == keys.Length - 1 ? 0f : (keys[i + 1].value - keys[i].value) * 60f;
            keys[i] = key;
        }
        return keys;
    }

    /// <summary>외부 이벤트·이동 판정·Sprite 참조 없이 모션 자식의 위치와 Euler 곡선만 저장</summary>
    private static AnimationClip CreateClip(string clipName, Keyframe[] x, Keyframe[] y, Keyframe[] angle)
    {
        // 원작의 연속 키는 그대로 사용하고 편집 눈금만 프로젝트의 60fps 기준으로 지정
        string path = Folder + "/" + clipName + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool create = clip == null;
        if (create) clip = new AnimationClip();
        clip.ClearCurves();
        clip.name = clipName;
        clip.frameRate = 60f;
        clip.legacy = false;
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
        if (create) AssetDatabase.CreateAsset(clip, path);
        EditorUtility.SetDirty(clip);
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

