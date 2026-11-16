using System;
using UnityEngine;

/// <summary>확정한 시작점·방향·고정 길이와 폭을 보관하는 경로 값</summary>
public struct Line
{
    public Vector2 StartPoint; // 확정한 경로가 시작하는 월드 좌표
    public Vector2 Direction; // 두 입력 점으로 결정한 단위 방향
    public float Length; // 두 입력 점 사이 거리와 별개인 고정 경로 길이
    public float Width; // 표시 전체 폭과 Slash 판정 폭을 계산할 기준 경로 폭
    public Vector2 EndPoint => StartPoint + Direction * Length; // 고정 길이를 적용한 실제 경로 끝점

    /// <summary>유한하고 양수인 기하 값으로 확정 경로 구성</summary>
    public Line(Vector2 startPoint, Vector2 direction, float length, float width)
    {
        // 입력 좌표와 크기가 유효한 경로를 만드는지 확인
        if (!IsFinite(startPoint) || !IsFinite(direction) || !IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude <= 0.000001f
            || !IsFinite(length) || length <= 0f || !IsFinite(width) || width <= 0f)
            throw new ArgumentException("Line requires finite points, a nonzero direction and positive length/width.");

        // 입력 간 거리 대신 지정한 길이를 유지할 기하 값 보관
        StartPoint = startPoint;
        Direction = direction.normalized;
        Length = length;
        Width = width;
    }

    /// <summary>확정 방향·고정 길이로 계산한 월드 끝점 반환</summary>
    public Vector2 GetEndPoint()
    {
        return EndPoint;
    }

    /// <summary>회전한 경로 사각형 전체를 포함하는 월드 축 기준 범위 계산</summary>
    public Bounds GetBounds()
    {
        Vector2 center = StartPoint + Direction * (Length * 0.5f);
        Vector2 size = new Vector2(Mathf.Abs(Direction.x) * Length + Mathf.Abs(Direction.y) * Width,
            Mathf.Abs(Direction.y) * Length + Mathf.Abs(Direction.x) * Width);
        return new Bounds(center, size);
    }

    /// <summary>좌표의 두 성분이 유한한지 확인</summary>
    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    /// <summary>기하 수치가 NaN이나 무한이 아닌지 확인</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
