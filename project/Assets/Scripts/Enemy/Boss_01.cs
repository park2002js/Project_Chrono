using System;
using UnityEngine;

/// <summary>양 총구 고속탄·교대 내려찍기·양손 중앙 내려찍기·눈 레이저·교대 부채꼴·한 손 충격파를 실행할 경비 보스</summary>
[DefaultExecutionOrder(1)] // Player 이동 이후의 위치를 읽어 같은 프레임의 보스 조준에 사용
public class Boss_01 : BossCharacter
{
    public Transform[] Muzzles = Array.Empty<Transform>(); // 복부 왼쪽·오른쪽 순서로 연결할 두 총구
    public NormalProjectile NormalPrefab; // 원형 탄막과 부채꼴에 쓸 빨강 일반탄 프리팹
    public GoldProjectile GoldFastPrefab; // 양 총구에서 쏠 황금 특수탄 프리팹
    public AreaWarningVfx WarningPrefab; // 직선 경로와 부채꼴 예고로 복제해 쓸 공격 예고 프리팹
    public GuardHandAttack HandAttack; // 두 손의 내려찍기 세 종류를 진행할 실행체
    public GuardEyeLaser EyeLaser; // 양눈 충전과 가속 소사를 진행할 실행체
    public SpriteClipPlayer[] MuzzleFlashes = Array.Empty<SpriteClipPlayer>(); // 왼쪽·오른쪽 총구의 발사 섬광 재생기
    public SpriteClip[] MuzzleFlashClips = Array.Empty<SpriteClip>(); // 왼쪽·오른쪽 총구의 발사 섬광 클립
    public SpriteClipPlayer[] MuzzleCharges = Array.Empty<SpriteClipPlayer>(); // 왼쪽·오른쪽 총구의 충전 효과 재생기
    public SpriteClip MuzzleChargeClip; // 두 총구가 함께 재생할 충전 효과 클립
    public AudioSource MuzzleAudio; // 총구의 준비음과 발사음을 낼 오디오 소스
    public AudioClip MuzzleChargeSound; // 두 총구가 준비 효과를 재생하는 동안 낼 소리
    public AudioClip MuzzleFireSound; // 총구에서 탄환을 쏠 때마다 낼 소리
    [Range(0f, 1f)] public float MuzzleVolume = 0.8f; // 총구 소리의 음량
    public float MuzzleFireSoundStart = 0.15f; // 발사음 앞쪽의 무음 구간을 건너뛰고 재생을 시작할 지점
    public float TwinTrackDuration = 0.7f; // 패턴 2에서 Player를 추적하며 예고A를 보여줄 게임 시간
    public float TwinLockDuration = 0.6f; // 패턴 2에서 경로를 고정하고 예고B를 보여줄 게임 시간
    public float TwinRecovery = 0.5f; // 패턴 2에서 발사 뒤 Idle로 돌아가기까지의 게임 시간
    public float TwinCooldown = 6f; // 패턴 2의 시작부터 다음 사용까지 필요한 게임 시간
    public float TwinBulletSpeed = 50f; // 황금 특수탄의 초당 이동 거리
    public float TwinDamage = 10f; // 황금 특수탄의 피해
    public float TwinPathWidth = 1.125f; // 패턴 2 예고 경로의 폭이며 탄 몸체 지름과 같게 설정
    public float TwinPathOverrun = 0.5f; // 전투 구역 경계에서 벽까지 예고를 더 늘릴 거리
    public Vector2 CentralLandingOffset = new Vector2(0f, -2.7f); // 패턴 4에서 보스 기준점부터 착지점까지의 거리
    public float CentralCooldown = 12f; // 패턴 4의 시작부터 다음 사용까지 필요한 게임 시간
    public int RingBulletCount = 20; // 패턴 4에서 원형 한 줄을 이루는 탄환 수
    public float RingOuterSpeed = 6f; // 바깥 줄 탄환의 초당 이동 거리
    public float RingInnerSpeed = 4.5f; // 안쪽 줄 탄환의 초당 이동 거리
    public float RingDamage = 10f; // 원형 탄환의 피해
    public float LaserCooldown = 10f; // 패턴 5의 시작부터 다음 사용까지 필요한 게임 시간
    public float FanChargeDuration = 1.5f; // 패턴 6에서 두 총구가 준비 효과를 재생할 게임 시간
    public float FanInterval = 0.6f; // 패턴 6에서 좌우를 번갈아 발사하는 간격
    public int FanVolleyCount = 4; // 패턴 6에서 왼쪽부터 번갈아 발사할 총횟수
    public int FanBulletCount = 6; // 한 번의 발사에 한 총구에서 퍼져 나갈 탄환 수
    public float FanAngle = 140f; // 아래 방향을 중심으로 부채꼴이 벌어지는 각도
    public float FanBulletSpeed = 6.5f; // 부채꼴 탄환의 초당 이동 거리
    public float FanDamage = 10f; // 부채꼴 탄환의 피해
    public float FanRecovery = 0.5f; // 마지막 발사 뒤 Idle로 돌아가기까지의 게임 시간
    public float FanCooldown = 10f; // 패턴 6의 시작부터 다음 사용까지 필요한 게임 시간
    public float EdgeCooldown = 14f; // 패턴 7의 시작부터 다음 사용까지 필요한 게임 시간
    public float EdgeInset = 2f; // 패턴 7에서 전투 구역 좌우 끝으로부터 착지점까지의 거리
    public float EdgeVerticalMargin = 3f; // 패턴 7의 착지점을 전투 구역 위아래 끝에서 띄울 거리
    public float EdgeTargetRange = 3f; // 패턴 7의 착지점 세로 위치를 Player 높이에서 위아래로 고를 범위
    public float EdgeWaveTop = 0f; // 패턴 7의 폭발을 허용할 위쪽 한계이며 보스 기준점(몸의 바닥)에서 잰 세로 거리
    [Range(0f, 1f)] public float EdgeWaveReach = 0.75f; // 패턴 7의 충격파가 닿을 지점이며 손이 선 쪽 벽에서 잰 전투 구역 폭에 대한 비율
    public float ScreenMargin = 0.5f; // 공격이 시작되는 지점이 화면 가장자리에서 떨어져 있어야 할 거리

    private readonly AreaWarningVfx[] muzzleWarnings = new AreaWarningVfx[2]; // 두 총구가 패턴 2의 직선 경로에 쓸 예고
    private BossSpawner bossSpawner; // 조우 영역에서 전투 구역 크기를 읽을 자기 생성 담당
    private int idleStopFrame = -1; // 다음 패턴을 위해 새 내려찍기를 시작하지 않기로 한 프레임

    protected override void OnDestroy()
    {
        // 공통 전투 연결을 해제한 뒤 보스 밖에 만들어 둔 예고 객체 정리
        base.OnDestroy();
        for (int i = 0; i < muzzleWarnings.Length; i++)
            if (muzzleWarnings[i] != null) Destroy(muzzleWarnings[i].gameObject);
    }

    /// <summary>자기 프리팹의 발사·손·눈 참조를 확인하고 패턴 2·4·5·6·7 등록</summary>
    protected override void InitializePatterns()
    {
        // Player 생성 전에는 자기 프리팹에 직렬화된 참조만 확인
        bossSpawner = GetComponent<BossSpawner>();
        if (Muzzles == null || Muzzles.Length != 2 || Muzzles[0] == null || Muzzles[1] == null || NormalPrefab == null || GoldFastPrefab == null
            || WarningPrefab == null || HandAttack == null || EyeLaser == null || bossSpawner == null
            || MuzzleFlashes == null || MuzzleFlashes.Length != 2 || MuzzleFlashClips == null || MuzzleFlashClips.Length != 2
            || MuzzleCharges == null || MuzzleCharges.Length != 2)
            throw new InvalidOperationException("Boss_01 requires two muzzles with flash and charge players, both bullet prefabs, a warning prefab, its hand attack, eye laser and spawner.");

        // 예고는 보스의 색 변화에 묶이지 않도록 보스와 같은 부모 아래에 생성
        for (int i = 0; i < muzzleWarnings.Length; i++) muzzleWarnings[i] = Instantiate(WarningPrefab, transform.parent);

        // 패턴 3은 Idle 행동으로 진행하고 나머지 다섯 패턴만 공통 선택기에 등록
        PatternSelector.SetPatterns(new TwinShotPattern(this), new CentralSlamPattern(this), new EyeLaserPattern(this),
            new FanShotPattern(this), new EdgeWavePattern(this));
    }

    /// <summary>패턴 3: 선택된 패턴이 없는 동안 두 손을 번갈아 내려찍음</summary>
    protected override void TickIdle(float gameDelta)
    {
        // 다음 패턴이 기다리는 프레임에는 새 손을 시작하지 않고 진행 중인 손만 마침
        if (!HandAttack.IsRunning && idleStopFrame != Time.frameCount && Target != null) HandAttack.BeginIdleSlam(Target);
        HandAttack.Tick(gameDelta);
    }

    /// <summary>한 손의 추적·예고·내려찍기·복귀가 끝나야 다음 패턴으로 전환</summary>
    protected override bool IsIdleActionRunning => HandAttack != null && HandAttack.IsRunning;

    /// <summary>이번 프레임부터 새 내려찍기를 시작하지 않도록 표시</summary>
    protected override void RequestIdleStop()
    {
        idleStopFrame = Time.frameCount;
    }

    /// <summary>전투 종료·그로기·컷씬에서 손과 총구의 진행 중인 표시 정리</summary>
    protected override void CancelIdleAction()
    {
        if (HandAttack != null && (HandAttack.IsRunning || HandAttack.IsWaveActive)) HandAttack.Cancel();
    }

    /// <summary>조우 영역의 중심과 크기로 탄환·레이저·손이 쓸 전투 구역 계산</summary>
    private bool TryGetArena(out Rect arena)
    {
        // 조절식 전투 구역이 있으면 그 범위를 쓰고 없으면 조우 영역의 변환과 로컬 크기 사용
        if (bossSpawner != null && bossSpawner.Arena != null)
        {
            arena = bossSpawner.Arena.Zone;
            return true;
        }
        if (bossSpawner != null && bossSpawner.EncounterTrigger is BoxCollider2D box)
        {
            Vector2 center = box.transform.TransformPoint(box.offset);
            Vector2 size = Vector2.Scale(box.size, box.transform.lossyScale);
            arena = new Rect(center - size * 0.5f, size);
            return true;
        }
        arena = default;
        return false;
    }

    /// <summary>공격이 시작되는 지점이 지금 화면 안쪽에 있어 Player가 볼 수 있는지 확인</summary>
    private bool IsOnScreen(Vector2 point)
    {
        return GameCamera.Instance == null || GameCamera.Instance.IsVisible(point, ScreenMargin);
    }

    /// <summary>지정 총구의 발사 섬광을 한 번 재생</summary>
    private void PlayMuzzleFlash(int index)
    {
        MuzzleFlashes[index].gameObject.SetActive(true);
        MuzzleFlashes[index].Play(MuzzleFlashClips[index]);
    }

    /// <summary>두 총구의 충전 효과를 함께 켜거나 끔</summary>
    private void SetMuzzleCharge(bool playing, float duration)
    {
        for (int i = 0; i < MuzzleCharges.Length; i++)
        {
            MuzzleCharges[i].gameObject.SetActive(playing);
            if (playing) MuzzleCharges[i].PlayFor(MuzzleChargeClip, duration);
        }

        // 준비음은 준비 효과와 함께 시작하고 준비가 끝나거나 취소되면 멈춤
        if (playing) PlayMuzzleSound(MuzzleChargeSound, 0f);
        else if (MuzzleAudio != null && MuzzleAudio.clip == MuzzleChargeSound) MuzzleAudio.Stop();
    }

    /// <summary>총구에서 탄환을 쏜 순간의 발사음을 앞쪽 무음 구간 뒤부터 재생</summary>
    private void PlayMuzzleFireSound()
    {
        PlayMuzzleSound(MuzzleFireSound, MuzzleFireSoundStart);
    }

    /// <summary>총구 오디오 소스에서 앞선 소리를 끊고 지정한 소리를 시작 지점부터 한 번 재생</summary>
    private void PlayMuzzleSound(AudioClip sound, float start)
    {
        if (MuzzleAudio == null || sound == null) return;
        MuzzleAudio.Stop();
        MuzzleAudio.clip = sound;
        MuzzleAudio.loop = false;
        MuzzleAudio.volume = MuzzleVolume * GameSettings.SfxVolume;
        MuzzleAudio.time = Mathf.Clamp(start, 0f, Mathf.Max(0f, sound.length - 0.01f));
        MuzzleAudio.Play();
    }

    /// <summary>전투가 이어지는 동안에만 탄환을 만들어 보스와 분리된 수명으로 발사</summary>
    private Projectile Fire(Projectile prefab, Vector3 position, Vector2 direction, float speed, float damage)
    {
        if (!IsInCombat || IsDead || IsGroggy || Target == null || Target.IsDead || direction.sqrMagnitude <= 0f) return null;
        Projectile projectile = Instantiate(prefab, position, Quaternion.identity, transform.parent);
        projectile.Initialize(this, direction, speed, 0f, damage);
        return projectile;
    }

    /// <summary>패턴 2: 양 총구에서 Player를 지나는 직선 경로를 예고한 뒤 황금 특수탄을 동시에 발사</summary>
    private class TwinShotPattern : BossPattern
    {
        private readonly Boss_01 boss; // 등록 때 연결한 자기 보스와 발사 설정
        private readonly Projectile[] bullets = new Projectile[2]; // 예고C가 지켜볼 이번 발사의 두 탄환
        private Vector2 aim; // 두 경로가 함께 지나갈 지점
        private Rect arena; // 경로 길이를 정할 전투 구역
        private bool hasArena; // 전투 구역을 읽었는지 여부
        private int stage; // 0 추적, 1 고정, 2 발사 후 회복
        private float completeAt; // 발사 뒤 회복을 마칠 누적 시간

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public TwinShotPattern(Boss_01 owner) : base("F-B07-02")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 유효한 발사 설정 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.TwinCooldown;
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && boss.TwinTrackDuration >= 0f && boss.TwinLockDuration >= 0f && boss.TwinBulletSpeed > 0f && boss.TwinPathWidth > 0f
                && boss.IsOnScreen(boss.Muzzles[0].position) && boss.IsOnScreen(boss.Muzzles[1].position);
        }

        /// <summary>현재 Player 위치로 두 경로의 예고A 시작</summary>
        protected override void OnBegin()
        {
            stage = 0;
            completeAt = float.PositiveInfinity;
            hasArena = boss.TryGetArena(out arena);
            aim = boss.Target.transform.position;
            UpdatePaths();
            for (int i = 0; i < 2; i++) boss.muzzleWarnings[i].BeginNotice();
        }

        /// <summary>추적 → 고정 → 발사 → 회복을 패턴 시간으로 진행</summary>
        protected override void OnTick(float elapsed)
        {
            boss.HandAttack.Tick(elapsed);
            if (stage == 0)
            {
                // 추적이 끝나는 순간의 Player 위치를 경로에 고정하고 예고B 시작
                aim = boss.Target.transform.position;
                UpdatePaths();
                if (Elapsed < boss.TwinTrackDuration) return;
                stage = 1;
                boss.muzzleWarnings[0].BeginImminent(boss.TwinLockDuration);
                boss.muzzleWarnings[1].BeginImminent(boss.TwinLockDuration);
            }
            if (stage == 1 && Elapsed >= boss.TwinTrackDuration + boss.TwinLockDuration)
            {
                stage = 2;
                FireBoth();
                completeAt = Elapsed + boss.TwinRecovery;
            }
            if (stage == 2 && Elapsed >= completeAt) Complete();
        }

        /// <summary>발사 전이면 예고를 지우고 발사 뒤면 탄환과 예고C를 그대로 유지</summary>
        protected override void OnCancel()
        {
            if (stage < 2)
                for (int i = 0; i < 2; i++) boss.muzzleWarnings[i].Hide();
            stage = 2;
        }

        /// <summary>각 총구에서 조준 지점을 지나 전투 구역 끝까지 이어지는 열린 경로 배치</summary>
        private void UpdatePaths()
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 start = boss.Muzzles[i].position;
                Vector2 direction = GetDirection(start);
                float length = hasArena ? ExitDistance(start, direction) + boss.TwinPathOverrun : 30f;
                boss.muzzleWarnings[i].ShowOpenPath(start, start + direction * Mathf.Max(1f, length), boss.TwinPathWidth);
            }
        }

        /// <summary>총구에서 조준 지점을 향하는 방향이며 겹칠 때는 아래 방향 사용</summary>
        private Vector2 GetDirection(Vector2 start)
        {
            Vector2 offset = aim - start;
            return offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.down;
        }

        /// <summary>구역 안의 점에서 주어진 방향으로 나아가 구역 경계에 닿는 거리</summary>
        private float ExitDistance(Vector2 start, Vector2 direction)
        {
            float distance = float.PositiveInfinity;
            if (direction.x > 0f) distance = Mathf.Min(distance, (arena.xMax - start.x) / direction.x);
            else if (direction.x < 0f) distance = Mathf.Min(distance, (arena.xMin - start.x) / direction.x);
            if (direction.y > 0f) distance = Mathf.Min(distance, (arena.yMax - start.y) / direction.y);
            else if (direction.y < 0f) distance = Mathf.Min(distance, (arena.yMin - start.y) / direction.y);
            return float.IsInfinity(distance) || distance < 0f ? 0f : distance;
        }

        /// <summary>두 총구에서 같은 순간 황금 특수탄을 쏘고 각 경로를 예고C로 전환</summary>
        private void FireBoth()
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 start = boss.Muzzles[i].position;
                bullets[i] = boss.Fire(boss.GoldFastPrefab, start, GetDirection(start), boss.TwinBulletSpeed, boss.TwinDamage);
                boss.PlayMuzzleFlash(i);

                // 경로를 지나는 탄환이 사라지거나 반사되면 예고가 스스로 사라지도록 연결
                if (bullets[i] != null) boss.muzzleWarnings[i].BeginDanger(bullets[i]);
                else boss.muzzleWarnings[i].Hide();
            }
            boss.PlayMuzzleFireSound();
        }
    }

    /// <summary>패턴 4: 두 손을 모아 충전한 뒤 중앙을 내려치고 2중 원형 탄막 발사</summary>
    private class CentralSlamPattern : BossPattern
    {
        private readonly Boss_01 boss; // 같은 보스와 두 손 실행체
        private readonly Action<Vector2> fireRings; // 매 실행에 다시 할당하지 않을 착지 순간 발사 콜백

        /// <summary>패턴 식별자와 착지 콜백을 한 번 준비</summary>
        public CentralSlamPattern(Boss_01 owner) : base("F-B07-04")
        {
            boss = owner;
            fireRings = FireRings;
        }

        /// <summary>대기 중인 두 손과 유효한 탄환 설정 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.CentralCooldown;
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && !boss.HandAttack.IsRunning && boss.RingBulletCount > 0 && boss.RingOuterSpeed > 0f && boss.RingInnerSpeed > 0f
                && boss.IsOnScreen((Vector2)boss.transform.position + boss.CentralLandingOffset);
        }

        /// <summary>보스 앞 착지점 위로 두 손을 모으기 시작</summary>
        protected override void OnBegin()
        {
            Vector2 landing = (Vector2)boss.transform.position + boss.CentralLandingOffset;
            if (!boss.HandAttack.BeginCentral(boss.Target, landing, fireRings)) Complete();
        }

        /// <summary>두 손의 이동·충전·착지·복귀가 끝나면 정상 완료</summary>
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

        /// <summary>착지점을 중심으로 서로 반 칸 어긋난 원형 두 줄의 일반탄 발사</summary>
        private void FireRings(Vector2 center)
        {
            if (!IsRunning) return;
            int count = boss.RingBulletCount;
            float step = 360f / count;
            Vector3 position = new Vector3(center.x, center.y, boss.transform.position.z);
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < count; i++)
                {
                    // 안쪽 줄은 바깥 줄의 탄 사이 각도에서 더 느리게 출발
                    Vector2 direction = Quaternion.Euler(0f, 0f, step * (i + ring * 0.5f)) * Vector2.right;
                    boss.Fire(boss.NormalPrefab, position, direction, ring == 0 ? boss.RingOuterSpeed : boss.RingInnerSpeed, boss.RingDamage);
                }
        }
    }

    /// <summary>패턴 5: 눈 충전 뒤 Player를 지나는 경로로 두 빔의 지면 접점을 가속 이동</summary>
    private class EyeLaserPattern : BossPattern
    {
        private readonly Boss_01 boss; // 같은 보스와 눈 레이저 실행체

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public EyeLaserPattern(Boss_01 owner) : base("F-B07-05")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 대기 중인 눈 레이저 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.LaserCooldown;
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead && !boss.EyeLaser.IsRunning
                && boss.IsOnScreen(boss.EyeLaser.Eyes[0].position) && boss.IsOnScreen(boss.EyeLaser.Eyes[1].position);
        }

        /// <summary>전투 구역을 전달하고 양눈 충전 시작</summary>
        protected override void OnBegin()
        {
            boss.TryGetArena(out Rect arena);
            if (!boss.EyeLaser.Begin(boss.Target, arena)) Complete();
        }

        /// <summary>충전·소사·회복을 같은 패턴 시간으로 진행</summary>
        protected override void OnTick(float elapsed)
        {
            boss.HandAttack.Tick(elapsed);
            boss.EyeLaser.Tick(elapsed);
            if (!boss.EyeLaser.IsRunning) Complete();
        }

        /// <summary>그로기·사망·재도전에서 눈 충전과 진행 중인 빔을 즉시 정리</summary>
        protected override void OnCancel()
        {
            boss.EyeLaser.Cancel();
        }
    }

    /// <summary>패턴 6: 두 총구가 함께 충전한 뒤 바닥 예고 없이 부채꼴 탄환을 좌·우·좌·우로 번갈아 발사</summary>
    private class FanShotPattern : BossPattern
    {
        private readonly Boss_01 boss; // 같은 보스와 두 총구
        private bool charged; // 두 총구의 준비 효과가 끝났는지 여부
        private int fired; // 이번 실행에서 이미 발사한 횟수
        private float nextVolleyAt; // 다음 발사를 시작할 누적 게임 시간
        private float completeAt; // 마지막 발사 뒤 회복을 마칠 누적 시간

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public FanShotPattern(Boss_01 owner) : base("F-B07-06")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 유효한 부채꼴 설정 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.FanCooldown;
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && boss.FanVolleyCount > 0 && boss.FanBulletCount > 0 && boss.FanInterval > 0f && boss.FanBulletSpeed > 0f
                && boss.FanAngle > 0f && boss.FanAngle < 360f
                && boss.IsOnScreen(boss.Muzzles[0].position) && boss.IsOnScreen(boss.Muzzles[1].position);
        }

        /// <summary>두 총구의 준비 효과를 같은 순간 시작</summary>
        protected override void OnBegin()
        {
            charged = false;
            fired = 0;
            nextVolleyAt = boss.FanChargeDuration;
            completeAt = float.PositiveInfinity;
            boss.SetMuzzleCharge(true, boss.FanChargeDuration);
        }

        /// <summary>충전 → 번갈아 발사 → 회복을 패턴 시간으로 진행</summary>
        protected override void OnTick(float elapsed)
        {
            boss.HandAttack.Tick(elapsed);
            if (!charged)
            {
                if (Elapsed < boss.FanChargeDuration) return;

                // 준비 효과가 끝나는 순간 곧바로 첫 발사로 이어짐
                charged = true;
                boss.SetMuzzleCharge(false, 0f);
            }

            // 지연된 프레임에서도 발사를 몰아내지 않고 한 프레임에 한 번씩 순서대로 실행
            if (fired < boss.FanVolleyCount && Elapsed + 0.00001f >= nextVolleyAt)
            {
                FireVolley(fired % 2);
                fired++;
                nextVolleyAt += boss.FanInterval;
                if (fired >= boss.FanVolleyCount) completeAt = Elapsed + boss.FanRecovery;
            }
            if (fired >= boss.FanVolleyCount && Elapsed >= completeAt) Complete();
        }

        /// <summary>남은 발사만 취소하고 준비 효과를 지우며 이미 쏜 탄환은 유지</summary>
        protected override void OnCancel()
        {
            fired = int.MaxValue;
            boss.SetMuzzleCharge(false, 0f);
        }

        /// <summary>지정 총구에서 아래 방향을 중심으로 고르게 벌린 일반탄 발사</summary>
        private void FireVolley(int muzzle)
        {
            if (!IsRunning) return;
            Vector3 position = boss.Muzzles[muzzle].position;
            int count = boss.FanBulletCount;
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : -boss.FanAngle * 0.5f + boss.FanAngle * i / (count - 1);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.down;
                boss.Fire(boss.NormalPrefab, position, direction, boss.FanBulletSpeed, boss.FanDamage);
            }
            boss.PlayMuzzleFlash(muzzle);
            boss.PlayMuzzleFireSound();
        }
    }

    /// <summary>패턴 7: 한 손이 좌우 가장자리에서 기를 모은 뒤 내려쳐 Player 쪽으로 충격파 전파</summary>
    private class EdgeWavePattern : BossPattern
    {
        private readonly Boss_01 boss; // 같은 보스와 두 손 실행체

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public EdgeWavePattern(Boss_01 owner) : base("F-B07-07")
        {
            boss = owner;
        }

        /// <summary>대기 중인 두 손과 준비된 전투 구역 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.EdgeCooldown;
            return owner == boss && base.CanRun(owner) && boss.Target != null && !boss.Target.IsDead
                && !boss.HandAttack.IsRunning && !boss.HandAttack.IsWaveActive && boss.TryGetArena(out _);
        }

        /// <summary>무작위로 고른 손을 같은 쪽 가장자리의 무작위 높이로 보내고 폭발을 허용할 구역과 닿을 거리 고정</summary>
        protected override void OnBegin()
        {
            boss.TryGetArena(out Rect arena);
            int hand = UnityEngine.Random.Range(0, 2);
            float x = hand == 0 ? arena.xMin + boss.EdgeInset : arena.xMax - boss.EdgeInset;

            // 보스 몸과 겹치지 않도록 보스 기준점보다 아래에서, Player가 보고 있는 높이 근처로 착지점 선택
            float top = Mathf.Min(arena.yMax - boss.EdgeVerticalMargin, boss.transform.position.y - 1f);
            float bottom = Mathf.Min(top, arena.yMin + boss.EdgeVerticalMargin);
            float focus = Mathf.Clamp(boss.Target.transform.position.y, bottom, top);
            var landing = new Vector2(x, UnityEngine.Random.Range(Mathf.Max(bottom, focus - boss.EdgeTargetRange), Mathf.Min(top, focus + boss.EdgeTargetRange)));

            // 폭발은 보스 몸의 바닥보다 아래쪽 바닥에서만 일어나고, 손이 선 쪽 벽에서 잰 구역 폭의 정해진 비율 지점까지 닿음
            Rect waveArea = Rect.MinMaxRect(arena.xMin, arena.yMin, arena.xMax, Mathf.Min(arena.yMax, boss.transform.position.y + boss.EdgeWaveTop));
            float reach = Mathf.Max(1f, arena.width * boss.EdgeWaveReach - boss.EdgeInset);
            if (!boss.HandAttack.BeginEdgeWave(boss.Target, hand, landing, arena.center - landing, waveArea, reach)) Complete();
        }

        /// <summary>손의 복귀와 충격파 전파가 모두 끝나면 정상 완료</summary>
        protected override void OnTick(float elapsed)
        {
            boss.HandAttack.Tick(elapsed);
            if (!boss.HandAttack.IsRunning && !boss.HandAttack.IsWaveActive) Complete();
        }

        /// <summary>아직 오지 않은 착지와 진행 중인 충격파를 닫고 손과 예고 복원</summary>
        protected override void OnCancel()
        {
            boss.HandAttack.Cancel();
        }
    }
}
