using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Tilemaps;

/// <summary>실제 Title 새 게임의 입장·입력 잠금·보행·저장과 세션 중단 확인</summary>
public static class TutorialEntryChecks
{
    private const string CaptureFolder = "Logs/TutorialEntry"; // 실제 입장 보행의 월드 카메라 프레임 저장 위치
    private const int Width = 960; // 복도와 Player를 함께 보여 줄 입장 영상의 가로 크기
    private const int Height = 540; // 기본 카메라 비율을 유지할 입장 영상의 세로 크기

    /// <summary>기존 배치를 보존해 입구만 준비한 뒤 슬롯 선택 전부터 검사</summary>
    public static void ApplyAndRun()
    {
        MapEntranceSetup.Apply();
        PlayerAttackIntegrationChecks.RunTutorial();
    }

    /// <summary>격리 저장 슬롯으로 최초 입장과 완료·구세이브·중단 경계 일곱 묶음 검사</summary>
    public static IEnumerator Run(GameManager manager, Action<string> passed)
    {
        // 앱 수명 연출 담당은 재사용하고 이 실행의 관측 자료만 별도 보관
        TutorialController tutorial = manager.Stage.Tutorial;
        EntranceReport evidence = new EntranceReport();
        Directory.CreateDirectory(CaptureFolder);
        float captureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 0f;
        try
        {
            // 실제 Title 선택으로 새 슬롯을 만들고 최초 이동 권한이 열리기 전부터 관찰
            Require(tutorial != null && SaveManager.Slots[0] == null, "The isolated Title must begin with an empty slot and its app-owned tutorial.");
            Title().SelectSlot(0);
            yield return Until(() => tutorial.IsPlaying || manager.LastError != null, "first entrance start"); // 실제 월드 생성·바인딩 뒤 입장 연출 시작 대기
            Require(tutorial.IsPlaying, "First entrance failed before playback: " + manager.LastError);
            manager.SetGameplayPaused(true);
            PlayerCharacter player = manager.CurrentPlayer;
            MapManager map = manager.Stage.Map;
            PlayerInputController input = PlayerInputController.Instance;
            CharacterRendering appearance = player.GetComponent<CharacterRendering>();
            Vector2 spawn = map.EntranceSpawnPoint.position;
            Vector2 stop = map.EntranceStopPoint.position;
            evidence.spawn = spawn;
            evidence.stop = stop;
            evidence.speed = player.SP;
            evidence.initialPosition = player.transform.position;
            Require(manager.Stage.IsReady && player.IsInitialized && player.Spawner.IsReady && !manager.IsLoading
                && input.Player == player && UIController.Instance.Player == player && GameCamera.Instance.Target == map.EntranceStopPoint,
                "Map, services and the same spawned Player must finish binding before entrance playback.");
            Require(player.IsCutScene && input.BlockInput && !player.CanMove && !player.IsSpawning && !UIController.Instance.IsPlayerHudVisible
                && !SaveManager.Current.Get<bool>("EntranceCompleted") && player.Spawner.SpawnPoint == map.GetCheckpoint("start"),
                "New entrance must own the cutscene and input locks without changing its saved respawn checkpoint.");
            Require(Vector2.Distance(player.transform.position, spawn) < 0.65f, "The first observable entrance must begin near the lower spawn point.");
            Camera camera = GameCamera.Instance.Camera;
            evidence.fixedCameraPosition = camera.transform.position;
            evidence.fixedCameraRotation = camera.transform.rotation;
            evidence.fixedCameraZoom = camera.orthographicSize;
            evidence.initialDoorViewport = DoorViewport(camera, map);
            evidence.initialBodyViewport = ViewportBounds(camera, appearance.BodySprite);
            evidence.initialWeaponViewport = ViewportBounds(camera, appearance.WeaponSprite);
            Require(evidence.initialBodyViewport.yMax < 0f && evidence.initialWeaponViewport.yMax < 0f
                && Mathf.Abs(camera.transform.position.x - stop.x) < 0.001f && Mathf.Abs(camera.transform.position.y - stop.y) < 0.001f,
                "The actual first entrance frame must hide the entire body and weapon below a camera centered on handoff.");
            evidence.captureStarted = Time.realtimeSinceStartupAsDouble;
            evidence.captureStartedGameTime = Time.timeAsDouble;
            Capture(player, evidence, true);
            passed("Actual Title new game prepares one locked Player entirely below the first camera frame while preserving the checkpoint and centering the fixed camera on handoff");

            // 남쪽 추가 셀·기존 문·물리 경계와 현재 카메라 연결을 함께 확인
            CheckMap(map, GameCamera.Instance, evidence);
            passed("Entrance preserves the original checkpoint, adds twelve southern rows and uses an independent hidden-door handoff marker with a seven-unit offscreen approach");

            // 실제 장치 누름은 차단 중 소비되고 일시정지 동안 연출 시각·위치도 유지
            using (InputFixture devices = new InputFixture(input))
            {
                Vector2 pausedAt = player.transform.position;
                devices.Send(player, true, Key.S, Key.D, Key.Space, Key.LeftShift, Key.F);
                yield return Frames(3); // 차단된 실제 액션 콜백과 Player 갱신을 거칠 때까지 대기
                Require(Vector2.Distance(player.transform.position, pausedAt) < 0.0001f && tutorial.IsPlaying
                    && input.BlockInput && player.IsCutScene && !player.IsAttacking && !player.IsDashing && !player.IsDrawing && !player.IsSlashing,
                    "Paused entrance must keep its position and reject actual mouse, WASD, dash, Draw and Slash requests.");
                devices.Send(player, false);
                manager.SetGameplayPaused(false);
                yield return Frames(3); // 게임 시간 재개 후 차단된 단발 요청이 실행되지 않는지 관찰
                Require(tutorial.IsPlaying && input.BlockInput && player.IsCutScene && !player.IsAttacking && !player.IsDashing
                    && !player.IsDrawing && !player.IsSlashing && Mathf.Abs(player.transform.position.x - spawn.x) < 0.001f,
                    "Released blocked requests must not change the scripted upward path or start another action.");
                passed("Actual blocked actions do not leak after release and gameplay pause freezes the entrance without releasing its control ownership");

                // 실제 카메라·충돌 몸체·애니메이션으로 전 구간을 촬영하고 목표 초과 여부 기록
                HashSet<Sprite> sprites = new HashSet<Sprite>();
                int footsteps = 0;
                Action<PlayerSoundCue, AudioClip> heard = (cue, clip) => { if (cue == PlayerSoundCue.Footstep) footsteps++; };
                player.Feedback.OnSoundPlayed += heard; // 실제 입장 보행에서 거리 기반 발소리가 나는지 관찰
                try
                {
                    while (tutorial.IsPlaying)
                    {
                        Require(manager.CurrentPlayer == player && input.BlockInput && player.IsCutScene && !manager.IsLoading && !UIController.Instance.IsPlayerHudVisible
                            && !manager.IsGameplayPaused && Time.timeScale > 0f, "Entrance must keep the same Player and locks until arrival.");
                        Require(Mathf.Abs(player.transform.position.x - spawn.x) < 0.001f && player.transform.position.y <= stop.y + 0.00001f,
                            "Scripted entrance must remain on its upward path without passing its handoff point.");
                        Require(Vector3.Distance(camera.transform.position, evidence.fixedCameraPosition) < 0.0001f
                            && Quaternion.Angle(camera.transform.rotation, evidence.fixedCameraRotation) < 0.0001f
                            && Mathf.Abs(camera.orthographicSize - evidence.fixedCameraZoom) < 0.0001f
                            && GameCamera.Instance.Target == map.EntranceStopPoint,
                            "The entrance camera must remain completely fixed while the Player walks into view.");
                        Require(DoorViewport(camera, map).yMin > 1f,
                            "Every part of the actual first door sprites must stay above the viewport throughout entrance playback.");
                        if (ViewportBounds(camera, appearance.BodySprite).yMax > 0f) evidence.enteredViewport = true;
                        if (appearance.IsMoving) sprites.Add(appearance.BodySprite.sprite);
                        Capture(player, evidence);
                        yield return null;
                    }
                    yield return Frames(2); // 정상 도착의 저장·입력 인계와 정지 모션 반영 대기
                    Capture(player, evidence, true);
                    evidence.distinctWalkingSprites = sprites.Count;
                    evidence.footsteps = footsteps;
                    evidence.arrived = player.transform.position;
                    evidence.handoffViewport = camera.WorldToViewportPoint(player.transform.position);
                    evidence.handoffBodyViewport = ViewportBounds(camera, appearance.BodySprite);
                    Require(tutorial.WasCompleted && !tutorial.HasPendingEntrance && tutorial.LastError == null
                        && SaveManager.Current.Get<bool>("EntranceCompleted") && Vector2.Distance(player.transform.position, stop) <= 0.00001f,
                        "Only an accurate normal arrival may mark first entrance complete.");
                    Require(evidence.enteredViewport && Vector2.Distance(evidence.handoffViewport, new Vector2(0.5f, 0.5f)) <= 0.00001f
                        && GameCamera.Instance.Target == player.transform,
                        "Arrival must reach the exact camera center before returning ownership to normal Player follow.");
                    Require(!player.IsCutScene && !input.BlockInput && player.CanMove && UIController.Instance.IsPlayerHudVisible && !appearance.IsMoving && !player.IsAttacking
                        && !player.IsDashing && !player.IsDrawing && !player.IsSlashing && sprites.Count > 1 && footsteps > 0,
                        "Arrival must stop the real walking sprites and hand off control without replaying blocked actions.");
                    Vector2 fineTarget = (Vector2)player.transform.position + Vector2.up * 0.0002f;
                    player.IsCutScene = true;
                    player.SetCutsceneMoveTarget(fineTarget);
                    try
                    {
                        yield return Frames(2); // 작은 잔여 거리도 실제 컷씬 이동·몸체 경로가 정확히 처리하는지 관찰
                        evidence.fineMoveError = Vector2.Distance(player.transform.position, fineTarget);
                        Require(evidence.fineMoveError <= 0.00001f,
                            "A0.0002-world residual must still reach the exact scripted target without stalling below the body movement threshold.");
                    }
                    finally
                    {
                        player.SetCutsceneMoveTarget(null);
                        player.IsCutScene = false;
                    }
                    Vector2 beforeInput = player.transform.position;
                    devices.Send(player, false, Key.D);
                    yield return GameSeconds(0.12f); // 인계 뒤 새 WASD 입력이 실제 일반 이동을 만드는지 관찰
                    devices.Send(player, false);
                    yield return Frames(2); // 실제 키 해제 뒤 일반 보행 정지 대기
                    Require(player.transform.position.x > beforeInput.x + 0.15f && !appearance.IsMoving,
                        "A fresh movement press must work only after handoff and stop on release.");
                    evidence.handoffMovement = (Vector2)player.transform.position - beforeInput;
                    yield return CheckWorldBounds(player, devices, evidence); // 인계 뒤 실제 걷기·Space 대시의 남쪽·좌우 맵 경계 차단 검사 대기
                }
                finally { if (player != null) player.Feedback.OnSoundPlayed -= heard; } // 세션 종료 뒤 검사 발소리 구독 해제
            }
            passed("Real walking enters the fixed camera continuously, hands off at viewport center, restores follow and keeps subsequent walking and dashing inside southern and side boundaries");

            // 정상 완료를 디스크에서 다시 읽고 다른 저장 체크포인트와 구세이브의 최초 입장 생략 확인
            Transform mid = map.GetCheckpoint("mid");
            Vector2 midPosition = mid.position;
            SaveManager.Current.Set("CheckpointId", "mid");
            SaveManager.Current.Set("ActivatedCheckpoints", new List<string> { "start", "mid" });
            SaveManager.Save();
            yield return ReturnTitle(manager); // 저장 큐 반영과 Title 슬롯 재로드 완료 대기
            Require(SaveManager.Slots[0].Get<bool>("EntranceCompleted"), "Normal completion must survive the actual disk reload.");
            Title().SelectSlot(0);
            yield return ReadyWorld(manager); // 완료 슬롯의 체크포인트 기상·일반 입력 준비 대기
            Require(!tutorial.HasPendingEntrance && !tutorial.IsPlaying && !tutorial.WasCompleted
                && manager.CurrentPlayer.Spawner.SpawnPoint == manager.Stage.Map.GetCheckpoint("mid")
                && Vector2.Distance(manager.CurrentPlayer.transform.position, midPosition) < 0.02f,
                "Completed saves must resume their saved checkpoint without the entrance warp or replay.");
            SaveManager.Current.Values.Remove("EntranceCompleted");
            SaveManager.Save();
            yield return ReturnTitle(manager); // 표식 없는 기존 형식 저장의 디스크 반영 대기
            Require(!SaveManager.Slots[0].Values.ContainsKey("EntranceCompleted"), "The old-save fixture must really omit the entrance key on disk.");
            Title().SelectSlot(0);
            yield return ReadyWorld(manager); // 구세이브도 기존 체크포인트 생성 흐름만 완료할 때까지 대기
            Require(!tutorial.IsPlaying && !tutorial.HasPendingEntrance && !SaveManager.Current.Values.ContainsKey("EntranceCompleted")
                && Vector2.Distance(manager.CurrentPlayer.transform.position, midPosition) < 0.02f,
                "Old saves missing the new marker must preserve their checkpoint and skip entrance playback.");
            yield return ReturnTitle(manager); // 다음 새 슬롯의 중단 검사를 위한 기존 세션 정리 대기
            passed("Completion survives disk reload and both completed saves and legacy saves missing the marker resume their saved checkpoint without replay");

            // 두 번째 새 슬롯에서는 물리 벽에 실제로 막히고 Title 중단 시 미완 표식·옛 대상 정리
            Title().SelectSlot(1);
            yield return Until(() => tutorial.IsPlaying || manager.LastError != null, "interrupted entrance start"); // 새 슬롯의 독립 최초 입장 시작 대기
            Require(tutorial.IsPlaying, "The second new slot must independently receive first entrance playback.");
            PlayerCharacter interrupted = manager.CurrentPlayer;
            PlayerInputController interruptedInput = PlayerInputController.Instance;
            GameObject wall = new GameObject("TutorialEntryVerificationWall", typeof(BoxCollider2D));
            BoxCollider2D shape = wall.GetComponent<BoxCollider2D>();
            shape.size = new Vector2(3f, 0.16f);
            wall.transform.position = interrupted.transform.position + Vector3.up * 0.7f;
            Physics2D.SyncTransforms();
            try
            {
                yield return GameSeconds(0.25f); // 목표 보간 대신 실제 몸체 충돌에 도달할 때까지 대기
                Vector2 wallStop = interrupted.transform.position;
                yield return GameSeconds(0.16f); // 벽 접촉 상태에서 추가 이동이 차단되는지 관찰
                evidence.collisionStop = interrupted.transform.position;
                Require(tutorial.IsPlaying && Vector2.Distance(interrupted.transform.position, wallStop) < 0.01f
                    && interrupted.transform.position.y < shape.bounds.min.y && interruptedInput.BlockInput && interrupted.IsCutScene,
                    "The scripted actor must stop against a real collider while retaining entrance control.");
                wall.SetActive(false);
                UnityEngine.Object.Destroy(wall);
                yield return GameSeconds(0.12f); // 같은 실제 Player가 벽 제거 후 목표 이동을 재개하는지 관찰
                Require(interrupted.transform.position.y > wallStop.y + 0.2f, "Removing the wall must resume actual scripted walking.");
                manager.ReturnToTitle();
                yield return TitleReady(manager); // 입장 도중 Title 복귀·옛 씬 서비스 해제 완료 대기
                Require(interrupted == null && interruptedInput == null && manager.CurrentPlayer == null && GameCamera.Instance == null
                    && !tutorial.IsPlaying && !tutorial.HasPendingEntrance && !tutorial.WasCompleted
                    && !SaveManager.Slots[1].Get<bool>("EntranceCompleted"),
                    "Title interruption must release old scene references and preserve the incomplete entrance marker.");
            }
            finally { if (wall != null) UnityEngine.Object.Destroy(wall); }
            passed("Actual collision stops entrance walking, removing the wall resumes it, and Title interruption releases old references without saving completion");

            // 미완 슬롯은 재시도하고 명시 취소의 후행 코루틴이 다음 세션의 잠금을 열지 못하는지 확인
            Title().SelectSlot(1);
            yield return Until(() => tutorial.IsPlaying, "retry after Title interruption"); // 저장된 false 표식에 따른 실제 재시도 시작 대기
            PlayerCharacter cancelled = manager.CurrentPlayer;
            GameCamera cancelledCamera = GameCamera.Instance;
            tutorial.Cancel();
            Require(cancelledCamera.Target == cancelled.transform && !cancelled.IsCutScene,
                "Cancellation must synchronously restore the live Player's normal camera target and cutscene state.");
            yield return TitleReady(manager); // 완료되지 않은 입장의 오류 복구·Title 정리 완료 대기
            Require(cancelled == null && !SaveManager.Slots[1].Get<bool>("EntranceCompleted") && !tutorial.WasCompleted,
                "Explicit cancellation must restore Title and retain the incomplete marker.");
            Title().SelectSlot(1);
            yield return Until(() => tutorial.IsPlaying, "retry after explicit cancellation"); // 취소된 앱 담당을 재사용한 다음 세션 입장 시작 대기
            PlayerCharacter retry = manager.CurrentPlayer;
            Require(tutorial == manager.Stage.Tutorial && retry != null && retry.IsCutScene && PlayerInputController.Instance.BlockInput,
                "The next session must reuse its app tutorial and keep the new Player locked despite old coroutine completion.");
            yield return ReadyWorld(manager); // 재시도 입장의 정상 도착·저장·입력 인계 완료 대기
            Require(manager.CurrentPlayer == retry && tutorial.WasCompleted && SaveManager.Current.Get<bool>("EntranceCompleted")
                && !retry.IsCutScene && !PlayerInputController.Instance.BlockInput, "Only the new session's own arrival may release its control lock.");
            yield return ReturnTitle(manager); // 최종 실제 Title 복귀와 앱·씬 수명 경계 정리 대기
            Require(retry == null && manager.CurrentPlayer == null && MapManager.Instance == null && GameCamera.Instance == null
                && PlayerInputController.Instance == null && !tutorial.IsPlaying && !tutorial.HasPendingEntrance,
                "Final Title must retain only the app-owned tutorial and release all scene-owned references.");
            passed("Interrupted entrances retry, explicit cancellation never saves completion, and stale coroutines cannot unlock a later session before its own arrival");
        }
        finally
        {
            // 실패한 관측도 남기고 검사 전 게임 시각 설정만 복구
            Time.captureDeltaTime = captureDelta;
            manager.SetGameplayPaused(false);
            File.WriteAllText("Logs/TutorialEntry-metrics.json", JsonUtility.ToJson(evidence, true));
        }
    }

    /// <summary>현재 맵의 실제 셀·기존 문·경계·카메라 범위를 데이터와 함께 확인</summary>
    private static void CheckMap(MapManager map, GameCamera camera, EntranceReport evidence)
    {
        // 지정된 실제 두 위치와 첫 문 중심으로 연출 거리와 기존 대비 배율 측정
        Transform checkpoint = map.GetCheckpoint("start");
        Require(map.EntranceSpawnPoint != null && map.EntranceStopPoint != null && map.EntranceStopPoint != checkpoint
            && map.EntranceStopPoint.IsChildOf(map.transform), "Entrance handoff must use a separate Map-owned marker and preserve the saved start checkpoint.");
        Vector2 spawn = map.EntranceSpawnPoint.position;
        Vector2 stop = map.EntranceStopPoint.position;
        evidence.checkpoint = checkpoint.position;
        GameObject entry = map.BossPositions[0].EntryDoor;
        SpriteRenderer door = entry.GetComponentInChildren<SpriteRenderer>();
        BoxCollider2D blocker = entry.GetComponent<BoxCollider2D>();
        Require(door != null && blocker != null && blocker.enabled, "The first closed door must own its actual sprite and passage blocker.");
        float doorY = blocker.bounds.center.y;
        evidence.doorSpriteCenterY = door.bounds.center.y;
        evidence.doorY = doorY;
        evidence.walkDistance = Vector2.Distance(spawn, stop);
        evidence.oldDoorDistance = doorY - checkpoint.position.y;
        evidence.stopDoorDistance = doorY - stop.y;
        evidence.newDoorDistance = doorY - spawn.y;
        evidence.distanceRatio = evidence.newDoorDistance / evidence.oldDoorDistance;
        Require(Vector2.Distance(stop, (Vector2)checkpoint.position + Vector2.down * 6f) < 0.001f
            && Vector2.Distance(spawn, stop + Vector2.down * 7f) < 0.001f && Mathf.Abs(checkpoint.position.y - 1.4f) < 0.001f
            && Mathf.Abs(evidence.walkDistance - 7f) < 0.001f && Mathf.Abs(evidence.oldDoorDistance - 3.1f) < 0.001f,
            "The saved checkpoint must remain at1.4 while the independent handoff is six units south and its offscreen spawn another seven units south.");

        // 보충 Tilemap의 실제 행과 통로 바닥을 확인하고 물리 남쪽 경계의 연결 검사
        Transform extension = map.transform.Find("LaboratoryLayout/EntranceExtension");
        Require(extension != null, "The Map must own its minimal southern tile extension.");
        Tilemap floor = extension.Find("corridor-floor").GetComponent<Tilemap>();
        evidence.worldSouth = floor.CellToWorld(new Vector3Int(0, floor.cellBounds.yMin, 0)).y;
        Require(floor.cellBounds.yMin == -12 && floor.cellBounds.yMax == 0, "The actual southern floor must contain exactly twelve added rows.");
        foreach (Tilemap tiles in extension.GetComponentsInChildren<Tilemap>())
        {
            int count = 0;
            foreach (Vector3Int cell in tiles.cellBounds.allPositionsWithin)
                if (tiles.HasTile(cell)) { Require(cell.y >= floor.cellBounds.yMin && cell.y < 0, "Extension tiles must stay inside the twelve added southern rows."); count++; }
            evidence.extensionTileCount += count;
        }
        Require(evidence.extensionTileCount == 432 && floor.HasTile(floor.WorldToCell(spawn)), "Added rows must contain all432 copied cells and actual floor below the Player spawn.");
        Transform boundaries = map.transform.Find("WorldCollision");
        bool bottom = false;
        float left = float.NegativeInfinity;
        float right = float.PositiveInfinity;
        foreach (BoxCollider2D wall in boundaries.GetComponentsInChildren<BoxCollider2D>(true))
        {
            Bounds bounds = wall.bounds;
            if (Mathf.Abs(bounds.max.y - evidence.worldSouth) < 0.001f && bounds.min.x < spawn.x && bounds.max.x > spawn.x) bottom = true;
            if (bounds.min.y <= spawn.y && bounds.max.y >= stop.y)
            {
                if (bounds.max.x < spawn.x) left = Mathf.Max(left, bounds.max.x);
                if (bounds.min.x > spawn.x) right = Mathf.Min(right, bounds.min.x);
            }
        }
        evidence.cameraLimits = camera.VerticalLimits;
        Require(bottom && !float.IsInfinity(left) && !float.IsInfinity(right) && evidence.worldSouth == -12f
            && camera.VerticalLimits == new Vector2(evidence.worldSouth, 77f)
            && Mathf.Abs(camera.FollowDuration - 0.1283f) < 0.00001f,
            "Extended floor must remain enclosed and camera bounds must expand south without changing its tuned follow duration.");
        evidence.worldLeft = left;
        evidence.worldRight = right;
        Require(DoorViewport(camera.Camera, map).yMin > 1f, "The fixed entrance camera must place every first-door sprite fully above its screen.");
    }

    /// <summary>조작 인계 뒤 실제 이동·대시도 통로의 남쪽과 양옆 경계를 통과하지 못하는지 확인</summary>
    private static IEnumerator CheckWorldBounds(PlayerCharacter player, InputFixture devices, EntranceReport evidence)
    {
        // 실제 몸체와 기존 통로 경계를 사용하며 촬영·임시 벽과 분리해 검사
        BoxCollider2D shape = player.GetComponent<BoxCollider2D>();
        Key[] directions = { Key.S, Key.A, Key.D };
        float[] walkingTimes = { 2.1f, 1.1f, 1.3f };
        for (int i = 0; i < directions.Length; i++)
        {
            devices.Send(player, false, directions[i]);
            yield return GameSeconds(walkingTimes[i]); // 실제 키 입력으로 남쪽·왼쪽·오른쪽 벽까지 걸어갈 시간 대기
            Physics2D.SyncTransforms();
            Require(shape.bounds.min.y >= evidence.worldSouth - 0.001f && shape.bounds.min.x >= evidence.worldLeft - 0.001f
                && shape.bounds.max.x <= evidence.worldRight + 0.001f,
                "Normal walking must keep the entire physical body inside its southern and side world boundaries.");
            Vector2 atWall = player.transform.position;
            if (i == 0) Require(shape.bounds.min.y < evidence.worldSouth + 0.05f, "The southern boundary fixture must actually reach the bottom wall.");
            else if (i == 1) Require(shape.bounds.min.x < evidence.worldLeft + 0.05f, "The left boundary fixture must actually reach its wall.");
            else Require(shape.bounds.max.x > evidence.worldRight - 0.05f, "The right boundary fixture must actually reach its wall.");

            // 같은 실제 Space 누름으로 벽을 향한 고속 대시를 실행하고 관통·밀림 여부 검사
            devices.Send(player, false, directions[i], Key.Space);
            yield return Frames(1); // 실제 액션 콜백이 Character의 대시 요청으로 소비될 때까지 대기
            Require(player.IsDashing || player.DashCooldownRemaining > 0f, "The boundary fixture must start a real Space-triggered dash.");
            yield return Until(() => !player.IsDashing, "boundary dash completion"); // 감속 대시의 전체 이동 시간과 충돌 보정 완료 대기
            devices.Send(player, false);
            yield return Frames(2); // 이동·대시 키 해제 뒤 몸체·모션 정지 반영 대기
            Physics2D.SyncTransforms();
            Require(shape.bounds.min.y >= evidence.worldSouth - 0.001f && shape.bounds.min.x >= evidence.worldLeft - 0.001f
                && shape.bounds.max.x <= evidence.worldRight + 0.001f
                && Vector2.Distance(player.transform.position, atWall) < 0.02f,
                "A real dash into an already reached world wall must not escape or move the Player through it.");
            evidence.boundaries.Add(new BoundarySample { direction = directions[i].ToString(), position = player.transform.position,
                minimum = shape.bounds.min, maximum = shape.bounds.max });
        }
    }

    /// <summary>실제 Sprite 월드 경계 전체를 카메라 viewport 사각형으로 변환</summary>
    private static Rect ViewportBounds(Camera camera, SpriteRenderer renderer)
    {
        // 피벗 위치만 비교하지 않고 회전된 검까지 포함하는 전체 렌더 경계 사용
        Bounds bounds = renderer.bounds;
        Vector3 lower = camera.WorldToViewportPoint(bounds.min);
        Vector3 upper = camera.WorldToViewportPoint(bounds.max);
        return Rect.MinMaxRect(Mathf.Min(lower.x, upper.x), Mathf.Min(lower.y, upper.y),
            Mathf.Max(lower.x, upper.x), Mathf.Max(lower.y, upper.y));
    }

    /// <summary>첫 입구문의 모든 Sprite를 포함한 viewport 전체 경계 조회</summary>
    private static Rect DoorViewport(Camera camera, MapManager map)
    {
        // 문 오브젝트 원점·충돌선 대신 실제 표시 전체가 화면 위에 있는지 확인
        SpriteRenderer[] renderers = map.BossPositions[0].EntryDoor.GetComponentsInChildren<SpriteRenderer>(true);
        Require(renderers.Length > 0, "The first entrance door must own visible sprite geometry.");
        Rect total = ViewportBounds(camera, renderers[0]);
        foreach (SpriteRenderer renderer in renderers)
        {
            Rect bounds = ViewportBounds(camera, renderer);
            total = Rect.MinMaxRect(Mathf.Min(total.xMin, bounds.xMin), Mathf.Min(total.yMin, bounds.yMin),
                Mathf.Max(total.xMax, bounds.xMax), Mathf.Max(total.yMax, bounds.yMax));
        }
        return total;
    }

    /// <summary>현재 카메라 설정을 유지해 실제 입장 프레임과 관측값 저장</summary>
    private static void Capture(PlayerCharacter player, EntranceReport evidence, bool force = false)
    {
        // 검사 일시정지 구간은 영상에서 제외하고 실제 게임 시각 간격으로 촬영
        double elapsed = Time.timeAsDouble - evidence.captureStartedGameTime;
        if (!force && elapsed < evidence.nextCapture) return;
        evidence.nextCapture = elapsed + 1d / 24d;
        string file = "entrance-" + evidence.frames.Count.ToString("D3") + ".png";
        Camera camera = GameCamera.Instance.Camera;
        RenderTexture texture = RenderTexture.GetTemporary(Width, Height, 24);
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        Texture2D pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(CaptureFolder, file), pixels.EncodeToPNG());
            CharacterRendering appearance = player.GetComponent<CharacterRendering>();
            evidence.frames.Add(new CaptureSample { file = file, elapsed = elapsed, gameTime = Time.timeAsDouble,
                position = player.transform.position, moving = appearance.IsMoving, sprite = appearance.BodySprite.sprite.name,
                cutscene = player.IsCutScene, blocked = PlayerInputController.Instance.BlockInput,
                cameraPosition = camera.transform.position, bodyViewport = ViewportBounds(camera, appearance.BodySprite),
                weaponViewport = ViewportBounds(camera, appearance.WeaponSprite), rootViewport = camera.WorldToViewportPoint(player.transform.position),
                doorViewport = DoorViewport(camera, MapManager.Instance) });
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    /// <summary>실제 저장 재로드까지 끝난 Title로 복귀</summary>
    private static IEnumerator ReturnTitle(GameManager manager)
    {
        manager.ReturnToTitle();
        yield return TitleReady(manager); // 세션 해제와 Title 슬롯 최신화 완료 대기
    }

    /// <summary>첫 입장·기상 표현 뒤 실제 일반 조작 가능 시점 대기</summary>
    private static IEnumerator ReadyWorld(GameManager manager)
    {
        yield return Until(() => manager.Stage.IsReady && manager.CurrentPlayer != null && !manager.IsLoading
            && !manager.CurrentPlayer.IsSpawning && !manager.CurrentPlayer.IsCutScene && !PlayerInputController.Instance.BlockInput,
            "world control handoff"); // 준비 상태만이 아니라 생성·입장 권한 인계 완료 대기
    }

    /// <summary>이전 MainWorld 대상이 제거되고 새 Title 데이터가 준비될 때까지 대기</summary>
    private static IEnumerator TitleReady(GameManager manager)
    {
        yield return Until(() => Title() != null && Title().IsReady && !manager.IsLoading && manager.CurrentPlayer == null,
            "Title reload"); // 씬 전환·슬롯 디스크 로드·옛 Player 해제 완료 대기
    }

    private static UI_Title Title() => UnityEngine.Object.FindFirstObjectByType<UI_Title>();

    /// <summary>실제 Player Loop의 지정 프레임 경계 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "entrance frames"); // 실제 프레임 갱신 횟수 대기
    }

    /// <summary>컷씬·이동이 사용하는 scaled 시각으로 관찰 구간 대기</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        yield return Until(() => Time.timeAsDouble >= end, "entrance game time"); // 지정된 실제 게임 시간 진행 대기
    }

    /// <summary>지정 조건을 관찰하며 준비 실패·무한 대기만 차단</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double end = Time.realtimeSinceStartupAsDouble + 25d;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException("Tutorial entrance timed out: " + description);
            yield return null;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>원래 입력 장치와 포커스 정책을 보존하는 실제 액션 검사 장치</summary>
    private class InputFixture : IDisposable
    {
        private PlayerInput actions; // 이번 씬의 실제 Unity 입력 액션 전달 대상
        private InputDevice[] previousDevices; // 검사 뒤 복원할 이전 연결 장치
        private string previousScheme; // 검사 뒤 복원할 이전 입력 스킴
        private Keyboard keyboard; // 입장 중 실제 키 누름·해제를 전달할 검사 장치
        private Mouse mouse; // 입장 중 실제 공격과 조준 입력을 전달할 검사 장치
        private InputSettings.BackgroundBehavior background; // 검사 전 백그라운드 장치 처리 정책
        private InputSettings.EditorInputBehaviorInPlayMode editor; // 검사 전 Editor 입력 전달 정책

        public InputFixture(PlayerInputController input)
        {
            // 기존 연결과 포커스 정책을 보관한 뒤 독립 실제 장치만 연결
            actions = input.GetComponent<PlayerInput>();
            previousDevices = new InputDevice[actions.devices.Count];
            for (int i = 0; i < previousDevices.Length; i++) previousDevices[i] = actions.devices[i];
            previousScheme = actions.currentControlScheme;
            background = InputSystem.settings.backgroundBehavior;
            editor = InputSystem.settings.editorInputBehaviorInPlayMode;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            actions.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        }

        public void Send(PlayerCharacter player, bool attack, params Key[] keys)
        {
            // 실제 Input System 큐를 통해 평소 PlayerInput UnityEvent 경로 사용
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            Vector3 screen = GameCamera.Instance.Camera.WorldToScreenPoint(player.transform.position + Vector3.right * 2f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, attack));
            InputSystem.Update();
        }

        public void Dispose()
        {
            // 검사 장치만 제거하고 세션의 기존 입력 환경 복구
            if (actions != null && !string.IsNullOrEmpty(previousScheme) && previousDevices.Length > 0)
                actions.SwitchCurrentControlScheme(previousScheme, previousDevices);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editor;
        }
    }

    /// <summary>입장 경로·권한 인계·맵·실제 화면 관측값</summary>
    [Serializable]
    private class EntranceReport
    {
        public Vector2 spawn; // 맵이 지정한 최초 입장 시작 위치
        public Vector2 stop; // 조작 권한을 넘길 실제 정지 위치
        public Vector3 checkpoint; // 최초 입장 정지점과 구분해 보존할 원래 저장 체크포인트 위치
        public Vector3 initialPosition; // 처음 관찰한 실제 Player 위치
        public Vector3 arrived; // 정상 완료 뒤 실제 Player 위치
        public Vector2 handoffMovement; // 인계 뒤 새로운 일반 키 입력으로 움직인 변위
        public Vector3 collisionStop; // 임시 벽에 막힌 실제 Player 위치
        public float speed; // 입장 중 적용한 Player 보행 속도
        public float doorY; // 보존한 첫 보스 입구문의 실제 통과 차단 중심 높이
        public float doorSpriteCenterY; // 여백 포함 전체 Sprite 경계의 중심으로 차단 중심과 구분할 높이
        public float walkDistance; // 최초 입장 시작점에서 정지점까지 실제 거리
        public float oldDoorDistance; // 기존 체크포인트에서 첫 입구문까지 거리
        public float newDoorDistance; // 확장한 시작점에서 첫 입구문까지 거리
        public float stopDoorDistance; // 독립 입장 정지점에서 첫 입구문 통과선까지 거리
        public float distanceRatio; // 기존 대비 실제 보이는 입구 거리 배율
        public int extensionTileCount; // 남쪽 여섯 행의 실제 추가 타일 수
        public Vector2 cameraLimits; // 확장 뒤 실제 GameCamera의 남북 제한
        public float worldSouth; // 추가된 실제 바닥과 맞닿은 남쪽 물리 경계
        public float worldLeft; // 입장 통로의 실제 왼쪽 벽 안쪽 경계
        public float worldRight; // 입장 통로의 실제 오른쪽 벽 안쪽 경계
        public int distinctWalkingSprites; // 실제 입장 보행에서 바뀐 몸체 Sprite 개수
        public int footsteps; // 실제 입장 보행에서 재생한 거리 기반 발소리 수
        public double captureStarted; // 촬영 시작 당시의 실제 시각
        public double captureStartedGameTime; // 검사 일시정지를 제외한 영상의 실제 게임 시작 시각
        public double nextCapture; // 일정 간격 촬영을 위한 다음 실제 시각
        public Vector3 fixedCameraPosition; // 최초 입장 전체 동안 고정할 실제 카메라 위치
        public Quaternion fixedCameraRotation; // 최초 입장 중 유지할 실제 카메라 회전
        public float fixedCameraZoom; // 최초 입장 중 유지할 실제 직교 카메라 크기
        public Rect initialBodyViewport; // 첫 프레임의 몸체 전체 경계로 화면 아래 비가시 확인
        public Rect initialWeaponViewport; // 첫 프레임의 검 전체 경계로 화면 아래 비가시 확인
        public Rect initialDoorViewport; // 첫 프레임 문 전체가 화면 위에 비가시인지 확인할 경계
        public Vector3 handoffViewport; // 조작 인계 시 Player 중심의 viewport 좌표
        public Rect handoffBodyViewport; // 시각 피벗 차이와 Player 중심을 구분할 인계 시 몸체 전체 경계
        public bool enteredViewport; // 비가시 시작 뒤 실제 몸체가 화면 안으로 들어왔는지 표시
        public float fineMoveError; //0.0002world 잔여 이동을 처리한 뒤 목표까지 남은 실제 오차
        public List<BoundarySample> boundaries = new List<BoundarySample>(); // 인계 뒤 실제 남쪽·좌우 걷기·대시 경계 관측값
        public List<CaptureSample> frames = new List<CaptureSample>(); // 현재 월드 카메라에서 저장한 프레임별 관측값
    }

    /// <summary>입장 영상 한 프레임의 실제 좌표와 표시·권한 상태</summary>
    [Serializable]
    private class CaptureSample
    {
        public string file; // 프레임 PNG 파일명
        public double elapsed; // 영상 시작부터 경과한 실제 시간
        public double gameTime; // 해당 프레임의 실제 scaled 게임 시각
        public Vector3 position; // 해당 프레임의 실제 Player 위치
        public bool moving; // 몸체가 표시하는 보행 모션 상태
        public string sprite; // 표시한 실제 몸체 Sprite 이름
        public bool cutscene; // 해당 프레임의 Player 컷씬 잠금 상태
        public bool blocked; // 해당 프레임의 일반 입력 차단 상태
        public Vector3 cameraPosition; // 보행 중 고정 화면 유지와 인계 뒤 추적 복구를 비교할 실제 카메라 위치
        public Rect bodyViewport; // 몸체가 화면 아래에서 안으로 들어오는 전체 렌더 경계
        public Rect weaponViewport; // 검을 포함한 최초 완전 비가시와 입장 연속성 확인 경계
        public Vector3 rootViewport; // 해당 프레임의 Player 중심 viewport 좌표
        public Rect doorViewport; // 입장 내내 모든 문 Sprite가 화면 위에 유지되는지 확인할 전체 경계
    }

    /// <summary>인계 뒤 한 방향의 실제 걷기·대시 경계 정지 결과</summary>
    [Serializable]
    private class BoundarySample
    {
        public string direction; // 검사에 사용한 실제 WASD 키 방향
        public Vector3 position; // 걷기와 대시를 모두 마친 실제 Player 위치
        public Vector3 minimum; // 실제 몸체 Collider 전체 경계의 최소 좌표
        public Vector3 maximum; // 실제 몸체 Collider 전체 경계의 최대 좌표
    }
}
