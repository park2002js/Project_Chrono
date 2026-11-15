using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>수정 범위의 상호작용 후보와 최초 활성화·회복·저장 요청 관리</summary>
public class CrystalCheckPoint : MonoBehaviour
{
    public string CheckpointId = "start"; // 저장된 재생성 위치와 활성 이력을 연결할 수정 식별자
    public string BossId; // Map이 활성화 알림에서 열 출입문을 찾을 Boss 식별자
    public Transform SpawnPosition; // 이 수정 활성화 뒤 Player가 재생성될 위치
    public bool IsActivated { get; private set; } // 최초 활성화와 저장 이력 적용이 끝난 상태
    public bool IsActivating { get; private set; } // 같은 입력·콜백의 활성화 재진입 방지
    public float ActivationDuration = 0.2f; // 임시 상호작용 연출 완료까지 기다릴 실제 시간의 초기 튜닝값
    public Animator CrystalAnimator; // 활성 색과 부유 클립을 재생할 완성 수정 아트
    public SpriteRenderer InteractionPrompt; // 실제 상호작용 가능 상태에서 표시할 F 자판 이미지
    public Collider2D InteractionTrigger { get; private set; } // 최초 활성화 때 제거할 상호작용 범위
    public event Action<CrystalCheckPoint> OnActivated; // 저장 요청까지 끝난 수정을 Map 등 자기 책임의 구독자에게 알릴 사건
    public event Action<CrystalCheckPoint> OnActivationFinished; // 완료·취소의 상태 정리 뒤 Player 등 자기 책임의 구독자에게 알릴 사건

    private PlayerInputController input; // MainWorld 준비 후 한 번 확보할 공통 F 입력·후보 창구
    private PlayerCharacter nearbyPlayer; // 범위 안에서 이 수정의 후보를 보유한 현재 Player
    private readonly HashSet<Collider2D> overlaps = new HashSet<Collider2D>(); // 여러 Player Collider 중 일부만 나가도 후보를 유지할 접촉 기록
    private bool initialized; // Map의 식별자·저장 이력과 입력 연결이 준비됐는지 조회
    private Coroutine activationRoutine; // 범위 이탈·외부 중단에서 종료할 진행 중 상호작용
    private UIController ui; // 초기화 때 한 번 확보할 실제 F 후보·입력 허용 표시 상태

    /// <summary>다른 객체 생성과 무관한 자기 범위 컴포넌트 준비</summary>
    private void Awake()
    {
        InteractionTrigger = GetComponent<Collider2D>();
        if (InteractionTrigger != null) InteractionTrigger.isTrigger = true;
        if (InteractionPrompt != null) InteractionPrompt.enabled = false;
    }

    /// <summary>현재 후보와 입력 상태만 읽어 수정 위의 준비된 자판 표시 갱신</summary>
    private void LateUpdate()
    {
        if (InteractionPrompt != null)
            InteractionPrompt.enabled = !IsActivated && !IsActivating && ui != null && ui.InteractionTarget == this && ui.IsInteractionVisible;
    }

    private void OnDisable()
    {
        CancelActivation();
        ClearInteraction();
        if (InteractionPrompt != null) InteractionPrompt.enabled = false;
    }

    /// <summary>현재 입력 Player가 범위에 들어오면 후보로 등록</summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Map 초기화와 입력 Player 연결이 끝난 현재 접촉만 수집
        if (!isActiveAndEnabled || !initialized || IsActivated || input == null) return;
        PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
        if (player == null || input.Player != player) return;
        if (nearbyPlayer != player)
        {
            CancelActivation();
            ClearInteraction();
        }
        overlaps.Add(other);
        SetInteraction(player, true);
    }

    /// <summary>마지막 Player Collider가 범위를 벗어나면 활성화 없이 후보만 해제</summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!overlaps.Remove(other) || overlaps.Count > 0) return;
        CancelActivation();
        ClearInteraction();
    }

    /// <summary>Map 배치 검증 뒤 저장된 활성 이력을 적용하고 입력 참조 한 번 확보</summary>
    public void Initialize(MapManager map, SaveData save)
    {
        // Map과 저장 구조가 준비된 시점에 식별·배치·공통 입력 연결 확인
        if (map == null || !map.IsInitialized || save == null || string.IsNullOrWhiteSpace(CheckpointId))
            throw new InvalidOperationException("Checkpoint requires an initialized Map and save data.");
        SpawnPosition = map.GetCheckpoint(CheckpointId);
        if (InteractionTrigger == null) InteractionTrigger = GetComponent<Collider2D>();
        if (InteractionTrigger != null) InteractionTrigger.isTrigger = true;
        input = PlayerInputController.Instance;
        ui = UIController.Instance;
        if (input == null) throw new InvalidOperationException("Checkpoint requires the MainWorld input service.");
        initialized = true;

        // 이미 활성화한 수정은 회복·저장 알림 없이 상호작용 범위만 제거
        IsActivated = save.Get<List<string>>("ActivatedCheckpoints").Contains(CheckpointId);
        if (CrystalAnimator != null) CrystalAnimator.SetBool("Active", IsActivated);
        if (IsActivated) RemoveTrigger();
        else if (InteractionTrigger == null)
            throw new InvalidOperationException("An inactive checkpoint requires its interaction trigger.");
    }

    /// <summary>범위 접촉이 전달한 Player의 공통 F 후보를 등록하거나 해제</summary>
    public void SetInteraction(PlayerCharacter player, bool available)
    {
        if (!available)
        {
            if (nearbyPlayer == player)
            {
                CancelActivation();
                ClearInteraction();
            }
            return;
        }
        if (!isActiveAndEnabled || !initialized || IsActivated || player == null || input == null || input.Player != player) return;
        nearbyPlayer = player;
        input.SetInteraction(this, true);
    }

    /// <summary>Character가 허용한 최초 F 상호작용의 연출 완료 대기 시작</summary>
    public bool activateCheckPoint(PlayerCharacter player)
    {
        // 수정의 상태·실제 범위와 공통 저장 도구 준비만 확인
        if (!isActiveAndEnabled || !initialized || IsActivated || IsActivating || player == null || player != nearbyPlayer || SaveManager.Current == null
            || float.IsNaN(ActivationDuration) || float.IsInfinity(ActivationDuration) || ActivationDuration <= 0f || !ContainsPlayer())
            return false;
        IsActivating = true;
        activationRoutine = StartCoroutine(ActivateAfterDelay(player, Time.unscaledTimeAsDouble + ActivationDuration)); // 연출이 끝난 뒤에만 회복·저장할 상호작용 시작
        return true;
    }

    /// <summary>완료 전 중단은 활성화·회복·저장 없이 대기만 종료하고 정리 알림</summary>
    public void CancelActivation()
    {
        if (!IsActivating) return;
        if (activationRoutine != null) StopCoroutine(activationRoutine); // 범위 이탈·외부 상태 변경으로 진행 중 연출 대기 중단
        activationRoutine = null;
        IsActivating = false;
        OnActivationFinished?.Invoke(this);
    }

    /// <summary>최소 한 프레임의 연출 경계 뒤 회복·기록·저장을 확정하고 완료 알림</summary>
    private IEnumerator ActivateAfterDelay(PlayerCharacter player, double completesAt)
    {
        // 시작 콜백과 완료를 분리하고 게임 정지와 무관한 임시 연출 시간 대기
        yield return null;
        while (Time.unscaledTimeAsDouble < completesAt) yield return null;
        activationRoutine = null;
        if (!IsActivating) yield break;
        if (!isActiveAndEnabled || player == null || player != nearbyPlayer || !ContainsPlayer())
        {
            CancelActivation();
            yield break;
        }

        // 재진입 전에 최초 사용을 확정하고 회복한 값과 수정 이력 저장 요청
        IsActivated = true;
        if (CrystalAnimator != null) CrystalAnimator.SetBool("Active", true);
        if (InteractionPrompt != null) InteractionPrompt.enabled = false;
        player.Heal(player.maxHP);
        player.Groggy = 0f;
        SaveData save = SaveManager.Current;
        List<string> activated = save.Get<List<string>>("ActivatedCheckpoints");
        if (!activated.Contains(CheckpointId)) activated.Add(CheckpointId);
        save.Set("ActivatedCheckpoints", activated);
        save.Set("CheckpointId", CheckpointId);
        save.Set("PlayerHP", player.HP);
        SaveManager.Save();

        // 범위·후보와 진행 상태를 닫은 뒤 각 구독자의 완료 책임에 알림
        IsActivating = false;
        RemoveTrigger();
        try
        {
            OnActivated?.Invoke(this);
        }
        finally
        {
            OnActivationFinished?.Invoke(this);
        }
    }

    /// <summary>이동·순간 재배치 뒤에도 실제 겹친 Player Collider가 남아 있는지 확인</summary>
    private bool ContainsPlayer()
    {
        if (InteractionTrigger == null || !InteractionTrigger.enabled) return false;
        Physics2D.SyncTransforms();
        foreach (Collider2D other in overlaps)
            if (other != null && other.enabled && other.gameObject.activeInHierarchy && InteractionTrigger.Distance(other).isOverlapped) return true;
        return false;
    }

    /// <summary>활성화·범위 제거에서 자기 후보와 접촉 기록만 해제</summary>
    private void ClearInteraction()
    {
        if (input != null) input.SetInteraction(this, false);
        overlaps.Clear();
        nearbyPlayer = null;
    }

    /// <summary>한 번 사용한 범위는 즉시 판정을 닫고 해당 Collider만 제거</summary>
    private void RemoveTrigger()
    {
        ClearInteraction();
        if (InteractionTrigger == null) return;
        InteractionTrigger.enabled = false;
        Destroy(InteractionTrigger);
    }
}
