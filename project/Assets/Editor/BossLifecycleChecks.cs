using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>실제 Stage의 조우·사망·재도전·저장·문과 몸체 차단 확인</summary>
public static class BossLifecycleChecks
{
    /// <summary>같은 세션의 Boss와 저장 기록으로 전환 순서·일회 종료 검사</summary>
    public static IEnumerator Run(GameManager manager, Action<string> passed)
    {
        // 전투 검사 뒤 기존 Player·진행·연출 설정을 복원할 원본 보관
        StageManager stage = manager.Stage;
        BossCharacter boss = manager.CurrentBoss;
        PlayerCharacter player = manager.CurrentPlayer;
        PlayerInputController input = PlayerInputController.Instance;
        UIController ui = UIController.Instance;
        GameCamera camera = GameCamera.Instance;
        BossSpawner spawner = boss.GetComponent<BossSpawner>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        GameObject entryDoor = stage.Map.GetEntryDoor(boss.BossId);
        GameObject exitDoor = stage.Map.GetExitDoor(boss.BossId);
        MapDoor entryMotion = entryDoor.GetComponent<MapDoor>();
        MapDoor exitMotion = exitDoor.GetComponent<MapDoor>();
        Vector2 initialPosition = body.position;
        var encountered = new List<string>(SaveManager.Current.Get<List<string>>("EncounteredBosses"));
        var defeated = new List<string>(SaveManager.Current.Get<List<string>>("DefeatedBosses"));
        float savedHP = SaveManager.Current.Get<float>("PlayerHP");
        float hp = player.HP;
        bool inputEnabled = input.enabled;
        bool inputBlock = input.BlockInput;
        float first = stage.FirstEncounterDuration;
        float repeat = stage.ReencounterDuration;
        float death = stage.BossDeathDuration;
        float slashDuration = player.Skills.SlashDuration;
        float slashCooldown = player.Skills.ConfirmSlashCooldown;
        LayerMask slashMask = player.Skills.SlashTargetMask;
        int starts = 0;
        Action started = () => starts++;
        stage.Cutscene.OnStarted += started; // 중복 Trigger 요청이 하나의 연출만 시작하는지 수집
        try
        {
            // 검사 전 실제 전투·진행을 초기화하고 외부 입력 간섭 차단
            stage.ResetEncounter();
            input.enabled = false;
            input.BlockInput = false;
            player.ResetMotion();
            player.Skills.ResetSkills();
            player.Buffs.Clear();
            SaveManager.Current.Set("EncounteredBosses", new List<string>());
            SaveManager.Current.Set("DefeatedBosses", new List<string>());
            spawner.ResetEncounter();
            stage.FirstEncounterDuration = 0.5f;
            stage.ReencounterDuration = 0.04f;
            stage.BossDeathDuration = 0.06f;
            stage.Map.SetEntryDoorOpen(boss.BossId, true);
            yield return Until(() => entryMotion != null ? entryMotion.IsOpen && !entryMotion.Blocker.enabled : !entryDoor.activeSelf,
                "entry opening before encounter"); // 실제 입구가 완전히 열린 뒤 조우 연출의 문 유지 상태 확인
            Require(boss.PatternsInitialized && !boss.IsDead && boss.HP == boss.maxHP && spawner.EncounterTrigger.enabled,
                "A living Boss must finish its pattern and health initialization before encounter activation.");
            Require(boss.ApplyDamage(Damage(player, 1f)), "A live Boss must accept valid damage outside combat.");
            passed("F-B01 living Boss initializes once at the map position and F-B02 valid damage works outside combat");

            // 실제 Player 몸체를 조우 Trigger 안에 배치해 물리 진입부터 확인
            Vector2 encounterPosition = spawner.EncounterTrigger.bounds.center;
            body.position = encounterPosition;
            player.transform.position = encounterPosition;
            Physics2D.SyncTransforms();
            yield return Until(() => stage.IsBossSequencePlaying, "physical Player encounter trigger"); // 실제 OnTriggerEnter2D가 Stage의 조우 준비를 시작할 때까지 대기
            stage.BeginEncounter(boss);
            Require(stage.IsBossSequencePlaying && stage.Cutscene.CurrentKind == CutsceneKind.BossEncounter
                && input.BlockInput && manager.IsBossCutscenePaused && player.IsCutScene && boss.IsCutScene
                && !boss.IsInCombat && !ui.IsBossHudVisible && (entryMotion != null ? entryMotion.IsOpen && !entryMotion.Blocker.enabled : !entryDoor.activeSelf)
                && SaveManager.Current.Get<List<string>>("EncounteredBosses").Count == 0 && starts == 1,
                "First encounter must block duplicates and defer combat, door and record until presentation completes.");
            yield return Until(() => !stage.IsBossSequencePlaying, "first encounter"); // 첫 조우 연출 이후의 전투 전환 관찰
            Require(boss.IsInCombat && player.IsInCombat && boss.Target == player && ui.Boss == boss && ui.IsBossHudVisible
                && (entryMotion != null ? !entryMotion.WantsOpen && !entryMotion.IsOpen && entryMotion.Blocker.enabled : entryDoor.activeSelf)
                && !input.BlockInput && !manager.IsBossCutscenePaused
                && camera.Target == player.transform && SaveManager.Current.Get<List<string>>("EncounteredBosses").Count == 1,
                "Completed encounter must record once, close entrance, show HUD and connect both combatants.");
            passed("F-B02 real Player OnTriggerEnter2D starts first encounter, waits for unscaled presentation, rejects duplicate requests and then records, closes door, shows HUD and starts combat");

            // 후속 취소 검사에서 Trigger 재활성화가 새 물리 진입을 만들지 않도록 원위치 복원
            body.position = initialPosition;
            player.transform.position = initialPosition;
            Physics2D.SyncTransforms();
            stage.FirstEncounterDuration = 0.08f;

            // 취소된 첫 조우는 저장하지 않고 기존 외부 정지를 보존
            stage.ResetEncounter();
            SaveManager.Current.Set("EncounteredBosses", new List<string>());
            manager.SetGameplayPaused(true);
            input.BlockInput = true;
            stage.BeginEncounter(boss);
            stage.Cutscene.Cancel();
            yield return Until(() => !stage.IsBossSequencePlaying, "cancelled encounter"); // 외부 중단의 조우 기록·재무장 경계 확인
            Require(!boss.IsInCombat && spawner.EncounterTrigger.enabled && input.BlockInput
                && manager.IsGameplayPaused && !manager.IsBossCutscenePaused && Time.timeScale == 0f
                && SaveManager.Current.Get<List<string>>("EncounteredBosses").Count == 0,
                "Cancelled encounter must not record or start combat and must preserve an external pause/block.");

            // 공개 중단과 시작 콜백의 즉시 중단도 조우 대상·구독을 남기지 않음
            stage.BeginEncounter(boss);
            stage.CancelBossSequence();
            Require(!stage.IsBossSequencePlaying && !stage.Cutscene.IsPlaying && spawner.EncounterTrigger.enabled
                && !player.IsCutScene && !boss.IsCutScene && input.BlockInput && Time.timeScale == 0f,
                "Standalone Stage cancellation must release the encounter and trigger while preserving the external pause.");
            stage.BeginEncounter(boss);
            Require(stage.IsBossSequencePlaying, "A standalone cancellation must allow a fresh encounter immediately.");
            stage.CancelBossSequence();
            Action cancelAtStart = stage.CancelBossSequence;
            stage.Cutscene.OnStarted += cancelAtStart; // 시작 통지 안에서 중단해도 이전 코루틴이 전투를 열지 않는지 확인
            try
            {
                stage.BeginEncounter(boss);
                yield return Frames(2); // 중단된 실행의 뒤늦은 완료·저장 가능 경계 관찰
                Require(!stage.IsBossSequencePlaying && !stage.Cutscene.IsPlaying && spawner.EncounterTrigger.enabled
                    && !boss.IsInCombat && !player.IsCutScene && !boss.IsCutScene && !manager.IsBossCutscenePaused
                    && input.BlockInput && Time.timeScale == 0f
                    && SaveManager.Current.Get<List<string>>("EncounteredBosses").Count == 0,
                    "Cancellation from OnStarted must not leave presentation ownership or resume a stale encounter.");
            }
            finally { stage.Cutscene.OnStarted -= cancelAtStart; } // 즉시 중단 검사가 다음 조우에 남지 않도록 해제
            passed("F-B02 standalone Stage cancellation and synchronous OnStarted cancellation restore trigger, reject stale completion and allow reentry without losing external pause");

            // 정상 재조우는 앞선 중단과 무관하게 저장된 분기에서 진행
            manager.SetGameplayPaused(false);
            input.BlockInput = false;
            SaveManager.Current.Get<List<string>>("EncounteredBosses").Add(boss.BossId);
            stage.BeginEncounter(boss);
            Require(stage.Cutscene.CurrentKind == CutsceneKind.BossReencounter, "Recorded encounter must choose the retry presentation.");
            yield return Until(() => !stage.IsBossSequencePlaying, "reencounter"); // 재조우가 새 첫 조우 기록 없이 전투를 여는지 확인
            Require(SaveManager.Current.Get<List<string>>("EncounteredBosses").Count == 1, "Reencounter must not duplicate the first encounter record.");
            passed("F-B02 cancelled first encounter restores only its pause and input ownership; retry uses reencounter and preserves one encounter record");

            // 실제 이동 몸체는 Boss 진입을 막고 접촉만으로 체력이 줄지 않음
            PlayerBodyController movement = player.GetComponent<PlayerBodyController>();
            Vector2 direction = Vector2.left;
            Vector2 origin = (Vector2)boss.transform.position + direction * 2f;
            body.position = origin;
            player.transform.position = origin;
            Physics2D.SyncTransforms();
            float beforeContact = player.HP;
            float nextFixed = Time.fixedTime + Time.fixedDeltaTime;
            movement.Move(Vector2.right * 3f);
            yield return Until(() => Time.fixedTime >= nextFixed - 0.00001f, "Boss body collision"); // 실제 물리 시간이 진행된 뒤 몸체 충돌 결과 확인
            Require(body.position.x < boss.transform.position.x - 0.4f && player.HP == beforeContact,
                "Player movement must stop outside the Boss body without contact damage.");
            passed("F-B06 actual Player body movement is blocked by Boss collision without contact damage");

            // 처치 시 소유 위험물 정리·즉시 기록 뒤 사망 연출 완료에서 통로와 사체 전환
            Projectile prefab = AssetDatabase.LoadAssetAtPath<Projectile>("Assets/Prefabs/BossTests/Area.prefab");
            Projectile hazard = UnityEngine.Object.Instantiate(prefab, boss.transform.position + Vector3.up * 3f, Quaternion.identity);
            hazard.Initialize(boss, Vector2.right, 0f, 5f, 1f);
            int deaths = 0;
            Action died = () => deaths++;
            boss.OnDie += died; // 같은 Boss 처치가 한 번만 발생하는지 수집
            try
            {
                Require(boss.ApplyDamage(Damage(player, boss.HP + 1f)), "Boss must accept the real killing attack.");
                Require(!boss.ApplyDamage(Damage(player, 1f)) && deaths == 1 && boss.IsDead && !boss.IsInCombat
                    && !player.IsInCombat && !ui.IsBossHudVisible && !spawner.EncounterTrigger.enabled
                    && (hazard == null || !hazard.IsActive) && SaveManager.Current.Get<List<string>>("DefeatedBosses").Count == 1,
                    "Death must stop combat, clear owned hazards, record once and reject repeated lethal hits.");
                yield return Until(() => !stage.IsBossSequencePlaying, "Boss death"); // 사체 전환·입구와 출구 개방 요청까지 대기
                Require((entryMotion != null ? entryMotion.WantsOpen : !entryDoor.activeSelf)
                    && (exitMotion != null ? exitMotion.WantsOpen : !exitDoor.activeSelf)
                    && !boss.GetComponent<Collider2D>().enabled && !player.IsCutScene && !manager.IsBossCutscenePaused,
                    "Death completion must show a noncolliding corpse and request both doors while releasing presentation.");
                yield return Until(() => (entryMotion != null ? entryMotion.IsOpen && !entryMotion.Blocker.enabled : !entryDoor.activeSelf)
                    && (exitMotion != null ? exitMotion.IsOpen && !exitMotion.Blocker.enabled : !exitDoor.activeSelf),
                    "both Boss doors fully open"); // 각 개방 모션의 최종 open 상태에서만 통과 차단이 풀리는지 확인
            }
            finally { boss.OnDie -= died; } // 처치 횟수 수집 종료
            stage.ResetEncounter();
            Require(boss.IsDead && boss.HP == 0f && !spawner.EncounterTrigger.enabled && !boss.GetComponent<Collider2D>().enabled,
                "Retry must preserve the defeated Boss instead of reviving it.");
            passed("F-B05 lethal damage records once, clears owned hazards and combat subscriptions, waits for death presentation and opens both doors; F-B01 retry preserves corpse");

            // 생존 재도전은 같은 객체의 체력·상태·위치·선택 이력을 복원
            SaveManager.Current.Get<List<string>>("DefeatedBosses").Clear();
            stage.ResetEncounter();
            boss.transform.position += Vector3.up;
            boss.AD = 1f;
            boss.EnterGroggy();
            stage.ResetEncounter();
            Require(boss == manager.CurrentBoss && !boss.IsDead && boss.HP == boss.maxHP && boss.AD == boss.basicAD
                && !boss.IsGroggy && !boss.IsInCombat && boss.Target == null && boss.CurrentPattern == null
                && boss.PatternSelector.LastPatternId == null && boss.transform.position == boss.BasePosition
                && spawner.EncounterTrigger.enabled && boss.GetComponent<Collider2D>().enabled,
                "Living retry must reuse the same Boss and restore baseline values, spawn, trigger and pattern history.");
            passed("F-B01 living retry reuses the same Boss and restores health, base values, position, colliders, trigger and pattern history");

            // 실제 Slash 피해 중 Boss 사망 연출로 넘어가도 정상 HUD·카메라를 복원
            stage.BeginEncounter(boss);
            yield return Until(() => !stage.IsBossSequencePlaying, "encounter before Slash kill"); // 실제 전투 HUD를 준비한 뒤 Slash로 처치
            player.ResetMotion();
            player.Skills.ResetSkills();
            player.Skills.SlashDuration = 0.08f;
            player.Skills.ConfirmSlashCooldown = 0f;
            player.Skills.SlashTargetMask = ~0;
            input.BlockInput = false;
            ui.SetPlayerHudVisible(true);
            camera.Follow(player.transform, true);
            float normalZoom = camera.Camera.orthographicSize;
            boss.HP = 1f;
            player.Skills.SetLine(new Line((Vector2)boss.transform.position + Vector2.left * 2f, Vector2.right, 4f, 2f));
            Require(player.ApplySlash() && player.IsSlashing && ui.IsSlashMode && !ui.IsPlayerHudVisible,
                "A real Slash must own its hidden HUD and camera before the killing hit.");
            yield return Until(() => boss.IsDead && !stage.IsBossSequencePlaying && !player.IsSlashing, "Slash kill presentation handoff"); // Slash 판정의 사망 사건부터 Boss 연출 복구까지 확인
            Require(ui.IsPlayerHudVisible && !ui.IsBossHudVisible && !ui.IsSlashMode && !camera.IsSlashMode
                && camera.Target == player.transform && Mathf.Abs(camera.Camera.orthographicSize - normalZoom) < 0.001f
                && !player.IsCutScene && !input.BlockInput && !manager.IsSlashPaused && !manager.IsBossCutscenePaused
                && Time.timeScale == 1f && SaveManager.Current.Get<List<string>>("DefeatedBosses").Count == 1,
                "Slash-to-death handoff must restore the normal Player HUD, camera, input and independent time owners.");
            passed("F-B05 real Slash killing hit hands presentation to Boss death and restores the normal Player HUD, camera, input and independent pauses");
        }
        finally
        {
            // 검사 중단에서도 저장·연출·입력·현재 세션을 정상 상태로 복구
            stage.Cutscene.OnStarted -= started; // 조우 시작 횟수 수집 종료
            stage.CancelBossSequence();
            SaveManager.Current.Set("EncounteredBosses", encountered);
            SaveManager.Current.Set("DefeatedBosses", defeated);
            SaveManager.Current.Set("PlayerHP", savedHP);
            stage.ResetEncounter();
            stage.FirstEncounterDuration = first;
            stage.ReencounterDuration = repeat;
            stage.BossDeathDuration = death;
            player.Skills.ResetSkills();
            player.Skills.SlashDuration = slashDuration;
            player.Skills.ConfirmSlashCooldown = slashCooldown;
            player.Skills.SlashTargetMask = slashMask;
            manager.SetGameplayPaused(false);
            player.HP = hp;
            player.Spawner.playerSpawn();
            input.enabled = inputEnabled;
            input.BlockInput = inputBlock;
            SaveManager.Save();
        }
    }

    /// <summary>실제 피해 경로로 전달할 고유 공격 구성</summary>
    private static DamageInstance Damage(PlayerCharacter player, float amount) => new DamageInstance(player, amount, DamageType.Slash, DamageInstance.NextAttackId());

    /// <summary>게임 정지 중에도 완료되는 연출의 실제 시간 제한 확인</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 5d;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new InvalidOperationException("Timed out waiting for " + description);
            yield return null;
        }
    }

    /// <summary>물리·프레임 사건이 실제 진행되는 동안 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        while (Time.frameCount < end) yield return null;
    }

    /// <summary>실제 조건 불일치를 공통 검사 결과에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
