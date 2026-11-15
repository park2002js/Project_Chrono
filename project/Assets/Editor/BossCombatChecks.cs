using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>더미 보스의 실제 패턴·탄환·Slash·그로기와 공통 추첨 계약을 Play Mode에서 확인</summary>
public static class BossCombatChecks
{
    private const string PrefabDirectory = "Assets/Prefabs/BossTests/"; // 실제 패턴 검증에 사용할 더미 보스·탄환 프리팹 위치

    /// <summary>현재 Player로 독립 더미 전투를 검사하고 세션 상태와 임시 대상을 복구</summary>
    public static IEnumerator Run(GameManager manager, Action<string> passed)
    {
        // 실제 세션의 수치·입력·위치·난수를 검사 전 상태로 보관
        PlayerCharacter player = manager.CurrentPlayer;
        PlayerInputController input = PlayerInputController.Instance;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 position = body.position;
        Vector2 aim = player.AimWorldPosition;
        float health = player.HP;
        float attack = player.AD;
        float attackSpeed = player.AS;
        float speed = player.SP;
        float groggy = player.Groggy;
        float protection = player.HitProtectionDuration;
        bool playerEnabled = player.enabled;
        bool inputEnabled = input.enabled;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        bool invulnerable = player.IsInvulnerable;
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        var fixtures = new List<GameObject>();
        DummyBoss boss = null;
        try
        {
            // 기존 전투 공간과 떨어진 위치에서 Player와 실제 더미 프리팹 준비
            input.enabled = false;
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            player.Skills.ResetSkills();
            player.Buffs.Clear();
            player.IsInvulnerable = true;
            player.HitProtectionDuration = 0f;
            player.HP = player.maxHP;
            SetPosition(body, new Vector2(400f, 400f));
            DummyBoss prefab = AssetDatabase.LoadAssetAtPath<DummyBoss>(PrefabDirectory + "DummyBoss.prefab");
            Require(prefab != null, "F-B01 requires the saved DummyBoss prefab.");
            boss = UnityEngine.Object.Instantiate(prefab, new Vector3(404f, 400f, 0f), Quaternion.identity);
            fixtures.Add(boss.gameObject);
            boss.InitializeHealth(boss.maxHP);
            boss.BeginEncounter(player);
            boss.CancelPattern();
            Require(boss is BossCharacter && boss is Character && boss.GetComponent<BossSpawner>() != null
                && boss.Buffs != null && boss.PatternSelector != null && boss.PatternSelector.Patterns.Count == 4,
                "DummyBoss must inherit Character/BossCharacter and prepare four reusable patterns and its own components in Awake.");
            Require(boss.ProjectilePrefabs.Length == 4 && boss.ProjectilePrefabs[0] is NormalProjectile
                && boss.ProjectilePrefabs[1] is GoldProjectile && boss.ProjectilePrefabs[2] is LaserProjectile
                && boss.ProjectilePrefabs[3] is AreaProjectile,
                "The real DummyBoss prefab must connect normal, gold, laser and area projectile subclasses in order.");
            passed("F-B01/F-B06 saved DummyBoss inherits Character and BossCharacter and connects all four real projectile subclasses and Awake-owned pattern objects");

            // 조건·가중·직전 제외는 재사용 C# 패턴을 통해 독립적으로 검증
            CheckSelection(boss, player, passed);
            yield return CheckPatterns(manager, boss, player, fixtures, passed); // 실제 Update의 패턴 발사·완료·그로기 중단 확인
            yield return CheckProjectiles(manager, boss, player, body, fixtures, passed); // 실제 탄환 이동·피격·Slash·반사와 지속 위험물 확인
            yield return CheckBody(boss, player, body, passed); // 더미의 실제 몸체가 이동을 막고 접촉 피해가 없는지 확인
        }
        finally
        {
            // 실패한 검사에서도 더미 소유 위험물과 생성물을 먼저 닫고 Player 복원
            if (boss != null)
            {
                boss.EndEncounter();
                ClearProjectiles(boss);
            }
            foreach (GameObject fixture in fixtures)
                if (fixture != null)
                {
                    fixture.SetActive(false);
                    UnityEngine.Object.Destroy(fixture);
                }
            player.enabled = playerEnabled;
            player.Buffs.Clear();
            player.ResetMotion();
            player.Skills.ResetSkills();
            player.HP = health;
            player.AD = attack;
            player.AS = attackSpeed;
            player.SP = speed;
            player.Groggy = groggy;
            player.HitProtectionDuration = protection;
            player.IsInvulnerable = invulnerable;
            player.SetAimPosition(aim);
            SetPosition(body, position);
            UnityEngine.Random.state = randomState;
            manager.SetGameplayPaused(paused);
            input.enabled = inputEnabled;
            input.BlockInput = blocked;
        }
    }

    /// <summary>기본 추첨 분포·실행 조건·우선순위·직전 제외와 잘못된 등록 처리 확인</summary>
    private static void CheckSelection(DummyBoss boss, PlayerCharacter player, Action<string> passed)
    {
        // 고정 난수에서 이론 가중치와 실제 선택 빈도를 비교하고 조회가 실행 이력을 바꾸지 않는지 확인
        var selector = new BossPatternSelector();
        var light = new ProbePattern("weight_one") { Weight = 1f };
        var heavy = new ProbePattern("weight_three") { Weight = 3f };
        selector.SetPatterns(light, heavy);
        UnityEngine.Random.InitState(614032);
        int heavyCount = 0;
        for (int i = 0; i < 12000; i++)
            if (selector.GetNextPattern(boss) == heavy) heavyCount++;
        Require(heavyCount > 8700 && heavyCount < 9300 && selector.LastPatternId == null
            && light.UseCount == 0 && heavy.UseCount == 0,
            "A 1:3 weighted draw must remain near 75 percent over 12000 seeded samples without recording unstarted candidates.");
        passed("F-B03 12000 seeded weighted draws match the 1:3 probability range while candidate lookup leaves actual-start history unchanged");

        // HP·거리·다른 패턴 횟수와 쿨타임을 모두 통과한 후보만 선택
        var gated = new ProbePattern("gated")
        {
            Condition = owner => owner.HP <= owner.maxHP * 0.5f
                && Vector2.Distance(owner.transform.position, player.transform.position) <= 6f && light.UseCount > 0
        };
        selector.SetPatterns(gated);
        float health = boss.HP;
        boss.HP = boss.maxHP;
        Require(selector.GetNextPattern(boss) == null, "HP and prerequisite-use conditions must reject an otherwise registered candidate.");
        boss.HP = boss.maxHP * 0.5f;
        Require(selector.GetNextPattern(boss) == null, "A missing prerequisite use must keep the Boss idle after HP condition passes.");
        Require(light.Begin(boss), "A prerequisite pattern must accept its first start.");
        light.Cancel();
        Require(selector.GetNextPattern(boss) == gated, "The candidate must become available when HP, range and prerequisite-use conditions all pass.");
        Vector3 bossPosition = boss.transform.position;
        boss.transform.position += Vector3.right * 30f;
        Require(selector.GetNextPattern(boss) == null, "Moving outside the pattern's allowed range must remove the candidate.");
        boss.transform.position = bossPosition;
        gated.Cooldown = 10f;
        Require(gated.Begin(boss), "The first start must not be blocked by an unused cooldown.");
        gated.Cancel();
        Require(selector.GetNextPattern(boss) == null && gated.UseCount == 1, "The start-based cooldown must remain active even after a cancelled action.");
        selector.ResetHistory();
        Require(gated.UseCount == 0 && float.IsNegativeInfinity(gated.LastUsedTime)
            && selector.GetNextPattern(boss) == gated, "Retry reset must clear usage and cooldown history.");
        boss.HP = health;
        passed("F-B03 candidate conditions follow current HP, distance, prerequisite use and start-based cooldown while retry clears selection and usage history");

        // 상황 우선값은 가중 추첨보다 앞서며 직전 중복 제외는 우선 패턴에도 적용
        var priority = new ProbePattern("priority") { Weight = 1f, Priority = 10 };
        var ordinary = new ProbePattern("ordinary") { Weight = 10000f };
        selector.SetPatterns(ordinary, priority);
        for (int i = 0; i < 100; i++) Require(selector.GetNextPattern(boss) == priority, "The highest eligible priority must override ordinary weight.");
        Require(priority.Begin(boss), "The priority candidate must accept its start.");
        selector.HandlePatternStarted(priority);
        priority.Cancel();
        Require(selector.GetNextPattern(boss) == ordinary, "The previous pattern must remain excluded even with a higher priority.");
        selector.SetPatterns(light, heavy);
        string previous = null;
        for (int i = 0; i < 1000; i++)
        {
            BossPattern selected = selector.GetNextPattern(boss);
            Require(selected != null && selected.PatternId != previous && selected.Begin(boss), "Consecutive actual starts must never choose the same pattern.");
            selector.HandlePatternStarted(selected);
            previous = selected.PatternId;
            selected.Cancel();
        }
        selector.SetPatterns(priority);
        Require(priority.Begin(boss), "Single-candidate check requires one actual start.");
        selector.HandlePatternStarted(priority);
        priority.Cancel();
        Require(selector.GetNextPattern(boss) == null, "A single previous candidate must leave Idle rather than bypassing the no-repeat rule.");
        passed("F-B03 contextual priority overrides weights, 1000 actual starts never repeat consecutively and an excluded sole candidate leaves Idle");

        // 유효하지 않은 가중치는 제외하고 실패한 등록은 기존 선택 목록을 보존
        var zero = new ProbePattern("zero") { Weight = 0f };
        var negative = new ProbePattern("negative") { Weight = -1f };
        var nan = new ProbePattern("nan") { Weight = float.NaN };
        var infinity = new ProbePattern("infinity") { Weight = float.PositiveInfinity };
        selector.SetPatterns(zero, negative, nan, infinity);
        Require(selector.GetNextPattern(boss) == null, "Zero, negative and non-finite weights must not produce a candidate.");
        ExpectArgument(() => selector.SetPatterns(new ProbePattern("same"), new ProbePattern("same")));
        Require(selector.Patterns.Count == 4 && selector.Patterns[0] == zero, "Rejected duplicate IDs must not replace the registered list.");
        ExpectArgument(() => selector.SetPatterns(zero, null));
        ExpectArgument(() => selector.HandlePatternStarted(new ProbePattern("unregistered")));
        passed("F-B03 invalid weights produce no candidate and duplicate IDs, null patterns and unregistered history are rejected without partial replacement");
    }

    /// <summary>더미 네 패턴의 실제 발사와 페이즈·그로기·정지·사망의 실행 경계 확인</summary>
    private static IEnumerator CheckPatterns(GameManager manager, DummyBoss boss, PlayerCharacter player, List<GameObject> fixtures, Action<string> passed)
    {
        // 실제 더미가 만든 패턴 객체와 프리팹을 그대로 사용하고 검사 시간만 짧게 조정
        var patterns = new BossPattern[boss.PatternSelector.Patterns.Count];
        for (int i = 0; i < patterns.Length; i++) patterns[i] = boss.PatternSelector.Patterns[i];
        boss.Warmup = 0.08f;
        boss.ShotInterval = 0.08f;
        boss.Recovery = 0.08f;
        boss.ShotCount = 3;
        boss.BulletSpeed = 0f;
        boss.BulletLifetime = 5f;
        boss.ProjectileDamage = 1f;
        boss.AreaRange = 20f;
        boss.HP = boss.maxHP;
        Require(boss.Phase == 1 && !patterns[2].CanRun(boss), "The dummy laser must be gated until the configured second phase.");
        boss.HP = boss.maxHP * 0.5f;
        Require(boss.Phase == 2 && patterns[2].CanRun(boss), "The dummy HP threshold must enable phase-two laser eligibility.");

        // 한 패턴씩 등록해 무작위 운과 무관하게 모든 구체 탄환의 생성·정상 완료 확인
        Type[] expected = { typeof(NormalProjectile), typeof(GoldProjectile), typeof(LaserProjectile), typeof(AreaProjectile) };
        for (int i = 0; i < patterns.Length; i++)
        {
            boss.CancelPattern();
            boss.PatternSelector.SetPatterns(patterns[i]);
            if (i == 3)
            {
                patterns[1].ResetHistory();
                Require(patterns[1].Begin(boss), "The area pattern's gold-use prerequisite must accept a real earlier start.");
                patterns[1].Cancel();
            }
            int completed = 0;
            Action<BossPattern> onCompleted = value => completed++;
            patterns[i].OnCompleted += onCompleted; // 실제 패턴의 정상 완료가 한 번만 오는지 수집
            try
            {
                Require(boss.ApplyPattern(patterns[i]), "Each eligible dummy pattern must start through BossCharacter.ApplyPattern.");
                yield return Until(() => boss.CurrentPattern == null, "dummy pattern " + patterns[i].PatternId); // 예고·연속 발사·후딜을 거쳐 Idle로 돌아올 때까지 대기
                Projectile[] emitted = OwnedProjectiles(boss);
                Require(completed == 1 && patterns[i].UseCount == 1 && emitted.Length == boss.ShotCount,
                    "Each dummy pattern must emit its configured shot count and complete once without automatically repeating itself.");
                foreach (Projectile projectile in emitted)
                {
                    fixtures.Add(projectile.gameObject);
                    Require(projectile.GetType() == expected[i] && projectile.Owner == boss && projectile.IsActive,
                        "Pattern emission must instantiate the configured concrete projectile and initialize its owner and lifetime.");
                }
                ClearProjectiles(boss);
            }
            finally
            {
                patterns[i].OnCompleted -= onCompleted; // 현재 패턴의 완료 수집 해제
            }
        }
        passed("F-B03/F-B06 HP phase gating and all four real dummy patterns emit three configured normal, gold, laser or area projectiles and complete exactly once");

        // 시작 즉시 완료한 콜백이 다음 패턴을 시작해도 이전 실행이 새 상태를 덮지 않음
        var instant = new ProbePattern("instant_complete") { CompleteOnBegin = true };
        var chained = new ProbePattern("chained_start");
        var rejected = new ProbePattern("during_end");
        boss.PatternSelector.SetPatterns(instant, chained, rejected);
        bool chainedStarted = false;
        bool startedDuringEnd = false;
        Action<BossPattern> startNext = value => chainedStarted = boss.ApplyPattern(chained);
        instant.OnCompleted += startNext; // 즉시 완료 콜백 안에서 다음 실행을 요청하는 재진입 경계 준비
        try
        {
            Require(boss.ApplyPattern(instant) && chainedStarted && boss.CurrentPattern == chained
                && boss.PatternSelector.LastPatternId == chained.PatternId && chained.IsRunning,
                "An immediate completion callback must preserve the next pattern's current state and actual-start history.");
            chained.CancelAction = () => startedDuringEnd = boss.ApplyPattern(rejected);
            boss.EndEncounter();
            Require(!startedDuringEnd && boss.CurrentPattern == null && boss.Target == null && !chained.IsRunning,
                "Encounter teardown must reject a new action requested from a cancellation callback.");
        }
        finally
        {
            instant.OnCompleted -= startNext; // 재진입 실행 콜백 수집 해제
        }
        boss.BeginEncounter(player);
        passed("F-B03 immediate-completion callbacks preserve a chained pattern and its history while encounter teardown rejects cancellation-callback restarts");

        // 실행 중 게임 정지는 패턴 시간과 추가 발사를 보존하고 컷씬은 즉시 취소
        var longPattern = new ProbePattern("long_running") { FinishAfter = 10f };
        var nextPattern = new ProbePattern("after_groggy") { FinishAfter = 0.03f };
        boss.PatternSelector.SetPatterns(longPattern, nextPattern);
        Require(boss.ApplyPattern(longPattern), "A running-pattern check requires a valid long action.");
        yield return Frames(2); // 실제 보스 Update가 패턴 시간을 진행한 뒤 정지 비교 시작
        manager.SetGameplayPaused(true);
        yield return Frames(1); // 정지 진입 프레임 이후의 패턴 경과 값 보관
        float frozen = longPattern.Elapsed;
        yield return RealSeconds(0.08f); // 게임 정지 중에도 실제 프레임이 흐르는 경계 확인
        Require(longPattern.Elapsed == frozen && boss.CurrentPattern == longPattern, "Game pause must preserve the current pattern and its elapsed game time.");
        manager.SetGameplayPaused(false);
        boss.IsCutScene = true;
        Require(boss.CurrentPattern == null && !longPattern.IsRunning && longPattern.Cancels == 1,
            "Entering a cutscene must synchronously cancel the active pattern once.");
        boss.IsCutScene = false;
        boss.PatternSelector.ResetHistory();
        Require(boss.ApplyPattern(longPattern), "The cancelled pattern must become available after retry history reset.");

        // 그로기는 현재 동작만 끊고 회복 뒤 직전과 다른 후보를 실행
        boss.GroggyDuration = 0.2f;
        boss.EnterGroggy();
        Require(boss.IsGroggy && boss.CurrentPattern == null && !longPattern.IsRunning, "Groggy must immediately cancel the running pattern.");
        yield return Until(() => !boss.IsGroggy && nextPattern.UseCount == 1, "groggy recovery and next pattern"); // 그로기 만료 뒤 Idle 추첨에서 다른 패턴이 시작될 때까지 대기
        boss.EndEncounter();
        Require(boss.CurrentPattern == null && boss.Target == null && !boss.IsInCombat, "Encounter end must release the current pattern and its cached Player target.");
        boss.ResetCombatState();
        Require(boss.PatternSelector.LastPatternId == null && longPattern.UseCount == 0 && nextPattern.UseCount == 0,
            "Retry must reset actual-start, cooldown and use-count history for every registered pattern.");
        passed("F-B03/F-B04 game pause freezes pattern time, cutscene and groggy cancel immediately, recovery selects a different pattern and retry clears history and target");

        // 비전투 피해 허용을 유지하면서 실제 사망은 진행 중 패턴과 대상 연결을 종료
        boss.InitializeHealth(boss.maxHP);
        Require(boss.ApplyDamage(new DamageInstance(player, 1f, DamageType.Normal, DamageInstance.NextAttackId())),
            "A live non-combat Boss must still accept valid damage.");
        boss.BeginEncounter(player);
        Require(boss.ApplyPattern(longPattern), "The death check must begin an actual pattern.");
        int deaths = 0;
        Action onDie = () => deaths++;
        boss.OnDie += onDie; // 치명 피해의 사망 사건 중복 여부 수집
        try
        {
            Require(boss.ApplyDamage(new DamageInstance(player, boss.HP + 1f, DamageType.Slash, DamageInstance.NextAttackId())), "The live Boss must accept lethal Slash damage.");
            Require(boss.IsDead && boss.CurrentPattern == null && !longPattern.IsRunning && boss.Target == null && deaths == 1,
                "Actual Boss death must cancel the current pattern, release its target and notify once.");
            Require(!boss.ApplyDamage(new DamageInstance(player, 1f, DamageType.Normal, DamageInstance.NextAttackId())) && deaths == 1,
                "Further damage to the same dead Boss must not publish another death.");
        }
        finally
        {
            boss.OnDie -= onDie; // 임시 더미의 사망 수집 해제
        }
        boss.ResetCombatState();
        boss.InitializeHealth(boss.maxHP);
        boss.PatternSelector.SetPatterns();
        passed("F-B02/F-B05 non-combat Boss damage remains valid and one lethal hit cancels pattern and target exactly once without repeated death events");
    }

    /// <summary>실제 탄환 프리팹의 피해·Slash·반사·그로기·레이저·장판 수명 확인</summary>
    private static IEnumerator CheckProjectiles(GameManager manager, DummyBoss boss, PlayerCharacter player, Rigidbody2D body, List<GameObject> fixtures, Action<string> passed)
    {
        // Player를 일반 탄 경로에 두고 빠른 이동과 중복 Collider 피해를 함께 확인
        boss.EndEncounter();
        player.IsInvulnerable = false;
        player.HP = player.maxHP;
        player.Buffs.Clear();
        SetPosition(body, new Vector2(420f, 400f));
        NormalProjectile normal = Spawn<NormalProjectile>("Normal", boss, body.position + Vector2.left * 3f, Vector2.right, 100f, 5f, 7f, fixtures);
        yield return Until(() => normal == null || !normal.IsActive, "fast normal projectile hit"); // 한 프레임 사이의 swept 판정으로 Player 적중·탄 종료 대기
        Require(Close(player.HP, player.maxHP - 7f), "A fast normal projectile must hit the Player once instead of tunnelling or duplicating damage.");
        passed("F-B06 the real normal projectile prefab uses swept collision to damage Player once at high speed and then closes its active state");

        // 전조가 있는 원형·직선 위험물은 같은 Player에 수명 동안 한 번만 피해
        foreach (string name in new[] { "Area", "Laser" })
        {
            player.HP = player.maxHP;
            AreaProjectile hazard = Spawn<AreaProjectile>(name, boss, body.position + (name == "Laser" ? Vector2.left * 2f : Vector2.zero), Vector2.right, 0f, 5f, 6f, fixtures);
            hazard.TelegraphDuration = 0.3f;
            Require(!hazard.IsArmed && Close(player.HP, player.maxHP), "A new area or laser must not deal damage before its telegraph finishes.");
            yield return Until(() => hazard.IsArmed, name + " arms"); // 전조 시간 완료 뒤 실제 피해 구간이 열릴 때까지 대기
            yield return Frames(2); // 활성 범위의 Player 피격을 실제 Update가 처리하도록 대기
            Require(Close(player.HP, player.maxHP - 6f), "An armed area or laser must damage an overlapping Player once.");
            yield return GameSeconds(0.15f); // 같은 범위에 남아도 동일 공격 피해가 반복되지 않는지 관찰
            Require(Close(player.HP, player.maxHP - 6f), "Remaining inside a persistent hazard must not repeat the same attack damage.");
            float beforeGauge = player.Groggy;
            var slash = new CollisionBox();
            slash.Begin(new DamageInstance(player, 1f, DamageType.Slash, DamageInstance.NextAttackId()), ~0);
            slash.QuerySlash(hazard.transform.position, Vector2.one * 12f, 0f, player, boss);
            Require(hazard.IsActive && !hazard.IsReflected && player.Groggy == beforeGauge,
                "Laser and area hazards must ignore Slash without granting groggy or reflection rewards.");
            slash.End();
            hazard.Despawn();
        }
        passed("F-B06 real laser and area prefabs telegraph before one damage per Player, remain active and ignore Slash without gauge rewards");

        // 같은 Slash에서 일반·금색을 한 번 처리하고 현재 Boss 대신 실제 발사자에게 반사
        player.IsInvulnerable = true;
        player.Groggy = 0f;
        player.Buffs.Clear();
        yield return Frames(1); // 같은 프레임 효과 제거 우선 경계를 지난 뒤 금색 보상 요청
        Vector2 slashCenter = new Vector2(430f, 400f);
        boss.transform.position = new Vector3(436f, 400f, 0f);
        Rigidbody2D bossBody = boss.GetComponent<Rigidbody2D>();
        if (bossBody != null) bossBody.position = boss.transform.position;
        NormalProjectile removed = Spawn<NormalProjectile>("Normal", boss, slashCenter + Vector2.left, Vector2.up, 0f, 5f, 4f, fixtures);
        GoldProjectile gold = Spawn<GoldProjectile>("Gold", boss, slashCenter + Vector2.right, Vector2.up, 0f, 5f, 4f, fixtures);
        AreaProjectile retained = Spawn<AreaProjectile>("Area", boss, slashCenter + Vector2.up * 4f, Vector2.right, 0f, 5f, 1f, fixtures);
        removed.GroggyGain = 60f;
        gold.GroggyGain = 40f;
        gold.ReflectionSpeed = 30f;
        AddDuplicateHitbox(removed);
        AddDuplicateHitbox(gold);
        var pattern = new ProbePattern("slash_cancel") { FinishAfter = 5f };
        boss.PatternSelector.SetPatterns(pattern);
        boss.BeginEncounter(player);
        boss.GroggyDuration = 0.8f;
        Require(boss.ApplyPattern(pattern), "The gauge test must interrupt an actual running pattern.");
        float bossHealth = boss.HP;
        int groggyEvents = 0;
        Action onGroggy = () => groggyEvents++;
        boss.OnGroggy += onGroggy; // 같은 Slash의 게이지 상한 도달이 그로기를 한 번만 여는지 수집
        try
        {
            var slash = new CollisionBox();
            slash.Begin(new DamageInstance(player, 1f, DamageType.Slash, DamageInstance.NextAttackId()), ~0);
            Physics2D.SyncTransforms();
            Require(slash.QuerySlash(slashCenter, new Vector2(4f, 1f), 0f, player, manager.CurrentBoss) == 2,
                "A single real Slash query must apply one reaction to each projectile despite duplicate Collider children.");
            Require(!removed.IsActive && gold.IsReflected && gold.ReflectionTarget == boss && retained.IsActive
                && player.Groggy == 0f && boss.IsGroggy && boss.CurrentPattern == null && !pattern.IsRunning && groggyEvents == 1,
                "At 100 gauge, Slash must immediately consume it, groggy the owning Boss, cancel its pattern and preserve existing field hazards.");
            Require(slash.QuerySlash(slashCenter, new Vector2(4f, 1f), 0f, player, manager.CurrentBoss) == 0 && player.Groggy == 0f,
                "Repeating one Slash query must not repeat projectile rewards.");
            slash.End();
            yield return Until(() => player.Buffs.IsFever, "gold grants Fever"); // 금색 반사의 예약 보상이 다음 프레임에 각성으로 적용될 때까지 대기
            yield return Until(() => boss.HP < bossHealth, "gold reflection reaches its owner"); // 반사 탄이 원래 Boss에 도달해 한 번 피해를 전달할 때까지 대기
            Require(Close(boss.HP, bossHealth - 4f) && (gold == null || !gold.IsActive) && retained.IsActive,
                "Reflected gold must damage its owning Boss exactly once while retained hazards survive the groggy interruption.");
        }
        finally
        {
            boss.OnGroggy -= onGroggy; // 일회 그로기 진입 수집 해제
        }
        boss.EndEncounter();
        boss.PatternSelector.SetPatterns();
        ClearProjectiles(boss);
        passed("F-B04/F-B06 real Slash deduplicates normal and gold reactions, consumes 100 gauge immediately, cancels the pattern, preserves hazards and reflects gold to its owner with Fever");

        // 같은 공격 번호의 두 부위가 모두 모였을 때만 더미 보스가 자기 성공 조건 판단
        Hitbox firstPart = null;
        Hitbox secondPart = null;
        foreach (Hitbox hitbox in boss.GetComponentsInChildren<Hitbox>())
        {
            if (hitbox.PartId == 1) firstPart = hitbox;
            if (hitbox.PartId == 2) secondPart = hitbox;
        }
        Require(firstPart != null && secondPart != null, "The dummy prefab must expose separate PartId 1 and 2 Hitboxes.");
        var partQuery = new CollisionBox();
        partQuery.Begin(new DamageInstance(player, 1f, DamageType.Slash, DamageInstance.NextAttackId()), ~0);
        Physics2D.SyncTransforms();
        partQuery.QuerySlash(firstPart.transform.position, Vector2.one * 0.1f, 0f, player, boss);
        Require(!boss.IsGroggy && boss.SlashParts.Count == 1, "A Slash through only one dummy part must not satisfy the two-part groggy condition.");
        float beforePartsHealth = boss.HP;
        int bothAttackId = DamageInstance.NextAttackId();
        partQuery.Begin(new DamageInstance(player, 2f, DamageType.Slash, bothAttackId), ~0);
        partQuery.QuerySlash(boss.transform.position, Vector2.one * 4f, 0f, player, boss);
        Require(boss.IsGroggy && boss.SlashParts.Count >= 2 && boss.LastCompletedSlashAttackId == bothAttackId
            && Close(boss.HP, beforePartsHealth - 2f),
            $"A single Slash crossing both dummy parts must let the Boss trigger groggy while body damage remains deduplicated: groggy={boss.IsGroggy}, parts={boss.SlashParts.Count}, attack={boss.LastCompletedSlashAttackId}/{bothAttackId}, HP={boss.HP}/{beforePartsHealth - 2f}.");
        partQuery.End();
        boss.ExitGroggy();
        passed("F-B04/F-B06 DummyBoss judges its own same-Slash two-part condition: one part fails, both parts groggy and shared body damage is applied once");

        // 무효 반사 목표에는 보상을 주지 않고 정지 중 모든 위험물 수명을 보존
        player.Buffs.Clear();
        player.Groggy = 0f;
        GoldProjectile invalid = Spawn<GoldProjectile>("Gold", boss, slashCenter, Vector2.up, 0f, 5f, 1f, fixtures);
        boss.IsDead = true;
        Require(!invalid.OnSlashed(player, boss, new DamageInstance(player, 1f, DamageType.Slash, DamageInstance.NextAttackId()))
            && !invalid.IsReflected && player.Groggy == 0f, "A dead reflection target must reject gold without granting rewards.");
        boss.IsDead = false;
        invalid.ReflectionSpeed = float.NaN;
        Require(!invalid.Reflect(boss, player), "A non-finite reflection speed must not enter reflected movement.");
        invalid.Despawn();
        NormalProjectile frozen = Spawn<NormalProjectile>("Normal", boss, slashCenter, Vector2.right, 1f, 0.8f, 1f, fixtures);
        AreaProjectile frozenArea = Spawn<AreaProjectile>("Area", boss, slashCenter + Vector2.up * 2f, Vector2.right, 0f, 0.8f, 1f, fixtures);
        manager.SetGameplayPaused(true);
        yield return Frames(1); // 시간 정지 진입 이후 수명·위치 비교 시작
        float lifetime = frozen.RemainingLifetime;
        float areaLifetime = frozenArea.RemainingLifetime;
        Vector3 frozenPosition = frozen.transform.position;
        yield return RealSeconds(0.1f); // 실제 시간만 흐를 때 탄·장판 상태가 유지되는지 확인
        Require(frozen.RemainingLifetime == lifetime && frozenArea.RemainingLifetime == areaLifetime && frozen.transform.position == frozenPosition,
            "Paused gameplay must preserve normal and area lifetime as well as moving projectile position.");
        manager.SetGameplayPaused(false);
        yield return Until(() => (frozen == null || !frozen.IsActive) && (frozenArea == null || !frozenArea.IsActive), "hazard lifetime expiry"); // 게임 시간 재개 뒤 각 위험물이 자기 수명으로 종료될 때까지 대기
        passed("F-B06 invalid reflection targets and speeds grant no rewards, pause freezes projectile and area lifetime, and resumed game time expires both");
    }

    /// <summary>더미 몸체의 실제 물리 충돌과 접촉 자체의 무피해 확인</summary>
    private static IEnumerator CheckBody(DummyBoss boss, PlayerCharacter player, Rigidbody2D body, Action<string> passed)
    {
        // 패턴·탄환을 닫은 실제 더미 몸체 왼쪽에서 누적 이동 요청
        boss.EndEncounter();
        ClearProjectiles(boss);
        player.Buffs.Clear();
        player.IsInvulnerable = false;
        player.enabled = false;
        player.ResetMotion();
        SetPosition(body, (Vector2)boss.transform.position + Vector2.left * 3f);
        float health = player.HP;
        Collider2D bossShape = boss.GetComponent<Collider2D>();
        Collider2D playerShape = player.GetComponent<Collider2D>();
        Require(bossShape != null && bossShape.enabled && !bossShape.isTrigger, "The dummy must keep a solid Boss body independent of its Hitbox.");
        Bounds solidBounds = bossShape.bounds;
        foreach (Collider2D shape in boss.GetComponentsInChildren<Collider2D>())
            if (shape.enabled && !shape.isTrigger) solidBounds.Encapsulate(shape.bounds);
        PlayerBodyController controller = player.GetComponent<PlayerBodyController>();
        Vector2 beforePosition = body.position;
        float beforeFixed = Time.fixedTime;
        float nextFixed = beforeFixed + Time.fixedDeltaTime;
        controller.Move(Vector2.right * 6f);
        yield return Until(() => Time.fixedTime >= nextFixed - 0.00001f, "dummy body fixed step"); // 검사 실행기에서 실제 FixedUpdate 시간이 한 번 이상 진행될 때까지 대기
        Physics2D.SyncTransforms();
        Require(playerShape.bounds.max.x <= solidBounds.min.x + 0.02f && playerShape.bounds.max.x > solidBounds.min.x - 0.2f,
            $"The solid Boss body must stop Player movement at its edge instead of allowing entry: PlayerBounds={playerShape.bounds}, BossBounds={solidBounds}, position={beforePosition}->{body.position}, movement={controller.ActualMovement}, skin={controller.SkinWidth}, fixedTime={beforeFixed}->{Time.fixedTime}.");
        yield return GameSeconds(0.1f); // 접촉을 유지하는 동안 별도 접촉 피해가 발생하지 않는지 확인
        Require(Close(player.HP, health), "Touching a solid Boss must not itself deal damage.");
        player.enabled = true;
        passed("F-B06 the real dummy body blocks Player entry at its collider edge without contact damage");
    }

    /// <summary>저장된 실제 탄환 프리팹을 복제하고 공통 발사 API로 준비</summary>
    private static T Spawn<T>(string name, BossCharacter owner, Vector2 position, Vector2 direction, float speed, float lifetime, float damage, List<GameObject> fixtures) where T : Projectile
    {
        T prefab = AssetDatabase.LoadAssetAtPath<T>(PrefabDirectory + name + ".prefab");
        Require(prefab != null, "Missing concrete projectile prefab: " + name);
        T instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        fixtures.Add(instance.gameObject);
        instance.Initialize(owner, direction, speed, lifetime, damage);
        Physics2D.SyncTransforms();
        return instance;
    }

    /// <summary>같은 탄환의 여러 Collider가 보상 중복을 만들지 않는지 검사할 자식 준비</summary>
    private static void AddDuplicateHitbox(Projectile projectile)
    {
        var child = new GameObject("DuplicateHitbox");
        child.transform.SetParent(projectile.transform, false);
        child.AddComponent<CircleCollider2D>().isTrigger = true;
        child.AddComponent<Hitbox>();
    }

    /// <summary>검사에서만 더미 소유의 활성 탄환을 수집</summary>
    private static Projectile[] OwnedProjectiles(BossCharacter owner)
    {
        var result = new List<Projectile>();
        foreach (Projectile projectile in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            if (projectile.Owner == owner && projectile.IsActive) result.Add(projectile);
        return result.ToArray();
    }

    /// <summary>검사 경계에서 더미 소유 탄환만 즉시 비활성화</summary>
    private static void ClearProjectiles(BossCharacter owner)
    {
        foreach (Projectile projectile in OwnedProjectiles(owner)) projectile.Despawn();
    }

    /// <summary>Transform과 물리 위치를 함께 이동해 같은 프레임의 판정에 반영</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        body.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>잘못된 등록의 계약 오류가 실제로 전달되는지 확인</summary>
    private static void ExpectArgument(Action action)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, "Invalid pattern registration must throw an argument error.");
    }

    /// <summary>피해 수치를 부동소수점 오차 범위 안에서 비교</summary>
    private static bool Close(float first, float second) => Mathf.Abs(first - second) < 0.001f;

    /// <summary>실제 Update 실행 횟수가 필요한 검사 경계 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= target, "frames"); // 실제 런타임 갱신이 지정한 횟수만큼 끝날 때까지 대기
    }

    /// <summary>게임 정지를 반영하는 수명 관찰 시간 대기</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        float target = Time.time + seconds;
        yield return Until(() => Time.time >= target, "game seconds"); // 패턴·탄환과 같은 게임 시간 경과 대기
    }

    /// <summary>게임 시간이 멈춘 채 실제 시간 경과 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float target = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= target, "real seconds"); // 일시정지 중 상태 보존 비교 대기
    }

    /// <summary>조건을 실제 프레임에서 기다리고 무한 대기를 검사 실패로 전환</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-B01~06 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>보스 전투 계약 위반을 기존 검사 결과로 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>콘텐츠 발사와 분리해 공통 선택·시간·취소 계약을 확인할 C# 패턴</summary>
    private class ProbePattern : BossPattern
    {
        public Func<BossCharacter, bool> Condition; // HP·거리·선행 패턴 사용 등 현재 조건을 검사할 함수
        public float FinishAfter = 100f; // 실제 Tick이 정상 완료를 발생시킬 게임 경과 시간
        public int Cancels; // 취소 경계에서 중복 호출이 없는지 비교할 누적 횟수
        public bool CompleteOnBegin; // 시작 콜백 안에서 정상 완료하는 재진입 검사 여부
        public Action CancelAction; // 취소 콜백에서 다음 행동을 요청할 재진입 검사

        /// <summary>검사 패턴의 실제 선택 식별자 준비</summary>
        public ProbePattern(string id) : base(id) { }

        /// <summary>공통 쿨타임과 검사에서 지정한 현재 상태 조건을 함께 적용</summary>
        public override bool CanRun(BossCharacter owner)
        {
            return base.CanRun(owner) && (Condition == null || Condition(owner));
        }

        /// <summary>기본 Begin의 실행 상태만 사용해 부가 동작 없이 시작</summary>
        protected override void OnBegin()
        {
            if (CompleteOnBegin) Complete();
        }

        /// <summary>실제 패턴 시간 누적이 종료 기한을 지났을 때 정상 완료 통지</summary>
        protected override void OnTick(float elapsed)
        {
            if (Elapsed >= FinishAfter) Complete();
        }

        /// <summary>그로기·컷씬·사망 경계에서 실제 취소된 횟수 수집</summary>
        protected override void OnCancel()
        {
            Cancels++;
            CancelAction?.Invoke();
        }
    }
}
