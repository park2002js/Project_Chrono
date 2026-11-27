using UnityEngine;

/// <summary>강한 착지 지점에서 화면을 바깥으로 밀어내는 방사 왜곡의 재생 상태</summary>
public static class ImpactDistortionFx
{
    public static bool IsPlaying => playing && Time.time - startTime < duration; // 현재 왜곡을 그려야 하는지 여부
    public static Vector2 WorldPoint { get; private set; } // 왜곡 중심의 월드 좌표
    public static float Progress => duration > 0f ? Mathf.Clamp01((Time.time - startTime) / duration) : 1f; // 게임 시간 기준 0~1 진행률

    private static bool playing; // 한 번이라도 재생을 요청했는지 여부
    private static float startTime; // 재생을 시작한 게임 시각
    private static float duration; // 이번 재생의 전체 길이

    /// <summary>플레이 시작마다 이전 실행의 재생 상태 초기화</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        playing = false;
    }

    /// <summary>지정한 월드 지점을 중심으로 왜곡을 처음부터 재생</summary>
    public static void Play(Vector2 worldPoint, float length = 0.8333f)
    {
        WorldPoint = worldPoint;
        duration = Mathf.Max(0.01f, length);
        startTime = Time.time;
        playing = true;
    }

    /// <summary>진행 중인 왜곡을 즉시 중단</summary>
    public static void Stop()
    {
        playing = false;
    }
}
