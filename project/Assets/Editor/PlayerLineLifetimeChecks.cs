using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Player가 보관한 확정 Line의 수명·만료와 연결된 표현을 실제 갱신으로 확인</summary>
public static class PlayerLineLifetimeChecks
{
    /// <summary>기존 Draw 검사 뒤 수명 상태와 표현을 확인하고 세션 수치 복구</summary>
    public static IEnumerator Run(GameManager manager, PlayerInputController input, Action<string> passed)
    {
        // 확정 경로가 없는 검사 경계와 복원할 Inspector 값을 보관
        PlayerCharacter player = manager.CurrentPlayer;
        SkillController skills = player.Skills;
        DrawVfx vfx = player.GetComponent<DrawVfx>();
        DrawLineVfx lineVisual = vfx.LineObject.GetComponent<DrawLineVfx>();
        DrawLineVfx expiryVisual = vfx.ExpiryObject.GetComponent<DrawLineVfx>();
        LineRenderer lineRenderer = vfx.LineObject.GetComponent<LineRenderer>();
        LineRenderer expiryRenderer = vfx.ExpiryObject.GetComponent<LineRenderer>();
        ParticleSystem particles = vfx.Evaporation.Particles;
        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[Mathf.Max(1, particles.main.maxParticles)];
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        GameCamera gameCamera = GameCamera.Instance;
        Camera camera = gameCamera.Camera;
        Transform cameraTarget = gameCamera.Target;
        Vector3 cameraPosition = camera.transform.position;
        float cameraZoom = camera.orthographicSize;
        List<string> frameTimes = new List<string> { "frame,game_time,real_time,line_opacity,expiry_progress,is_expiring,particle_count,center_x,center_y,mean_alpha,mean_size" };
        List<string> particleTimes = new List<string> { "frame,seed,world_x,world_y,world_z,start_width,start_height,current_width,current_height,alpha,remaining_lifetime" };
        double nextCaptureTime = 0d;
        int capturedFrames = 0;
        Require(!player.CurrentLine.HasValue && !skills.IsDrawing, "Lifetime checks require the clean confirmed-Line boundary restored by the Draw checks.");
        float lifetime = skills.LineLifetime;
        float cancelCooldown = skills.CancelDrawCooldown;
        float normalCooldown = skills.NormalDrawCooldown;
        float slashCooldown = skills.ConfirmSlashCooldown;
        float warningFraction = vfx.WarningFraction;
        float slowPeriod = vfx.SlowBlinkPeriod;
        float fastPeriod = vfx.FastBlinkPeriod;
        float expiryDuration = vfx.ExpiryDuration;
        float health = player.HP;
        bool invulnerable = player.IsInvulnerable;
        bool blocked = input.BlockInput;
        bool paused = manager.IsGameplayPaused;
        Action beforeInputUpdate = null;
        try
        {
            // 실제 입력 해제를 반영하고 짧은 검사 수명과 독립 표현 설정 준비
            input.BlockInput = false;
            manager.SetGameplayPaused(false);
            player.ResetMotion();
            skills.ResetSkills();
            skills.CancelDrawCooldown = 0f;
            skills.ConfirmSlashCooldown = 0f;
            skills.LineLifetime = 1.2f;
            vfx.WarningFraction = 0.75f;
            vfx.SlowBlinkPeriod = 0.3f;
            vfx.FastBlinkPeriod = 0.06f;
            vfx.ExpiryDuration = 1.3f;
            yield return Frames(3); // 이전 검사에서 해제한 키가 새 수명 검사에 남지 않도록 대기

            // Draw 확정 결과가 Player 원본으로 저장되고 새 확정은 수명을 처음부터 시작
            Vector2 origin = player.transform.position;
            Line first = Confirm(player, origin + Vector2.right, Vector2.up);
            Require(player.CurrentLine.HasValue && SameLine(player.CurrentLine.Value, first) && skills.HasLine,
                "Draw confirmation must store its only confirmed-Line value on PlayerCharacter.");
            Require(skills.LineRemaining == skills.LineLifetime && skills.LineElapsed == 0f && vfx.LineRemainingRatio == 1f && vfx.IsLineVisible
                && Mathf.Abs(vfx.LineOpacity - 1f) < 0.001f,
                "A new confirmed Line must begin with a full lifetime and an immediately visible VFX.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, vfx.LineOpacity);
            yield return GameSeconds(0.2f); // 실제 Skill.Update가 Player에 저장된 경로의 수명을 진행하는지 관찰
            Require(skills.LineRemaining < skills.LineLifetime && skills.LineElapsed > 0f && SameLine(player.CurrentLine.Value, first),
                "The manager must advance time while the Player retains the same geometry.");
            Line second = Confirm(player, origin + Vector2.up, Vector2.left);
            Require(SameLine(player.CurrentLine.Value, second) && skills.LineRemaining == skills.LineLifetime && skills.LineElapsed == 0f,
                "A replacement confirmation must replace Player geometry and reset the old timer.");
            passed("F-P06 Draw confirmation stores Line on Player and Skill lifetime restarts only for a new confirmed value");

            // 재Draw 진입·취소와 일반 정지가 기존 Line·경고 위상을 보존
            yield return GameSeconds(0.12f); // 정지 전 이미 일부 소비된 잔여 수명 준비
            Require(player.ApplyDraw(), "A valid Player must enter redraw while preserving its existing Line.");
            yield return Frames(1); // Draw 정지 진입 프레임의 시간 샘플을 지난 뒤 잔여 값 보관
            float remaining = skills.LineRemaining;
            float elapsed = skills.LineElapsed;
            float phase = vfx.LifePhase;
            float opacity = vfx.LineOpacity;
            bool visible = vfx.IsLineVisible;
            yield return RealSeconds(0.12f); // 게임 시간이 멈춘 Draw 편집 중 기존 경로 수명·표현 위상 관찰
            Require(SameLine(player.CurrentLine.Value, second) && skills.LineRemaining == remaining && skills.LineElapsed == elapsed
                && vfx.LifePhase == phase && vfx.IsLineVisible == visible && vfx.LineOpacity == opacity,
                "Redraw must freeze the stored Line's lifetime, alpha and warning phase.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, opacity);
            player.CancelDraw();
            Require(SameLine(player.CurrentLine.Value, second) && skills.LineRemaining == remaining,
                "Cancelling editing must preserve the old Line and its remaining lifetime.");
            manager.SetGameplayPaused(true);
            yield return Frames(1); // 일반 정지의 첫 프레임을 지난 뒤 같은 시간 기준으로 비교
            remaining = skills.LineRemaining;
            phase = vfx.LifePhase;
            opacity = vfx.LineOpacity;
            yield return RealSeconds(0.12f); // 일반 pause도 경로와 수명 기반 연출을 멈추는지 관찰
            Require(skills.LineRemaining == remaining && vfx.LifePhase == phase && vfx.LineOpacity == opacity,
                "Ordinary pause must freeze the same Line clock, alpha and VFX phase.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, opacity);
            manager.SetGameplayPaused(false);
            yield return GameSeconds(0.08f); // 정지를 푼 뒤 이전 잔여 시간에서 재개하는지 관찰
            Require(skills.LineRemaining < remaining && SameLine(player.CurrentLine.Value, second), "The existing lifetime must resume after its pause owners release.");
            passed("F-P06 redraw cancellation and ordinary pause preserve Player Line, remaining lifetime and warning phase");

            // 후반 경고는 실제 재질 알파를 부드럽게 바꾸고 만료에 가까워질수록 주기 단축
            skills.LineLifetime = 2.4f;
            Line warningLine = new Line((Vector2)manager.CurrentBoss.transform.position - new Vector2(5f, 4f),
                new Vector2(Mathf.Cos(Mathf.PI / 6f), Mathf.Sin(Mathf.PI / 6f)), 6f, 0.25f);
            skills.SetLine(warningLine);
            vfx.WarningFraction = 0.9f;
            vfx.SlowBlinkPeriod = 0.4f;
            vfx.FastBlinkPeriod = 0.08f;
            yield return Until(() => vfx.LineRemainingRatio <= 0.85f, "warning begins"); // 수명 후반 경고가 시작되는 실제 게임 시간 대기
            float earlyInterval = vfx.BlinkInterval;
            phase = vfx.LifePhase;
            opacity = vfx.LineOpacity;
            visible = vfx.IsLineVisible;
            remaining = skills.LineRemaining;
            float ratio = vfx.LineRemainingRatio;
            Require(player.ApplyDraw(), "A warning Line must still permit redraw when Character conditions allow it.");
            Require(vfx.LifePhase == phase && vfx.IsLineVisible == visible && vfx.LineRemainingRatio == ratio && skills.LineRemaining == remaining
                && vfx.LineOpacity == opacity,
                "Entering redraw must not restart the existing Line VFX, warning ratio or phase.");
            yield return RealSeconds(0.12f); // 이미 경고 중인 선도 재Draw 편집 동안 같은 위상을 유지하는지 관찰
            Require(vfx.LifePhase == phase && vfx.LineRemainingRatio == ratio && vfx.LineOpacity == opacity,
                "Redraw must preserve the alpha and phase of an already active lifetime warning.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, opacity);
            player.CancelDraw();
            manager.SetGameplayPaused(true);
            yield return Frames(1); // 실제 경고 구간에서 정지 진입 프레임을 넘긴 뒤 위상 보관
            phase = vfx.LifePhase;
            opacity = vfx.LineOpacity;
            visible = vfx.IsLineVisible;
            yield return RealSeconds(0.12f); // 진행 중 경고 깜빡임이 일반 정지에서 함께 멈추는지 관찰
            Require(vfx.LifePhase == phase && vfx.IsLineVisible == visible && vfx.LineOpacity == opacity,
                "Pause must preserve the phase and opacity of an already fading warning.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, opacity);
            manager.SetGameplayPaused(false);

            // 실험실 바닥 위 경고와 위로 퍼지는 실제 입자를 같은 카메라로 기록
            Directory.CreateDirectory("Logs/DrawExpiryFrames");
            gameCamera.Follow(null);
            Vector2 center = (warningLine.StartPoint + warningLine.EndPoint) * 0.5f;
            camera.transform.position = new Vector3(center.x, center.y + 0.8f, cameraPosition.z);
            camera.orthographicSize = 3.5f;
            float previousOpacity = vfx.LineOpacity;
            float minimumOpacity = previousOpacity;
            float maximumOpacity = previousOpacity;
            bool fadedDown = false;
            bool fadedUp = false;
            bool hadIntermediateOpacity = false;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (skills.HasLine && vfx.LineRemainingRatio > 0.25f)
            {
                float currentOpacity = vfx.LineOpacity;
                minimumOpacity = Mathf.Min(minimumOpacity, currentOpacity);
                maximumOpacity = Mathf.Max(maximumOpacity, currentOpacity);
                fadedDown |= currentOpacity < previousOpacity - 0.005f;
                fadedUp |= currentOpacity > previousOpacity + 0.005f;
                hadIntermediateOpacity |= currentOpacity > 0.1f && currentOpacity < 0.9f;
                previousOpacity = currentOpacity;
                Require(vfx.IsLineVisible && vfx.LineObject.activeSelf,
                    "A fading warning must keep its live Line object active, including at zero opacity.");
                AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, currentOpacity);
                CaptureFrame(vfx, particles, particleBuffer, frameTimes, particleTimes, ref nextCaptureTime, ref capturedFrames);
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P06 warning did not advance.");
                yield return null;
            }
            Require(skills.HasLine && fadedDown && fadedUp && hadIntermediateOpacity && minimumOpacity < 0.2f && maximumOpacity > 0.8f
                && vfx.BlinkInterval < earlyInterval && vfx.BlinkInterval > 0f,
                "The live Line must fade through intermediate alpha in both directions while its warning period accelerates.");
            skills.CancelDrawCooldown = 0.3f;
            while (player.CurrentLine.HasValue)
            {
                AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, vfx.LineOpacity);
                CaptureFrame(vfx, particles, particleBuffer, frameTimes, particleTimes, ref nextCaptureTime, ref capturedFrames);
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P06 Line did not naturally expire.");
                yield return null;
            }
            Require(!skills.HasLine && skills.LineRemaining == 0f && !vfx.IsLineVisible && !vfx.LineObject.activeSelf,
                "Natural expiry must clear the Player-owned value and close its live line VFX.");
            Require(skills.DrawCooldownRemaining > 0f && skills.DrawCooldownRemaining <= skills.CancelDrawCooldown
                && vfx.IsExpiryPlaying && vfx.ExpiryObject != null && vfx.ExpiryObject.activeSelf,
                "Natural expiry must apply the short Draw cooldown and start its separate expiration VFX once.");
            Require(expiryRenderer.sharedMaterial.mainTexture == lineRenderer.sharedMaterial.mainTexture
                && expiryRenderer.startColor == Color.white && expiryRenderer.endColor == Color.white,
                "Natural expiry must preserve the original source beam while small round grains detach without an orange tint.");
            Require(particles.main.simulationSpace == ParticleSystemSimulationSpace.World && vfx.Evaporation.Particles == particles,
                "Evaporation must reuse a prepared particle system with world-space positions.");
            Require(particleRenderer.renderMode == ParticleSystemRenderMode.Billboard && particleRenderer.sharedMaterial != null
                && particleRenderer.sharedMaterial.shader != null && particleRenderer.sharedMaterial.shader.name == "Chrono/DrawEvaporationParticle",
                "Small round grains must use the prepared billboard renderer and circular evaporation shader.");
            Require(particleRenderer.sharedMaterial.HasProperty("_LinePaletteTex") && lineRenderer.sharedMaterial.mainTexture != null
                && particleRenderer.sharedMaterial.GetTexture("_LinePaletteTex") == lineRenderer.sharedMaterial.mainTexture,
                "Small round grains must sample their palette from the same texture as the live Line image FX.");
            Vector3 expiryScale = vfx.ExpiryObject.transform.localScale;
            float expiryWidth = expiryRenderer.widthMultiplier;
            float previousProgress = vfx.ExpiryProgress;
            int initialCount = particles.GetParticles(particleBuffer);
            Require(initialCount > 0, "Natural expiry must emit actual small round grains from the line.");
            AssertParticleCenterStream(particleRenderer, camera);
            Dictionary<uint, ParticleSystem.Particle> initialParticles = new Dictionary<uint, ParticleSystem.Particle>();
            Dictionary<uint, Vector3> initialSizes = new Dictionary<uint, Vector3>();
            Dictionary<uint, byte> maximumAlpha = new Dictionary<uint, byte>();
            for (int i = 0; i < initialCount; i++)
            {
                ParticleSystem.Particle particle = particleBuffer[i];
                Require(!initialParticles.ContainsKey(particle.randomSeed), "Each emitted small round grain must keep a unique tracking seed.");
                Vector3 startSize = particle.startSize3D;
                Vector3 currentSize = particle.GetCurrentSize3D(particles);
                Require(Mathf.Abs(startSize.x - startSize.y) < 0.00001f && startSize.x >= 0.045f - 0.00001f && startSize.x <= 0.085f + 0.00001f
                    && Mathf.Abs(currentSize.x - currentSize.y) < 0.00001f && currentSize.x >= 0.045f - 0.00001f && currentSize.x <= 0.085f + 0.00001f,
                    "Each small round grain must have equal width and height with an actual diameter between 0.045 and 0.085 world units.");
                initialParticles.Add(particle.randomSeed, particle);
                initialSizes.Add(particle.randomSeed, currentSize);
                maximumAlpha.Add(particle.randomSeed, particle.GetCurrentColor(particles).a);
            }
            CaptureFrame(vfx, particles, particleBuffer, frameTimes, particleTimes, ref nextCaptureTime, ref capturedFrames);

            // 일반 정지는 입자의 실제 위치·수명·색을 멈추고 비활성화는 같은 입자만 숨김
            manager.SetGameplayPaused(true);
            yield return Frames(1); // 정지 진입 프레임을 지난 뒤 실제 입자 상태 보관
            float frozenProgress = vfx.ExpiryProgress;
            float frozenParticleTime = particles.time;
            ParticleSystem.Particle[] frozenParticles = new ParticleSystem.Particle[particleBuffer.Length];
            int frozenCount = particles.GetParticles(frozenParticles);
            yield return RealSeconds(0.12f); // 실제 시간이 지나도 입자 위치·수명·알파가 멈추는지 관찰
            Require(vfx.IsExpiryPlaying && vfx.ExpiryProgress == frozenProgress && particles.time == frozenParticleTime
                && vfx.ExpiryObject.transform.localScale == expiryScale,
                "Ordinary pause must freeze particle simulation and preserve the source ribbon geometry.");
            AssertParticlesFrozen(particles, frozenParticles, frozenCount, particleBuffer);
            skills.enabled = false;
            Require(!particleRenderer.enabled || !particleRenderer.gameObject.activeInHierarchy,
                "Suspending the skill must hide existing evaporation particles.");
            AssertParticlesFrozen(particles, frozenParticles, frozenCount, particleBuffer);
            skills.enabled = true;
            Require(particleRenderer.enabled && particleRenderer.gameObject.activeInHierarchy && vfx.ExpiryProgress == frozenProgress,
                "Restoring the skill must reveal the same paused particle state.");
            vfx.enabled = false;
            Require(!particleRenderer.enabled || !particleRenderer.gameObject.activeInHierarchy,
                "Disabling the visual owner must hide its independent particle renderer.");
            vfx.enabled = true;
            Require(particleRenderer.enabled && particleRenderer.gameObject.activeInHierarchy,
                "Re-enabling the visual owner must restore its surviving particles.");
            AssertParticlesFrozen(particles, frozenParticles, frozenCount, particleBuffer);
            manager.SetGameplayPaused(false);
            bool evaporationAdvanced = false;
            bool movedLeft = false;
            bool movedRight = false;
            bool movedUp = false;
            bool alphaFaded = false;
            bool particlesOutlivedSource = false;
            float maximumMeanRise = 0f;
            while (vfx.IsExpiryPlaying)
            {
                float progress = vfx.ExpiryProgress;
                Require(progress >= previousProgress && progress >= 0f && progress <= 1f
                    && vfx.ExpiryObject.transform.localScale == expiryScale && Mathf.Abs(expiryRenderer.widthMultiplier - expiryWidth) < 0.001f,
                    "Evaporation must advance while preserving the source ribbon length and width.");
                evaporationAdvanced |= progress > 0.1f && progress < 0.9f;
                previousProgress = progress;
                AssertVisualParameters(expiryVisual, expiryRenderer, propertyBlock, expiryVisual.Opacity);
                int count = particles.GetParticles(particleBuffer);
                float rise = 0f;
                int tracked = 0;
                for (int i = 0; i < count; i++)
                {
                    ParticleSystem.Particle particle = particleBuffer[i];
                    if (!initialParticles.TryGetValue(particle.randomSeed, out ParticleSystem.Particle initial)) continue;
                    Vector3 displacement = particle.position - initial.position;
                    movedLeft |= displacement.x < -0.05f;
                    movedRight |= displacement.x > 0.05f;
                    movedUp |= displacement.y > 0.05f;
                    rise += displacement.y;
                    tracked++;
                    Require(Vector3.Distance(particle.startSize3D, initial.startSize3D) < 0.001f
                        && Vector3.Distance(particle.GetCurrentSize3D(particles), initialSizes[particle.randomSeed]) < 0.001f,
                        "Small round grains must keep their actual diameter instead of shrinking as they evaporate.");
                    byte alpha = particle.GetCurrentColor(particles).a;
                    alphaFaded |= alpha + 12 < maximumAlpha[particle.randomSeed];
                    if (alpha > maximumAlpha[particle.randomSeed]) maximumAlpha[particle.randomSeed] = alpha;
                }
                if (tracked > 0) maximumMeanRise = Mathf.Max(maximumMeanRise, rise / tracked);
                if (progress >= 0.27f)
                {
                    Require(expiryVisual.Opacity <= 0.001f, "The source beam must be transparent after its initial quarter handoff.");
                    particlesOutlivedSource |= count > 0;
                }
                CaptureFrame(vfx, particles, particleBuffer, frameTimes, particleTimes, ref nextCaptureTime, ref capturedFrames);
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P06 evaporation did not finish.");
                yield return null;
            }
            Require(evaporationAdvanced && movedLeft && movedRight && movedUp && maximumMeanRise > 0.05f && alphaFaded && particlesOutlivedSource
                && !vfx.ExpiryObject.activeSelf && vfx.ExpiryProgress >= 0.99f && particles.GetParticles(particleBuffer) == 0,
                "Actual small round grains must spread left, right and upward at a fixed diameter, fade their alpha and finish without residual particles.");
            Require(!particleRenderer.enabled || !particleRenderer.gameObject.activeInHierarchy,
                "Completed evaporation must hide its reusable particle renderer.");
            CaptureFrame(vfx, particles, particleBuffer, frameTimes, particleTimes, ref nextCaptureTime, ref capturedFrames);
            yield return GameSeconds(0.1f); // 이미 비워진 Line이 다음 프레임마다 소멸 연출을 다시 만들지 않는지 관찰
            Require(!player.CurrentLine.HasValue && !vfx.IsExpiryPlaying && !vfx.ExpiryObject.activeSelf && particles.GetParticles(particleBuffer) == 0,
                "An expired empty Line must not restart its expiration effect.");
            CaptureFrame(vfx, particles, particleBuffer, frameTimes, particleTimes, ref nextCaptureTime, ref capturedFrames);
            yield return Frames(3); // 마지막 빈 화면이 저장된 뒤 카메라·다음 검사 상태 복원
            File.WriteAllLines("Logs/DrawExpiryFrames/frames.csv", frameTimes);
            File.WriteAllLines("Logs/DrawExpiryFrames/particles.csv", particleTimes);
            camera.orthographicSize = cameraZoom;
            gameCamera.Follow(cameraTarget);
            camera.transform.position = cameraPosition;
            passed("F-P06 smooth alpha warning releases actual small round grains that keep their diameter, scatter sideways, float upward and fade once");

            // 교체·수동 삭제·능력 비활성화·사망은 낡은 수명이나 자연 소멸 연출을 남기지 않음
            skills.CancelDrawCooldown = 0f;
            vfx.WarningFraction = 0.1f;
            skills.LineLifetime = 0.25f;
            skills.SetLine(first);
            yield return GameSeconds(0.12f); // 교체 직전 소모된 짧은 이전 수명 준비
            skills.LineLifetime = 1.2f;
            skills.SetLine(second);
            yield return GameSeconds(0.2f); // 이전 만료 시각을 지나도 새 경로가 유지되는지 관찰
            Require(SameLine(player.CurrentLine.Value, second) && skills.LineRemaining > 0f && !vfx.IsExpiryPlaying,
                "Replacing a Line must invalidate the previous lifetime and expiration effect.");
            Require(Mathf.Abs(vfx.LineOpacity - 1f) < 0.001f && vfx.ExpiryProgress == 0f,
                "A replacement Line must reset both its warning opacity and old evaporation progress.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, 1f);

            // 이미 떠오르는 이전 입자도 새 확정에서 지우고 같은 시스템을 다음 만료에 재사용
            skills.LineLifetime = 0.08f;
            skills.SetLine(first);
            yield return Until(() => vfx.IsExpiryPlaying, "replacement during evaporation"); // 실제 입자가 생성되는 다음 자연 만료 대기
            Require(vfx.Evaporation.Particles == particles && particles.GetParticles(particleBuffer) > 0,
                "A later expiry must reuse the same particle system and emit a new set of small round grains.");
            skills.LineLifetime = 1.2f;
            skills.SetLine(second);
            Require(!vfx.IsExpiryPlaying && !vfx.ExpiryObject.activeSelf && vfx.ExpiryProgress == 0f && vfx.LineOpacity == 1f
                && particles.GetParticles(particleBuffer) == 0,
                "Replacing an evaporating Line must clear its particles and reset the new Line opacity.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, 1f);
            skills.SetLine(null);
            Require(!player.CurrentLine.HasValue && skills.LineRemaining == 0f && !vfx.IsExpiryPlaying && !vfx.LineObject.activeSelf
                && vfx.ExpiryProgress == 0f && particles.GetParticles(particleBuffer) == 0,
                "Explicit clearing must remove geometry and lifetime without playing natural expiration VFX.");
            skills.SetLine(first);
            skills.enabled = false;
            remaining = skills.LineRemaining;
            phase = vfx.LifePhase;
            opacity = vfx.LineOpacity;
            yield return RealSeconds(0.12f); // 비활성 능력이 수명을 진행하거나 숨긴 표현을 다시 켜지 않는지 관찰
            Require(player.CurrentLine.HasValue && skills.LineRemaining == remaining && vfx.LifePhase == phase && vfx.LineOpacity == opacity
                && !vfx.LineObject.activeSelf,
                "A disabled manager must preserve its data and clock while suspending presentation.");
            skills.enabled = true;
            Require(skills.LineRemaining == remaining && vfx.LifePhase == phase && vfx.LineOpacity == opacity && vfx.LineObject.activeSelf,
                "Re-enabling the manager must restore the same lifetime phase and current presentation.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, opacity);
            vfx.enabled = false;
            remaining = skills.LineRemaining;
            yield return GameSeconds(0.12f); // 표현만 꺼도 관리자의 실제 수명은 계속 진행하는지 관찰
            Require(player.CurrentLine.HasValue && skills.LineRemaining < remaining && !vfx.LineObject.activeSelf,
                "Disabling presentation must hide the line without stopping its manager-owned lifetime.");
            vfx.enabled = true;
            Require(vfx.LineObject.activeSelf && Mathf.Abs(vfx.LineRemainingRatio - skills.LineRemaining / skills.LineLifetime) < 0.001f,
                "Re-enabled presentation must restore the latest manager-owned lifetime state.");
            AssertVisualParameters(lineVisual, lineRenderer, propertyBlock, vfx.LineOpacity);

            // 실제 사망은 이미 떠오르는 입자도 중단하고 모든 진행도를 즉시 초기화
            skills.LineLifetime = 0.08f;
            skills.SetLine(first);
            yield return Until(() => vfx.IsExpiryPlaying, "death during evaporation"); // 사망 정리를 받을 실제 증발 입자 준비
            player.IsInvulnerable = false;
            Require(player.ApplyDamage(new DamageInstance(manager.CurrentBoss, player.HP + 1f, DamageType.Effect, 9201, false)),
                "The lifetime cleanup check must use actual lethal damage.");
            Require(player.IsDead && !player.CurrentLine.HasValue && skills.LineRemaining == 0f && skills.DrawCooldownRemaining == 0f
                && !vfx.LineObject.activeSelf && !vfx.IsExpiryPlaying && !vfx.ExpiryObject.activeSelf && vfx.ExpiryProgress == 0f
                && particles.GetParticles(particleBuffer) == 0,
                "Death must synchronously reset the Player-owned Line and all ability timers without natural expiration VFX.");
            player.InitializeHealth(health);
            passed("F-P06 replacement, explicit clearing, manager suspension and actual death leave no stale Line clock or expiration VFX");

            // 유효 경로의 인계는 원본·수명·표현을 비우고 긴 Draw 대기만 적용
            skills.ResetSkills();
            skills.LineLifetime = 1.2f;
            skills.CancelDrawCooldown = 0.24f;
            skills.NormalDrawCooldown = 3f;
            skills.SetLine(first);
            Require(skills.ConsumeLine(out Line consumed) && SameLine(consumed, first) && !player.CurrentLine.HasValue
                && skills.LineRemaining == 0f && !vfx.LineObject.activeSelf && !vfx.IsExpiryPlaying && particles.GetParticles(particleBuffer) == 0
                && skills.DrawCooldownRemaining == skills.NormalDrawCooldown,
                "A valid Line handoff must return its geometry, clear ownership and lifetime, and apply the normal cooldown without natural expiration VFX.");
            remaining = skills.DrawCooldownRemaining;
            Require(!skills.ConsumeLine(out _) && skills.DrawCooldownRemaining == remaining && !vfx.IsExpiryPlaying,
                "A second same-frame handoff must fail without changing cooldown or playing expiration VFX.");

            // Input 갱신 직전 실제 만료 시각에 요청해 Skill.Update보다 먼저 지난 경로 거부
            skills.ResetSkills();
            skills.LineLifetime = 0.2f;
            skills.SetLine(second);
            double expirationTime = Time.timeAsDouble + skills.LineLifetime;
            bool boundaryReached = false;
            bool pendingBeforeSkillUpdate = false;
            bool consumedExpiredLine = false;
            bool expirationApplied = false;
            beforeInputUpdate = () =>
            {
                if (InputState.currentUpdateType != InputUpdateType.Dynamic || Time.timeAsDouble < expirationTime) return;

                // 갱신 전 남아 있는 경로가 API의 시각 검사에서 자연 만료되는지 수집
                pendingBeforeSkillUpdate = player.CurrentLine.HasValue && skills.LineRemaining > 0f;
                consumedExpiredLine = skills.ConsumeLine(out _);
                expirationApplied = !player.CurrentLine.HasValue && skills.LineRemaining == 0f && vfx.IsExpiryPlaying
                    && skills.DrawCooldownRemaining > 0f && skills.DrawCooldownRemaining <= skills.CancelDrawCooldown;
                boundaryReached = true;
                InputSystem.onBeforeUpdate -= beforeInputUpdate; // 실제 만료 경계에서 한 번 요청한 뒤 수집 종료
            };
            InputSystem.onBeforeUpdate += beforeInputUpdate; // Dynamic Input 직전의 경로 인계·만료 순서 수집
            yield return Until(() => boundaryReached, "handoff before Skill.Update at expiration"); // 게임 시간이 만료 기한에 도달한 첫 입력 갱신 대기
            Require(pendingBeforeSkillUpdate && !consumedExpiredLine && expirationApplied,
                "An overdue Line must be rejected and naturally expired by the handoff API before Skill.Update can clear it.");
            remaining = skills.DrawCooldownRemaining;
            Require(!skills.ConsumeLine(out _) && skills.DrawCooldownRemaining == remaining && vfx.IsExpiryPlaying,
                "Repeated handoff after boundary expiry must not restart its short cooldown or expiration VFX.");
            passed("F-P06 Line handoff returns live geometry once with the normal cooldown and rejects overdue geometry before Skill.Update with natural expiry");
        }
        finally
        {
            // 검사 상태를 지우고 기존 서비스·체력·튜닝값 복구
            if (beforeInputUpdate != null) InputSystem.onBeforeUpdate -= beforeInputUpdate; // 실패·중단에서도 만료 경계 수집 해제
            if (capturedFrames > 0)
            {
                File.WriteAllLines("Logs/DrawExpiryFrames/frames.csv", frameTimes);
                File.WriteAllLines("Logs/DrawExpiryFrames/particles.csv", particleTimes);
            }
            camera.orthographicSize = cameraZoom;
            gameCamera.Follow(cameraTarget);
            camera.transform.position = cameraPosition;
            skills.enabled = true;
            vfx.enabled = true;
            player.IsCutScene = false;
            player.IsDead = false;
            skills.ResetSkills();
            skills.LineLifetime = lifetime;
            skills.CancelDrawCooldown = cancelCooldown;
            skills.NormalDrawCooldown = normalCooldown;
            skills.ConfirmSlashCooldown = slashCooldown;
            vfx.WarningFraction = warningFraction;
            vfx.SlowBlinkPeriod = slowPeriod;
            vfx.FastBlinkPeriod = fastPeriod;
            vfx.ExpiryDuration = expiryDuration;
            player.HP = health;
            player.IsInvulnerable = invulnerable;
            manager.SetDrawPaused(false);
            manager.SetGameplayPaused(paused);
            input.BlockInput = blocked;
        }
    }

    /// <summary>실제 입자 메시의 TEXCOORD1에 월드 중심이 전달되는지 확인</summary>
    private static void AssertParticleCenterStream(ParticleSystemRenderer renderer, Camera camera)
    {
        // UV 두 채널 뒤에 중심 좌표를 넣는 선언 순서 확인
        List<ParticleSystemVertexStream> streams = new List<ParticleSystemVertexStream>();
        renderer.GetActiveVertexStreams(streams);
        Require(streams.Count == 5 && streams[0] == ParticleSystemVertexStream.Position && streams[1] == ParticleSystemVertexStream.Color
            && streams[2] == ParticleSystemVertexStream.UV && streams[3] == ParticleSystemVertexStream.UV2 && streams[4] == ParticleSystemVertexStream.Center,
            "The particle stream layout must fill TEXCOORD0 with UV and UV2 before the Center stream.");

        // native 메시의 첫 쿼드 중심과 실제 정점 평균을 대조해 채널 패킹 검증
        Mesh mesh = new Mesh();
        try
        {
            renderer.BakeMesh(mesh, camera);
            Vector3[] vertices = mesh.vertices;
            List<Vector4> centers = new List<Vector4>();
            mesh.GetUVs(1, centers);
            Require(vertices.Length >= 4 && centers.Count == vertices.Length,
                $"The baked particle mesh must contain a TEXCOORD1 center for each vertex: vertices={vertices.Length}, centers={centers.Count}.");
            Vector3 center = new Vector3(centers[0].x, centers[0].y, centers[0].z);
            Vector3 average = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                Vector3 currentCenter = new Vector3(centers[i].x, centers[i].y, centers[i].z);
                Require(Vector3.Distance(currentCenter, center) < 0.001f, "All four particle corners must receive the same native center value.");
                average += vertices[i];
            }
            average *= 0.25f;
            Require(Vector3.Distance(average, center) < 0.002f,
                $"The native Center stream must match the first quad's world-space midpoint: center={center}, midpoint={average}.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    /// <summary>공유 재질 대신 실제 렌더러의 개별 속성으로 알파가 반영되는지 확인</summary>
    private static void AssertVisualParameters(DrawLineVfx visual, LineRenderer renderer, MaterialPropertyBlock properties, float opacity)
    {
        // 상태 조회값과 GPU에 전달한 렌더러 속성을 같은 진행도로 비교
        properties.Clear();
        renderer.GetPropertyBlock(properties);
        Require(Mathf.Abs(visual.Opacity - opacity) < 0.001f && Mathf.Abs(properties.GetFloat("_Opacity") - opacity) < 0.001f,
            "The renderer property block must match its own opacity without changing shared material state.");
    }

    /// <summary>일반 정지 중 같은 실제 입자의 위치·수명·현재 알파가 보존되는지 확인</summary>
    private static void AssertParticlesFrozen(ParticleSystem particles, ParticleSystem.Particle[] previous, int previousCount, ParticleSystem.Particle[] buffer)
    {
        // 입자 배열 순서에 기대지 않고 동일한 식별자의 상태를 비교
        Dictionary<uint, ParticleSystem.Particle> bySeed = new Dictionary<uint, ParticleSystem.Particle>();
        for (int i = 0; i < previousCount; i++) bySeed.Add(previous[i].randomSeed, previous[i]);
        int currentCount = particles.GetParticles(buffer);
        Require(currentCount == previousCount && currentCount > 0, "Pause must preserve the same live particle count.");
        for (int i = 0; i < currentCount; i++)
        {
            ParticleSystem.Particle current = buffer[i];
            Require(bySeed.TryGetValue(current.randomSeed, out ParticleSystem.Particle before)
                && Vector3.Distance(current.position, before.position) < 0.0001f
                && Mathf.Abs(current.remainingLifetime - before.remainingLifetime) < 0.0001f
                && current.GetCurrentColor(particles).a == before.GetCurrentColor(particles).a,
                "Paused particles must preserve actual world position, remaining lifetime and alpha.");
        }
    }

    /// <summary>기존 실행 프레임에서 경고·증발 화면과 실제 입자 좌표를 약20Hz로 기록</summary>
    private static void CaptureFrame(DrawVfx vfx, ParticleSystem particles, ParticleSystem.Particle[] buffer, List<string> times, List<string> particleTimes, ref double nextCaptureTime, ref int count)
    {
        // 같은 시각의 중복 캡처를 피하고 실제 게임 시간 간격을 PNG 순서와 함께 보관
        double now = Time.timeAsDouble;
        if (now < nextCaptureTime) return;
        ScreenCapture.CaptureScreenshot($"Logs/DrawExpiryFrames/frame-{count:D4}.png");
        int particleCount = particles.GetParticles(buffer);
        Vector3 center = Vector3.zero;
        float alpha = 0f;
        float size = 0f;
        for (int i = 0; i < particleCount; i++)
        {
            ParticleSystem.Particle particle = buffer[i];
            float currentSize = particle.GetCurrentSize(particles);
            Vector3 currentSize3D = particle.GetCurrentSize3D(particles);
            byte currentAlpha = particle.GetCurrentColor(particles).a;
            center += particle.position;
            alpha += currentAlpha / 255f;
            size += currentSize;
            particleTimes.Add(FormattableString.Invariant($"{count},{particle.randomSeed},{particle.position.x:F6},{particle.position.y:F6},{particle.position.z:F6},{particle.startSize3D.x:F6},{particle.startSize3D.y:F6},{currentSize3D.x:F6},{currentSize3D.y:F6},{currentAlpha},{particle.remainingLifetime:F6}"));
        }
        if (particleCount > 0)
        {
            center /= particleCount;
            alpha /= particleCount;
            size /= particleCount;
        }
        times.Add(FormattableString.Invariant($"{count},{now:F6},{Time.realtimeSinceStartupAsDouble:F6},{vfx.LineOpacity:F6},{vfx.ExpiryProgress:F6},{(vfx.IsExpiryPlaying ? 1 : 0)},{particleCount},{center.x:F6},{center.y:F6},{alpha:F6},{size:F6}"));
        count++;
        nextCaptureTime = now + 0.05d;
    }

    /// <summary>실제 Draw 확정 API로 시작점·방향을 Player 보관 경로로 변환</summary>
    private static Line Confirm(PlayerCharacter player, Vector2 start, Vector2 direction)
    {
        SkillController skills = player.Skills;
        Require(player.ApplyDraw(), "Character must permit the Draw confirmation used by lifetime checks.");
        Require(skills.PlacePoint(start) && skills.PlacePoint(start + direction), "Two valid points must be accepted.");
        Line expected = skills.Drawer.PreviewLine.Value;
        Require(skills.ConfirmLine(), "The manager must accept valid confirmed geometry.");
        return expected;
    }

    /// <summary>확정 경로의 데이터가 같은 값인지 비교</summary>
    private static bool SameLine(Line first, Line second)
    {
        return Vector2.Distance(first.StartPoint, second.StartPoint) < 0.001f && Vector2.Distance(first.Direction, second.Direction) < 0.001f
            && Mathf.Abs(first.Length - second.Length) < 0.001f && Mathf.Abs(first.Width - second.Width) < 0.001f;
    }

    /// <summary>실제 게임 시간이 흐르는 동안 수명 진행 관찰</summary>
    private static IEnumerator GameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end, "game time"); // Skill.Update에 사용하는 게임 시간 경과 대기
    }

    /// <summary>게임 시간 정지와 별도로 실제 경과 시간 관찰</summary>
    private static IEnumerator RealSeconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        yield return Until(() => Time.realtimeSinceStartup >= end, "real time"); // 정지 중 데이터·VFX 위상 보존을 관찰할 실제 시간 대기
    }

    /// <summary>지정한 렌더 프레임만큼 입력·시간 변경 반영 대기</summary>
    private static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        yield return Until(() => Time.frameCount >= end, "render frames"); // 상태 전환 프레임의 시간 샘플이 반영될 때까지 대기
    }

    /// <summary>기존 Editor 실행기에서 상태 완료와 제한 시간 확인</summary>
    private static IEnumerator Until(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("F-P06 timed out: " + description);
            yield return null;
        }
    }

    /// <summary>수명·원본 데이터·표현 계약 위반을 기존 검사 보고서에 전달</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
