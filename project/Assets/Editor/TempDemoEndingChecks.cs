using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 경비 OnDie의 암전·문구·Title 복귀와 중단·새 세션 재사용 검사</summary>
public static class TempDemoEndingChecks
{
    /// <summary>기존 실행기의 격리 저장에서 두 실제 세션의 엔딩과 해제 확인</summary>
    public static IEnumerator Run(GameManager manager, Action<string> passed)
    {
        // 기본 시간과 실제 생성 프리팹·구독 연결 확인
        TempDemoEnding ending = manager.Stage.DemoEnding;
        Require(ending != null && ending.FadeDuration == 2f && ending.MessageDuration == 3f, "GameSystems must provide the configured demo ending.");
        Require(manager.StartGame(0, true), "First isolated session must start.");
        yield return Ready(manager); // 실제 입장 또는 기상과 입력 인계 완료 대기
        BossCharacter firstBoss = manager.CurrentBoss;
        PlayerCharacter firstPlayer = manager.CurrentPlayer;
        ending.Bind();
        ending.Bind();
        Require(!ending.IsPlaying, "Binding alone must never play an ending.");
        Require(Kill(firstBoss, firstPlayer), "First guard must accept lethal damage.");
        Require(ending.IsPlaying && firstPlayer.IsCutScene && PlayerInputController.Instance.BlockInput
            && UIController.Instance.IsDeathMode && !manager.Stage.IsBossSequencePlaying,
            "Actual OnDie must transfer control from the old death cutscene to a single locked demo ending.");
        Require(SaveManager.Current.Get<List<string>>("DefeatedBosses").Contains(GameManager.SubmittedBossId), "Defeat must remain recorded before the ending.");
        passed("Actual guard OnDie starts one ending after defeat storage, cancels the old death sequence and blocks Player/HUD");

        // 다른 정지 소유가 있어도 실제 시간 암전이 진행되고 중복 피해는 재시작하지 않음
        manager.SetGameplayPaused(true);
        yield return new WaitForSecondsRealtime(0.35f); // timeScale0에서 부분 암전 표본 확보
        float partial = ending.FadeAlpha;
        Require(partial > 0f && partial < 1f && !ending.IsMessageVisible && !Kill(firstBoss, firstPlayer), "Fade must progress under pause, hide text until black and reject repeated lethal damage.");
        manager.ReturnToTitle();
        yield return Until(() => AtTitle(manager)); // 부분 암전 중 Title 복귀의 세션 해제 완료 대기
        Require(!ending.IsPlaying && ending.FadeAlpha == 0f && !ending.IsMessageVisible && !manager.IsBossCutscenePaused
            && manager.CurrentPlayer == null, "Interrupted ending must release overlay, actors and owned pause.");
        yield return new WaitForSecondsRealtime(2.1f); // 취소된 첫 암전 코루틴의 지연 복귀·문구 표시 누출 검사
        Require(AtTitle(manager) && !ending.IsPlaying, "Cancelled coroutine must not resume at Title.");
        passed("Partial fade advances during timeScale0, repeated death is rejected and early Title cancels all ending state");

        // 같은 앱 컴포넌트가 새 경비에만 연결돼 기본 시간으로 끝까지 실행
        Require(manager.StartGame(1, true), "Second isolated session must start after cancellation.");
        yield return Ready(manager); // 새 Player 입장과 경비 구독 준비 완료 대기
        BossCharacter boss = manager.CurrentBoss;
        PlayerCharacter player = manager.CurrentPlayer;
        Require(boss != firstBoss && ending == manager.Stage.DemoEnding && !ending.IsPlaying, "New session must reuse the app ending with a new guard.");
        double started = Time.realtimeSinceStartupAsDouble;
        Require(Kill(boss, player), "Second guard must accept lethal damage.");
        yield return Until(() => ending.IsMessageVisible); // 기본2초 암전 완료와 문구 표시 시작 대기
        Require(ending.FadeAlpha == 1f && player.IsCutScene && PlayerInputController.Instance.BlockInput
            && manager.IsBossCutscenePaused && Time.timeScale == 0f && Time.realtimeSinceStartupAsDouble - started >= 1.9d,
            "Text must begin only after the full fade, with cutscene, input and gameplay pause held.");
        Require(!player.ApplyAttack() && !player.ApplyDraw() && !player.ApplyDash(), "Ending must reject new combat actions.");
        passed("A fresh guard triggers the reused component; full black precedes text and locks combat while gameplay is paused");
        yield return new WaitForSecondsRealtime(1f); // 안내가 즉시 Title로 사라지지 않고 유지되는지 확인
        Require(ending.IsMessageVisible && manager.IsSessionActive && !manager.IsLoading, "Message must remain visible before its three-second hold completes.");
        yield return Until(() => AtTitle(manager)); // 문구3초 뒤 기존 ReturnToTitle·슬롯 재로드 완료 대기
        double elapsed = Time.realtimeSinceStartupAsDouble - started;
        Require(elapsed >= 4.9d && !ending.IsPlaying && !ending.IsMessageVisible && ending.FadeAlpha == 0f
            && !manager.IsBossCutscenePaused && Time.timeScale == 1f && manager.CurrentBoss == null,
            "Full ending must hold the default timing, return to Title and release locks for the next game.");
        Require(SaveManager.Slots[1].Get<List<string>>("DefeatedBosses").Contains(GameManager.SubmittedBossId)
            && !manager.StartGame(1), "Reloaded completed slot must preserve defeat and require new-game confirmation.");
        passed("Default two-second fade and three-second message return to Title, clear overlay/pause and preserve the completed slot");
    }

    /// <summary>실제 피해 API를 통해서만 경비의 일회 사망 사건 발생</summary>
    private static bool Kill(BossCharacter boss, PlayerCharacter player)
    {
        return boss.ApplyDamage(new DamageInstance(player, boss.maxHP * 2f, DamageType.Effect, DamageInstance.NextAttackId(), false));
    }

    /// <summary>최초 입장과 입력 인계까지 끝난 실제 MainWorld 대기</summary>
    private static IEnumerator Ready(GameManager manager)
    {
        yield return Until(() => manager.Stage.IsReady && !manager.IsLoading && manager.CurrentPlayer != null
            && !manager.CurrentPlayer.IsCutScene && !PlayerInputController.Instance.BlockInput);
    }

    /// <summary>세션 정리·Title 로드·슬롯 재로드 완료 여부 확인</summary>
    private static bool AtTitle(GameManager manager)
    {
        return !manager.IsSessionActive && !manager.IsLoading && SaveManager.IsReady
            && SceneManager.GetActiveScene().name == "Title";
    }

    /// <summary>실제 프레임을 진행하며 제한 시간 안에서 조건 대기</summary>
    private static IEnumerator Until(Func<bool> predicate)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 25d;
        while (!predicate())
        {
            Require(Time.realtimeSinceStartupAsDouble < deadline, "Demo ending wait timed out.");
            yield return null;
        }
    }

    /// <summary>실제 상태가 다르면 기존 실행기의 결과에 실패 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
