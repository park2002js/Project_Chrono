using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>실제 카메라 갱신으로 Draw의 원작 조준 오프셋과 복귀 경계를 확인</summary>
public static class DrawCameraChecks
{
    private static readonly MethodInfo CameraUpdate = typeof(GameCamera).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic); // 실제 위치 계산을 같은 프레임에서 반복 호출할 갱신 함수

    /// <summary>일반 추적의 실제 접근 시간과 컷씬·정지·즉시 배치를 좁은 범위에서 검사</summary>
    public static IEnumerator RunFollow(GameManager manager, Action<string> passed)
    {
        // 같은 씬 수명 동안 유지할 카메라와 실제 연출·입력 대상 한 번 확보
        GameCamera gameCamera = GameCamera.Instance;
        Camera camera = gameCamera.Camera;
        PlayerCharacter player = manager.CurrentPlayer;
        BossCharacter boss = manager.CurrentBoss;
        CutsceneController cutscene = manager.Stage.Cutscene;
        PlayerInputController input = PlayerInputController.Instance;
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        Vector3 playerPosition = player.transform.position;
        Vector3 bossPosition = boss.transform.position;
        Vector3 cameraPosition = camera.transform.position;
        Transform cameraTarget = gameCamera.Target;
        Vector2 limits = gameCamera.VerticalLimits;
        float zoom = camera.orthographicSize;
        float capture = Time.captureDeltaTime;
        bool paused = manager.IsGameplayPaused;
        bool inputEnabled = input.enabled;
        bool spawning = player.Spawner.enabled;
        Coroutine playing = null;
        GameObject focus = new GameObject("CameraFollowVerificationTarget");
        FollowReport report = new FollowReport { smoothTime = gameCamera.FollowDuration };
        try
        {
            // 실제 프리팹의 설정을 확인하고 기존 Draw 카메라 다섯 경계 재사용
            GameCamera prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Startup/WorldCamera.prefab").GetComponent<GameCamera>();
            Require(Mathf.Abs(gameCamera.FollowDuration - 0.1283f) < 0.000001f && Mathf.Abs(prefab.FollowDuration - 0.1283f) < 0.000001f,
                "The runtime camera and WorldCamera prefab must both use raw SmoothDamp time.1283.");
            Require(!cutscene.IsPlaying && !gameCamera.IsDrawMode && !gameCamera.IsSlashMode && !gameCamera.IsDeathMode,
                "Follow timing must begin without another camera owner.");
            Run(gameCamera, passed);

            // 원본 맵의 충돌·조우·세로 경계와 분리하고 실제 LateUpdate만 관찰
            input.enabled = false;
            player.Spawner.enabled = false;
            player.ResetMotion();
            SetPosition(player.transform, new Vector3(40f, 40f, playerPosition.z));
            gameCamera.VerticalLimits = new Vector2(-10000f, 10000f);
            Vector3 origin = new Vector3(40f, 40f, cameraPosition.z);
            focus.transform.position = new Vector3(41f, 40f, 0f);
            foreach (bool freeze in new[] { false, true })
            {
                manager.SetGameplayPaused(freeze);
                foreach (int fps in new[] { 30, 60, 144 })
                {
                    Time.captureDeltaTime = 1f / fps;
                    yield return Frames(2); // 캡처 간격 변경을 반영하되 실제 unscaled 간격은 측정값으로 사용
                    camera.transform.position = origin;
                    gameCamera.Follow(focus.transform, false);
                    Require(Vector3.Distance(camera.transform.position, origin) < 0.000001f,
                        "Follow(false) must retain its current position before the next camera update.");
                    FollowSample sample = new FollowSample { scenario = freeze ? "paused Follow(false)" : "ordinary Follow(false)", requestedFps = fps };
                    yield return MeasureApproach(gameCamera, focus.transform.position, sample); // 실제 카메라가1월드 이동의90%에 도달할 때까지 대기
                    report.samples.Add(sample);
                }
            }
            passed("Ordinary and paused Follow(false) reach90% near.25 unscaled seconds at30/60/144 capture requests, with actual sample intervals recorded");

            // snap 요청과 Draw는 일반 지연 설정을 거치지 않고 즉시 표시
            gameCamera.Follow(player.transform, true);
            Require(Vector2.Distance(camera.transform.position, player.transform.position) < 0.000001f,
                "Follow(true) must snap immediately even while timeScale is0.");
            gameCamera.SetDrawAim(true, (Vector2)player.transform.position + Vector2.right * camera.aspect * 1000000f);
            camera.orthographicSize = 6.5f;
            Tick(gameCamera);
            At(camera, (Vector2)player.transform.position + Vector2.right * 3.25f,
                "Draw must still apply its mouse offset in one camera update while paused.");
            gameCamera.Restore();
            passed("Follow(true) still snaps immediately and paused Draw still applies its position without ordinary follow delay");

            // 실제 컷씬의 진입·자동 완료·복귀를 기존 정지를 유지한 채 실행
            Time.captureDeltaTime = 1f / 60f;
            yield return Frames(2); // 컷씬 표본의 실제 시간 간격이 안정될 때까지 대기
            SetPosition(boss.transform, new Vector3(41f, 40f, bossPosition.z));
            gameCamera.Follow(player.transform, true);
            playing = cutscene.StartCoroutine(cutscene.Play(player, boss, CutsceneKind.BossEncounter, 1f)); // 실제 컷씬의 대상 변경·정지·HUD 복구 코루틴 실행
            Require(cutscene.IsPlaying && Time.timeScale == 0f && gameCamera.Target == boss.transform,
                "The real cutscene must focus the Boss and preserve unscaled camera updates while gameplay is paused.");
            FollowSample entering = new FollowSample { scenario = "Cutscene Play to Boss at timeScale0", requestedFps = 60 };
            yield return MeasureApproach(gameCamera, boss.transform.position, entering); // 실제 Boss 연출의 카메라 접근90% 시각 관찰
            report.samples.Add(entering);
            yield return Until(() => !cutscene.IsPlaying, "automatic cutscene completion"); // unscaled 연출 시간이 끝나 원래 추적 대상을 복구할 때까지 대기
            Require(cutscene.WasCompleted && gameCamera.Target == player.transform && manager.IsGameplayPaused && Time.timeScale == 0f,
                "Automatic cutscene completion must restore the Player target and preserve the pre-existing pause.");
            FollowSample returning = new FollowSample { scenario = "Cutscene return to Player at timeScale0", requestedFps = 60 };
            yield return MeasureApproach(gameCamera, player.transform.position, returning); // 컷씬 복귀도 같은 일반 추적 설정으로90% 접근하는지 관찰
            report.samples.Add(returning);
            Require(GameCamera.Instance == gameCamera && Mathf.Abs(gameCamera.FollowDuration - report.smoothTime) < 0.000001f,
                "The cutscene must retain the same scene camera and shared follow setting.");
            passed("Actual Cutscene Play and automatic return share the same.25-second90% follow while timeScale0 preserves the existing pause");
        }
        finally
        {
            // 임시 추적 대상과 연출만 닫고 기존 씬의 위치·입력·시간 복구
            if (playing != null) cutscene.StopCoroutine(playing); // 검사 중단 때 남은 임시 컷씬 코루틴 종료
            cutscene.Cancel();
            manager.SetGameplayPaused(paused);
            gameCamera.Restore();
            gameCamera.VerticalLimits = limits;
            camera.orthographicSize = zoom;
            gameCamera.Follow(cameraTarget);
            camera.transform.position = cameraPosition;
            SetPosition(player.transform, playerPosition);
            SetPosition(boss.transform, bossPosition);
            playerBody.linearVelocity = Vector2.zero;
            input.enabled = inputEnabled;
            player.Spawner.enabled = spawning;
            Time.captureDeltaTime = capture;
            UnityEngine.Object.Destroy(focus);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PlayerCameraFollow-metrics.json", JsonUtility.ToJson(report, true));
        }
    }

    /// <summary>실제 unscaled 시계와 위치 표본으로90% 교차 구간·보간 시각 기록</summary>
    private static IEnumerator MeasureApproach(GameCamera gameCamera, Vector3 target, FollowSample sample)
    {
        // 세로 clamp를 제외한 같은 평면 거리에서 정규화한 접근률 계산
        Vector2 start = gameCamera.transform.position;
        float initial = Vector2.Distance(start, target);
        Require(initial > 0.5f, "Follow timing must begin with a nontrivial target displacement.");
        sample.initialDistance = initial;
        double began = Time.unscaledTimeAsDouble;
        double previousTime = 0d;
        float previousProgress = 0f;
        double deadline = EditorApplication.timeSinceStartup + 10d;
        while (true)
        {
            yield return null;
            double elapsed = Time.unscaledTimeAsDouble - began;
            if (elapsed <= previousTime) continue;
            float progress = 1f - Vector2.Distance(gameCamera.transform.position, target) / initial;
            sample.maximumSampleInterval = Math.Max(sample.maximumSampleInterval, elapsed - previousTime);
            sample.observedSamples++;
            if (progress > 0f && sample.firstPositiveTime == 0d) sample.firstPositiveTime = elapsed;
            if (progress >= 0.9f)
            {
                sample.lowerTime = previousTime;
                sample.upperTime = elapsed;
                sample.interpolatedTime = previousTime + (elapsed - previousTime) * (0.9f - previousProgress) / (progress - previousProgress);
                Require(Math.Abs(sample.interpolatedTime - 0.25d) <= sample.maximumSampleInterval + 0.005d,
                    sample.scenario + "90% follow differs: t=" + sample.interpolatedTime + ", maximum sample=" + sample.maximumSampleInterval);
                Require(sample.firstPositiveTime <= sample.maximumSampleInterval * 2.1d,
                    "Ordinary follow must start without an additional delay beyond one observed camera frame.");
                yield break;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Camera follow did not reach90%: " + sample.scenario);
            previousTime = elapsed;
            previousProgress = progress;
        }
    }

    /// <summary>물리 몸체가 있는 대상도 Transform과 같은 검사 위치로 이동</summary>
    private static void SetPosition(Transform target, Vector3 position)
    {
        target.position = position;
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body != null) body.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>실제 Player Loop에서 지정한 렌더 프레임 진행 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "camera setup frames"); // 시간 설정 변경 후 실제 게임 프레임 진행 대기
    }

    /// <summary>실제 연출·프레임 상태가 충족될 때까지 제한 시각 안에서 관찰</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        double end = EditorApplication.timeSinceStartup + 15d;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Camera check timed out: " + description);
            yield return null;
        }
    }

    /// <summary>한 번의 카메라 접근 시간에서 관측한 표본 구간</summary>
    [Serializable]
    private sealed class FollowSample
    {
        public string scenario; // 일반·정지·컷씬 진입·복귀의 구분
        public int requestedFps; // Time.captureDeltaTime으로 요청한 프레임 간격 기준
        public float initialDistance; // 접근률을 정규화할 최초 평면 거리
        public int observedSamples; // 실제 위치와 unscaled 시각을 함께 읽은 횟수
        public double maximumSampleInterval; // 실제 관측 시계에서 가장 긴 표본 간격
        public double firstPositiveTime; // 처음 위치 변화가 관측된 unscaled 경과시간
        public double lowerTime; //90% 직전 마지막 표본의 unscaled 경과시간
        public double upperTime; //90% 이상이 된 첫 표본의 unscaled 경과시간
        public double interpolatedTime; // 두 실제 표본 사이에서 선형 보간한90% 접근 시각
    }

    /// <summary>원시 SmoothDamp 설정과 실제 추적 시간의 독립 결과</summary>
    [Serializable]
    private sealed class FollowReport
    {
        public float smoothTime; // 실제 프리팹에서 읽은 Unity SmoothDamp 원시 설정
        public List<FollowSample> samples = new List<FollowSample>(); // 일반·정지·컷씬 진입과 복귀의 실제 접근 시간 표본
    }

    /// <summary>일반 추종을 보존하면서 Draw의 거리·화면비·상한·재귀 수렴과 복구 확인</summary>
    public static void Run(GameCamera gameCamera, Action<string> passed)
    {
        // 진행 중인 연출을 건드리지 않고 검사 후 되돌릴 카메라 상태 보관
        Require(!gameCamera.IsDrawMode && !gameCamera.IsSlashMode && !gameCamera.IsDeathMode && gameCamera.Target != null,
            "Camera checks must begin with an ordinary Player target.");
        Camera camera = gameCamera.Camera;
        Transform target = gameCamera.Target;
        Vector3 position = camera.transform.position;
        Vector2 limits = gameCamera.VerticalLimits;
        float zoom = camera.orthographicSize;
        float aspect = camera.aspect;
        float drawZoom = gameCamera.DrawZoom;
        float followDuration = gameCamera.FollowDuration;
        Vector2 player = target.position;
        try
        {
            // 일반 이동은 여전히 중간 위치를 거치는 SmoothDamp인지 확인
            gameCamera.VerticalLimits = new Vector2(-10000f, 10000f);
            gameCamera.FollowDuration = 0.5f;
            gameCamera.Follow(target);
            camera.transform.position = new Vector3(player.x + 8f, player.y, position.z);
            Tick(gameCamera);
            Require(camera.transform.position.x > player.x && camera.transform.position.x < player.x + 8f,
                "Ordinary follow must remain smoothed rather than snapping to the Player.");
            gameCamera.Follow(target);
            passed("F-P04 camera keeps ordinary SmoothDamp separate from immediate mouse tracking");

            // 잘못된 조준값은 모드 진입이나 이전 정상 커서의 오염 없이 거부
            gameCamera.SetDrawAim(true, new Vector2(float.NaN, 0f));
            Require(!gameCamera.IsDrawMode, "Invalid aim must not begin Draw camera ownership.");

            // Draw 진입 전 줌을 보관하고 화면비 보정된 1월드 범위와 극소 이동 제거 확인
            gameCamera.DrawZoom = 6.5f;
            gameCamera.SetDrawAim(true, player);
            camera.orthographicSize = 6.5f;
            camera.aspect = 2f;
            gameCamera.SetDrawAim(true, player + Vector2.right * 1.8f);
            Tick(gameCamera);
            At(camera, player, "The aspect-corrected one-world-unit dead zone must keep the Player centered.");
            gameCamera.SetDrawAim(true, player + Vector2.right * 2.0002f);
            Tick(gameCamera);
            At(camera, player, "Offsets below one sixty-fourth of a world unit must remain centered.");
            passed("F-P04 camera has the ETG one-unit aim dead zone and subpixel offset cutoff");

            // 같은 월드 입력도 화면비에 따라 달라지고 원작 축별 최대 이동을 넘지 않는지 확인
            camera.aspect = 1f;
            gameCamera.SetDrawAim(true, player + Vector2.right * 13f);
            Tick(gameCamera);
            At(camera, player + Vector2.right * 3.25f, "Square-view horizontal aim must stop at half the orthographic size.");
            camera.aspect = 2f;
            gameCamera.SetDrawAim(true, player + Vector2.right * 13f);
            Tick(gameCamera);
            At(camera, player + Vector2.right * (6.5f / 3f), "Wide-view horizontal aim must use aspect-corrected distance.");
            gameCamera.SetDrawAim(true, player + new Vector2(1000000f, 1000000f));
            Tick(gameCamera);
            At(camera, player + new Vector2(3.25f, 3.25f), "Extreme cursor coordinates must remain capped on both axes.");
            gameCamera.SetDrawAim(true, player + Vector2.left * 1000000f);
            Tick(gameCamera);
            At(camera, player + Vector2.left * 3.25f, "Mouse reversal must take effect in the same camera update without lingering velocity.");
            gameCamera.SetDrawAim(true, new Vector2(float.PositiveInfinity, 0f));
            Tick(gameCamera);
            At(camera, player + Vector2.left * 3.25f, "Invalid aim must preserve the last valid Draw camera position.");
            passed("F-P04 camera corrects aspect, caps distant aim and reverses without mouse smoothing inertia");

            // 고정 화면커서 재투영을 30·60·144회율의 0.2초분 계산 횟수로 비교
            camera.aspect = 16f / 9f;
            foreach (int callbacksPerSecond in new[] { 30, 60, 144 })
            {
                camera.transform.position = new Vector3(player.x, player.y, position.z);
                int callbacks = Mathf.CeilToInt(callbacksPerSecond * 0.2f);
                float previousOffset = 0f;
                for (int i = 0; i < callbacks; i++)
                {
                    Vector3 cursor = camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, target.position.z - position.z));
                    gameCamera.SetDrawAim(true, cursor);
                    Tick(gameCamera);
                    float offset = camera.transform.position.x - player.x;
                    Require(offset >= previousOffset - 0.0001f && offset <= 8f / 3f + 0.001f,
                        "A fixed screen-edge cursor must converge monotonically within its expected offset.");
                    previousOffset = offset;
                }
                Require(Mathf.Abs(previousOffset - 8f / 3f) < 0.002f,
                    "Fixed screen-edge cursor projection must settle in the bounded ETG equilibrium.");
            }
            passed("F-P04 camera fixed-screen recursion converges at 30, 60 and 144 callback-count samples (not a performance measurement)");

            // 확대한 화면 전체가 기존 맵 위아래 범위 안에 남는지 확인
            gameCamera.VerticalLimits = new Vector2(player.y - 8f, player.y + 8f);
            gameCamera.SetDrawAim(true, player + Vector2.up * 1000000f);
            Tick(gameCamera);
            Require(camera.transform.position.y + camera.orthographicSize <= gameCamera.VerticalLimits.y + 0.001f,
                "Draw zoom must not reveal space beyond the upper map boundary.");
            gameCamera.SetDrawAim(true, player + Vector2.down * 1000000f);
            Tick(gameCamera);
            Require(camera.transform.position.y - camera.orthographicSize >= gameCamera.VerticalLimits.x - 0.001f,
                "Draw zoom must not reveal space beyond the lower map boundary.");
            gameCamera.Restore();
            Require(!gameCamera.IsDrawMode && gameCamera.Target == target && camera.orthographicSize == zoom,
                "Draw exit must restore the target and zoom captured before entry.");
            passed("F-P04 camera preserves map boundaries at Draw zoom and restores its original target and zoom");
        }
        finally
        {
            // 후속 실제 입력 검사가 원래 세션 상태에서 시작하도록 모든 변경 복구
            gameCamera.Restore();
            gameCamera.VerticalLimits = limits;
            gameCamera.DrawZoom = drawZoom;
            gameCamera.FollowDuration = followDuration;
            camera.aspect = aspect;
            camera.orthographicSize = zoom;
            gameCamera.Follow(target);
            camera.transform.position = position;
        }
    }

    /// <summary>Unity가 사용하는 실제 LateUpdate를 추가 프레임 대기 없이 호출</summary>
    private static void Tick(GameCamera gameCamera)
    {
        CameraUpdate.Invoke(gameCamera, null);
    }

    /// <summary>카메라 깊이와 별개로 실제 평면 초점이 기대 좌표에 즉시 도달했는지 확인</summary>
    private static void At(Camera camera, Vector2 expected, string description)
    {
        Require(Vector2.Distance(camera.transform.position, expected) < 0.001f, description);
    }

    /// <summary>검사 실패를 기존 Play Mode 결과 장치에 전달</summary>
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }
}
