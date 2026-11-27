using UnityEngine;

/// <summary>Player가 맵 끝의 문 속에 들어와 있는 동안 맵을 떠나겠다는 요청을 Stage에 전달</summary>
public class StageExit : MonoBehaviour
{
    private StageManager stage; // 맵 생성 때 한 번 연결할 세션 진행 담당
    private int inside; // 문 속에 들어와 있는 이번 세션 Player의 Collider 수

    private void OnDisable() => inside = 0;

    /// <summary>문 속에 있는 동안 매 프레임 요청해 들어온 순간에 거절됐어도 조건이 풀리면 넘어감</summary>
    private void Update()
    {
        if (inside > 0 && stage != null) stage.EnterExit();
    }

    /// <summary>이번 세션 Player의 Collider가 문 속에 들어온 수를 기록</summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsSessionPlayer(other)) inside++;
    }

    /// <summary>이번 세션 Player의 Collider가 문 밖으로 나간 수를 기록</summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsSessionPlayer(other)) inside = Mathf.Max(0, inside - 1);
    }

    /// <summary>맵 생성 뒤 같은 세션의 진행 담당 연결</summary>
    public void Initialize(StageManager owner)
    {
        stage = owner;
    }

    /// <summary>닿은 Collider가 이번 세션의 Player에 속하는지 확인</summary>
    private bool IsSessionPlayer(Collider2D other)
    {
        if (stage == null) return false;
        PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
        return player != null && player == stage.Player;
    }
}
