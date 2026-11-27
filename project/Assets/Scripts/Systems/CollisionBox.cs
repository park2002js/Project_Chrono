using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>카타나의 원형·사각 판정과 Slash의 사각 판정에서 공격별 중복 대상을 관리</summary>
public class CollisionBox
{
    public DamageInstance Damage { get; private set; } // 이번 공격에서 Hitbox에 전달할 피해 정보
    public readonly HashSet<Character> HitTargets = new HashSet<Character>(); // 같은 공격에서 이미 판정한 피해 수신자
    public RaycastHit2D[] Results = new RaycastHit2D[16]; // 매 질의에 재사용하며 포화 시 확장할 Cast 결과 버퍼
    public bool IsActive { get; private set; } // 현재 피해 판정 구간이 열려 있는지 조회

    private ContactFilter2D filter; // 트리거를 포함해 지정한 레이어의 피격 영역을 조회할 조건
    private Collider2D[] circleResults = new Collider2D[16]; // 카타나의 원·사각에 겹치는 모든 Collider를 수집할 재사용 배열
    private readonly HashSet<Projectile> hitProjectiles = new HashSet<Projectile>(); // 같은 Slash에서 여러 Collider를 가진 탄환의 중복 보상 방지
    private readonly HashSet<BossStructure> hitStructures = new HashSet<BossStructure>(); // 같은 Slash에서 구조물별 반응을 한 번만 전달할 기록
    private readonly HashSet<BossCharacter> hitBosses = new HashSet<BossCharacter>(); // 모든 부위를 보고한 뒤 판정을 마칠 Boss 목록
    private readonly List<BossCharacter> bossCompletionOrder = new List<BossCharacter>(); // 콜백에서 취소·목록 정리되어도 안전하게 순회할 Boss 순서
    private int queryVersion; // 취소 콜백에서 다른 공격이 시작되어도 이전 질의를 중단할 세대

    /// <summary>새 공격의 피해·조회 조건을 적용하고 이전 적중 기록 초기화</summary>
    public void Begin(DamageInstance damage, int layerMask)
    {
        // 새 공격의 피해와 실제 Collider 조회 조건 준비
        Damage = damage;
        queryVersion++;
        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(layerMask);
        ClearHits();
        IsActive = true;
    }

    /// <summary>카타나의 원 안에 겹친 피격 대상을 찾아 같은 수신자에 한 번만 피해 전달</summary>
    public int QueryCircle(Vector2 center, float radius, float angle, Action<Vector2, float> onHit = null)
    {
        // 포화된 배열은 늘려서 같은 원에 겹친 모든 부위를 수집
        if (!IsActive || !IsFinite(center) || !IsFinite(radius) || radius <= 0f || !IsFinite(angle)) return 0;
        int count = Physics2D.OverlapCircle(center, radius, filter, circleResults);
        while (count == circleResults.Length)
        {
            Array.Resize(ref circleResults, checked(circleResults.Length * 2));
            count = Physics2D.OverlapCircle(center, radius, filter, circleResults);
        }
        return DeliverKatanaHits(count, center, angle, onHit);
    }

    /// <summary>카타나의 회전한 사각 안에 겹친 피격 대상을 찾아 같은 수신자에 한 번만 피해 전달</summary>
    public int QueryBox(Vector2 center, Vector2 size, float angle, Action<Vector2, float> onHit = null)
    {
        // 사각의 x축을 angle 방향으로 돌리고 포화된 배열은 늘려서 겹친 모든 부위를 수집
        if (!IsActive || !IsFinite(center) || !IsFinite(size) || size.x <= 0f || size.y <= 0f || !IsFinite(angle)) return 0;
        int count = Physics2D.OverlapBox(center, size, angle, filter, circleResults);
        while (count == circleResults.Length)
        {
            Array.Resize(ref circleResults, checked(circleResults.Length * 2));
            count = Physics2D.OverlapBox(center, size, angle, filter, circleResults);
        }
        return DeliverKatanaHits(count, center, angle, onHit);
    }

    /// <summary>수집한 Collider를 수신자 기준으로 합산해 카타나 피해와 적중 표시 전달</summary>
    private int DeliverKatanaHits(int count, Vector2 center, float angle, Action<Vector2, float> onHit)
    {
        // 피해 콜백에서 판정이 닫히면 즉시 중단
        int version = queryVersion;
        int applied = 0;
        for (int i = 0; i < count && IsActive && version == queryVersion; i++)
        {
            Collider2D collider = circleResults[i];
            if (collider == null || !collider.TryGetComponent(out Hitbox hitbox) || !hitbox.isActiveAndEnabled || !hitbox.IsEnabled)
                continue;

            // 판정 중심에서 피격 영역에 가장 가까운 지점에 섬광 표시
            Vector2 point = collider.ClosestPoint(center);
            DamageInstance damage = Damage;
            if (hitbox.Structure != null)
            {
                if (hitStructures.Add(hitbox.Structure) && hitbox.ReceiveHit(damage))
                { applied++; onHit?.Invoke(point, angle); }
                continue;
            }
            Character target = hitbox.Owner;
            if (target == null || target == Damage.Source || HitTargets.Contains(target))
                continue;
            if (hitbox.ReceiveHit(damage))
            {
                if (IsActive && version == queryVersion)
                    HitTargets.Add(target);
                applied++;
                onHit?.Invoke(point, angle);
            }
        }
        return applied;
    }

    /// <summary>Slash 한 번의 탄 반응·구조물·부위 보고와 수신자별 피해 전달</summary>
    public int QuerySlash(Vector2 center, Vector2 size, float angle, PlayerCharacter player, BossCharacter boss)
    {
        // 벽 유무와 무관한 전체 Line 사각을 한 번 수집
        int count = Collect(center, size, angle);
        int version = queryVersion;
        int applied = 0;
        Vector2 lineDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        for (int i = 0; i < count && IsActive && version == queryVersion; i++)
        {
            Collider2D collider = Results[i].collider;
            if (collider == null || !collider.TryGetComponent(out Hitbox hitbox) || !hitbox.isActiveAndEnabled || !hitbox.IsEnabled) continue;

            // 탄·구조물의 독립 반응을 일반 캐릭터 피해와 구분
            if (hitbox.Projectile != null)
            {
                if (hitProjectiles.Add(hitbox.Projectile) && hitbox.Projectile.OnSlashed(player, boss, Damage, lineDirection)) applied++;
                continue;
            }
            if (hitbox.Structure != null)
            {
                BossStructure structure = hitbox.Structure;
                if (structure.Owner != null && hitBosses.Add(structure.Owner)) bossCompletionOrder.Add(structure.Owner);
                if (hitStructures.Add(structure) && structure.HandleSlash(Damage)) applied++;
                continue;
            }

            // 피해는 Boss당 한 번, 부위 보고는 PartId별로 전달
            Character target = hitbox.Owner;
            if (target == null || target == Damage.Source) continue;
            if (target is BossCharacter hitBoss)
            {
                if (hitBosses.Add(hitBoss)) bossCompletionOrder.Add(hitBoss);
                hitBoss.ReportSlashPart(Damage.AttackId, hitbox.PartId);
                if (!IsActive || version != queryVersion) break;
            }
            if (!HitTargets.Contains(target) && hitbox.ReceiveHit(Damage))
            {
                if (IsActive && version == queryVersion) HitTargets.Add(target);
                applied++;
            }
        }

        // 피해 콜백에서 중단된 판정은 성공으로 확정하지 않음
        for (int i = 0; i < bossCompletionOrder.Count && IsActive && version == queryVersion; i++)
        {
            BossCharacter hitBoss = bossCompletionOrder[i];
            if (hitBoss != null) hitBoss.CompleteSlash(Damage.AttackId);
        }
        return applied;
    }

    /// <summary>현재 공격의 추가 공간 질의 중단</summary>
    public void End()
    {
        IsActive = false;
        queryVersion++;
    }

    /// <summary>다음 공격을 위해 수신자별 적중 기록 초기화</summary>
    public void ClearHits()
    {
        HitTargets.Clear();
        hitProjectiles.Clear();
        hitStructures.Clear();
        hitBosses.Clear();
        bossCompletionOrder.Clear();
    }

    /// <summary>공간 질의에 사용할 두 좌표가 유한한지 확인</summary>
    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    /// <summary>재사용 버퍼를 필요할 때만 늘려 회전 사각의 모든 Collider 수집</summary>
    private int Collect(Vector2 center, Vector2 size, float angle)
    {
        // 비활성 판정과 유효하지 않은 기하 요청 제외
        if (!IsActive || !IsFinite(center) || !IsFinite(size) || size.x <= 0f || size.y <= 0f || !IsFinite(angle)) return 0;
        if (Results == null || Results.Length == 0) Results = new RaycastHit2D[16];

        // 배열 포화에서 재조회해 임의의 최대 적중 수로 대상을 누락하지 않도록 처리
        int count;
        while (true)
        {
            count = Physics2D.BoxCast(center, size, angle, Vector2.zero, filter, Results, 0f);
            if (count < Results.Length) return count;
            Array.Resize(ref Results, checked(Results.Length * 2));
        }
    }

    /// <summary>공간 질의에 사용할 숫자가 유한한지 확인</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
