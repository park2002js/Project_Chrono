using System;
using UnityEngine;

/// <summary>Player의 방향별 기본 모션과 생성·사망·발도를 구분할 표시 상태</summary>
public enum PlayerSpriteAnimationState
{
    IdleLeftFront, // 왼쪽 앞을 바라보는 정지 모션
    IdleRightFront, // 오른쪽 앞을 바라보는 정지 모션
    IdleLeftBack, // 왼쪽 뒤를 바라보는 정지 모션
    IdleRightBack, // 오른쪽 뒤를 바라보는 정지 모션
    MoveLeftFront, // 왼쪽 앞을 바라보는 이동 모션
    MoveRightFront, // 오른쪽 앞을 바라보는 이동 모션
    MoveLeftBack, // 왼쪽 뒤를 바라보는 이동 모션
    MoveRightBack, // 오른쪽 뒤를 바라보는 이동 모션
    DashLeftFront, // 남쪽 공통 공격 첫 프레임을 사용하는 왼쪽 대시 상태
    DashRightFront, // 남쪽 공통 공격 첫 프레임을 사용하는 오른쪽 대시 상태
    DashLeftBack, // 북쪽 공통 공격 첫 프레임을 사용하는 왼쪽 대시 상태
    DashRightBack, // 북쪽 공통 공격 첫 프레임을 사용하는 오른쪽 대시 상태
    Death, // 사망 후 마지막 자세를 유지할 모션
    Spawn, // 체크포인트에서 일어서는 모션
    SlashDraw, // 몸을 낮추며 검을 일부 꺼내는 Slash 준비 모션
    SlashHold, // 일부 꺼낸 검을 유지하는 Slash 대기 자세
    SlashSheathe, // 칼날이 완전히 닫힐 때까지의 Slash 납도 모션
    IdleLeftUpper, // 왼쪽 위 사선을 바라보는 정지 모션
    IdleRightUpper, // 오른쪽 위 사선을 바라보는 정지 모션
    IdleLeftLower, // 왼쪽 아래 사선을 바라보는 정지 모션
    IdleRightLower, // 오른쪽 아래 사선을 바라보는 정지 모션
    MoveLeftUpper, // 왼쪽 위 사선을 바라보는 이동 모션
    MoveRightUpper, // 오른쪽 위 사선을 바라보는 이동 모션
    MoveLeftLower, // 왼쪽 아래 사선을 바라보는 이동 모션
    MoveRightLower, // 오른쪽 아래 사선을 바라보는 이동 모션
    AttackLeftBack, // 북쪽 공통 원본을 사용하는 왼쪽 공격 상태
    AttackRightBack, // 북쪽 공통 원본을 사용하는 오른쪽 공격 상태
    AttackLeftUpper, // 왼쪽 위 사선을 바라보는 공격 모션
    AttackRightUpper, // 오른쪽 위 사선을 바라보는 공격 모션
    AttackLeftLower, // 왼쪽 아래 사선을 바라보는 공격 모션
    AttackRightLower, // 오른쪽 아래 사선을 바라보는 공격 모션
    AttackLeftFront, // 남쪽 공통 원본을 사용하는 왼쪽 공격 상태
    AttackRightFront, // 남쪽 공통 원본을 사용하는 오른쪽 공격 상태
    DashLeftUpper, // 왼쪽 위 사선의 독립 대시 자세
    DashRightUpper, // 오른쪽 위 사선의 독립 대시 자세
    DashLeftLower, // 왼쪽 아래 사선의 독립 대시 자세
    DashRightLower // 오른쪽 아래 사선의 독립 대시 자세
}

/// <summary>Sephiria AnimationSet처럼 상태별 Sprite·재생 시간·반전을 보관할 Player 전용 데이터</summary>
[CreateAssetMenu(fileName = "PlayerSpriteAnimationSet", menuName = "Project Chrono/Player Sprite Animation Set")]
public class PlayerSpriteAnimationSet : ScriptableObject
{
    public StateData[] States = Array.Empty<StateData>(); // CharacterRendering이 상태 진입 때 선택할 모션 목록

    /// <summary>상태가 바뀔 때만 해당 모션의 원본 데이터 조회</summary>
    public StateData GetState(PlayerSpriteAnimationState state)
    {
        // 설정이 비어 있으면 표시 측에서 누락을 알리고 기존 Sprite 유지
        if (States == null) return null;
        for (int i = 0; i < States.Length; i++)
            if (States[i] != null && States[i].State == state)
                return States[i];
        return null;
    }

    /// <summary>한 상태의 프레임과 기본 FPS 또는 프레임별 유지 시간</summary>
    [Serializable]
    public class StateData
    {
        public PlayerSpriteAnimationState State; // 이 프레임 묶음을 사용할 표시 상태
        public Sprite[] Frames = Array.Empty<Sprite>(); // 처음부터 순서대로 표시할 Sprite 목록
        public float FramesPerSecond = 10f; // 개별 유지 시간이 없을 때 사용할 초당 프레임 수
        public float[] FrameDurations = Array.Empty<float>(); // 생성·사망처럼 프레임별 유지 시간이 다른 모션의 초 단위 설정
        public bool Loop = true; // 끝에 도달한 기본 모션을 처음부터 반복할지 구분
        public bool FlipX; // 왼쪽 원본으로 오른쪽 방향을 표시할 좌우 반전

        /// <summary>개별 시간이 누락되거나 유효하지 않으면 안전한 FPS 간격 사용</summary>
        public float GetFrameDuration(int index)
        {
            // 작성된 개별 시간은 양수·유한 값일 때만 반영
            if (FrameDurations != null && index >= 0 && index < FrameDurations.Length)
            {
                float duration = FrameDurations[index];
                if (duration > 0f && !float.IsInfinity(duration))
                    return Mathf.Max(0.0001f, duration);
            }

            // 잘못된 FPS로 재생이 멈추거나 0으로 나누지 않도록 기본 10FPS 보장
            return FramesPerSecond > 0f && !float.IsInfinity(FramesPerSecond)
                ? Mathf.Max(0.0001f, 1f / FramesPerSecond) : 0.1f;
        }

        /// <summary>상태 진입 때 현재 프레임 목록의 전체 재생 길이 계산</summary>
        public float GetTotalDuration()
        {
            // 실제 Sprite 개수만큼의 유지 시간을 합산
            float duration = 0f;
            if (Frames != null)
                for (int i = 0; i < Frames.Length; i++)
                    duration += GetFrameDuration(i);
            return duration;
        }

        /// <summary>경과시간에 해당하는 프레임을 찾고 끝에서는 마지막 자세 유지</summary>
        public int GetFrameIndex(float time)
        {
            // 기본 모션의 균일한 FPS는 프레임 목록 순회 없이 바로 계산
            int count = Frames == null ? 0 : Frames.Length;
            if (count <= 1) return 0;
            if (FrameDurations == null || FrameDurations.Length == 0)
                return Mathf.Clamp(Mathf.FloorToInt(time / GetFrameDuration(0)), 0, count - 1);

            // 마지막 프레임 뒤의 시간도 마지막 자세로 처리
            float frameEnd = 0f;
            for (int i = 0; i < count - 1; i++)
            {
                frameEnd += GetFrameDuration(i);
                if (time < frameEnd) return i;
            }
            return Mathf.Max(0, count - 1);
        }
    }
}
