using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>현재 Play 세션의 실제 Slash를 같은 경로로 반복하며 구간·확대·화면 효과 조정</summary>
public class SlashPresentationTuning : EditorWindow
{
    private static readonly string[] SkillFields = { "SlashFastZoomDuration", "SlashHoldDuration", "SlashGlintDuration", "SlashSheatheDuration", "SlashImpactDelay", "SlashZoomOutDuration", "SlashFastZoomCurve", "SlashSlowZoomCurve", "SlashZoomOutCurve" }; // 명시적 저장 대상인 연출 시간·곡선 목록
    private static readonly string[] CameraFields = { "SlashFastZoom", "SlashZoom" }; // 빠른 확대 종료와 느린 확대 종료의 조절 대상
    private static readonly string[] FxFields = { "SpaceSlashDuration", "SpaceDisplacementPixels", "CutGlowPixels", "BladeGlintSize", "Volume" }; // 공간 어긋남·칼날 빛·음량의 조절 대상
    private static readonly GUIContent[] CameraLabels = { new GUIContent("빠른 확대 끝 배율"), new GUIContent("느린 확대 끝 배율") }; // 직교 크기가 작을수록 더 확대되는 두 목표의 표시 이름
    private static readonly GUIContent[] FxLabels = { new GUIContent("공간참 복원 시간"), new GUIContent("화면 어긋남 픽셀"), new GUIContent("절단 빛 폭"), new GUIContent("칼날 빛 크기"), new GUIContent("효과음 음량") }; // 표현 조절값을 읽기 쉬운 이름으로 표시

    private PlayerCharacter player; // 연결 버튼을 누른 순간의 현재 Play Player
    private PlayerInputController input; // 연결 때 한 번 확보해 외부 입력 차단을 존중할 입력 서비스
    private SkillController skills; // 같은 Line 재생과 현재 연출 구간을 조회할 Player 능력
    private GameCamera gameCamera; // 연결 때 확보한 확대 목표 조절 대상
    private SlashPresentationFx fx; // 현재 Player가 재사용하는 칼날 빛·공간참 표현
    private DrawVfx drawVfx; // 경로 검격의 여운이 끝난 뒤 다음 반복을 시작할 표현
    private SkillController skillSaveTarget; // 실제 Stage의 PlayerPrefab에서 확보한 시간·곡선 저장 대상
    private GameCamera cameraSaveTarget; // 현재 씬 카메라의 실제 원본 프리팹에서 확보한 확대 저장 대상
    private SlashPresentationFx fxSaveTarget; // 실제 Stage의 PlayerPrefab에서 확보한 표현 저장 대상
    private SerializedObject skillObject; // Play 세션의 시간·곡선 값을 편집할 직렬화 대상
    private SerializedObject cameraObject; // Play 세션의 확대 목표를 편집할 직렬화 대상
    private SerializedObject fxObject; // Play 세션의 공간참·섬광을 편집할 직렬화 대상
    private SerializedProperty[] skillProperties; // 연결 때 확보해 재사용할 시간·곡선 프로퍼티
    private SerializedProperty[] cameraProperties; // 연결 때 확보해 재사용할 확대 목표 프로퍼티
    private SerializedProperty[] fxProperties; // 연결 때 확보해 재사용할 효과 표현 프로퍼티
    private SerializedProperty speedProperty; // 프리팹 저장에서 제외할 미리보기 배속 프로퍼티
    private Line? savedLine; // 버튼을 누른 순간 기억한 확정 또는 실행 중인 경로
    private bool repeat; // 현재 경로의 실제 Slash를 반복할지 선택한 상태
    private bool ownsPreview; // 창에서 시작한 Slash만 중단·완료 감시할 소유 기록
    private int previewAttackId; // 창에서 시작한 실행과 이후 외부 Slash를 구분할 공격 번호
    private double nextReplayAt; // 여운 뒤 다음 반복을 시작할 Editor 실제 시각
    private Vector2 scroll; // 좁은 창에서도 조절 항목을 모두 볼 스크롤 위치
    private string message = "Play에서 Line을 만든 뒤 현재 Player를 연결하세요"; // 시작 거부·저장·연결 결과 안내

    /// <summary>현재 세션의 Slash 연출을 직접 비교할 조절 창 열기</summary>
    [MenuItem("Tools/Chrono/Tune Slash")]
    public static void Open()
    {
        GetWindow<SlashPresentationTuning>("Slash 연출 조정").minSize = new Vector2(340f, 440f);
    }

    private void OnEnable()
    {
        // 스크립트 재로드 뒤 의도하지 않은 실제 피해 반복을 재개하지 않음
        repeat = false;
        ownsPreview = false;
        EditorApplication.update += UpdatePreview; // 실제 Slash 종료와 여운을 감시해 다음 반복 예약
        EditorApplication.playModeStateChanged += HandlePlayModeChanged; // Play 종료에서 이전 세션 참조 해제
    }

    /// <summary>수치 입력과 재생 결과를 같은 창에서 비교</summary>
    private void OnGUI()
    {
        // 미리보기가 현재 장면의 실제 전투 판정을 수행한다는 점을 명시
        EditorGUILayout.HelpBox("Play 모드의 실제 Slash입니다. 현재 장면 대상에 판정을 적용합니다. 조정값은 명시적으로 저장하지 않으면 Play 종료 시 사라집니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            if (GUILayout.Button("현재 Player 연결")) ConnectPlayer();
        if (!EditorApplication.isPlaying || player == null || skills == null)
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
            return;
        }

        // 현재 구간과 진행 상태를 숫자 해석 없이 확인
        EditorGUILayout.ObjectField("연결된 Player", player, typeof(PlayerCharacter), true);
        EditorGUILayout.LabelField("현재 구간", PhaseLabel());
        float total = skills.SlashTimelineDuration;
        Rect progressRect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(progressRect, total > 0f && skills.IsSlashing ? skills.SlashElapsed / total : 0f,
            skills.IsSlashing ? $"남은 시간 {skills.SlashRemaining:0.00}초" : "다음 재생 대기");

        // 배속은 다음 실행에서만 적용하며 프리팹 저장 대상에는 포함하지 않음
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("정상 1×")) SetSpeed(1f);
        if (GUILayout.Button("느리게 0.25×")) SetSpeed(0.25f);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField($"다음 재생 {skills.SlashPlaybackSpeed:0.##}× · 시간 변경은 다음 재생부터 적용", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("현재 Line 기억")) RememberLine();
        using (new EditorGUI.DisabledScope(skills.IsSlashing || EditorApplication.isPaused))
            if (GUILayout.Button("한번 재생")) PlayPreview(true);
        using (new EditorGUI.DisabledScope(!ownsPreview && !repeat))
            if (GUILayout.Button("중단")) StopPreview();
        EditorGUILayout.EndHorizontal();
        repeat = EditorGUILayout.ToggleLeft("같은 Line 반복", repeat);
        EditorGUILayout.LabelField(savedLine.HasValue ? "같은 위치의 Line을 기억했습니다" : "기억한 Line 없음", EditorStyles.miniLabel);
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);

        // 연결 때 확보한 프로퍼티만 수정해 원본 에셋의 자동 변경 방지
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawProperties(skillObject, skillProperties, "구간 시간과 속도 곡선");
        DrawProperties(cameraObject, cameraProperties, "확대 목표 · 작을수록 크게 보임", CameraLabels);
        DrawProperties(fxObject, fxProperties, "칼날 빛과 공간참", FxLabels);
        EditorGUILayout.Space();
        if (GUILayout.Button("현재 조정값을 프리팹에 저장")) SaveProperties();
        EditorGUILayout.LabelField("저장 버튼은 위 시간·곡선·확대·FX 값만 반영합니다. 미리보기 배속은 저장하지 않습니다.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
    }

    private void OnDisable()
    {
        // 창에서 시작한 재생과 반복만 끝내고 외부 정지·입력 차단은 유지
        StopPreview();
        EditorApplication.update -= UpdatePreview; // 닫힌 창의 반복 감시 해제
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged; // 닫힌 창의 Play 상태 알림 해제
    }

    /// <summary>현재 입력 서비스의 Player와 고정 컴포넌트 참조를 한 번 연결</summary>
    private void ConnectPlayer()
    {
        // 이전 대상에서 시작한 재생을 끝낸 뒤 새 세션 대상만 확보
        StopPreview();
        input = PlayerInputController.Instance;
        player = input != null ? input.Player : null;
        skills = player != null ? player.Skills : null;
        gameCamera = GameCamera.Instance;
        fx = player != null ? player.GetComponent<SlashPresentationFx>() : null;
        drawVfx = player != null ? player.GetComponent<DrawVfx>() : null;
        savedLine = null;
        if (skills == null || gameCamera == null || fx == null)
        {
            skills = null;
            message = "MainWorld의 Player 생성과 준비가 끝난 뒤 다시 연결하세요";
            return;
        }

        // 같은 세션의 조절 항목과 경로를 연결 경계에서 확보
        skillObject = new SerializedObject(skills);
        cameraObject = new SerializedObject(gameCamera);
        fxObject = new SerializedObject(fx);
        skillProperties = Array.ConvertAll(SkillFields, skillObject.FindProperty);
        cameraProperties = Array.ConvertAll(CameraFields, cameraObject.FindProperty);
        fxProperties = Array.ConvertAll(FxFields, fxObject.FindProperty);
        speedProperty = skillObject.FindProperty("SlashPlaybackSpeed");

        // Object.Instantiate로 원본 연결이 없는 Player도 실제 생성 프리팹에만 명시 저장
        GameManager manager = GameManager.Instance;
        PlayerCharacter prefab = manager != null && manager.Stage != null && manager.Stage.Player == player ? manager.Stage.PlayerPrefab : null;
        skillSaveTarget = ResolveSaveTarget(skills, prefab != null ? prefab.GetComponent<SkillController>() : null);
        fxSaveTarget = ResolveSaveTarget(fx, prefab != null ? prefab.GetComponent<SlashPresentationFx>() : null);
        cameraSaveTarget = ResolveSaveTarget(gameCamera, null);
        RememberLine();
        if (skillSaveTarget == null || cameraSaveTarget == null || fxSaveTarget == null)
            message += $"\n저장 대상 확인 필요 · 시간:{skillSaveTarget != null} / 카메라:{cameraSaveTarget != null} / FX:{fxSaveTarget != null}";
    }

    /// <summary>확정 또는 실행 중인 Line을 현재 위치 그대로 기억</summary>
    private void RememberLine()
    {
        // 새 Line이 없으면 이전 기억을 지우지 않고 다시 사용할 수 있도록 보존
        if (player == null || skills == null) return;
        if (player.CurrentLine.HasValue) savedLine = player.CurrentLine.Value;
        else if (skills.IsSlashing) savedLine = skills.Slasher.ActiveLine;
        message = savedLine.HasValue ? "Line을 기억했습니다. 한번 재생을 누르면 현재 조정값으로 실행합니다" : "먼저 게임에서 Draw로 Line을 확정한 뒤 현재 Line 기억을 누르세요";
    }

    /// <summary>외부 차단을 유지한 채 기억한 Line의 실제 Slash 시작 요청</summary>
    private void PlayPreview(bool rememberCurrent)
    {
        // 외부 일시 정지와 입력 차단을 풀어 미리보기를 강제하지 않음
        if (player == null || skills == null || EditorApplication.isPaused || (input != null && input.BlockInput))
        {
            repeat = false;
            message = "현재 Player 연결·Editor 일시 정지·외부 입력 차단 상태를 확인하세요";
            return;
        }
        if (rememberCurrent) RememberLine();
        if (!savedLine.HasValue || skills.IsSlashing || !player.CanMove || player.IsAttacking || player.IsDashing || player.IsDrawing
            || player.IsInputBlocked || skills.SlashCooldownRemaining > 0f)
        {
            repeat = false;
            message = "기억한 Line 또는 현재 Slash 시작 조건을 확인하세요. 생성·컷씬·Draw·공격·정지·대기 중에는 실행하지 않습니다";
            return;
        }

        // 기존 시작 API가 거부하면 이전 Line을 복구하고 반복도 중단
        Line? previousLine = player.CurrentLine;
        skills.SetLine(savedLine);
        if (!player.ApplySlash())
        {
            skills.SetLine(previousLine);
            repeat = false;
            message = "Slash 시작이 거부됐습니다. 연결된 발도·FX 자산과 Console의 준비 오류를 확인하세요";
            return;
        }
        ownsPreview = true;
        previewAttackId = skills.Slasher.AttackId;
        nextReplayAt = 0d;
        message = "재생 중입니다. 확대·대기·섬광·납도·공간참 순서를 비교하세요";
    }

    /// <summary>이 창이 시작한 실행만 중단하고 자기 반복 예약 정리</summary>
    private void StopPreview()
    {
        repeat = false;
        nextReplayAt = 0d;
        if (ownsPreview && skills != null && (!skills.IsSlashing || skills.Slasher.AttackId == previewAttackId)) skills.CancelSlash();
        ownsPreview = false;
    }

    /// <summary>프레임별 참조 재검색 없이 현재 실행과 표현 여운의 종료 감시</summary>
    private void UpdatePreview()
    {
        // Play 종료·대상 제거 뒤에는 이전 세션에서 실제 Slash를 재요청하지 않음
        if (!EditorApplication.isPlaying || player == null || skills == null)
        {
            repeat = false;
            ownsPreview = false;
            return;
        }
        if (ownsPreview && skills.IsSlashing && skills.Slasher.AttackId != previewAttackId)
        {
            ownsPreview = false;
            repeat = false;
            message = "외부에서 새 Slash를 시작해 이 창의 반복을 중단했습니다";
        }
        if (ownsPreview && !skills.IsSlashing && (fx == null || !fx.IsSpaceSlashPlaying) && (drawVfx == null || !drawVfx.IsSlashPlaying))
        {
            ownsPreview = false;
            nextReplayAt = EditorApplication.timeSinceStartup + 0.5d;
            message = repeat ? "여운 종료 후 같은 Line을 다시 재생합니다" : "재생을 마쳤습니다. 값을 바꾸고 같은 Line으로 비교할 수 있습니다";
        }
        if (repeat && !ownsPreview && nextReplayAt > 0d && EditorApplication.timeSinceStartup >= nextReplayAt)
            PlayPreview(false);
        Repaint();
    }

    /// <summary>Play 종료 경계에서 반복과 이전 대상 연결 제거</summary>
    private void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        StopPreview();
        player = null;
        skills = null;
        skillSaveTarget = null;
        cameraSaveTarget = null;
        fxSaveTarget = null;
        message = "Play를 종료했습니다. 다음 Play에서 현재 Player를 다시 연결하세요";
    }

    /// <summary>현재 연출 단계와 공간참 여운을 읽기 쉬운 이름으로 표시</summary>
    private string PhaseLabel()
    {
        if (!skills.IsSlashing) return fx != null && fx.IsSpaceSlashPlaying ? "공간참 · 화면 복원" : "대기";
        switch (skills.SlashPhase)
        {
            case SlashPresentationPhase.FastZoom: return "빠른 확대 · 부분 발도";
            case SlashPresentationPhase.SlowZoomHold: return "느린 확대 · 발도 유지";
            case SlashPresentationPhase.BladeGlint: return "칼날 중앙의 빛";
            case SlashPresentationPhase.Sheathing: return "칼집 닫는 중";
            case SlashPresentationPhase.ImpactWait: return "탁 · 줌 복귀 · 공간참 대기";
            default: return "대기";
        }
    }

    /// <summary>다음 재생의 Slash 배속만 현재 Play 인스턴스에 적용</summary>
    private void SetSpeed(float speed)
    {
        if (skillObject == null || speedProperty == null) return;
        skillObject.Update();
        speedProperty.floatValue = speed;
        skillObject.ApplyModifiedProperties();
    }

    /// <summary>연결 때 준비한 프로퍼티만 같은 Play 대상에 직접 편집</summary>
    private static void DrawProperties(SerializedObject target, SerializedProperty[] properties, string title, GUIContent[] labels = null)
    {
        if (target == null || properties == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        target.Update();
        for (int i = 0; i < properties.Length; i++)
        {
            if (properties[i] == null) continue;
            if (labels == null) EditorGUILayout.PropertyField(properties[i], true);
            else EditorGUILayout.PropertyField(properties[i], labels[i], true);
        }
        target.ApplyModifiedProperties();
    }

    /// <summary>사용자가 누른 저장 버튼에서 조절 대상으로 지정한 프로퍼티만 원본에 반영</summary>
    private void SaveProperties()
    {
        // 실제 생성 원본과 모든 저장 프로퍼티를 쓰기 전에 확인하고 임의의 경로 사용 방지
        if (!EditorApplication.isPlaying || player == null || skills == null || !CanCopyProperties(skillObject, skillProperties, skillSaveTarget)
            || !CanCopyProperties(cameraObject, cameraProperties, cameraSaveTarget) || !CanCopyProperties(fxObject, fxProperties, fxSaveTarget))
        {
            message = "실제 Player 생성 프리팹·씬 카메라 원본 또는 저장 필드를 확인할 수 없어 저장하지 않았습니다. Play 종료 전 연결 상태를 확인하세요";
            return;
        }

        // 전체 컴포넌트·재생 배속·전투 상태 대신 지정한 표현 값만 개별 적용
        try
        {
            CopyProperties(skillObject, skillProperties, skillSaveTarget);
            CopyProperties(cameraObject, cameraProperties, cameraSaveTarget);
            CopyProperties(fxObject, fxProperties, fxSaveTarget);
            GameObject playerAsset = skillSaveTarget.transform.root.gameObject;
            GameObject cameraAsset = cameraSaveTarget.transform.root.gameObject;
            GameObject fxAsset = fxSaveTarget.transform.root.gameObject;
            PrefabUtility.SavePrefabAsset(playerAsset);
            if (cameraAsset != playerAsset) PrefabUtility.SavePrefabAsset(cameraAsset);
            if (fxAsset != playerAsset && fxAsset != cameraAsset) PrefabUtility.SavePrefabAsset(fxAsset);
            AssetDatabase.SaveAssets();
            message = "현재 시간·곡선·확대·FX 조정값을 원본 프리팹에 저장했습니다. 배속과 전투 상태는 저장하지 않았습니다";
        }
        catch (Exception exception)
        {
            message = "프리팹 저장 중 일부 값의 적용 여부를 확인해야 합니다: " + exception.Message;
            Debug.LogException(exception);
        }
    }

    /// <summary>실제 런타임 원본이나 Stage의 생성 프리팹에서 저장 컴포넌트를 한 번 확보</summary>
    private static T ResolveSaveTarget<T>(T runtime, T creationPrefab) where T : Component
    {
        // Stage가 실제 생성한 Player는 명시된 생성 원본을 가장 먼저 사용
        if (creationPrefab != null && EditorUtility.IsPersistent(creationPrefab) && PrefabUtility.IsPartOfPrefabAsset(creationPrefab))
            return creationPrefab;
        T source = runtime != null ? PrefabUtility.GetCorrespondingObjectFromSource(runtime) : null;
        if (source != null) return source;

        // 씬 카메라는 해당 인스턴스가 참조하는 프리팹 안의 유일한 동종 컴포넌트만 사용
        string path = runtime != null ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(runtime.gameObject) : null;
        GameObject root = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        T[] candidates = root != null ? root.GetComponentsInChildren<T>(true) : null;
        if (candidates != null && candidates.Length == 1) return candidates[0];

        // Play 로드에서 인스턴스 연결이 사라졌으면 실제 씬 원본의 프리팹 참조 확인
        return ResolveSceneSaveTarget(runtime);
    }

    /// <summary>현재 런타임 씬의 원본을 별도 미리보기로 읽어 저장할 실제 프리팹 확인</summary>
    private static T ResolveSceneSaveTarget<T>(T runtime) where T : Component
    {
        // 씬 경로가 없거나 동종 대상이 여러 개면 임의의 프리팹을 선택하지 않음
        string scenePath = runtime != null ? runtime.gameObject.scene.path : null;
        if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return null;
        Scene preview = default;
        try
        {
            preview = EditorSceneManager.OpenPreviewScene(scenePath);
            T candidate = null;
            foreach (GameObject root in preview.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    if (candidate != null) return null;
                    candidate = component;
                }
            }
            T source = candidate != null ? PrefabUtility.GetCorrespondingObjectFromSource(candidate) : null;
            return source != null && EditorUtility.IsPersistent(source) && PrefabUtility.IsPartOfPrefabAsset(source) ? source : null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Slash 조정 창이 실제 씬의 프리팹 저장 대상을 확인하지 못했습니다: " + exception.Message);
            return null;
        }
        finally
        {
            // 현재 Play 씬을 바꾸지 않고 읽은 미리보기 씬만 반드시 정리
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    /// <summary>현재 저장 대상 또는 연결된 원본 프리팹의 실제 경로 조회</summary>
    private static string SourcePath(UnityEngine.Object target)
    {
        if (target != null && EditorUtility.IsPersistent(target)) return AssetDatabase.GetAssetPath(target);
        UnityEngine.Object source = target != null ? PrefabUtility.GetCorrespondingObjectFromSource(target) : null;
        return source != null ? AssetDatabase.GetAssetPath(source) : null;
    }

    /// <summary>지정한 표현 필드가 실제 원본 프리팹에 모두 있는지 저장 전에 확인</summary>
    private static bool CanCopyProperties(SerializedObject source, SerializedProperty[] properties, Component destination)
    {
        if (source == null || source.targetObject == null || properties == null || destination == null || !EditorUtility.IsPersistent(destination)
            || !PrefabUtility.IsPartOfPrefabAsset(destination) || string.IsNullOrEmpty(SourcePath(destination))) return false;
        source.Update();
        SerializedObject target = new SerializedObject(destination);
        foreach (SerializedProperty property in properties)
        {
            if (property == null) return false;
            SerializedProperty saved = target.FindProperty(property.propertyPath);
            if (saved == null || saved.propertyType != property.propertyType) return false;
        }
        return true;
    }

    /// <summary>런타임 연결 방식과 무관하게 지정한 표현 값만 실제 생성 원본에 복사</summary>
    private static void CopyProperties(SerializedObject source, SerializedProperty[] properties, Component destination)
    {
        source.Update();
        SerializedObject target = new SerializedObject(destination);
        target.Update();
        foreach (SerializedProperty property in properties) target.CopyFromSerializedProperty(property);
        target.ApplyModifiedProperties();
        EditorUtility.SetDirty(destination);
    }
}
