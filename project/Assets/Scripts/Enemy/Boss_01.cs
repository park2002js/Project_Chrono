using System;
using UnityEngine;

/// <summary>복부 황금탄·손 내려찍기·추적 눈 레이저·다섯 총구 부채꼴을 실행할 경비 보스</summary>
[DefaultExecutionOrder(1)] // Player 이동 이후의 위치를 기록하고 같은 프레임의 보스 조준에 사용
public class Boss_01 : BossCharacter
{
    public Transform[] Muzzles = Array.Empty<Transform>(); // 복부 왼쪽부터 오른쪽 순서로 연결할 다섯 총구
    public GoldProjectile GoldPrefab; // 발사와 반사·각성 처리를 함께 사용할 황금 탄환 프리팹
    public NormalProjectile NormalPrefab; // 중앙 세로줄과 다섯 총구 부채꼴에 사용할 일반 탄환 프리팹
    public GuardHandAttack HandAttack; // 같은 보스에서 두 손과 예고를 재사용할 내려찍기 실행체
    public GuardEyeLaser EyeLaser; // 양눈 충전과 과거 위치를 추적하는 두 레이저를 재사용할 실행체
    public int MuzzleIndex = 2; // 첫 패턴에서 사용할 총구의 배열 번호
    public float ActivationRange = 4.5f; // 총구 발사를 화면에서 확인할 수 있도록 제한할 접근 거리
    public float Warmup = 0.8f; // 패턴 시작 뒤 실제 발사까지 기다릴 게임 시간
    public float Recovery = 1f; // 한 발 발사한 뒤 Idle로 돌아가기까지의 게임 시간
    public float PatternCooldown = 2f; // 각 패턴의 시작부터 다음 사용까지 필요한 게임 시간
    public float BulletSpeed = 6f; // 황금 탄환이 직선으로 이동할 초당 월드 거리
    public float BulletLifetime = 6f; // 발사한 황금 탄환이 독립적으로 유지될 게임 시간
    public float ProjectileDamage = 10f; // 황금 탄환이 실제 적중에서 전달할 기본 피해
    public int CentralRowCount = 6; // 중앙 세로줄에서 좌우 각각 발사할 탄환 수
    public float CentralRowSpacing = 1f; // 세로줄의 이웃한 발사 원점 사이 월드 거리
    public float CentralBulletSpeed = 4.5f; // 중앙에서 좌우로 퍼지는 탄환의 초당 월드 거리
    public float CentralBulletLifetime; // 0이면 중앙 탄환을 시간 만료 없이 벽에 닿을 때까지 유지
    public float CentralBulletDamage = 10f; // 중앙 좌우 탄환의 실제 적중 피해
    public float FanWarmup = 0.4f; // 다섯 총구 부채꼴을 시작하기 전 준비 시간
    public float FanBurstInterval = 0.3f; // 실제 부채꼴 발사 사이에 확보할 게임 시간
    public float FanRecovery = 0.5f; // 열 번째 부채꼴 발사 뒤 회복 시간
    public int FanBulletCount = 5; // 한 총구의 부채꼴에 배치할 일반 탄환 수
    public float FanSpread = 60f; // 아래 방향을 중심으로 부채꼴 전체가 벌어지는 각도
    public float FanBulletSpeed = 6f; // 부채꼴 탄환의 초당 월드 이동 거리
    public float FanBulletLifetime; // 0이면 부채꼴 탄환을 시간 만료 없이 벽까지 유지
    public float FanBulletDamage = 10f; // 부채꼴 탄환이 실제 적중에서 전달할 피해

    private BossSpawner bossSpawner; // 비활성 조우 영역에서도 중앙 좌표를 얻을 자기 생성 담당

    /// <summary>자기 프리팹의 발사·손·눈 참조를 확인하고 경비의 다섯 패턴 등록</summary>
    protected override void InitializePatterns()
    {
        // Player 생성 전에는 자기 프리팹에 직렬화된 참조만 확인
        bossSpawner = GetComponent<BossSpawner>();
        if (Muzzles == null || Muzzles.Length != 5 || GoldPrefab == null || NormalPrefab == null || HandAttack == null || EyeLaser == null || bossSpawner == null)
            throw new InvalidOperationException("Boss_01 requires five muzzles, both bullet prefabs, its hand attack, eye laser and spawner.");
        for (int i = 0; i < Muzzles.Length; i++)
            if (Muzzles[i] == null) throw new InvalidOperationException("Boss_01 has an unassigned muzzle transform.");

        // 다섯 패턴을 등록하고 직전 제외와 재도전 이력은 공통 선택기에 위임
        PatternSelector.SetPatterns(new GoldShotPattern(this), new HandSlamPattern(this), new CentralSlamPattern(this), new EyeLaserPattern(this), new FanShotPattern(this));
    }

    /// <summary>눈 충전 뒤 과거 Player 위치로 두 레이저를 추적 발사할 네 번째 패턴</summary>
    private class EyeLaserPattern : BossPattern
    {
        private readonly Boss_01 boss; // 같은 보스의 대상과 재사용할 눈 레이저 실행체

        /// <summary>네 번째 식별자와 같은 보스의 공통 쿨타임 연결</summary>
        public EyeLaserPattern(Boss_01 owner) : base("F-B07-04")
        {
            boss = owner;
            Cooldown = owner.PatternCooldown;
        }

        /// <summary>살아 있는 전투 대상과 대기 중인 눈 레이저 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && boss.EyeLaser != null && !boss.EyeLaser.IsRunning;
        }

        /// <summary>이번 전투 대상을 주입하고 양눈 충전과 위치 기록 시작</summary>
        protected override void OnBegin()
        {
            if (!boss.EyeLaser.Begin(boss.Target)) Complete();
        }

        /// <summary>충전·추적 발사·회복을 같은 패턴 게임 시간으로 진행</summary>
        protected override void OnTick(float elapsed)
        {
            boss.EyeLaser.Tick(elapsed);
            if (!boss.EyeLaser.IsRunning) Complete();
        }

        /// <summary>그로기·사망·재도전에서 눈 충전과 진행 중인 빔 피해 즉시 정리</summary>
        protected override void OnCancel()
        {
            boss.EyeLaser.Cancel();
        }
    }

    /// <summary>다섯 총구를 두 바퀴 순회하며 짧은 간격으로 부채꼴을 발사할 다섯 번째 패턴</summary>
    private class FanShotPattern : BossPattern
    {
        private const int BurstCount = 10; // 다섯 총구를 두 바퀴 돌며 실행할 부채꼴 횟수
        private readonly Boss_01 boss; // 등록 때 연결한 같은 보스와 다섯 총구
        private int firedBursts; // 이번 실행에서 이미 발사한 부채꼴 횟수
        private float nextBurstAt; // 이번 실행의 다음 부채꼴을 시작할 누적 게임 시간
        private float interval; // 실행 시작 때 고정한 실제 발사 사이 간격
        private float completeAt; // 열 번째 실제 발사 뒤 회복을 마칠 누적 시간

        /// <summary>다섯 번째 식별자와 같은 보스의 공통 쿨타임 연결</summary>
        public FanShotPattern(Boss_01 owner) : base("F-B07-05")
        {
            boss = owner;
            Cooldown = owner.PatternCooldown;
        }

        /// <summary>총구가 보이는 접근 거리와 유효한 부채꼴 설정 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && boss.NormalPrefab != null && Positive(boss.ActivationRange)
                && ((Vector2)boss.Target.transform.position - (Vector2)boss.Muzzles[2].position).sqrMagnitude
                    <= boss.ActivationRange * boss.ActivationRange
                && Nonnegative(boss.FanWarmup) && Positive(boss.FanBurstInterval) && Nonnegative(boss.FanRecovery)
                && boss.FanBulletCount > 0 && Nonnegative(boss.FanSpread) && boss.FanSpread < 180f
                && Positive(boss.FanBulletSpeed) && Nonnegative(boss.FanBulletLifetime) && Nonnegative(boss.FanBulletDamage);
        }

        /// <summary>첫 총구부터 새 두 바퀴 발사와 실제 발사 간격 준비</summary>
        protected override void OnBegin()
        {
            firedBursts = 0;
            nextBurstAt = boss.FanWarmup;
            interval = boss.FanBurstInterval;
            completeAt = float.PositiveInfinity;
        }

        /// <summary>지연 프레임에도 발사를 몰아내지 않고 순서대로 열 번 실행</summary>
        protected override void OnTick(float elapsed)
        {
            // 누적 소수 오차로 발사가 한 프레임 늦어지지 않도록 작은 허용값 적용
            if (firedBursts < BurstCount && Elapsed + 0.00001f >= nextBurstAt)
            {
                FireFan(firedBursts % 5);
                firedBursts++;
                nextBurstAt = Elapsed + interval;
                if (firedBursts == BurstCount) completeAt = Elapsed + boss.FanRecovery;
            }

            // 이미 생성한 탄환의 비행과 마지막 발사 뒤 회복 시간은 분리
            if (firedBursts == BurstCount && Elapsed >= completeAt) Complete();
        }

        /// <summary>아직 발사하지 않은 총구만 닫고 기존 부채꼴 탄환 유지</summary>
        protected override void OnCancel()
        {
            firedBursts = BurstCount;
        }

        /// <summary>지정 총구에서 아래 방향을 중심으로 일반 탄환 부채꼴 생성</summary>
        private void FireFan(int muzzle)
        {
            // 취소·사망 경계에서는 새로운 묶음을 시작하지 않음
            if (!IsRunning || !boss.IsInCombat || boss.IsDead || boss.IsGroggy || boss.Target == null || boss.Target.IsDead) return;
            Vector3 position = boss.Muzzles[muzzle].position;
            for (int i = 0; i < boss.FanBulletCount; i++)
            {
                float angle = boss.FanBulletCount == 1 ? 0f : -boss.FanSpread * 0.5f + boss.FanSpread * i / (boss.FanBulletCount - 1);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.down;
                NormalProjectile projectile = UnityEngine.Object.Instantiate(boss.NormalPrefab, position, Quaternion.identity, boss.transform.parent);
                projectile.Initialize(boss, direction, boss.FanBulletSpeed, boss.FanBulletLifetime, boss.FanBulletDamage);
            }
        }

        /// <summary>간격과 이동에 필요한 양수 유한값 확인</summary>
        private static bool Positive(float value) => Nonnegative(value) && value > 0f;

        /// <summary>시간·각도·피해에 필요한 0 이상 유한값 확인</summary>
        private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>두 손의 중앙 착지에서 세로줄 탄환을 좌우로 한 번 발사할 세 번째 패턴</summary>
    private class CentralSlamPattern : BossPattern
    {
        private readonly Boss_01 boss; // 같은 세션의 보스와 재사용할 두 손 실행체
        private readonly Action<Vector2> fireRows; // 매 실행에 다시 할당하지 않을 착지 순간 발사 콜백

        /// <summary>세 번째 식별자·공통 쿨타임·착지 콜백을 한 번 준비</summary>
        public CentralSlamPattern(Boss_01 owner) : base("F-B07-03")
        {
            boss = owner;
            Cooldown = owner.PatternCooldown;
            fireRows = FireRows;
        }

        /// <summary>준비된 조우 영역과 대기 중인 두 손·유효한 탄환 설정 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && boss.HandAttack != null && !boss.HandAttack.IsRunning && boss.bossSpawner.EncounterTrigger != null
                && boss.NormalPrefab != null && boss.CentralRowCount > 0
                && Positive(boss.CentralRowSpacing) && Positive(boss.CentralBulletSpeed)
                && Nonnegative(boss.CentralBulletLifetime) && Nonnegative(boss.CentralBulletDamage);
        }

        /// <summary>비활성 조우 영역의 저장된 중심에서 두 손 충전 시작</summary>
        protected override void OnBegin()
        {
            // 전투 중 꺼진 Collider의 빈 bounds 대신 변환과 로컬 중심을 사용
            Collider2D area = boss.bossSpawner.EncounterTrigger;
            Vector2 center = area.transform.TransformPoint(area.offset);
            if (!boss.HandAttack.BeginCentral(center, fireRows)) Complete();
        }

        /// <summary>양손과 큰 예고를 진행하고 손 복귀 뒤 정상 완료</summary>
        protected override void OnTick(float elapsed)
        {
            boss.HandAttack.Tick(elapsed);
            if (!boss.HandAttack.IsRunning) Complete();
        }

        /// <summary>아직 오지 않은 착지·발사를 닫고 손과 예고 복원</summary>
        protected override void OnCancel()
        {
            boss.HandAttack.Cancel();
        }

        /// <summary>같은 중앙 세로줄의 각 원점에서 좌우 일반 탄환 생성</summary>
        private void FireRows(Vector2 center)
        {
            // 착지 전 취소·사망이 발생하면 새 탄환을 남기지 않도록 차단
            if (!IsRunning || boss.IsDead || !boss.IsInCombat || boss.IsGroggy || boss.Target == null || boss.Target.IsDead) return;
            float halfRows = (boss.CentralRowCount - 1) * 0.5f;
            for (int i = 0; i < boss.CentralRowCount; i++)
            {
                Vector3 position = new Vector3(center.x, center.y + (i - halfRows) * boss.CentralRowSpacing, boss.transform.position.z);
                for (int side = 0; side < 2; side++)
                {
                    NormalProjectile projectile = UnityEngine.Object.Instantiate(boss.NormalPrefab, position, Quaternion.identity, boss.transform.parent);
                    projectile.Initialize(boss, side == 0 ? Vector2.left : Vector2.right, boss.CentralBulletSpeed,
                        boss.CentralBulletLifetime, boss.CentralBulletDamage);
                }
            }
        }

        /// <summary>기하·비행에 필요한 양수 유한값 확인</summary>
        private static bool Positive(float value) => Nonnegative(value) && value > 0f;

        /// <summary>실제 피해에 필요한 0 이상 유한값 확인</summary>
        private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>공통 선택·취소 경계에서 같은 손 실행체를 진행할 두 번째 패턴</summary>
    private class HandSlamPattern : BossPattern
    {
        private readonly Boss_01 boss; // 초기화 때 연결한 보스와 재사용할 손 실행체

        /// <summary>두 번째 패턴의 식별자와 보스의 쿨타임 연결</summary>
        public HandSlamPattern(Boss_01 owner) : base("F-B07-02")
        {
            boss = owner;
            Cooldown = owner.PatternCooldown;
        }

        /// <summary>살아 있는 전투 대상과 대기 중인 손 실행체를 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && boss.HandAttack != null && !boss.HandAttack.IsRunning;
        }

        /// <summary>좌우 중 한 손을 골라 현재 Player를 추적하기 시작</summary>
        protected override void OnBegin()
        {
            if (!boss.HandAttack.Begin(boss.Target)) Complete();
        }

        /// <summary>게임 시간으로 손을 진행하고 제자리 복귀 뒤 정상 완료</summary>
        protected override void OnTick(float elapsed)
        {
            boss.HandAttack.Tick(elapsed);
            if (!boss.HandAttack.IsRunning) Complete();
        }

        /// <summary>그로기·사망·재도전에서 미래 착지 피해와 경고 정리</summary>
        protected override void OnCancel()
        {
            boss.HandAttack.Cancel();
        }
    }

    /// <summary>준비·단발 발사·회복을 진행하고 취소 시 미래 발사만 중단</summary>
    private class GoldShotPattern : BossPattern
    {
        private readonly Boss_01 boss; // 등록 때 한 번 연결한 자기 보스와 발사 설정
        private bool fired; // 같은 실행의 중복 발사와 취소 뒤 추가 발사 차단
        private float fireAt; // 현재 실행 시작부터 실제 발사까지의 누적 시간
        private float completeAt; // 실제 발사 뒤 회복 대기를 마칠 누적 시간

        /// <summary>첫 패턴의 식별자와 자기 보스 설정 연결</summary>
        public GoldShotPattern(Boss_01 owner) : base("F-B07-01")
        {
            // 등록된 같은 보스와 시작 기준 쿨타임을 연결
            boss = owner;
            Cooldown = owner.PatternCooldown;
        }

        /// <summary>현재 대상과 총구 번호·발사 수치가 유효할 때 공통 선택 허용</summary>
        public override bool CanRun(BossCharacter owner)
        {
            // 공통 쿨타임에 현재 상대와 발사 설정 조건을 함께 적용
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && boss.MuzzleIndex >= 0 && boss.MuzzleIndex < boss.Muzzles.Length
                && boss.Muzzles[boss.MuzzleIndex] != null && boss.GoldPrefab != null
                && Nonnegative(boss.ActivationRange) && boss.ActivationRange > 0f
                && ((Vector2)boss.Target.transform.position - (Vector2)boss.Muzzles[boss.MuzzleIndex].position).sqrMagnitude
                    <= boss.ActivationRange * boss.ActivationRange
                && Nonnegative(boss.Warmup) && Nonnegative(boss.Recovery)
                && Nonnegative(boss.BulletSpeed) && Nonnegative(boss.BulletLifetime) && boss.BulletLifetime > 0f
                && Nonnegative(boss.ProjectileDamage);
        }

        /// <summary>이전 발사 상태를 비우고 준비 시간과 발사 전 완료 대기 상태 설정</summary>
        protected override void OnBegin()
        {
            // 실제 발사 전에는 회복 종료가 먼저 실행되지 않도록 준비
            fired = false;
            fireAt = boss.Warmup;
            completeAt = float.PositiveInfinity;
        }

        /// <summary>준비 시간이 지나면 한 발만 발사하고 회복 뒤 정상 완료</summary>
        protected override void OnTick(float elapsed)
        {
            // 낮은 프레임에서도 같은 발사 경계는 한 번만 통과
            if (!fired && Elapsed >= fireAt)
            {
                fired = true;
                Fire();
                completeAt = Elapsed + boss.Recovery;
            }

            // 지연된 프레임에서도 실제 발사 시점부터 현재 회복 시간만큼 대기
            if (fired && Elapsed >= completeAt) Complete();
        }

        /// <summary>현재 실행의 추가 발사만 닫고 이미 생성한 탄환은 유지</summary>
        protected override void OnCancel()
        {
            // 취소된 실행의 발사 예약만 닫고 필드 탄환은 유지
            fired = true;
        }

        /// <summary>실제 발사 순간의 총구 위치에서 현재 Player 방향으로 황금 탄환 생성</summary>
        private void Fire()
        {
            // 전투 시작에서 연결한 같은 Player의 현재 위치만 읽고 방향 고정
            if (!IsRunning || boss.Target == null || boss.Target.IsDead) return;
            Vector3 position = boss.Muzzles[boss.MuzzleIndex].position;
            Vector2 offset = (Vector2)boss.Target.transform.position - (Vector2)position;
            Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.down;

            // 보스 자식과 분리해 그로기 뒤에도 탄환 자체의 비행·수명 유지
            GoldProjectile projectile = UnityEngine.Object.Instantiate(boss.GoldPrefab, position, Quaternion.identity, boss.transform.parent);
            projectile.Initialize(boss, direction, boss.BulletSpeed, boss.BulletLifetime, boss.ProjectileDamage);
        }

        /// <summary>발사 설정의 유한한 0 이상 값 확인</summary>
        private static bool Nonnegative(float value)
        {
            // 무효한 숫자와 음수 설정을 발사 전에 제외
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
