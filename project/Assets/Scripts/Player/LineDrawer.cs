using UnityEngine;

/// <summary>Draw의 임시 점·커서 미리보기와 고정 길이 경로 확정만 처리하는 도구</summary>
public class LineDrawer
{
    public Vector2 StartPoint { get; private set; } // 첫 클릭으로 선택한 월드 시작점
    public Vector2 EndPoint { get; private set; } // 고정 길이와 별개로 방향을 선택한 두 번째 클릭 좌표
    public int PointCount { get; private set; } // 현재 편집에서 선택한 점 개수
    public bool IsDrawing { get; private set; } // 임시 점을 편집하고 있는지 조회
    public Line? PreviewLine { get; private set; } // 한 점과 커서 또는 두 점으로 계산한 임시 경로
    public bool CanConfirm => IsDrawing && PointCount == 2 && PreviewLine.HasValue; // 서로 다른 두 점으로 확정할 경로가 준비됐는지 조회

    private float lineLength; // 이번 편집에서 끝점 계산에 사용할 고정 길이
    private float lineWidth; // 이번 편집에서 표시와 판정에 전달할 경로 폭

    /// <summary>기존 확정 경로를 건드리지 않고 새 임시 점 편집 시작</summary>
    public bool Begin(float length, float width)
    {
        // 경로를 구성할 수 없는 설정은 편집 시작 전에 거부
        if (!IsFinite(length) || length <= 0f || !IsFinite(width) || width <= 0f)
            return false;

        // 새 편집의 크기를 보관하고 임시 선택만 초기화
        lineLength = length;
        lineWidth = width;
        IsDrawing = true;
        ResetPoints();
        return true;
    }

    /// <summary>최대 두 점만 선택하고 같은 점으로는 방향을 확정하지 않도록 처리</summary>
    public bool PlacePoint(Vector2 position)
    {
        // 편집 밖 입력과 이미 두 점을 고른 뒤의 추가 입력 제외
        if (!IsDrawing || PointCount >= 2 || !IsFinite(position))
            return false;

        // 첫 클릭의 시작점을 보관하고 커서 미리보기 준비
        if (PointCount == 0)
        {
            StartPoint = position;
            EndPoint = position;
            PointCount = 1;
            PreviewLine = null;
            return true;
        }

        // 두 번째 클릭은 방향만 확정하고 실제 끝점에는 고정 길이 적용
        Vector2 direction = position - StartPoint;
        if (!IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude <= 0.000001f)
            return false;
        EndPoint = position;
        PointCount = 2;
        PreviewLine = new Line(StartPoint, direction, lineLength, lineWidth);
        return true;
    }

    /// <summary>첫 점만 선택한 동안 커서를 따라 고정 길이 미리보기 갱신</summary>
    public void UpdatePreview(Vector2 cursorPosition)
    {
        // 두 점을 고른 뒤에는 커서를 움직여도 같은 경로 유지
        if (!IsDrawing || PointCount != 1 || !IsFinite(cursorPosition))
            return;

        // 시작점과 커서가 겹치면 임시 방향을 만들지 않음
        Vector2 direction = cursorPosition - StartPoint;
        if (!IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude <= 0.000001f)
        {
            PreviewLine = null;
            return;
        }
        PreviewLine = new Line(StartPoint, direction, lineLength, lineWidth);
    }

    /// <summary>Draw 모드를 유지하면서 임시 점과 미리보기만 제거</summary>
    public void ResetPoints()
    {
        StartPoint = Vector2.zero;
        EndPoint = Vector2.zero;
        PointCount = 0;
        PreviewLine = null;
    }

    /// <summary>유효한 두 점의 경로 값을 반환하고 임시 편집 종료</summary>
    public bool Confirm(out Line line)
    {
        // 점이 부족한 확정 요청은 현재 편집을 그대로 유지
        line = default;
        if (!CanConfirm)
            return false;

        // 확정 값의 저장·교체는 호출자에게 맡기고 임시 데이터만 정리
        line = PreviewLine.Value;
        Cancel();
        return true;
    }

    /// <summary>소유자가 보관한 확정 경로와 별개로 임시 편집만 취소</summary>
    public void Cancel()
    {
        ResetPoints();
        IsDrawing = false;
    }

    /// <summary>입력 좌표의 두 성분이 유한한지 확인</summary>
    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    /// <summary>편집 설정이 NaN이나 무한이 아닌지 확인</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
