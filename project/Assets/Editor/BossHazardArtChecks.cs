using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>격리된 실제 세션에서 새 위험물의 판정·시간·표시와 Unity 화면을 확인</summary>
[InitializeOnLoad]
public static class BossHazardArtChecks
{
    private const string ActiveKey = "Chrono.BossHazardArt.Active"; // Play Mode 재로드 뒤 검사 요청을 복원할 키
    private const string FinishedKey = "Chrono.BossHazardArt.Finished"; // 편집 모드 복귀에서 종료를 구분할 키
    private const string SuccessKey = "Chrono.BossHazardArt.Success"; // 배치 종료 코드를 결정할 검사 결과 키
    private const string SceneKey = "Chrono.BossHazardArt.Scene"; // 열린 Editor에서 복귀할 원래 씬 경로 키
    private const string SaveKey = "Chrono.BossHazardArt.Save"; // 실제 사용자 저장과 분리한 검사 저장 경로 키
    private static Stack<IEnumerator> routines; // 중첩 대기와 검사를 순차 실행할 스택
    private static Report report; // 통과 항목과 실제 실패 원인을 저장할 결과
    private static double deadline; // 실행이 끝나지 않을 때 검사를 닫을 Editor 시각

    /// <summary>도메인 재로드 뒤에도 같은 Play Mode 검사 요청 연결</summary>
    static BossHazardArtChecks()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode; // Play 진입에서 검사를 시작하고 편집 복귀에서 종료 처리
    }

    /// <summary>메뉴와 배치에서 프리팹을 준비하고 저장이 격리된 실제 Title 진입</summary>
    [MenuItem("Tools/Chrono/Check Boss Hazard Art")]
    public static void RunBatch()
    {
        // 사용자가 저장하지 않은 씬을 덮어쓰지 않도록 시작 조건 확인
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Begin hazard checks outside Play Mode.");
        Scene original = SceneManager.GetActiveScene();
        if (!Application.isBatchMode && (SceneManager.sceneCount != 1 || original.isDirty || string.IsNullOrEmpty(original.path)))
            throw new InvalidOperationException("Hazard checks require one saved, unmodified scene.");
        SessionState.SetString(SceneKey, original.path);
        BossHazardArtSetup.Run();
        string savePath = Path.GetFullPath(Path.Combine("Temp", "BossHazardArtChecks", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(savePath);
        SessionState.SetString(SaveKey, savePath);
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (manager == null) throw new InvalidOperationException("Title requires GameManager.");
        manager.SaveDirectory = savePath;
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(FinishedKey, false);
        SessionState.SetBool(SuccessKey, false);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>이번 검사 요청에만 반응하고 배치 결과 또는 원래 편집 씬 복귀</summary>
    private static void HandlePlayMode(PlayModeStateChange state)
    {
        // 다른 Play Mode 실행에는 개입하지 않도록 요청 상태 확인
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report();
            routines = new Stack<IEnumerator>();
            routines.Push(Check()); // 실제 세션과 위험물 검사 순차 실행
            deadline = EditorApplication.timeSinceStartup + 120d;
            EditorApplication.update += Tick; // 실제 프레임마다 검사 진행과 전체 제한 시간 확인
        }
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(FinishedKey, false))
        {
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(SuccessKey, false) ? 0 : 1);
            else EditorSceneManager.OpenScene(SessionState.GetString(SceneKey, "Assets/Scenes/Title.unity"));
        }
    }

    /// <summary>검사와 중첩 대기를 진행하고 실패하면 실제 예외 저장</summary>
    private static void Tick()
    {
        try
        {
            // 실제 프레임을 양보하면서 중첩 검사와 종료 조건 처리
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Hazard art checks exceeded 120 seconds.");
            while (routines.Count > 0)
            {
                IEnumerator routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); (routine as IDisposable)?.Dispose(); continue; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    /// <summary>실제 Player와 새 프리팹으로 전조·단일피해·정지·수명·Slash 및 화면 검사</summary>
    private static IEnumerator Check()
    {
        // 실제 슬롯과 씬 서비스를 준비하고 현재 맵과 떨어진 검사 공간 사용
        yield return WaitFor(() => GameManager.Instance != null && GetTitle() != null && GetTitle().IsReady, "Title readiness"); // 저장과 Title 준비 완료 대기
        GameManager manager = GameManager.Instance;
        GetTitle().SelectSlot(0);
        yield return WaitFor(() => !manager.IsLoading && manager.Stage.IsReady && manager.CurrentPlayer != null
            && !manager.CurrentPlayer.IsSpawning, "session readiness"); // 실제 Player 생성 연출 완료 대기
        PlayerCharacter player = manager.CurrentPlayer;
        PlayerInputController.Instance.enabled = false;
        PlayerInputController.Instance.BlockInput = false;
        manager.SetGameplayPaused(false);
        player.ResetMotion();
        player.Buffs.Clear();
        player.IsInvulnerable = false;
        player.HitProtectionDuration = 0f;
        player.HP = player.maxHP;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        SetPosition(body, new Vector2(400.8f, 400f));
        var fixtures = new List<GameObject>();
        GameObject bossRoot = new GameObject("HazardFixtureBoss");
        fixtures.Add(bossRoot);
        BossCharacter boss = bossRoot.AddComponent<BossCharacter>();
        boss.transform.position = new Vector3(410f, 400f, 0f);
        CircleCollider2D bossShape = bossRoot.AddComponent<CircleCollider2D>();
        bossShape.isTrigger = true;
        bossShape.radius = 0.5f;
        bossRoot.AddComponent<Hitbox>().Owner = boss;
        boss.InitializeHealth(boss.maxHP);
        try
        {
            // 저장한 실사용 및 기존 시험 프리팹의 시각·판정 연결 확인
            CheckPrefabContracts();
            CheckWarningShapes(fixtures);
            LaserProjectile laser = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LaserProjectile>("Assets/Prefabs/BossTests/Laser.prefab"), new Vector3(420f, 400f, 0f), Quaternion.identity);
            fixtures.Add(laser.gameObject);
            laser.Initialize(boss, Vector2.right, 0f, 3f, 10f);
            SpriteRenderer laserSprite = laser.GetComponentInChildren<SpriteRenderer>();
            Require(laser.GetComponent<BoxCollider2D>().size == new Vector2(laser.Length, laser.Width)
                && laserSprite != null && laserSprite.enabled && laserSprite.size == new Vector2(laser.Length, laser.Width)
                && laser.GetComponent<CircleWarningVfx>() == null, "Existing box laser must retain its Sprite and rectangular geometry.");
            Passed("Existing laser keeps its rectangular Sprite path after the optional circle warning change");
            AreaProjectile area = SpawnArea(boss, new Vector2(400f, 400f), 1f, 3f, 10f, fixtures);
            NormalProjectile moving = SpawnBullet(boss, new Vector2(406f, 400f), Vector2.up, 2f, 3f, 7f, fixtures);
            ProjectileVfx movingVisual = moving.GetComponent<ProjectileVfx>();
            yield return WaitGame(0.18f); // 전조 구간의 실제 Update 진행 대기
            Require(player.HP == player.maxHP && !area.IsArmed, "Telegraph must not damage an overlapping Player.");
            Passed("Actual Player takes no damage during the red outline telegraph");

            // 일반 정지가 경고 경계·이동·수명을 함께 멈추는지 실제 시간으로 비교
            float areaLife = area.RemainingLifetime;
            float bulletLife = moving.RemainingLifetime;
            Vector3 bulletPosition = moving.transform.position;
            float pulsePhase = movingVisual.PulsePhase;
            float haloOpacity = movingVisual.AuraOpacity;
            SpriteRenderer cachedHalo = movingVisual.Halo;
            Material cachedHaloMaterial = cachedHalo.sharedMaterial;
            manager.SetGameplayPaused(true);
            yield return WaitReal(0.18f); // 게임 정지 중 실제 시간 경과 대기
            Require(!area.IsArmed && area.RemainingLifetime == areaLife && moving.RemainingLifetime == bulletLife
                && moving.transform.position == bulletPosition && player.HP == player.maxHP,
                "Pause must preserve the telegraph, lifetime, position and Player HP.");
            Passed("Pause freezes both warning and pixel bullet lifetime and movement");
            Require(movingVisual.PulsePhase == pulsePhase && movingVisual.AuraOpacity == haloOpacity
                && Mathf.Approximately(cachedHalo.color.a, haloOpacity), "Pause must freeze the scaled halo pulse and its visible opacity.");
            manager.SetGameplayPaused(false);
            yield return WaitFor(() => Mathf.Abs(movingVisual.PulsePhase - pulsePhase) > 0.01f
                && Mathf.Abs(movingVisual.AuraOpacity - haloOpacity) > 0.005f, "halo pulse resume"); // 실제 게임 시간 재개 후 후광 밝기 변화 대기
            Require(movingVisual.AuraOpacity >= movingVisual.MinimumOpacity && movingVisual.AuraOpacity <= 1f
                && movingVisual.Halo == cachedHalo && cachedHalo.sharedMaterial == cachedHaloMaterial
                && moving.transform.localScale == Vector3.one && moving.GetComponent<SpriteRenderer>().color == Color.white,
                "Resumed halo must reuse its renderer/material while preserving body scale and color.");
            Passed("The scaled halo pulse freezes on pause, resumes within its opacity range and reuses its renderer/material without changing the body");
            yield return WaitFor(() => area.IsArmed && player.HP < player.maxHP, "armed area damage"); // 경고 종료와 실제 범위 피해 완료 대기
            Require(Mathf.Approximately(player.HP, player.maxHP - 10f), "Arming must damage the Player exactly once.");
            Color activeOutline = area.GetComponent<LineRenderer>().startColor;
            Require(((Color32)activeOutline).Equals((Color32)area.ActiveColor),
                "Arming must change the outline color: actual=" + activeOutline.ToString("F4") + ", expected=" + area.ActiveColor.ToString("F4"));
            CircleWarningVfx activeWarning = area.GetComponent<CircleWarningVfx>();
            var fillProperties = new MaterialPropertyBlock();
            activeWarning.FillRenderer.GetPropertyBlock(fillProperties);
            Require(Mathf.Approximately(fillProperties.GetColor("_Color").a, area.ActiveColor.a * activeWarning.FillOpacityMultiplier)
                && activeWarning.FillRenderer.sortingOrder < area.GetComponent<LineRenderer>().sortingOrder,
                "Activation must update the translucent fill and keep its outline above it.");
            Passed("Warning activation changes the red outline and delivers one actual damage event; color=" + activeOutline.ToString("F4"));
            SetPosition(body, new Vector2(403f, 400f));
            yield return WaitGame(0.08f); // 실제 범위 밖 위치가 물리 질의에 반영될 때까지 대기
            SetPosition(body, new Vector2(400.8f, 400f));
            player.ResetHitProtection();
            yield return WaitGame(0.08f); // 같은 장판 재진입 후 중복 피해 검사 대기
            Require(Mathf.Approximately(player.HP, player.maxHP - 10f), "Re-entering the same area must not damage twice.");
            Require(!area.OnSlashed(player, boss, new DamageInstance(player, 10f, DamageType.Slash, DamageInstance.NextAttackId()), Vector2.right)
                && area.IsActive, "A circular warning area must ignore Slash removal.");
            Passed("The same area ignores Slash and does not damage again after re-entry");

            // 그로기는 이미 발사한 탄과 장판의 수명을 제거하지 않음
            boss.EnterGroggy();
            Require(area.IsActive && moving.IsActive && boss.IsGroggy, "Groggy must retain existing hazards.");
            yield return WaitGame(0.08f); // 그로기 중 기존 위험물의 독립 진행 대기
            Require(area.RemainingLifetime < areaLife && moving.transform.position.y > bulletPosition.y,
                "Existing hazards must continue their own lifetime and flight during Boss groggy.");
            Passed("Boss groggy keeps existing circular areas and pixel bullets running");
            yield return WaitFor(() => area == null && moving == null, "hazard lifetime expiry"); // 발사 수명 만료와 실제 객체 제거 완료 대기
            Passed("The pixel bullet and circular area despawn at their game-time lifetime");

            // 새 일반 탄이 실제 Player 경로에 닿을 때 피해와 종료를 한 번 처리
            float before = player.HP;
            NormalProjectile hit = SpawnBullet(boss, (Vector2)player.transform.position + Vector2.left, Vector2.right, 6f, 1f, 7f, fixtures);
            yield return WaitFor(() => hit == null || !hit.IsActive, "pixel bullet hit"); // 실제 이동 경로의 Player 적중과 종료 완료 대기
            Require(Mathf.Approximately(player.HP, before - 7f), "Pixel bullet must deliver its actual configured damage once.");
            NormalProjectile slashed = SpawnBullet(boss, new Vector2(406f, 400f), Vector2.up, 1f, 2f, 7f, fixtures);
            float groggy = player.Groggy;
            DamageInstance slash = new DamageInstance(player, 10f, DamageType.Slash, DamageInstance.NextAttackId());
            Require(slashed.OnSlashed(player, boss, slash, Vector2.right) && !slashed.IsActive && !slashed.OnSlashed(player, boss, slash, Vector2.right)
                && Mathf.Approximately(player.Groggy, groggy + slashed.GroggyGain), "Normal bullet Slash must remove it and award groggy once.");
            Passed("Pixel bullet preserves actual flight damage, one Slash removal and one groggy award");
            ProjectileVfx disabledVisual = slashed.GetComponent<ProjectileVfx>();
            Require(disabledVisual.AuraOpacity == 1f && disabledVisual.PulsePhase == 0f && disabledVisual.Halo.color == Color.white
                && slashed.GetComponent<SpriteRenderer>().color == Color.white, "Despawn must reset only the halo phase and restore its original color.");
            Passed("Projectile disable restores the original halo opacity and phase while keeping the pixel body unchanged");

            // 왼쪽 입사 금색을 수직 Line으로 오른쪽 반사해 실제 Boss Hitbox에 적중
            GoldProjectile gold = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GoldProjectile>(BossHazardArtSetup.GoldPath),
                boss.transform.position + Vector3.left * 2f, Quaternion.identity);
            fixtures.Add(gold.gameObject);
            gold.Initialize(boss, Vector2.left, 1f, 2f, 9f);
            float beforeGoldHP = player.HP;
            float beforeBossHP = boss.HP;
            float beforeGoldGroggy = player.Groggy;
            float goldGain = gold.GroggyGain;
            Require(gold.OnSlashed(player, null, slash, Vector2.up) && gold.IsActive && gold.IsReflected && gold.ReflectionTarget == boss
                && Vector2.Dot(gold.Direction, Vector2.right) > 0.9999f
                && !gold.OnSlashed(player, boss, slash, Vector2.up) && Mathf.Approximately(player.Groggy, beforeGoldGroggy + goldGain),
                "A vertical Line must mirror leftward gold to the right, retain its actual Owner and award groggy exactly once.");
            yield return WaitFor(() => player.Buffs.IsFever, "gold reflection fever"); // 실제 다음 프레임의 각성 및 획득 회복 적용 대기
            Require(Mathf.Approximately(player.HP, Mathf.Min(player.maxHP, beforeGoldHP + player.Buffs.HealPerAcquisition))
                && Mathf.Approximately(player.Groggy, beforeGoldGroggy + goldGain), "One gold reflection must grant Fever and one acquisition heal.");
            Passed("Gold pixel bullet reflects to Owner, rejects duplicate Slash and grants Fever, heal and groggy once");
            yield return WaitFor(() => gold == null || !gold.IsActive, "gold Owner impact"); // 실제 반사 이동과 소유 Boss 도달 피해 완료 대기
            Require(Mathf.Approximately(boss.HP, beforeBossHP - 9f), "Reflected gold bullet must damage its Owner once and despawn.");
            Passed("Reflected gold pixel bullet reaches its Owner, applies configured damage once and despawns");

            // 실제 Unity 카메라로 배경 두 가지와 원형 반경 두 가지를 캡처
            CaptureGallery(fixtures, player);
            Passed("Actual Unity camera captures Player and normal/gold halos at the same 6x pixels/baseline, six warning forms, and a dark bullet close-up");
        }
        finally
        {
            // 실패한 검사에서도 자기 위험물과 임시 보스만 제거하고 시간 복원
            manager.SetGameplayPaused(false);
            foreach (GameObject fixture in fixtures) if (fixture != null) UnityEngine.Object.Destroy(fixture);
        }
        manager.ReturnToTitle();
        yield return WaitFor(() => GetTitle() != null && GetTitle().IsReady && !manager.IsLoading, "Title cleanup"); // 실제 세션 정리 완료 대기
        Passed("Isolated save session returns to Title after removing hazard fixtures");
    }

    /// <summary>순수 예고 네 종류의 표시·크기 변경·공유 수명만 피해 판정과 독립 확인</summary>
    private static void CheckWarningShapes(List<GameObject> fixtures)
    {
        // 같은 공유 도형과 재질을 쓸 네 프리팹을 실제 복제해 표시 초기화 확인
        string[] paths = { BossHazardArtSetup.CircleWarningPath, BossHazardArtSetup.RectangleWarningPath,
            BossHazardArtSetup.SectorWarningPath, BossHazardArtSetup.StraightWarningPath };
        WarningShape[] shapes = { WarningShape.Circle, WarningShape.Rectangle, WarningShape.Sector, WarningShape.OpenStrip };
        Mesh commonCircle = null;
        Mesh commonRectangle = null;
        Material commonMaterial = null;
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            Require(prefab != null && prefab.GetComponent<AreaWarningVfx>() != null && prefab.transform.localScale == Vector3.one
                && prefab.GetComponent<Projectile>() == null && prefab.GetComponentsInChildren<Collider2D>(true).Length == 0,
                "Warning shape prefab must remain a pure visual at unit root scale: " + paths[i]);
            AreaWarningVfx first = UnityEngine.Object.Instantiate(prefab, new Vector3(450f, 450f, 0f), Quaternion.identity).GetComponent<AreaWarningVfx>();
            AreaWarningVfx second = UnityEngine.Object.Instantiate(prefab, new Vector3(454f, 450f, 0f), Quaternion.identity).GetComponent<AreaWarningVfx>();
            fixtures.Add(first.gameObject);
            fixtures.Add(second.gameObject);
            LineRenderer line = first.GetComponent<LineRenderer>();
            MeshFilter fill = first.FillRenderer.GetComponent<MeshFilter>();
            MeshFilter otherFill = second.FillRenderer.GetComponent<MeshFilter>();
            Require(first.Shape == shapes[i] && fill.sharedMesh != null && otherFill.sharedMesh != null,
                "Prefab instantiation must prepare its actual fill mesh and requested warning shape.");
            if (i == 0)
            {
                commonCircle = first.CircleMesh;
                commonRectangle = first.RectangleMesh;
                commonMaterial = line.sharedMaterial;
            }
            Require(first.CircleMesh == commonCircle && first.RectangleMesh == commonRectangle && line.sharedMaterial == commonMaterial
                && first.FillRenderer.sharedMaterial == commonMaterial, "Warning instances must share unit circle, unit rectangle and material assets.");

            // 공용 재질을 바꾸지 않고 경계색과 더 옅은 채움색을 인스턴스에만 적용
            Color color = new Color(1f, 0.12f, 0.16f, 0.45f);
            first.SetColor(color);
            var properties = new MaterialPropertyBlock();
            first.FillRenderer.GetPropertyBlock(properties);
            Require(((Color32)line.startColor).Equals((Color32)color)
                && Mathf.Approximately(properties.GetColor("_Color").a, color.a * first.FillOpacityMultiplier)
                && first.FillRenderer.sortingOrder < line.sortingOrder, "Warning fill must stay more translucent than its outline.");
            Mesh original = fill.sharedMesh;
            first.SetColor(new Color(1f, 0.12f, 0.16f, 0.8f));
            first.SetColor(color);
            Require(fill.sharedMesh == original && line.sharedMaterial == commonMaterial, "Changing warning color must reuse geometry and shared material.");

            // 원·사각형은 루트의 가로·세로 확대만으로 타원·직사각형 표시
            if (shapes[i] == WarningShape.Circle || shapes[i] == WarningShape.Rectangle)
            {
                Require(fill.sharedMesh == (shapes[i] == WarningShape.Circle ? commonCircle : commonRectangle)
                    && otherFill.sharedMesh == fill.sharedMesh && line.loop, "Circle and rectangle must reuse shared unit meshes with closed outlines.");
                Vector3 scale = shapes[i] == WarningShape.Circle ? new Vector3(2f, 1.5f, 1f) : new Vector3(2.5f, 0.75f, 1f);
                first.transform.localScale = scale;
                Require(Mathf.Abs(first.FillRenderer.bounds.size.x - scale.x) < 0.005f
                    && Mathf.Abs(first.FillRenderer.bounds.size.y - scale.y) < 0.005f,
                    "Independent root x/y scale must stretch the actual warning fill bounds.");
            }
            else if (shapes[i] == WarningShape.Sector)
            {
                // 각도 변경은 자기 가변 메시를 재사용하며 다른 부채꼴 복제본과 분리
                Require(original != otherFill.sharedMesh && !EditorUtility.IsPersistent(original), "Each sector must own its reusable variable-angle mesh.");
                first.Configure(WarningShape.Sector, color, 120f);
                Require(fill.sharedMesh == original && Mathf.Abs(original.bounds.size.x - 0.8660254f) < 0.01f
                    && Mathf.Abs(original.bounds.size.y - 0.5f) < 0.01f, "A 120-degree sector must retain its mesh while changing its actual arc bounds.");
                first.Configure(WarningShape.Sector, color, 90f);
                Require(fill.sharedMesh == original && Mathf.Abs(original.bounds.size.x - 0.7071068f) < 0.01f,
                    "Restoring 90 degrees must update the same sector mesh.");
                first.Configure(WarningShape.Sector, color, 360f);
                Require(Mathf.Abs(fill.sharedMesh.bounds.size.x - 1f) < 0.01f && Mathf.Abs(fill.sharedMesh.bounds.size.y - 1f) < 0.01f,
                    "A full-circle sector must cover the unit circle.");
            }
            else
            {
                // 직선 띠는 양끝을 닫지 않는 평행한 긴 경계 두 개와 사각 채움만 사용
                LineRenderer other = first.SecondaryOutline;
                Require(fill.sharedMesh == commonRectangle && otherFill.sharedMesh == commonRectangle && other != null
                    && !line.loop && !other.loop && line.positionCount == 2 && other.positionCount == 2
                    && line.numCapVertices == 0 && other.numCapVertices == 0
                    && Mathf.Approximately(line.GetPosition(0).y, line.GetPosition(1).y)
                    && Mathf.Approximately(other.GetPosition(0).y, other.GetPosition(1).y)
                    && Mathf.Abs(line.GetPosition(0).x - line.GetPosition(1).x) > 0.99f
                    && Mathf.Abs(line.GetPosition(0).y - other.GetPosition(0).y) > 0.99f,
                    "Open strip must use exactly two uncapped parallel long edges, without vertical closing edges.");
            }
        }
        Passed("Four pure warning prefabs preserve shared geometry/materials, translucent fill and closed/open outline contracts");
        Passed("Actual warning instances stretch into ellipse/rectangle bounds and reuse their sector mesh across 90/120/360-degree changes");
    }

    /// <summary>기존 Dummy 참조와 새 프리팹의 실제 Sprite·선·판정 설정 확인</summary>
    private static void CheckPrefabContracts()
    {
        // 실사용과 시험 프리팹이 같은 새 표시 및 기존 전투 컴포넌트를 사용하는지 확인
        Sprite normalBody = AssetDatabase.LoadAssetAtPath<Sprite>(BossHazardArtSetup.SpritePath);
        Sprite normalHalo = AssetDatabase.LoadAssetAtPath<Sprite>(BossHazardArtSetup.NormalHaloPath);
        foreach (string path in new[] { BossHazardArtSetup.BulletPath, "Assets/Prefabs/BossTests/Normal.prefab",
            BossHazardArtSetup.GoldPath, "Assets/Prefabs/BossTests/Gold.prefab" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            bool gold = path == BossHazardArtSetup.GoldPath || path == "Assets/Prefabs/BossTests/Gold.prefab";
            Require(prefab.GetComponent<NormalProjectile>() != null && prefab.GetComponent<Hitbox>() != null
                && (prefab.GetComponent<GoldProjectile>() != null) == gold
                && renderer.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(gold ? BossHazardArtSetup.GoldSpritePath : BossHazardArtSetup.SpritePath)
                && renderer.sprite != null && renderer.sprite.texture.width == 18 && renderer.sprite.texture.height == 18
                && renderer.sprite.pixelsPerUnit == 16f && renderer.sprite.texture.filterMode == FilterMode.Point
                && renderer.sprite.bounds.size == normalBody.bounds.size
                && renderer.drawMode == SpriteDrawMode.Simple && renderer.color == Color.white
                && Mathf.Approximately(prefab.GetComponent<CircleCollider2D>().radius, 0.39f)
                && Mathf.Approximately(prefab.GetComponent<NormalProjectile>().CollisionRadius, 0.39f),
                "Pixel bullet prefab must preserve its normal/gold type and use the expanded 18x18 Sprite at natural 16 PPU size: " + path);
            ProjectileVfx visual = prefab.GetComponent<ProjectileVfx>();
            Require(visual != null && visual.Halo != null && visual.Halo.sprite != null
                && visual.Halo.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(gold ? BossHazardArtSetup.GoldHaloPath : BossHazardArtSetup.NormalHaloPath)
                && visual.Halo.sprite.texture.width == 32 && visual.Halo.sprite.texture.height == 32
                && visual.Halo.sprite.pixelsPerUnit == 16f && visual.Halo.sprite.texture.filterMode == FilterMode.Bilinear
                && visual.Halo.sprite.texture.mipmapCount == 1 && visual.Halo.transform.localScale == Vector3.one
                && visual.Halo.sprite.bounds.size == normalHalo.bounds.size
                && visual.Halo.transform.localPosition == Vector3.zero && visual.Halo.sortingOrder == 29
                && visual.Halo.sharedMaterial == renderer.sharedMaterial && visual.MinimumOpacity == 0.7f && visual.PulseFrequency == 1f
                && prefab.GetComponentsInChildren<Collider2D>(true).Length == 1
                && prefab.transform.Find("InteractionRing") == null && prefab.GetComponentsInChildren<SpriteRenderer>(true).Length == 2,
                "Bullet halo must use the cached 32px soft-alpha Sprite and the existing material without adding physical geometry: " + path);
        }
        foreach (string path in new[] { BossHazardArtSetup.AreaPath, "Assets/Prefabs/BossTests/Area.prefab" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            AreaProjectile area = prefab.GetComponent<AreaProjectile>();
            LineRenderer line = prefab.GetComponent<LineRenderer>();
            CircleWarningVfx warning = prefab.GetComponent<CircleWarningVfx>();
            Require(area != null && warning != null && warning.FillRenderer != null
                && warning.FillRenderer.GetComponent<MeshFilter>().sharedMesh != null && prefab.GetComponent<CircleCollider2D>().radius == 1f
                && prefab.GetComponentsInChildren<SpriteRenderer>(true).Length == 0 && line.loop && !line.useWorldSpace
                && line.positionCount == 96 && Mathf.Approximately(line.startWidth, 0.04f)
                && area.TelegraphDuration == (path == BossHazardArtSetup.AreaPath ? 0.8f : 0.5f),
                "Area prefab must connect a translucent disk and closed red outline while keeping its configured telegraph: " + path);
        }
        DummyBoss dummy = AssetDatabase.LoadAssetAtPath<DummyBoss>("Assets/Prefabs/BossTests/DummyBoss.prefab");
        Require(AssetDatabase.GetAssetPath(dummy.ProjectilePrefabs[0]) == "Assets/Prefabs/BossTests/Normal.prefab"
            && AssetDatabase.GetAssetPath(dummy.ProjectilePrefabs[1]) == "Assets/Prefabs/BossTests/Gold.prefab"
            && AssetDatabase.GetAssetPath(dummy.ProjectilePrefabs[3]) == "Assets/Prefabs/BossTests/Area.prefab", "Dummy must preserve its existing normal, gold and area references.");
        Passed("Production and existing Dummy normal/gold prefabs use their new Sprites and geometry while area references remain intact");
    }

    /// <summary>실제 발사 API로 원형 위험물 생성</summary>
    private static AreaProjectile SpawnArea(BossCharacter boss, Vector2 position, float radius, float lifetime, float damage, List<GameObject> fixtures)
    {
        // 실제 프리팹을 복제한 뒤 반경과 발사 수명 연결
        AreaProjectile area = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AreaProjectile>(BossHazardArtSetup.AreaPath), position, Quaternion.identity);
        fixtures.Add(area.gameObject);
        area.Radius = radius;
        area.Initialize(boss, Vector2.up, 0f, lifetime, damage);
        return area;
    }

    /// <summary>실제 발사 API로 픽셀 탄환 생성</summary>
    private static NormalProjectile SpawnBullet(BossCharacter boss, Vector2 position, Vector2 direction, float speed, float lifetime, float damage, List<GameObject> fixtures)
    {
        // 실제 프리팹의 Sprite와 비행·피해 API를 함께 사용
        NormalProjectile bullet = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<NormalProjectile>(BossHazardArtSetup.BulletPath), position, Quaternion.identity);
        fixtures.Add(bullet.gameObject);
        bullet.Initialize(boss, direction, speed, lifetime, damage);
        return bullet;
    }

    /// <summary>흰 배경과 실험실 바닥의 같은 표시를 실제 카메라로 출력</summary>
    private static void CaptureGallery(List<GameObject> fixtures, PlayerCharacter player)
    {
        // 16 PPU의 원본 1px이 높이 768px 카메라에서 6px로 표시되는 투영 사용
        var root = new GameObject("HazardArtGallery");
        fixtures.Add(root);
        root.transform.position = new Vector3(500f, 500f, 0f);
        Camera camera = new GameObject("HazardArtCamera").AddComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.transform.localPosition = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.white;
        camera.enabled = false;
        GameObject bullet = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BossHazardArtSetup.BulletPath), root.transform);
        bullet.transform.localPosition = new Vector3(-1f, 2.5f, 0f);
        bullet.GetComponent<Projectile>().enabled = false;
        GameObject gold = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BossHazardArtSetup.GoldPath), root.transform);
        gold.transform.localPosition = new Vector3(0.5f, 2.5f, 0f);
        gold.GetComponent<Projectile>().enabled = false;

        // 실제 Player Sprite를 같은16PPU와 변형 없이 나란히 배치
        SpriteRenderer source = player.GetComponent<CharacterRendering>().BodySprite;
        SpriteRenderer comparison = new GameObject("PlayerSizeComparison").AddComponent<SpriteRenderer>();
        comparison.transform.SetParent(root.transform, false);
        comparison.transform.localPosition = new Vector3(-2.4f, 2f, 0f);
        comparison.transform.localScale = source.transform.lossyScale;
        comparison.sprite = source.sprite;
        comparison.sharedMaterial = source.sharedMaterial;
        comparison.flipX = source.flipX;
        comparison.flipY = source.flipY;
        comparison.color = Color.white;
        comparison.sortingOrder = 20;
        Require(comparison.sprite.pixelsPerUnit == 16f && bullet.transform.localScale == Vector3.one && gold.transform.localScale == Vector3.one,
            "Player and expanded normal/gold bullets must use the same natural 16 PPU comparison scale.");
        // 피해 없는 같은 경고 표시를 원·타원·정사각·직사각·부채꼴·양끝 열린 띠로 비교
        Color warning = new Color(1f, 0.12f, 0.16f, 0.45f);
        AddGalleryWarning(root.transform, BossHazardArtSetup.CircleWarningPath, new Vector2(-3.7f, 0.2f), new Vector2(1.4f, 1.4f), warning);
        AddGalleryWarning(root.transform, BossHazardArtSetup.CircleWarningPath, new Vector2(-1.3f, 0.2f), new Vector2(2.1f, 1.4f), warning);
        AddGalleryWarning(root.transform, BossHazardArtSetup.RectangleWarningPath, new Vector2(2.7f, 0.2f), new Vector2(1.4f, 1.4f), new Color(1f, 0.12f, 0.16f, 0.8f));
        AddGalleryWarning(root.transform, BossHazardArtSetup.RectangleWarningPath, new Vector2(-3.4f, -2.1f), new Vector2(2.4f, 1f), warning);
        AddGalleryWarning(root.transform, BossHazardArtSetup.SectorWarningPath, new Vector2(-0.5f, -2.8f), new Vector2(2.4f, 2.4f), warning, 120f);
        AddGalleryWarning(root.transform, BossHazardArtSetup.StraightWarningPath, new Vector2(3f, -2.1f), new Vector2(2.6f, 1f), warning);
        Capture(camera, "Logs/BossHazardArtVerification-white.png", true);

        // 기존 바닥이 반투명 채움 뒤에서도 보이는 실제 화면 확인
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Environment/Laboratory/Tiles/environment-018.asset");
        Require(tile != null && tile.sprite != null, "The laboratory floor Sprite must exist for background verification.");
        Transform floorRoot = new GameObject("FloorBackground").transform;
        floorRoot.SetParent(root.transform, false);
        for (int y = -5; y <= 4; y++)
            for (int x = -6; x <= 5; x++)
            {
                SpriteRenderer floor = new GameObject("Floor").AddComponent<SpriteRenderer>();
                floor.transform.SetParent(floorRoot, false);
                floor.transform.localPosition = new Vector3(x, y, 1f);
                floor.sprite = tile.sprite;
                floor.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Laboratory/LaboratoryUnlit.mat");
                floor.sortingOrder = -20;
            }
        Capture(camera, "Logs/BossHazardArtVerification-laboratory.png", false);

        // 어두운 배경의 실제 카메라 근접 화면으로 후광과 반사 고리를 별도 확인
        floorRoot.gameObject.SetActive(false);
        comparison.enabled = false;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f, 1f);
        camera.transform.localPosition = new Vector3(-0.25f, 2.5f, -10f);
        camera.orthographicSize = 1.5f;
        Capture(camera, "Logs/BossHazardArtVerification-dark-closeup.png", false);
    }

    /// <summary>기존 예고 프리팹을 같은 갤러리 바닥에서 크기·각도만 달리 배치</summary>
    private static void AddGalleryWarning(Transform parent, string path, Vector2 position, Vector2 scale, Color color, float angle = 90f)
    {
        // 순수 표시 컴포넌트의 기존 설정 API로만 실제 화면 비교 구성
        GameObject instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
        instance.transform.localPosition = position;
        instance.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        AreaWarningVfx visual = instance.GetComponent<AreaWarningVfx>();
        visual.Configure(visual.Shape, color, angle);
    }

    /// <summary>RenderTexture 실제 화면을 PNG로 저장하고 반투명 채움과 더 진한 테두리 확인</summary>
    private static void Capture(Camera camera, string path, bool inspect)
    {
        // 현재 렌더 대상 복원에 필요한 값과 새 캡처 버퍼 준비
        RenderTexture target = RenderTexture.GetTemporary(1024, 768, 24);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        Texture2D pixels = new Texture2D(1024, 768, TextureFormat.RGBA32, false);
        try
        {
            // Unity의 실제 렌더 결과를 추가 가공 없이 저장
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            if (inspect)
            {
                Color center = Sample(pixels, camera, camera.transform.position + new Vector3(-3.7f, 0.2f, 10f));
                Color edge = Sample(pixels, camera, camera.transform.position + new Vector3(-3.7f, 0.9f, 10f));
                Require(center.r > center.g + 0.025f && center.r > center.b + 0.025f && center.g > 0.5f && center.b > 0.5f,
                    "The warning interior must contain translucent red fill over the white background: " + center.ToString("F4"));
                Require(edge.r > edge.g + 0.08f && edge.r > edge.b + 0.08f, "The actual Unity render must contain the translucent red outline.");
                Require(edge.g < center.g - 0.025f, "The outline must be more opaque than its red interior.");
            }
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(pixels);
        }
    }

    /// <summary>지정한 월드 위치에 대응하는 실제 화면의 색 조회</summary>
    private static Color Sample(Texture2D pixels, Camera camera, Vector3 point)
    {
        // 월드 좌표를 이번 카메라의 실제 출력 픽셀에 대응
        Vector3 view = camera.WorldToViewportPoint(point);
        return pixels.GetPixel(Mathf.Clamp(Mathf.RoundToInt(view.x * pixels.width), 0, pixels.width - 1),
            Mathf.Clamp(Mathf.RoundToInt(view.y * pixels.height), 0, pixels.height - 1));
    }

    /// <summary>Player 위치와 물리 질의 위치를 같은 시점에 갱신</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        // 다음 실제 프레임 전에도 동일 위치로 물리 질의 가능하게 동기화
        body.position = position;
        body.transform.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>비활성 객체를 포함해 현재 Title 한 번 조회</summary>
    private static UI_Title GetTitle() => UnityEngine.Object.FindFirstObjectByType<UI_Title>(FindObjectsInactive.Include);

    /// <summary>실제 프레임을 진행하며 제한 시간 안에 조건 성립 대기</summary>
    private static IEnumerator WaitFor(Func<bool> condition, string reason)
    {
        // 실패 사유를 보존하며 조건 대기의 무한 반복 방지
        double end = EditorApplication.timeSinceStartup + 30d;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(reason);
            yield return null;
        }
    }

    /// <summary>게임 시간 경과를 기다려 Update 동작 확인</summary>
    private static IEnumerator WaitGame(float seconds)
    {
        // 게임 정지 중에는 완료되지 않는 경과 기준 사용
        double end = Time.timeAsDouble + seconds;
        while (Time.timeAsDouble < end) yield return null;
    }

    /// <summary>게임 정지와 독립된 Editor 시간 경과 대기</summary>
    private static IEnumerator WaitReal(float seconds)
    {
        // 게임 시간 정지 검사에는 독립된 실제 경과 기준 사용
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    /// <summary>조건 불성립을 즉시 실패로 기록할 예외 발생</summary>
    private static void Require(bool condition, string message)
    {
        // 실제 관측 결과가 조건을 위반한 경우에만 실패 발생
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>실제로 통과한 검사 항목만 결과에 추가</summary>
    private static void Passed(string message) => report.checks.Add(message);

    /// <summary>실제 결과를 저장하고 Play Mode 종료와 원래 Editor 복귀 요청</summary>
    private static void Finish(Exception exception)
    {
        // 검사 반복을 닫고 실제 성공 여부와 격리 저장 위치 기록
        EditorApplication.update -= Tick; // 종료 후 같은 검사가 반복 실행되지 않도록 구독 해제
        report.success = exception == null;
        report.error = exception?.ToString() ?? "";
        report.saveDirectory = SessionState.GetString(SaveKey, "");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BossHazardArtVerification-result.json", JsonUtility.ToJson(report, true));
        SessionState.SetBool(SuccessKey, report.success);
        SessionState.SetBool(FinishedKey, true);
        if (exception == null) Debug.Log("[BossHazardArtChecks] ALL PASSED (" + report.checks.Count + " checks)");
        else Debug.LogError("[BossHazardArtChecks] FAILED: " + exception);
        EditorApplication.ExitPlaymode();
    }

    /// <summary>실제 검사 항목과 실패 예외를 저장할 결과</summary>
    [Serializable]
    private class Report
    {
        public bool success; // 모든 검사가 예외 없이 끝났는지 저장할 결과
        public List<string> checks = new List<string>(); // 실제 통과한 검사 항목의 설명
        public string error; // 실패했을 때 실제 예외의 전체 내용
        public string saveDirectory; // 사용자 저장과 분리한 검사 세션의 저장 위치
    }
}
