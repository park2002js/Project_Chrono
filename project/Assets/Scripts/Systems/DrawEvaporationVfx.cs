using UnityEngine;

/// <summary>Draw 선에서 작은 원형 알갱이를 흩뿌려 위로 떠오르게 하는 수동 진행 파티클</summary>
[RequireComponent(typeof(ParticleSystem))]
public class DrawEvaporationVfx : MonoBehaviour
{
    public ParticleSystem Particles { get; private set; } // 소유자가 일회 방출·정지·실제 이동을 확인할 재사용 파티클

    private const int MaximumParticles = 768; // 긴 선에서도 한 번 방출할 원형 알갱이의 최대 수
    private ParticleSystemRenderer particleRenderer; // 활성 상태를 바꾸지 않고 표현만 숨길 렌더러
    private System.Random random; // 다른 게임 로직의 난수 상태와 분리한 알갱이 배치·이동 난수
    private bool prepared; // 원형 입자 재질과 파티클 모듈이 연결됐는지 보관
    private uint nextParticleSeed = 1; // 동일 입자의 위치·수명 추적에 사용할 중복 없는 식별 난수

    /// <summary>자기 파티클·렌더러·난수만 한 번 확보하고 자동 재생 중단</summary>
    private void Awake()
    {
        Particles = GetComponent<ParticleSystem>();
        particleRenderer = GetComponent<ParticleSystemRenderer>();
        random = new System.Random(18671);
        Particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        Particles.useAutoRandomSeed = false;
        Particles.randomSeed = 18671u;
    }

    /// <summary>원형 입자 재질과 고정 크기·상승·알파 소멸 모듈을 한 번 연결</summary>
    public bool Prepare(Material material)
    {
        // 연결 실패는 소유자가 처리하고 일부만 준비된 상태로 재생하지 않음
        if (Particles == null || particleRenderer == null || material == null) return false;
        Particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 씬 루트의 월드 좌표에서 크기 변화나 자동 방출 없이 조각만 수동 진행
        ParticleSystem.MainModule main = Particles.main;
        main.loop = false;
        main.prewarm = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.simulationSpeed = 1f;
        main.useUnscaledTime = false;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.startSize3D = true;
        main.startRotation3D = true;
        main.maxParticles = MaximumParticles;
        main.stopAction = ParticleSystemStopAction.None;
        ParticleSystem.EmissionModule emission = Particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = Particles.shape;
        shape.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = Particles.sizeOverLifetime;
        size.enabled = false;
        ParticleSystem.SizeBySpeedModule sizeBySpeed = Particles.sizeBySpeed;
        sizeBySpeed.enabled = false;

        // 작은 알갱이의 크기와 색은 유지하고 떠오른 뒤 알파만 부드럽게 소멸
        ParticleSystem.ForceOverLifetimeModule force = Particles.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        force.x = 0f;
        force.y = 0.35f;
        force.z = 0f;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.75f, 0.65f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = Particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(fade);

        // 정사각 쿼드에 원형 중심과 부드러운 테두리를 표시하는 입자 재질 연결
        particleRenderer.sharedMaterial = material;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortingOrder = 100;
        particleRenderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.Center
        }); // UV 한 채널을 채운 다음 월드 입자 중심을 셰이더로 전달
        prepared = true;
        Clear();
        return true;
    }

    /// <summary>같은 선 영역에 작은 원형 알갱이를 한 번만 방출하고 자동 진행 없이 대기</summary>
    public void Play(Line line, float duration)
    {
        // 이전 조각을 비운 다음 유효한 수명에서만 새 일회 방출 준비
        Clear();
        if (!prepared || duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return;
        int count = Mathf.Clamp(Mathf.CeilToInt(line.Length * 12f), 1, MaximumParticles);
        Vector2 perpendicular = new Vector2(-line.Direction.y, line.Direction.x);
        // 원래 FX 전체 길이에서 작은 원형 알갱이가 양옆·위로 떠오르도록 배치
        for (int i = 0; i < count; i++)
        {
            float along = (i + Range(0f, 1f)) / count;
            Vector2 point = line.StartPoint + line.Direction * (line.Length * along)
                + perpendicular * Range(-line.Width * 0.5f, line.Width * 0.5f);
            float diameter = Range(0.045f, 0.085f);
            float side = Range(0.5f, 1.3f) * (random.NextDouble() < 0.5 ? -1f : 1f);
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = new Vector3(point.x, point.y, 0f),
                velocity = new Vector3(side, Range(0.9f, 1.8f), 0f),
                startLifetime = duration * Range(0.65f, 0.95f),
                startSize3D = new Vector3(diameter, diameter, 1f),
                rotation3D = Vector3.zero,
                startColor = Color.white,
                randomSeed = nextParticleSeed++
            };
            if (nextParticleSeed == 0) nextParticleSeed = 1;
            Particles.Emit(particle, 1);
        }
        Particles.Pause(false);
    }

    /// <summary>소유자가 전달한 게임 시간만큼 실제 위치·수명·알파를 진행</summary>
    public void Tick(float deltaTime)
    {
        if (!prepared || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        Particles.Simulate(deltaTime, false, false, false);
        Particles.Pause(false);
    }

    /// <summary>교체·수동 해제·사망에서 남은 조각을 즉시 비우고 재사용 상태로 초기화</summary>
    public void Clear()
    {
        if (Particles == null) return;
        Particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        Particles.Simulate(0f, false, true, false);
        Particles.Pause(false);
    }

    /// <summary>진행 상태를 지우지 않고 독립된 월드 입자의 표시만 전환</summary>
    public void SetVisible(bool visible)
    {
        if (particleRenderer != null) particleRenderer.enabled = visible;
    }

    /// <summary>게임 전역 난수 상태를 변경하지 않고 지정 범위의 값을 생성</summary>
    private float Range(float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)random.NextDouble();
    }
}
