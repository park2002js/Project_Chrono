using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

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

    private GameManager _game; // 슬롯 시작·Title 재시도·종료를 요청할 전역 관리자
    private bool _showSlots; // 시작 버튼 대신 슬롯 목록을 표시할지 구분
    private bool _isPreparing; // Title 준비의 중복 실행 방지
    private double _introStartedAt; // 게임 시간 정지와 무관하게 페이드인 경과 시간을 계산할 기준
    private GUIStyle _titleStyle; // 첫 OnGUI에서 생성해 반복 표시할 Title 글자 스타일
    private GUIStyle _errorStyle; // 첫 OnGUI에서 생성해 반복 표시할 줄바꿈 오류 안내 스타일
    private GUIStyle _menuStyle; // 던그리드의 배경 없는 중앙 메뉴를 표시할 PF스타더스트 스타일
    private GUIStyle _panelButtonStyle; // 슬롯과 확인창에 사용하는 PF스타더스트 버튼 스타일

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
            _menuStyle = new GUIStyle(GUI.skin.button) { font = TitleFont, fontSize = 48, alignment = TextAnchor.MiddleCenter };
            _menuStyle.normal.background = _menuStyle.hover.background = _menuStyle.active.background = _menuStyle.focused.background = null;
            _menuStyle.normal.textColor = Color.white;
            _menuStyle.hover.textColor = new Color(0.48f, 0.96f, 1f);
            _menuStyle.active.textColor = new Color(0.2f, 0.7f, 0.85f);
        }

        // 기준 1920×1080을 비율 유지로 표시하고 원래 GUI 상태를 보관
        Matrix4x4 originalMatrix = GUI.matrix;
        Color originalColor = GUI.color;
        bool originalEnabled = GUI.enabled;
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920f * scale) * 0.5f, (Screen.height - 1080f * scale) * 0.5f, 0f), Quaternion.identity, Vector3.one * scale);
        float alpha = IntroDuration <= 0f ? 1f : Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - _introStartedAt) / IntroDuration);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        if (TitleLogo != null)
            GUI.DrawTexture(new Rect(360f, 141f, 1200f, 480f), TitleLogo, ScaleMode.ScaleToFit, true);
        else
            GUI.Label(new Rect(360f, 141f, 1200f, 480f), "DRAW & SLASH", _titleStyle);

        // 로고 중심과 세 메뉴 간격은 던그리드 Title 320×180 배치를 6배로 환산
        GUI.enabled = _game != null && !_game.IsLoading;
        if (IsReady && !_showSlots && !IsRestartConfirmationOpen)
        {
            if (GUI.Button(new Rect(672f, 670.8f, 576f, 67.2f), "시작", _menuStyle)) ShowSlots();
            GUI.enabled = false;
            GUI.Button(new Rect(672f, 742.8f, 576f, 67.2f), "설정", _menuStyle);
            GUI.enabled = _game != null && !_game.IsLoading;
            if (GUI.Button(new Rect(672f, 814.8f, 576f, 67.2f), "종료", _menuStyle)) QuitGame();
        }
        else
        {
            // 기존 저장 슬롯·완료 슬롯 확인·실패 복구 기능을 같은 글꼴로 유지
            GUILayout.BeginArea(new Rect(560f, 650f, 800f, 350f));
            if (!IsReady)
            {
                GUILayout.Label(_isPreparing || SaveManager.IsLoading || (_game != null && _game.IsLoading) ? "준비 중..." : "준비에 실패했습니다", _errorStyle);
                if (_game != null && !_isPreparing && !SaveManager.IsLoading && !_game.IsLoading && GUILayout.Button("다시 시도", _panelButtonStyle, GUILayout.Height(60f))) Retry();
            }
            else if (IsRestartConfirmationOpen)
            {
                GUILayout.Label($"슬롯 {SelectedSlot + 1}의 완료 기록을 새 게임으로 교체할까요?", _errorStyle);
                if (GUILayout.Button("새 게임", _panelButtonStyle, GUILayout.Height(60f))) ConfirmRestart(true);
                if (GUILayout.Button("취소", _panelButtonStyle, GUILayout.Height(60f))) ConfirmRestart(false);
            }
            else if (_showSlots)
            {
                for (int slot = 0; slot < SaveManager.Slots.Length; slot++)
                {
                    SaveData data = SaveManager.Slots[slot];
                    string label = data == null ? $"슬롯 {slot + 1} | 새 게임" :
                        $"슬롯 {slot + 1} | {(IsCompleted(data) ? 100 : 0)}% | {data.Get<double>("PlayTime") / 60d:F0}분 | 사망 {data.Get<int>("DeathCount")}회";
                    if (GUILayout.Button(label, _panelButtonStyle, GUILayout.Height(62f))) SelectSlot(slot);
                }
                if (GUILayout.Button("뒤로", _panelButtonStyle, GUILayout.Height(54f))) _showSlots = false;
            }
            GUILayout.EndArea();
        }

        // 오류 안내와 다른 화면 코드가 사용할 GUI 공통 상태 복원
        if (!string.IsNullOrEmpty(ErrorMessage)) GUI.Label(new Rect(360f, 1000f, 1200f, 70f), ErrorMessage, _errorStyle);
        GUI.enabled = originalEnabled;
        GUI.color = originalColor;
        GUI.matrix = originalMatrix;
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
            _showSlots = true;
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
            IsReady = false;
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
}
