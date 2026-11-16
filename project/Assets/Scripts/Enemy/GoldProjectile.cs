using UnityEngine;

/// <summary>일반 탄의 비행을 사용하고 Slash Line의 각도로 반사되는 금색 탄환</summary>
public class GoldProjectile : NormalProjectile
{
    /// <summary>일반 탄 준비 뒤 금색 반사·각성 반응 적용</summary>
    protected override void Awake()
    {
        base.Awake();
        SlashResponse = ProjectileSlashResponse.Reflect;
        GrantsFever = true;
    }

    /// <summary>새 발사에서도 반사와 각성 반응 유지</summary>
    public override void Initialize(BossCharacter owner, Vector2 direction, float speed, float lifetime, float damage)
    {
        base.Initialize(owner, direction, speed, lifetime, damage);
        SlashResponse = ProjectileSlashResponse.Reflect;
        GrantsFever = true;
    }
}
