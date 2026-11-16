using UnityEngine;
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
    public bool UseWorldInteractionPrompt; // 수정에 연결한 월드 키 안내와 기존 문자 안내의 중복 방지
    public Font UIFont; // 기존 Title 복귀 안내에 사용할 PF스타더스트
    public bool IsDrawMode { get; private set; } // 일반 HUD 대신 Draw 전용 표시를 사용하는 상태
    public bool IsSlashMode { get; private set; } // Slash 동안 HUD를 숨기고 암전만 표시하는 상태
    public bool IsDeathMode { get; private set; } // 사망 연출 중 HUD·Title 복귀·상호작용 안내를 숨기는 상태
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
    private GUIStyle returnStyle; // 첫 GUI 호출에서 준비할 작은 Title 복귀 버튼 스타일
    private bool cursorApplied; // 자기 UI가 적용한 커서를 비활성화 때 해제할 상태

    /// <summary>Player 생성 전에도 외부에서 접근할 수 있도록 MainWorld HUD 등록</summary>
    private void Awake()
    {
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
        if (gameManager != null) ApplyCursor();
        if (input == null) return;
        input.OnInteractionChanged += SetInteraction; // 재활성화한 HUD의 수정 후보 구독 복원
        SetInteraction(input.InteractionTarget);
    }

    private void OnDisable()
    {
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

    /// <summary>기존 Title 복귀와 월드 안내가 없는 검사 환경의 F 안내 표시</summary>
    private void OnGUI()
    {
        // 첫 GUI 문맥에서 스타일을 준비하고 HUD와 겹치지 않는 우상단에 복귀 배치
        if (returnStyle == null) returnStyle = new GUIStyle(GUI.skin.button) { font = UIFont, fontSize = 16 };
        if (!IsDrawMode && !IsSlashMode && !IsDeathMode && gameManager != null && !gameManager.IsLoading && GUI.Button(new Rect(Screen.width - 116f, 12f, 104f, 28f), "Title", returnStyle))
            gameManager.ReturnToTitle();

        // 조작 가능한 수정 범위에서만 공통 F의 상호작용 우선 동작 안내
        if (!UseWorldInteractionPrompt && IsInteractionVisible)
            GUI.Box(new Rect(Screen.width * 0.5f - 150f, Screen.height - 105f, 300f, 36f), "[F] 체크포인트 활성화");
    }

    private void OnDestroy()
    {
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

    /// <summary>MainWorld 진입·재활성화 경계에서 준비된 조준점을 한 번 적용</summary>
    private void ApplyCursor()
    {
        if (AimCursor == null) return;
        Cursor.SetCursor(AimCursor, new Vector2(15f, 15f), CursorMode.Auto);
        cursorApplied = true;
    }
}
