using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>실제 수정 범위·F 우선순위·최초 저장·문과 재생성 위치 연결 확인</summary>
public static class PlayerInteractionChecks
{
    /// <summary>기존 검사 장치를 재사용하고 실제 mid 활성 진행을 다음 부활 검사에 유지</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, Keyboard keyboard, Action<string> passed)
    {
        // 실제 맵의 미활성 수정과 복원할 임시 검사 상태 보관
        MapManager map = manager.Stage.Map;
        PlayerCharacter player = manager.CurrentPlayer;
        SkillController skills = player.Skills;
        UIController ui = UIController.Instance;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        CrystalCheckPoint crystal = null;
        foreach (CrystalCheckPoint candidate in map.GetComponentsInChildren<CrystalCheckPoint>(true))
            if (candidate.CheckpointId == "mid") crystal = candidate;
        Require(crystal != null && !crystal.IsActivated, "Checkpoint checks require the fresh session's inactive mid crystal.");
        GameObject door = map.GetEntryDoor(crystal.BossId);
        Require(door != null, "The mid crystal must control its Boss entry door.");
        MapDoor doorMotion = door.GetComponent<MapDoor>();
        Require(doorMotion != null ? !doorMotion.WantsOpen && doorMotion.Blocker.enabled : door.activeSelf,
            "The mid crystal must control its initially closed Boss entry door.");
        MapManager.CheckpointSpawn[] checkpoints = map.Checkpoints;
        string originalCheckpoint = SaveManager.Current.Get<string>("CheckpointId");
        float health = player.HP;
        float groggy = player.Groggy;
        float lifetime = skills.LineLifetime;
        float cancelCooldown = skills.CancelDrawCooldown;
        float slashDuration = skills.SlashDuration;
        float activationDuration = crystal.ActivationDuration;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        var fixtures = new List<GameObject>();
        bool activated = false;
        try
        {
            // 물리 범위와 실제 F만으로 진행하도록 일반 조작·능력 상태 준비
            input.enabled = true;
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            skills.ResetSkills();
            skills.LineLifetime = 30f;
            skills.CancelDrawCooldown = 0f;
            skills.SlashDuration = 0.25f;
            crystal.ActivationDuration = 0.35f;
            Keys(keyboard);
            SetPosition(body, (Vector2)crystal.SpawnPosition.position + Vector2.left * 3f);
            yield return Frames(3); // 이전 입력과 범위 이탈의 물리 접촉 해제 대기
            Require(input.InteractionTarget == null, "The Player must begin outside any inactive crystal range.");

            // 진입은 후보·안내만 만들고 차단·정지·사망 UI에서는 활성화 없이 안내 숨김
            SetPosition(body, crystal.SpawnPosition.position);
            yield return Until(() => input.InteractionTarget == crystal, "enter real crystal range"); // 실제 Trigger가 Player의 여러 Collider를 후보로 등록할 때까지 대기
            Require(ui.InteractionTarget == crystal && ui.IsInteractionVisible && !crystal.IsActivated && SaveManager.Current.Get<string>("CheckpointId") == originalCheckpoint,
                "Entering a crystal must publish an F candidate without healing, activation or save progression.");
            input.BlockInput = true;
            Require(!ui.IsInteractionVisible, "Blocked input must hide the F interaction guide.");
            yield return Press(keyboard, Key.F); // 차단 중 실제 F가 수정 활성화로 전달되지 않는지 관찰
            input.BlockInput = false;
            manager.SetGameplayPaused(true);
            Require(!ui.IsInteractionVisible, "Ordinary pause must hide the F guide.");
            yield return Press(keyboard, Key.F); // 정지 중 누른 F가 정지 해제 이후 예약되지 않는지 관찰
            manager.SetGameplayPaused(false);
            bool playerHud = ui.IsPlayerHudVisible;
            bool bossHud = ui.IsBossHudVisible;
            ui.SetDeathMode(true);
            Require(!ui.IsInteractionVisible && !ui.IsPlayerHudVisible && !ui.IsBossHudVisible, "Death mode must hide interaction and both HUDs.");
            ui.SetDeathMode(false);
            Require(ui.IsInteractionVisible && ui.IsPlayerHudVisible == playerHud && ui.IsBossHudVisible == bossHud,
                "Leaving death mode must restore the previous HUD state and current interaction guide.");
            SetPosition(body, (Vector2)crystal.SpawnPosition.position + Vector2.left * 3f);
            yield return Until(() => input.InteractionTarget == null, "leave crystal range"); // 마지막 Player Collider의 이탈이 후보만 해제할 때까지 대기
            Require(!ui.IsInteractionVisible && !crystal.IsActivated && (doorMotion != null ? !doorMotion.WantsOpen && doorMotion.Blocker.enabled : door.activeSelf)
                && SaveManager.Current.Get<string>("CheckpointId") == originalCheckpoint,
                "Leaving range must not activate the crystal, change checkpoint or open the entry door.");
            passed("F-P10 real trigger entry and exit only update the F candidate, while block, pause and death UI hide interaction without activating or reserving input");

            // 겹친 수정 중 한 후보를 제거해도 남은 범위와 여러 Player Collider의 후보 유지
            Vector2 overlapPosition = new Vector2(220f, 220f);
            CrystalCheckPoint first = CreateCrystal(fixtures, "interaction-a", overlapPosition);
            CrystalCheckPoint second = CreateCrystal(fixtures, "interaction-b", overlapPosition + Vector2.right * 0.1f);
            var expanded = new List<MapManager.CheckpointSpawn>(checkpoints)
            {
                new MapManager.CheckpointSpawn { Id = first.CheckpointId, Position = first.transform },
                new MapManager.CheckpointSpawn { Id = second.CheckpointId, Position = second.transform }
            };
            map.Checkpoints = expanded.ToArray();
            first.Initialize(map, SaveManager.Current);
            second.Initialize(map, SaveManager.Current);
            SetPosition(body, overlapPosition);
            yield return Until(() => input.InteractionTarget == first || input.InteractionTarget == second, "overlapping crystal candidates"); // 두 실제 범위의 후보 등록 대기
            yield return Frames(2); // 두 Trigger의 진입 콜백이 모두 반영될 때까지 대기
            CrystalCheckPoint removed = input.InteractionTarget;
            CrystalCheckPoint remaining = removed == first ? second : first;
            removed.gameObject.SetActive(false);
            Require(input.InteractionTarget == remaining && ui.InteractionTarget == remaining, "Removing one overlapping crystal must restore the other live candidate.");
            var extra = new GameObject("InteractionExtraPlayerCollider");
            fixtures.Add(extra);
            extra.transform.SetParent(player.transform, false);
            BoxCollider2D extraShape = extra.AddComponent<BoxCollider2D>();
            extraShape.size = Vector2.one * 0.1f;
            extraShape.isTrigger = true;
            Physics2D.SyncTransforms();
            yield return Frames(3); // 추가 Player Collider가 현재 수정 범위에 들어오는 물리 갱신 대기
            extra.transform.localPosition = Vector2.right * 10f;
            Physics2D.SyncTransforms();
            yield return Frames(3); // 일부 Collider만 나가도 몸체가 남은 후보를 유지하는지 관찰
            Require(input.InteractionTarget == remaining, "One child Collider exiting must not clear a crystal still overlapping the Player body.");

            // 연출 완료 전 차단·컷씬·Player 비활성·범위 이탈은 회복·저장 없이 중단
            remaining.ActivationDuration = 0.3f;
            player.HP = player.maxHP * 0.4f;
            player.Groggy = 30f;
            float storedHealth = SaveManager.Current.Get<float>("PlayerHP");
            int storedCount = SaveManager.Current.Get<List<string>>("ActivatedCheckpoints").Count;
            int finished = 0;
            Action<CrystalCheckPoint> onCancelled = value => finished++;
            remaining.OnActivationFinished += onCancelled; // 중단에서도 상태 정리 후 완료 사건을 한 번 보내는지 수집
            try
            {
                Action[] cancel = { () => input.BlockInput = true, () => player.IsCutScene = true, () => player.enabled = false };
                foreach (Action interrupt in cancel)
                {
                    bool accepted = player.ApplyInteraction(remaining);
                    Require(accepted && player.IsInteracting && player.IsInvulnerable && !player.CanMove
                        && remaining.IsActivating && !remaining.IsActivated && Mathf.Approximately(player.HP, player.maxHP * 0.4f),
                        $"Interaction must defer recovery until completion: accepted={accepted}, interacting={player.IsInteracting}, protected={player.IsInvulnerable}, canMove={player.CanMove}, activating={remaining.IsActivating}, activated={remaining.IsActivated}, HP={player.HP:R}/{player.maxHP:R}, active={player.isActiveAndEnabled}, dead={player.IsDead}, cutscene={player.IsCutScene}, inputBlock={player.IsInputBlocked}, timeScale={Time.timeScale}, finished={finished}, distance={remaining.InteractionTrigger.Distance(player.GetComponent<Collider2D>()).distance}.");
                    yield return Frames(1); // 최초 프레임을 분리한 연출 대기가 실제로 유지되는지 관찰
                    interrupt();
                    Require(!remaining.IsActivating && !remaining.IsActivated && !player.IsInteracting && !player.IsInvulnerable,
                        "Block, cutscene or Player disable must synchronously cancel an unfinished interaction.");
                    input.BlockInput = false;
                    player.IsCutScene = false;
                    player.enabled = true;
                }
                Require(player.ApplyInteraction(remaining), "A cancelled crystal must remain available for another interaction.");
                SetPosition(body, overlapPosition + Vector2.left * 3f);
                yield return Until(() => !remaining.IsActivating && input.InteractionTarget == null, "range exit cancels activation"); // 연출 중 실제 마지막 Collider 이탈이 활성화 없이 중단할 때까지 대기
                Require(finished == 4 && Mathf.Approximately(player.HP, player.maxHP * 0.4f) && player.Groggy == 30f
                    && SaveManager.Current.Get<string>("CheckpointId") == originalCheckpoint && SaveManager.Current.Get<float>("PlayerHP") == storedHealth
                    && SaveManager.Current.Get<List<string>>("ActivatedCheckpoints").Count == storedCount,
                    "Cancelled presentations must publish one finish each without healing, clearing groggy or updating any save progression.");
                SetPosition(body, overlapPosition);
                yield return Until(() => input.InteractionTarget == remaining, "return before crystal disable"); // 수정 자체 비활성화 중단을 확인할 새 범위 진입 대기
                Require(player.ApplyInteraction(remaining), "The crystal must allow another presentation before its own disable check.");
                ui.enabled = false;
                remaining.gameObject.SetActive(false);
                Require(finished == 5 && !player.IsInteracting && !remaining.IsActivating && !remaining.IsActivated
                    && Mathf.Approximately(player.HP, player.maxHP * 0.4f) && SaveManager.Current.Get<string>("CheckpointId") == originalCheckpoint,
                    "Disabling the crystal during presentation must cancel the actor lock without committing recovery or save progression.");
            }
            finally
            {
                remaining.OnActivationFinished -= onCancelled; // 미완 연출 중단 수집 종료
            }
            ui.enabled = false;
            remaining.gameObject.SetActive(false);
            Require(input.InteractionTarget == null, "Removing the last overlapping crystal must clear the input candidate.");
            ui.enabled = true;
            Require(ui.InteractionTarget == null, "Re-enabled UI must recover the latest candidate after its event subscription was suspended.");
            map.Checkpoints = checkpoints;
            passed("F-P10 overlapping ranges retain valid candidates and unfinished interaction cancels on block, cutscene, disable or range exit without healing or saving");

            // 실제 F는 Slash보다 먼저 수정 한 번만 활성화하고 알림 전에 저장·회복·범위 정리 완료
            SetPosition(body, crystal.SpawnPosition.position);
            yield return Until(() => input.InteractionTarget == crystal, "return to real crystal"); // 실제 수정 범위의 후보 재등록 대기
            player.HP = player.maxHP * 0.5f;
            player.Groggy = 70f;
            Line line = new Line((Vector2)player.transform.position + Vector2.up * 8f, Vector2.right, 6f, 0.25f);
            skills.SetLine(line);
            int activationEvents = 0;
            bool committedBeforeCallback = false;
            bool reentryAccepted = false;
            Action<CrystalCheckPoint> onActivated = value =>
            {
                activationEvents++;
                committedBeforeCallback = value.IsActivated && !value.IsActivating && player.HP == player.maxHP && player.Groggy == 0f
                    && SaveManager.Current.Get<string>("CheckpointId") == value.CheckpointId && SaveManager.Current.Get<float>("PlayerHP") == player.maxHP
                    && SaveManager.Current.Get<List<string>>("ActivatedCheckpoints").Contains(value.CheckpointId)
                    && input.InteractionTarget == null && (value.InteractionTrigger == null || !value.InteractionTrigger.enabled)
                    && player.Spawner.SpawnPoint == value.SpawnPosition
                    && (doorMotion != null ? doorMotion.WantsOpen && !doorMotion.IsOpen && doorMotion.Blocker.enabled : !door.activeSelf);
                reentryAccepted = value.activateCheckPoint(player);
            };
            crystal.OnActivated += onActivated; // Map의 캐시·문 처리 뒤 저장·회복·재진입 상태 수집
            try
            {
                Keys(keyboard, Key.F);
                yield return Until(() => crystal.IsActivating, "F starts checkpoint presentation"); // 실제 F가 최초 활성화의 연출 대기부터 시작할 때까지 대기
                Require(!crystal.IsActivated && player.IsInteracting && player.IsInvulnerable && !ui.IsInteractionVisible
                    && player.HP == player.maxHP * 0.5f && player.Groggy == 70f && SaveManager.Current.Get<string>("CheckpointId") == originalCheckpoint,
                    "The first F must begin its presentation without committing activation, recovery or save progression early.");
                yield return Until(() => crystal.IsActivated, "F activates checkpoint before Slash"); // 실제 일반 F가 Slash 대신 최초 수정 활성화로 전달될 때까지 대기
                activated = true;
                yield return Frames(3); // F를 계속 눌러도 후보 제거 뒤 Slash로 반복 전환되지 않는지 관찰
                Require(committedBeforeCallback && !reentryAccepted && activationEvents == 1 && !crystal.IsActivating
                    && !player.IsInteracting && !player.IsSlashing && player.CurrentLine.HasValue && !ui.IsInteractionVisible,
                    "One F press must activate exactly once before notifying observers and preserve the existing Slash Line.");
                Require(!player.ApplyInteraction(crystal), "An already activated crystal must reject later interaction.");
                Keys(keyboard);
                yield return Frames(2); // 저장 후 독립 F 입력을 검사하기 위한 키 해제 대기
                Require(crystal.InteractionTrigger == null, "Activation must remove the actual interaction Trigger after closing it immediately.");
            }
            finally
            {
                crystal.OnActivated -= onActivated; // 실제 F 활성화·재진입 수집 종료
            }
            yield return Until(() => doorMotion != null ? doorMotion.IsOpen && !doorMotion.Blocker.enabled : !door.activeSelf,
                "checkpoint entry door fully open"); // 모션이 완전히 열린 뒤에만 실제 통과 차단이 풀리는지 확인
            yield return SaveManager.FlushAsync().ToCoroutine(); // 검사에서만 실제 파일 기록이 끝날 때까지 대기
            var reloadedSave = new SaveData(Path.GetDirectoryName(SaveManager.Current.FilePath), SaveManager.SelectSlot);
            yield return reloadedSave.LoadAsync().ToCoroutine(); // 별도 SaveData로 암호화 저장 파일의 체크포인트 값 대조
            Require(reloadedSave.Get<string>("CheckpointId") == crystal.CheckpointId && reloadedSave.Get<float>("PlayerHP") == player.maxHP
                && reloadedSave.Get<List<string>>("ActivatedCheckpoints").Contains(crystal.CheckpointId),
                "The fire-and-forget save request must persist the healed HP, last checkpoint and activation history.");
            passed("F-P10 real F prioritizes checkpoint activation, commits save and spawn before observers, then opens passage only after the entry animation completes");

            // 저장된 활성 수정은 재생성 때 범위만 제거하고 후보가 없을 때 다음 독립 F는 Slash
            CrystalCheckPoint restored = CreateCrystal(fixtures, crystal.CheckpointId, new Vector2(240f, 240f));
            int restoredEvents = 0;
            Action<CrystalCheckPoint> onRestored = value => restoredEvents++;
            restored.OnActivated += onRestored; // 저장 이력 적용이 신규 활성화 알림을 다시 발생시키는지 수집
            try
            {
                restored.Initialize(map, reloadedSave);
                yield return Frames(2); // 저장된 활성 수정의 Trigger 제거가 실제 반영될 때까지 대기
                Require(restored.IsActivated && restored.InteractionTrigger == null && restoredEvents == 0 && !restored.activateCheckPoint(player),
                    "Loading an already activated crystal must remove its Trigger without a second activation, heal or save event.");
            }
            finally
            {
                restored.OnActivated -= onRestored; // 저장 이력 적용 수집 종료
            }
            Require(input.InteractionTarget == null && player.CurrentLine.HasValue, "The activated crystal must leave no candidate and preserve the previous Line.");
            Keys(keyboard, Key.F);
            yield return Until(() => player.IsSlashing, "F without interaction falls back to Slash"); // 후보가 없는 새 F 누름이 기존 Slash 입력으로 전달될 때까지 대기
            Require(!player.CurrentLine.HasValue, "The fallback Slash must consume the Line only on the independent second F press.");
            Keys(keyboard);
            yield return Until(() => !player.IsSlashing, "fallback Slash completes"); // 다음 부활 검사 전에 Slash의 시간·입력 상태 복구 대기
            passed("F-P10 saved activation removes the recreated Trigger without replaying activation and an independent F with no candidate falls back to Slash");
        }
        finally
        {
            // 실제 활성 진행은 유지하고 임시 후보·기하·수치와 입력만 복구
            Keys(keyboard);
            ui.enabled = true;
            ui.SetDeathMode(false);
            input.ClearInteractions();
            player.CancelSlash();
            player.CancelDraw();
            player.CancelInteraction();
            skills.ResetSkills();
            player.ResetMotion();
            map.Checkpoints = checkpoints;
            foreach (GameObject fixture in fixtures) if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            skills.LineLifetime = lifetime;
            skills.CancelDrawCooldown = cancelCooldown;
            skills.SlashDuration = slashDuration;
            crystal.ActivationDuration = activationDuration;
            player.HP = activated ? player.maxHP : health;
            player.Groggy = activated ? 0f : groggy;
            SetPosition(body, activated ? (Vector2)crystal.SpawnPosition.position : (Vector2)map.GetCheckpoint(originalCheckpoint).position);
            manager.SetGameplayPaused(paused);
            input.BlockInput = blocked;
        }
    }

    /// <summary>실제 Trigger를 가진 임시 수정 준비</summary>
    private static CrystalCheckPoint CreateCrystal(List<GameObject> fixtures, string id, Vector2 position)
    {
        var root = new GameObject("InteractionCrystal_" + id);
        fixtures.Add(root);
        root.transform.position = position;
        CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
        trigger.radius = 0.6f;
        trigger.isTrigger = true;
        CrystalCheckPoint crystal = root.AddComponent<CrystalCheckPoint>();
        crystal.CheckpointId = id;
        return crystal;
    }

    /// <summary>실제 키 누름·해제를 독립 입력으로 전달</summary>
    private static IEnumerator Press(Keyboard keyboard, Key key)
    {
        Keys(keyboard, key);
        yield return Frames(2); // 액션 콜백과 공통 F 전달 대기
        Keys(keyboard);
        yield return Frames(2); // 다음 독립 입력을 위한 키 해제 대기
    }

    /// <summary>검사용 키보드의 전체 현재 상태 전달</summary>
    private static void Keys(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    /// <summary>현재 Player의 Transform·물리 위치를 함께 적용</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        body.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>현재 게임 갱신에서 지정한 렌더 프레임 진행 관찰</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "render frames"); // 입력·물리·UI 전환이 반영될 프레임 대기
    }

    /// <summary>실제 범위·입력 상태 전이를 기다리고 무한 대기 방지</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P10 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>수정·입력·저장 연결의 계약 위반을 기존 결과 보고서에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
