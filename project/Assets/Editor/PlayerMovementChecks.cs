using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

/// <summary>별도 입력 장치로 실제 Update·즉시 이동·Collider·Sprite 흐름을 검사</summary>
public static class PlayerMovementChecks
{
    /// <summary>초기화된 MainWorld에서 F-P01을 검사하고 입력·위치·수치를 원래 상태로 복구</summary>
    public static IEnumerator Run(GameManager manager, Action<string> passed)
    {
        // 실제 씬 연결을 확인하고 검사 후 복원할 장치·바인딩 상태 보관
        PlayerCharacter player = manager.CurrentPlayer;
        PlayerSpawner lifecycle = player.Spawner;
        bool lifecycleEnabled = lifecycle.enabled;
        lifecycle.enabled = false;
        EncounterTrigger encounterScript = manager.Stage.Map.GetEncounterArea(GameManager.SubmittedBossId).GetComponent<EncounterTrigger>();
        bool encounterEnabled = encounterScript != null && encounterScript.enabled;
        if (encounterScript != null) encounterScript.enabled = false;
        GameObject entryDoor = manager.Stage.Map.GetEntryDoor(GameManager.SubmittedBossId);
        bool doorActive = entryDoor != null && entryDoor.activeSelf;
        MapDoor entryMotion = entryDoor != null ? entryDoor.GetComponent<MapDoor>() : null;
        bool doorWantedOpen = entryMotion != null && entryMotion.WantsOpen;
        if (entryMotion != null) entryMotion.SetOpen(true, true);
        else if (entryDoor != null) entryDoor.SetActive(false);
        PlayerInputController input = PlayerInputController.Instance;
        Require(input != null && GameCamera.Instance != null, "MainWorld must register input and camera before Player checks begin.");
        PlayerInput actions = input.GetComponent<PlayerInput>();
        Require(actions != null && actions.actions != null, "PlayerInput and its action asset must be connected.");
        InputAction moveAction = actions.actions.FindAction("Player/Move", true);
        InputDevice[] previousDevices = new InputDevice[actions.devices.Count];
        for (int i = 0; i < previousDevices.Length; i++) previousDevices[i] = actions.devices[i];
        string previousScheme = actions.currentControlScheme;
        int canceledCount = 0;
        int performedCount = 0;
        Vector2 lastPerformedValue = Vector2.zero;
        Action<InputAction.CallbackContext> onCanceled = context => canceledCount++;
        Action<InputAction.CallbackContext> onPerformed = context =>
        {
            performedCount++;
            lastPerformedValue = context.ReadValue<Vector2>();
        };
        moveAction.canceled += onCanceled; // 액션 해제 콜백 횟수 수집
        moveAction.performed += onPerformed; // 액션 실행 횟수와 마지막 입력값 수집
        int reboundIndex = -1;
        string previousOverride = null;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        CharacterRendering rendering = player.GetComponent<CharacterRendering>();
        AttackController attack = player.GetComponent<AttackController>();
        Camera camera = GameCamera.Instance.Camera;
        Vector2 spawn = body.position;
        float speed = player.SP;
        InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;

        // Editor 포커스와 무관한 검사 전용 입력 장치 준비
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        GameObject testWall = null;
        try
        {
            // 검사 장치 연결 뒤 생성된 Player와 공유 입력·표시 연결 확인
            actions.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            Require(input.Player == player && camera != null && GameCamera.Instance.Target == player.transform, "Spawner must bind the Player through registered input and camera instances.");
            Require(player.GetComponent<PlayerInputController>() == null && player.GetComponent<PlayerInput>() == null, "Player prefab must not own or duplicate the shared input components.");
            Require(actions.notificationBehavior == PlayerNotifications.InvokeUnityEvents && moveAction.enabled && !input.BlockInput, "PlayerInput must invoke action events and GameManager must release the startup input block.");
            Require(rendering.BodySprite != null && rendering.FacingMarker != null && attack.WeaponPivot != null, "Facing and weapon visuals must be wired.");
            passed("F-P01 input, body, facing and fixed weapon are bound to the spawned Player");

            // 실제 장치 이벤트가 Action·콜백을 거쳐 고정 프레임 이동에 전달되는지 검사
            Keys(keyboard, Key.W, Key.S, Key.A, Key.D);
            yield return Steps(3); // 반대 방향 키가 상쇄된 채 위치를 유지하는지 관찰
            Require(Vector2.Distance(body.position, spawn) < 0.001f && input.MoveInput == Vector2.zero, $"Opposite held keys must cancel without drift: spawn={spawn}, actual={body.position}, input={input.MoveInput}, fixed={Time.fixedTime}.");
            Keys(keyboard, Key.D);
            yield return Until(() => input.MoveInput == Vector2.right, "right action callback"); // 오른쪽 입력이 액션 콜백으로 도착할 때까지 대기
            Require(input.MoveInput == Vector2.right, $"Right input must reach Player: device={keyboard.deviceId}, current={Keyboard.current?.deviceId}, key={keyboard.dKey.isPressed}, enabled={keyboard.enabled}, input={input.MoveInput}, canMove={player.CanMove}, blocked={input.BlockInput}, component={input.enabled}, session={manager.IsSessionActive}, loading={manager.IsLoading}.");
            float straightElapsed = 0f;
            float straightSpeed = 0f;
            yield return MeasureInputMotion(player, input, keyboard, new[] { Key.D }, (elapsed, displacement) =>
            {
                straightElapsed = elapsed;
                straightSpeed = displacement.magnitude / elapsed;
            }); // 실제 이동 입력이 적용될 Update 시간과 해제 뒤 소비된 직선 변위 측정 대기
            float diagonalElapsed = 0f;
            Vector2 diagonal = Vector2.zero;
            yield return MeasureInputMotion(player, input, keyboard, new[] { Key.W, Key.D }, (elapsed, displacement) =>
            {
                diagonalElapsed = elapsed;
                diagonal = displacement;
            }); // 같은 Update 시간 기준과 비워진 요청 경계로 대각 변위 측정 대기
            float diagonalSpeed = diagonal.magnitude / diagonalElapsed;
            float speedTolerance = 0.02f;
            Require(Mathf.Abs(straightSpeed - speed) < speedTolerance && Mathf.Abs(diagonalSpeed - speed) < speedTolerance,
                $"Cardinal and diagonal speed must match SP over their actual input Update time: straight={straightSpeed}/{straightElapsed}s, diagonal={diagonalSpeed}/{diagonalElapsed}s, SP={speed}, tolerance={speedTolerance}.");
            Require(Mathf.Abs(diagonal.x - diagonal.y) < 0.005f, "Diagonal motion must preserve both axes.");
            Keys(keyboard);
            yield return Until(() => input.MoveInput == Vector2.zero && canceledCount > 0, "released move action cancellation"); // 키 해제 canceled 콜백과 이동 캐시 초기화 대기
            yield return Steps(1); // 키 해제 직후 남은 이동 프레임 정리 대기
            Vector2 begin = body.position;
            yield return Steps(3); // 키를 놓은 뒤 위치가 유지되는지 관찰
            Require(Vector2.Distance(body.position, begin) < 0.001f, "Released keys must stop without inertia.");
            passed("F-P01 opposite keys cancel, held keys move, diagonals normalize and release stops");

            // 커서 사분면별 몸체 방향·무기 조준과 뒷걸음 표현 확인
            Vector2[] directions = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) };
            for (int i = 0; i < directions.Length; i++)
            {
                Cursor(mouse, camera, body.position + directions[i] * 2f);
                yield return Steps(2); // 커서 이동이 몸체·무기 방향에 반영될 때까지 대기
                Require(rendering.FacingIndex == i, "Cursor quadrant must select the matching body direction: " + i);
                Vector2 actualAim = (input.AimWorldPosition - (Vector2)player.transform.position).normalized;
                Require(Vector2.Dot(player.AimDirection, actualAim) > 0.999f, "Screen cursor must be projected independently of movement.");
                Require(Vector2.Dot((Vector2)attack.WeaponPivot.right, player.AimDirection) > 0.999f, "Weapon pivot must aim at the cursor.");
            }
            input.enabled = false;
            Vector2 previousAim = player.AimDirection;
            player.SetAimPosition(body.position);
            yield return Steps(2); // 중심점 조준 후 마지막 방향이 유지되는지 관찰
            Require(Vector2.Distance(player.AimDirection, previousAim) < 0.001f, "Zero aim must retain the last valid direction.");
            input.enabled = true;
            Cursor(mouse, camera, body.position + Vector2.right * 4f);
            Keys(keyboard, Key.A);
            yield return Until(() => input.MoveInput == Vector2.left && lastPerformedValue == Vector2.left, "left action callback before block"); // 차단 검사 전에 왼쪽 입력 전달 확인
            yield return Steps(3); // 반대 방향 이동이 뒷걸음 표현에 반영될 때까지 대기
            Require(rendering.IsBackpedaling && player.AimDirection.x > 0.9f && body.position.x < begin.x + diagonal.x + 1f, "Moving away from the cursor must select backpedal.");
            passed("F-P01 stationary cursor selects four directions, weapon follows and backward motion is detected");

            // 명시적 차단 진입은 기존 이동을 지우고 해제는 키를 재조회하지 않는지 확인
            int canceledBeforeBlock = canceledCount;
            int performedBeforeBlock = performedCount;
            input.BlockInput = true;
            Require(input.MoveInput == Vector2.zero && player.MoveDirection == Vector2.zero, "Enabling the block must immediately clear the cached and delivered move.");
            yield return Steps(1); // 차단 직후 정지 위치를 측정할 프레임 대기
            begin = body.position;
            Cursor(mouse, camera, body.position + Vector2.down * 4f);
            yield return Steps(3); // 입력 차단 중 정지 유지와 독립 조준 갱신 관찰
            Require(Vector2.Distance(body.position, begin) < 0.001f && player.AimDirection.y < -0.99f, "The block must stop input movement while cursor aiming remains independent.");
            input.BlockInput = false;
            yield return Steps(3); // 새 콜백 없이 차단을 풀어도 이동이 재개되지 않는지 관찰
            Require(Vector2.Distance(body.position, begin) < 0.001f && input.MoveInput == Vector2.zero && player.MoveDirection == Vector2.zero && performedCount == performedBeforeBlock && canceledCount == canceledBeforeBlock && lastPerformedValue == Vector2.left, $"Unblocking must not restore the last performed move without a new action callback: begin={begin}, position={body.position}, cache={input.MoveInput}, delivered={player.MoveDirection}, performed={performedCount}/{performedBeforeBlock}, canceled={canceledCount}/{canceledBeforeBlock}, lastPerformed={lastPerformedValue}.");

            // 차단 중 새 입력 캐시와 외부 이동을 분리하고 해제 후 새 값 전달 확인
            input.BlockInput = true;
            Keys(keyboard, Key.D);
            yield return Until(() => input.MoveInput == Vector2.right, "new input cached during block"); // 차단 중 오른쪽 입력이 캐시에 반영될 때까지 대기
            yield return Steps(3); // 차단 중 새 캐시 값이 실제 이동을 만들지 않는지 관찰
            Require(Vector2.Distance(body.position, begin) < 0.001f, "Input changes must be cached but not forwarded during the block.");

            // 차단 중 연출 코드가 준 이동을 일반 입력으로 덮어쓰는지 확인
            player.SetMoveInput(Vector2.up);
            yield return Steps(3); // 차단 중 외부 코드의 이동이 유지되는지 관찰
            Require(body.position.y > begin.y + 0.05f && player.MoveDirection == Vector2.up, "Blocked input must leave scripted movement to its owner.");
            player.SetMoveInput(Vector2.zero);
            begin = body.position;
            input.BlockInput = false;
            yield return Steps(3); // 차단 해제 뒤 새로 받은 이동 값이 전달되는지 관찰
            Require(body.position.x > begin.x + 0.05f, "Unblocking must forward a new action value received during the block.");

            // 차단 중 놓은 키가 해제 뒤 남은 이동을 만들지 않는지 확인
            input.BlockInput = true;
            Keys(keyboard, Key.A);
            yield return Until(() => input.MoveInput == Vector2.left, "blocked direction change"); // 차단 중 방향 변경 콜백 도착 대기
            Keys(keyboard);
            yield return Until(() => input.MoveInput == Vector2.zero, "key release during block"); // 차단 중 키 해제가 캐시에 반영될 때까지 대기
            begin = body.position;
            input.BlockInput = false;
            yield return Steps(3); // 차단 중 해제한 키가 이동을 남기지 않는지 관찰
            Require(Vector2.Distance(body.position, begin) < 0.001f && player.MoveDirection == Vector2.zero, "A key released while blocked must not leave stale movement after unblocking.");
            passed("F-P01 block clears old input once, caches new callbacks, preserves scripted movement and keeps aiming independent");

            // 명시적 입력 차단과 별개인 컷씬·사망·시간 정지 조건 확인
            Keys(keyboard, Key.D);
            yield return Until(() => input.MoveInput == Vector2.right, "input before player state gates"); // 상태별 차단 검사 전에 이동 입력 전달 대기
            player.IsCutScene = true;
            yield return Steps(1); // 컷씬 상태가 이동을 멈추는 프레임 대기
            begin = body.position;
            yield return Steps(3); // 컷씬 상태에서 정지 위치가 유지되는지 관찰
            Require(Vector2.Distance(body.position, begin) < 0.001f, "Cutscene state must block movement.");
            player.IsCutScene = false;
            player.IsDead = true;
            yield return Steps(2); // 사망 상태에서 이동이 멈추는지 관찰
            Require(Vector2.Distance(body.position, begin) < 0.001f, "Dead Player must not move.");
            player.IsDead = false;
            manager.SetGameplayPaused(true);
            begin = body.position;
            for (int i = 0; i < 5; i++) yield return null;
            Require(Vector2.Distance(body.position, begin) < 0.001f, "Paused game must not move.");
            manager.SetGameplayPaused(false);
            Keys(keyboard);
            yield return Until(() => input.MoveInput == Vector2.zero, "release after player state gates"); // 상태 제한 해제 후 키 해제 콜백 대기
            passed("F-P01 cutscene, death and pause independently stop player movement");

            // 액션 맵 비활성화·재활성화의 취소 콜백과 현재 상태 반영 확인
            Keys(keyboard, Key.D);
            yield return Until(() => input.MoveInput == Vector2.right, "input before action map disable"); // 액션 맵 비활성화 전에 이동 입력 전달 대기
            int canceledBeforeDisable = canceledCount;
            actions.DeactivateInput();
            yield return Steps(2); // 액션 비활성화의 canceled 처리와 이동 정지 대기
            begin = body.position;
            yield return Steps(2); // 액션 비활성화 뒤 위치가 유지되는지 관찰
            Require(canceledCount > canceledBeforeDisable && input.MoveInput == Vector2.zero && Vector2.Distance(body.position, begin) < 0.001f, "Disabling the action map must send canceled and stop movement.");
            actions.ActivateInput();
            yield return Until(() => input.MoveInput == Vector2.right, "enabled Value action initial state check"); // 액션 재활성화의 현재 키 상태 반영 대기
            Keys(keyboard);
            yield return Until(() => input.MoveInput == Vector2.zero, "release after action map enable"); // 재활성화 검사 후 이동 해제 콜백 대기

            // 키 바인딩을 바꿔도 컨트롤러가 액션 값만 받아 동작하는지 확인
            for (int i = 0; i < moveAction.bindings.Count; i++)
                if (moveAction.bindings[i].path == "<Keyboard>/d") reboundIndex = i;
            Require(reboundIndex >= 0, "The Move action must expose its right-key binding.");
            previousOverride = moveAction.bindings[reboundIndex].overridePath;
            moveAction.ApplyBindingOverride(reboundIndex, "<Keyboard>/l");
            Keys(keyboard, Key.D);
            yield return Steps(3); // 바인딩 변경 후 이전 키가 움직임을 만들지 않는지 관찰
            Require(input.MoveInput == Vector2.zero, "Changing the action binding must stop the former key from moving Player.");
            Keys(keyboard, Key.L);
            yield return Until(() => input.MoveInput == Vector2.right, "rebound action callback"); // 변경한 키 바인딩의 이동 콜백 대기
            Keys(keyboard);
            yield return Until(() => input.MoveInput == Vector2.zero, "rebound action release"); // 변경한 키 바인딩의 해제 콜백 대기
            if (previousOverride == null) moveAction.RemoveBindingOverride(reboundIndex);
            else moveAction.ApplyBindingOverride(reboundIndex, previousOverride);
            reboundIndex = -1;
            passed("F-P01 action cancellation, enable initial state and rebinding drive the controller without physical-key polling");

            // 좁은 벽·고속 이동·접선 이동과 실제 Boss의 일반 Collider를 검사
            body.position = new Vector2(-3f, 0f);
            testWall = new GameObject("F-P01-TestWall");
            testWall.transform.position = new Vector3(-1.5f, 0f, 0f);
            BoxCollider2D wall = testWall.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(0.1f, 5f);
            Physics2D.SyncTransforms();
            player.SP = 100f;
            Keys(keyboard, Key.D);
            yield return Steps(4); // 고속 이동을 진행해 얇은 벽 통과 여부 관찰
            Require(player.GetComponent<Collider2D>().bounds.max.x <= wall.bounds.min.x + 0.001f, "High-speed movement must not tunnel through a thin wall.");
            player.SP = speed;
            begin = body.position;
            Keys(keyboard, Key.W, Key.D);
            yield return Steps(6); // 대각 이동을 진행해 벽을 따라 미끄러지는지 관찰
            Require(body.position.y > begin.y + 0.1f && player.GetComponent<Collider2D>().bounds.max.x <= wall.bounds.min.x + 0.001f, "Diagonal motion must slide along the wall without crossing.");
            Keys(keyboard, Key.A);
            yield return Steps(3); // 벽 반대 방향으로 이동해 끼임 여부 관찰
            Require(body.position.x < begin.x - 0.05f, "Player must move away from a wall without sticking.");
            UnityEngine.Object.Destroy(testWall);
            testWall = null;
            Keys(keyboard);
            yield return Steps(1); // 검사용 벽 제거와 이동 키 해제 반영 대기
            body.position = Vector2.zero;
            Physics2D.SyncTransforms();
            player.SP = 100f;
            Keys(keyboard, Key.D);
            yield return Steps(4); // 고속 이동을 진행해 Boss 몸체와 Trigger의 충돌 차이 관찰
            Collider2D bossBody = manager.CurrentBoss.GetComponent<Collider2D>();
            Require(player.GetComponent<Collider2D>().bounds.max.x <= bossBody.bounds.min.x + 0.001f && body.position.x > 1f, "Solid Boss body must block movement while its encounter Trigger must not.");
            Keys(keyboard);
            passed("F-P01 swept movement blocks thin walls and Boss bodies, slides and ignores encounter triggers");

            // 입력 생산과 분리해 몸체의 요청 누적·고정 프레임 소비·정리 확인
            Keys(keyboard);
            yield return Until(() => input.MoveInput == Vector2.zero, "release before body request checks"); // 일반 입력 요청이 누적 이동 검사에 섞이지 않도록 해제 대기
            yield return CheckBodyRequests(player, input, passed); // 몸체가 이동 요청을 한 번만 적용하고 정리하는지 검사

            // 실제 대시 액션과 이동·보호·잠금·재사용 타이머를 기존 물리 흐름에서 확인
            yield return CheckDash(manager, player, input, actions, keyboard, mouse, camera, passed); // 대시 방향·충돌·독립 상태·취소 검사 완료 대기
            yield return CheckUpdateMovement(player, input, passed); // Update 생산과 FixedUpdate 소비 주기가 달라도 대시 거리를 보존하는지 검사

            // 같은 실제 입력 장치로 일반 공격·피해 판정과 행동 중단 확인
            yield return PlayerAttackChecks.Run(manager, input, actions, keyboard, mouse, camera, passed); // 공격 입력부터 타격·콤보·중단까지 검사 완료 대기

            // 시간 정지 중 전용 Draw 입력·표현과 일반 조작 복구 확인
            yield return PlayerDrawChecks.Run(manager, input, actions, keyboard, mouse, camera, passed); // Draw 전환·점과 경로·중단 복구 검사 완료 대기
            yield return PlayerLineEditingChecks.Run(manager, input, keyboard, passed); // 두 점 편집의 입력 경계·고정 길이 기하 검사 완료 대기
            yield return PlayerLineLifetimeChecks.Run(manager, input, passed); // Player 소유 Line의 수명·경고·만료·중단 검사 완료 대기
            yield return PlayerSlashChecks.Run(manager, input, actions, keyboard, mouse, camera, passed); // 실제 Slash 입력·판정·수신 반응·중단 복구 검사 완료 대기
            yield return PlayerDamageChecks.Run(manager, input, passed); // 실제 피해·독립 피격 보호·깜빡임·사망 통지 검사 완료 대기

            // 이동 검사와 분리한 임시 Sprite로 재생 방향·Idle 복귀 확인
            yield return CheckAnimation(passed); // Sprite 정방향·역재생·Idle 전환 검사 완료 대기

            // 전투 컴포넌트 검사와 분리해 실제 효과·수정·생명주기 연결 확인
            yield return PlayerBuffChecks.Run(manager, input, passed); // 각성·슬로우 합성·제거·그로기 검사 완료 대기
            if (entryMotion != null) entryMotion.SetOpen(doorWantedOpen, true);
            else if (entryDoor != null) entryDoor.SetActive(doorActive);
            if (encounterScript != null) encounterScript.enabled = encounterEnabled;
            yield return PlayerInteractionChecks.Run(manager, input, keyboard, passed); // 실제 F 우선순위·회복 완료·저장·문·배치 캐시 검사 완료 대기
            lifecycle.enabled = lifecycleEnabled;
            yield return PlayerLifecycleChecks.Run(manager, passed); // 같은 Player의 자동 부활·처치 보존·중단 검사 완료 대기
            spawn = lifecycle.SpawnPoint.position;
        }
        finally
        {
            // 검사 성공 여부와 무관하게 벽·바인딩·이벤트 구독 정리
            lifecycle.CancelLifecycle();
            lifecycle.enabled = lifecycleEnabled;
            if (encounterScript != null) encounterScript.enabled = encounterEnabled;
            manager.Stage.Map.RestoreCheckpointDoors();
            if (testWall != null) UnityEngine.Object.Destroy(testWall);
            if (reboundIndex >= 0)
            {
                if (previousOverride == null) moveAction.RemoveBindingOverride(reboundIndex);
                else moveAction.ApplyBindingOverride(reboundIndex, previousOverride);
            }
            moveAction.canceled -= onCanceled; // 검사용 해제 횟수 수집 종료
            moveAction.performed -= onPerformed; // 검사용 입력값 수집 종료

            // 원래 장치·입력 설정과 Player 상태·위치 복원
            if (!string.IsNullOrEmpty(previousScheme))
                actions.SwitchCurrentControlScheme(previousScheme, previousDevices);
            else
            {
                actions.user.UnpairDevices();
                actions.user.ActivateControlScheme((string)null);
                foreach (InputDevice device in previousDevices)
                    InputUser.PerformPairingWithDevice(device, actions.user);
            }
            actions.ActivateInput();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            input.enabled = true;
            input.BlockInput = false;
            player.IsCutScene = false;
            player.IsDead = false;
            player.SP = speed;
            player.SetMoveInput(Vector2.zero);
            manager.SetGameplayPaused(false);
            body.position = spawn;
            Physics2D.SyncTransforms();
        }
    }

    /// <summary>이동의 즉시 적용·비정상 값 거부와 정지·비활성 상태 검사</summary>
    private static IEnumerator CheckBodyRequests(PlayerCharacter player, PlayerInputController input, Action<string> passed)
    {
        // 일반 입력·이동 생산을 중단하고 검사 전 위치와 활성 상태 보관
        PlayerBodyController movement = player.GetComponent<PlayerBodyController>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 originalPosition = body.position;
        bool inputWasEnabled = input.enabled;
        bool playerWasEnabled = player.enabled;
        try
        {
            input.enabled = false;
            player.SetMoveInput(Vector2.zero);
            player.enabled = false;
            movement.Stop();
            body.position = Vector2.zero;
            Physics2D.SyncTransforms();

            // 각 호출 안에서 위치를 바꾸고 비정상 변위는 즉시 거부
            Vector2 expected = new Vector2(0.19f, 0.05f);
            movement.Move(new Vector2(0.12f, 0.08f));
            Require(Vector2.Distance(body.position, new Vector2(0.12f, 0.08f)) < 0.001f, "Move must apply its displacement before returning.");
            movement.Move(new Vector2(0.07f, -0.03f));
            movement.Move(new Vector2(float.NaN, 0f));
            movement.Move(new Vector2(0f, float.PositiveInfinity));
            movement.Move(new Vector2(float.MaxValue, float.MaxValue));
            Require(Vector2.Distance(body.position, expected) < 0.001f && Vector2.Distance(body.transform.position, expected) < 0.001f,
                $"Move must immediately synchronize valid displacement and reject nonfinite values: expected={expected}, actual={body.position}.");
            yield return Steps(3); // 후속 프레임에서 이전 이동이 다시 적용되지 않는지 관찰
            Require(Vector2.Distance(body.position, expected) < 0.001f, "Completed Move calls must not replay on later frames.");

            // 정지는 이미 적용한 위치를 보존하고 비활성 몸체는 새 이동 거부
            movement.Move(Vector2.right * 0.2f);
            expected += Vector2.right * 0.2f;
            movement.Stop();
            Require(Vector2.Distance(body.position, expected) < 0.001f && movement.ActualMovement == Vector2.zero,
                "Stop must clear movement state without undoing a completed Move.");
            movement.enabled = false;
            movement.Move(Vector2.up * 0.2f);
            Require(Vector2.Distance(body.position, expected) < 0.001f, "An inactive Body must reject Move.");
            movement.enabled = true;
            yield return Steps(2); // 재활성화 이후 거부한 이동이 재생되지 않는지 관찰
            Require(Vector2.Distance(body.position, expected) < 0.001f, "Re-enabling the Body must not replay rejected movement.");
            passed("F-P01 Body moves immediately, rejects invalid or inactive requests and does not replay displacement");
        }
        finally
        {
            // 이동 상태를 비우고 일반 Player·입력 갱신과 원래 위치 복구
            movement.Stop();
            movement.enabled = true;
            body.position = originalPosition;
            player.enabled = playerWasEnabled;
            input.enabled = inputWasEnabled;
            Physics2D.SyncTransforms();
        }
    }

    /// <summary>실제 대시 입력과 방향·충돌·독립 타이머·취소 상태 검사</summary>
    private static IEnumerator CheckDash(GameManager manager, PlayerCharacter player, PlayerInputController input, PlayerInput actions, Keyboard keyboard, Mouse mouse, Camera camera, Action<string> passed)
    {
        // 검사 뒤 복원할 대시 조정값·상태와 입력 액션 보관
        PlayerBodyController movement = player.GetComponent<PlayerBodyController>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        InputAction dashAction = actions.actions.FindAction("Player/Dash", true);
        float originalSpeed = player.SP;
        float dashSpeed = player.DashSpeed;
        float duration = player.DashDuration;
        float dodgeDuration = player.DodgeDuration;
        float inputDuration = player.InputBlockDuration;
        float cooldown = player.DashCooldown;
        Vector2 originalPosition = body.position;
        bool wasBlocked = input.BlockInput;
        int performed = 0;
        Action<InputAction.CallbackContext> onDash = context => performed++;
        dashAction.performed += onDash; // Space 홀드가 새 실행 콜백을 반복 생성하는지 수집
        GameObject wallObject = null;
        try
        {
            // 일반 이동 거리를 제외하고 대시의 방향과 적용 거리만 관찰
            Require(dashAction.enabled, "The Player Dash action must be enabled and connected.");
            player.DashSpeed = 4f;
            player.DashDuration = 0.2f;
            player.DodgeDuration = 0.1f;
            player.InputBlockDuration = 0.3f;
            player.DashCooldown = 0.5f;
            player.SP = 0f;
            player.ResetMotion();
            input.BlockInput = false;
            SetBodyPosition(body, new Vector2(-3f, 0f));
            Cursor(mouse, camera, body.position + Vector2.up * 3f);
            Keys(keyboard, Key.D);
            yield return Until(() => input.MoveInput == Vector2.right && player.MoveDirection == Vector2.right, "dash movement direction input"); // 대시 직전 오른쪽 이동 의도 전달 대기
            Vector2 beganAt = body.position;
            Keys(keyboard, Key.D, Key.Space);
            yield return Until(() => player.IsDashing, "Space Dash action"); // 실제 액션·콜백·전달 판단을 거친 대시 시작 대기
            Require(performed == 1 && Vector2.Dot(player.DashDirection, Vector2.right) > 0.999f, "Moving Dash must prefer the move direction over the cursor.");
            Keys(keyboard, Key.W, Key.Space);
            yield return Until(() => input.MoveInput == Vector2.up, "direction change during dash"); // 대시 중 새 이동 입력을 받아도 시작 방향이 유지되는지 확인
            Require(Vector2.Dot(player.DashDirection, Vector2.right) > 0.999f, "A Dash must keep the direction chosen at its start.");
            yield return Steps(40); // 홀드 상태에서 대시와 쿨타임이 끝나도 자동 재실행하지 않는지 관찰
            Require(performed == 1 && !player.IsDashing && player.DashCooldownRemaining == 0f && Vector2.Distance(body.position, beganAt + Vector2.right * 0.8f) < 0.015f, "Held Space must produce one Dash with its configured distance and no automatic repeat.");
            player.SP = 2f;
            Vector2 unlockedPosition = body.position;
            yield return Steps(3); // 새 키 콜백 없이 홀드 중인 이동 입력이 잠금 해제 후 다시 적용되는지 관찰
            Require(body.position.y > unlockedPosition.y + 0.05f, "Held movement received during Dash must resume after its independent input lock ends.");
            player.SP = 0f;
            Keys(keyboard);
            yield return Until(() => input.MoveInput == Vector2.zero, "release after held dash"); // 정지 대시 검사 전 이동 키 해제 대기
            passed("F-P02 Dash action fires once per press, prefers move input and fixes its direction for the full dash");

            // 입력 수집을 잠시 멈추고 커서·마지막 방향·대각 방향의 계산만 검사
            input.enabled = false;
            player.ResetMotion();
            SetBodyPosition(body, new Vector2(-3f, 0f));
            player.SetAimPosition(body.position + Vector2.up * 2f);
            Require(player.ApplyDash() && Vector2.Dot(player.DashDirection, Vector2.up) > 0.999f, $"Stationary Dash must use the current cursor world position: move={player.MoveDirection}, aim={player.AimWorldPosition}, transform={player.transform.position}, body={body.position}, dash={player.DashDirection}, remaining={player.DashRemaining}.");
            yield return Until(() => !player.IsDashing, "stationary cursor Dash end"); // 커서 방향 대시의 Update 이동 계산이 끝날 때까지 대기
            yield return Steps(2); // 마지막 Update의 이동 요청이 Body 위치에 적용될 때까지 대기
            Require(Mathf.Abs(body.position.y - 0.8f) < 0.015f, "A stationary Dash must apply the configured distance through Body FixedUpdate.");
            player.ResetMotion();
            player.AimDirection = Vector2.left;
            player.SetAimPosition(body.position);
            Require(player.ApplyDash() && Vector2.Dot(player.DashDirection, Vector2.left) > 0.999f, "A cursor at the Player center must use its last valid aim direction.");
            player.ResetMotion();
            player.AimDirection = Vector2.zero;
            player.SetAimPosition(body.position);
            Require(player.ApplyDash() && Vector2.Dot(player.DashDirection, Vector2.down) > 0.999f, "A Dash with neither cursor offset nor last aim must use the default facing direction.");
            player.ResetMotion();
            player.SetMoveInput(Vector2.one);
            Require(player.ApplyDash() && Mathf.Abs(player.DashDirection.magnitude - 1f) < 0.001f, "Diagonal Dash must normalize its start direction.");
            passed("F-P02 stationary Dash follows the cursor, zero aim falls back and diagonal direction is normalized");

            // 한 프레임 이동 거리가 얇은 벽보다 길어도 기존 Cast 충돌 경로 유지
            player.ResetMotion();
            player.DashSpeed = 100f;
            player.DashDuration = 0.1f;
            SetBodyPosition(body, new Vector2(-3f, 0f));
            wallObject = new GameObject("F-P02-TestWall");
            wallObject.transform.position = new Vector3(-1.5f, 0f, 0f);
            BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(0.1f, 5f);
            Physics2D.SyncTransforms();
            player.SetMoveInput(Vector2.right);
            Require(player.ApplyDash(), "A ready Player must accept a Dash toward a wall.");
            yield return Until(() => !player.IsDashing, "wall-blocked Dash end"); // 고속 대시의 전체 이동 구간에서 벽 관통 여부 관찰
            Require(player.GetComponent<Collider2D>().bounds.max.x <= wall.bounds.min.x + 0.001f, "A Dash must not tunnel through a thin wall.");
            UnityEngine.Object.Destroy(wallObject);
            wallObject = null;
            yield return Steps(1); // 임시 벽 제거가 실제 물리 조회에 반영될 때까지 대기
            player.ResetMotion();
            SetBodyPosition(body, Vector2.zero);
            player.SetMoveInput(Vector2.right);
            Require(player.ApplyDash(), "A ready Player must accept a Dash toward the Boss.");
            yield return Until(() => !player.IsDashing, "Boss-blocked Dash end"); // 조우 Trigger를 지나 일반 Boss 몸체에서 멈추는지 관찰
            Collider2D boss = manager.CurrentBoss.GetComponent<Collider2D>();
            Require(body.position.x > 1f && player.GetComponent<Collider2D>().bounds.max.x <= boss.bounds.min.x + 0.001f, "Dash must pass encounter Triggers while the solid Boss body blocks it.");
            passed("F-P02 fast Dash uses swept wall and Boss collision while encounter Triggers remain passable");

            // 이동·회피·입력 잠금·쿨타임을 다른 길이로 두고 각각의 종료 확인
            player.ResetMotion();
            SetBodyPosition(body, new Vector2(-3f, 0f));
            player.DashSpeed = 2f;
            player.DashDuration = 0.4f;
            player.DodgeDuration = 0.1f;
            player.InputBlockDuration = 0.2f;
            player.DashCooldown = 0.6f;
            player.SetMoveInput(Vector2.right);
            Require(player.ApplyDash() && player.IsDodge && !player.ApplyDash(), "A Dash must start its protection and reject an immediate repeat.");
            yield return Until(() => !player.IsDodge, "Dash Dodge duration"); // 회피 보호가 이동보다 먼저 끝나는지 관찰
            Require(player.IsDashing && player.IsInputBlocked && player.DashCooldownRemaining > 0f, "Ending Dodge must not end movement, input lock or cooldown.");
            yield return Until(() => !player.IsInputBlocked, "Dash input block duration"); // 입력 잠금이 이동 종료와 별개로 해제되는지 관찰
            Require(player.IsDashing && player.DashCooldownRemaining > 0f, "Input lock duration must remain independent of dash movement and cooldown.");
            manager.SetGameplayPaused(true);
            float pausedDash = player.DashRemaining;
            float pausedCooldown = player.DashCooldownRemaining;
            Vector2 pausedPosition = body.position;
            float pauseUntil = Time.realtimeSinceStartup + 0.12f;
            while (Time.realtimeSinceStartup < pauseUntil) yield return null;
            Require(player.DashRemaining == pausedDash && player.DashCooldownRemaining == pausedCooldown && Vector2.Distance(body.position, pausedPosition) < 0.001f, "Game time pause must preserve Dash timers and position.");
            manager.SetGameplayPaused(false);
            yield return Until(() => !player.IsDashing, "Dash movement duration"); // 시간 재개 뒤 남은 대시 이동 종료 대기
            Require(player.DashCooldownRemaining > 0f && !player.ApplyDash(), "Movement end must not bypass the remaining cooldown.");
            yield return Until(() => player.DashCooldownRemaining == 0f, "Dash cooldown end"); // 재사용 제한이 끝난 뒤 대시를 다시 받을 수 있는지 대기
            Require(player.ApplyDash(), "A Dash must be usable again after its independent cooldown ends.");
            passed("F-P02 movement, Dodge, input lock and cooldown expire independently and pause with game time");

            // 행동 제한과 외부 차단·비활성화·체크포인트 이동의 남은 대시 정리 확인
            player.ResetMotion();
            Require(player.ApplyAttack(), "Player must begin a real attack before checking the Dash gate.");
            Require(!player.ApplyDash(), "Attacking Player must reject Dash.");
            player.CancelAttack();
            player.IsDead = true;
            Require(!player.ApplyDash(), "Dead Player must reject Dash.");
            player.IsDead = false;
            player.IsCutScene = true;
            Require(!player.ApplyDash(), "Cutscene Player must reject Dash.");
            player.IsCutScene = false;
            Require(player.ApplyDash(), "Dash must recover after state restrictions end.");
            player.IsDead = true;
            yield return Until(() => !player.IsDashing, "death during Dash"); // 진행 중 사망이 다음 갱신에서 대시를 취소하는지 관찰
            Require(!player.IsDodge && !player.IsInputBlocked, "Death during Dash must clear its protection and input lock.");
            player.IsDead = false;
            player.ResetMotion();
            Require(player.ApplyDash(), "Dash must be available before cutscene cancellation.");
            player.IsCutScene = true;
            yield return Until(() => !player.IsDashing, "cutscene during Dash"); // 컷씬 전환이 남은 대시 이동을 취소하는지 관찰
            Require(!player.IsDodge && !player.IsInputBlocked, "Cutscene entry during Dash must clear its protection and input lock.");
            player.IsCutScene = false;
            player.ResetMotion();
            Require(player.ApplyDash(), "Dash must be available before external input blocking.");
            input.BlockInput = true;
            Require(!player.IsDashing && !player.IsDodge && !player.IsInputBlocked && player.DashCooldownRemaining > 0f, "External input block must cancel active Dash without clearing its cooldown.");
            input.enabled = true;
            Keys(keyboard, Key.Space);
            yield return Steps(3); // 차단 중 입력한 대시 요청을 이번 프레임에서 폐기하는지 관찰
            input.BlockInput = false;
            yield return Steps(35); // 차단 해제와 쿨타임 종료 뒤 오래된 대시 입력이 발사되지 않는지 관찰
            Require(!player.IsDashing, "Unblocking input must not execute an old Dash press.");
            Keys(keyboard);
            input.enabled = false;
            player.ResetMotion();
            Require(player.ApplyDash(), "Dash must be available before disable cleanup.");
            movement.enabled = false;
            yield return Until(() => !player.IsDashing && !player.IsDodge && !player.IsInputBlocked && player.DashCooldownRemaining == 0f, "Character observes disabled body"); // 물리 몸체 비활성화를 Character가 다음 Update에서 확인·정리할 때까지 대기
            movement.enabled = true;
            Require(!player.IsDashing && !player.IsDodge && !player.IsInputBlocked && player.DashCooldownRemaining == 0f, "Character must reset its Dash timers after observing an inactive body.");
            Require(player.ApplyDash(), "Dash must be available before checkpoint reposition cleanup.");
            player.GetComponent<PlayerSpawner>().playerSpawn();
            Require(!player.IsDashing && !player.IsDodge && !player.IsInputBlocked && player.DashCooldownRemaining == 0f, "Checkpoint reposition must reset movement, Dodge, input lock and Dash cooldown.");
            passed("F-P02 state gates reject Dash and block, disable or checkpoint reposition remove stale dash state");
        }
        finally
        {
            // 검사용 액션 구독·오브젝트와 이동·대시 조정값 복원
            dashAction.performed -= onDash; // 대시 입력 횟수 수집 종료
            if (wallObject != null) UnityEngine.Object.Destroy(wallObject);
            manager.SetGameplayPaused(false);
            Keys(keyboard);
            player.CancelAttack();
            player.IsDead = false;
            player.IsCutScene = false;
            movement.enabled = true;
            player.ResetMotion();
            player.DashSpeed = dashSpeed;
            player.DashDuration = duration;
            player.DodgeDuration = dodgeDuration;
            player.InputBlockDuration = inputDuration;
            player.DashCooldown = cooldown;
            player.SP = originalSpeed;
            input.enabled = true;
            input.BlockInput = wasBlocked;
            SetBodyPosition(body, originalPosition);
        }
    }

    /// <summary>물리 간격과 무관하게 Update의 대시 거리가 즉시 적용되는지 검사</summary>
    private static IEnumerator CheckUpdateMovement(PlayerCharacter player, PlayerInputController input, Action<string> passed)
    {
        // 검사 후 복원할 갱신 간격·위치·입력 상태와 대시 조정값 보관
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 originalPosition = body.position;
        float fixedStep = Time.fixedDeltaTime;
        float speed = player.SP;
        float dashSpeed = player.DashSpeed;
        float dashDuration = player.DashDuration;
        float dodgeDuration = player.DodgeDuration;
        float inputDuration = player.InputBlockDuration;
        float cooldown = player.DashCooldown;
        bool inputWasEnabled = input.enabled;
        try
        {
            // 물리 간격을 바꿔도 Character의 대시 총거리는 같은지 확인
            input.enabled = false;
            player.SP = 0f;
            player.DashSpeed = 3f;
            player.DashDuration = 0.23f;
            player.DodgeDuration = 0.1f;
            player.InputBlockDuration = 0.3f;
            player.DashCooldown = 0.45f;
            foreach (float step in new[] { 0.01f, 0.05f })
            {
                Time.fixedDeltaTime = step;
                player.ResetMotion();
                SetBodyPosition(body, new Vector2(-3f, 0f));
                player.SetAimPosition(body.position + Vector2.up * 2f);
                Require(player.ApplyDash(), "A ready Player must start the Update-driven dash rate check.");
                yield return Until(() => !player.IsDashing, "Update dash duration at fixed step " + step); // Character.Update가 전체 대시 시간을 계산할 때까지 대기
                Require(Vector2.Distance(body.position, new Vector2(-3f, 0.69f)) < 0.015f, $"Update-driven dash distance must survive the fixed step {step}: actual={body.position}.");
            }
            passed("Character Update applies the full dash distance before completion at different physics intervals");
        }
        finally
        {
            // 실제 게임의 시간 간격·수치·위치·입력 상태 복원
            player.ResetMotion();
            Time.fixedDeltaTime = fixedStep;
            player.SP = speed;
            player.DashSpeed = dashSpeed;
            player.DashDuration = dashDuration;
            player.DodgeDuration = dodgeDuration;
            player.InputBlockDuration = inputDuration;
            player.DashCooldown = cooldown;
            SetBodyPosition(body, originalPosition);
            input.enabled = inputWasEnabled;
        }
    }

    /// <summary>실제 Sprite 전환에서 전진·역재생과 정지 전환을 확인</summary>
    private static IEnumerator CheckAnimation(Action<string> passed)
    {
        // 최종 아트와 무관하게 프레임 전환을 확인할 임시 Sprite 준비
        GameObject root = new GameObject("F-P01-AnimationCheck");
        Sprite[] frames = new Sprite[4];
        Texture2D texture = new Texture2D(4, 1);
        PlayerSpriteAnimationSet animationSet = ScriptableObject.CreateInstance<PlayerSpriteAnimationSet>();
        try
        {
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i, 0, 1, 1), Vector2.zero);
            CharacterRendering rendering = root.AddComponent<CharacterRendering>();
            rendering.BodySprite = root.AddComponent<SpriteRenderer>();
            animationSet.States = new[]
            {
                AnimationState(PlayerSpriteAnimationState.IdleLeftFront, new[] { frames[0] }),
                AnimationState(PlayerSpriteAnimationState.IdleRightFront, new[] { frames[0] }),
                AnimationState(PlayerSpriteAnimationState.IdleLeftBack, new[] { frames[0] }),
                AnimationState(PlayerSpriteAnimationState.IdleRightBack, new[] { frames[0] }),
                AnimationState(PlayerSpriteAnimationState.MoveLeftFront, frames),
                AnimationState(PlayerSpriteAnimationState.MoveRightFront, frames),
                AnimationState(PlayerSpriteAnimationState.MoveLeftBack, frames),
                AnimationState(PlayerSpriteAnimationState.MoveRightBack, frames)
            };
            rendering.AnimationSet = animationSet;

            // 정방향·역방향·정지 입력에 따라 실제 Sprite가 바뀌는지 확인
            rendering.SetMovement(Vector2.right, Vector2.right);
            yield return Until(() => rendering.BodySprite.sprite == frames[1], "forward animation second frame"); // 정방향 재생의 다음 Sprite 전환 대기
            rendering.SetMovement(Vector2.left, Vector2.right);
            yield return Until(() => rendering.BodySprite.sprite == frames[0], "reverse animation previous frame"); // 뒷걸음 역재생의 이전 Sprite 전환 대기
            rendering.SetMovement(Vector2.zero, Vector2.right);
            yield return Steps(2); // 이동 종료가 Idle 표시로 반영될 때까지 대기
            Require(rendering.BodySprite.sprite == frames[0] && !rendering.IsMoving && !rendering.IsBackpedaling, "Stopping must restore idle display.");
            passed("F-P01 real Sprite sequence advances forward, reverses for backpedal and returns to idle");
        }
        finally
        {
            // 검사에서 생성한 GameObject·Sprite·Texture 제거
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(animationSet);
            foreach (Sprite frame in frames) if (frame != null) UnityEngine.Object.Destroy(frame);
            UnityEngine.Object.Destroy(texture);
        }
    }

    /// <summary>독립 이동 검사에서 재사용할 몸체 Sprite 상태 구성</summary>
    private static PlayerSpriteAnimationSet.StateData AnimationState(PlayerSpriteAnimationState state, Sprite[] frames)
    {
        return new PlayerSpriteAnimationSet.StateData
        {
            State = state, Frames = frames, FramesPerSecond = 5f, FrameDurations = Array.Empty<float>(), Loop = true, FlipX = false
        };
    }

    /// <summary>실제 입력이 적용될 Update 시간과 모든 요청이 소비된 최종 변위 측정</summary>
    private static IEnumerator MeasureInputMotion(PlayerCharacter player, PlayerInputController input, Keyboard keyboard, Key[] keys, Action<float, Vector2> measured)
    {
        // 이전 이동 입력과 미소비 요청을 측정 구간 밖에서 먼저 정리
        Keys(keyboard);
        yield return Until(() => input.MoveInput == Vector2.zero, "speed sample input reset"); // 이전 키 해제가 실제 액션 캐시에 반영될 때까지 대기
        yield return Steps(2); // 이전 Update 요청이 FixedUpdate에서 모두 소비될 때까지 대기
        PlayerBodyController movement = player.GetComponent<PlayerBodyController>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        movement.Stop();
        Vector2 begin = body.position;
        float elapsed = 0f;
        int sampledFrame = -1;
        Action sample = () =>
        {
            // 입력 처리 직후·Character Update 직전에 같은 렌더 프레임의 게임 시간을 한 번 수집
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || sampledFrame == Time.frameCount) return;
            sampledFrame = Time.frameCount;
            if (input.MoveInput != Vector2.zero) elapsed += Time.deltaTime;
        };
        InputSystem.onAfterUpdate += sample; // 실제 이동 의도를 생산할 Update 시간 수집 시작
        try
        {
            // 실제 키 누름으로 이동하고 해제 뒤 마지막 누적 요청까지 최종 위치에 포함
            Keys(keyboard, keys);
            yield return Until(() => input.MoveInput != Vector2.zero, "speed sample movement input"); // 새 누름이 실제 이동 입력으로 전달될 때까지 대기
            yield return Steps(25); // 직선·대각 각각 충분한 실제 이동 시간 수집
            Keys(keyboard);
            yield return Until(() => input.MoveInput == Vector2.zero, "speed sample release"); // 마지막 입력 프레임을 포함한 키 해제 대기
            yield return Steps(2); // 측정한 모든 Update 요청이 FixedUpdate에 적용된 최종 위치 대기
            Require(elapsed > 0f, "A speed measurement must observe actual movement input frames.");
            measured(elapsed, body.position - begin);
        }
        finally
        {
            InputSystem.onAfterUpdate -= sample; // 실패·중단에서도 입력 프레임 시간 수집 해제
            Keys(keyboard);
        }
    }

    /// <summary>검사용 순간 이동에서 화면 좌표·물리 위치를 같은 상태로 준비</summary>
    private static void SetBodyPosition(Rigidbody2D body, Vector2 position)
    {
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        body.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>검사 전용 키보드에 눌린 키 전체를 전달</summary>
    private static void Keys(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    /// <summary>검사 전용 마우스의 화면 위치를 월드 목표점에 투영</summary>
    private static void Cursor(Mouse mouse, Camera camera, Vector2 world)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(world) });
    }

    /// <summary>기존 Editor 검사 루프에서 실제 고정 시간 진행을 대기</summary>
    private static IEnumerator Steps(int count)
    {
        float until = Time.fixedTime + Time.fixedDeltaTime * count;
        yield return Until(() => Time.fixedTime >= until - 0.00001f, "fixed frames"); // 요청한 횟수만큼 실제 FixedUpdate 시간이 지날 때까지 대기
    }

    /// <summary>게임 프레임을 실행하면서 제한 시간 안에 실제 상태가 변하는지 대기</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline)
                throw new TimeoutException("F-P01 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>깨진 이동·입력·표시 계약을 기존 검사 결과에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
