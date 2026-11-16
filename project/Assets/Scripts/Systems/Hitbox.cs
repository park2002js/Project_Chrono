using UnityEngine;

/// <summary>피격 Collider의 대상 종류를 구분하는 데이터 식별값</summary>
public enum TargetKind
{
    Player, // Player의 피격 영역
    Boss, // Boss의 피격 영역
    NormalStructure, // 일반 공격이 통하는 구조물의 피격 영역
    SpecialStructure, // 별도 기믹을 가진 구조물의 피격 영역
    Projectile // 탄환의 판정 영역
}

/// <summary>피격 Collider를 피해 수신 캐릭터와 연결하고 피해 허용 판단을 위임</summary>
public class Hitbox : MonoBehaviour
{
    public Character Owner; // 이 피격 영역에서 받은 피해를 처리할 캐릭터
    public TargetKind TargetKind = TargetKind.Boss; // 공격자가 이 피격 영역을 공격할 수 있는지 구분할 대상 종류
    public int PartId; // 같은 수신자의 여러 부위를 구분할 식별자
    public Projectile Projectile { get; private set; } // Slash 반응을 맡길 자기 탄환 컴포넌트
    public BossStructure Structure { get; private set; } // 캐릭터 상속 없이 독립 피해를 받을 구조물 컴포넌트
    public bool IsEnabled { get; private set; } = true; // 몸체 충돌을 유지하면서 피격 판정만 열고 닫는 상태

    private Collider2D shape; // 피격 영역의 실제 활성 상태를 조회할 자기 Collider

    /// <summary>자기 Collider와 같은 프리팹의 피해 수신자를 한 번 확보</summary>
    private void Awake()
    {
        // 프리팹에서 지정한 연결을 우선 사용하고 같은 대상의 수신자 보완
        shape = GetComponent<Collider2D>();
        Projectile = GetComponentInParent<Projectile>();
        Structure = GetComponentInParent<BossStructure>();
        if (Owner == null)
            Owner = GetComponentInParent<Character>();

        // 캐릭터 피격 영역의 기본 분류 설정
        if (Projectile != null)
            TargetKind = TargetKind.Projectile;
        else if (Structure != null)
        {
            TargetKind = Structure.AcceptsNormalAttack ? TargetKind.NormalStructure : TargetKind.SpecialStructure;
            PartId = Structure.PartId;
        }
        else if (Owner is PlayerCharacter)
            TargetKind = TargetKind.Player;
        else if (Owner is BossCharacter)
            TargetKind = TargetKind.Boss;
    }

    /// <summary>공격 대상과 활성 상태를 확인하고 실제 피해 허용은 캐릭터에 위임</summary>
    public bool ReceiveHit(DamageInstance damage)
    {
        // 닫힌 피격 영역·자기 공격·같은 진영의 일반 타격 제외
        if (!isActiveAndEnabled || !IsEnabled || shape == null || !shape.enabled || (Owner == null && Structure == null) || Owner == damage.Source)
            return false;
        if (damage.Source is PlayerCharacter && (TargetKind == TargetKind.Player || TargetKind == TargetKind.Projectile))
            return false;
        if (damage.Source is BossCharacter && TargetKind != TargetKind.Player)
            return false;
        if (TargetKind == TargetKind.SpecialStructure && damage.Kind == DamageType.Normal)
            return false;

        // 생존·회피·무적 등 피해 수신자의 현재 상태로 최종 처리
        return Structure != null ? Structure.ApplyDamage(damage) : Owner.ApplyDamage(damage);
    }

    /// <summary>이동을 막는 Collider를 건드리지 않고 피격 가능 상태만 변경</summary>
    public void SetEnabled(bool value)
    {
        IsEnabled = value;
    }
}
