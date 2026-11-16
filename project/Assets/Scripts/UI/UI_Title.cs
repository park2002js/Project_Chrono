using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Title 준비와 3슬롯 표시, 완료 슬롯의 새 게임 확인</summary>
public class UI_Title : MonoBehaviour
{
    public int SelectedSlot = -1; // 사용자가 선택한 슬롯 번호, -1은 선택 전 상태
    public bool IsReady; // 슬롯과 Title 준비를 마쳐 선택 입력을 받을 수 있는 상태
    public bool IsRestartConfirmationOpen; // 완료 슬롯의 새 게임 확인창 표시 여부
    public string ErrorMessage; // Title 준비나 월드 진입 실패를 알릴 문구
    public float IntroDuration = 0.25f; // Title 페이드인과 초기 준비 대기 시간(초)
    public Texture2D TitleLogo; // 사용자 확정 원본을 그대로 표시할 Title 로고
    public Font TitleFont; // 세 메뉴 버튼과 슬롯 안내에 사용할 PF스타더스트
    public Texture2D TitleLogoGlint; // 원본 로고의 칼날 하이라이트 위에 겹칠 별도 십자 발광 VFX
    public Texture2D[] TitleFissureGlowFrames; // 로고 틈새 선 안팎의 푸른 광 흐름을 이어 표시할 고정 이미지
    public Texture2D SlotFrame; // 가로 저장 카드와 완료 기록 확인창에 재사용할 픽셀 프레임
    public Color BackgroundColor = new Color32(21, 21, 24, 255); // 화면 전체를 채울 회빛 검정 바탕
    public Vector2 GlintPivot = new Vector2(130.5f, 31.5f); // 200×80 원본에서 칼날 빛의 회전 중심
    [Min(0.01f)] public float GlintDuration = 1.25f; // 십자 빛 한 번의 확대·회전·소멸 시간
    [Min(0.01f)] public float GlintInterval = 3.2f; // 십자 빛 시작 사이의 실제 시간 간격
    public float GlintRotation = 90f; // 한 번 반짝이는 동안 회전할 각도
    [Min(0.01f)] public float FissureGlowCycle = 2.4f; // 푸른 광이 틈새를 따라 흐르는 한 주기의 실제 시간

    private GameManager _game; // 슬롯 시작·Title 재시도·종료를 요청할 전역 관리자
    private InputAction _backAction; // 슬롯 화면이 열린 동안에만 Esc 뒤로 입력을 받을 씬 수명 액션
    private bool _showSlots; // 시작 버튼 대신 슬롯 목록을 표시할지 구분
    private bool _isPreparing; // Title 준비의 중복 실행 방지
    private double _introStartedAt; // 게임 시간 정지와 무관하게 페이드인 경과 시간을 계산할 기준
    private GUIStyle _titleStyle; // 첫 OnGUI에서 생성해 반복 표시할 Title 글자 스타일
    private GUIStyle _errorStyle; // 첫 OnGUI에서 생성해 반복 표시할 줄바꿈 오류 안내 스타일
    private GUIStyle _menuStyle; // 던그리드의 배경 없는 중앙 메뉴를 표시할 PF스타더스트 스타일
    private GUIStyle _panelButtonStyle; // Title 준비 실패의 재시도 버튼에 사용하는 스타일
    private GUIStyle _slotStyle; // 12px 모서리를 보존하며 가로 저장 카드에 늘려 쓸 프레임 스타일
    private GUIStyle _slotTextStyle; // 640×360 슬롯 화면에서 사용할 안내와 글자 버튼 스타일
    private GUIStyle _slotHeadingStyle; // 슬롯 화면 상단의 saveslot 제목 스타일

    /// <summary>자기 Title 화면에서만 사용할 Esc 입력 액션을 한 번 준비</summary>
    private void Awake()
    {
        _backAction = new InputAction("TitleBack", InputActionType.Button, "<Keyboard>/escape");
    }

    /// <summary>Title 씬 시작 시 표시 연출과 슬롯 정보 준비 시작</summary>
    private void Start()
    {
        InitializeAsync().Forget(); // Start를 막지 않고 Title 연출과 슬롯 준비 진행
    }

    /// <summary>확정 로고와 던그리드 배치의 세 메뉴·기존 슬롯 흐름 표시</summary>
    private void OnGUI()
    {
        // GUI.skin을 사용할 수 있는 첫 GUI 호출에서만 표시 스타일 준비
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label) { font = TitleFont, fontSize = 42, alignment = TextAnchor.MiddleCenter };
            _errorStyle = new GUIStyle(_titleStyle) { fontSize = 28, wordWrap = true };
            _panelButtonStyle = new GUIStyle(GUI.skin.button) { font = TitleFont, fontSize = 30 };
            _menuStyle = new GUIStyle { font = TitleFont, fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (GUIStyleState state in new[] { _menuStyle.normal, _menuStyle.hover, _menuStyle.active, _menuStyle.focused,
                _menuStyle.onNormal, _menuStyle.onHover, _menuStyle.onActive, _menuStyle.onFocused })
            {
                state.background = null;
                state.scaledBackgrounds = Array.Empty<Texture2D>();
            }
            _menuStyle.normal.textColor = Color.white;
            _menuStyle.hover.textColor = new Color(0.48f, 0.96f, 1f);
            _menuStyle.focused.textColor = _menuStyle.hover.textColor;
            _menuStyle.active.textColor = new Color(0.2f, 0.7f, 0.85f);

            // 저장 화면은 원본 픽셀 기준 스타일을 한 번 만들고 씬 수명 동안 재사용
            _slotTextStyle = new GUIStyle(_menuStyle) { fontSize = 10, wordWrap = true };
            _slotHeadingStyle = new GUIStyle(_slotTextStyle) { fontSize = 20 };
            _slotStyle = new GUIStyle(_slotTextStyle) { border = new RectOffset(12, 12, 12, 12), padding = new RectOffset(14, 14, 14, 14) };
            foreach (GUIStyleState state in new[] { _slotStyle.normal, _slotStyle.hover, _slotStyle.active, _slotStyle.focused,
                _slotStyle.onNormal, _slotStyle.onHover, _slotStyle.onActive, _slotStyle.onFocused })
            {
                state.background = SlotFrame;
                state.scaledBackgrounds = Array.Empty<Texture2D>();
            }
            _slotStyle.normal.textColor = new Color32(161, 171, 195, 255);
            _slotStyle.active.textColor = new Color32(176, 148, 206, 255);
        }

        // 기준 1920×1080을 비율 유지로 표시하고 원래 GUI 상태를 보관
        Matrix4x4 originalMatrix = GUI.matrix;
        Color originalColor = GUI.color;
        bool originalEnabled = GUI.enabled;
        GUI.color = BackgroundColor;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920f * scale) * 0.5f, (Screen.height - 1080f * scale) * 0.5f, 0f), Quaternion.identity, Vector3.one * scale);
        float elapsed = (float)(Time.realtimeSinceStartupAsDouble - _introStartedAt);
        float alpha = IntroDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / IntroDuration);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.enabled = _game != null && !_game.IsLoading;
        if (IsReady && (_showSlots || IsRestartConfirmationOpen))
            DrawSaveSlots();
        else
        {
            // Title 로고와 빛은 메뉴·준비 화면에 표시하고 슬롯 화면은 독립 구성
            if (TitleLogo != null)
            {
                GUI.DrawTexture(new Rect(360f, 141f, 1200f, 480f), TitleLogo, ScaleMode.ScaleToFit, true);
                DrawFissureGlow(elapsed, alpha);
                if (TitleLogoGlint != null)
                    DrawLogoGlint(elapsed, alpha);
            }
            else
                GUI.Label(new Rect(360f, 141f, 1200f, 480f), "DRAW & SLASH", _titleStyle);

            // 로고 중심과 세 메뉴 간격은 던그리드 Title 320×180 배치를 6배로 환산
            if (IsReady)
            {
                if (GUI.Button(new Rect(672f, 670.8f, 576f, 67.2f), "시작", _menuStyle)) ShowSlots();
                GUI.enabled = false;
                GUI.Button(new Rect(672f, 742.8f, 576f, 67.2f), "설정", _menuStyle);
                GUI.enabled = _game != null && !_game.IsLoading;
                if (GUI.Button(new Rect(672f, 814.8f, 576f, 67.2f), "종료", _menuStyle)) QuitGame();
            }
            else
            {
                // 실패한 준비는 기존 재시도로 복구하고 로드 중 입력을 차단
                GUILayout.BeginArea(new Rect(560f, 650f, 800f, 350f));
                GUILayout.Label(_isPreparing || SaveManager.IsLoading || (_game != null && _game.IsLoading) ? "준비 중..." : "준비에 실패했습니다", _errorStyle);
                if (_game != null && !_isPreparing && !SaveManager.IsLoading && !_game.IsLoading && GUILayout.Button("다시 시도", _panelButtonStyle, GUILayout.Height(60f))) Retry();
                GUILayout.EndArea();
            }
        }

        // 오류 안내와 다른 화면 코드가 사용할 GUI 공통 상태 복원
        if (!string.IsNullOrEmpty(ErrorMessage)) GUI.Label(new Rect(360f, 1000f, 1200f, 70f), ErrorMessage, _errorStyle);
        GUI.enabled = originalEnabled;
        GUI.color = originalColor;
        GUI.matrix = originalMatrix;
    }

    private void OnEnable()
    {
        _backAction.performed += HandleBack; // 활성 Title의 슬롯 뒤로 입력 연결
        if (_showSlots || IsRestartConfirmationOpen)
            _backAction.Enable();
    }

    private void OnDisable()
    {
        _backAction.Disable();
        _backAction.performed -= HandleBack; // 비활성 Title이 Esc를 처리하지 않도록 연결 해제
    }

    private void OnDestroy()
    {
        _backAction.Dispose();
    }

    /// <summary>슬롯 로드와 Title 연출을 함께 기다린 뒤 선택 입력 허용</summary>
    public async UniTask InitializeAsync()
    {
        // 중복 준비를 막고 이번 Title의 표시 상태 초기화
        if (_isPreparing)
            return;
        _game = GameManager.Instance;
        IsReady = false;
        ErrorMessage = null;
        SelectedSlot = -1;
        IsRestartConfirmationOpen = false;
        _showSlots = false;
        _backAction.Disable();
        _introStartedAt = Time.realtimeSinceStartupAsDouble;
        if (_game == null)
        {
            ErrorMessage = "GameManager is missing from Title.";
            return;
        }

        // 부팅 시 요청된 슬롯 로드와 Title 연출이 모두 끝난 뒤 입력 허용
        _isPreparing = true;
        try
        {
            // 저장 도구에 로드를 다시 요청하지 않고 준비 완료만 대기
            await UniTask.WhenAll(UniTask.WaitUntil(() => !SaveManager.IsLoading, cancellationToken: destroyCancellationToken),
                UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, IntroDuration)), ignoreTimeScale: true, cancellationToken: destroyCancellationToken));
            if (this == null)
                return;
            if (!SaveManager.IsReady)
            {
                ErrorMessage = SaveManager.LoadError ?? "Save slots could not be prepared.";
                return;
            }

            // 이전 월드 오류를 표시하고 슬롯 선택 가능 상태로 전환
            ErrorMessage = _game.LastError;
            IsReady = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (this != null)
                ErrorMessage = exception.Message;
        }
        finally
        {
            _isPreparing = false;
        }
    }

    /// <summary>Title 준비 완료 후 슬롯 목록 표시</summary>
    public void ShowSlots()
    {
        if (IsReady && _game != null && !_game.IsLoading)
        {
            _showSlots = true;
            _backAction.Enable();
        }
    }

    /// <summary>슬롯 화면과 완료 확인을 함께 닫고 저장값 변경 없이 Title 메뉴로 복귀</summary>
    public void CloseSlots()
    {
        // 준비나 월드 진입 중에는 뒤로 입력을 처리하지 않음
        if (!IsReady || _game == null || _game.IsLoading || (!_showSlots && !IsRestartConfirmationOpen))
            return;

        // UI 선택 상태만 비우고 다음 슬롯 열기 전까지 Esc 액션 비활성화
        _showSlots = false;
        IsRestartConfirmationOpen = false;
        SelectedSlot = -1;
        _backAction.Disable();
    }

    /// <summary>완료 슬롯은 새 게임 확인, 나머지는 GameManager에 시작 요청</summary>
    public void SelectSlot(int slot)
    {
        // 준비 전 입력·중복 선택·범위 밖 슬롯 차단
        if (!IsReady || _game == null || _game.IsLoading || IsRestartConfirmationOpen || slot < 0 || slot >= SaveManager.Slots.Length)
            return;

        // 완료 슬롯 덮어쓰기는 확인을 받고 나머지 슬롯은 바로 시작
        SelectedSlot = slot;
        if (IsCompleted(SaveManager.Slots[slot]))
        {
            IsRestartConfirmationOpen = true;
            _backAction.Enable();
            return;
        }
        RequestStart();
    }

    /// <summary>사용자가 승인한 완료 슬롯만 새 게임으로 시작</summary>
    public void ConfirmRestart(bool confirmed)
    {
        // 유효한 확인창에서 들어온 응답만 수락
        if (!IsRestartConfirmationOpen || !IsReady || _game == null || _game.IsLoading)
            return;

        // 확인창을 닫고 승인한 경우에만 저장 데이터 교체 요청
        IsRestartConfirmationOpen = false;
        if (!_showSlots)
            _backAction.Disable();
        if (confirmed)
            RequestStart(true);
    }

    /// <summary>기존 저장 작업의 완료를 기다리는 GameManager 종료 경로 호출</summary>
    public void QuitGame()
    {
        _game?.QuitGame();
    }

    /// <summary>슬롯 시작 요청의 수락 여부에 따라 입력·오류 표시 갱신</summary>
    private void RequestStart(bool newGame = false)
    {
        if (_game.StartGame(SelectedSlot, newGame))
        {
            IsReady = false;
            _backAction.Disable();
        }
        else
            ErrorMessage = _game.LastError;
    }

    /// <summary>실패한 슬롯 로드와 Title 표시 준비 재시도</summary>
    private void Retry()
    {
        SaveManager.LoadSlotsAsync().Forget(); // 실패한 슬롯 파일 로드를 저장 도구에 재요청
        InitializeAsync().Forget(); // 새 로드 결과와 Title 연출 준비를 함께 대기
    }

    /// <summary>제출 Boss 처치 기록으로 슬롯 완료 여부 확인</summary>
    private bool IsCompleted(SaveData data)
    {
        return data != null && data.Get<List<string>>("DefeatedBosses").Contains(GameManager.SubmittedBossId);
    }

    /// <summary>Input System의 Esc 누름을 현재 슬롯 화면의 닫기 요청으로 전달</summary>
    private void HandleBack(InputAction.CallbackContext context)
    {
        CloseSlots();
    }

    /// <summary>상단 제목과 가로3카드를 동일 픽셀 배율로 표시하고 완료 기록 교체를 확인</summary>
    private void DrawSaveSlots()
    {
        // 기존 1920×1080 비율 안에서 640×360의 원본 픽셀과 12px 모서리를 함께 확대
        Matrix4x4 matrix = GUI.matrix;
        bool enabled = GUI.enabled;
        GUI.matrix = matrix * Matrix4x4.Scale(Vector3.one * 3f);
        GUI.Label(new Rect(80f, 34f, 480f, 36f), "saveslot", _slotHeadingStyle);
        GUI.enabled = enabled && !IsRestartConfirmationOpen;
        for (int slot = 0; slot < SaveManager.Slots.Length; slot++)
        {
            SaveData data = SaveManager.Slots[slot];
            string label = data == null ? $"슬롯 {slot + 1}\n\n새 게임" :
                $"슬롯 {slot + 1}\n\n진행도 {(IsCompleted(data) ? 100 : 0)}%\n\n플레이 {data.Get<double>("PlayTime") / 60d:F0}분\n\n사망 {data.Get<int>("DeathCount")}회";
            if (GUI.Button(GetSlotRect(slot), label, _slotStyle)) SelectSlot(slot);
        }

        // 확인창이 열려 있는 동안 카드 입력을 막고 같은 프레임으로 새 게임·취소 표시
        GUI.enabled = enabled;
        if (IsRestartConfirmationOpen)
        {
            Color color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.DrawTexture(new Rect(0f, 0f, 640f, 360f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Box(new Rect(160f, 120f, 320f, 132f), GUIContent.none, _slotStyle);
            GUI.Label(new Rect(176f, 132f, 288f, 54f), $"슬롯 {SelectedSlot + 1}의 완료 기록을\n새 게임으로 교체할까요?", _slotTextStyle);
            if (GUI.Button(new Rect(180f, 202f, 128f, 28f), "새 게임", _slotTextStyle)) ConfirmRestart(true);
            if (GUI.Button(new Rect(332f, 202f, 128f, 28f), "취소", _slotTextStyle)) ConfirmRestart(false);
        }

        // 다음 화면과 오류 안내가 사용할 기준 좌표·입력 상태 복원
        GUI.matrix = matrix;
        GUI.enabled = enabled;
    }

    /// <summary>3개의 가로 저장 카드에 동일 크기와 간격을 적용한 원본 픽셀 영역 반환</summary>
    private Rect GetSlotRect(int slot)
    {
        return new Rect(80f + 168f * slot, 104f, 144f, 206f);
    }

    /// <summary>틈새 주변에 푸른 빛을 상시 유지하며 실제 시간으로 밝기와 흐름을 이어 표시</summary>
    private void DrawFissureGlow(float elapsed, float introAlpha)
    {
        // 준비된 이미지의 인접 두 위상을 사용해 끊김 없이 빛을 이동
        if (TitleFissureGlowFrames == null || TitleFissureGlowFrames.Length == 0)
            return;
        float phase = Mathf.Repeat(elapsed / Mathf.Max(0.01f, FissureGlowCycle), 1f) * TitleFissureGlowFrames.Length;
        int first = Mathf.FloorToInt(phase);
        int next = (first + 1) % TitleFissureGlowFrames.Length;
        float blend = phase - first;
        float brightness = 0.8f + 0.12f * Mathf.Sin(elapsed * Mathf.PI) + 0.06f * Mathf.Sin(elapsed * Mathf.PI * 3f);
        Color color = GUI.color;

        // 원본 로고와 같은 좌표에서 틈새만 덮고 다른 표시의 색은 보존
        Rect rect = new Rect(360f, 141f, 1200f, 480f);
        GUI.color = new Color(1f, 1f, 1f, introAlpha * brightness * (1f - blend));
        if (TitleFissureGlowFrames[first] != null)
            GUI.DrawTexture(rect, TitleFissureGlowFrames[first], ScaleMode.ScaleToFit, true);
        GUI.color = new Color(1f, 1f, 1f, introAlpha * brightness * blend);
        if (TitleFissureGlowFrames[next] != null)
            GUI.DrawTexture(rect, TitleFissureGlowFrames[next], ScaleMode.ScaleToFit, true);
        GUI.color = color;
    }

    /// <summary>게임 정지와 무관한 실제 시간으로 칼날 중심의 십자 빛만 반복 표시</summary>
    private void DrawLogoGlint(float elapsed, float introAlpha)
    {
        // 페이드인 뒤 잠시 기다리고 각 반복의 표시 구간만 진행
        float time = elapsed - Mathf.Max(0f, IntroDuration) - 0.6f;
        float duration = Mathf.Max(0.01f, GlintDuration);
        float phase = Mathf.Repeat(Mathf.Max(0f, time), Mathf.Max(duration, GlintInterval));
        if (time < 0f || phase >= duration)
            return;

        // 원본 하이라이트 위에 별도 발광 십자를 확대·회전하고 투명하게 소멸
        float progress = phase / duration;
        float size = Mathf.Lerp(0.15f, 1.15f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.7f)));
        Vector2 pivot = new Vector2(360f + GlintPivot.x * 6f, 141f + GlintPivot.y * 6f);
        Matrix4x4 matrix = GUI.matrix;
        Color color = GUI.color;
        GUI.matrix = matrix * Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -18f + GlintRotation * progress))
            * Matrix4x4.Scale(new Vector3(size, size, 1f)) * Matrix4x4.Translate(-pivot);
        GUI.color = new Color(1f, 1f, 1f, introAlpha * Mathf.Sin(progress * Mathf.PI));
        GUI.DrawTexture(new Rect(pivot.x - 160f, pivot.y - 160f, 320f, 320f), TitleLogoGlint, ScaleMode.ScaleToFit, true);

        // 로고 변환과 밝기가 메뉴의 위치·클릭 영역에 남지 않도록 복원
        GUI.matrix = matrix;
        GUI.color = color;
    }
}
