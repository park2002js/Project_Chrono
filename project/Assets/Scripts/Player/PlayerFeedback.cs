using System.Collections.Generic;
using UnityEngine;

/// <summary>자기 플레이어의 공격·적중·걷기·대시·Draw 점 소리와 월드 고정 피격 섬광 표시</summary>
[DefaultExecutionOrder(20)]
public class PlayerFeedback : MonoBehaviour
{
    public AudioSource Source; // Slash 소리와 분리해 효과음을 겹쳐 재생할 자기 오디오 소스
    public AudioClip[] AttackStartClips = new AudioClip[3]; // 1·2·3타 시작 순간의 소리이며 1·2타는 스윙, 3타는 납도 준비이고 비우면 생략
    public AudioClip[] AttackStrikeClips = new AudioClip[3]; // 1·2·3타 판정 개시 순간의 소리이며 비우면 생략
    public AudioClip HitClip; // 피해가 실제 적용된 대상의 적중음
    public AudioClip[] FootstepClips; // 같은 타일 재질의 발소리이며 여러 개면 차례로 순환
    public AudioClip DashClip; // 대시 시작 순간의 소리
    public AudioClip DrawPointClip; // Draw 편집에서 점 하나를 찍은 순간의 소리
    public Sprite[] HitFrames; // 원작 SwordSlashFx의0~6 프레임
    public Material HitMaterial; // 밝기 속성 없이 원본 색을 표시할 공용 Unlit 재질
    public float Volume = 0.8f; // 자기 효과음의 공통 재생 음량
    public float AttackVolume = 2f; // 작게 녹음된 공격 소리를 적중음 크기에 맞출 추가 재생 배율
    public float FootstepVolume = 3f; // 원본의 작은 발소리를 보정할 추가 재생 배율
    public float DashVolume = 4f; // 작게 녹음된 대시 소리를 보정할 추가 재생 배율
    public float DrawPointVolume = 1f; // Draw 점 소리의 추가 재생 배율
    public float StepDistance = 1.2f; // 정상 걷기의 다음 발소리까지 실제 이동할 거리

    private readonly List<HitVisual> visuals = new List<HitVisual>(8); // 동시 적중 수만큼 준비해 재사용할 섬광 표시
    private float walkingDistance; // 다음 발소리까지 누적한 실제 일반 이동 거리
    private int footstepIndex; // 같은 소리 연속 재생 없이 순환할 발소리 위치
    private bool ready; // 최초 에셋 연결 검사를 통과했는지 표시
    private const float HitFps = 20f; // 원작 피격 이미지의 초당 표시 수
    private const float HitPositionRadius = 0.2f; // 피격 접점 주변의 무작위 위치 반경
    private const float HitAngleError = 10f; // 수직 섬광 방향에 더할 좌우 최대 각도 오차
    private const int HitSortingOrder = 30; // 맵과 몸체 위에 표시할 피격 섬광 순서

    /// <summary>직렬화한 자기 에셋을 확인하고 재사용 섬광 여덟 개 준비</summary>
    private void Awake()
    {
        // 다른 객체를 찾지 않고 프리팹의 자기 소리·이미지 연결 확인
        ready = Source != null && AttackStartClips != null && AttackStartClips.Length == 3 && AttackStrikeClips != null && AttackStrikeClips.Length == 3
            && HitClip != null && DashClip != null && DrawPointClip != null
            && FootstepClips != null && FootstepClips.Length > 0 && HitFrames != null && HitFrames.Length == 7 && HitMaterial != null;
        if (ready)
        {
            foreach (AudioClip clip in FootstepClips) ready &= clip != null;
            foreach (Sprite frame in HitFrames) ready &= frame != null;
        }
        if (!ready) { Debug.LogError(name + ": Connect Player feedback audio and seven hit frames.", this); return; }

        // 자기 소리만 중단할 수 있도록 Slash와 독립된 소스 사용
        Source.playOnAwake = false;
        Source.loop = false;
        Source.spatialBlend = 0f;
        Source.ignoreListenerPause = true;
        for (int i = 0; i < 8; i++) CreateVisual();
    }

    /// <summary>게임 시간으로 피격 프레임을 진행하고 종료한 표시 반환</summary>
    private void Update()
    {
        if (!ready) return;
        foreach (HitVisual visual in visuals)
        {
            if (!visual.Active) continue;
            float age = (float)(Time.timeAsDouble - visual.StartedAt);
            int frame = Mathf.FloorToInt((age + 0.000001f) * HitFps);
            visual.Active = frame < HitFrames.Length;
            visual.Renderer.enabled = visual.Active;
            if (!visual.Active) continue;
            visual.Renderer.sprite = HitFrames[frame];
            visual.Renderer.transform.position = visual.Position;
        }
    }

    private void OnDisable()
    {
        // 자기 소리·월드 표시만 닫고 다음 활성화의 걷기 거리 초기화
        if (Source != null) Source.Stop();
        foreach (HitVisual visual in visuals) { visual.Active = false; if (visual.Renderer != null) visual.Renderer.enabled = false; }
        walkingDistance = 0f;
    }

    /// <summary>AttackController가 새 타를 시작한 순간 그 타의 시작 소리 재생</summary>
    public void PlayAttackStart(int attack)
    {
        if (attack >= 0 && attack < 3) Play(AttackStartClips[attack], Mathf.Max(0f, AttackVolume));
    }

    /// <summary>AttackController가 판정을 연 순간 그 타의 휘두르기 소리 재생</summary>
    public void PlayAttackStrike(int attack)
    {
        if (attack >= 0 && attack < 3) Play(AttackStrikeClips[attack], Mathf.Max(0f, AttackVolume));
    }

    /// <summary>허용된 대시가 시작된 순간 대시 소리 재생</summary>
    public void PlayDash()
    {
        Play(DashClip, Mathf.Max(0f, DashVolume));
    }

    /// <summary>Draw 편집에서 점 하나가 실제로 놓인 순간 소리 재생</summary>
    public void PlayDrawPoint()
    {
        Play(DrawPointClip, Mathf.Max(0f, DrawPointVolume));
    }

    /// <summary>유효 피해의 접점에 공격 방향을 반영한 섬광과 적중음 한 번 생성</summary>
    public void PlayImpact(Vector2 hitPoint, float attackAngle)
    {
        if (!ready || !isActiveAndEnabled) return;

        // 원작의 +Y 기준 각도와 좌우 반사를 우리 +X 공격각으로 변환
        HitVisual visual = null;
        foreach (HitVisual slot in visuals) if (!slot.Active) { visual = slot; break; }
        if (visual == null) visual = CreateVisual();
        float sign = Mathf.Cos(attackAngle * Mathf.Deg2Rad) < -0.000001f ? -1f : 1f;
        float angle = attackAngle - 90f + 90f * sign + UnityEngine.Random.Range(-HitAngleError, HitAngleError);
        visual.Position = hitPoint + UnityEngine.Random.insideUnitCircle * HitPositionRadius;
        visual.StartedAt = Time.timeAsDouble;
        visual.Active = true;
        visual.Renderer.transform.position = visual.Position;
        visual.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        visual.Renderer.transform.localScale = new Vector3(sign, 1f, 1f);
        visual.Renderer.sprite = HitFrames[0];
        visual.Renderer.enabled = true;

        // 피해가 성공한 수신자만 소리 재생
        Play(HitClip, 1f);
    }

    /// <summary>강제 이동을 제외한 실제 걷기 거리만 받아 타일 발소리 재생</summary>
    public void AddWalkingMovement(Vector2 movement)
    {
        // 정지·벽·강제 이동 구간에서는 다음 걷기까지 누적 거리 초기화
        if (!ready || !isActiveAndEnabled || movement.sqrMagnitude <= 0.0000001f)
        { walkingDistance = 0f; return; }
        if (float.IsNaN(StepDistance) || float.IsInfinity(StepDistance) || StepDistance <= 0f) return;

        // 큰 프레임에서도 지나간 발소리를 한꺼번에 겹쳐 재생하지 않음
        walkingDistance += movement.magnitude;
        if (walkingDistance < StepDistance) return;
        walkingDistance %= StepDistance;
        AudioClip clip = FootstepClips[footstepIndex];
        footstepIndex = (footstepIndex + 1) % FootstepClips.Length;
        Play(clip, Mathf.Max(0f, FootstepVolume));
    }

    /// <summary>자기 오디오 소스로 효과음을 겹쳐 재생</summary>
    private void Play(AudioClip clip, float extraVolume)
    {
        if (!ready || !isActiveAndEnabled || Source == null || !Source.isActiveAndEnabled || clip == null) return;
        Source.PlayOneShot(clip, Mathf.Clamp01(Volume) * extraVolume * GameSettings.SfxVolume);
    }

    /// <summary>플레이어 수명 안에서 돌려쓸 월드 고정 섬광 표시 한 개 준비</summary>
    private HitVisual CreateVisual()
    {
        var go = new GameObject("HitFeedback" + visuals.Count);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = HitMaterial;
        renderer.sortingOrder = HitSortingOrder;
        renderer.color = Color.white;
        renderer.enabled = false;
        var visual = new HitVisual { Renderer = renderer };
        visuals.Add(visual);
        return visual;
    }

    /// <summary>피격 후 대상·공격자가 움직여도 월드 위치를 유지할 표시 한 개</summary>
    private class HitVisual
    {
        public SpriteRenderer Renderer; // 재사용할 피격 이미지 표시
        public Vector3 Position; // 생성 순간에 고정한 실제 월드 위치
        public double StartedAt; // 게임 시간 기준 생성 시각
        public bool Active; // 현재 프레임을 표시 중인지 여부
    }
}
