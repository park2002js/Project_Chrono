using System;
using UnityEngine;

/// <summary>상속·조건부 추첨·네 가지 위험물과 동시 부위 Slash를 확인할 시험 보스</summary>
public class DummyBoss : BossCharacter
{
    public Projectile[] ProjectilePrefabs = Array.Empty<Projectile>(); // 일반탄·금색탄·레이저·장판 순서로 연결할 시험 프리팹
    public float Warmup = 0.35f; // 패턴을 시작한 뒤 최초 발사까지의 게임 시간
    public float ShotInterval = 0.25f; // 한 패턴 안에서 다음 위험물을 생성할 간격
    public float Recovery = 0.35f; // 마지막 발사 뒤 Idle로 돌아가기 전 대기
    public int ShotCount = 3; // 각 임시 패턴이 정상 완료까지 생성할 위험물 수
    public float PatternCooldown = 1f; // 같은 임시 패턴을 다시 시작할 수 있는 간격
    public float BulletSpeed = 3f; // 일반탄과 금색탄의 초기 비행 속도
    public float BulletLifetime = 4f; // 생성한 위험물의 예고를 포함한 총 게임 시간
    public float ProjectileDamage = 5f; // 시험 위험물 한 번의 실제 피해
    public float AreaRange = 10f; // 장판 패턴의 실행을 허용할 Player와의 최대 거리
    public int Phase => HP <= maxHP * 0.5f ? 2 : 1; // 체력 절반에서 조건과 우선순위를 바꿀 시험 페이즈

    /// <summary>구체 보스의 Awake에서 자기 프리팹과 네 가지 임시 패턴을 한 번 연결</summary>
    protected override void InitializePatterns()
    {
        // 시험 보스는 네 위험물 참조가 모두 연결된 프리팹으로 생성
        if (ProjectilePrefabs == null || ProjectilePrefabs.Length != 4)
            throw new InvalidOperationException("DummyBoss requires normal, gold, laser and area prefabs.");
        for (int i = 0; i < ProjectilePrefabs.Length; i++)
            if (ProjectilePrefabs[i] == null) throw new InvalidOperationException("DummyBoss has an unassigned projectile prefab.");

        // 실제 콘텐츠와 분리한 예시 조건·가중치·우선순위 객체를 재사용
        var normal = new DummyProjectilePattern(this, 0, "dummy_normal");
        var gold = new DummyProjectilePattern(this, 1, "dummy_gold") { Weight = 2f };
        var laser = new DummyProjectilePattern(this, 2, "dummy_laser");
        var area = new DummyProjectilePattern(this, 3, "dummy_area", gold);
        PatternSelector.SetPatterns(normal, gold, laser, area);
    }

    /// <summary>시험 부위1·2를 같은 Slash로 통과하면 보스 자신이 그로기 성공 판단</summary>
    public override void HandleSlashHit(int attackId)
    {
        // Player가 보스별 조건을 알지 않도록 집계 완료 뒤 자기 부위만 확인
        bool first = false;
        bool second = false;
        foreach (int part in SlashParts)
        {
            if (part == 1) first = true;
            if (part == 2) second = true;
        }
        if (first && second) EnterGroggy();
    }

    /// <summary>공통 패턴을 상속해 페이즈·거리·다른 패턴 횟수와 연속 발사를 보여주는 예시</summary>
    private class DummyProjectilePattern : BossPattern
    {
        private readonly DummyBoss boss; // 등록 때 연결한 자기 보스와 Inspector 시험 설정
        private readonly int index; // 일반탄·금색탄·레이저·장판의 프리팹 및 예시 조건 구분
        private readonly BossPattern prerequisite; // 장판 조건에서 조회할 금색탄 패턴의 실제 시작 횟수
        private int firedCount; // 현재 패턴에서 이미 생성한 위험물 수
        private float nextShotAt; // 현재 패턴의 다음 발사 누적 시간

        /// <summary>보스와 위험물 종류를 등록 시 한 번 연결</summary>
        public DummyProjectilePattern(DummyBoss owner, int projectileIndex, string id, BossPattern requiredPattern = null) : base(id)
        {
            boss = owner;
            index = projectileIndex;
            prerequisite = requiredPattern;
            Cooldown = owner.PatternCooldown;
        }

        /// <summary>쿨타임에 페이즈·거리·다른 패턴 실행 횟수의 예시 조건 추가</summary>
        public override bool CanRun(BossCharacter owner)
        {
            // 시험용 설정이 유효하고 같은 보스의 현재 전투 상대가 있는지 검사
            if (!base.CanRun(owner) || owner != boss || boss.Target == null || boss.ShotCount <= 0
                || !Nonnegative(boss.Warmup) || !Nonnegative(boss.ShotInterval) || !Nonnegative(boss.Recovery)
                || !Nonnegative(boss.BulletSpeed) || !Nonnegative(boss.BulletLifetime) || boss.BulletLifetime <= 0f
                || !Nonnegative(boss.ProjectileDamage) || !Nonnegative(boss.AreaRange)) return false;

            // 레이저는2페이즈부터, 장판은 금색탄 사용 뒤 가까운 대상에게만 허용
            if (index == 2 && boss.Phase < 2) return false;
            if (index == 3 && (prerequisite.UseCount == 0
                || ((Vector2)boss.Target.transform.position - (Vector2)boss.transform.position).sqrMagnitude > boss.AreaRange * boss.AreaRange))
                return false;
            return true;
        }

        /// <summary>2페이즈에서 아직 사용하지 않은 금색탄만 일반 가중 추첨보다 우선</summary>
        public override int GetPriority(BossCharacter owner)
        {
            return index == 1 && boss.Phase == 2 && UseCount == 0 ? 10 : base.GetPriority(owner);
        }

        /// <summary>이전 실행의 발사 수를 비우고 첫 예고 시간 준비</summary>
        protected override void OnBegin()
        {
            firedCount = 0;
            nextShotAt = boss.Warmup;
        }

        /// <summary>예고 뒤 정해진 수만 발사하고 회복 시간이 끝나면 정상 완료</summary>
        protected override void OnTick(float elapsed)
        {
            // 낮은 프레임에서도 발사 횟수는 유지하되 취소된 실행에서는 후속 발사 차단
            while (IsRunning && firedCount < boss.ShotCount && Elapsed >= nextShotAt)
            {
                Fire();
                firedCount++;
                nextShotAt += boss.ShotInterval;
            }

            // 마지막 발사 뒤 회복 대기가 끝난 시점에만 Idle 전환
            float end = boss.Warmup + (boss.ShotCount - 1) * boss.ShotInterval + boss.Recovery;
            if (firedCount >= boss.ShotCount && Elapsed >= end) Complete();
        }

        /// <summary>현재 조준 위치로 위험물을 생성하고 자기 수명은 패턴과 독립시켜 유지</summary>
        private void Fire()
        {
            // 같은 Player 참조에서 현재 위치만 읽고 생성 시 조준 방향 확정
            if (boss.Target == null || boss.Target.IsDead) return;
            Vector2 offset = (Vector2)boss.Target.transform.position - (Vector2)boss.transform.position;
            Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.down;
            Projectile prefab = boss.ProjectilePrefabs[index];
            Vector3 position = boss.transform.position + (Vector3)(direction * 0.8f);
            if (prefab is LaserProjectile laser) position = boss.transform.position + (Vector3)(direction * (laser.Length * 0.5f));
            else if (prefab is AreaProjectile) position = boss.Target.transform.position;

            // 보스의 자식 대신 세션 부모에 배치해 그로기·패턴 취소와 필드 수명 분리
            Projectile projectile = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity, boss.transform.parent);
            projectile.Initialize(boss, direction, boss.BulletSpeed, boss.BulletLifetime, boss.ProjectileDamage);
        }

        /// <summary>발사 시간·속도·거리의 유효한 설정 범위 확인</summary>
        private static bool Nonnegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
