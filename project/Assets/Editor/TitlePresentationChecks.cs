using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>격리 저장으로 Title 이미지·십자/틈새 빛과 기존 슬롯 진입·복귀 확인</summary>
public static class TitlePresentationChecks
{
    private const string Output = "Logs/TitlePresentation"; // 실제 Title 화면과 연속 프레임 저장 위치
    private static readonly FieldInfo IntroStartedAt = typeof(UI_Title).GetField("_introStartedAt", BindingFlags.Instance | BindingFlags.NonPublic); // 재생 위상별 실제 화면을 촬영할 시작 시각
    private static readonly FieldInfo MenuStyle = typeof(UI_Title).GetField("_menuStyle", BindingFlags.Instance | BindingFlags.NonPublic); // 첫 OnGUI에서 준비한 메뉴 상태 검사
    private static readonly FieldInfo ShowSlots = typeof(UI_Title).GetField("_showSlots", BindingFlags.Instance | BindingFlags.NonPublic); // 기존 시작 버튼의 슬롯 목록 전환 검사
    private static readonly FieldInfo SlotStyle = typeof(UI_Title).GetField("_slotStyle", BindingFlags.Instance | BindingFlags.NonPublic); // 첫 GUI 호출에서 준비한 슬롯 프레임과 경계 검사
    private static readonly FieldInfo SlotTextStyle = typeof(UI_Title).GetField("_slotTextStyle", BindingFlags.Instance | BindingFlags.NonPublic); // 슬롯 안내·확인창 글자의 실제 굵기 검사
    private static readonly FieldInfo SlotHeadingStyle = typeof(UI_Title).GetField("_slotHeadingStyle", BindingFlags.Instance | BindingFlags.NonPublic); // saveslot 제목의 실제 굵기 검사
    private static readonly FieldInfo BackAction = typeof(UI_Title).GetField("_backAction", BindingFlags.Instance | BindingFlags.NonPublic); // 슬롯 전용 Esc 액션의 활성 수명 검사
    private static readonly FieldInfo CurrentSave = typeof(SaveManager).GetField("current", BindingFlags.Static | BindingFlags.NonPublic); // 메모리 표본 검사 뒤 원래 선택 참조 복원
    private static readonly FieldInfo SelectedSave = typeof(SaveManager).GetField("<SelectSlot>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic); // 메모리 표본 검사 뒤 원래 저장 슬롯 번호 복원
    private static readonly MethodInfo SlotRect = typeof(UI_Title).GetMethod("GetSlotRect", BindingFlags.Instance | BindingFlags.NonPublic); // 실제 가로 카드 배치와 다른 화면비의 클릭 영역 검사

    /// <summary>Title 전용 표시와 실제 시간 재생·슬롯 선택·재진입을 같은 검사 장치에서 확인</summary>
    public static IEnumerator Run(Action<string> passed)
    {
        // 실제 씬과 공통 저장 준비를 기다린 뒤 같은 수명의 참조 확보
        yield return Until(() => UnityEngine.Object.FindFirstObjectByType<UI_Title>()?.IsReady == true, "Title preparation"); // 슬롯 준비와 Title 페이드인 완료 대기
        UI_Title title = UnityEngine.Object.FindFirstObjectByType<UI_Title>();
        GameManager manager = GameManager.Instance;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chronoSaveSlotInfo") >= 0)
        {
            yield return CheckSlotInfo(title, manager, passed); // 실제 Esc와 슬롯 정보·글자 전용 검사 완료 대기
            yield break;
        }
        Require(title.TitleLogo != null && title.TitleLogoGlint != null && title.TitleFont != null,
            "The intact original logo, separate star VFX and title font must be connected.");
        Require(title.TitleLogo.width == 200 && title.TitleLogo.height == 80 && title.TitleLogoGlint.width == 256 && title.TitleLogoGlint.height == 256,
            "The original logo must remain200x80 and the independent star VFX must use a256x256 canvas.");
        foreach (Texture2D texture in new[] { title.TitleLogo, title.TitleLogoGlint })
        {
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            Require(importer != null && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled,
                "The original logo and independent star VFX require point filtering without mipmaps.");
        }
        Require(title.TitleFissureGlowFrames != null && title.TitleFissureGlowFrames.Length == 3 && title.FissureGlowCycle > 0f,
            "Title must connect three persistent fissure glow phases and a positive cycle.");
        foreach (Texture2D texture in title.TitleFissureGlowFrames)
        {
            Require(texture != null && texture.width == 200 && texture.height == 80, "Every fissure glow phase must match the200x80 logo canvas.");
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            Require(importer != null && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled,
                "Fissure glow phases require point filtering without mipmaps.");
        }
        Require(((Color32)title.BackgroundColor).Equals(new Color32(21, 21, 24, 255)), "Title must use the approved gray-black background.");
        Require(SaveManager.Slots.Length == 3 && SaveManager.IsReady && SaveManager.Current == null,
            "The title must prepare three slots without selecting a save.");
        passed("Title connects200x80 logo, independent256x256 star and three200x80 fissure phases with point imports, gray-black background and three unselected slots");

        // 첫 GUI 호출이 만든 스타일에서 모든 배율·상태의 상자 제거 확인
        yield return Until(() => MenuStyle.GetValue(title) != null, "Title menu GUI preparation"); // GUI.skin이 유효한 첫 OnGUI 호출 완료 대기
        GUIStyle menu = (GUIStyle)MenuStyle.GetValue(title);
        foreach (GUIStyleState state in new[] { menu.normal, menu.hover, menu.active, menu.focused, menu.onNormal, menu.onHover, menu.onActive, menu.onFocused })
            Require(state.background == null && (state.scaledBackgrounds == null || state.scaledBackgrounds.Length == 0),
                "Every menu GUI state must remain text-only, including high-DPI backgrounds.");
        passed("All eight menu states and high-DPI variants have no box background");

        // 실제 OnGUI 화면을 여러 위상에서 촬영하고 게임 정지 중에도 경과 확인
        Directory.CreateDirectory(Output);
        float previousScale = Time.timeScale;
        double previousStart = (double)IntroStartedAt.GetValue(title);
        try
        {
            Time.timeScale = 0f;
            foreach (float phase in new[] { -0.15f, title.GlintDuration * 0.5f, title.GlintDuration + 0.15f, title.GlintInterval + title.GlintDuration * 0.5f })
            {
                SetPhase(title, phase);
                yield return Delay(0.06f); // 설정한 위상으로 OnGUI가 실제 표시될 때까지 대기
                string name = phase < 0f ? "before" : phase < title.GlintDuration ? "peak" : phase < title.GlintInterval ? "after" : "repeat";
                yield return Capture(name); // 프레임 종료에 예약한 PNG 저장 완료 대기
            }
            // 원본 픽셀 대조용으로만 푸른 광을 잠시 숨기고 일반 재생에는 즉시 복원
            Texture2D[] glowFrames = title.TitleFissureGlowFrames;
            try
            {
                title.TitleFissureGlowFrames = null;
                SetPhase(title, title.GlintDuration + 0.15f);
                yield return Capture("original-only"); // 십자·틈새 VFX를 제외한 원본 실제 화면 저장 완료 대기
            }
            finally
            {
                title.TitleFissureGlowFrames = glowFrames;
            }
            CheckOriginalHighlight(title);

            // 십자가 없는 구간에서 게임 시간 정지 중의 상시 푸른 광만 따로 관찰
            SetPhase(title, 2.4f - Mathf.Max(0f, title.IntroDuration) - 0.6f);
            yield return Capture("fissure-a"); // 첫 번째 푸른 광 흐름 화면 저장 완료 대기
            yield return Delay(0.3f); // 실제 시간으로 밝기와 푸른 광 흐름 변화 관찰
            yield return Capture("fissure-b"); // 다음 흐름의 푸른 광 화면 저장 완료 대기
            Require(Time.timeScale == 0f, "Fissure glow observations must occur while gameplay time is stopped.");
            CheckFissureGlow(title);
            passed("Persistent blue fissure glow changes at timeScale0 while both samples stay inside the source glow mask and retain visible light");

            SetPhase(title, 0.2f);
            yield return Capture("paused-a"); // 정지 상태의 첫 실제 화면 저장 완료 대기
            double start = Time.realtimeSinceStartupAsDouble;
            yield return Delay(0.3f); // scaled 시간이 멈춘 상태에서 실제 시간만 진행
            yield return Capture("paused-b"); // 실시간 진행 뒤 회전·크기·alpha 화면 저장 완료 대기
            Require(Time.timeScale == 0f && Time.realtimeSinceStartupAsDouble - start >= 0.3,
                "The glint observation must occur while gameplay time remains stopped.");
            passed("Actual Title screenshots preserve the original cyan highlight without VFX and cover peak/disappearance/repeat plus real-time samples at timeScale0");

            // 반복을 포함한 실제 표시 연속 프레임을 GIF 검토용으로 보관
            SetPhase(title, -0.1f);
            StringBuilder timings = new StringBuilder("frame,realtimeSeconds\n");
            for (int index = 0; index < 60; index++)
            {
                timings.Append(index).Append(',').Append(Time.realtimeSinceStartupAsDouble.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                yield return Capture($"frame-{index:D3}"); // 각 실제 렌더 프레임의 PNG 저장 완료 대기
                yield return Delay(0.05f); // 다음 실제 시간의 확대·회전 상태 관찰
            }
            File.WriteAllText($"{Output}/frame-times.csv", timings.ToString());
            passed("Sixty actual OnGUI frames are saved for animated visual review");
        }
        finally
        {
            Time.timeScale = previousScale;
            if (title != null) IntroStartedAt.SetValue(title, previousStart);
        }

        // 기존 시작 API로 월드 진입 후 Title을 새로 준비하고 저장 참조 보존 확인
        title.ShowSlots();
        Require((bool)ShowSlots.GetValue(title), "The start action must still open the three-slot list.");
        yield return Capture("horizontal-slots"); // 실제 가로 슬롯 화면의 프레임·제목 배치 저장 완료 대기
        CheckSlotPresentation(title);
        passed("Three horizontal cards use the imported12px nine-slice frame, point filtering and centered bounds across720p/1080p/1440p plus wide/tall screens");

        // 사용자 파일에 쓰지 않는 메모리 표본으로 완료 슬롯 확인창과 취소 보존 검사
        SaveData originalSlot = SaveManager.Slots[2];
        SaveData originalCurrent = SaveManager.Current;
        SaveData completed = new SaveData(Path.Combine("Temp", "SaveSlotPresentation"), 2);
        completed.Set("DefeatedBosses", new List<string> { GameManager.SubmittedBossId });
        completed.Set("PlayTime", 90d);
        completed.Set("DeathCount", 2);
        try
        {
            SaveManager.Slots[2] = completed;
            title.SelectSlot(2);
            Require(title.IsRestartConfirmationOpen && title.SelectedSlot == 2 && !manager.IsSessionActive,
                "A completed horizontal card must request confirmation before starting a session.");
            yield return Capture("horizontal-slots-confirmation"); // 같은 프레임으로 표시한 완료 슬롯 확인창 저장 완료 대기
            title.ConfirmRestart(false);
            Require(!title.IsRestartConfirmationOpen && title.IsReady && !manager.IsSessionActive
                && ReferenceEquals(SaveManager.Current, originalCurrent) && ReferenceEquals(SaveManager.Slots[2], completed)
                && completed.Get<List<string>>("DefeatedBosses").Contains(GameManager.SubmittedBossId),
                "Cancelling a completed slot must preserve its data and leave the horizontal list usable.");
        }
        finally
        {
            SaveManager.Slots[2] = originalSlot;
        }
        passed("Completed card opens the restart overlay and cancel preserves save data, Current and usable horizontal slot selection");

        // 전투 코드가 동시 작업 중인 경우 명시한 실행에서만 Title 표시 범위를 검사
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chronoTitleVisualOnly") >= 0)
            yield break;
        title.SelectSlot(0);
        yield return Until(() => manager.Stage.IsReady && !manager.IsLoading && manager.CurrentPlayer != null, "slot world entry"); // 실제 선택 슬롯의 월드 생성 완료 대기
        SaveData current = SaveManager.Current;
        Require(current != null && manager.IsSessionActive, "Slot selection must still create the active session.");
        manager.ReturnToTitle();
        yield return Until(() => SceneManager.GetActiveScene().name == "Title" && !manager.IsLoading && UnityEngine.Object.FindFirstObjectByType<UI_Title>()?.IsReady == true,
            "Title return"); // 월드 정리와 새 Title 자체 준비 완료 대기
        UI_Title returned = UnityEngine.Object.FindFirstObjectByType<UI_Title>();
        Require(title == null && returned.TitleLogo != null && returned.TitleLogoGlint != null
            && returned.TitleFissureGlowFrames != null && returned.TitleFissureGlowFrames.Length == 3 && !(bool)ShowSlots.GetValue(returned),
            "Returning to Title must recreate logo presentation and reset the menu.");
        Require(ReferenceEquals(current, SaveManager.Current) && !manager.IsSessionActive && manager.CurrentPlayer == null,
            "Title return must preserve Current and release the prior session.");
        yield return Capture("returned-title"); // 새 Title의 로고·세 메뉴 표시 저장 완료 대기
        passed("Start opens slots, slot0 enters the real world, and Title return resets presentation while preserving Current");
    }

    /// <summary>검사 키보드의 실제 Esc와 메모리 저장 표본으로 슬롯 정보·굵은 글자·복귀 확인</summary>
    private static IEnumerator CheckSlotInfo(UI_Title title, GameManager manager, Action<string> passed)
    {
        // 실제 GUI가 준비한 스타일과 검사 전 저장·입력 상태 보관
        yield return Until(() => MenuStyle.GetValue(title) != null, "SaveSlot GUI preparation"); // 최초 OnGUI의 스타일 생성 완료 대기
        Directory.CreateDirectory(Output);
        GUIStyle[] styles = { (GUIStyle)MenuStyle.GetValue(title), (GUIStyle)SlotStyle.GetValue(title),
            (GUIStyle)SlotTextStyle.GetValue(title), (GUIStyle)SlotHeadingStyle.GetValue(title) };
        foreach (GUIStyle style in styles)
            Require(style.fontStyle == FontStyle.Bold && style.alignment == TextAnchor.MiddleCenter,
                "Title menus, slot data, heading and confirmation must inherit bold centered text.");
        InputAction back = (InputAction)BackAction.GetValue(title);
        SaveData[] originalSlots = (SaveData[])SaveManager.Slots.Clone();
        SaveData originalCurrent = SaveManager.Current;
        int originalSelected = SaveManager.SelectSlot;
        bool originalReady = title.IsReady;
        bool originalLoading = manager.IsLoading;
        Keyboard originalKeyboard = Keyboard.current;
        InputSettings.BackgroundBehavior originalBackground = InputSystem.settings.backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            // 사용자 저장 파일을 쓰지 않는 빈칸·진행 중·완료 표본 구성
            SaveData progress = new SaveData(Path.Combine("Temp", "SaveSlotInfo"), 1);
            progress.Set("DefeatedBosses", new List<string>());
            progress.Set("PlayTime", 72d * 60d);
            progress.Set("DeathCount", 13);
            SaveData completed = new SaveData(Path.Combine("Temp", "SaveSlotInfo"), 2);
            completed.Set("DefeatedBosses", new List<string> { GameManager.SubmittedBossId });
            completed.Set("PlayTime", 125d * 60d);
            completed.Set("DeathCount", 27);
            SaveManager.Slots[0] = null;
            SaveManager.Slots[1] = progress;
            SaveManager.Slots[2] = completed;
            SaveManager.SetCurrent(1);
            Require(!back.enabled && !manager.IsSessionActive, "Esc must begin disabled on the Title menu without a session.");

            // 동일 글자의 Normal·Bold 실제 화면을 남겨 합성 굵기와 항목 간 빈줄 검토
            foreach (GUIStyle style in styles) style.fontStyle = FontStyle.Normal;
            yield return Delay(0.06f); // 변경된 글꼴 스타일로 실제 메뉴 표시 대기
            yield return Capture("menu-normal"); // 같은 메뉴의 보통 굵기 화면 저장 완료 대기
            foreach (GUIStyle style in styles) style.fontStyle = FontStyle.Bold;
            yield return Delay(0.06f); // 굵게 변경한 실제 메뉴 표시 대기
            yield return Capture("menu-bold"); // 같은 메뉴의 굵은 화면 저장 완료 대기
            title.ShowSlots();
            foreach (GUIStyle style in styles) style.fontStyle = FontStyle.Normal;
            yield return Delay(0.06f); // 세 저장 표본을 보통 굵기로 표시 대기
            yield return Capture("slots-normal"); // 슬롯 정보의 보통 굵기 화면 저장 완료 대기
            foreach (GUIStyle style in styles) style.fontStyle = FontStyle.Bold;
            yield return Delay(0.06f); // 빈줄과 굵은 글자를 적용한 세 저장 표본 표시 대기
            yield return Capture("slots-bold"); // 슬롯 정보의 최종 굵은 화면 저장 완료 대기
            yield return Capture("horizontal-slots"); // 기존 프레임·확장 카드 검사에 사용할 화면 저장 완료 대기
            CheckSlotPresentation(title);
            passed("Bold Title and save-slot styles preserve centered144x206 cards; normal/bold screenshots show empty,72-minute and completed125-minute data samples");

            // 실제 Input System Esc 누름으로 슬롯을 닫고 저장 선택·표본 보존 확인
            Require(back.enabled && (bool)ShowSlots.GetValue(title), "Showing slots must enable its Esc action.");
            yield return EscapeKey(keyboard, true); // 검사 장치의 실제 Esc performed 전달 완료 대기
            Require(!(bool)ShowSlots.GetValue(title) && !title.IsRestartConfirmationOpen && title.SelectedSlot == -1 && !back.enabled,
                "Esc from the slot list must return to the Title menu and disable only its UI action.");
            Require(ReferenceEquals(SaveManager.Current, progress) && SaveManager.SelectSlot == 1 && !manager.IsSessionActive,
                "Esc must preserve a non-null Current save, selected save index and inactive session.");
            yield return EscapeKey(keyboard, false); // 다음 독립 Esc 누름을 위한 해제 완료 대기
            yield return EscapeKey(keyboard, true); // Title 메뉴에서 처리하지 않을 Esc 누름 전달 완료 대기
            Require(!(bool)ShowSlots.GetValue(title) && title.SelectedSlot == -1 && !back.enabled,
                "Esc on the Title menu must leave its presentation and selection unchanged.");
            passed("Real Esc closes slots to Title, remains inactive on the menu and preserves non-null Current and save selection");

            // 완료 확인창을 두 진입 경로로 열고 Esc가 승인 없이 메뉴까지 닫는지 확인
            yield return EscapeKey(keyboard, false); // 완료 확인창 검사 전 키 해제 대기
            title.ShowSlots();
            title.SelectSlot(2);
            Require(title.IsRestartConfirmationOpen && back.enabled, "Completed slot must open its confirmation with Esc enabled.");
            yield return Capture("slots-confirmation-bold"); // 굵은 확인 안내·새 게임·취소 글자 저장 완료 대기
            yield return EscapeKey(keyboard, true); // 확인창과 슬롯을 함께 닫을 실제 Esc 전달 완료 대기
            Require(!title.IsRestartConfirmationOpen && !(bool)ShowSlots.GetValue(title) && title.SelectedSlot == -1,
                "Esc must close the completed-slot confirmation and list together.");
            yield return EscapeKey(keyboard, false); // 직접 완료 슬롯 선택 API 검사 전 키 해제 대기
            title.SelectSlot(2);
            title.ConfirmRestart(false);
            Require(!title.IsRestartConfirmationOpen && !(bool)ShowSlots.GetValue(title) && !back.enabled,
                "Cancelling a confirmation opened directly from the menu must disable the hidden slot action.");
            title.SelectSlot(2);
            Require(title.IsRestartConfirmationOpen && back.enabled, "Direct completed selection must also enable Esc without ShowSlots.");
            yield return EscapeKey(keyboard, true); // 직접 열린 확인창의 실제 Esc 처리 완료 대기
            Require(!title.IsRestartConfirmationOpen && !(bool)ShowSlots.GetValue(title) && !back.enabled
                && ReferenceEquals(SaveManager.Slots[2], completed) && ReferenceEquals(SaveManager.Current, progress)
                && completed.Get<List<string>>("DefeatedBosses").Contains(GameManager.SubmittedBossId)
                && completed.Get<double>("PlayTime") == 125d * 60d && completed.Get<int>("DeathCount") == 27,
                "Both confirmation exits must preserve the completed data and prior Current without starting a new game.");
            passed("Esc from both displayed and directly opened completed confirmations returns to Title without overwriting completed or current data");

            // 준비·월드 로딩 동안의 Esc 누름을 나중에 재사용하지 않는지 확인
            yield return EscapeKey(keyboard, false); // 준비·로딩 입력 경계 검사 전 키 해제 대기
            title.ShowSlots();
            title.IsReady = false;
            yield return EscapeKey(keyboard, true); // Title 준비 중 무시할 Esc 전달 완료 대기
            Require((bool)ShowSlots.GetValue(title), "Esc must not close slots while Title preparation is incomplete.");
            title.IsReady = true;
            yield return EscapeKey(keyboard, false); // 별도 로딩 중 누름을 위한 키 해제 대기
            manager.IsLoading = true;
            yield return EscapeKey(keyboard, true); // 월드 로딩 중 무시할 Esc 전달 완료 대기
            Require((bool)ShowSlots.GetValue(title), "Esc must not close slots while a world load is in progress.");
            manager.IsLoading = false;
            yield return EscapeKey(keyboard, false); // 로딩 종료 후 새 누름을 위한 키 해제 대기
            yield return EscapeKey(keyboard, true); // 준비된 슬롯의 새 Esc 전달 완료 대기
            Require(!(bool)ShowSlots.GetValue(title) && !back.enabled, "Only a fresh allowed Esc may close the restored slot screen.");
            passed("Esc is ignored during incomplete preparation and loading, then a new allowed press returns to Title");

            // 비활성 화면의 입력 해제와 다시 켠 뒤 눌린 키의 잘못된 초기 소비 방지 확인
            yield return EscapeKey(keyboard, false); // 활성 수명 검사 전 키 해제 대기
            title.ShowSlots();
            title.enabled = false;
            Require(!back.enabled, "Disabling Title must disable its Esc action.");
            yield return EscapeKey(keyboard, true); // 비활성 화면에서 유지할 Esc 누름 전달 완료 대기
            Require((bool)ShowSlots.GetValue(title), "Disabled Title must not react to Esc.");
            title.enabled = true;
            InputSystem.Update();
            yield return null;
            Require(back.enabled && (bool)ShowSlots.GetValue(title), "Reenabled Title must not consume an already held Esc as a fresh press.");
            yield return EscapeKey(keyboard, false); // 재활성화 후 새 누름을 위한 키 해제 대기
            yield return EscapeKey(keyboard, true); // 재연결된 화면의 실제 새 Esc 전달 완료 대기
            Require(!(bool)ShowSlots.GetValue(title) && !back.enabled && ReferenceEquals(SaveManager.Current, progress),
                "Reenabled Title must handle a fresh Esc and preserve the selected data.");
            yield return Capture("esc-returned-title"); // 실제 Esc로 돌아온 굵은 Title 메뉴 저장 완료 대기
            passed("Title disable/reenable releases Esc, ignores an already held key and reconnects the next press without changing saves");
        }
        finally
        {
            // 검사 실패에도 원래 화면·저장 참조·입력 환경을 복원
            title.IsReady = originalReady;
            manager.IsLoading = originalLoading;
            if (!title.enabled) title.enabled = true;
            title.CloseSlots();
            foreach (GUIStyle style in styles) style.fontStyle = FontStyle.Bold;
            for (int index = 0; index < originalSlots.Length; index++) SaveManager.Slots[index] = originalSlots[index];
            CurrentSave.SetValue(null, originalCurrent);
            SelectedSave.SetValue(null, originalSelected);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.RemoveDevice(keyboard);
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
        }
    }

    /// <summary>검사 전용 Keyboard 장치의 Esc 상태를 실제 Input System에 전달</summary>
    private static IEnumerator EscapeKey(Keyboard keyboard, bool pressed)
    {
        InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(Key.Escape) : new KeyboardState());
        InputSystem.Update();
        yield return null;
    }

    /// <summary>실제 슬롯 스타일·임포트와 화면비별 가로 카드의 표시 범위 확인</summary>
    private static void CheckSlotPresentation(UI_Title title)
    {
        // 원본 PNG의 실제 Unity 임포트와 IMGUI에서 사용하는12px 경계 확인
        Require(title.SlotFrame != null && title.SlotFrame.width == 256 && title.SlotFrame.height == 64,
            "Title must connect the256x64 reusable slot frame.");
        string path = AssetDatabase.GetAssetPath(title.SlotFrame);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Require(importer != null && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
            && importer.wrapMode == TextureWrapMode.Clamp && importer.textureCompression == TextureImporterCompression.Uncompressed
            && importer.textureType == TextureImporterType.Sprite && sprite != null && sprite.border == new Vector4(12f, 12f, 12f, 12f),
            "The frame must import as an uncompressed clamp/point Sprite with12px borders and no mipmaps.");
        GUIStyle style = (GUIStyle)SlotStyle.GetValue(title);
        Require(style != null && style.border.left == 12 && style.border.right == 12 && style.border.top == 12 && style.border.bottom == 12,
            "IMGUI must use the source12px borders rather than stretch its corners.");
        foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused })
            Require(state.background == title.SlotFrame, "Every slot interaction state must retain the reusable frame.");

        // 구현에서 사용하는 카드 사각형을 읽고 겹침·동일 행·화면 가장자리 여유 확인
        Rect[] cards = new Rect[3];
        for (int index = 0; index < cards.Length; index++)
        {
            cards[index] = (Rect)SlotRect.Invoke(title, new object[] { index });
            Require(cards[index].width > 24f && cards[index].height > 24f && cards[index].xMin >= 0f && cards[index].xMax <= 640f
                && cards[index].yMin >= 0f && cards[index].yMax <= 360f,
                "Every card must leave room for the fixed corners and remain inside the logical640x360 screen.");
            Require(cards[index].height == 206f && cards[index].yMax == 310f,
                "Every card must extend downward to the former Back button bottom at310 logical pixels.");
            if (index > 0)
                Require(cards[index].y == cards[0].y && cards[index].size == cards[0].size && cards[index].xMin > cards[index - 1].xMax,
                    "All three save cards must share one horizontal row with a positive gap.");
        }
        Require(Mathf.Abs((cards[0].xMin + cards[2].xMax) * 0.5f - 320f) < 0.01f,
            "The three-card group must be centered horizontally.");
        foreach (Vector2 screen in new[] { new Vector2(1280f, 720f), new Vector2(1920f, 1080f), new Vector2(2560f, 1440f),
            new Vector2(2560f, 1080f), new Vector2(1080f, 1920f) })
        {
            float scale = Mathf.Min(screen.x / 640f, screen.y / 360f);
            Vector2 offset = (screen - new Vector2(640f, 360f) * scale) * 0.5f;
            Require(offset.x + cards[0].xMin * scale >= 0f && offset.x + cards[2].xMax * scale <= screen.x
                && offset.y + cards[0].yMin * scale >= 0f && offset.y + cards[0].yMax * scale <= screen.y,
                "The actual card rectangles must stay inside wide and tall letterboxed screens.");
        }

        // 스타일 속성 외에도 실제 캡처의 각 카드에 파랑 테두리·보라 모서리가 표시되는지 대조
        Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D screenImage = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Require(original.LoadImage(File.ReadAllBytes(path))
                && screenImage.LoadImage(File.ReadAllBytes($"{Output}/horizontal-slots.png")),
                "Frame source and actual horizontal slots screenshot must decode.");
            float scale = Mathf.Min(screenImage.width / 640f, screenImage.height / 360f);
            float offsetX = (screenImage.width - 640f * scale) * 0.5f;
            float offsetY = (screenImage.height - 360f * scale) * 0.5f;
            foreach (Rect card in cards)
            {
                foreach (Vector2 sample in new[] { new Vector2(3f, 32f), new Vector2(8f, 3f) })
                {
                    Color32 source = original.GetPixel((int)sample.x, original.height - 1 - (int)sample.y);
                    int x = Mathf.FloorToInt(offsetX + (card.x + sample.x + 0.5f) * scale);
                    int y = screenImage.height - 1 - Mathf.FloorToInt(offsetY + (card.y + sample.y + 0.5f) * scale);
                    Color32 actual = screenImage.GetPixel(x, y);
                    Require(Mathf.Abs(actual.r - source.r) <= 3 && Mathf.Abs(actual.g - source.g) <= 3 && Mathf.Abs(actual.b - source.b) <= 3,
                        "Every actual horizontal card must show its source blue border and dark-violet fixed corner.");
                }

                // 확장한 카드의 새 하단에도 원본 파랑 경계가 끊기지 않고 표시되는지 확인
                Color32 bottomSource = original.GetPixel(original.width / 2, 3);
                int bottomX = Mathf.FloorToInt(offsetX + card.center.x * scale);
                int bottomY = screenImage.height - 1 - Mathf.FloorToInt(offsetY + (card.yMax - 3.5f) * scale);
                Color32 bottom = screenImage.GetPixel(bottomX, bottomY);
                Require(Mathf.Abs(bottom.r - bottomSource.r) <= 3 && Mathf.Abs(bottom.g - bottomSource.g) <= 3 && Mathf.Abs(bottom.b - bottomSource.b) <= 3,
                    "The extended card must retain its source blue lower border at the new bottom.");
            }

            // 이전 뒤로 버튼의 글자 영역이 이제 가운데 카드의 빈 내부로 채워졌는지 대조
            Color32 panel = original.GetPixel(original.width / 2, original.height / 2);
            for (int logicalY = 286; logicalY <= 300; logicalY += 2)
            {
                for (int logicalX = 280; logicalX <= 360; logicalX += 2)
                {
                    int x = Mathf.FloorToInt(offsetX + (logicalX + 0.5f) * scale);
                    int y = screenImage.height - 1 - Mathf.FloorToInt(offsetY + (logicalY + 0.5f) * scale);
                    Color32 actual = screenImage.GetPixel(x, y);
                    Require(Mathf.Abs(actual.r - panel.r) <= 3 && Mathf.Abs(actual.g - panel.g) <= 3 && Mathf.Abs(actual.b - panel.b) <= 3,
                        "The former Back button text area must contain only the extended middle card interior.");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(screenImage);
        }
    }

    /// <summary>실제 시계의 시작점만 옮겨 같은 OnGUI 구현의 지정 위상을 촬영</summary>
    private static void SetPhase(UI_Title title, float phase)
    {
        IntroStartedAt.SetValue(title, Time.realtimeSinceStartupAsDouble - Mathf.Max(0f, title.IntroDuration) - 0.6f - phase);
    }

    /// <summary>원본 전용 캡처에서 칼날의 청록 하이라이트와 실제 메뉴 무테두리 확인</summary>
    private static void CheckOriginalHighlight(UI_Title title)
    {
        // 임포터 설정을 바꾸지 않고 원본 PNG와 실제 화면을 임시 디코드
        Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D screen = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Require(original.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(title.TitleLogo)))
                && screen.LoadImage(File.ReadAllBytes($"{Output}/original-only.png")), "Original logo and captured Title must decode.");
            float scale = Mathf.Min(screen.width / 1920f, screen.height / 1080f);
            float offsetX = (screen.width - 1920f * scale) * 0.5f;
            float offsetY = (screen.height - 1080f * scale) * 0.5f;
            int checkedPixels = 0;

            // 스타일 속성 외에도 실제 세 메뉴의 빈 영역에 상자가 그려지지 않는지 대조
            for (int menu = 0; menu < 3; menu++)
            {
                foreach (float x in new[] { 696f, 1224f })
                {
                    int screenX = Mathf.FloorToInt(offsetX + x * scale);
                    int screenY = screen.height - 1 - Mathf.FloorToInt(offsetY + (704.4f + menu * 72f) * scale);
                    Color32 actual = screen.GetPixel(screenX, screenY);
                    Require(actual.Equals((Color32)title.BackgroundColor), "Actual menu pixels must contain only the Title background outside the letters.");
                }
            }

            // 원본 십자 영역의 불투명 청록 픽셀 중심을 화면의 같은 좌표와 대조
            for (int y = 23; y <= 41; y++)
            {
                for (int x = 96; x <= 168; x++)
                {
                    Color32 source = original.GetPixel(x, original.height - 1 - y);
                    if (source.a != 255 || source.b < source.r + 20 || source.g < source.r + 20) continue;
                    int screenX = Mathf.FloorToInt(offsetX + (360f + (x + 0.5f) * 6f) * scale);
                    int screenY = screen.height - 1 - Mathf.FloorToInt(offsetY + (141f + (y + 0.5f) * 6f) * scale);
                    Color32 actual = screen.GetPixel(screenX, screenY);
                    Require(Mathf.Abs(actual.r - source.r) <= 8 && Mathf.Abs(actual.g - source.g) <= 8 && Mathf.Abs(actual.b - source.b) <= 8,
                        $"The original highlight pixel({x},{y}) must remain visible after the independent VFX disappears.");
                    checkedPixels++;
                }
            }
            Require(checkedPixels >= 20, "Original logo must retain enough opaque cyan highlight samples.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(screen);
        }
    }

    /// <summary>십자 없는 두 실제 화면에서 푸른 광의 지속·변화와 틈새 외부 픽셀 보존 확인</summary>
    private static void CheckFissureGlow(UI_Title title)
    {
        // 원본 전용 화면과 정지 중 실제 두 위상을 디코드하고 파생 이미지의 표시 범위 구성
        Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D first = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D second = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D phase = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Require(original.LoadImage(File.ReadAllBytes($"{Output}/original-only.png"))
                && first.LoadImage(File.ReadAllBytes($"{Output}/fissure-a.png"))
                && second.LoadImage(File.ReadAllBytes($"{Output}/fissure-b.png")), "Actual fissure glow screenshots must decode.");
            Require(original.width == first.width && original.height == first.height
                && first.width == second.width && first.height == second.height, "Fissure samples must share one actual screen size.");
            bool[] mask = new bool[200 * 80];
            foreach (Texture2D frame in title.TitleFissureGlowFrames)
            {
                Require(phase.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame))), "Fissure source phase must decode.");
                for (int y = 0; y < 80; y++)
                    for (int x = 0; x < 200; x++)
                        mask[y * 200 + x] |= ((Color32)phase.GetPixel(x, 79 - y)).a > 0;
            }

            // 로고의 각 native 픽셀 중심을 대조해 선 주변에서만 지속·변화하는지 확인
            float scale = Mathf.Min(original.width / 1920f, original.height / 1080f);
            float offsetX = (original.width - 1920f * scale) * 0.5f;
            float offsetY = (original.height - 1080f * scale) * 0.5f;
            int litFirst = 0;
            int litSecond = 0;
            int changed = 0;
            for (int y = 0; y < 80; y++)
            {
                for (int x = 0; x < 200; x++)
                {
                    int screenX = Mathf.FloorToInt(offsetX + (360f + (x + 0.5f) * 6f) * scale);
                    int screenY = original.height - 1 - Mathf.FloorToInt(offsetY + (141f + (y + 0.5f) * 6f) * scale);
                    Color32 source = original.GetPixel(screenX, screenY);
                    Color32 a = first.GetPixel(screenX, screenY);
                    Color32 b = second.GetPixel(screenX, screenY);
                    int firstChange = Mathf.Max(Mathf.Abs(a.r - source.r), Mathf.Abs(a.g - source.g), Mathf.Abs(a.b - source.b));
                    int secondChange = Mathf.Max(Mathf.Abs(b.r - source.r), Mathf.Abs(b.g - source.g), Mathf.Abs(b.b - source.b));
                    if (!mask[y * 200 + x])
                        Require(firstChange <= 2 && secondChange <= 2, "Persistent glow must not change the logo outside its narrow fissure mask.");
                    else
                    {
                        if (firstChange > 4 && a.b > a.r + 10) litFirst++;
                        if (secondChange > 4 && b.b > b.r + 10) litSecond++;
                        if (Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)) > 3) changed++;
                    }
                }
            }
            Require(litFirst >= 30 && litSecond >= 30 && changed >= 20,
                "Both paused samples must retain blue fissure light and show a visible flow or brightness change.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(phase);
        }
    }

    /// <summary>실제 프레임 종료 화면 캡처가 PNG로 저장될 때까지 대기</summary>
    private static IEnumerator Capture(string name)
    {
        string path = Path.GetFullPath($"{Output}/{name}.png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return Until(() => File.Exists(path) && new FileInfo(path).Length > 0, $"screenshot {name}"); // 이전 위상으로 바꾸기 전에 화면 파일 저장 완료 대기
        yield return null;
    }

    /// <summary>편집기 검사 루프에서 실제 시간 대기</summary>
    private static IEnumerator Delay(float seconds)
    {
        double end = Time.realtimeSinceStartupAsDouble + seconds;
        while (Time.realtimeSinceStartupAsDouble < end) yield return null;
    }

    /// <summary>무한 대기 없이 실제 준비 조건 확인</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double end = Time.realtimeSinceStartupAsDouble + 30d;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException($"Timed out waiting for {description}.");
            yield return null;
        }
    }

    /// <summary>실제 관측 조건이 다르면 검사 실패로 보고</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
