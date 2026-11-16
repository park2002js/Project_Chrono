using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실제 Slash의 구간별 자세·실제 시간·지연 판정·월드 왜곡과 UI 보존 검사</summary>
public static class SlashPresentationChecks
{
    /// <summary>현재 완성 맵에서 새 연출을 실행하고 단계별 관측·화면 캡처·중단 복원 확인</summary>
    public static IEnumerator Run(GameManager manager, Action<string> passed)
    {
        // 실제 서비스와 조정값을 보관하고 임시 피격·시각 표본만 준비
        PlayerCharacter player = manager.CurrentPlayer;
        SkillController skill = player.Skills;
        CharacterRendering rendering = player.GetComponent<CharacterRendering>();
        SlashPresentationFx fx = player.GetComponent<SlashPresentationFx>();
        DrawVfx draw = player.GetComponent<DrawVfx>();
        GameCamera camera = GameCamera.Instance;
        UIController ui = UIController.Instance;
        float speed = skill.SlashPlaybackSpeed;
        float spaceDuration = fx.SpaceSlashDuration;
        float attack = player.AD;
        string directory = Path.GetFullPath("Logs/SlashPresentation");
        Directory.CreateDirectory(directory);
        GameObject fixture = new GameObject("SlashPresentationFixtures");
        Texture2D markerTexture = Texture2D.whiteTexture;
        Sprite markerSprite = Sprite.Create(markerTexture, new Rect(0, 0, markerTexture.width, markerTexture.height), Vector2.one * 0.5f, markerTexture.width);
        GameObject overlay = CreateOverlay();
        try
        {
            // 원본 배속에서 부분 발도 유지와 실제 시간축을 관찰
            skill.ResetSkills();
            player.ResetMotion();
            player.AD = 10f;
            skill.SlashPlaybackSpeed = 1f;
            fx.SpaceSlashDuration = 1.2f;
            Require(rendering.HasSlashAnimation && fx.BoundCamera == camera.Camera && fx.SheathClip != null && fx.SpaceSlashClip != null,
                "R02 animation, world camera, materials and both synthesized sound drafts must be connected.");
            passed("Slash R02 draw/hold/sheath art, two sound drafts and camera FX are connected");
            camera.Follow(player.transform, true);
            yield return Frames(3); // 맵 경계와 기본 카메라 배율이 안정된 프레임까지 대기
            float normalZoom = camera.Camera.orthographicSize;
            Vector2 center = camera.transform.position;
            Vector2 direction = new Vector2(3f, 1f).normalized;
            Line line = new Line(center - direction * 3f, direction, 6f, 0.25f);
            Character target = CreateTarget(fixture.transform, center);
            GameObject brightPatch = new GameObject("BrightFloorDamageProof", typeof(SpriteRenderer));
            brightPatch.transform.SetParent(fixture.transform);
            brightPatch.transform.position = line.StartPoint + direction * 1.5f;
            brightPatch.transform.localScale = new Vector3(1.4f, 0.8f, 1f);
            SpriteRenderer brightRenderer = brightPatch.GetComponent<SpriteRenderer>();
            brightRenderer.sprite = markerSprite;
            brightRenderer.sortingOrder = 90;
            Vector3 redPosition = center + new Vector2(-1f, 0.15f);
            Vector3 bluePosition = center + new Vector2(1f, -0.15f);
            CreateMarker(fixture.transform, markerSprite, redPosition, Color.red);
            CreateMarker(fixture.transform, markerSprite, bluePosition, Color.blue);
            skill.SetLine(line);
            Require(player.ApplySlash(), "A valid Line must begin the new Slash presentation.");
            Require(skill.SlashPhase == SlashPresentationPhase.FastZoom && rendering.IsSlashAnimation && !rendering.WeaponSprite.enabled
                && !player.CurrentLine.HasValue && manager.IsSlashPaused && Time.timeScale == 0f && player.IsInvulnerable,
                "Start must consume once, hide the separate sword and own time, input and protection.");
            float started = Time.realtimeSinceStartup;
            float frozenTime = Time.time;
            passed("Slash starts rapid zoom and partial draw under a separate gameplay pause");
            RequirePendingLine(draw, line);

            // 빠른 확대 종료에 꺼낸 자세로 도착한 뒤 같은 Sprite를 유지하며 느리게 접근
            yield return Until(() => skill.SlashPhase == SlashPresentationPhase.SlowZoomHold, "slow zoom hold"); // 빠른 확대·부분 발도의 동시 완료 대기
            Require(rendering.CurrentState == PlayerSpriteAnimationState.SlashHold && target.HP == 1000f && !fx.IsBladeGlintPlaying,
                "The slow zoom must begin with the sword already partially drawn, without an early glint or hit.");
            Sprite holdSprite = rendering.BodySprite.sprite;
            float fastZoom = camera.Camera.orthographicSize;
            float holdStarted = Time.realtimeSinceStartup;
            yield return RealSeconds(0.25f); // 꺼낸 자세 유지 중 느린 확대 진행 관찰
            Require(rendering.BodySprite.sprite == holdSprite && camera.Camera.orthographicSize < fastZoom && Time.time == frozenTime && target.HP == 1000f,
                "The held body pose must remain fixed while only camera zoom and unscaled time advance.");
            passed("The slow zoom holds the partially drawn pose while gameplay and damage remain frozen");
            RequirePendingLine(draw, line);
            yield return Capture(directory + "/01-held-draw.png"); // 실제 유지 자세와 확대 화면 저장 완료 대기

            // 느린 확대 약0.7초 뒤 칼날 중앙의 빛을 먼저 재생하고 납도로 진행
            yield return Until(() => fx.IsBladeGlintPlaying, "blade center glint"); // 조정된 유지 시간의 끝에서 섬광이 시작될 때까지 대기
            Require(Time.realtimeSinceStartup - holdStarted >= skill.SlashHoldDuration - 0.12f && target.HP == 1000f,
                "Blade glint must follow the configured hold interval and precede damage.");
            GameObject glint = GameObject.Find("SlashBladeGlint");
            Require(glint != null && Vector3.Distance(glint.transform.position, rendering.SlashBladeGlintPosition) < 0.001f,
                "The glint must use the exposed blade center calculated from the R02 hold pose.");
            passed("Blade-center glint follows the hold interval at the actual R02 exposed blade position");
            RequirePendingLine(draw, line);
            yield return Capture(directory + "/02-blade-glint.png"); // 칼날 중앙 섬광의 실제 월드 출력 저장 대기
            yield return Until(() => skill.SlashPhase == SlashPresentationPhase.ImpactWait, "sheath closure"); // 마지막 닫힌 프레임과 납도음 경계 대기
            Require(rendering.CurrentState == PlayerSpriteAnimationState.SlashSheathe && rendering.FrameIndex == 2 && target.HP == 1000f
                && player.GetComponent<AudioSource>().isPlaying && manager.IsSlashPaused,
                "Sheath clack must coincide with the fully closed last frame while gameplay still waits for the delayed slash.");
            float closed = Time.realtimeSinceStartup;
            passed("The last sheath frame and clack coincide before the delayed space slash");
            RequirePendingLine(draw, line);
            yield return RealSeconds(skill.SlashZoomOutDuration + 0.04f); // 납도에서 시작한 부드러운 확대 복귀 완료 관찰
            Require(player.IsSlashing && Mathf.Abs(camera.Camera.orthographicSize - normalZoom) < 0.001f && target.HP == 1000f
                && Time.time == frozenTime, "The camera must return before delayed damage while game time remains frozen.");
            passed("Zoom returns smoothly after sheath closure while the impact delay retains pause and protection");
            RequirePendingLine(draw, line);
            passed("The consumed Line stays visible at its original geometry throughout fast zoom, hold, glint, sheath and impact delay");

            // 공간참 사건에서만 한 번 판정하고 UI와 전투를 복구한 뒤 월드 화면을 절단
            yield return Until(() => !player.IsSlashing, "delayed space slash"); // 납도 후 대기와 마지막 한 번의 실제 판정 완료 대기
            Require(Time.realtimeSinceStartup - closed >= skill.SlashImpactDelay - 0.1f && target.HP == 970f
                && fx.IsSpaceSlashPlaying && SlashPresentationFx.ActiveSpaceSlash == fx && !manager.IsSlashPaused && !camera.IsSlashMode
                && !ui.IsSlashMode && ui.IsPlayerHudVisible && !rendering.IsSlashAnimation && rendering.WeaponSprite.enabled,
                "Only delayed space slash may damage once, start distortion and restore gameplay, HUD, camera and ordinary body rendering.");
            Require(!draw.IsSlashLineVisible && !draw.LineObject.activeSelf,
                "The original map Line must disappear exactly when delayed damage and the yellow space cut begin.");
            Require(Mathf.Abs(Time.realtimeSinceStartup - started - skill.SlashDuration) < 0.25f,
                "The actual presentation duration must follow the sum of its independent unscaled intervals.");
            passed("Delayed space slash applies damage once, plays the slash sound and restores gameplay with HUD visible");
            yield return Capture(directory + "/03-space-cut.png"); // UI를 포함한 최종 화면에서 월드 어긋남 저장 대기
            yield return RealSeconds(0.1f); // 같은 공간 위에서 잔광과 빛 조각이 시간에 따라 변하는지 비교
            yield return Capture(directory + "/03b-space-energy.png"); // 검격 에너지의 후속 프레임 저장 대기
            fx.Cancel();
            yield return Capture(directory + "/04-restored.png"); // 동일 배율·자세의 원상 화면을 비교 표본으로 저장 대기
            CheckImages(directory, camera.Camera, redPosition, bluePosition, direction, line);
            passed("Rendered world markers move in opposite directions along the Line while the overlay UI stays fixed");
            passed("Animated yellow energy spans the original Line over the shifted world and remains readable without a dark gap on a white floor");
            fx.SpaceSlashDuration = spaceDuration;
            yield return CaptureEnergyMotion(fx, line, directory + "/energy-motion", fixture, overlay); // 실제 기본 수명의 검격 VFX 동영상 표본 저장 대기
            fx.SpaceSlashDuration = 1.2f;
            passed("The normal-duration energy VFX is captured over time without replaying damage");
            yield return RealSeconds(0.1f); // 늦은 프레임의 중복 판정이 없는지 확인
            Require(target.HP == 970f, "The completed action must not apply damage a second time.");

            // 각 단계에서 중단하고 다음 실행에 이전 타이머·음원·재질이 남지 않는지 확인
            foreach (SlashPresentationPhase phase in new[] { SlashPresentationPhase.FastZoom, SlashPresentationPhase.SlowZoomHold,
                SlashPresentationPhase.BladeGlint, SlashPresentationPhase.Sheathing, SlashPresentationPhase.ImpactWait })
            {
                skill.SlashPlaybackSpeed = 2f;
                skill.SetLine(line);
                Require(player.ApplySlash(), "A fresh Slash must start for cancellation at " + phase);
                yield return Until(() => skill.SlashPhase == phase, "cancel phase " + phase); // 해당 실제 연출 구간에 진입할 때까지 대기
                RequirePendingLine(draw, line);
                skill.CancelSlash();
                Require(!player.IsSlashing && !rendering.IsSlashAnimation && !manager.IsSlashPaused && !camera.IsSlashMode
                    && !fx.IsBladeGlintPlaying && !fx.IsSpaceSlashPlaying && SlashPresentationFx.ActiveSpaceSlash == null
                    && !draw.IsSlashLineVisible && !draw.LineObject.activeSelf
                    && target.HP == 970f && Mathf.Abs(camera.Camera.orthographicSize - normalZoom) < 0.001f,
                    "Cancellation must restore state and prevent delayed effects or damage at " + phase);
                passed("Slash cancellation restores state and suppresses pending damage at " + phase);
            }

            // 필수 표시 컴포넌트가 중간에 꺼지면 보이지 않는 지연 타격 없이 전체 상태 복구
            skill.SetLine(line);
            Require(player.ApplySlash(), "Disable recovery requires a running Slash.");
            fx.enabled = false;
            yield return Until(() => !player.IsSlashing, "FX disable recovery"); // 표현 중단이 Skill의 정지·보호 복구에 전달될 때까지 대기
            fx.enabled = true;
            Require(!manager.IsSlashPaused && !rendering.IsSlashAnimation && target.HP == 970f,
                "Disabling a required FX component must cancel the pending hit and restore gameplay.");
            rendering.enabled = false;
            skill.SetLine(line);
            Require(!player.ApplySlash() && player.CurrentLine.HasValue, "Disabled body rendering must reject Slash before Line consumption.");
            rendering.enabled = true;
            passed("Required presentation component disable cancels pending damage and rejects incomplete starts");

            // 빠른 전체 재생도 같은 결과로 끝나고 잘못된 시간은 경로 소비 전에 거부
            skill.SlashDuration = 0.12f;
            skill.SetLine(line);
            Require(player.ApplySlash(), "Short playback must start.");
            yield return Until(() => !player.IsSlashing, "short playback completion"); // 여러 경계를 넘는 빠른 재생의 최종 판정 완료 대기
            Require(target.HP == 940f && fx.IsSpaceSlashPlaying, "Short playback must still reach exactly one final hit and screen effect.");
            passed("Short playback crosses all presentation boundaries and still applies one delayed hit");
            skill.CancelSlash();
            skill.SlashPlaybackSpeed = float.NaN;
            skill.SetLine(line);
            Require(!player.ApplySlash() && player.CurrentLine.HasValue && !rendering.IsSlashAnimation,
                "Invalid playback must retain the Line and ordinary rendering.");
            passed("Invalid timeline tuning is rejected before consuming a Line or changing presentation ownership");
            skill.SlashPlaybackSpeed = 5f;
            skill.SetLine(line);
            yield return CheckTuningWindow(manager, passed); // 실제 Play 대상을 연결한 조정 창의 재생·저장 API 확인 대기
        }
        finally
        {
            // 이번 실행의 효과·임시 시각 표본·조정값만 복구
            skill.ResetSkills();
            skill.SlashPlaybackSpeed = speed;
            fx.SpaceSlashDuration = spaceDuration;
            player.AD = attack;
            UnityEngine.Object.Destroy(fixture);
            UnityEngine.Object.Destroy(overlay);
            UnityEngine.Object.Destroy(markerSprite);
        }
    }

    /// <summary>원본 데이터 소비 후에도 같은 월드 경로와 불투명 표시를 타격 전까지 유지하는지 확인</summary>
    private static void RequirePendingLine(DrawVfx draw, Line line)
    {
        Require(draw.IsSlashLineVisible && draw.LineObject.activeSelf && draw.LineOpacity == 1f
            && Vector2.Distance(draw.LineObject.transform.position, line.StartPoint) < 0.001f
            && Mathf.Abs(draw.LineObject.transform.localScale.x - line.Length) < 0.001f
            && Vector2.Dot(draw.LineObject.transform.right, line.Direction) > 0.999f,
            "The consumed map Line must remain opaque at its original world start, direction and length until impact.");
    }

    /// <summary>조정 창이 실제 런타임 Player를 연결하고 지정값만 원본 프리팹에 저장하는지 확인</summary>
    private static IEnumerator CheckTuningWindow(GameManager manager, Action<string> passed)
    {
        // Editor 창의 실제 연결 API와 저장 대상 확보
        SlashPresentationTuning window = ScriptableObject.CreateInstance<SlashPresentationTuning>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(SlashPresentationTuning);
        type.GetMethod("ConnectPlayer", flags).Invoke(window, null);
        SkillController sourceSkill = (SkillController)type.GetField("skillSaveTarget", flags).GetValue(window);
        GameCamera sourceCamera = (GameCamera)type.GetField("cameraSaveTarget", flags).GetValue(window);
        SlashPresentationFx sourceFx = (SlashPresentationFx)type.GetField("fxSaveTarget", flags).GetValue(window);
        Require(sourceSkill != null && sourceCamera != null && sourceFx != null, "The tuning window must locate the actual persistent Player and camera prefabs. skill="
            + (sourceSkill != null) + ", camera=" + (sourceCamera != null) + ", fx=" + (sourceFx != null)
            + ", stagePrefabPersistent=" + EditorUtility.IsPersistent(manager.Stage.PlayerPrefab)
            + ", cameraPrefab=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(GameCamera.Instance.gameObject));
        string skillBackup = EditorJsonUtility.ToJson(sourceSkill);
        string cameraBackup = EditorJsonUtility.ToJson(sourceCamera);
        string fxBackup = EditorJsonUtility.ToJson(sourceFx);
        SkillController skill = manager.CurrentPlayer.Skills;
        float hold = skill.SlashHoldDuration;
        float assetSpeed = sourceSkill.SlashPlaybackSpeed;
        float assetDamage = sourceSkill.SlashDamageMultiplier;
        try
        {
            // 배속 버튼과 같은 API로 느린 재생을 선택하고 저장 대상에서 배속 제외 확인
            type.GetMethod("SetSpeed", flags).Invoke(window, new object[] { 0.25f });
            Require(skill.SlashPlaybackSpeed == 0.25f, "The slow playback control must change the current instance.");
            skill.SlashHoldDuration = hold + 0.01f;
            type.GetMethod("SaveProperties", flags).Invoke(window, null);
            Require(Mathf.Abs(sourceSkill.SlashHoldDuration - skill.SlashHoldDuration) < 0.0001f
                && sourceSkill.SlashPlaybackSpeed == assetSpeed && sourceSkill.SlashDamageMultiplier == assetDamage,
                "Explicit save must write the selected presentation value while preserving playback speed and combat data.");
            passed("The tuning window connects runtime instances and explicitly saves only presentation fields to their actual prefabs");

            // 기억한 Line의 실제 재생 요청과 창 소유의 중단 경로 확인
            skill.SlashPlaybackSpeed = 5f;
            type.GetMethod("PlayPreview", flags).Invoke(window, new object[] { false });
            Require(skill.IsSlashing, "The tuning window must start the actual Slash using its remembered Line.");
            yield return Until(() => !skill.IsSlashing, "tuning window single preview"); // 창이 시작한 실제 Slash의 한 번 재생 완료 대기
            type.GetMethod("StopPreview", flags).Invoke(window, null);
            Require(!manager.IsSlashPaused && !manager.CurrentPlayer.GetComponent<SlashPresentationFx>().IsSpaceSlashPlaying,
                "Stopping the owned preview must also clear its remaining world effect.");
            passed("The tuning window replays the remembered Line and stops only its owned presentation");
        }
        finally
        {
            // 격리 검사에서 임시로 저장한 값과 Editor 창의 구독·참조를 원래 상태로 복구
            type.GetMethod("StopPreview", flags).Invoke(window, null);
            skill.SlashHoldDuration = hold;
            EditorJsonUtility.FromJsonOverwrite(skillBackup, sourceSkill);
            EditorJsonUtility.FromJsonOverwrite(cameraBackup, sourceCamera);
            EditorJsonUtility.FromJsonOverwrite(fxBackup, sourceFx);
            EditorUtility.SetDirty(sourceSkill);
            EditorUtility.SetDirty(sourceCamera);
            EditorUtility.SetDirty(sourceFx);
            PrefabUtility.SavePrefabAsset(sourceSkill.transform.root.gameObject);
            PrefabUtility.SavePrefabAsset(sourceCamera.transform.root.gameObject);
            AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    /// <summary>화면 절단이 UI를 바꾸지 않는지 확인할 자홍색 오버레이 표본</summary>
    private static GameObject CreateOverlay()
    {
        GameObject root = new GameObject("SlashUIExclusionProof", typeof(Canvas));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        GameObject patch = new GameObject("FixedMagentaPatch", typeof(RectTransform), typeof(Image));
        patch.transform.SetParent(root.transform, false);
        RectTransform rect = patch.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -20f);
        rect.sizeDelta = new Vector2(64f, 32f);
        patch.GetComponent<Image>().color = Color.magenta;
        return root;
    }

    /// <summary>공간참의 실제 피해 시점을 측정할 작은 Trigger 대상 준비</summary>
    private static Character CreateTarget(Transform parent, Vector2 position)
    {
        GameObject root = new GameObject("DelayedSlashTarget", typeof(BoxCollider2D), typeof(Character), typeof(Hitbox));
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.GetComponent<BoxCollider2D>().isTrigger = true;
        Character target = root.GetComponent<Character>();
        target.maxHP = 1000f;
        target.InitializeHealth(1000f);
        root.GetComponent<Hitbox>().Owner = target;
        return target;
    }

    /// <summary>Line 양쪽의 실제 화면 이동 방향을 검출할 원색 Sprite 표본 준비</summary>
    private static void CreateMarker(Transform parent, Sprite sprite, Vector3 position, Color color)
    {
        GameObject root = new GameObject("WorldShiftMarker", typeof(SpriteRenderer));
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * 0.1f;
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 200;
    }

    /// <summary>실제 최종 렌더 출력의 표본 색 중심과 UI 고정 위치를 비교</summary>
    private static void CheckImages(string directory, Camera camera, Vector3 red, Vector3 blue, Vector2 direction, Line line)
    {
        Texture2D cut = new Texture2D(2, 2);
        Texture2D animated = new Texture2D(2, 2);
        Texture2D restored = new Texture2D(2, 2);
        try
        {
            cut.LoadImage(File.ReadAllBytes(directory + "/03-space-cut.png"));
            animated.LoadImage(File.ReadAllBytes(directory + "/03b-space-energy.png"));
            restored.LoadImage(File.ReadAllBytes(directory + "/04-restored.png"));
            Color ui = cut.GetPixel(32, cut.height - 32);
            Color originalUi = restored.GetPixel(32, restored.height - 32);
            Require(ui.r > 0.95f && ui.g < 0.05f && ui.b > 0.95f && ui == originalUi,
                "The ScreenSpaceOverlay proof patch must remain unchanged during the world-only screen pass.");
            Vector2 redScreen = camera.WorldToScreenPoint(red);
            Vector2 blueScreen = camera.WorldToScreenPoint(blue);
            Vector2 redShift = FindMarker(cut, redScreen, true) - FindMarker(restored, redScreen, true);
            Vector2 blueShift = FindMarker(cut, blueScreen, false) - FindMarker(restored, blueScreen, false);
            Require(Vector2.Dot(redShift, direction) < -8f && Vector2.Dot(blueShift, direction) > 8f,
                "World marker displacements must follow opposite Line sides with the actual camera UV orientation. red=" + redShift + ", blue=" + blueShift);

            // 어긋난 화면의 원래 Line 위에 선명한 황색이 남고 끝점 밖에는 피해선을 그리지 않음
            foreach (float ratio in new[] { 0.15f, 0.25f, 0.5f, 0.75f, 0.85f })
            {
                Vector2 point = camera.WorldToScreenPoint(line.StartPoint + line.Direction * (line.Length * ratio));
                Require(HasYellowEnergy(cut, point),
                    "The post-distortion energy must retain a yellow halo around its original Line, including the white-floor sample.");
            }
            foreach (Vector2 end in new[] { line.StartPoint - line.Direction * 0.2f, line.EndPoint + line.Direction * 0.2f })
            {
                Vector2 point = camera.WorldToScreenPoint(end);
                Require(!IsYellow(cut.GetPixel(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y))),
                    "The fixed yellow damage path must stop at the actual Line endpoints.");
            }
            Vector2 startScreen = camera.WorldToScreenPoint(line.StartPoint);
            Vector2 endScreen = camera.WorldToScreenPoint(line.EndPoint);
            int movingEnergyPixels = 0;
            for (int y = Mathf.Max(0, (int)Mathf.Min(startScreen.y, endScreen.y) - 8); y < Mathf.Min(cut.height, Mathf.Max(startScreen.y, endScreen.y) + 8); y++)
                for (int x = Mathf.Max(0, (int)Mathf.Min(startScreen.x, endScreen.x)); x < Mathf.Min(cut.width, Mathf.Max(startScreen.x, endScreen.x)); x++)
                {
                    Color before = cut.GetPixel(x, y);
                    Color after = animated.GetPixel(x, y);
                    if ((IsYellow(before) || IsYellow(after)) && ((Vector4)before - (Vector4)after).sqrMagnitude > 0.002f) movingEnergyPixels++;
                }
            Require(movingEnergyPixels > 20, "The yellow energy must shimmer, thin or move over time rather than remain an opaque static bar.");
            Vector2 normal = new Vector2(-line.Direction.y, line.Direction.x);
            Vector2 brightEdge = camera.WorldToScreenPoint(line.StartPoint + line.Direction * 1.5f + normal * (line.Width * 0.44f));
            Color edge = cut.GetPixel(Mathf.RoundToInt(brightEdge.x), Mathf.RoundToInt(brightEdge.y));
            Require(edge.r > 0.6f && edge.g > 0.45f, "The VFX must preserve the bright background without painting a black gap or dark band.");
            File.WriteAllText(directory + "/render-observation.json", JsonUtility.ToJson(new RenderObservation
                { Width = cut.width, Height = cut.height, RedShift = redShift, BlueShift = blueShift, OverlayUnchanged = true, YellowDamagePath = true,
                    AnimatedEnergyPixels = movingEnergyPixels, LineStartScreen = startScreen, LineEndScreen = endScreen }, true));
        }
        finally
        {
            UnityEngine.Object.Destroy(cut);
            UnityEngine.Object.Destroy(animated);
            UnityEngine.Object.Destroy(restored);
        }
    }

    /// <summary>밝은 배경의 흰색·청록색과 구분되는 황색 피해선 검출</summary>
    private static bool IsYellow(Color color) => color.r > 0.55f && color.r > color.b + 0.12f && color.g > color.b + 0.06f;

    /// <summary>밝은 중심을 허용하면서 주변의 부드러운 금빛 잔광 검출</summary>
    private static bool HasYellowEnergy(Texture2D image, Vector2 center)
    {
        for (int y = Mathf.Max(0, Mathf.RoundToInt(center.y) - 5); y <= Mathf.Min(image.height - 1, Mathf.RoundToInt(center.y) + 5); y++)
            for (int x = Mathf.Max(0, Mathf.RoundToInt(center.x) - 5); x <= Mathf.Min(image.width - 1, Mathf.RoundToInt(center.x) + 5); x++)
                if (IsYellow(image.GetPixel(x, y))) return true;
        return false;
    }

    /// <summary>예상 표본 주변에서 채도가 높은 빨강·파랑 픽셀의 중심 검출</summary>
    private static Vector2 FindMarker(Texture2D image, Vector2 expected, bool red)
    {
        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int y = Mathf.Max(0, (int)expected.y - 45); y < Mathf.Min(image.height, (int)expected.y + 45); y++)
            for (int x = Mathf.Max(0, (int)expected.x - 45); x < Mathf.Min(image.width, (int)expected.x + 45); x++)
            {
                Color color = image.GetPixel(x, y);
                if (color.g < 0.1f && (red ? color.r > 0.9f && color.b < 0.1f : color.b > 0.9f && color.r < 0.1f))
                { sum += new Vector2(x, y); count++; }
            }
        Require(count > 2, "The rendered world marker must be visible in the camera capture.");
        return sum / count;
    }

    /// <summary>결과 로그에 남길 실제 화면 이동 관측값</summary>
    [Serializable] private class RenderObservation
    {
        public int Width; // 검사한 최종 화면의 가로 픽셀 수
        public int Height; // 검사한 최종 화면의 세로 픽셀 수
        public Vector2 RedShift; // Line 한쪽의 빨강 월드 표본 이동량
        public Vector2 BlueShift; // Line 반대쪽의 파랑 월드 표본 이동량
        public bool OverlayUnchanged; // 오버레이 UI의 색·위치 불변 확인 결과
        public bool YellowDamagePath; // 원래 Line 위치의 황색 피해선과 끝점·밝은 바닥 가독성 확인 결과
        public int AnimatedEnergyPixels; // 서로 다른 실제 시각에서 색·위치·두께가 변한 황색 잔광 픽셀 수
        public Vector2 LineStartScreen; // 실제 Line 시작점의 화면 좌표
        public Vector2 LineEndScreen; // 실제 Line 끝점의 화면 좌표
    }

    /// <summary>현재 판정은 반복하지 않고 기본 수명의 검격 화면만 프레임별로 저장</summary>
    private static IEnumerator CaptureEnergyMotion(SlashPresentationFx fx, Line line, string directory, GameObject fixture, GameObject overlay)
    {
        // 사용자 검토 영상은 측정용 색 표본·흰 바닥·자홍 UI 없이 실제 맵에서 촬영
        SpriteRenderer[] proofRenderers = fixture.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer proof in proofRenderers) proof.enabled = false;
        overlay.SetActive(false);
        yield return Frames(2); // 측정용 표시가 제거된 실제 맵 프레임 반영 대기

        // 실제 프레임 간격을 기록해 검토 GIF에서도 원래의 짧은 재생 속도 유지
        Directory.CreateDirectory(directory);
        var files = new List<string>();
        var times = new List<float>();
        fx.PlaySpaceSlash(line);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (fx.IsSpaceSlashPlaying && Time.realtimeSinceStartup < deadline)
        {
            string path = Path.Combine(directory, files.Count.ToString("D4") + ".png");
            if (File.Exists(path)) File.Delete(path);
            files.Add(path);
            times.Add(Time.realtimeSinceStartup);
            ScreenCapture.CaptureScreenshot(path);
            yield return Frames(1); // 다음 실제 렌더 프레임의 잔광·파편 위치 관찰
        }
        string last = Path.Combine(directory, files.Count.ToString("D4") + ".png");
        yield return Capture(last); // 잔광이 사라진 최종 원상 화면 저장 대기
        files.Add(last);
        times.Add(Time.realtimeSinceStartup);
        yield return Until(() => files.TrueForAll(path => File.Exists(path) && new FileInfo(path).Length > 0), "energy motion capture"); // 요청한 모든 렌더 출력 완료 대기
        Require(files.Count >= 3, "Normal-duration VFX must produce multiple distinct render frames.");

        // 출력 도구가 프레임마다 실제 간격을 사용하도록 concat 입력 작성
        var manifest = new List<string>();
        for (int i = 0; i < files.Count; i++)
        {
            manifest.Add("file '" + Path.GetFileName(files[i]) + "'");
            float duration = i == files.Count - 1 ? 0.4f : Mathf.Max(0.001f, times[i + 1] - times[i]);
            manifest.Add("duration " + duration.ToString("0.######", CultureInfo.InvariantCulture));
        }
        manifest.Add("file '" + Path.GetFileName(last) + "'");
        File.WriteAllLines(Path.Combine(directory, "frames.txt"), manifest);
        foreach (SpriteRenderer proof in proofRenderers) proof.enabled = true;
        overlay.SetActive(true);
    }

    /// <summary>다음 렌더 프레임의 최종 UI 합성 화면을 저장하고 파일 완료 대기</summary>
    private static IEnumerator Capture(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return Until(() => File.Exists(path) && new FileInfo(path).Length > 0, "screen capture " + path); // 실제 렌더 출력 파일 완료 대기
        yield return Frames(2); // 인코딩 완료와 파일 닫힘 대기
    }

    /// <summary>정지 중에도 흐르는 실제 시간 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= end, "real seconds"); // 실제 연출 시간의 경과 대기
    }

    /// <summary>구간 전이가 렌더 프레임에 반영될 때까지 관찰</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "frames"); // Unity 렌더 프레임 진행 대기
    }

    /// <summary>기존 Play 실행기에서 조건 대기와 시간 초과 전달</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 12f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Slash presentation timed out: " + description);
            yield return null;
        }
    }

    /// <summary>관측된 계약 위반을 기존 검사 결과로 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
