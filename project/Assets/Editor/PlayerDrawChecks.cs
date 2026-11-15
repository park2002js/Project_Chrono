using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>실제 Draw 입력 전환·점과 경로 표시·정지 소유와 강제 종료 복구 확인</summary>
public static class PlayerDrawChecks
{
    /// <summary>기존 검사 장치를 재사용해 Draw를 확인하고 수치·표시·시간·입력을 복구</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, PlayerInput actions, Keyboard keyboard, Mouse mouse, Camera camera, Action<string> passed)
    {
        // 실제 세션 연결과 검사 종료 후 되돌릴 상태 보관
        PlayerCharacter player = manager.CurrentPlayer;
        SkillController skills = player.Skills;
        DrawVfx vfx = player.GetComponent<DrawVfx>();
        CharacterRendering rendering = player.GetComponent<CharacterRendering>();
        AttackController attack = player.GetComponent<AttackController>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        GameCamera gameCamera = GameCamera.Instance;
        UIController ui = UIController.Instance;
        UI_Draw drawView = ui.GetComponent<UI_Draw>();
        Require(skills != null && skills.Drawer != null && vfx != null && vfx.IsPrepared && input.HasDrawActions,
            "The Player must own prepared Draw data, visual prefabs and a connected Draw action map.");
        Line? previousLine = player.CurrentLine;
        Vector2 position = body.position;
        Vector2 aim = player.AimWorldPosition;
        Vector3 cameraPosition = camera.transform.position;
        Transform cameraTarget = gameCamera.Target;
        float zoom = camera.orthographicSize;
        float followDuration = gameCamera.FollowDuration;
        float lineLength = skills.LineLength;
        float lineWidth = skills.LineWidth;
        float cancelCooldown = skills.CancelDrawCooldown;
        float slashCooldown = skills.ConfirmSlashCooldown;
        float lifetime = skills.LineLifetime;
        float health = player.HP;
        bool invulnerable = player.IsInvulnerable;
        bool playerHud = ui.IsPlayerHudVisible;
        bool bossHud = ui.IsBossHudVisible;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        string actionMap = actions.currentActionMap.name;
        GameObject pointVisual = vfx.PointObjects[0];
        GameObject previewVisual = vfx.PreviewObject;
        GameObject lineVisual = vfx.LineObject;
        int attackRequests = 0;
        InputAction attackAction = actions.actions.FindAction("Player/Attack", true);
        Action<InputAction.CallbackContext> countAttack = context => attackRequests++;
        attackAction.performed += countAttack; // Draw의 클릭이 일반 공격으로 누출되는지 수집
        try
        {
            // 점 편집과 복귀를 반복할 수 있도록 검사 값과 일반 조작 상태 준비
            Require(!skills.IsDrawing && !manager.IsDrawPaused, "Draw checks must begin in normal gameplay.");
            manager.SetGameplayPaused(false);
            input.enabled = true;
            input.BlockInput = false;
            skills.ResetSkills();
            player.ResetMotion();
            skills.LineLength = 6f;
            skills.LineWidth = 0.25f;
            skills.CancelDrawCooldown = 0f;
            skills.ConfirmSlashCooldown = 0.6f;
            skills.LineLifetime = 60f;
            gameCamera.FollowDuration = 0.05f;
            Keys(keyboard);
            MouseInput(mouse, camera, body.position + Vector2.right * 3f, false);
            yield return Frames(2); // 이전 공격·이동 입력의 해제와 커서 갱신 대기

            // 실제 Shift 한 번으로 전용 입력에 진입하고 같은 누름으로 즉시 취소되지 않는지 확인
            Keys(keyboard, Key.LeftShift);
            yield return Until(() => skills.IsDrawing, "Shift enters Draw"); // 실제 Shift 액션이 Character와 Skill을 거쳐 Draw를 여는 시점 대기
            Vector2 stoppedPosition = body.position;
            Vector3 initialCamera = camera.transform.position;
            yield return Frames(3); // 진입 키를 유지한 채 새 입력 맵에서 중복 취소가 발생하는지 관찰
            float stoppedTime = Time.time;
            Require(skills.IsDrawing && input.IsDrawInput && actions.currentActionMap.name == "Draw",
                "One held Shift press must enter the Draw map without cancelling itself.");
            Require(!actions.actions.FindAction("Player/Move", true).enabled && !attackAction.enabled && !actions.actions.FindAction("Player/Dash", true).enabled,
                "The normal movement, attack and dash actions must be disabled while drawing.");
            Require(manager.IsDrawPaused && Time.timeScale == 0f && ui.IsDrawMode && gameCamera.IsDrawMode && rendering.IsDrawHighlighted,
                "Draw must activate its own pause, UI, camera and Player highlighting together.");
            Require(drawView != null && drawView.IsVisible && drawView.ScreenShade != null && drawView.ScreenShade.gameObject.activeInHierarchy,
                "Draw mode must display the prepared temporary screen shade and editing guide.");
            Keys(keyboard, Key.LeftShift, Key.W, Key.D, Key.Space);
            MouseInput(mouse, camera, body.position + Vector2.up * 4f, false);
            yield return RealSeconds(0.12f); // 게임 시간이 멈춰도 전용 커서·카메라 갱신은 계속되는지 관찰
            Require(Time.time == stoppedTime && Vector2.Distance(body.position, stoppedPosition) < 0.001f && !player.IsAttacking && !player.IsDashing,
                "Normal movement, attack and dash must not run during Draw.");
            Require(camera.orthographicSize > zoom && Vector3.Distance(camera.transform.position, initialCamera) > 0.001f,
                "Draw zoom and weighted cursor tracking must advance using unscaled time.");
            passed("F-P04 real Shift switches to Draw-only actions, freezes gameplay and keeps UI, highlighting and camera active");

            // 점 편집·동일 점 거부·미리보기·R 초기화와 실제 F 확정 연결 확인
            Keys(keyboard);
            yield return Click(mouse, camera, body.position + Vector2.right); // 첫 실제 클릭이 점 선택으로 전달될 때까지 대기
            Require(skills.Drawer.PointCount == 1 && vfx.PointObjects[0].activeSelf && !vfx.PointObjects[1].activeSelf,
                "The first Draw click must display one point without starting an attack.");
            Renderer pointRenderer = vfx.PointObjects[0].GetComponentInChildren<Renderer>();
            Require(pointRenderer != null && pointRenderer.enabled && pointRenderer.bounds.size.sqrMagnitude > 0f
                && Vector2.Distance(vfx.PointObjects[0].transform.position, skills.Drawer.StartPoint) < 0.002f,
                "The temporary point prefab must visibly render at the selected world position.");
            skills.PlacePoint(skills.Drawer.StartPoint);
            Require(skills.Drawer.PointCount == 1 && !skills.Drawer.CanConfirm, "The same point cannot provide a valid second direction.");
            MouseInput(mouse, camera, skills.Drawer.StartPoint + Vector2.up * 2f, false);
            yield return Frames(3); // 첫 점과 최신 커서가 만드는 고정 길이 미리보기 대기
            Require(skills.Drawer.PreviewLine.HasValue && vfx.PreviewObject.activeSelf, "One point and the cursor must display a preview.");
            AssertPath(vfx.PreviewObject, skills.Drawer.PreviewLine.Value);
            yield return Press(keyboard, Key.R); // R이 임시 점과 미리보기만 초기화하는 시점 대기
            Require(skills.IsDrawing && skills.Drawer.PointCount == 0 && !vfx.PointObjects[0].activeSelf && !vfx.PreviewObject.activeSelf,
                "Reset must stay in Draw while hiding the previous points and preview.");
            yield return Click(mouse, camera, body.position + Vector2.right); // 초기화 뒤 새 시작점 선택 대기
            MouseInput(mouse, camera, skills.Drawer.StartPoint + new Vector2(2f, 1f), true);
            yield return Until(() => skills.Drawer.PointCount == 2, "second point"); // 두 번째 실제 클릭으로 방향이 확정될 때까지 대기
            Require(skills.Drawer.CanConfirm && vfx.PointObjects[1].activeSelf, "Two distinct points must enable confirmation and display the second point.");
            Line selected = skills.Drawer.PreviewLine.Value;
            ScreenCapture.CaptureScreenshot("Temp/DrawValidation.png");
            yield return Frames(3); // 임시 점·선·암전의 실제 Game View 캡처가 저장될 때까지 대기
            Keys(keyboard, Key.F);
            yield return Until(() => !skills.IsDrawing, "F confirms line"); // 확정 액션의 데이터 저장과 일반 입력 복귀 대기
            yield return Frames(3); // 두 번째 점의 M1을 누른 채 일반 입력 맵으로 돌아가도 공격이 누출되지 않는지 관찰
            Require(skills.HasLine && SameLine(player.CurrentLine.Value, selected) && Mathf.Abs(selected.Length - 6f) < 0.001f && Mathf.Abs(selected.Width - 0.25f) < 0.001f,
                "Confirmation must store the fixed configured length and width rather than the clicked distance.");
            Require(attackRequests == 0 && !player.IsAttacking, "A held point-placement click must not become a normal attack when the map changes back.");
            AssertRestored(manager, input, actions, ui, gameCamera, rendering);
            Require(!drawView.IsVisible && !drawView.ScreenShade.gameObject.activeInHierarchy, "Confirm must hide the temporary Draw screen shade.");
            Require(ui.IsPlayerHudVisible == playerHud && ui.IsBossHudVisible == bossHud && camera.orthographicSize == zoom && gameCamera.Target == cameraTarget,
                "Confirm must restore the previous HUD visibility, zoom and camera target.");
            Require(!vfx.PointObjects[0].activeSelf && !vfx.PointObjects[1].activeSelf && !vfx.PreviewObject.activeSelf && vfx.LineObject.activeSelf,
                "Confirm must hide editing VFX and keep the confirmed line visible.");
            AssertPath(vfx.LineObject, selected);
            Keys(keyboard);
            MouseInput(mouse, camera, body.position + Vector2.right, false);
            yield return Frames(2); // 복귀 검사 뒤 실제 버튼과 키 해제 대기
            passed("F-P04 point input, preview, reset and confirm use temporary VFX and return to normal controls without click leakage");

            // 재Draw의 미완 확정·취소는 기존 Line과 재사용 표현을 보존하고 새 확정만 교체
            Line firstLine = player.CurrentLine.Value;
            yield return EnterDraw(keyboard, skills); // 기존 확정 Line을 보유한 채 새 편집 시작 대기
            Require(SameLine(player.CurrentLine.Value, firstLine) && vfx.LineObject.activeSelf, "Entering Draw must preserve the confirmed Line.");
            yield return Press(keyboard, Key.F); // 점이 없는 확정 요청이 편집을 끝내지 않는지 관찰
            Require(skills.IsDrawing && SameLine(player.CurrentLine.Value, firstLine), "An incomplete confirmation must preserve both editing and the previous line.");
            yield return Click(mouse, camera, body.position + Vector2.up); // 취소 전 임시 점이 있는 재편집 상태 준비
            yield return Press(keyboard, Key.LeftShift); // Shift가 이번 편집만 취소하는 시점 대기
            Require(!skills.IsDrawing && skills.Drawer.PointCount == 0 && SameLine(player.CurrentLine.Value, firstLine) && vfx.LineObject.activeSelf,
                "Cancelling a redraw must retain the previous line and remove only temporary points.");
            Require(vfx.PointObjects[0] == pointVisual && vfx.PreviewObject == previewVisual && vfx.LineObject == lineVisual,
                "Repeated edits must reuse the prepared visual instances.");
            SetPosition(body, body.position + Vector2.left);
            AssertPath(vfx.LineObject, firstLine);
            yield return EnterDraw(keyboard, skills); // Player를 재배치한 뒤 기존 월드 선과 독립적으로 새 편집 시작 대기
            yield return Click(mouse, camera, body.position + Vector2.up); // 새 경로의 시작점 선택 대기
            yield return Click(mouse, camera, skills.Drawer.StartPoint + Vector2.left * 2f); // 새 경로의 두 번째 방향점 선택 대기
            yield return Press(keyboard, Key.F); // 새 확정만 기존 Line을 교체하는지 관찰
            Require(!skills.IsDrawing && skills.HasLine && !SameLine(player.CurrentLine.Value, firstLine), "A valid new confirmation must replace the old line.");
            AssertPath(vfx.LineObject, player.CurrentLine.Value);
            passed("F-P04 redraw cancellation preserves the old world-space Line and reuses visuals while a new confirmation replaces it");

            // 공통 행동 허용과 능력 자체 쿨타임을 각 소유 API에서 확인
            player.ResetMotion();
            player.SetAimPosition(body.position + Vector2.right);
            Require(player.ApplyAttack() && !player.ApplyDraw(), "Attacking must prevent Draw entry at the Character gate.");
            player.CancelAttack();
            Require(player.ApplyDash() && !player.ApplyDraw(), "Dash and its input lock must prevent Draw entry.");
            player.ResetMotion();
            player.IsDead = true;
            Require(!player.ApplyDraw(), "Dead Characters cannot enter Draw.");
            player.IsDead = false;
            player.IsCutScene = true;
            Require(!player.ApplyDraw(), "Cutscene Characters cannot enter Draw.");
            player.IsCutScene = false;
            input.BlockInput = true;
            yield return Press(keyboard, Key.LeftShift); // 차단 중 실제 Shift 요청이 전달되지 않는지 관찰
            Require(!skills.IsDrawing, "Input blocking must prevent a physical Draw request.");
            input.BlockInput = false;
            manager.SetGameplayPaused(true);
            Require(!player.ApplyDraw(), "Ordinary pause must prevent Draw entry.");
            manager.SetGameplayPaused(false);
            skills.CancelDrawCooldown = 0.12f;
            Require(player.ApplyDraw(), "A ready Character must enter Draw after its common restrictions clear.");
            player.CancelDraw();
            Require(skills.DrawCooldownRemaining > 0f && !player.ApplyDraw(), "The Skill cooldown must prevent immediate reentry.");
            manager.SetGameplayPaused(true);
            yield return Frames(1); // 정지 요청이 해당 프레임의 시간 샘플에 반영되는 경계 대기
            float frozenCooldown = skills.DrawCooldownRemaining;
            yield return RealSeconds(0.1f); // 게임 시간 정지 중 능력 쿨타임이 줄지 않는지 관찰
            Require(skills.DrawCooldownRemaining == frozenCooldown, "Draw cooldown must use game time.");
            manager.SetGameplayPaused(false);
            yield return Until(() => skills.DrawCooldownRemaining <= 0f, "draw cooldown completion"); // 일반 게임 시간이 재개된 뒤 짧은 대기 종료 확인
            skills.CancelDrawCooldown = 0f;
            passed("F-P04 Character restrictions and Skill cooldown prevent invalid entry and cooldowns stop with game time");

            // 컷씬·차단·비활성화·실제 사망은 다음 프레임 전 전용 상태를 즉시 복구
            yield return EnterDraw(keyboard, skills); // 컷씬 중단을 받을 진행 중 Draw 준비
            player.IsCutScene = true;
            AssertRestored(manager, input, actions, ui, gameCamera, rendering);
            player.IsCutScene = false;
            yield return EnterDraw(keyboard, skills); // 외부 입력 차단 중단을 받을 Draw 준비
            input.BlockInput = true;
            AssertRestored(manager, input, actions, ui, gameCamera, rendering);
            input.BlockInput = false;
            yield return EnterDraw(keyboard, skills); // 입력 컴포넌트 비활성화 중단을 받을 Draw 준비
            input.enabled = false;
            AssertRestored(manager, input, actions, ui, gameCamera, rendering);
            input.enabled = true;
            yield return EnterDraw(keyboard, skills); // 능력 컴포넌트 비활성화 중단을 받을 Draw 준비
            skills.enabled = false;
            AssertRestored(manager, input, actions, ui, gameCamera, rendering);
            skills.enabled = true;
            yield return EnterDraw(keyboard, skills); // Player 컴포넌트 비활성화 중단을 받을 Draw 준비
            player.enabled = false;
            AssertRestored(manager, input, actions, ui, gameCamera, rendering);
            player.enabled = true;
            yield return EnterDraw(keyboard, skills); // 실제 치명 피해의 OnDie 복구를 받을 Draw 준비
            player.IsInvulnerable = false;
            Require(player.ApplyDamage(new DamageInstance(manager.CurrentBoss, player.HP + 1f, DamageType.Effect, 9101, false)), "Draw must receive the actual lethal damage used for death cleanup.");
            AssertRestored(manager, input, actions, ui, gameCamera, rendering);
            Require(player.IsDead && !skills.HasLine && skills.Drawer.PointCount == 0 && !vfx.LineObject.activeSelf,
                "Death must reset the confirmed Line, editing points and their visuals.");
            player.InitializeHealth(health);
            yield return Frames(2); // 체력 복원만으로 이전 Draw 의도가 다시 실행되지 않는지 관찰
            Require(!skills.IsDrawing, "Health restoration must not restart old Draw input.");
            passed("F-P04 cutscene, block, component disable and real death immediately restore input, time, UI and camera");

            // Draw가 끝나도 외부 pause는 유지하고 일반 pause 해제로 Draw 정지를 덮지 않는지 확인
            yield return EnterDraw(keyboard, skills); // 일반 정지와 중첩할 Draw 정지 준비
            manager.SetGameplayPaused(true);
            player.CancelDraw();
            Require(!manager.IsDrawPaused && manager.IsGameplayPaused && Time.timeScale == 0f, "Ending Draw must not release an ordinary pause owned elsewhere.");
            manager.SetGameplayPaused(false);
            yield return EnterDraw(keyboard, skills); // Draw 정지가 살아 있는 동안 일반 정지 해제 경계 확인
            manager.SetGameplayPaused(false);
            Require(manager.IsDrawPaused && Time.timeScale == 0f, "Clearing ordinary pause must preserve Draw's own stop request.");
            input.ClearPlayer();
            AssertRestored(manager, input, actions, ui, gameCamera, rendering);
            Require(input.Player == null, "Clearing the input target must finish Draw before dropping the Player reference.");
            input.SetPlayer(player);
            input.BlockInput = false;
            passed("F-P04 independent pause ownership survives nested transitions and clearing the input target cleans up Draw");
        }
        finally
        {
            // 전용 상태·입력 수집을 정리하고 기존 세션 연결과 일반 시간을 복원
            attackAction.performed -= countAttack; // Draw 클릭의 일반 공격 누출 수집 종료
            input.enabled = true;
            skills.enabled = true;
            player.enabled = true;
            player.IsCutScene = false;
            player.IsDead = false;
            player.IsInvulnerable = invulnerable;
            skills.ResetSkills();
            player.ResetMotion();
            if (input.Player != player) input.SetPlayer(player);
            input.SetDrawMode(false);
            manager.SetDrawPaused(false);
            manager.SetGameplayPaused(paused);

            // Inspector 튜닝값·기존 확정 Line·카메라·HUD·위치와 입력 맵 복구
            skills.LineLength = lineLength;
            skills.LineWidth = lineWidth;
            skills.CancelDrawCooldown = cancelCooldown;
            skills.ConfirmSlashCooldown = slashCooldown;
            skills.LineLifetime = lifetime;
            skills.SetLine(previousLine);
            player.HP = health;
            player.SetAimPosition(aim);
            SetPosition(body, position);
            ui.SetPlayerHudVisible(playerHud);
            ui.SetBossHudVisible(bossHud);
            gameCamera.Restore();
            gameCamera.Follow(cameraTarget);
            gameCamera.FollowDuration = followDuration;
            camera.orthographicSize = zoom;
            camera.transform.position = cameraPosition;
            actions.SwitchCurrentActionMap(actionMap);
            input.BlockInput = blocked;
            Keys(keyboard);
            MouseInput(mouse, camera, position + Vector2.down, false);
        }
    }

    /// <summary>공통 서비스가 Draw 이전의 일반 상태로 돌아왔는지 확인</summary>
    private static void AssertRestored(GameManager manager, PlayerInputController input, PlayerInput actions, UIController ui, GameCamera camera, CharacterRendering rendering)
    {
        Require((input.Player == null || !input.Player.IsDrawing) && !manager.IsDrawPaused && !input.IsDrawInput && actions.currentActionMap.name == "Player" && !ui.IsDrawMode && !camera.IsDrawMode && !rendering.IsDrawHighlighted,
            "Draw termination must immediately restore its action map, pause ownership, UI, camera and highlighting.");
        Require(Time.timeScale == (manager.IsGameplayPaused ? 0f : 1f), "Draw cleanup must preserve the ordinary pause owner.");
    }

    /// <summary>표현 프리팹의 실제 선 끝점이 확정 기하와 같은 월드 좌표인지 확인</summary>
    private static void AssertPath(GameObject visual, Line line)
    {
        // 실제 렌더러의 월드 시작점·끝점과 확정 기하 비교
        LineRenderer renderer = visual.GetComponentInChildren<LineRenderer>(true);
        Require(renderer != null && renderer.positionCount >= 2, "The temporary line prefab must render an actual path.");
        Vector3 first = renderer.GetPosition(0);
        Vector3 last = renderer.GetPosition(renderer.positionCount - 1);
        if (!renderer.useWorldSpace)
        {
            first = renderer.transform.TransformPoint(first);
            last = renderer.transform.TransformPoint(last);
        }
        Require(Vector2.Distance(first, line.StartPoint) < 0.002f && Vector2.Distance(last, line.EndPoint) < 0.002f,
            "The visual line must match the fixed world-space start and end points.");

        // 회전·배율을 반영한 실제 메시를 선의 수직축에 투영해 표시 폭 확인
        var mesh = new Mesh();
        try
        {
            renderer.BakeMesh(mesh, GameCamera.Instance.Camera, true);
            Vector3[] vertices = mesh.vertices;
            Require(vertices.Length >= 4, "The temporary LineRenderer must produce visible ribbon geometry.");
            Vector2 perpendicular = new Vector2(-line.Direction.y, line.Direction.x);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (Vector3 vertex in vertices)
            {
                float projection = Vector2.Dot(vertex, perpendicular);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
            float actualWidth = maximum - minimum;
            Require(Mathf.Abs(actualWidth - line.Width) < 0.005f,
                $"The rendered ribbon width must match Line.Width after scale and rotation: expected={line.Width}, actual={actualWidth}.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    /// <summary>확정 Line 교체 여부를 값 타입의 실제 기하로 비교</summary>
    private static bool SameLine(Line left, Line right)
    {
        return Vector2.Distance(left.StartPoint, right.StartPoint) < 0.001f && Vector2.Distance(left.Direction, right.Direction) < 0.001f
            && Mathf.Abs(left.Length - right.Length) < 0.001f && Mathf.Abs(left.Width - right.Width) < 0.001f;
    }

    /// <summary>이전 키를 해제한 뒤 실제 Shift 입력으로 Draw 전환</summary>
    private static IEnumerator EnterDraw(Keyboard keyboard, SkillController skills)
    {
        Keys(keyboard);
        yield return Frames(2); // 이전 키의 canceled 콜백이 처리될 때까지 대기
        Keys(keyboard, Key.LeftShift);
        yield return Until(() => skills.IsDrawing, "Draw entry"); // 실제 입력이 전용 편집을 시작할 때까지 대기
        Keys(keyboard);
        yield return Frames(2); // 진입 Shift를 해제해 이후 독립 입력 준비
    }

    /// <summary>검사 장치의 한 키 누름과 해제를 렌더 프레임에 걸쳐 전달</summary>
    private static IEnumerator Press(Keyboard keyboard, Key key)
    {
        Keys(keyboard, key);
        yield return Frames(2); // 실제 액션 콜백과 입력 컨트롤러의 요청 처리 대기
        Keys(keyboard);
        yield return Frames(2); // 다음 독립 입력을 위한 해제 콜백 대기
    }

    /// <summary>현재 카메라로 투영한 월드 위치에 실제 M1 누름·해제 전달</summary>
    private static IEnumerator Click(Mouse mouse, Camera camera, Vector2 world)
    {
        MouseInput(mouse, camera, world, true);
        yield return Frames(2); // 점 선택 누름이 Draw 입력으로 전달될 때까지 대기
        MouseInput(mouse, camera, world, false);
        yield return Frames(2); // 다음 클릭을 독립 요청으로 만들기 위한 해제 대기
    }

    /// <summary>검사용 키보드의 현재 전체 키 상태 전달</summary>
    private static void Keys(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    /// <summary>Draw 전용 커서와 M1 버튼 상태를 같은 장치 이벤트로 전달</summary>
    private static void MouseInput(Mouse mouse, Camera camera, Vector2 world, bool held)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(world) }.WithButton(MouseButton.Left, held));
    }

    /// <summary>검사 위치 변경 때 Transform과 물리 좌표를 함께 적용</summary>
    private static void SetPosition(Rigidbody2D body, Vector2 position)
    {
        body.transform.position = new Vector3(position.x, position.y, body.transform.position.z);
        body.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>정지된 게임 시간과 별개로 지정한 렌더 프레임 진행 관찰</summary>
    private static IEnumerator Frames(int count)
    {
        int frame = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= frame, "render frames"); // Draw 중에도 진행하는 Input·UI·카메라 프레임 대기
    }

    /// <summary>게임 시간 정지 중 실제 시간으로 상태 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= end, "real seconds"); // 정지 시간에 영향받지 않는 관찰 구간 대기
    }

    /// <summary>기존 Editor 검사 실행기에서 상태 전환과 대기 제한 확인</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P04 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>Draw 전환·표현·복구의 계약 위반을 검사 보고서로 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
