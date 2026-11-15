using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>두 점 편집의 입력 경계와 고정 길이 경로의 기하 규칙 확인</summary>
public static class PlayerLineEditingChecks
{
    /// <summary>실제 Draw의 미완 확정·초기화와 순수 편집 도구의 좌표 경계 확인</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, Keyboard keyboard, Action<string> passed)
    {
        // 확정 경로가 없는 검사 경계와 복원할 세션·튜닝값 보관
        PlayerCharacter player = manager.CurrentPlayer;
        SkillController skills = player.Skills;
        Require(!player.CurrentLine.HasValue && !skills.IsDrawing, "Editing checks require a clean confirmed-Line boundary.");
        float lifetime = skills.LineLifetime;
        float cancelCooldown = skills.CancelDrawCooldown;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        try
        {
            // 기존 Player 경로와 독립된 편집을 준비하고 이전 검사 입력 해제
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            skills.ResetSkills();
            skills.LineLifetime = 60f;
            skills.CancelDrawCooldown = 0f;
            Keys(keyboard);
            yield return Frames(2); // 이전 검사 키 해제가 실제 입력에 반영될 때까지 대기
            Line stored = new Line(player.transform.position, Vector2.right, 6f, 0.25f);
            skills.SetLine(stored);
            Require(player.ApplyDraw(), "A ready Player must enter the editing boundary check.");

            // 유효하지 않은 좌표와 한 점 F 요청은 임시 점·기존 확정 경로 보존
            Vector2 start = (Vector2)player.transform.position + Vector2.up;
            Require(!skills.PlacePoint(new Vector2(float.NaN, 0f)) && !skills.PlacePoint(new Vector2(0f, float.PositiveInfinity))
                && skills.Drawer.PointCount == 0 && !skills.Drawer.CanConfirm, "Non-finite first points must not create editing data.");
            Require(skills.PlacePoint(start) && skills.Drawer.PointCount == 1, "One finite point must be accepted.");
            Require(!skills.PlacePoint(start) && !skills.PlacePoint(new Vector2(float.NegativeInfinity, 1f))
                && !skills.PlacePoint(new Vector2(1f, float.NaN)) && skills.Drawer.PointCount == 1,
                "Coincident and non-finite second points must preserve the first point.");
            yield return Press(keyboard, Key.F); // 실제 F 입력이 한 점 경로를 확정하지 못하는지 관찰
            Require(skills.IsDrawing && skills.Drawer.PointCount == 1 && !skills.Drawer.CanConfirm && SameLine(player.CurrentLine.Value, stored),
                "F with only one point must preserve editing and the existing Player Line.");

            // 두 점 이후의 추가 입력을 무시하고 R은 이번 편집만 초기화
            Require(skills.PlacePoint(start + Vector2.left * 2f) && skills.Drawer.CanConfirm, "A distinct second point must enable confirmation.");
            Line preview = skills.Drawer.PreviewLine.Value;
            Require(!skills.PlacePoint(start + Vector2.down * 8f) && skills.Drawer.PointCount == 2
                && SameLine(skills.Drawer.PreviewLine.Value, preview), "A third point must not change the selected direction.");
            yield return Press(keyboard, Key.R); // 실제 R 입력이 기존 Player 경로와 별개로 편집점만 지우는지 관찰
            Require(skills.IsDrawing && skills.Drawer.PointCount == 0 && !skills.Drawer.PreviewLine.HasValue
                && SameLine(player.CurrentLine.Value, stored), "Reset must clear only temporary geometry and preserve the Player Line.");
            player.CancelDraw();
            Require(SameLine(player.CurrentLine.Value, stored), "Cancelling the reset edit must also preserve the Player Line.");
            passed("F-P05 invalid points, one-point F and third-point input preserve editing rules while R retains the Player-owned Line");

            // 네 사분면과 서로 다른 클릭 거리에서도 맵 밖 시작점·고정 길이 유지
            LineDrawer drawer = new LineDrawer();
            Vector2 outside = new Vector2(10000f, -10000f);
            Vector2[] directions = { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) };
            foreach (Vector2 direction in directions)
            {
                Require(drawer.Begin(6f, 0.25f) && !drawer.Confirm(out _) && drawer.PointCount == 0 && drawer.IsDrawing,
                    "Zero-point confirmation must fail without ending editing.");
                Require(drawer.PlacePoint(outside) && !drawer.Confirm(out _) && drawer.PointCount == 1 && drawer.IsDrawing,
                    "One-point confirmation must fail without ending editing.");
                drawer.UpdatePreview(outside + direction * 0.25f);
                Line near = drawer.PreviewLine.Value;
                drawer.UpdatePreview(new Vector2(float.NaN, 0f));
                drawer.UpdatePreview(new Vector2(0f, float.PositiveInfinity));
                Require(SameLine(drawer.PreviewLine.Value, near), "Invalid cursor values must not poison a valid preview.");
                Line far = default;
                Require(drawer.PlacePoint(outside + direction * 20f) && drawer.Confirm(out far), "Two finite points must confirm in every quadrant.");
                Require(SameLine(near, far) && far.StartPoint == outside && Vector2.Distance(far.Direction, direction.normalized) < 0.001f
                    && Vector2.Distance(far.EndPoint, outside + direction.normalized * 6f) < 0.003f
                    && !drawer.IsDrawing && drawer.PointCount == 0, "Click distance must not change fixed length or clamp coordinates to the map.");
            }

            // 크기가 무효한 편집 설정은 시작 전에 거부하고 다음 정상 편집은 허용
            float[] invalidSizes = { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            foreach (float invalid in invalidSizes)
                Require(!drawer.Begin(invalid, 0.25f) && !drawer.Begin(6f, invalid) && !drawer.IsDrawing,
                    "A non-positive or non-finite length or width must be rejected before editing begins.");
            Require(drawer.Begin(6f, 0.25f), "Valid geometry must remain available after rejected settings.");
            drawer.Cancel();
            passed("F-P05 all direction quadrants keep fixed length across click distances and preserve unclamped world coordinates while invalid sizes are rejected");
        }
        finally
        {
            // 임시 검사 경로·입력을 정리하고 기존 시간·튜닝값 복구
            Keys(keyboard);
            skills.ResetSkills();
            skills.LineLifetime = lifetime;
            skills.CancelDrawCooldown = cancelCooldown;
            manager.SetDrawPaused(false);
            manager.SetGameplayPaused(paused);
            input.BlockInput = blocked;
        }
    }

    /// <summary>확정 경로의 시작점·방향·크기를 허용 오차 안에서 비교</summary>
    private static bool SameLine(Line first, Line second)
    {
        return Vector2.Distance(first.StartPoint, second.StartPoint) < 0.001f && Vector2.Distance(first.Direction, second.Direction) < 0.001f
            && Mathf.Abs(first.Length - second.Length) < 0.001f && Mathf.Abs(first.Width - second.Width) < 0.001f;
    }

    /// <summary>실제 키 누름·해제를 독립 입력으로 전달</summary>
    private static IEnumerator Press(Keyboard keyboard, Key key)
    {
        Keys(keyboard, key);
        yield return Frames(2); // 입력 액션과 Draw 요청 처리가 끝날 때까지 대기
        Keys(keyboard);
        yield return Frames(2); // 다음 요청과 분리할 키 해제 반영 대기
    }

    /// <summary>검사용 키보드의 전체 키 상태 전달</summary>
    private static void Keys(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    /// <summary>게임 시간 정지와 별개로 지정한 렌더 프레임 진행 관찰</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        float deadline = Time.realtimeSinceStartup + 8f;
        while (Time.frameCount < end)
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P05 timed out waiting for input frames.");
            yield return null;
        }
    }

    /// <summary>점 편집·기하 규칙 위반을 기존 검사 보고서에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
