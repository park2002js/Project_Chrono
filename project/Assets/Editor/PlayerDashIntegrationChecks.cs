using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Tilemaps;

/// <summary>원작 프리팹 대시의 감속·입력·회피 경계와 실제 바닥의 검 표시 검사</summary>
public static class PlayerDashIntegrationChecks
{
    /// <summary>격리본의 현행 프리팹으로 대시 표적 검사 시작</summary>
    public static void Run()
    {
        PlayerAttackIntegrationChecks.RunDash();
    }

    /// <summary>실제 Player Loop에서 대시의 다섯 책임과 바닥 위 표현 검사</summary>
    public static IEnumerator Check(GameManager manager, Action<string> passed)
    {
        // 검사 대상과 종료 때 복구할 사용자 상태 보관
        PlayerCharacter player = manager.CurrentPlayer;
        PlayerInputController input = PlayerInputController.Instance;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 originalPosition = body.position;
        float capture = Time.captureDeltaTime;
        float speed = player.SP;
        float hp = player.HP;
        bool enabled = input.enabled;
        bool blocked = input.BlockInput;
        bool spawnerEnabled = player.Spawner.enabled;
        Metrics metrics = new Metrics();
        Directory.CreateDirectory("Logs/DashIntegration");
        input.enabled = false;
        player.Spawner.enabled = false;
        player.ResetMotion();
        SetPosition(body, new Vector2(40f, 40f));
        try
        {
            // 원작 코드 기본값 대신 실제 Player 프리팹의 확정 튜닝 확인
            Require(Near(player.DashSpeed, 24f) && Near(player.DashDeceleration, 87f)
                && Near(player.DashDuration, 0.14f) && Near(player.DodgeDuration, 0.1f)
                && Near(player.InputBlockDuration, 0.14f) && Near(player.DashCooldown, 0.65f)
                && Near(player.basicSP, 4.4f), "Connected dash tuning must be24/87/.14/.1/.14 and preserve cooldown.65 and baseSP4.4.");
            yield return CheckDistance(player, body, metrics); // 세 프레임 간격의 실제 변위와 감속 표본 완료 대기
            passed("Dash24, deceleration87 and duration.14 integrate to2.5074 at30,60,144fps without changing cooldown.65");
            Time.captureDeltaTime = 1f / 240f;
            yield return CheckInput(player, input, body); // 실제 Space와 WASD 액션의 방향 고정·잠금 해제 검사 대기
            passed("Actual Space fires once per press, fixes move-priority direction, resumes held WASD after.14 and preserves.65 cooldown");
            yield return CheckDodge(player, body); // 회피와 대시 이동의 서로 다른 종료 경계 검사 대기
            passed("Hitbox damage is rejected during the first.1 seconds and accepted while dash movement remains until.14");
            yield return CheckPauseCancel(manager, player, input, body); // 정지·외부 차단의 남은 상태 보존과 정리 검사 대기
            passed("Pause preserves dash position and timers; input blocking cancels movement and Dodge while retaining cooldown");
            yield return CheckWall(player, body); // 원래 맵과 분리한 얇은 벽에서 관통 여부 검사 대기
            passed("Decelerating dash uses swept collision against an isolated thin wall and remains cancellable");
            player.ResetMotion();
            SetPosition(body, originalPosition);
            player.SP = speed;
            yield return CheckFloorRendering(manager, player, body, metrics); // 실제 실험실 바닥에서 검·검기의 정렬과 렌더 픽셀 검사 대기
            passed("Actual laboratory floor keeps idle and moving sword behind body20 at19, with attack VFX21 and visible rendered pixels");
        }
        finally
        {
            // 검사에서 바꾼 상태와 시간만 복구하고 관측 수치 별도 저장
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            player.InitializeHealth(hp);
            player.SP = speed;
            SetPosition(body, originalPosition);
            input.BlockInput = blocked;
            input.enabled = enabled;
            player.Spawner.enabled = spawnerEnabled;
            Time.captureDeltaTime = capture;
            File.WriteAllText("Logs/PlayerDashIntegration-metrics.json", JsonUtility.ToJson(metrics, true));
        }
    }

    /// <summary>실제 갱신 간격을 바꾸고 중간 감속과 전체 거리 비교</summary>
    private static IEnumerator CheckDistance(PlayerCharacter player, Rigidbody2D body, Metrics metrics)
    {
        // 일반 입력 이동을 제외하고 원작 대시의 면적만 관찰
        player.SP = 0f;
        foreach (int fps in new[] { 30, 60, 144 })
        {
            Time.captureDeltaTime = 1f / fps;
            yield return Frames(2); // 변경된 게임 프레임 간격이 Player Loop에 반영될 때까지 대기
            player.ResetMotion();
            SetPosition(body, new Vector2(40f, 40f));
            player.SetMoveInput(Vector2.right);
            Require(player.ApplyDash(), "Distance fixture must start a real dash.");
            while (player.IsDashing)
            {
                yield return Frames(1); // 실제 Character.Update와 Body.Move 한 프레임 적용 대기
                float elapsed = player.DashDuration - player.DashRemaining;
                float expected = 24f * elapsed - 43.5f * elapsed * elapsed;
                Require(Mathf.Abs(body.position.x - 40f - expected) < 0.003f && Mathf.Abs(body.position.y - 40f) < 0.001f,
                    "Dash deceleration trajectory differs at" + fps + "fps, t=" + elapsed + ", x=" + (body.position.x - 40f));
            }
            float distance = body.position.x - 40f;
            Require(Mathf.Abs(distance - 2.5074f) < 0.003f, "Complete dash distance differs at" + fps + "fps: " + distance);
            metrics.distanceSamples.Add(new DistanceSample { fps = fps, distance = distance });
            Require(!player.IsInputBlocked && !player.IsDodge && player.DashCooldownRemaining > 0f,
                "Dash end must release movement and Dodge while cooldown remains.");
        }
        player.ResetMotion();

        // 빠른 공격 뒤 남은 별도 전진이 새 대시에 더해지지 않는지 확인
        Time.captureDeltaTime = 1f / 240f;
        float attackSpeed = player.AS;
        try
        {
            player.SP = 4.4f;
            player.AS = 2f;
            player.SetAimPosition(body.position + Vector2.right * 3f);
            Require(player.ApplyAttack(), "AS2 residual movement fixture must start an attack.");
            yield return Until(() => !player.IsAttacking, "AS2 attack finishes before its fixed movement"); // 공격 모션 종료 뒤 별도.3초 전진이 남는 경계 대기
            SetPosition(body, new Vector2(40f, 40f));
            player.SetMoveInput(Vector2.right);
            Require(player.ApplyDash(), "A dash must be accepted after the AS2 attack ends.");
            player.SetMoveInput(Vector2.zero);
            yield return Until(() => !player.IsDashing, "dash over residual attack movement"); // 공격 잔여 전진과 겹친 대시의 전체 적용 대기
            metrics.afterFastAttackDistance = body.position.x - 40f;
            Require(Mathf.Abs(metrics.afterFastAttackDistance - 2.5074f) < 0.003f,
                "Residual AS2 attack movement must not add distance during dash: " + metrics.afterFastAttackDistance);
        }
        finally
        {
            player.AS = attackSpeed;
            player.SP = 0f;
            player.ResetMotion();
        }
    }

    /// <summary>실제 Space 콜백과 유지 중인 이동 키를 끝까지 전달</summary>
    private static IEnumerator CheckInput(PlayerCharacter player, PlayerInputController input, Rigidbody2D body)
    {
        // 기존 장치 연결을 보관하고 검사 전용 키보드와 마우스 사용
        PlayerInput actions = input.GetComponent<PlayerInput>();
        InputDevice[] previousDevices = new InputDevice[actions.devices.Count];
        for (int i = 0; i < previousDevices.Length; i++) previousDevices[i] = actions.devices[i];
        string previousScheme = actions.currentControlScheme;
        InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode editor = InputSystem.settings.editorInputBehaviorInPlayMode;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        InputAction dash = actions.actions.FindAction("Player/Dash", true);
        int performed = 0;
        Action<InputAction.CallbackContext> count = context => performed++;
        dash.performed += count; // 실제 Space 눌림 콜백 횟수 수집
        try
        {
            // 이동 우선 방향과 대시 도중 방향 고정·일반 입력 잠금 확인
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            actions.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            input.enabled = true;
            input.BlockInput = false;
            player.SP = 0f;
            player.ResetMotion();
            SetPosition(body, new Vector2(40f, 40f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = GameCamera.Instance.Camera.WorldToScreenPoint(body.position + Vector2.up * 3f) });
            Keys(keyboard, Key.D);
            yield return Until(() => player.MoveDirection == Vector2.right, "D movement intent"); // 실제 이동 액션의 오른쪽 의도 전달 대기
            Keys(keyboard, Key.D, Key.Space);
            yield return Until(() => player.IsDashing, "Space dash start"); // 실제 Dash 콜백이 Character까지 전달될 때까지 대기
            Require(performed == 1 && Vector2.Dot(player.DashDirection, Vector2.right) > 0.999f,
                "Space must dash once in movement direction instead of cursor direction.");
            Keys(keyboard, Key.W, Key.Space);
            yield return Until(() => input.MoveInput == Vector2.up, "W intent during dash"); // 대시 중 새 일반 이동 입력 수집 대기
            Require(player.IsDashing && Vector2.Dot(player.DashDirection, Vector2.right) > 0.999f && !player.ApplyAttack(),
                "Changing WASD must keep dash direction and reject attacking during dash.");
            yield return Until(() => !player.IsDashing, "dash release at.14"); // 대시와 입력 잠금이 같은 종료 경계에 도달할 때까지 대기
            Require(!player.IsInputBlocked && player.DashCooldownRemaining > 0f && !player.ApplyDash(),
                "Movement lock must end before the unchanged cooldown.");
            Require(player.ApplyAttack(), "Attack must be allowed immediately after dash movement lock ends.");
            player.CancelAttack();
            player.SP = 4.4f;
            Vector2 unlocked = body.position;
            yield return Frames(3); // 새 키 입력 없이 유지 중인 W가 이동을 재개하는지 관찰
            Require(body.position.y > unlocked.y + 0.01f, "Held W must resume movement after.14 without a new input event.");
            player.SP = 0f;
            yield return Until(() => player.DashCooldownRemaining == 0f, "unchanged cooldown end"); // 최초 시작부터.65초의 쿨타임 종료 대기
            yield return Frames(3); // Space 홀드가 대시를 자동 반복하지 않는지 관찰
            Require(performed == 1 && !player.IsDashing, "Held Space must not repeat when cooldown expires.");
            Keys(keyboard);
            yield return Frames(1); // 새로운 Space 눌림을 구분할 해제 프레임 대기
            Keys(keyboard, Key.Space);
            yield return Until(() => player.IsDashing, "second fresh Space press"); // 쿨타임 종료 후 새 Space 눌림 허용 대기
            Require(performed == 2, "A second fresh Space press must start exactly one new dash.");
        }
        finally
        {
            // 가상 장치·콜백·포커스 정책을 원래 입력 환경으로 복구
            Keys(keyboard);
            dash.performed -= count; // 실제 Space 눌림 횟수 수집 종료
            input.enabled = false;
            player.ResetMotion();
            if (!string.IsNullOrEmpty(previousScheme) && previousDevices.Length > 0)
                actions.SwitchCurrentControlScheme(previousScheme, previousDevices);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editor;
        }
    }

    /// <summary>Hitbox 경로로 초기 회피와 회피 만료 뒤의 실제 피해 확인</summary>
    private static IEnumerator CheckDodge(PlayerCharacter player, Rigidbody2D body)
    {
        // 실제 Player 피격 영역에서 대시 이동보다 짧은 회피 구간 확인
        player.ResetMotion();
        player.SP = 0f;
        SetPosition(body, new Vector2(40f, 40f));
        Hitbox hitbox = player.GetComponentInChildren<Hitbox>();
        Require(hitbox != null, "The connected Player must own an active hitbox.");
        player.SetMoveInput(Vector2.right);
        Require(player.ApplyDash(), "Dodge fixture must start a dash.");
        float hp = player.HP;
        Require(player.IsDodge && !hitbox.ReceiveHit(new DamageInstance(null, 1f, DamageType.Normal, DamageInstance.NextAttackId()))
            && player.HP == hp, "Initial Dodge must reject Hitbox damage without reducing HP.");
        yield return Until(() => !player.IsDodge, "Dodge expiration at.1"); // 회피 타이머가 먼저 만료되는 실제 게임 시각 대기
        Require(player.IsDashing && player.IsInputBlocked && player.DashRemaining <= 0.041f,
            "The.1..14 interval must retain movement and input lock after Dodge expires.");
        Require(hitbox.ReceiveHit(new DamageInstance(null, 1f, DamageType.Normal, DamageInstance.NextAttackId()))
            && Near(player.HP, hp - 1f), "Hitbox damage must apply once Dodge expires while dash still moves.");
        player.ResetMotion();
        player.InitializeHealth(hp);
    }

    /// <summary>시간 정지와 외부 입력 차단을 서로 다른 수명 경계로 검사</summary>
    private static IEnumerator CheckPauseCancel(GameManager manager, PlayerCharacter player, PlayerInputController input, Rigidbody2D body)
    {
        // 일시정지는 이동과 네 타이머 모두 같은 게임 시각에 보존
        player.ResetMotion();
        SetPosition(body, new Vector2(40f, 40f));
        player.SetMoveInput(Vector2.right);
        Require(player.ApplyDash(), "Pause fixture must begin a dash.");
        yield return Frames(2); // 실제로 시작된 감속 이동 뒤 시간 정지 대기
        manager.SetGameplayPaused(true);
        float remaining = player.DashRemaining;
        float dodge = player.DodgeRemaining;
        float cooldown = player.DashCooldownRemaining;
        Vector2 position = body.position;
        yield return Frames(4); // 게임 시간이 정지된 채 실제 렌더 프레임만 진행
        Require(player.DashRemaining == remaining && player.DodgeRemaining == dodge && player.DashCooldownRemaining == cooldown
            && Vector2.Distance(body.position, position) < 0.001f, "Pause must preserve dash timers and position.");
        manager.SetGameplayPaused(false);

        // 외부 입력 차단은 진행 중 대시만 취소하고 재사용 비용은 유지
        input.BlockInput = true;
        Require(!player.IsDashing && !player.IsDodge && !player.IsInputBlocked && player.DashCooldownRemaining > 0f,
            "External input block must cancel movement and Dodge without refunding cooldown.");
        input.BlockInput = false;
        player.ResetMotion();
    }

    /// <summary>높은 초기 속도에서도 얇은 벽의 앞에서 대시를 정지</summary>
    private static IEnumerator CheckWall(PlayerCharacter player, Rigidbody2D body)
    {
        // 원본 맵 충돌·조우에서 떨어진 검사 위치에 임시 벽 하나만 생성
        player.ResetMotion();
        SetPosition(body, new Vector2(40f, 40f));
        GameObject wallObject = new GameObject("DashVerificationWall");
        wallObject.transform.position = new Vector3(41.2f, 40f, 0f);
        BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(0.05f, 3f);
        Physics2D.SyncTransforms();
        try
        {
            player.SetMoveInput(Vector2.right);
            Require(player.ApplyDash(), "Wall fixture must begin a real dash.");
            yield return Until(() => !player.IsDashing, "thin wall dash end"); // 감속 대시의 전체 이동 구간 동안 충돌 보정 완료 대기
            Require(body.position.x > 40f && player.GetComponent<Collider2D>().bounds.max.x <= wall.bounds.min.x + 0.001f,
                "A decelerating dash must reach but never tunnel through its thin wall.");
            Require(!player.IsInputBlocked && player.DashCooldownRemaining > 0f, "Blocked travel must retain normal time and cooldown boundaries.");
        }
        finally
        {
            UnityEngine.Object.Destroy(wallObject);
            player.ResetMotion();
        }
    }

    /// <summary>실제 바닥을 유지한 채 몸체 기준 정렬과 렌더 기여 픽셀 확인</summary>
    private static IEnumerator CheckFloorRendering(GameManager manager, PlayerCharacter player, Rigidbody2D body, Metrics metrics)
    {
        // 생성 위치의 실제 바닥 Tilemap을 확인하고 정렬 기준 기록
        AttackController attack = player.GetComponent<AttackController>();
        SpriteRenderer appearance = player.GetComponent<CharacterRendering>().BodySprite;
        bool floor = false;
        foreach (TilemapRenderer renderer in manager.Stage.Map.GetComponentsInChildren<TilemapRenderer>())
        {
            Tilemap tiles = renderer.GetComponent<Tilemap>();
            if (!renderer.enabled || !tiles.HasTile(tiles.WorldToCell(player.transform.position))) continue;
            if (renderer.sortingLayerID != appearance.sortingLayerID || renderer.sortingOrder >= appearance.sortingOrder) continue;
            floor = true;
            metrics.floorOrders.Add(renderer.name + ":" + renderer.sortingOrder);
            Require(renderer.sortingOrder < appearance.sortingOrder - 1, "Floor tiles must render below the sword's back order.");
        }
        Require(floor && appearance.sortingOrder == 20, "The rendering fixture must remain on the actual laboratory floor at body order20.");
        player.SetAimPosition(body.position + Vector2.up * 3f);
        yield return Frames(2); // 위쪽 대기 조준이 실제 무기 뒤쪽 정렬에 적용될 때까지 대기
        Require(attack.WeaponRenderer.sortingLayerID == appearance.sortingLayerID && attack.WeaponRenderer.sortingOrder == 19,
            "Upward idle sword must use body20-1, not global order-1.");
        player.SetAimPosition(body.position + Vector2.down * 3f);
        yield return Frames(2); // 아래쪽 대기 조준이 실제 무기 앞쪽 정렬에 적용될 때까지 대기
        Require(attack.WeaponRenderer.sortingOrder == 19 && attack.AttackVfxRenderer.sortingOrder == 21,
            "Downward idle sword must stay behind body20 at19 while VFX stays21.");
        player.SetMoveInput(Vector2.right);
        yield return Frames(3); // 아래 조준을 유지한 일반 이동에서도 검의 몸체 뒤 정렬이 유지되는지 관찰
        player.SetMoveInput(Vector2.zero);
        Require(attack.WeaponRenderer.sortingOrder == 19, "A moving Player must retain its downward idle sword behind the body.");
        metrics.visibleSwordPixels = CaptureComparison(player, attack.WeaponRenderer, "sword-on-laboratory-floor");
        Require(metrics.visibleSwordPixels > 10, "The sword must contribute visible rendered pixels on the actual laboratory floor.");
        try
        {
            // 기존 절대 순서-1을 같은 포즈에만 재현해 바닥에 묻히는 원인 확인
            attack.WeaponRenderer.sortingOrder = -1;
            metrics.oldOrderSwordPixels = CaptureComparison(player, attack.WeaponRenderer, "sword-old-order-on-same-floor");
            Require(metrics.oldOrderSwordPixels < metrics.visibleSwordPixels,
                "The same sword pose at old global order-1 must lose visible pixels below the actual floor.");
        }
        finally { attack.WeaponRenderer.sortingOrder = 19; }

        // 실제 공격 중 검기가 바닥 위에 나타나는 장면도 같은 카메라로 확인
        player.SetAimPosition(body.position + Vector2.up * 3f);
        Require(player.ApplyAttack(), "Floor VFX fixture must begin a real attack.");
        yield return Until(() => attack.AttackVfxRenderer.enabled, "real floor attack VFX"); // 원작 이벤트 시점에 검기가 생성될 때까지 대기
        Require(attack.AttackVfxRenderer.sortingOrder == 21 && attack.WeaponRenderer.sortingOrder == 21,
            "A first swing's active sword and effect must render in front of body20 and floor.");
        metrics.visibleEffectPixels = CaptureComparison(player, attack.AttackVfxRenderer, "attack-on-laboratory-floor");
        Require(metrics.visibleEffectPixels > 50, "The attack effect must contribute visible rendered pixels above the laboratory floor.");
        yield return Until(() => !attack.IsAttacking, "floor attack combo wait"); // 공격 곡선이 끝난 직후 콤보 대기 자세에 들어갈 때까지 대기
        Require(attack.ComboRemaining > 0f && attack.WeaponRenderer.sortingOrder == 19,
            "A finished first attack must immediately restore behind-body order19 during combo wait.");
        player.CancelAttack();
    }

    /// <summary>같은 실제 바닥 장면에서 대상 Renderer 유무만 바꿔 가려짐 판별</summary>
    private static int CaptureComparison(PlayerCharacter player, SpriteRenderer renderer, string name)
    {
        // 실제 게임 카메라를 잠시 가까이 두고 바닥·조명을 그대로 렌더
        Camera camera = GameCamera.Instance.Camera;
        RenderTexture texture = RenderTexture.GetTemporary(512, 512, 24);
        RenderTexture active = RenderTexture.active;
        RenderTexture target = camera.targetTexture;
        Vector3 position = camera.transform.position;
        float size = camera.orthographicSize;
        Texture2D pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = player.transform.position + new Vector3(0f, 0.75f, -10f);
            camera.orthographicSize = 2.6f;
            camera.targetTexture = texture;
            Render(texture, camera, pixels);
            Render(texture, camera, pixels);
            Color32[] shown = pixels.GetPixels32();
            File.WriteAllBytes("Logs/DashIntegration/" + name + ".png", pixels.EncodeToPNG());
            renderer.enabled = false;
            Render(texture, camera, pixels);
            Color32[] hidden = pixels.GetPixels32();
            int changed = 0;
            for (int i = 0; i < shown.Length; i++)
                if (!shown[i].Equals(hidden[i])) changed++;
            return changed;
        }
        finally
        {
            // 기존 카메라와 Renderer를 같은 프레임의 상태로 복구
            renderer.enabled = true;
            camera.targetTexture = target;
            camera.transform.position = position;
            camera.orthographicSize = size;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    /// <summary>GPU 준비를 포함해 동일 렌더 타깃의 최종 픽셀 동기화</summary>
    private static void Render(RenderTexture texture, Camera camera, Texture2D pixels)
    {
        camera.Render();
        RenderTexture.active = texture;
        pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
        pixels.Apply();
    }

    /// <summary>가상 키보드의 눌림·해제를 실제 Input System에 전달</summary>
    private static void Keys(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }

    /// <summary>실제 Player Loop의 지정 프레임 진행 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "Player frames"); // 렌더 프레임과 실제 게임 Update 진행 대기
    }

    /// <summary>제한 시각 안에 실제 런타임 조건이 충족되는지 관찰</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double end = EditorApplication.timeSinceStartup + 20d;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Dash verification timed out: " + description);
            yield return null;
        }
    }

    /// <summary>몸체와 Transform의 검사 위치를 같은 시점에 맞춤</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.position = position;
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        Physics2D.SyncTransforms();
    }

    /// <summary>부동소수점의 작은 표현 차이만 허용</summary>
    private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < 0.0001f;

    /// <summary>관측한 계약 불일치를 공통 실행기의 실패 결과로 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>실제 실행에서 관측한 거리와 바닥 렌더 결과</summary>
    [Serializable]
    private sealed class Metrics
    {
        public List<DistanceSample> distanceSamples = new List<DistanceSample>(); // 서로 다른 게임 프레임 간격에서 관측한 전체 대시 거리
        public List<string> floorOrders = new List<string>(); // 실제 생성 위치 아래 Tilemap의 전역 정렬 순서
        public int visibleSwordPixels; // 바닥 위에서 검을 숨겼을 때 달라지는 렌더 픽셀 수
        public int oldOrderSwordPixels; // 같은 포즈를 이전 절대 순서-1에 두었을 때 남는 검 픽셀 수
        public int visibleEffectPixels; // 바닥 위에서 검기를 숨겼을 때 달라지는 렌더 픽셀 수
        public float afterFastAttackDistance; // AS2 공격의 잔여 전진과 겹친 대시에서 실제로 이동한 거리
    }

    /// <summary>한 프레임 간격에서 측정한 전체 대시 이동</summary>
    [Serializable]
    private sealed class DistanceSample
    {
        public int fps; // 검사에 적용한 게임 시간의 프레임 간격 기준
        public float distance; // 실제 몸체 위치로 관측한 전체 대시 거리
    }
}
