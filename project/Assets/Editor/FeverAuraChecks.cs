using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>실제 각성 상태의 황금 오라·정지·Draw·생명주기와 현재 맵 화면 확인</summary>
public static class FeverAuraChecks
{
    private const string CaptureFolder = "Logs/FeverAura"; // 현재 맵의 정지·이동·Draw 화면을 보관할 격리 출력 폴더
    private const int Width = 768; // 오라 검토용 실제 월드 카메라 화면의 가로 픽셀
    private const int Height = 432; // 오라 검토용 실제 월드 카메라 화면의 세로 픽셀

    /// <summary>기존 Player 프리팹에 오라만 연결한 뒤 좁은 실제 Play 검사 시작</summary>
    public static void ApplyAndRun()
    {
        FeverAuraSetup.Apply();
        PlayerAttackIntegrationChecks.RunFever();
    }

    /// <summary>기존 Buff 네 묶음과 각성 오라 다섯 경계를 같은 세션에서 검사</summary>
    public static IEnumerator Run(GameManager manager, Action<string> passed)
    {
        // 씬 수명 대상과 검사 뒤 복구할 수치·위치·시간 한 번 확보
        PlayerCharacter player = manager.CurrentPlayer;
        CharacterBuff buffs = player.Buffs;
        FeverAuraFx aura = player.GetComponent<FeverAuraFx>();
        PlayerInputController input = PlayerInputController.Instance;
        CharacterRendering appearance = player.GetComponent<CharacterRendering>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Camera camera = GameCamera.Instance.Camera;
        Vector2 position = body.position;
        Vector2 aim = player.AimWorldPosition;
        float duration = buffs.FeverDuration;
        float health = player.HP;
        float capture = Time.captureDeltaTime;
        bool inputEnabled = input.enabled;
        bool blocked = input.BlockInput;
        bool spawning = player.Spawner.enabled;
        bool paused = manager.IsGameplayPaused;
        AuraReport evidence = new AuraReport();
        Action<bool> changed = active => evidence.events.Add(new FeverEvent
        { active = active, frame = Time.frameCount, gameTime = Time.timeAsDouble, attack = player.AD, speed = player.SP });
        Require(aura != null && aura.Emitters != null && aura.Emitters.Length == 2, "The Player must connect its two native Fever aura emitters.");
        ParticleSystem[] emitters = (ParticleSystem[])aura.Emitters.Clone();
        Directory.CreateDirectory(CaptureFolder);
        input.enabled = false;
        input.BlockInput = false;
        player.Spawner.enabled = false;
        Time.captureDeltaTime = 1f / 60f;
        try
        {
            // 기존 Buff의 수치·예약·제거·생명주기 계약을 그대로 재사용
            yield return PlayerBuffChecks.Run(manager, input, passed); // 기존 효과 네 묶음의 실제 적용·복구 완료 대기
            buffs.OnFeverChanged += changed; // 각성 전환 시점과 수치 복원 순서 기록
            player.ResetMotion();
            CheckConfiguration(aura, appearance);
            yield return CheckTransitions(player, buffs, aura, evidence); // 다음 프레임 적용·재획득·제거·자연 만료 검사 대기
            passed("Fever transition events apply next frame, refresh duration without restarting aura, and clear particles on removal or expiry");
            yield return CheckPauseAndDraw(manager, player, buffs, aura, appearance); // scaled 입자 정지와 Draw의 상대 정렬·복구 검사 대기
            passed("Pause and Draw freeze scaled aura particles while Draw raises both emitters above shade and restores their body-relative order");
            yield return CheckReactivation(player, buffs, aura); // 표시·Buff·Player의 독립 활성화 수명 검사 대기
            passed("Aura and Player reactivation resynchronize existing Fever with one subscription while disabling Buff alone preserves its active state");
            yield return CheckDeath(player, buffs, aura); // 실제 사망·부활과 예약 제거 검사 대기
            passed("Actual Player death and revival remove active and queued Fever aura and allow a clean new acquisition");
            SetPosition(body, position);
            yield return CaptureGameplay(manager, player, buffs, aura, camera, evidence); // 현재 맵 바닥의 정지·이동과 황금 픽셀 실제 촬영 대기
            for (int i = 0; i < emitters.Length; i++) Require(aura.Emitters[i] == emitters[i], "Aura cycles must reuse the original native emitter references.");
            passed("Current laboratory floor renders golden aura pixels and stationary/moving sequences using the same cached local emitters");
        }
        finally
        {
            // 이번 검사에서 바꾼 자기 상태만 닫고 원래 세션 수치·위치 복구
            buffs.OnFeverChanged -= changed; // 검사 이후 각성 전환 기록 구독 해제
            manager.SetGameplayPaused(false);
            player.CancelDraw();
            buffs.enabled = true;
            aura.enabled = true;
            if (!player.gameObject.activeSelf) player.gameObject.SetActive(true);
            buffs.Clear();
            buffs.FeverDuration = duration;
            player.InitializeHealth(health);
            player.ResetMotion();
            player.Skills.ResetSkills();
            SetPosition(body, position);
            player.SetAimPosition(aim);
            player.Spawner.enabled = spawning;
            input.BlockInput = blocked;
            input.enabled = inputEnabled;
            Time.captureDeltaTime = capture;
            manager.SetGameplayPaused(paused);
            File.WriteAllText("Logs/FeverAura-metrics.json", JsonUtility.ToJson(evidence, true));
        }
    }

    /// <summary>실제 프리팹의 로컬·scaled 파티클과 몸체 앞뒤 정렬 확인</summary>
    private static void CheckConfiguration(FeverAuraFx aura, CharacterRendering appearance)
    {
        foreach (ParticleSystem emitter in aura.Emitters)
        {
            ParticleSystem.MainModule main = emitter.main;
            ParticleSystemRenderer renderer = emitter.GetComponent<ParticleSystemRenderer>();
            Require(main.loop && !main.playOnAwake && !main.useUnscaledTime && main.simulationSpace == ParticleSystemSimulationSpace.Local
                && renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported
                && renderer.sortingLayerID == appearance.BodySprite.sortingLayerID, "Aura must use connected local, scaled looping particles on the body sorting layer.");
        }
        Require(aura.Emitters[0].GetComponent<ParticleSystemRenderer>().sortingOrder < appearance.BodySprite.sortingOrder
            && aura.Emitters[1].GetComponent<ParticleSystemRenderer>().sortingOrder > appearance.BodySprite.sortingOrder,
            "Flames must render behind the body and embers in front.");
        Require(SubscriberCount(aura) == 1, "An active aura must subscribe to its own Buff once.");
    }

    /// <summary>각성 전환과 재획득의 수명·수치·재생 시작 경계 확인</summary>
    private static IEnumerator CheckTransitions(PlayerCharacter player, CharacterBuff buffs, FeverAuraFx aura, AuraReport evidence)
    {
        // 요청 프레임에는 기존 표시를 유지하고 다음 프레임 전환에서만 시작
        buffs.Clear();
        buffs.FeverDuration = 0.65f;
        yield return Frames(1); // 같은 프레임 Remove 우선 경계 통과
        int start = evidence.events.Count;
        Require(buffs.ApplyFever() && !buffs.IsFever && !aura.IsPlaying, "Fever request must remain queued and keep the aura off in its request frame.");
        yield return Until(() => buffs.IsFever && aura.IsPlaying, "initial aura start"); // 실제 다음 프레임 Buff 전환과 오라 시작 대기
        Require(evidence.events.Count == start + 1 && evidence.events[start].active
            && Mathf.Approximately(evidence.events[start].attack, player.basicAD * buffs.AttackMultiplier), "One enter event must observe already-applied Fever stats.");
        yield return GameSeconds(0.12f); // 재획득 전 실제 파티클 재생 시간과 기간 일부 진행
        float before = buffs.FeverRemaining;
        float particleTime = aura.Emitters[0].time;
        Require(buffs.ApplyFever(), "An active Fever must accept duration refresh.");
        yield return Until(() => buffs.FeverRemaining > before, "Fever duration refresh"); // 다음 프레임의 기간 갱신 완료 대기
        Require(evidence.events.Count == start + 1 && aura.Emitters[0].time >= particleTime,
            "Refreshing duration must not emit a duplicate enter event or restart the native particle system.");
        buffs.Remove(BuffType.Fever);
        Require(!aura.IsPlaying && Cleared(aura) && evidence.events.Count == start + 2 && !evidence.events[start + 1].active
            && Mathf.Approximately(evidence.events[start + 1].attack, player.basicAD), "Removal must restore stats before one exit event and clear all particles immediately.");

        // 같은 프레임 제거는 예약을 취소하고 짧은 실제 수명도 종료 알림 한 번만 발생
        yield return Frames(1); // 제거 우선 프레임 이후 새 예약 준비
        Require(buffs.ApplyFever(), "A fresh request must be accepted after removal.");
        buffs.Remove(BuffType.Fever);
        Require(!buffs.ApplyFever(), "Same-frame Remove must win over reacquisition.");
        yield return Frames(2); // 취소된 예약이 늦게 오라를 되살리지 않는지 관찰
        Require(!buffs.IsFever && !aura.IsPlaying && evidence.events.Count == start + 2, "Cancelled pending Fever must not emit a late enter event.");
        buffs.FeverDuration = 0.22f;
        Require(buffs.ApplyFever(), "A later acquisition must begin the natural-expiry fixture.");
        yield return Until(() => aura.IsPlaying, "natural-expiry aura entry"); // 짧은 각성의 실제 표시 시작 대기
        yield return Until(() => !buffs.IsFever, "natural-expiry aura exit"); // 실제 scaled 수명 만료 대기
        Require(!aura.IsPlaying && Cleared(aura) && evidence.events.Count == start + 4 && !evidence.events[start + 3].active,
            "Natural expiration must emit one exit and leave no live aura particles.");
    }

    /// <summary>실제 정지·Draw 암전에서 native 입자의 시간과 상대 표시 순서 확인</summary>
    private static IEnumerator CheckPauseAndDraw(GameManager manager, PlayerCharacter player, CharacterBuff buffs, FeverAuraFx aura, CharacterRendering appearance)
    {
        yield return BeginFever(buffs, aura, 8f); // 충분한 기간의 실제 각성과 입자 생성 준비 대기
        yield return GameSeconds(0.25f); // native 방출로 실제 입자가 생성될 때까지 대기
        Require(ParticleCount(aura) > 0, "A running aura must emit actual native particles.");
        manager.SetGameplayPaused(true);
        float remaining = buffs.FeverRemaining;
        float phase = aura.Emitters[0].time;
        yield return RealSeconds(0.08f); // 실제 시간만 지나도 scaled 입자와 각성 기간이 동결되는지 관찰
        Require(buffs.FeverRemaining == remaining && Mathf.Abs(aura.Emitters[0].time - phase) < 0.00001f && aura.IsPlaying,
            "Gameplay pause must freeze both Fever duration and native particle simulation.");
        manager.SetGameplayPaused(false);

        // Draw의 실제 진입 API가 몸체와 파티클을 같은 정렬 차이만큼 올리는지 확인
        player.ResetMotion();
        player.Skills.ResetSkills();
        ParticleSystemRenderer rear = aura.Emitters[0].GetComponent<ParticleSystemRenderer>();
        ParticleSystemRenderer front = aura.Emitters[1].GetComponent<ParticleSystemRenderer>();
        int rearOrder = rear.sortingOrder;
        int frontOrder = front.sortingOrder;
        int bodyOrder = appearance.BodySprite.sortingOrder;
        Require(player.ApplyDraw(), "The live Player must enter Draw for aura highlight verification.");
        int offset = appearance.BodySprite.sortingOrder - bodyOrder;
        UI_Draw view = UIController.Instance.GetComponent<UI_Draw>();
        Require(offset > 0 && rear.sortingOrder == rearOrder + offset && front.sortingOrder == frontOrder + offset
            && view.ScreenShade != null && rear.sortingOrder > view.ScreenShade.sortingOrder && Time.timeScale == 0f,
            "Draw must preserve aura rear/front offsets above its actual shade sprite.");
        phase = aura.Emitters[0].time;
        remaining = buffs.FeverRemaining;
        yield return Frames(4); // Draw 중 입자·기간 정지와 정렬 유지 관찰
        Require(aura.IsPlaying && buffs.FeverRemaining == remaining && Mathf.Abs(aura.Emitters[0].time - phase) < 0.00001f,
            "Draw must keep the active aura frozen rather than resetting it.");
        CaptureFrame(GameCamera.Instance.Camera, "draw-aura.png");
        player.CancelDraw();
        Require(!manager.IsDrawPaused && rear.sortingOrder == rearOrder && front.sortingOrder == frontOrder
            && appearance.BodySprite.sortingOrder == bodyOrder && aura.IsPlaying, "Draw exit must restore all native aura sorting without ending Fever.");
    }

    /// <summary>표시·Buff·Player의 독립 활성화와 구독 수명 확인</summary>
    private static IEnumerator CheckReactivation(PlayerCharacter player, CharacterBuff buffs, FeverAuraFx aura)
    {
        float remaining = buffs.FeverRemaining;
        buffs.enabled = false;
        yield return Frames(3); // Buff 단독 비활성화는 상태 원본을 지우지 않는 기존 정책 관찰
        Require(buffs.IsFever && buffs.FeverRemaining == remaining && aura.IsPlaying, "Disabling Buff alone must preserve its existing Fever and aura state.");
        buffs.enabled = true;
        aura.enabled = false;
        Require(!aura.IsPlaying && Cleared(aura) && SubscriberCount(aura) == 0 && buffs.IsFever, "Disabling aura must clear visuals and unsubscribe without removing Fever.");
        aura.enabled = true;
        Require(aura.IsPlaying && SubscriberCount(aura) == 1, "Re-enabling aura must synchronize existing Fever and subscribe once.");
        player.gameObject.SetActive(false);
        Require(!aura.IsPlaying && Cleared(aura) && SubscriberCount(aura) == 0 && buffs.IsFever, "Player disable must clear aura visuals while preserving the existing Buff state.");
        player.gameObject.SetActive(true);
        yield return Frames(1); // 같은 Player의 재활성과 기존 각성 동기화 완료 대기
        Require(aura.IsPlaying && buffs.IsFever && SubscriberCount(aura) == 1, "Player reactivation must resume retained Fever without duplicate subscriptions.");
        aura.enabled = false;
        buffs.Remove(BuffType.Fever);
        aura.enabled = true;
        Require(!aura.IsPlaying && Cleared(aura) && SubscriberCount(aura) == 1, "Reactivation after a missed removal must remain off and keep one subscription.");
    }

    /// <summary>실제 치명 피해·부활과 미처리 예약이 표시를 남기지 않는지 확인</summary>
    private static IEnumerator CheckDeath(PlayerCharacter player, CharacterBuff buffs, FeverAuraFx aura)
    {
        yield return BeginFever(buffs, aura, 8f); // 사망할 실제 Player의 각성 준비 대기
        Require(player.ApplyDamage(new DamageInstance(null, player.maxHP + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false)), "Actual lethal effect damage must reach the Player.");
        Require(player.IsDead && !buffs.IsFever && !aura.IsPlaying && Cleared(aura), "Actual death must immediately remove Fever aura and particles.");
        Require(player.Revive(), "The same Player must revive for aura lifecycle verification.");
        yield return Frames(1); // 부활의 제거 우선 프레임 통과
        Require(!aura.IsPlaying && !buffs.IsFever, "Revival must not restore the old aura.");
        Require(buffs.ApplyFever(), "A revived Player must accept a fresh queued acquisition.");
        Require(player.ApplyDamage(new DamageInstance(null, player.maxHP + 1f, DamageType.Effect, DamageInstance.NextAttackId(), false)), "A second lethal damage must clear the queued acquisition.");
        Require(player.Revive(), "The queued-death fixture must revive the same Player.");
        yield return Frames(2); // 사망 전 예약이 부활 뒤 늦게 재생되지 않는지 관찰
        Require(!buffs.IsFever && !aura.IsPlaying && Cleared(aura), "A queued acquisition before death must not resurrect aura after revival.");
        yield return BeginFever(buffs, aura, 8f); // 부활 이후 새 각성의 정상 복구 확인 대기
    }

    /// <summary>현재 게임 바닥의 황금 렌더 기여와 실제 정지·이동 프레임 촬영</summary>
    private static IEnumerator CaptureGameplay(GameManager manager, PlayerCharacter player, CharacterBuff buffs, FeverAuraFx aura, Camera camera, AuraReport evidence)
    {
        float zoom = camera.orthographicSize;
        try
        {
        // 촬영 줌을 먼저 적용해 실제 카메라가 그 줌의 맵 경계에 접근할 시간 확보
        camera.orthographicSize = 3.2f;
        // 실제 게임 시간과 unscaled 카메라 시간이 같은 흐름으로 움직이도록 캡처 고정 시간 해제
        Time.captureDeltaTime = 0f;
        yield return Frames(2); // 실제 시간 기반 이동·카메라·입자 갱신 준비 대기
        player.SetMoveInput(Vector2.zero);
        player.SetAimPosition((Vector2)player.transform.position + Vector2.down * 3f);
        yield return BeginFever(buffs, aura, 8f); // 촬영 전체를 유지할 실제 각성 시작 대기
        yield return GameSeconds(0.45f); // 현재 게임 시각으로 불길과 불씨 방출이 자리잡을 때까지 대기
        bool floor = false;
        foreach (Tilemap tilemap in manager.Stage.Map.GetComponentsInChildren<Tilemap>())
            if (tilemap.HasTile(tilemap.WorldToCell(player.transform.position))) floor = true;
        Require(floor, "Aura capture must remain above a tile in the actual current map.");
        evidence.floorPosition = player.transform.position;
        evidence.goldenPixels = CountGoldenContribution(camera, aura);
        Require(evidence.goldenPixels > 10, "The active native aura must contribute visible golden pixels on the current laboratory floor.");
        yield return CaptureSequence(camera, player, evidence.idle, false, 30); // 현재 바닥의2초 정지 시퀀스 실제 촬영 대기
        yield return CaptureSequence(camera, player, evidence.moving, true, 45); // 안전한 복도에서3초 이동 시퀀스 실제 촬영 대기
        player.SetMoveInput(Vector2.zero);
        Require(aura.IsPlaying && !manager.Stage.IsBossSequencePlaying, "Gameplay capture must retain Fever without triggering an unrelated boss sequence.");
        }
        finally { camera.orthographicSize = zoom; }
    }

    /// <summary>게임 시간에15회씩 현재 월드 카메라를 촬영하고 실제 시각·이동 위치 기록</summary>
    private static IEnumerator CaptureSequence(Camera camera, PlayerCharacter player, List<CaptureSample> samples, bool moving, int count)
    {
        double began = Time.timeAsDouble;
        for (int frame = 0; frame < count; frame++)
        {
            Vector2 direction = moving ? (frame / 15 % 2 == 0 ? Vector2.up : Vector2.down) : Vector2.zero;
            player.SetMoveInput(direction);
            if (moving) player.SetAimPosition((Vector2)player.transform.position + direction * 3f);
            double at = began + frame / 15d;
            yield return Until(() => Time.timeAsDouble >= at, "aura gameplay capture"); // 실제 게임 시각의 다음15fps 촬영 표본까지 대기
            string file = (moving ? "moving-" : "idle-") + frame.ToString("000") + ".png";
            CaptureFrame(camera, file);
            samples.Add(new CaptureSample { file = file, gameTime = Time.timeAsDouble - began, position = player.transform.position });
        }
        player.SetMoveInput(Vector2.zero);
    }

    /// <summary>같은 프레임에서 오라 Renderer만 숨겨 실제 황금 픽셀 기여 비교</summary>
    private static int CountGoldenContribution(Camera camera, FeverAuraFx aura)
    {
        Color32[] shown = CaptureFrame(camera, "aura-on.png");
        ParticleSystemRenderer[] renderers = new ParticleSystemRenderer[aura.Emitters.Length];
        for (int i = 0; i < renderers.Length; i++) { renderers[i] = aura.Emitters[i].GetComponent<ParticleSystemRenderer>(); renderers[i].enabled = false; }
        Color32[] hidden;
        try { hidden = CaptureFrame(camera, "aura-hidden.png"); }
        finally { foreach (ParticleSystemRenderer renderer in renderers) renderer.enabled = true; }
        int count = 0;
        for (int i = 0; i < shown.Length; i++)
        {
            int difference = Math.Abs(shown[i].r - hidden[i].r) + Math.Abs(shown[i].g - hidden[i].g) + Math.Abs(shown[i].b - hidden[i].b);
            if (difference > 20 && shown[i].r > shown[i].b + 20 && shown[i].g > shown[i].b + 10) count++;
        }
        return count;
    }

    /// <summary>실제 바닥·조명과 현재 카메라 줌을 유지해 월드 화면 저장</summary>
    private static Color32[] CaptureFrame(Camera camera, string file)
    {
        RenderTexture texture = RenderTexture.GetTemporary(Width, Height, 24);
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        Texture2D pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture;
            for (int pass = 0; pass < 2; pass++)
            {
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
            }
            File.WriteAllBytes(Path.Combine(CaptureFolder, file), pixels.EncodeToPNG());
            return pixels.GetPixels32();
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    /// <summary>기존 제거 프레임을 지난 뒤 새 각성의 실제 표시 시작 대기</summary>
    private static IEnumerator BeginFever(CharacterBuff buffs, FeverAuraFx aura, float duration)
    {
        buffs.Clear();
        buffs.FeverDuration = duration;
        yield return Frames(1); // 같은 프레임 제거 우선 경계를 지난 새 획득 준비
        Require(buffs.ApplyFever(), "Fever fixture must accept a fresh acquisition.");
        yield return Until(() => buffs.IsFever && aura.IsPlaying, "fresh Fever aura"); // 실제 Buff 전환 사건이 오라에 반영될 때까지 대기
    }

    /// <summary>오라가 자기 Buff에 연결한 실제 구독 개수 조회</summary>
    private static int SubscriberCount(FeverAuraFx aura)
    {
        CharacterBuff buff = aura.GetComponent<CharacterBuff>();
        FieldInfo field = typeof(CharacterBuff).GetField("OnFeverChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate handlers = field.GetValue(buff) as Delegate;
        int count = 0;
        if (handlers != null) foreach (Delegate handler in handlers.GetInvocationList()) if (ReferenceEquals(handler.Target, aura)) count++;
        return count;
    }

    /// <summary>자기 native 입자들의 현재 전체 수 집계</summary>
    private static int ParticleCount(FeverAuraFx aura)
    {
        int count = 0;
        foreach (ParticleSystem emitter in aura.Emitters) count += emitter.particleCount;
        return count;
    }

    /// <summary>정지·제거 상태가 살아있는 입자를 남기지 않는지 확인</summary>
    private static bool Cleared(FeverAuraFx aura)
    {
        foreach (ParticleSystem emitter in aura.Emitters) if (emitter.isPlaying || emitter.particleCount != 0) return false;
        return true;
    }

    /// <summary>검사 후 물리 몸체와 Transform을 같은 위치로 복구</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.position = position;
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        Physics2D.SyncTransforms();
    }

    /// <summary>실제 Player Loop의 프레임 경계 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "Fever frames"); // 실제 프레임 수가 진행될 때까지 대기
    }

    /// <summary>효과와 native 입자가 사용하는 게임 시간 진행 대기</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        yield return Until(() => Time.timeAsDouble >= end, "Fever game time"); // scaled 게임 시각의 지정 구간 대기
    }

    /// <summary>게임 정지 중 실제 시간만 진행해 동결 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        double end = Time.realtimeSinceStartupAsDouble + seconds;
        yield return Until(() => Time.realtimeSinceStartupAsDouble >= end, "Fever real time"); // 정지 상태의 실제 시간 표본 대기
    }

    /// <summary>대기 중 게임을 계속 진행하고 무한 대기만 차단</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double end = Time.realtimeSinceStartupAsDouble + 20d;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException("Fever aura check timed out: " + description);
            yield return null;
        }
    }

    /// <summary>실제 관측한 계약 불일치를 공통 검사 결과로 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>상태 전환과 현재 맵 촬영의 실제 기록</summary>
    [Serializable]
    private class AuraReport
    {
        public List<FeverEvent> events = new List<FeverEvent>(); // 실제 각성 전환 사건의 상태·수치·시각
        public Vector3 floorPosition; // 현재 실험실 바닥에서 촬영을 시작한 Player 위치
        public int goldenPixels; // 오라 Renderer 유무 비교에서 확인한 실제 황금 픽셀 수
        public List<CaptureSample> idle = new List<CaptureSample>(); //2초 정지 화면의 실제 게임 시각·좌표
        public List<CaptureSample> moving = new List<CaptureSample>(); //3초 이동 화면의 실제 게임 시각·좌표
    }

    /// <summary>한 번의 실제 각성 진입 또는 해제 사건</summary>
    [Serializable]
    private class FeverEvent
    {
        public bool active; // 알림 시점의 각성 활성 여부
        public int frame; // 다음 프레임 적용과 제거 우선을 비교할 실제 프레임 번호
        public double gameTime; // 전환 알림을 받은 게임 시각
        public float attack; // 알림 전에 이미 합성·복원된 실제 공격력
        public float speed; // 알림 전에 이미 합성·복원된 실제 이동속도
    }

    /// <summary>실제 월드 카메라 한 프레임의 시간과 Player 위치</summary>
    [Serializable]
    private class CaptureSample
    {
        public string file; // 저장한 PNG 프레임 이름
        public double gameTime; // 시퀀스 시작부터 실제로 흐른 게임 시간
        public Vector3 position; // 해당 화면을 촬영한 실제 Player 월드 위치
    }
}
