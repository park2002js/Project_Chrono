using UnityEngine;

/// <summary>탄환의 몸체와 피해를 바꾸지 않고 바깥 광원의 알파만 부드럽게 왕복</summary>
public class ProjectileVfx : MonoBehaviour
{
    public SpriteRenderer Halo; // 부드러운 광원 원본을 표시할 자식 Sprite
    [Range(0f, 1f)] public float MinimumOpacity = 0.7f; // 광원 원래 알파에 곱할 왕복의 최솟값
    public float PulseFrequency = 1f; // 광원이 한 번 밝아졌다 돌아올 초당 주기 수
    public float AuraOpacity { get; private set; } = 1f; // 광원 원본에 현재 곱하는 알파 배율
    public float PulsePhase { get; private set; } // 게임 시간으로 진행하는 광원 왕복의 0~1 위상

    private SpriteRenderer cachedHalo; // 같은 탄환 수명에서 재사용할 최초 광원 참조
    private Color haloBaseColor; // 비활성화와 재시작에서 복원할 광원 원래 색과 알파

    /// <summary>직렬화된 광원과 원래 표시색을 최초 한 번 준비</summary>
    private void Awake()
    {
        PrepareHalo();
    }

    private void OnEnable()
    {
        ResetPulse();
    }

    /// <summary>탄환과 같은 게임 시간으로 광원의 알파만 왕복</summary>
    private void Update()
    {
        // 정지 중에는 현재 위상과 표시를 유지하고 고정 참조만 사용
        float elapsed = Time.deltaTime;
        if (cachedHalo == null || Time.timeScale <= 0f || elapsed <= 0f
            || float.IsNaN(PulseFrequency) || float.IsInfinity(PulseFrequency) || PulseFrequency < 0f) return;

        // 몸체는 유지하고 광원의 원본 알파에만 배율 적용
        PulsePhase = Mathf.Repeat(PulsePhase + elapsed * PulseFrequency, 1f);
        AuraOpacity = Mathf.Lerp(Mathf.Clamp01(MinimumOpacity), 1f, (Mathf.Cos(PulsePhase * Mathf.PI * 2f) + 1f) * 0.5f);
        Color color = haloBaseColor;
        color.a *= AuraOpacity;
        cachedHalo.color = color;
    }

    private void OnDisable()
    {
        // 탄환 제거와 비활성화에서는 자기 광원 원래 알파만 복원
        PulsePhase = 0f;
        AuraOpacity = 1f;
        if (cachedHalo != null) cachedHalo.color = haloBaseColor;
    }

    /// <summary>같은 광원 참조와 원색을 유지한 채 왕복 위상을 처음으로 복원</summary>
    public void ResetPulse()
    {
        PrepareHalo();
        PulsePhase = 0f;
        AuraOpacity = 1f;
        if (cachedHalo != null) cachedHalo.color = haloBaseColor;
    }

    /// <summary>광원 참조와 원색은 최초 유효 연결에서만 확보</summary>
    private void PrepareHalo()
    {
        if (cachedHalo != null || Halo == null) return;
        cachedHalo = Halo;
        haloBaseColor = Halo.color;
    }
}
