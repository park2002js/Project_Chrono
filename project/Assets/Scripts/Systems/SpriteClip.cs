using System;
using UnityEngine;

/// <summary>프레임별 Sprite와 표시 시간을 순서대로 보관하는 효과 재생 데이터</summary>
[CreateAssetMenu(fileName = "SpriteClip", menuName = "Project Chrono/Sprite Clip")]
public class SpriteClip : ScriptableObject
{
    public Sprite[] Frames = Array.Empty<Sprite>(); // 순서대로 표시할 Sprite이며 빈 칸은 그 시간 동안 숨김
    public float[] Durations = Array.Empty<float>(); // 각 프레임을 유지할 초 단위 시간
    public bool Loop; // 마지막 프레임 뒤에 반복 시작 프레임으로 돌아갈지 설정
    public int LoopStart; // 반복할 때 돌아갈 프레임 번호
    public int FrameCount => Frames != null && Durations != null ? Mathf.Min(Frames.Length, Durations.Length) : 0; // 시간과 짝이 맞는 유효 프레임 수

    /// <summary>첫 프레임부터 마지막 프레임까지 한 번 재생하는 전체 시간 계산</summary>
    public float GetLength()
    {
        float total = 0f;
        for (int i = 0; i < FrameCount; i++) total += Mathf.Max(0f, Durations[i]);
        return total;
    }

    /// <summary>재생 시작 뒤 경과 시간에 표시할 프레임 번호를 찾고 끝난 단발 클립은 -1 반환</summary>
    public int GetFrameIndex(float elapsed)
    {
        // 프레임이 없거나 음수 시간이면 표시할 내용 없음
        int count = FrameCount;
        if (count == 0 || elapsed < 0f) return -1;
        float length = GetLength();
        if (length <= 0f) return Loop ? Mathf.Clamp(LoopStart, 0, count - 1) : -1;

        // 반복 클립은 반복 구간 길이로 시간을 접고 단발 클립은 끝에서 종료
        if (elapsed >= length)
        {
            if (!Loop) return -1;
            int start = Mathf.Clamp(LoopStart, 0, count - 1);
            float loopLength = 0f;
            for (int i = start; i < count; i++) loopLength += Mathf.Max(0f, Durations[i]);
            if (loopLength <= 0f) return start;
            float head = length - loopLength;
            elapsed = head + Mathf.Repeat(elapsed - head, loopLength);
        }
        float time = 0f;
        for (int i = 0; i < count; i++)
        {
            time += Mathf.Max(0f, Durations[i]);
            if (elapsed < time) return i;
        }
        return count - 1;
    }
}
