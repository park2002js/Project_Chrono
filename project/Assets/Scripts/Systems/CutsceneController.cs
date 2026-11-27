using System;
using System.Collections;
using UnityEngine;

/// <summary>Boss 연출 요청의 종류를 구분</summary>
public enum CutsceneKind
{
    BossEncounter, // 처음 만나는 Boss의 등장
    BossReencounter, // 이미 만난 Boss의 재도전
    BossDeath // Boss 처치 뒤 사체 전환
}

/// <summary>Boss 연출의 입력·시간·HUD·카메라를 소유하고 완료·중단 때 복구</summary>
public class CutsceneController : MonoBehaviour
{
    public bool IsPlaying { get; private set; } // 연출이 준비한 표시와 차단의 소유 여부
    public CutsceneKind CurrentKind { get; private set; } // 현재 Boss 연출의 구분
    public bool WasCompleted { get; private set; } // 마지막 연출이 중단 없이 완료됐는지 확인

    private GameManager manager; // 앱 수명 동안 유지할 시간 관리자
    private PlayerInputController input; // 세션 준비 때 한 번 확보할 입력
    private UIController ui; // 세션 준비 때 한 번 확보할 HUD
    private GameCamera gameCamera; // 세션 준비 때 한 번 확보할 게임 카메라
    private PlayerCharacter player; // 이번 연출의 조작 제한 대상
    private BossCharacter boss; // 이번 연출의 초점과 행동 제한 대상
    private Transform previousTarget; // 연출 종료 뒤 복원할 카메라 대상
    private bool previousInput; // 연출 이전 입력 차단 상태
    private bool previousPlayerCutscene; // 연출 이전 Player 행동 제한
    private bool previousBossCutscene; // 연출 이전 Boss 행동 제한
    private bool previousPlayerHud; // 연출 이전 Player HUD 표시
    private bool previousBossHud; // 연출 이전 Boss HUD 표시
    private int version; // 중단된 실행의 뒤늦은 완료 차단

    /// <summary>자기 앱 관리자만 캐시하고 씬 서비스는 월드 생성 뒤 연결</summary>
    private void Awake() => manager = GetComponent<GameManager>();

    private void OnDisable() => Cancel();

    /// <summary>새 MainWorld의 모든 Awake·Start 이후 씬 수명 참조를 한 번 갱신</summary>
    public void Prepare()
    {
        Cancel();
        input = PlayerInputController.Instance;
        ui = UIController.Instance;
        gameCamera = GameCamera.Instance;
        if (manager == null || input == null || ui == null || gameCamera == null)
            throw new InvalidOperationException("Boss cutscene requires prepared world services.");
    }

    /// <summary>요청한 Boss를 비추고 실제 시간의 임시 연출이 끝나면 원래 표시 복구</summary>
    public IEnumerator Play(PlayerCharacter actor, BossCharacter target, CutsceneKind kind, float duration)
    {
        // 연출의 대상·시간을 확인하고 이전 요청을 먼저 닫기
        if (actor == null || target == null || float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            throw new ArgumentException("Boss cutscene requires actors and a finite nonnegative duration.");
        Cancel();
        int ticket = ++version;
        player = actor;
        boss = target;
        CurrentKind = kind;
        WasCompleted = false;
        previousPlayerCutscene = player.IsCutScene;
        previousBossCutscene = boss.IsCutScene;

        // Draw·Slash의 종료 사건이 정상 표시를 복구한 뒤 그 상태를 보관
        player.IsCutScene = true;
        boss.IsCutScene = true;
        previousInput = input.BlockInput;
        previousPlayerHud = ui.IsPlayerHudVisible;
        previousBossHud = ui.IsBossHudVisible;
        previousTarget = gameCamera.Target;

        // 기존 행동 취소가 자기 정지를 해제한 뒤 Boss 연출의 정지를 적용
        IsPlaying = true;
        input.BlockInput = true;
        ui.SetPlayerHudVisible(false);
        ui.SetBossHudVisible(false);
        gameCamera.Follow(boss.transform, temporary: true);
        manager.SetBossCutscenePaused(true);

        // 최종 애니메이션 연결 전에도 시작·완료·중단 경계를 실제 시간으로 유지
        float remaining = duration;
        while (IsPlaying && ticket == version && remaining > 0f)
        {
            yield return null;
            remaining -= Time.unscaledDeltaTime;
        }
        if (IsPlaying && ticket == version) Complete();
    }

    /// <summary>정상 연출의 완료 상태를 보관하고 표시 복구</summary>
    public void Complete()
    {
        if (!IsPlaying) return;
        WasCompleted = true;
        Restore();
    }

    /// <summary>세션 이탈·비활성화에서 자기 연출만 중단하고 표시 복구</summary>
    public void Cancel()
    {
        version++;
        if (!IsPlaying) return;
        WasCompleted = false;
        Restore();
    }

    /// <summary>자기 요청이 바꾼 대상·입력·표시·정지 상태를 한 번 복원</summary>
    private void Restore()
    {
        IsPlaying = false;
        if (player != null) player.IsCutScene = previousPlayerCutscene;
        if (boss != null) boss.IsCutScene = previousBossCutscene;
        if (input != null) input.BlockInput = previousInput;
        if (ui != null)
        {
            ui.SetPlayerHudVisible(previousPlayerHud);
            ui.SetBossHudVisible(previousBossHud);
        }
        if (gameCamera != null) gameCamera.Follow(previousTarget);
        if (manager != null) manager.SetBossCutscenePaused(false);
        player = null;
        boss = null;
        previousTarget = null;
    }
}
