using UnityEngine;

/// <summary>기본 비행을 따르며 Slash에 제거되는 일반 탄환</summary>
public class NormalProjectile : Projectile
{
    /// <summary>공통 수명을 준비하고 일반 탄의 Slash 반응 설정</summary>
    protected override void Awake()
    {
        base.Awake();
        SlashResponse = ProjectileSlashResponse.Destroy;
    }

    /// <summary>새 발사에서 일반 탄 제거 반응과 공통 비행 정보 준비</summary>
    public override void Initialize(BossCharacter owner, Vector2 direction, float speed, float lifetime, float damage)
    {
        base.Initialize(owner, direction, speed, lifetime, damage);
        SlashResponse = ProjectileSlashResponse.Destroy;
    }
}
