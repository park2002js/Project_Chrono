using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>양눈 레이저의 에너지 충전·지연 추적 발사·회복 단계</summary>
public enum GuardEyeLaserPhase
{
    Idle, // 눈의 발광과 빔을 숨긴 기본 상태
    Charging, // 양눈 에너지를 모으며 목표 위치 이력 수집
    Firing, // 과거 Player 위치를 향해 두 빔을 연속 조준
    Recovery // 빔을 닫고 다음 패턴 전환까지 대기
}

/// <summary>0.4게임초 과거 지면으로 내려오는 양눈 빔과 착탄점의 공통 일회 피해를 같은 Tick에서 진행</summary>
public class GuardEyeLaser : MonoBehaviour
{
    public Transform[] Eyes = Array.Empty<Transform>(); // 같은 보스의 왼눈·오른눈 발사 원점
    public LineRenderer[] BeamCores = Array.Empty<LineRenderer>(); // 두 빔의 밝은 흰 중심 표시
    public LineRenderer[] BeamBodies = Array.Empty<LineRenderer>(); // 두 빔의 두꺼운 반투명 연분홍 띠 표시
    public LineRenderer[] BeamGlows = Array.Empty<LineRenderer>(); // 부드러운 알파 단면을 사용할 두 빔의 외광 표시
    public SpriteRenderer[] ChargeLights = Array.Empty<SpriteRenderer>(); // 양눈에서 커지며 밝아질 충전 광원
    public AreaWarningVfx[] ImpactWarnings = Array.Empty<AreaWarningVfx>(); // 실제 착탄 반경을 고정된 분홍 원으로 표시할 두 지면 경고
    public SpriteRenderer[] ImpactCores = Array.Empty<SpriteRenderer>(); // 두 착탄점의 밝은 흰 중심 표시
    public float ChargeDuration = 1.2f; // 실제 빔 발사 전에 눈 에너지를 모을 게임 시간
    public float FiringDuration = 3f; // 실제 빔을 처음 표시한 뒤 지연 추적을 유지할 게임 시간
    public float RecoveryDuration = 0.5f; // 두 빔을 닫은 뒤 다음 패턴까지의 게임 시간
    public float BeamWidth = 0.5f; // 공중 분홍 빔 폭과 두 지면 착탄점의 수평 간격
    public float MaxLength = 40f; // 지면 목표까지의 빔 길이에 적용할 최대 상한
    public float ImpactRadius = 0.35f; // 각 지면 표시의 외곽선과 실제 착탄 피해가 공유할 반경
    public float BeamDamage = 10f; // 두 지면 착탄점을 합쳐 한 번 성공할 Player 피해
    public bool IsRunning { get; private set; } // 패턴이 시간 진행 또는 취소를 전달할 실행 상태
    public GuardEyeLaserPhase Phase { get; private set; } // 현재 충전·발사·회복 진행 단계
    public float PhaseElapsed { get; private set; } // 현재 표시 단계에서 경과한 게임 시간
    public float HistoryTime { get; private set; } // 이번 실행에서 위치 이력을 수집한 누적 게임 시간
    public Vector2 AimPosition { get; private set; } // 현재 두 눈이 함께 조준할 과거 Player 위치
    public float DelaySeconds => AimDelay; // 사용자 확정으로 유지할 지연 조준 시간
    public float AimSampleTime => Mathf.Max(0f, HistoryTime - AimDelay); // 현재 보간에 사용하는 과거 게임 시각

    private const float AimDelay = 0.4f; // 현재 위치 대신 정확히 이 게임 시간 전의 위치를 조준
    private readonly float[] beamLengths = new float[2]; // 같은 Tick의 지면 착탄점에서 끝낼 두 공중 빔의 유한 길이
    private readonly Vector2[] impactPositions = new Vector2[2]; // 빔 끝과 원형 피해가 같은 Tick에서 공유할 지면 착탄점
    private readonly Color[] chargeColors = new Color[2]; // 취소와 재시작에서 복원할 충전 광원 원래 색
    private readonly Vector3[] chargeScales = new Vector3[2]; // 충전 진행률에 곱할 각 광원 원래 크기
    private readonly HashSet<PlayerCharacter> hitPlayers = new HashSet<PlayerCharacter>(); // 두 착탄점 전체에서 성공한 Player 피해 중복 방지
    private float[] sampleTimes = new float[128]; // 지난 위치의 게임 시각에 재사용할 원형 버퍼
    private Vector2[] samplePositions = new Vector2[128]; // 같은 게임 시각의 Player 위치에 재사용할 원형 버퍼
    private RaycastHit2D[] wallHits = new RaycastHit2D[16]; // 지면 목표 앞의 맵 벽까지 확인할 재사용 결과
    private Collider2D[] overlaps = new Collider2D[8]; // 두 지면 원형 피격 영역 질의에 재사용할 결과
    private BossCharacter owner; // 자기 생성 범위에서 계속 재사용할 피해 출처
    private PlayerCharacter target; // 이번 패턴 시작에서 주입받은 같은 전투의 Player
    private MapManager map; // 발사 경계에서 한 번 연결한 맵 벽 판별 담당
    private ContactFilter2D wallFilter; // 눈과 겹친 출처 벽을 제외할 벽 질의 조건
    private ContactFilter2D damageFilter; // Trigger를 포함한 Player 피격 영역 질의 조건
    private int sampleHead; // 원형 이력 버퍼의 가장 오래된 유효 위치
    private int sampleCount; // 이번 실행에 남아 있는 유효한 위치 이력 수
    private int attackId; // 두 눈의 성공 피해를 같은 발사로 구분할 번호
    private int runVersion; // 피해 콜백의 취소와 새 실행을 이전 발사에서 구분할 번호
    private bool prepared; // 자기 참조·광원 원본·질의 조건의 일회 준비 완료 상태

    /// <summary>자기 눈·표시·질의 참조를 한 번 준비하고 기본 표시로 복원</summary>
    private void Awake()
    {
        Prepare();
        Cancel();
    }

    private void OnDisable()
    {
        // 현재 연속 빔과 충전 표시만 닫고 이미 떠난 다른 탄환은 유지
        Cancel();
    }

    /// <summary>같은 전투의 Player를 연결하고 양눈 충전과 과거 위치 수집 시작</summary>
    public bool Begin(PlayerCharacter player)
    {
        // 전투 대상과 시간을 확인한 뒤 표시를 바꾸어 부분 실행 방지
        Prepare();
        if (IsRunning || !OwnerReady() || player == null || player.IsDead || !player.IsInitialized || !player.isActiveAndEnabled
            || !Positive(ChargeDuration) || ChargeDuration < AimDelay || !Positive(FiringDuration) || !Nonnegative(RecoveryDuration)
            || !Positive(BeamWidth) || !Positive(MaxLength) || !Positive(ImpactRadius) || !Nonnegative(BeamDamage)) return false;

        // 같은 샘플 버퍼를 비우고 시작 위치부터 이번 실행의 이력 생성
        unchecked { runVersion++; }
        target = player;
        map = MapManager.Instance;
        HistoryTime = 0f;
        PhaseElapsed = 0f;
        Phase = GuardEyeLaserPhase.Charging;
        sampleHead = sampleCount = 0;
        AimPosition = player.transform.position;
        AppendSample(AimPosition);
        hitPlayers.Clear();
        attackId = DamageInstance.NextAttackId();
        IsRunning = true;

        // 표시 원본은 유지하고 충전 시작의 작은 광원만 표시
        SetBeamsVisible(false);
        SetImpactsVisible(false);
        ConfigureWidths();
        ConfigureImpacts();
        SetChargeProgress(0f, true);
        return true;
    }

    /// <summary>과거 조준·두 빔 표시·공통 피해를 하나의 게임 시간 Tick에서 순서대로 처리</summary>
    public void Tick(float gameDelta)
    {
        // 사망·그로기·전투 종료는 시간 정지와 별개로 현재 연속 빔을 즉시 닫음
        if (!IsRunning) return;
        if (!OwnerReady() || target == null || target.IsDead || !target.isActiveAndEnabled)
        {
            Cancel();
            return;
        }
        if (Time.timeScale <= 0f || !Positive(gameDelta)) return;

        // 이번 실제 프레임의 위치를 먼저 기록하고 0.4초 전 두 샘플 사이를 보간
        HistoryTime += gameDelta;
        AppendSample(target.transform.position);
        AimPosition = ReadDelayedPosition();
        PhaseElapsed += gameDelta;
        if (Phase == GuardEyeLaserPhase.Charging)
        {
            SetChargeProgress(Mathf.Clamp01(PhaseElapsed / ChargeDuration), true);
            if (PhaseElapsed < ChargeDuration) return;
            Phase = GuardEyeLaserPhase.Firing;
            PhaseElapsed = 0f;
            SetChargeProgress(1f, false);
            SetBeamsVisible(true);
            SetImpactsVisible(true);
            UpdateFiringBeams();
            return;
        }
        if (Phase == GuardEyeLaserPhase.Firing)
        {
            // 실제 처음 표시한 시점부터 발사 시간을 확보하고 종료 뒤에는 추가 판정 차단
            if (PhaseElapsed < FiringDuration)
            {
                UpdateFiringBeams();
                return;
            }
            SetBeamsVisible(false);
            SetImpactsVisible(false);
            Phase = GuardEyeLaserPhase.Recovery;
            PhaseElapsed = 0f;
            return;
        }
        if (Phase == GuardEyeLaserPhase.Recovery && PhaseElapsed >= RecoveryDuration) Cancel();
    }

    /// <summary>현재 충전·빔·미래 피해를 닫고 같은 눈 표시를 재사용 상태로 복원</summary>
    public void Cancel()
    {
        // 피해 콜백에서 취소되어도 이전 발사의 다음 눈 판정이 이어지지 않도록 먼저 종료
        unchecked { runVersion++; }
        IsRunning = false;
        Phase = GuardEyeLaserPhase.Idle;
        PhaseElapsed = 0f;
        HistoryTime = 0f;
        sampleHead = sampleCount = 0;
        target = null;
        map = null;
        if (!prepared) return;

        // 다른 위험물과 재질은 유지하고 자기 눈 광원과 빔만 숨김
        SetBeamsVisible(false);
        SetImpactsVisible(false);
        for (int i = 0; i < Eyes.Length; i++)
        {
            beamLengths[i] = 0f;
            ChargeLights[i].color = chargeColors[i];
            ChargeLights[i].transform.localScale = chargeScales[i];
            ChargeLights[i].enabled = false;
        }
    }

    /// <summary>지연된 지면 목표까지 실제로 표시한 공중 빔 길이 조회</summary>
    public float GetBeamLength(int index)
    {
        // 두 눈 이외의 번호로 미정 데이터를 조회하지 않도록 거부
        if (index < 0 || index >= beamLengths.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return beamLengths[index];
    }

    /// <summary>공중 빔 끝과 원형 피해가 실제로 공유하는 지면 착탄점 조회</summary>
    public Vector2 GetImpactPosition(int index)
    {
        // 두 눈 이외의 번호로 미정 착탄점을 조회하지 않도록 거부
        if (index < 0 || index >= impactPositions.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return impactPositions[index];
    }

    /// <summary>자기 생성 범위의 눈·광원·렌더러와 재사용 질의 조건 준비</summary>
    private void Prepare()
    {
        // 외부 Map과 Player의 Awake 순서에 의존하지 않고 자기 프리팹 참조만 확인
        if (prepared) return;
        owner = GetComponent<BossCharacter>();
        if (owner == null || Eyes == null || Eyes.Length != 2 || BeamCores == null || BeamCores.Length != 2
            || BeamBodies == null || BeamBodies.Length != 2 || BeamGlows == null || BeamGlows.Length != 2
            || ChargeLights == null || ChargeLights.Length != 2 || ImpactWarnings == null || ImpactWarnings.Length != 2
            || ImpactCores == null || ImpactCores.Length != 2)
            throw new InvalidOperationException("Guard eye laser requires its Boss, two eyes and two complete beam visuals.");
        for (int i = 0; i < Eyes.Length; i++)
        {
            if (Eyes[i] == null || BeamCores[i] == null || BeamBodies[i] == null || BeamGlows[i] == null || ChargeLights[i] == null
                || ImpactWarnings[i] == null || ImpactCores[i] == null)
                throw new InvalidOperationException("Guard eye laser has an unassigned eye, beam layer or charge light.");
            chargeColors[i] = ChargeLights[i].color;
            chargeScales[i] = ChargeLights[i].transform.localScale;
            PrepareLine(BeamCores[i]);
            PrepareLine(BeamBodies[i]);
            PrepareLine(BeamGlows[i]);
        }
        wallFilter = new ContactFilter2D { useTriggers = true };
        damageFilter = new ContactFilter2D { useTriggers = true };
        prepared = true;
    }

    /// <summary>두 공중 빔 본체와 더 얇은 중심·넓은 외광 크기 적용</summary>
    private void ConfigureWidths()
    {
        // 공중 본체·중심·외광은 표시 전용이며 피해는 지면 원형 범위에서 처리
        for (int i = 0; i < Eyes.Length; i++)
        {
            BeamCores[i].startWidth = BeamCores[i].endWidth = BeamWidth * 0.3f;
            BeamBodies[i].startWidth = BeamBodies[i].endWidth = BeamWidth;
            BeamGlows[i].startWidth = BeamGlows[i].endWidth = BeamWidth * 2f;
        }
    }

    /// <summary>같은 원형 에셋으로 실제 착탄 반경과 밝은 지면 표시 준비</summary>
    private void ConfigureImpacts()
    {
        // 충전 진행과 별개로 착탄 외곽선과 내부는 고정된 실제 반경 사용
        for (int i = 0; i < Eyes.Length; i++)
        {
            ImpactWarnings[i].transform.localRotation = Quaternion.identity;
            ImpactWarnings[i].transform.localScale = Vector3.one * (ImpactRadius * 2f);
            ImpactWarnings[i].Configure(WarningShape.Circle, ImpactWarnings[i].WarningColor);
            ImpactWarnings[i].FillRenderer.transform.localScale = Vector3.one;
        }
    }

    /// <summary>두 공중 빔을 지연된 지면 착탄점에서 끝내고 그 원형 범위만 질의</summary>
    private void UpdateFiringBeams()
    {
        // 한 눈의 피해 콜백이 다른 눈의 이전 판정을 이어 실행하지 못하도록 실행 번호 고정
        int version = runVersion;
        for (int i = 0; i < Eyes.Length; i++)
        {
            Vector2 origin = Eyes[i].position;
            Vector2 groundTarget = AimPosition + Vector2.right * (i == 0 ? -BeamWidth * 0.5f : BeamWidth * 0.5f);
            Vector2 offset = groundTarget - origin;
            float targetDistance = offset.magnitude;
            Vector2 direction = targetDistance > 0f ? offset / targetDistance : Vector2.down;
            float length = ClipLength(origin, direction, targetDistance);
            beamLengths[i] = length;
            impactPositions[i] = origin + direction * length;
            Vector3 end = new Vector3(impactPositions[i].x, impactPositions[i].y, owner.transform.position.z);
            SetLinePoints(BeamCores[i], Eyes[i].position, end);
            SetLinePoints(BeamBodies[i], Eyes[i].position, end);
            SetLinePoints(BeamGlows[i], Eyes[i].position, end);
            ImpactWarnings[i].transform.position = end;
            ImpactWarnings[i].transform.localScale = Vector3.one * (ImpactRadius * 2f);
            ImpactCores[i].transform.position = end;
        }

        // 공중 선분은 피해를 주지 않고 두 착탄 원 전체의 성공 피해만 한 번으로 제한
        for (int i = 0; i < Eyes.Length && IsRunning && runVersion == version; i++) QueryImpact(i, version);
    }

    /// <summary>지면 목표까지만 확인하고 그 앞의 벽에서는 착탄 원이 벽 앞에 머물도록 보정</summary>
    private float ClipLength(Vector2 origin, Vector2 direction, float targetDistance)
    {
        // 목표가 가까우면 최대 길이까지 연장하지 않고 같은 버퍼와 Map만 재사용
        float length = Mathf.Min(targetDistance, MaxLength);
        if (map == null) return length;
        int count;
        do
        {
            count = Physics2D.CircleCast(origin, ImpactRadius, direction, wallFilter, wallHits, length);
            if (count < wallHits.Length) break;
            Array.Resize(ref wallHits, checked(wallHits.Length * 2));
        } while (true);
        for (int i = 0; i < count; i++)
        {
            Collider2D wall = wallHits[i].collider;
            if (map.IsWall(wall) && !wall.OverlapPoint(origin)) length = Mathf.Min(length, wallHits[i].distance);
        }
        return length;
    }

    /// <summary>바닥에 표시한 착탄 원과 같은 중심·반경에서 성공 피해 한 번 전달</summary>
    private void QueryImpact(int index, int version)
    {
        // 공중 빔 중간과 외광은 피해에서 제외하고 실제 지면 범위만 사용
        int count;
        do
        {
            count = Physics2D.OverlapCircle(impactPositions[index], ImpactRadius, damageFilter, overlaps);
            if (count < overlaps.Length) break;
            Array.Resize(ref overlaps, checked(overlaps.Length * 2));
        } while (true);
        for (int i = 0; i < count && IsRunning && runVersion == version; i++)
        {
            Collider2D other = overlaps[i];
            if (other == null || !other.TryGetComponent(out Hitbox hitbox) || !(hitbox.Owner is PlayerCharacter player)
                || hitPlayers.Contains(player)) continue;
            if (hitbox.ReceiveHit(new DamageInstance(owner, BeamDamage, DamageType.Normal, attackId)) && runVersion == version)
                hitPlayers.Add(player);
        }
    }

    /// <summary>같은 위치 버퍼에 현재 게임 시각과 Player 위치를 추가</summary>
    private void AppendSample(Vector2 position)
    {
        // 높은 프레임에서도 0.4초 이력을 잃지 않도록 포화 경계에서만 버퍼 확장
        if (sampleCount == sampleTimes.Length)
        {
            int capacity = checked(sampleTimes.Length * 2);
            var times = new float[capacity];
            var positions = new Vector2[capacity];
            for (int i = 0; i < sampleCount; i++)
            {
                int index = (sampleHead + i) % sampleTimes.Length;
                times[i] = sampleTimes[index];
                positions[i] = samplePositions[index];
            }
            sampleTimes = times;
            samplePositions = positions;
            sampleHead = 0;
        }
        int next = (sampleHead + sampleCount) % sampleTimes.Length;
        sampleTimes[next] = HistoryTime;
        samplePositions[next] = position;
        sampleCount++;
    }

    /// <summary>목표 게임 시각을 감싸는 두 위치 사이를 보간해 고정 지연 조준 계산</summary>
    private Vector2 ReadDelayedPosition()
    {
        // 필요한 과거 샘플 하나를 남기고 더 오래된 기록만 제거
        float time = AimSampleTime;
        while (sampleCount > 2 && sampleTimes[(sampleHead + 1) % sampleTimes.Length] <= time)
        {
            sampleHead = (sampleHead + 1) % sampleTimes.Length;
            sampleCount--;
        }
        if (sampleCount == 1 || time <= sampleTimes[sampleHead]) return samplePositions[sampleHead];
        int next = (sampleHead + 1) % sampleTimes.Length;
        float progress = Mathf.InverseLerp(sampleTimes[sampleHead], sampleTimes[next], time);
        return Vector2.Lerp(samplePositions[sampleHead], samplePositions[next], progress);
    }

    /// <summary>같은 눈 광원을 충전 진행률에 따라 밝히고 확대</summary>
    private void SetChargeProgress(float progress, bool visible)
    {
        // 새 객체나 재질 없이 원래 색과 크기에 표시 배율만 적용
        for (int i = 0; i < Eyes.Length; i++)
        {
            Color color = chargeColors[i];
            color.a *= Mathf.Lerp(0.2f, 1f, progress);
            ChargeLights[i].color = color;
            ChargeLights[i].transform.localScale = chargeScales[i] * Mathf.Lerp(0.4f, 1f, progress);
            ChargeLights[i].enabled = visible;
        }
    }

    /// <summary>원래 색과 공유 재질을 유지하며 공중의 여섯 빔 표시만 전환</summary>
    private void SetBeamsVisible(bool visible)
    {
        // 눈 원점과 지면 종점은 별도 기하 갱신에서 적용
        for (int i = 0; i < Eyes.Length; i++)
        {
            BeamCores[i].enabled = visible;
            BeamBodies[i].enabled = visible;
            BeamGlows[i].enabled = visible;
        }
    }

    /// <summary>두 착탄점의 원형 범위와 흰 중심을 같은 발사 구간에만 표시</summary>
    private void SetImpactsVisible(bool visible)
    {
        // 별도 피해 Collider를 만들지 않고 고정된 지면 표시 객체만 재사용
        for (int i = 0; i < Eyes.Length; i++)
        {
            ImpactWarnings[i].gameObject.SetActive(visible);
            ImpactCores[i].enabled = visible;
        }
    }

    /// <summary>두 점의 월드 지면 좌표와 둥근 양끝을 가진 빔 렌더러 준비</summary>
    private static void PrepareLine(LineRenderer line)
    {
        // 같은 월드 두 점과 둥근 끝을 모든 표시 층에 적용
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.numCapVertices = 8;
        line.numCornerVertices = 0;
    }

    /// <summary>같은 빔의 모든 표시 층에 동일한 시작점과 끝점 적용</summary>
    private static void SetLinePoints(LineRenderer line, Vector3 from, Vector3 to)
    {
        // 배열을 새로 만들지 않고 기존 두 끝점만 갱신
        line.SetPosition(0, from);
        line.SetPosition(1, to);
    }

    /// <summary>같은 보스의 현재 전투 수명에서 연속 빔을 유지할 수 있는지 확인</summary>
    private bool OwnerReady()
    {
        // 고정한 자기 보스 참조에서 현재 전투 상태만 조회
        return isActiveAndEnabled && owner != null && owner.isActiveAndEnabled && owner.IsInitialized
            && !owner.IsDead && owner.IsInCombat && !owner.IsGroggy && !owner.IsCutScene;
    }

    /// <summary>양수 유한값이 필요한 시간·폭·길이 확인</summary>
    private static bool Positive(float value) => Nonnegative(value) && value > 0f;

    /// <summary>0 이상 유한값이 필요한 회복 시간과 피해 확인</summary>
    private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}
