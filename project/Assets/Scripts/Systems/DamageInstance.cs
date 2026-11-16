/// <summary>일반 공격·Slash·효과 피해를 구분할 전달용 식별값</summary>
public enum DamageType
{
    Normal, // 고정 무기의 일반 공격 피해
    Slash, // 확정한 Line을 사용하는 특수 공격 피해
    Effect // Hitbox 외 효과에서 발생한 피해
}

/// <summary>한 공격의 출처·피해량·종류·식별자를 수신자에게 값으로 전달</summary>
public struct DamageInstance
{
    private static int nextAttackId; // 일반 공격·Slash·탄환 사이에 겹치지 않을 런타임 공격 번호
    public readonly Character Source; // 피해를 발생시킨 캐릭터이며 환경 효과는 null 허용
    public readonly float Amount; // 수신자 체력에서 감소시킬 피해량
    public readonly DamageType Kind; // 일반 공격·Slash·효과 피해의 구분
    public readonly int AttackId; // 같은 공격의 여러 판정을 연결하는 식별자
    public readonly bool IsHitboxDamage; // Dodge로 회피할 수 있는 Hitbox 유래 피해인지 구분

    /// <summary>공격 종류와 무관하게 이번 실행을 구분할 번호 발급</summary>
    public static int NextAttackId() => unchecked(++nextAttackId);

    /// <summary>공격 시작 시점의 피해 정보를 구성</summary>
    public DamageInstance(Character source, float amount, DamageType kind, int attackId, bool isHitboxDamage = true)
    {
        Source = source;
        Amount = amount;
        Kind = kind;
        AttackId = attackId;
        IsHitboxDamage = isHitboxDamage;
    }
}
