using UnityEngine;

/// <summary>Player가 전투 영역에 들어온 사실만 Stage에 전달</summary>
public class EncounterTrigger : MonoBehaviour
{
    private StageManager stage; // 초기화에서 한 번 연결할 세션 전투 담당
    private BossCharacter boss; // 이 영역에서 전투를 시작할 상대

    /// <summary>Boss 생성 뒤 같은 세션의 전투 연결 준비</summary>
    public void Initialize(StageManager owner, BossCharacter target)
    {
        stage = owner;
        boss = target;
    }

    /// <summary>실제 Player 진입에서 전투 시작 요청만 전달</summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled || stage == null || boss == null) return;
        PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
        if (player != null && player == stage.Player) stage.BeginEncounter(boss);
    }
}
