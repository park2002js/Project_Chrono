using UnityEngine;

/// <summary>문 모션이 완전히 열린 뒤 통과를 허용하고 닫기 요청은 즉시 차단</summary>
[RequireComponent(typeof(Animator), typeof(BoxCollider2D))]
public class MapDoor : MonoBehaviour
{
    public bool InitiallyOpen; // 보스 조건과 무관하게 연결 복도를 열어 둘 초기 상태
    public string OpeningState = "opening"; // 출구의 점멸 예고를 포함한 개방 시작 상태
    public bool IsOpen { get; private set; } // 최종 열린 모션에 도착해 실제 통과 가능한 상태
    public bool WantsOpen { get; private set; } // 최신 사건이 요청한 문 목적 상태
    public Collider2D Blocker { get; private set; } // 개방 완료까지 이동을 막는 자기 충돌체
    private Animator animator; // 문 수명 동안 재사용할 준비된 개폐 Animator

    /// <summary>자기 참조를 확보하고 저장 이력 적용 전 기본 상태 준비</summary>
    private void Awake()
    {
        animator = GetComponent<Animator>();
        Blocker = GetComponent<BoxCollider2D>();
        SetOpen(InitiallyOpen, true);
    }

    /// <summary>마지막 open 상태에 실제 도착한 프레임에서만 이동 차단 해제</summary>
    private void LateUpdate()
    {
        if (!WantsOpen || IsOpen || animator.IsInTransition(0)) return;
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName("open")) return;
        IsOpen = true;
        Blocker.enabled = false;
    }

    /// <summary>수정·보스 사건은 모션으로, 저장 복원은 완결 상태로 문 전환</summary>
    public void SetOpen(bool open, bool immediate = false)
    {
        // 같은 목적 상태의 반복 요청으로 모션을 다시 시작하지 않도록 제한
        if (!immediate && WantsOpen == open) return;
        WantsOpen = open;
        IsOpen = immediate && open;
        Blocker.enabled = !IsOpen;
        animator.ResetTrigger("Open");
        animator.ResetTrigger("Close");

        // 중간 모션의 반대 요청도 즉시 반영하고 종료 상태만 통과 허용
        animator.Play(immediate ? (open ? "open" : "closed") : (open ? OpeningState : "closing"), 0, 0f);
        if (immediate) animator.Update(0f);
    }
}
