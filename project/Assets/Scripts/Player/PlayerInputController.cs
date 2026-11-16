using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>Input Actions 콜백 값을 보관하고 조작 상태에 따라 Player에 전달</summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputController : MonoBehaviour
{
    public static PlayerInputController Instance; // Player의 생성·제거와 무관하게 외부에서 입력 차단과 대상을 설정할 창구
    public PlayerCharacter Player; // 현재 입력 의도를 전달할 세션 Player
    public bool BlockInput { get => blockInput; set { blockInput = value; if (value) StopInput(); } } // 사용자 이동 전달 차단 여부, true 설정 시 기존 이동도 즉시 초기화
    public Vector2 MoveInput; // Move 액션의 최신 값, 차단 중에도 콜백으로 갱신
    public Vector2 aimInput; // Look 액션이 전달한 마지막 화면 커서 좌표
    public Vector2 AimWorldPosition; // Player 평면에 투영한 마지막 커서 좌표
    public bool AttackHeld { get; private set; } // 공격 액션의 최신 버튼 유지 여부
    public bool IsDrawInput { get; private set; } // 일반 조작 대신 Draw 전용 액션 맵을 사용하는 상태
    public bool HasDrawActions { get; private set; } // Start에서 확인한 일반·Draw 입력 맵의 연결 준비 여부
    public bool HasSlashAction { get; private set; } // 일반 맵의 Slash 액션 연결 준비 여부
    public bool IsSlashInput { get; private set; } // Slash 연출 동안 일반 입력 전달을 멈춘 상태
    public CrystalCheckPoint InteractionTarget { get; private set; } // 공통 F에서 Slash보다 먼저 요청할 범위 안 수정
    public event Action<CrystalCheckPoint> OnInteractionChanged; // 후보 변경을 UI 등 자기 책임의 구독자에게 알릴 사건

    [SerializeField, FormerlySerializedAs("BlockInput")] private bool blockInput; // BlockInput 프로퍼티가 읽고 변경할 실제 이동 차단 상태
    private GameManager gameManager; // Start에서 확보해 앱 수명 동안 유지되는 세션 상태 조회 대상
    private Camera worldCamera; // MainWorld의 모든 Awake가 끝난 뒤 확보해 씬 수명 동안 사용할 카메라
    private bool dashRequested; // 이번 Update에서 한 번만 판단할 대시 누름 요청
    private bool attackRequested; // 눌렀다 같은 프레임에 놓아도 한 번 전달할 공격 누름
    private PlayerInput playerInput; // Awake에서 확보해 액션 맵을 전환할 자기 입력 컴포넌트
    private bool drawRequested; // 이번 갱신에서 소비할 Draw 진입 또는 취소 누름
    private bool pointRequested; // Draw에서만 해석할 점 배치 누름
    private bool resetRequested; // Draw에서만 해석할 점 초기화 누름
    private bool confirmRequested; // Draw에서만 해석할 경로 확정 누름
    private bool slashRequested; // 이번 갱신에 한 번 판단하고 버릴 상호작용 우선·Slash 공통 F 누름
    private readonly List<CrystalCheckPoint> interactionCandidates = new List<CrystalCheckPoint>(); // 겹친 수정 범위에서 나간 후보만 제거하고 남은 후보를 유지할 목록

    /// <summary>다른 객체 조회 없이 MainWorld의 독립 입력 객체 등록</summary>
    private void Awake()
    {
        // 기존 입력 객체와 연결을 보존하고 중복 객체만 제거
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        // Player 연결과 월드 준비가 끝날 때까지 입력 차단
        Instance = this;
        playerInput = GetComponent<PlayerInput>();
        BlockInput = true;
    }

    /// <summary>씬 객체의 Awake가 끝난 뒤 입력 처리에 사용할 고정 참조를 한 번 확보</summary>
    private void Start()
    {
        gameManager = GameManager.Instance;
        GameCamera gameCamera = GameCamera.Instance;
        worldCamera = gameCamera != null ? gameCamera.Camera : null;
        HasDrawActions = playerInput.actions != null && playerInput.actions.FindActionMap("Draw", false) != null && playerInput.actions.FindAction("Player/Draw", false) != null;
        HasSlashAction = playerInput.actions != null && playerInput.actions.FindAction("Player/Slash", false) != null;
    }

    /// <summary>조작 상태에 따라 입력 캐시를 전달하고 명시적 차단 중에는 외부 이동 유지</summary>
    private void Update()
    {
        // 차단 중 누른 대시가 해제 이후 실행되지 않도록 매 프레임 한 번 소비
        bool requestDash = dashRequested;
        bool requestAttack = attackRequested;
        attackRequested = false;
        bool requestDraw = drawRequested;
        bool requestPoint = pointRequested;
        bool requestReset = resetRequested;
        bool requestConfirm = confirmRequested;
        bool requestSlash = slashRequested;
        slashRequested = false;
        dashRequested = false;
        drawRequested = pointRequested = resetRequested = confirmRequested = false;

        // Slash 중 누른 입력은 다음 일반 조작 프레임에 예약하지 않음
        if (IsSlashInput)
        {
            ClearActionCache();
            return;
        }

        // Draw의 시간 정지는 일반 CanMove 검사와 무관하게 전용 입력으로 처리
        if (IsDrawInput)
        {
            if (Player == null || !Player.IsDrawing)
            {
                SetDrawMode(false);
                return;
            }
            if (BlockInput || (gameManager != null && (!gameManager.IsSessionActive || gameManager.IsLoading)))
            {
                Player.CancelDraw();
                return;
            }
            UpdateAimPosition();
            Player.Skills.UpdateDrawAim(AimWorldPosition);
            if (requestDraw)
            {
                Player.CancelDraw();
                return;
            }
            if (requestReset) Player.Skills.ResetPoints();
            if (requestPoint) Player.Skills.PlacePoint(AimWorldPosition);
            if (requestConfirm) Player.Skills.ConfirmLine();
            return;
        }

        // 연결 대상과 세션·캐릭터 상태를 확인해 일반 조작 허용 여부 판단
        if (Player == null) return;
        if (!Player.CanMove || (gameManager != null && (!gameManager.IsSessionActive || gameManager.IsLoading || gameManager.IsGameplayPaused)))
        {
            Player.SetMoveInput(Vector2.zero);
            Player.SetAttackInput(false);
            if (gameManager != null && (!gameManager.IsSessionActive || gameManager.IsLoading))
                Player.CancelAttack();
            return;
        }

        // 입력이 없으면 0을 전달하되 차단 중에는 컷씬 등의 직접 이동 유지
        if (!BlockInput)
            Player.SetMoveInput(MoveInput);

        // 명시적 이동 차단과 별개로 게임 카메라를 통해 커서 위치 투영
        UpdateAimPosition();

        // Draw를 수락한 프레임에는 일반 공격·대시 요청을 함께 실행하지 않음
        if (requestDraw && !BlockInput && Player.ApplyDraw())
            return;

        // 일반 F는 범위 안 수정에 우선 전달하고 실패한 상호작용도 Slash로 누출하지 않음
        if (requestSlash && !BlockInput)
        {
            if (InteractionTarget != null)
            {
                Player.ApplyInteraction(InteractionTarget);
                return;
            }
            if (Player.ApplySlash()) return;
        }

        // 같은 프레임의 이동·커서 값을 먼저 반영한 뒤 대시 가능 여부를 Player에 요청
        if (requestDash && !BlockInput)
            Player.ApplyDash();

        // 입력 계층은 버튼 유지 사실만 전달하고 공격 허용·조준 결정은 Player에 위임
        Player.SetAttackInput(!BlockInput && AttackHeld);
        if (requestAttack && !BlockInput)
            Player.ApplyAttack();
    }

    private void OnDisable()
    {
        if (Instance == this)
            StopInput();
    }

    private void OnDestroy()
    {
        // 중복 객체 제거가 기존 Instance와 Player 연결에 영향을 주지 않도록 소유 확인
        if (Instance != this) return;

        // 자기 입력 대상과 전역 접근 참조 정리
        ClearPlayer();
        Instance = null;
    }

    /// <summary>PlayerInput의 Move 이벤트에서 눌림·변경·해제 값을 모두 보관</summary>
    public void HandleOnMove(InputAction.CallbackContext input)
    {
        if (!isActiveAndEnabled) return;
        MoveInput = input.ReadValue<Vector2>();
    }

    /// <summary>PlayerInput의 Look 이벤트에서 화면 커서 위치 보관</summary>
    public void HandleOnAim(InputAction.CallbackContext input)
    {
        if (!isActiveAndEnabled) return;
        aimInput = input.ReadValue<Vector2>();
    }

    /// <summary>대시 액션의 눌림만 보관하고 홀드·해제의 반복 실행 방지</summary>
    public void HandleOnDash(InputAction.CallbackContext input)
    {
        if (!isActiveAndEnabled || !input.performed) return;
        dashRequested = true;
    }

    /// <summary>공격 액션의 눌림·해제를 보관하고 공격 실행은 캐릭터 갱신에 위임</summary>
    public void HandleOnAttack(InputAction.CallbackContext input)
    {
        if (!isActiveAndEnabled) return;
        if (input.performed) { AttackHeld = true; attackRequested = true; }
        else if (input.canceled) AttackHeld = false;
    }

    /// <summary>일반·Draw 맵의 Shift 누름을 모드별 진입 또는 취소 요청으로 보관</summary>
    public void HandleOnDraw(InputAction.CallbackContext input)
    {
        if (isActiveAndEnabled && input.performed) drawRequested = true;
    }

    /// <summary>Draw 맵의 클릭을 점 배치 요청으로만 보관</summary>
    public void HandleOnDrawPoint(InputAction.CallbackContext input)
    {
        if (isActiveAndEnabled && IsDrawInput && input.performed) pointRequested = true;
    }

    /// <summary>Draw 맵의 R 누름을 이번 편집 점 초기화로만 보관</summary>
    public void HandleOnDrawReset(InputAction.CallbackContext input)
    {
        if (isActiveAndEnabled && IsDrawInput && input.performed) resetRequested = true;
    }

    /// <summary>Draw 맵의 F 누름을 이번 편집 확정으로만 보관</summary>
    public void HandleOnDrawConfirm(InputAction.CallbackContext input)
    {
        if (isActiveAndEnabled && IsDrawInput && input.performed) confirmRequested = true;
    }

    /// <summary>일반 맵 F의 상호작용 우선·Slash 공통 누름을 일회 요청으로 보관</summary>
    public void HandleOnSlash(InputAction.CallbackContext input)
    {
        if (isActiveAndEnabled && !IsDrawInput && !IsSlashInput && input.performed) slashRequested = true;
    }

    /// <summary>외부 BlockInput과 구분해 Slash 동안의 입력 캐시·전달만 정리</summary>
    public void SetSlashMode(bool slashing)
    {
        IsSlashInput = slashing;
        ClearActionCache();
    }

    /// <summary>전환 직전 요청을 비우고 실제 일반·Draw 액션 맵 교체</summary>
    public void SetDrawMode(bool drawing)
    {
        // 이전 맵의 누름이나 취소 콜백이 새 모드로 넘어가지 않도록 초기화
        ClearActionCache();
        IsDrawInput = drawing;
        if (Player != null)
        {
            Player.SetMoveInput(Vector2.zero);
            Player.SetAttackInput(false);
        }
        if (playerInput != null && playerInput.actions != null)
            playerInput.SwitchCurrentActionMap(drawing ? "Draw" : "Player");
        ClearActionCache();
    }

    /// <summary>Spawner가 지정한 Player를 연결하고 준비 완료 전까지 입력 차단</summary>
    public void SetPlayer(PlayerCharacter player)
    {
        ClearPlayer();
        Player = player;
        BlockInput = true;
    }

    /// <summary>세션 종료 때 이동 의도와 연결 해제</summary>
    public void ClearPlayer()
    {
        StopInput();
        Player = null;
        ClearInteractions();
    }

    /// <summary>수정의 범위 진입·이탈에서 후보를 갱신하고 현재 후보 변경만 알림</summary>
    public void SetInteraction(CrystalCheckPoint target, bool available)
    {
        // 같은 후보를 중복 등록하지 않고 해당 범위의 후보만 제거
        if (target == null) return;
        if (available)
        {
            if (target.IsActivated || interactionCandidates.Contains(target)) return;
            interactionCandidates.Add(target);
        }
        else
            interactionCandidates.Remove(target);

        // 마지막 진입 후보를 우선하고 겹친 다른 범위의 후보는 보존
        CrystalCheckPoint next = interactionCandidates.Count > 0 ? interactionCandidates[interactionCandidates.Count - 1] : null;
        if (InteractionTarget == next) return;
        InteractionTarget = next;
        OnInteractionChanged?.Invoke(next);
    }

    /// <summary>Player 교체·부활 경계에서 이전 몸체의 모든 상호작용 후보 정리</summary>
    public void ClearInteractions()
    {
        interactionCandidates.Clear();
        if (InteractionTarget == null) return;
        InteractionTarget = null;
        OnInteractionChanged?.Invoke(null);
    }

    /// <summary>차단 진입·비활성화·연결 해제 시 캐시와 이미 전달한 이동 초기화</summary>
    private void StopInput()
    {
        ClearActionCache();
        if (Player != null)
        {
            Player.SetMoveInput(Vector2.zero);
            Player.CancelDash();
            Player.CancelAttack();
            Player.CancelDraw();
            Player.CancelSlash();
            Player.CancelInteraction();
        }
    }

    /// <summary>커서 화면 좌표를 현재 Player 평면의 월드 좌표로 투영</summary>
    private void UpdateAimPosition()
    {
        if (worldCamera == null || Player == null) return;
        Ray ray = worldCamera.ScreenPointToRay(aimInput);
        Plane playerPlane = new Plane(Vector3.forward, Player.transform.position);
        if (!playerPlane.Raycast(ray, out float distance)) return;
        AimWorldPosition = ray.GetPoint(distance);
        Player.SetAimPosition(AimWorldPosition);
    }

    /// <summary>모드 전환과 외부 차단 경계에서 버튼·이동 캐시만 정리</summary>
    private void ClearActionCache()
    {
        dashRequested = drawRequested = pointRequested = resetRequested = confirmRequested = slashRequested = false;
        AttackHeld = false;
        attackRequested = false;
        MoveInput = Vector2.zero;
    }
}
