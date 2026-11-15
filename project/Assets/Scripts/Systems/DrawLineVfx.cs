using UnityEngine;

/// <summary>임시 LineRenderer의 월드 폭을 단위 프리팹의 Y 배율에 맞춰 표시</summary>
[RequireComponent(typeof(LineRenderer))]
public class DrawLineVfx : MonoBehaviour
{
    private LineRenderer line; // Awake에서 한 번 확보할 임시 선 렌더러

    /// <summary>데이터·입력과 분리된 자기 표현 컴포넌트 준비</summary>
    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    private void OnEnable()
    {
        ApplyWidth();
    }

    /// <summary>게임 정지 중에도 바뀐 경로 폭을 실제 렌더링에 반영</summary>
    private void LateUpdate()
    {
        ApplyWidth();
    }

    /// <summary>Transform 배율을 자동 적용하지 않는 LineRenderer의 폭 보정</summary>
    private void ApplyWidth()
    {
        if (line != null)
            line.widthMultiplier = Mathf.Abs(transform.lossyScale.y);
    }
}
