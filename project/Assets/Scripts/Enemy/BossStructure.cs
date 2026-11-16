using UnityEngine;

/// <summary>Boss 부위의 독립 체력·일반 공격 허용과 Slash 부위 보고</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossStructure : MonoBehaviour
{
    public BossCharacter Owner; // Slash 부위 결과를 모을 Boss
    public int PartId; // 같은 Boss 안에서 구분할 부위 식별자
    public bool AcceptsNormalAttack = true; // 일반 공격으로 이 구조물을 손상시킬 수 있는지 설정
    public float HP = 20f; // 몸체와 별개로 관리할 구조물 체력
    public bool IsActive { get; private set; } = true; // 제거된 부위의 중복 피해·보고 차단

    private Collider2D shape; // 제거 즉시 판정을 닫을 자기 Collider
    private int lastSlashAttackId; // 같은 Slash가 여러 Collider에서 들어올 때 중복 처리 방지
    private bool hasSlashAttack; // 아직 Slash 적중이 없는 초기 상태와 공격 번호 구분

    /// <summary>자기 Collider와 프리팹의 Boss 연결을 한 번 준비</summary>
    private void Awake()
    {
        shape = GetComponent<Collider2D>();
        if (Owner == null)
            Owner = GetComponentInParent<BossCharacter>();
    }

    /// <summary>허용된 일반 공격이나 Slash 피해를 독립 체력에 적용</summary>
    public bool ApplyDamage(DamageInstance damage)
    {
        if (!isActiveAndEnabled || !IsActive || shape == null || !shape.enabled
            || (damage.Kind == DamageType.Normal && !AcceptsNormalAttack)
            || float.IsNaN(damage.Amount) || float.IsInfinity(damage.Amount) || damage.Amount <= 0f
            || float.IsNaN(HP) || float.IsInfinity(HP) || HP <= 0f)
            return false;
        HP = Mathf.Max(0f, HP - damage.Amount);
        if (HP <= 0f)
            Remove();
        return true;
    }

    /// <summary>이번 Slash의 부위를 한 번 보고하고 자기 체력에 피해 적용</summary>
    public bool HandleSlash(DamageInstance damage)
    {
        if (!isActiveAndEnabled || !IsActive || damage.Kind != DamageType.Slash || (hasSlashAttack && lastSlashAttackId == damage.AttackId))
            return false;

        // 피해 콜백이나 제거 전에 이번 부위 적중을 먼저 기록
        hasSlashAttack = true;
        lastSlashAttackId = damage.AttackId;
        if (Owner != null)
            Owner.ReportSlashPart(damage.AttackId, PartId);
        return ApplyDamage(damage);
    }

    /// <summary>다음 전투에서 같은 구조물의 체력과 부위 상태 복구</summary>
    public void Reset(float health)
    {
        if (float.IsNaN(health) || float.IsInfinity(health) || health <= 0f)
            throw new System.ArgumentOutOfRangeException(nameof(health));
        HP = health;
        IsActive = true;
        hasSlashAttack = false;
        if (shape != null) shape.enabled = true;
    }

    /// <summary>프리팹은 유지하고 구조물의 추가 타격 판정 종료</summary>
    public void Remove()
    {
        IsActive = false;
        if (shape != null) shape.enabled = false;
    }
}
