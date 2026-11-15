using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>같은 Player의 실제 사망·부활·기존 연결·저장·중단 경계 확인</summary>
public static class PlayerLifecycleChecks
{
    /// <summary>활성 수정 진행을 유지하고 실제 사망에서 같은 인스턴스 복원 확인</summary>
    public static IEnumerator Run(GameManager manager, Action<string> passed)
    {
        // 같은 세션의 원본 연결과 복원할 튜닝·진행 상태 보관
        PlayerCharacter player = manager.CurrentPlayer;
        PlayerSpawner spawner = player.Spawner;
        PlayerInputController input = PlayerInputController.Instance;
        UIController ui = UIController.Instance;
        GameCamera camera = GameCamera.Instance;
        UI_Death view = ui.GetComponent<UI_Death>();
        BossCharacter boss = manager.CurrentBoss;
        float deathDuration = spawner.DeathDuration;
        float spawnDuration = spawner.SpawnDuration;
        float savedHealth = SaveManager.Current.Get<float>("PlayerHP");
        var defeatedBefore = new List<string>(SaveManager.Current.Get<List<string>>("DefeatedBosses"));
        var encountersBefore = new List<string>(SaveManager.Current.Get<List<string>>("EncounteredBosses"));
        int deathCount = SaveManager.Current.Get<int>("DeathCount");
        int revives = 0;
        Action onRevive = () => revives++;
        player.OnRevive += onRevive; // 같은 Player의 실제 부활 통지 횟수 수집
        try
        {
            // 기존 행동·효과를 남긴 뒤 실제 치명 피해로 새 생명주기 시작
            spawner.DeathDuration = 0.06f;
            spawner.SpawnDuration = 0.04f;
            input.BlockInput = false;
            player.ResetMotion();
            player.Buffs.ApplyFever();
            player.Buffs.Apply(BuffType.Slow);
            yield return Frames(2); // 예약 효과가 실제 적용된 뒤 사망 초기화 확인
            player.Skills.SetLine(new Line(player.transform.position, Vector2.right, 3f, 0.2f));
            player.Groggy = 40f;
            boss.HP = boss.maxHP * 0.5f;
            boss.transform.position += Vector3.up;
            manager.Stage.BeginEncounter(boss);
            yield return Until(() => !manager.Stage.IsBossSequencePlaying, "encounter presentation"); // 조우가 실제 전투로 전환된 뒤 Player 사망 검사
            int completedEncounters = SaveManager.Current.Get<List<string>>("EncounteredBosses").Count;
            SpriteRenderer bossSprite = boss.GetComponentInChildren<SpriteRenderer>();
            SpriteRenderer playerSprite = player.GetComponentInChildren<SpriteRenderer>();
            Material bossMaterial = bossSprite.sharedMaterial;
            Material playerMaterial = playerSprite.sharedMaterial;
            Require(view.GrayscaleMaterial != null && view.GrayscaleMaterial.shader.isSupported,
                "Death must have its supported Sprite texture grayscale material connected.");
            Require(player.ApplyDamage(Lethal(player)), "A live Player must accept the real lethal effect used for lifecycle validation.");
            Require(player.IsDead && spawner.IsRespawning && input.BlockInput && manager.IsDeathPaused && view.IsDeath
                && view.IsVisible && camera.IsDeathMode && input.Player == player && ui.Player == player,
                "Death must preserve the Player connections while opening its own pause and presentation.");
            Require(bossSprite.sharedMaterial == view.GrayscaleMaterial && playerSprite.sharedMaterial == playerMaterial,
                "Death must desaturate world Sprite textures while preserving the Player material.");
            Require(SaveManager.Current.Get<int>("DeathCount") == deathCount + 1 && SaveManager.Current.Get<float>("PlayerHP") == savedHealth
                && !player.ApplyDamage(Lethal(player)), "One real death must save one count without overwriting checkpoint HP or repeating the death event.");
            manager.SetGameplayPaused(true);
            yield return Until(() => !spawner.IsRespawning && !spawner.IsSpawning, "automatic checkpoint revival"); // 실제 시간의 사망·기상 표현이 끝날 때까지 대기
            Require(!player.IsDead && player.HP == player.maxHP && revives == 1 && player == manager.CurrentPlayer
                && input.Player == player && ui.Player == player && camera.Target == player.transform
                && !manager.IsDeathPaused && manager.IsGameplayPaused && Time.timeScale == 0f && !input.BlockInput
                && !view.IsVisible && !ui.IsDeathMode && !camera.IsDeathMode,
                "Automatic revival must keep the same objects, release only its own pause and preserve external pause.");
            Require(bossSprite.sharedMaterial == bossMaterial && playerSprite.sharedMaterial == playerMaterial,
                "Revival must restore each Sprite's original material.");
            passed("F-P09 real death saves one count and revives the same Player and service connections through independent unscaled death and spawn gates");

            // 새 수정의 로컬 위치·기본 전투 수치와 지속 기록을 구분
            Require(Vector2.Distance(player.transform.position, spawner.SpawnPoint.position) < 0.001f
                && player.AD == player.basicAD && player.AS == player.basicAS && player.SP == player.basicSP
                && !player.Buffs.IsFever && !player.Buffs.IsSlow && !player.Skills.HasLine && player.Groggy == 0f
                && !player.IsHitProtected && player.DashCooldownRemaining == 0f && player.Skills.DrawCooldownRemaining == 0f,
                "Revival must use the cached active checkpoint and clear actions, effects, Line, protection and cooldowns.");
            Require(boss.HP == boss.maxHP && !boss.IsDead && !boss.IsInCombat && !boss.IsGroggy
                && Vector3.Distance(boss.transform.position, boss.BasePosition) < 0.001f
                && SaveManager.Current.Get<float>("PlayerHP") == savedHealth
                && SaveManager.Current.Get<List<string>>("EncounteredBosses").Count == completedEncounters,
                "A live Boss must reset without changing the checkpoint HP or first-encounter history.");
            manager.SetGameplayPaused(false);
            passed("F-P09 revival restores cached checkpoint position and base combat state while live Boss resets and checkpoint and encounter records persist");

            // 처치 상대 보존과 세션 이탈 같은 중단에서 지연 부활·표시 잔류 방지
            Require(boss.ApplyDamage(new DamageInstance(player, boss.HP + 1f, DamageType.Slash, DamageInstance.NextAttackId())), "The current Boss must receive a real lethal hit.");
            Require(SaveManager.Current.Get<List<string>>("DefeatedBosses").Contains(boss.BossId), "A defeated Boss must be recorded before a later Player revival.");
            yield return Until(() => !manager.Stage.IsBossSequencePlaying, "Boss death presentation"); // 처치 연출이 전면 행동 제한을 해제한 뒤 다음 사망 검사
            Require(player.ApplyDamage(Lethal(player)), "The next real death must start after the previous revival.");
            yield return Until(() => !spawner.IsRespawning, "revival with defeated Boss"); // 사망 복구가 처치 Boss를 되살리지 않는지 확인
            Require(boss.IsDead && boss.HP == 0f && !boss.GetComponent<Collider2D>().enabled, "Revival must preserve the defeated Boss and its disabled body.");
            Require(player.ApplyDamage(Lethal(player)), "The cancellation case must begin with a real death.");
            spawner.enabled = false;
            Require(!spawner.IsRespawning && !spawner.IsSpawning && !manager.IsDeathPaused && !view.IsVisible && !camera.IsDeathMode,
                "Disabling a lifecycle owner must cancel delayed work and restore only its presentation.");
            yield return Frames(3); // 중단 뒤 지연 부활이 다시 실행되지 않는지 확인
            Require(player.IsDead, "A cancelled lifecycle must not revive the Player later.");
            spawner.enabled = true;
            player.Revive();
            Action cancelFromRevive = () => spawner.CancelLifecycle();
            player.OnRevive += cancelFromRevive; // 부활 콜백에서 취소된 실행이 표시를 다시 열지 않는지 확인
            try
            {
                Require(player.ApplyDamage(Lethal(player)), "The reentrant cancellation case must begin with a real death.");
                yield return Until(() => !player.IsDead && !spawner.IsRespawning, "cancellation from revival callback"); // 부활 알림 안에서 취소되는 실행 대기
                yield return Frames(2); // 이전 실행이 뒤늦게 표시를 다시 여는지 관찰
                Require(!view.IsVisible && !camera.IsDeathMode && !manager.IsDeathPaused, "A revival callback cancellation must not reopen old presentation.");
            }
            finally { player.OnRevive -= cancelFromRevive; } // 부활 재진입 수집 종료

            // 허용된 구 저장 HP0은 새 사망으로 세지 않고 최초 기상 안에서 복구
            PlayerCharacter zeroHealthPlayer = null;
            int beforeLoadCount = SaveManager.Current.Get<int>("DeathCount");
            try
            {
                SaveManager.Current.Set("PlayerHP", 0f);
                zeroHealthPlayer = UnityEngine.Object.Instantiate(manager.Stage.PlayerPrefab);
                SaveManager.Current.Set("PlayerHP", savedHealth);
                Require(zeroHealthPlayer.IsDead, "A zero-HP save must first restore its actual saved health.");
                zeroHealthPlayer.Spawner.Bind();
                yield return Frames(2); // 새 Player의 자기 Start와 서비스 캐시 준비 대기
                zeroHealthPlayer.Spawner.SpawnDuration = 0.02f;
                yield return zeroHealthPlayer.Spawner.PlayInitialSpawn(); // 최초 게이트가 저장된 사망 상태를 복구할 때까지 대기
                Require(!zeroHealthPlayer.IsDead && zeroHealthPlayer.HP == zeroHealthPlayer.maxHP
                    && SaveManager.Current.Get<int>("DeathCount") == beforeLoadCount,
                    "Loading zero HP must recover inside the spawn gate without counting another death.");
            }
            finally
            {
                SaveManager.Current.Set("PlayerHP", savedHealth);
                if (zeroHealthPlayer != null) UnityEngine.Object.Destroy(zeroHealthPlayer.gameObject);
                spawner.Bind();
                input.BlockInput = false;
            }
            passed("F-P09 defeated Boss state survives revival and lifecycle cancellation leaves no delayed revival, pause or death presentation");
        }
        finally
        {
            // 검사 실패에서도 같은 세션을 다음 저장·재진입 검사에 복귀
            spawner.CancelLifecycle();
            spawner.enabled = true;
            player.OnRevive -= onRevive; // 부활 통지 수집 종료
            spawner.DeathDuration = deathDuration;
            spawner.SpawnDuration = spawnDuration;
            manager.SetGameplayPaused(false);
            if (player.IsDead) player.Revive();
            SaveManager.Current.Set("DefeatedBosses", defeatedBefore);
            SaveManager.Current.Set("EncounteredBosses", encountersBefore);
            SaveManager.Current.Set("PlayerHP", savedHealth);
            manager.Stage.ResetEncounter();
            spawner.playerSpawn();
            input.BlockInput = false;
            SaveManager.Save();
        }
    }

    /// <summary>현재 Player의 전면 보호가 없는 실제 효과 치명 피해 구성</summary>
    private static DamageInstance Lethal(PlayerCharacter player) => new DamageInstance(null, player.HP + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false);

    /// <summary>생명주기 조건의 실제 시간 제한을 두고 다음 Editor 갱신에 양보</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double end = Time.realtimeSinceStartupAsDouble + 4d;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new InvalidOperationException("Timed out waiting for " + description);
            yield return null;
        }
    }

    /// <summary>예약·렌더 상태를 관찰할 프레임 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        while (Time.frameCount < end) yield return null;
    }

    /// <summary>통합 실행기의 실패 결과로 전달할 계약 검사</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
