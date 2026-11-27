using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>현재 Player·Boss의 값과 표시 상태를 준비된 HUD 아트에 연결</summary>
[RequireComponent(typeof(UI_Draw))]
public class UIController : MonoBehaviour
{
    public static UIController Instance; // 외부에서 MainWorld HUD의 연결과 표시를 제어할 창구
    public PlayerCharacter Player; // Player HUD에서 현재 체력과 최대 체력을 읽을 대상
    public BossCharacter Boss; // Boss HUD에서 이름과 체력을 읽을 대상
    public bool IsPlayerHudVisible; // 연결된 Player의 체력 HUD 표시 여부
    public bool IsBossHudVisible; // 연결된 Boss의 이름·체력 HUD 표시 여부
    public GameObject PlayerHudRoot; // Player HP·그로기·두 스킬·키 안내를 함께 표시할 루트
    public GameObject BossHudRoot; // 조우 때만 표시할 Boss 체력바 루트
    public Image PlayerHealthFill; // 현재 Player 체력 비율을 반영할 채움
    public Image GroggyFill; // 현재 그로기 0~100 값을 반영할 채움
    public Image BossHealthFill; // 현재 Boss 체력 비율을 반영할 채움
    public Image DrawIcon; // Draw 사용 가능 여부를 표시할 준비된 아이콘
    public Image SlashIcon; // 확정 Line과 재사용 대기를 표시할 준비된 아이콘
    public Image DrawCooldown; // Draw 남은 재사용 시간을 아래에서 위로 표시할 가림막
    public Image SlashCooldown; // Slash 남은 재사용 시간을 아래에서 위로 표시할 가림막
    public Texture2D AimCursor; // 게임 씬에서 사용하는 중앙 핫스폿 조준점
    public Font UIFont; // 전투 중 Esc 안내와 데모 종료 문구에 사용할 PF스타더스트
    public UI_Settings Settings; // 비전투 Esc로 열 음량·해상도 설정 창
    public RectTransform BossHealthBar; // 전투 중 Esc 안내를 바로 위에 띄울 Boss 체력바 틀
    [Min(0.1f)] public float UnsupportedNoticeDuration = 1.5f; // 전투 중 Esc 안내를 보여줄 실제 시간(초)
    public bool IsInBattle => (Player != null && Player.IsInCombat) || (gameManager != null && gameManager.Stage != null && gameManager.Stage.IsBossSequencePlaying); // Boss 전투 또는 조우·처치 연출 중 여부
    public bool IsDrawMode { get; private set; } // 일반 HUD 대신 Draw 전용 표시를 사용하는 상태
    public bool IsSlashMode { get; private set; } // Slash 동안 HUD를 숨기고 암전만 표시하는 상태
    public bool IsDeathMode { get; private set; } // 사망 연출 중 HUD·상호작용 안내를 숨기는 상태
    public CrystalCheckPoint InteractionTarget { get; private set; } // 입력 후보 변경 사건에서 받은 F 안내 대상
    public bool IsInteractionVisible => InteractionTarget != null && !InteractionTarget.IsActivated && Player != null && Player.isActiveAndEnabled && Player.CanMove
        && !Player.IsAttacking && !Player.IsDashing && !Player.IsInputBlocked && !IsDrawMode && !IsSlashMode && !IsDeathMode
        && input != null && input.isActiveAndEnabled && !input.BlockInput && gameManager != null && gameManager.IsSessionActive && !gameManager.IsLoading; // 현재 조작 가능한 수정 후보의 F 안내 표시 여부

    private GameManager gameManager; // Start에서 확보해 HUD가 유지되는 동안 상태 조회와 Title 복귀에 사용할 관리자
    private UI_Draw drawView; // 암전·편집 안내만 담당하는 자기 Draw 화면
    private bool previousPlayerHud; // Draw 진입 직전의 Player HUD 표시 상태
    private bool previousBossHud; // Draw 진입 직전의 Boss HUD 표시 상태
    private PlayerInputController input; // Start에서 확보해 수정 후보 사건을 구독할 MainWorld 입력
    private bool deathPlayerHud; // 사망 연출이 끝난 뒤 복구할 Player HUD 표시 상태
    private bool deathBossHud; // 사망 연출이 끝난 뒤 복구할 Boss HUD 표시 상태
    private bool cursorApplied; // 자기 UI가 적용한 커서를 비활성화 때 해제할 상태
    private const string UnsupportedNotice = "지원되지 않는 기능입니다"; // 전투 중 Esc를 눌렀을 때 Boss 체력바 위에 띄울 문구
    private InputAction escapeAction; // HUD 수명 동안 Esc로 설정 창 또는 전투 중 안내를 요청할 자기 입력
    private double noticeEndsAt; // 전투 중 Esc 안내를 끝낼 실제 시간
    private int settingsClosedFrame = -1; // 설정 창의 Esc 닫기와 같은 프레임에 창을 다시 열지 않도록 기록한 프레임
    private bool ownsSettingsPause; // 설정 창을 열며 건 일반 정지를 닫을 때 해제할지 여부
    private GUIStyle noticeStyle; // 첫 GUI 호출에서 준비할 전투 중 Esc 안내 스타일
    private readonly Vector3[] barCorners = new Vector3[4]; // Boss 체력바의 화면 모서리를 받을 재사용 배열

    /// <summary>Player 생성 전에도 외부에서 접근할 수 있도록 MainWorld HUD 등록</summary>
    private void Awake()
    {
        // 중복 객체도 비활성화 경로가 안전하도록 자기 Esc 액션을 먼저 준비
        escapeAction = new InputAction("WorldEscape", InputActionType.Button, "<Keyboard>/escape");

        // 기존 HUD를 유지하고 중복 객체만 제거
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        // 이번 씬의 HUD를 전역 조회 대상으로 등록
        Instance = this;
        drawView = GetComponent<UI_Draw>();
    }

    /// <summary>모든 씬 객체의 Awake 이후 앱 수명의 GameManager 참조 확보</summary>
    private void Start()
    {
        gameManager = GameManager.Instance;
        input = PlayerInputController.Instance;
        ApplyCursor();
        if (input == null) return;
        input.OnInteractionChanged += SetInteraction; // 공통 F 후보 변경을 받아 자기 안내 대상 갱신
        SetInteraction(input.InteractionTarget);
    }

    private void OnEnable()
    {
        escapeAction.performed += HandleEscape; // 활성 HUD의 Esc 요청 연결
        escapeAction.Enable();
        if (Settings != null) Settings.OnClosed += HandleSettingsClosed; // 설정 창이 닫히면 자기 정지 해제
        if (gameManager != null) ApplyCursor();
        if (input == null) return;
        input.OnInteractionChanged += SetInteraction; // 재활성화한 HUD의 수정 후보 구독 복원
        SetInteraction(input.InteractionTarget);
    }

    private void OnDisable()
    {
        // 열린 설정 창을 먼저 닫아 저장·정지 해제를 마친 뒤 Esc 연결 정리
        if (Settings != null)
        {
            Settings.Close();
            Settings.OnClosed -= HandleSettingsClosed; // 비활성 HUD의 설정 창 닫힘 구독 해제
        }
        escapeAction.Disable();
        escapeAction.performed -= HandleEscape; // 비활성 HUD가 Esc를 처리하지 않도록 연결 해제
        noticeEndsAt = 0d;
        if (input != null) input.OnInteractionChanged -= SetInteraction; // 비활성 HUD의 수정 후보 구독 해제
        if (PlayerHudRoot != null) PlayerHudRoot.SetActive(false);
        if (BossHudRoot != null) BossHudRoot.SetActive(false);
        if (cursorApplied)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            cursorApplied = false;
        }
    }

    /// <summary>캐시한 UI 참조에 현재 값만 전달하며 UI 애니메이션 없이 즉시 반영</summary>
    private void LateUpdate()
    {
        // 기존 모드별 표시 계약에 따라 네 Player 정보와 Boss HP를 독립 제어
        bool showPlayer = !IsDeathMode && IsPlayerHudVisible && Player != null;
        if (PlayerHudRoot != null) PlayerHudRoot.SetActive(showPlayer);
        if (BossHudRoot != null) BossHudRoot.SetActive(!IsDeathMode && IsBossHudVisible && Boss != null);
        if (PlayerHealthFill != null) PlayerHealthFill.fillAmount = Player != null && Player.maxHP > 0f ? Mathf.Clamp01(Player.HP / Player.maxHP) : 0f;
        if (GroggyFill != null) GroggyFill.fillAmount = Player != null ? Mathf.Clamp01(Player.Groggy / 100f) : 0f;
        if (BossHealthFill != null) BossHealthFill.fillAmount = Boss != null && Boss.maxHP > 0f ? Mathf.Clamp01(Boss.HP / Boss.maxHP) : 0f;

        // 연결된 고정 스킬의 현재 사용 가능·재사용 시간 표시
        SkillController skills = Player != null ? Player.Skills : null;
        if (DrawIcon != null) DrawIcon.color = skills != null && skills.CanDraw ? Color.white : new Color(0.45f, 0.45f, 0.45f);
        if (SlashIcon != null) SlashIcon.color = skills != null && skills.HasLine && skills.SlashCooldownRemaining <= 0f ? Color.white : new Color(0.45f, 0.45f, 0.45f);
        if (DrawCooldown != null) DrawCooldown.fillAmount = skills != null && skills.NormalDrawCooldown > 0f ? Mathf.Clamp01(skills.DrawCooldownRemaining / skills.NormalDrawCooldown) : 0f;
        if (SlashCooldown != null) SlashCooldown.fillAmount = skills != null && skills.ConfirmSlashCooldown > 0f ? Mathf.Clamp01(skills.SlashCooldownRemaining / skills.ConfirmSlashCooldown) : 0f;
    }

    /// <summary>전투 중 Esc 안내 표시</summary>
    private void OnGUI()
    {
        // 전투 중 Esc 안내는 실제 시간으로 표시하고 마지막 0.3초 동안 흐려짐
        double remaining = noticeEndsAt - Time.realtimeSinceStartupAsDouble;
        if (remaining > 0d)
            DrawUnsupportedNotice((float)remaining);
    }

    private void OnDestroy()
    {
        escapeAction?.Dispose();

        // 중복 HUD 제거가 현재 HUD의 연결을 끊지 않도록 확인
        if (Instance != this)
            return;

        // 현재 HUD를 제거할 때 대상과 전역 참조 해제
        Unbind();
        Instance = null;
    }

    /// <summary>PlayerSpawner가 지정한 Player를 HUD에 연결</summary>
    public void SetPlayer(PlayerCharacter player)
    {
        Player = player;
        IsPlayerHudVisible = player != null;
    }

    /// <summary>조우한 Boss를 HUD에 연결</summary>
    public void SetBoss(BossCharacter boss)
    {
        Boss = boss;
        IsBossHudVisible = boss != null;
    }

    /// <summary>Player HUD는 유지하고 Boss HUD 연결만 해제</summary>
    public void ClearBoss()
    {
        Boss = null;
        IsBossHudVisible = false;
    }

    /// <summary>세션 종료 시 모든 HUD 참조와 표시 상태 해제</summary>
    public void Unbind()
    {
        // 세션 종료가 건 정지는 유지한 채 설정 창만 닫고 저장
        ownsSettingsPause = false;
        if (Settings != null) Settings.Close();
        noticeEndsAt = 0d;

        // 연출 표시와 대상 연결 해제
        SetDeathMode(false);
        SetSlashMode(false);
        SetDrawMode(false);
        SetInteraction(null);
        Player = null;
        IsPlayerHudVisible = false;
        ClearBoss();
    }

    /// <summary>로딩·컷씬에 맞춰 Player HUD 표시 여부 설정</summary>
    public void SetPlayerHudVisible(bool visible)
    {
        IsPlayerHudVisible = visible && Player != null;
    }

    /// <summary>로딩·컷씬에 맞춰 Boss HUD 표시 여부 설정</summary>
    public void SetBossHudVisible(bool visible)
    {
        IsBossHudVisible = visible && Boss != null;
    }

    /// <summary>Draw 진입 전 HUD 상태를 보관하고 종료 시 같은 표시 상태로 복구</summary>
    public void SetDrawMode(bool drawing)
    {
        if (IsDrawMode == drawing) return;
        if (drawing)
        {
            previousPlayerHud = IsPlayerHudVisible;
            previousBossHud = IsBossHudVisible;
            IsPlayerHudVisible = false;
            IsBossHudVisible = false;
        }
        else
        {
            IsPlayerHudVisible = previousPlayerHud && Player != null;
            IsBossHudVisible = previousBossHud && Boss != null;
        }
        IsDrawMode = drawing;
        drawView.SetVisible(drawing, Player != null ? Player.Skills : null);
    }

    /// <summary>Slash가 숨긴 HUD만 복구하고 편집 안내 없이 암전 전환</summary>
    public void SetSlashMode(bool slashing)
    {
        if (IsSlashMode == slashing) return;
        if (slashing)
        {
            previousPlayerHud = IsPlayerHudVisible;
            previousBossHud = IsBossHudVisible;
            IsPlayerHudVisible = IsBossHudVisible = false;
        }
        else
        {
            IsPlayerHudVisible = previousPlayerHud && Player != null;
            IsBossHudVisible = previousBossHud && Boss != null;
        }
        IsSlashMode = slashing;
        drawView.SetVisible(slashing, null);
    }

    /// <summary>입력 후보 사건에서 받은 대상을 보관하고 안내 표시 여부는 현재 상태로 판단</summary>
    public void SetInteraction(CrystalCheckPoint target)
    {
        InteractionTarget = target;
    }

    /// <summary>사망 연출의 HUD 숨김을 적용하고 종료 때 기존 표시 상태만 복원</summary>
    public void SetDeathMode(bool dead)
    {
        if (IsDeathMode == dead) return;
        if (dead)
        {
            deathPlayerHud = IsPlayerHudVisible;
            deathBossHud = IsBossHudVisible;
            IsPlayerHudVisible = IsBossHudVisible = false;
        }
        else
        {
            IsPlayerHudVisible = deathPlayerHud && Player != null;
            IsBossHudVisible = deathBossHud && Boss != null;
        }
        IsDeathMode = dead;
    }

    /// <summary>전투 중 Esc는 안내만 띄우고, 조작 가능한 비전투 상태에서는 게임 시간을 멈추고 설정 창 열기</summary>
    private void HandleEscape(InputAction.CallbackContext context)
    {
        // 설정 창이 열려 있거나 방금 닫힌 프레임의 Esc는 창의 닫기 입력으로 둠
        if ((Settings != null && Settings.IsOpen) || Time.frameCount == settingsClosedFrame)
            return;
        if (gameManager == null || !gameManager.IsSessionActive || gameManager.IsLoading)
            return;

        // Boss 전투와 조우·처치 연출 중에는 설정 대신 안내 시간만 갱신
        if (IsInBattle)
        {
            noticeEndsAt = Time.realtimeSinceStartupAsDouble + UnsupportedNoticeDuration;
            return;
        }

        // 다른 정지·Draw·Slash·사망·입력 차단 연출이 없는 일반 조작 중에만 열기
        if (Settings == null || Time.timeScale <= 0f || IsDrawMode || IsSlashMode || IsDeathMode || input == null || input.BlockInput)
            return;
        gameManager.SetGameplayPaused(true);
        ownsSettingsPause = true;
        Settings.Open();
    }

    /// <summary>설정 창이 닫힌 프레임을 기록하고 자기가 건 일반 정지만 해제</summary>
    private void HandleSettingsClosed()
    {
        settingsClosedFrame = Time.frameCount;
        if (!ownsSettingsPause)
            return;
        ownsSettingsPause = false;
        if (gameManager != null)
            gameManager.SetGameplayPaused(false);
    }

    /// <summary>Boss 체력바 틀 바로 위 가운데에 전투 중 Esc 불가 안내 표시</summary>
    private void DrawUnsupportedNotice(float remaining)
    {
        // 첫 표시에서 스타일을 준비하고 1080 기준 크기를 현재 화면 높이에 맞춤
        float scale = Screen.height / 1080f;
        if (noticeStyle == null)
            noticeStyle = new GUIStyle(GUI.skin.label) { font = UIFont, alignment = TextAnchor.LowerCenter };
        noticeStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(28f * scale));

        // Overlay 캔버스의 체력바 위 모서리를 GUI 좌표로 바꾸고, 없으면 기준 배치(아래에서 102px)를 사용
        float bottom = Screen.height - 110f * scale;
        if (BossHealthBar != null)
        {
            BossHealthBar.GetWorldCorners(barCorners);
            if (barCorners[2].x > barCorners[0].x)
                bottom = Screen.height - barCorners[1].y - 8f * scale;
        }

        // 그림자를 깐 흰 글자를 남은 시간에 따라 흐리게 표시하고 GUI 색 복원
        Color color = GUI.color;
        float alpha = Mathf.Clamp01(remaining / 0.3f);
        Rect rect = new Rect(0f, bottom - 60f * scale, Screen.width, 60f * scale);
        GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
        GUI.Label(new Rect(rect.x + 2f * scale, rect.y + 2f * scale, rect.width, rect.height), UnsupportedNotice, noticeStyle);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(rect, UnsupportedNotice, noticeStyle);
        GUI.color = color;
    }

    /// <summary>MainWorld 진입·재활성화 경계에서 준비된 조준점을 한 번 적용</summary>
    private void ApplyCursor()
    {
        if (AimCursor == null) return;
        Cursor.SetCursor(AimCursor, new Vector2(15f, 15f), CursorMode.Auto);
        cursorApplied = true;
    }
}
