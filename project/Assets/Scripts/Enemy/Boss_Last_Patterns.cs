using System.Collections.Generic;
using UnityEngine;

/// <summary>최종 보스의 여섯 패턴과 차원문·기둥·수정의 진행</summary>
public partial class Boss_Last
{
    public float TeleportGap = 0.1f; // 패턴 1에서 사라진 뒤 다른 위치에 나타나기까지의 게임 시간
    public float TeleportRecover = 0.25f; // 패턴 1에서 발사 뒤 다음 이동을 시작하기까지의 게임 시간
    public float TeleportCooldown = 9f; // 패턴 1의 시작부터 다음 사용까지 필요한 게임 시간
    public float HomingSpeedRatio = 1.1f; // 유도탄 속도를 Player 이동 속도의 몇 배로 둘지 정하는 비율
    public float HomingLifetime = 6f; // 유도탄이 스스로 터지기까지의 게임 시간
    public float DashSpeed = 5f; // 패턴 2에서 지정 위치로 가는 느린 대시의 초당 이동 거리
    public float AfterimageInterval = 0.06f; // 대시 중 잔상을 남기는 간격
    public int AmethystCastCount = 3; // 패턴 2에서 자수정 줄을 융기시킬 횟수
    public int AmethystCount = 4; // 한 줄에 융기시킬 자수정 수
    public float AmethystStagger = 0.06f; // 보스에게 가까운 균열부터 차례로 생기는 간격
    public float AmethystWarning = 0.6f; // 균열이 생긴 뒤 자수정이 솟기까지의 게임 시간
    public float AmethystJitter = 0.6f; // 직선 경로에서 각 자수정을 무작위로 어긋나게 할 거리
    public Vector2 AmethystHitSize = new Vector2(1.6f, 1f); // 자수정이 솟는 순간의 피해 타원
    public float AmethystCastGap = 0.6f; // 한 줄이 모두 솟은 뒤 다음 시전까지의 게임 시간
    public float AmethystCooldown = 10f; // 패턴 2의 시작부터 다음 사용까지 필요한 게임 시간
    public int PillarCount = 4; // 패턴 3에서 떨어뜨릴 전기 기둥 수
    public float PillarTrack = 0.6f; // 기둥 하나가 예고B로 Player를 따라가는 게임 시간
    public float PillarFall = 0.1f; // 기둥이 하늘에서 떨어지는 게임 시간
    public float PillarWait = 0.5f; // 기둥이 떨어진 뒤 다음 기둥의 예고가 시작되기까지의 게임 시간
    public float PillarFuseDelay = 0.6f; // 마지막 기둥이 떨어진 뒤 폭발 예고가 시작되기까지의 게임 시간
    public float PillarFuse = 0.7f; // 모든 기둥이 깜빡이며 폭발을 예고하는 게임 시간
    public Vector2 PillarHitSize = new Vector2(1.5f, 0.9f); // 기둥이 떨어지는 자리의 피해 타원
    public Vector2 PillarBlastSize = new Vector2(4f, 2.5f); // 기둥 하나가 폭발할 때의 피해 타원
    public float PillarCooldown = 12f; // 패턴 3의 시작부터 다음 사용까지 필요한 게임 시간
    public int CrystalCount = 4; // 패턴 5에서 융기시킬 수정 수
    public Vector2 CrystalRadius = new Vector2(1.5f, 4.5f); // 수정을 놓을 Player 주변 고리의 안쪽·바깥쪽 반지름
    public int CrystalCandidates = 24; // 수정 하나를 놓을 때 서로의 거리를 비교할 후보 위치 수
    public float CrystalWarning = 0.6f; // 수정이 솟기 전 예고하는 게임 시간
    public float CrystalInterval = 0.5f; // 수정 사이의 융기 시작 간격
    public float CrystalHideDelay = 1f; // 사라진 뒤 수정 파괴를 시작하기까지의 게임 시간
    public float CrystalStrikeWarning = 0.6f; // 수정마다 내려찍기 범위를 예고하는 게임 시간
    public float CrystalHold = 0.5f; // 착지 뒤 세 번째 동작을 유지하는 게임 시간
    public Vector2 CrystalBlastSize = new Vector2(4f, 2.5f); // 수정을 부수는 착지의 피해 타원
    public int CrystalBulletCount = 8; // 수정에 착지할 때마다 고르게 나눈 방향으로 쏠 탄환 수
    public float CrystalBulletSpeed = 8f; // 착지에서 나가는 탄환의 초당 이동 거리
    public Vector2 CrystalLandingOffset = new Vector2(-0.46875f, 0.0625f); // 수정 위치에서 보스 착지 기준점까지의 거리
    public float CrystalCooldown = 14f; // 패턴 5의 시작부터 다음 사용까지 필요한 게임 시간
    public int TrackPortalCount = 10; // 패턴 6에서 차례로 여는 차원문 수
    public float TrackPortalInterval = 0.3f; // 차원문이 하나씩 생기는 간격
    public float TrackPortalWarning = 0.6f; // 차원문마다 확정한 경로를 예고하는 게임 시간
    public float TrackBulletSpeed = 30f; // 추적 차원문이 쏘는 황금 특수탄의 초당 이동 거리
    public float TrackPathWidth = 1.125f; // 추적 차원문 예고 경로의 폭이며 탄 몸체 지름과 같게 설정
    public float TrackRecover = 0.5f; // 마지막 발사 뒤 내려오기까지의 게임 시간
    public float TrackCooldown = 12f; // 패턴 6의 시작부터 다음 사용까지 필요한 게임 시간
    public int SlamCount = 4; // 패턴 7에서 내려찍는 횟수
    public float SlamVanish = 0.7f; // 준비 동작과 함께 사라지기까지의 게임 시간
    public float SlamHideDelay = 1f; // 사라진 뒤 첫 추적을 시작하기까지의 게임 시간
    public float SlamTrack = 0.7f; // 예고A가 Player를 따라가는 게임 시간
    public float SlamLock = 0.6f; // 위치를 확정하고 예고B를 재생하는 게임 시간
    public float SlamHold = 0.3f; // 착지 뒤 세 번째 동작과 금빛 강조를 유지하는 게임 시간
    public Vector2 SlamBlastSize = new Vector2(5f, 3f); // 내려찍기의 피해 타원
    public float SlamCooldown = 12f; // 패턴 7의 시작부터 다음 사용까지 필요한 게임 시간

    /// <summary>열림 → 유지 → 닫힘 순서로 재생하는 차원문 하나</summary>
    private class Rift
    {
        public SpriteClipPlayer Player; // 차원문 그림을 재생할 객체
        public float Age; // 열린 뒤 지난 게임 시간
        public bool Holding; // 열림이 끝나 유지 구간을 반복 중인지 여부
        public bool Oval; // 내려찍기용 타원 차원문인지 여부
    }

    /// <summary>지정한 자리에 차원문을 열기 시작</summary>
    private Rift OpenRift(Vector2 position, float angle, bool oval, int order)
    {
        var rift = new Rift { Player = CreatePlayer(oval ? "OvalPortal" : "Rift", position, order), Oval = oval };
        rift.Player.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        rift.Player.Play(Clip(oval ? "oval-open" : "rift-open-rift"));
        PlayRiftOpen();
        return rift;
    }

    /// <summary>열림이 끝난 차원문을 유지 반복으로 전환</summary>
    private void TickRift(Rift rift, float delta)
    {
        if (rift == null || rift.Player == null || rift.Holding) return;
        rift.Age += delta;
        if (rift.Age < Clip(rift.Oval ? "oval-open" : "rift-open-rift").GetLength()) return;
        rift.Holding = true;
        rift.Player.Play(Clip(rift.Oval ? "oval-hold" : "rift-hold-rift"));
    }

    /// <summary>차원문을 닫는 동작을 재생하고 끝나면 스스로 사라지게 함</summary>
    private void ReleaseRift(Rift rift)
    {
        if (rift == null || rift.Player == null) return;
        rift.Holding = true;
        rift.Player.DestroyOnFinish = true;
        rift.Player.Play(Clip(rift.Oval ? "oval-release" : "rift-release-rift"));
        PlayRiftClose();
    }

    /// <summary>빌려 쓴 예고 중 아직 화면에 남은 것이 있는지 확인</summary>
    private bool AnyWarningVisible()
    {
        for (int i = 0; i < warnings.Count; i++)
            if (warnings[i] != null && warnings[i].IsVisible) return true;
        return false;
    }

    /// <summary>구역 안의 점에서 주어진 방향으로 나아가 구역 경계에 닿는 거리</summary>
    private static float ExitDistance(Rect arena, Vector2 start, Vector2 direction)
    {
        float distance = float.PositiveInfinity;
        if (direction.x > 0f) distance = Mathf.Min(distance, (arena.xMax - start.x) / direction.x);
        else if (direction.x < 0f) distance = Mathf.Min(distance, (arena.xMin - start.x) / direction.x);
        if (direction.y > 0f) distance = Mathf.Min(distance, (arena.yMax - start.y) / direction.y);
        else if (direction.y < 0f) distance = Mathf.Min(distance, (arena.yMin - start.y) / direction.y);
        return float.IsInfinity(distance) || distance < 0f ? 0f : distance;
    }

    /// <summary>하늘에서 바닥으로 내려찍는 동안의 몸 높이를 진행하고 닿으면 true</summary>
    private bool TickFall(float fallElapsed)
    {
        float ratio = FallDuration > 0f ? Mathf.Clamp01(fallElapsed / FallDuration) : 1f;
        SetAltitude(Mathf.Lerp(FallHeight, 0f, ratio));
        return ratio >= 1f;
    }

    /// <summary>패턴 1: 세 위치로 순간이동하며 도착할 때마다 손끝 차원문에서 유도탄 한 발 발사</summary>
    private class TeleportVolleyPattern : BossPattern
    {
        private readonly Boss_Last boss; // 등록 때 연결한 자기 보스
        private int stop; // 지금 향하는 위치의 번호
        private int stage; // 0 준비, 1 소실, 2 이동 대기, 3 등장, 4 시전, 5 발사 뒤 회복
        private float stageStart; // 현재 단계가 시작된 패턴 시간
        private bool ignited; // 손끝 점화 효과를 이미 재생했는지 여부
        private Vector2 destination; // 이번에 나타날 위치
        private Vector2 portalPoint; // 손끝 앞에 여는 차원문의 위치
        private Rift rift; // 이번 위치에서 연 차원문

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public TeleportVolleyPattern(Boss_Last owner) : base("F-B12-01")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 이동할 위치 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.TeleportCooldown;
            return base.CanRun(owner) && boss.CanStart(owner, this, 1) && boss.TeleportOffsets != null && boss.TeleportOffsets.Length > 0;
        }

        /// <summary>첫 위치로 떠날 준비 동작 시작</summary>
        protected override void OnBegin()
        {
            stop = 0;
            rift = null;
            BeginStop();
        }

        /// <summary>준비 → 소실 → 등장 → 시전 → 발사를 위치마다 반복</summary>
        protected override void OnTick(float elapsed)
        {
            float time = Elapsed - stageStart;
            boss.TickRift(rift, elapsed);
            if (stage == 0)
            {
                // 준비 동작의 세 번째 장부터 손끝에서 주황빛이 짧게 빛남
                if (!ignited && time >= Frame * 2f)
                {
                    ignited = true;
                    boss.PlayHandFx(null, "p1-ignition-cast");
                }

                // 여섯 번째 장에서 가로선으로 흩어지기 시작하고 도착할 자리를 먼저 알림
                if (time < Frame * 5f) return;
                SetStage(1);
                boss.PlayHandFx(null, null);
                boss.PlayBody("vanish-ready6");
                boss.SpawnFx("p1-arrival-front", destination + new Vector2(0f, 1.2f), FrontFxOrder);
            }
            else if (stage == 1)
            {
                if (time < Frame * 3f) return;
                SetStage(2);
                boss.SetHidden(true);
            }
            else if (stage == 2)
            {
                // 사라진 뒤 정해진 간격이 지나면 다음 위치에서 가로선이 모이며 등장
                if (time < boss.TeleportGap) return;
                SetStage(3);
                boss.transform.position = new Vector3(destination.x, destination.y, boss.transform.position.z);
                boss.FaceTowards(boss.Target.transform.position);
                boss.SetHidden(false);
                boss.PlayBody("appear-ready8");
                portalPoint = destination + new Vector2((boss.Body.flipX ? -1f : 1f) * (HandLowered.x + 0.55f), HandLowered.y);
                rift = boss.OpenRift(portalPoint, 0f, false, FrontOrder + 2);
            }
            else if (stage == 3)
            {
                // 스미어인 첫 장을 한 장 분량만 보여 주고 곧바로 다음 장으로 넘어감
                if (time < Frame * 3f) return;
                SetStage(4);
                boss.PlayBody("body-attack-doing");
                boss.SpawnFx("homing-fire-open", portalPoint, FrontOrder + 3);
            }
            else if (stage == 4)
            {
                // 차원문이 다 열리는 순간 그 자리에서 유도탄 한 발 발사
                if (time < Mathf.Max(boss.Clip("rift-open-rift").GetLength() - Frame * 3f, boss.Clip("body-attack-doing").GetLength())) return;
                SetStage(5);
                FireHoming();
                boss.ReleaseRift(rift);
                rift = null;
                boss.PlayBody("body-attack-end");
            }
            else if (time >= boss.TeleportRecover)
            {
                // 동작이 끝나는 대로 다음 위치로 이동하고 마지막 위치에서는 패턴 종료
                stop++;
                if (stop >= boss.TeleportOffsets.Length)
                {
                    boss.ResetPresentation();
                    Complete();
                    return;
                }
                BeginStop();
            }
        }

        /// <summary>진행 중인 이동과 차원문을 지우고 현재 자리에서 다시 나타남</summary>
        protected override void OnCancel()
        {
            rift = null;
            boss.ResetPresentation();
        }

        /// <summary>다음 위치를 정하고 그 자리로 떠날 준비 동작 시작</summary>
        private void BeginStop()
        {
            Rect arena = boss.GetArena();
            destination = arena.center + boss.TeleportOffsets[stop];
            if (!boss.IsOpen(destination)) destination = arena.center;
            ignited = false;
            SetStage(0);
            boss.PlayBody("body-attack-ready");
        }

        /// <summary>단계와 그 시작 시간을 함께 기록</summary>
        private void SetStage(int value)
        {
            stage = value;
            stageStart = Elapsed;
        }

        /// <summary>차원문에서 맵 중앙을 향해 출발하는 유도탄을 만들고 추적 대상 연결</summary>
        private void FireHoming()
        {
            Rect arena = boss.GetArena();
            Vector2 direction = arena.center - portalPoint;
            if (direction.sqrMagnitude < 0.01f) direction = (Vector2)boss.Target.transform.position - portalPoint;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;
            float speed = Mathf.Max(0.1f, boss.Target.SP * boss.HomingSpeedRatio);
            var bullet = boss.Fire(boss.HomingPrefab, portalPoint, direction, speed, boss.HomingLifetime, boss.PatternDamage) as HomingProjectile;
            if (bullet == null) return;
            bullet.BeginHoming(boss.Target.transform, boss.GetGrid(), arena.center);
            boss.SpawnFx("homing-fire-release", portalPoint, FrontOrder + 3);
        }
    }

    /// <summary>패턴 2: 가까운 지정 위치로 대시한 뒤 Player를 지나는 직선 위에 자수정 네 개를 세 번 융기</summary>
    private class AmethystLinePattern : BossPattern
    {
        /// <summary>균열에서 솟았다가 스스로 사라지는 자수정 하나</summary>
        private class Amethyst
        {
            public Vector2 Position; // 솟을 자리
            public float StartAt; // 균열이 생기는 패턴 시간
            public int Variant; // 세 가지 모양 중 하나
            public SpriteClipPlayer Ground; // 균열과 바닥 표시
            public SpriteClipPlayer Body; // 자수정과 파편
            public bool Erupted; // 솟는 순간의 피해를 이미 처리했는지 여부
        }

        private readonly Boss_Last boss; // 등록 때 연결한 자기 보스
        private readonly List<Amethyst> crystals = new List<Amethyst>(); // 이번 시전에서 만든 자수정
        private Vector2 castPoint; // 대시해서 도착할 지정 위치
        private int stage; // 0 대시, 1 준비·시전 동작, 2 융기 진행, 3 다음 시전 대기
        private float stageStart; // 현재 단계가 시작된 패턴 시간
        private int cast; // 지금까지 마친 시전 횟수
        private float afterimageTimer; // 다음 잔상까지 남은 시간
        private int attackId; // 한 줄의 자수정이 같은 Player를 한 번만 맞히도록 묶을 번호

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public AmethystLinePattern(Boss_Last owner) : base("F-B12-02")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 지정 위치 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.AmethystCooldown;
            return base.CanRun(owner) && boss.CanStart(owner, this, 2) && boss.CastOffsets != null && boss.CastOffsets.Length > 0;
        }

        /// <summary>지금 위치에서 가장 가까운 지정 위치로 느린 대시 시작</summary>
        protected override void OnBegin()
        {
            Rect arena = boss.GetArena();
            Vector2 position = boss.transform.position;
            float best = float.PositiveInfinity;
            castPoint = position;
            foreach (Vector2 offset in boss.CastOffsets)
            {
                Vector2 point = arena.center + offset;
                float distance = (point - position).sqrMagnitude;
                if (distance < best && boss.IsOpen(point))
                {
                    best = distance;
                    castPoint = point;
                }
            }
            cast = 0;
            crystals.Clear();
            afterimageTimer = 0f;
            SetStage(0);
            boss.PlayBody("body-idle");
            boss.FaceTowards(castPoint);
        }

        /// <summary>대시 → 시전 → 융기 → 대기를 정해진 횟수만큼 반복</summary>
        protected override void OnTick(float elapsed)
        {
            float time = Elapsed - stageStart;
            TickCrystals();
            if (stage == 0)
            {
                // 잔상을 남기며 지정 위치까지 일정 속도로 이동
                Vector2 position = Vector2.MoveTowards(boss.transform.position, castPoint, boss.DashSpeed * elapsed);
                boss.transform.position = new Vector3(position.x, position.y, boss.transform.position.z);
                afterimageTimer -= elapsed;
                if (afterimageTimer <= 0f)
                {
                    afterimageTimer = boss.AfterimageInterval;
                    SpriteAfterimage.Spawn(boss.Body, boss.AfterimageColor, 0.3f, boss.Body.sortingOrder - 1, boss.SpriteMaterial, boss.transform.parent);
                }
                if ((position - castPoint).sqrMagnitude > 0.0001f) return;
                BeginCast();
            }
            else if (stage == 1)
            {
                // 준비 동작이 끝나면 스미어 한 장으로 시작하는 시전 동작으로 이어짐
                boss.FaceTowards(boss.Target.transform.position);
                float ready = boss.Clip("body-attack-ready").GetLength();
                if (time >= ready && boss.bodyClip == boss.Clip("body-attack-ready")) boss.PlayBody("body-attack-doing");
                if (time < ready + boss.Clip("body-attack-doing").GetLength()) return;
                SetStage(2);
                SpawnLine();
            }
            else if (stage == 2)
            {
                // 네 개가 모두 솟으면 손끝 효과를 끄고 마무리 자세
                foreach (Amethyst crystal in crystals)
                    if (!crystal.Erupted) return;
                SetStage(3);
                boss.PlayHandFx(null, null);
                boss.PlayBody("body-attack-end");
                cast++;
            }
            else if (cast < boss.AmethystCastCount)
            {
                if (time >= boss.AmethystCastGap) BeginCast();
            }
            else if (crystals.Count == 0)
            {
                // 마지막 줄의 자수정이 모두 스스로 사라진 뒤 종료
                boss.ResetPresentation();
                Complete();
            }
        }

        /// <summary>남은 균열과 자수정을 지우고 기본 자세로 복원</summary>
        protected override void OnCancel()
        {
            crystals.Clear();
            boss.ResetPresentation();
        }

        /// <summary>준비 동작과 손끝의 갈색·보라 효과 시작</summary>
        private void BeginCast()
        {
            SetStage(1);
            boss.FaceTowards(boss.Target.transform.position);
            boss.PlayBody("body-attack-ready");
            boss.PlayHandFx("p2-cast-back", "p2-cast-cast");
            boss.PlaySound(boss.AmethystCastSound);
        }

        /// <summary>단계와 그 시작 시간을 함께 기록</summary>
        private void SetStage(int value)
        {
            stage = value;
            stageStart = Elapsed;
        }

        /// <summary>Player가 경로의 2/3 지점에 오는 직선을 넷으로 나눠 조금씩 어긋난 자리에 균열 예약</summary>
        private void SpawnLine()
        {
            Vector2 origin = boss.transform.position;
            Vector2 toPlayer = (Vector2)boss.Target.transform.position - origin;
            if (toPlayer.sqrMagnitude < 1f) toPlayer = (boss.Body.flipX ? Vector2.left : Vector2.right) * 1f;
            Vector2 end = origin + toPlayer * 1.5f;
            attackId = DamageInstance.NextAttackId();
            for (int i = 0; i < boss.AmethystCount; i++)
            {
                Vector2 point = Vector2.Lerp(origin, end, (i + 1f) / boss.AmethystCount) + Random.insideUnitCircle * boss.AmethystJitter;
                if (!boss.IsOpen(point)) continue;
                crystals.Add(new Amethyst { Position = point, StartAt = Elapsed + i * boss.AmethystStagger, Variant = Random.Range(1, 4) });
            }
        }

        /// <summary>예약한 시간에 균열을 만들고 솟는 순간 피해를 주며 끝난 자수정은 목록에서 제거</summary>
        private void TickCrystals()
        {
            for (int i = crystals.Count - 1; i >= 0; i--)
            {
                Amethyst crystal = crystals[i];
                if (crystal.Ground == null && crystal.Body == null && !crystal.Erupted)
                {
                    if (Elapsed < crystal.StartAt) continue;

                    // 균열은 바닥에, 자수정은 밑동 높이의 공통 정렬로 그림
                    crystal.Ground = boss.CreatePlayer("AmethystGround", crystal.Position, GroundFxOrder);
                    crystal.Ground.DestroyOnFinish = true;
                    crystal.Ground.Play(boss.Clip("amethyst-" + crystal.Variant + "-ground"));
                    crystal.Body = boss.CreatePlayer("AmethystBody", crystal.Position, BackOrder);
                    crystal.Body.DestroyOnFinish = true;
                    crystal.Body.gameObject.AddComponent<DepthSorter>();
                    crystal.Body.Play(boss.Clip("amethyst-" + crystal.Variant + "-body"));
                }
                if (!crystal.Erupted && Elapsed >= crystal.StartAt + boss.AmethystWarning)
                {
                    crystal.Erupted = true;
                    boss.DamageEllipse(crystal.Position, boss.AmethystHitSize, boss.PatternDamage, attackId);
                    boss.PlaySound(boss.AmethystRiseSound, 0.8f);
                }
                if (crystal.Body == null && crystal.Erupted) crystals.RemoveAt(i);
            }
        }
    }

    /// <summary>패턴 3: 공중에서 전기 기둥 네 개를 차례로 떨어뜨린 뒤 한꺼번에 터뜨리며 모두 Slash로 지우면 그로기</summary>
    private class ShockPillarPattern : BossPattern
    {
        /// <summary>떨어져 서 있다가 폭발하거나 Slash에 지워지는 기둥 하나</summary>
        private class Pillar
        {
            public Vector2 Position; // 떨어진 자리
            public BossStructure Structure; // Slash로만 지울 수 있는 판정
            public SpriteRenderer Renderer; // 서 있는 기둥 그림
            public SpriteClipPlayer Drop; // 낙하 동작
            public bool Landed; // 바닥에 닿았는지 여부
            public bool Slashed; // Slash로 지워졌는지 여부
        }

        private readonly Boss_Last boss; // 등록 때 연결한 자기 보스
        private readonly List<Pillar> pillars = new List<Pillar>(); // 이번 실행에서 떨어뜨린 기둥
        private int index; // 지금 떨어뜨리는 기둥의 번호
        private int stage; // 0 추적 예고, 1 낙하, 2 다음 기둥 대기, 3 폭발 전 대기, 4 폭발 예고, 5 마무리
        private float stageStart; // 현재 단계가 시작된 패턴 시간
        private Vector2 aim; // 예고가 따라가다 고정한 낙하 지점
        private Rift portal; // 낙하 지점 위 하늘에 여는 차원문
        private int attackId; // 낙하와 폭발 피해를 구분할 번호

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public ShockPillarPattern(Boss_Last owner) : base("F-B12-03")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 유효한 기둥 설정 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.PillarCooldown;
            return base.CanRun(owner) && boss.CanStart(owner, this, 3) && boss.PillarCount > 0;
        }

        /// <summary>하늘로 손을 든 채 떠오르고 첫 기둥의 추적 예고 시작</summary>
        protected override void OnBegin()
        {
            pillars.Clear();
            index = 0;
            portal = null;
            boss.PlayBody("body-wave-begin");
            boss.PlayHandFx("p3-lightning-back", "p3-lightning-cast");
            boss.SetAirborne(true);
            BeginTrack();
        }

        /// <summary>기둥마다 추적 → 낙하 → 대기를 반복한 뒤 폭발 예고와 폭발 진행</summary>
        protected override void OnTick(float elapsed)
        {
            float time = Elapsed - stageStart;
            boss.TickRift(portal, elapsed);
            if (CheckSlashed()) return;
            if (stage == 0)
            {
                // 예고B와 하늘의 차원문이 Player를 따라가다 끝나는 순간의 위치에 고정
                aim = boss.Target.transform.position;
                boss.GetWarning(0).ShowEllipse(aim, boss.PillarHitSize);
                if (portal != null && portal.Player != null) portal.Player.transform.position = aim + new Vector2(0f, 4.2f);
                if (time < boss.PillarTrack) return;
                SetStage(1);
                BeginDrop();
            }
            else if (stage == 1)
            {
                if (time < boss.PillarFall) return;
                SetStage(2);
                Land();
            }
            else if (stage == 2)
            {
                // 낙하 동작이 끝나면 서 있는 기둥으로 바꾸고 다음 기둥 준비
                Pillar pillar = pillars[pillars.Count - 1];
                if (pillar.Drop != null && time >= boss.Clip("pillar-drop-body").GetLength() - boss.PillarFall) Stand(pillar);
                bool last = index >= boss.PillarCount;
                if (time < (last ? boss.PillarFuseDelay : boss.PillarWait)) return;
                if (pillar.Drop != null) Stand(pillar);
                if (!last)
                {
                    BeginTrack();
                    return;
                }

                // 남은 기둥마다 폭발 범위를 예고하며 깜빡임
                SetStage(4);
                for (int i = 0; i < pillars.Count; i++)
                {
                    if (pillars[i].Slashed) continue;
                    AreaWarningVfx warning = boss.GetWarning(i + 1);
                    warning.ShowEllipse(pillars[i].Position, boss.PillarBlastSize);
                    warning.BeginImminent(boss.PillarFuse);
                }
            }
            else if (stage == 4)
            {
                // 폭발이 가까울수록 눈에 띄도록 일정 간격으로 기둥을 숨겼다 보임
                bool visible = Mathf.FloorToInt(time / 0.07f) % 2 == 0;
                foreach (Pillar pillar in pillars)
                    if (!pillar.Slashed && pillar.Renderer != null) pillar.Renderer.enabled = visible;
                if (time < boss.PillarFuse) return;
                SetStage(5);
                Detonate();
            }
            else if (stage == 5 && time >= 0.5f)
            {
                boss.ResetPresentation();
                Complete();
            }
        }

        /// <summary>남은 기둥과 예고를 지우고 지상으로 복귀</summary>
        protected override void OnCancel()
        {
            pillars.Clear();
            portal = null;
            boss.ResetPresentation();
        }

        /// <summary>단계와 그 시작 시간을 함께 기록</summary>
        private void SetStage(int value)
        {
            stage = value;
            stageStart = Elapsed;
        }

        /// <summary>다음 기둥의 예고B와 하늘의 차원문 시작</summary>
        private void BeginTrack()
        {
            SetStage(0);
            aim = boss.Target.transform.position;
            AreaWarningVfx warning = boss.GetWarning(0);
            warning.ShowEllipse(aim, boss.PillarHitSize);
            warning.BeginImminent(boss.PillarTrack);
            portal = boss.OpenRift(aim + new Vector2(0f, 4.2f), 0f, true, FrontFxOrder);
        }

        /// <summary>고정한 지점에 기둥의 낙하 동작과 판정 객체 생성</summary>
        private void BeginDrop()
        {
            var pillar = new Pillar { Position = aim };
            pillar.Structure = boss.CreateStructure("ShockPillar", aim, new Vector2(0.7f, 1.4f), new Vector2(0f, 0.7f), 100 + index);
            pillar.Renderer = pillar.Structure.gameObject.AddComponent<SpriteRenderer>();
            pillar.Renderer.sharedMaterial = boss.SpriteMaterial;
            pillar.Renderer.sprite = boss.PillarSprite;
            pillar.Renderer.enabled = false;
            pillar.Drop = boss.CreatePlayer("Drop", aim, FrontOrder, pillar.Structure.transform);
            pillar.Drop.Play(boss.Clip("pillar-drop-body"));
            SpriteClipPlayer shadow = boss.CreatePlayer("DropShadow", aim, ShadowOrder, pillar.Structure.transform);
            DepthSorter.DrawOnGround(shadow.gameObject, ShadowOrder);
            shadow.DestroyOnFinish = true;
            shadow.Play(boss.Clip("pillar-drop-shadow"));
            pillars.Add(pillar);
            boss.ReleaseRift(portal);
            portal = null;
            index++;
        }

        /// <summary>바닥에 닿는 순간 피해와 흙먼지를 처리하고 마지막 기둥이면 모두 금빛으로 강조</summary>
        private void Land()
        {
            Pillar pillar = pillars[pillars.Count - 1];
            pillar.Landed = true;
            boss.GetWarning(0).Hide();
            attackId = DamageInstance.NextAttackId();
            boss.DamageEllipse(pillar.Position, boss.PillarHitSize, boss.PatternDamage, attackId);
            boss.SpawnFx("pillar-land-ground", pillar.Position, GroundFxOrder);
            boss.SpawnFx("pillar-land-back", pillar.Position, BackOrder - 1);
            boss.SpawnFx("pillar-land-front", pillar.Position, FrontOrder + 1);
            pillar.Structure.Reset(1f);
            if (index < boss.PillarCount) return;
            boss.PlaySound(boss.CounterSound);
            foreach (Pillar other in pillars)
                if (!other.Slashed) other.Renderer.sharedMaterial = boss.GoldMaterial;
        }

        /// <summary>낙하 동작을 지우고 서 있는 기둥과 그림자 표시</summary>
        private void Stand(Pillar pillar)
        {
            if (pillar.Drop != null) Destroy(pillar.Drop.gameObject);
            pillar.Drop = null;
            if (pillar.Slashed) return;
            pillar.Renderer.enabled = true;
            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(pillar.Structure.transform, false);
            shadow.sharedMaterial = boss.SpriteMaterial;
            shadow.sprite = boss.PillarShadowSprite;
            DepthSorter.DrawOnGround(shadow.gameObject, ShadowOrder);
        }

        /// <summary>Slash로 지워진 기둥을 정리하고 네 개 모두 지워졌으면 보스를 그로기에 빠뜨림</summary>
        private bool CheckSlashed()
        {
            int removed = 0;
            foreach (Pillar pillar in pillars)
            {
                if (!pillar.Slashed && pillar.Landed && !pillar.Structure.IsActive && stage < 5)
                {
                    // 지워진 자리에 접점 효과를 남기고 그림과 예고를 함께 지움
                    pillar.Slashed = true;
                    boss.SpawnFx("p1-arrival-front", pillar.Position + new Vector2(0f, 0.7f), FrontFxOrder);
                    pillar.Structure.gameObject.SetActive(false);
                    int warning = pillars.IndexOf(pillar) + 1;
                    if (stage == 4) boss.GetWarning(warning).Hide();
                }
                if (pillar.Slashed) removed++;
            }

            if (removed < boss.PillarCount) return false;

            // 전기 충격이 보스에게 되돌아가 그로기에 빠짐
            Vector2 center = boss.GetBodyCenter();
            pillars.Clear();
            boss.SpawnFx("p3-detonation-front", center, FrontFxOrder);
            boss.SpawnFx("counter-gold-front", center, FrontFxOrder + 1);
            boss.EnterGroggy();
            return true;
        }

        /// <summary>남은 기둥을 한꺼번에 터뜨려 각 범위에 피해 전달</summary>
        private void Detonate()
        {
            attackId = DamageInstance.NextAttackId();
            for (int i = 0; i < pillars.Count; i++)
            {
                Pillar pillar = pillars[i];
                boss.GetWarning(i + 1).Hide();
                if (pillar.Slashed) continue;
                boss.SpawnFx("p3-detonation-back", pillar.Position + new Vector2(0f, 0.3f), GroundFxOrder);
                boss.SpawnFx("p3-detonation-front", pillar.Position + new Vector2(0f, 0.7f), FrontFxOrder);
                boss.DamageEllipse(pillar.Position, boss.PillarBlastSize, boss.HeavyDamage, attackId);
                pillar.Structure.gameObject.SetActive(false);
            }
            boss.PlayHandFx(null, null);
            boss.SetAirborne(false);
        }
    }

    /// <summary>패턴 5: Player 주변에 서로 떨어뜨린 수정 네 개를 융기시키고 사라진 뒤 융기 순서대로 내려찍어 부수며 착지마다 여덟 방향 탄환을 쏘고 마지막 수정에서 카운터 허용</summary>
    private class CrystalDescentPattern : BossPattern
    {
        /// <summary>솟아 있다가 보스의 착지에 부서지는 수정 하나</summary>
        private class Crystal
        {
            public Vector2 Position; // 솟은 자리
            public SpriteClipPlayer Body; // 수정 그림
            public SpriteRenderer Shadow; // 바닥 그림자
            public BossStructure Structure; // 마지막 수정만 열어 둘 Slash 판정
            public bool Risen; // 솟았는지 여부
            public bool Broken; // 부서졌는지 여부
        }

        private readonly Boss_Last boss; // 등록 때 연결한 자기 보스
        private readonly List<Crystal> crystals = new List<Crystal>(); // 융기 순서대로 보관한 수정
        private readonly List<Vector2> points = new List<Vector2>(); // 뭉치지 않게 비교할 이미 고른 수정 위치
        private int stage; // 0 융기, 1 소실, 2 숨은 대기, 3 범위 예고, 4 낙하, 5 착지 유지, 6 다시 소실, 7 마무리
        private float stageStart; // 현재 단계가 시작된 패턴 시간
        private int raised; // 예고를 시작한 수정 수
        private int target; // 지금 부수러 가는 수정의 번호
        private int attackId; // 착지 피해를 구분할 번호
        private bool countered; // 카운터로 끝났는지 여부

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public CrystalDescentPattern(Boss_Last owner) : base("F-B12-05")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 유효한 수정 설정 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.CrystalCooldown;
            return base.CanRun(owner) && boss.CanStart(owner, this, 5) && boss.CrystalCount > 0;
        }

        /// <summary>준비 동작을 시작하며 수정 위치는 각 예고가 시작되는 순간에 정함</summary>
        protected override void OnBegin()
        {
            crystals.Clear();
            points.Clear();
            raised = 0;
            target = 0;
            countered = false;
            Vector2 player = boss.Target.transform.position;
            for (int i = 0; i < boss.CrystalCount; i++) crystals.Add(new Crystal());
            SetStage(0);
            boss.FaceTowards(player);
            boss.PlayBody("body-attack-ready");
        }

        /// <summary>융기 → 소실 → 수정마다 예고·낙하·착지를 진행</summary>
        protected override void OnTick(float elapsed)
        {
            float time = Elapsed - stageStart;
            Sort();

            // 금빛 수정이 지워져 그로기로 끝났으면 남은 단계를 진행하지 않음
            if (!IsRunning) return;
            if (stage == 0)
            {
                // 정해진 간격으로 위치를 골라 예고를 시작하고 예고가 끝난 수정부터 차례로 솟음
                while (raised < crystals.Count && time >= raised * boss.CrystalInterval)
                {
                    // 그 순간의 Player 주변 고리 안에서 먼저 놓은 수정들과 가장 멀리 떨어진 후보를 고름
                    Vector2 point = boss.GetSpreadPoint(boss.Target.transform.position, boss.CrystalRadius.x, boss.CrystalRadius.y, points, boss.CrystalCandidates);
                    points.Add(point);
                    crystals[raised].Position = point;
                    AreaWarningVfx warning = boss.GetWarning(raised);
                    warning.ShowEllipse(crystals[raised].Position, boss.PillarHitSize);
                    warning.BeginImminent(boss.CrystalWarning);
                    raised++;
                }
                for (int i = 0; i < raised; i++)
                    if (!crystals[i].Risen && time >= i * boss.CrystalInterval + boss.CrystalWarning) Raise(i);
                float done = (crystals.Count - 1) * boss.CrystalInterval + boss.CrystalWarning + boss.Clip("crystal-rise").GetLength();
                if (time < done) return;
                SetStage(1);
                boss.PlayBody("vanish-ready8");
            }
            else if (stage == 1)
            {
                if (time < Frame * 3f) return;
                SetStage(2);
                boss.SetHidden(true);
            }
            else if (stage == 2)
            {
                if (time >= boss.CrystalHideDelay) BeginStrike();
            }
            else if (stage == 3)
            {
                if (time < boss.CrystalStrikeWarning) return;

                // 수정 위 하늘에서 나타나 곧바로 내려찍음
                SetStage(4);
                Crystal crystal = crystals[target];
                Vector2 foot = crystal.Position + boss.CrystalLandingOffset;
                boss.transform.position = new Vector3(foot.x, foot.y, boss.transform.position.z);
                boss.Body.flipX = false;
                boss.SetAltitude(boss.FallHeight);
                boss.SetHidden(false);
                boss.BodyHitbox.SetEnabled(false);
                boss.PlayBody("body-wave-fall");
            }
            else if (stage == 4)
            {
                if (!boss.TickFall(time)) return;
                SetStage(5);
                Land();
            }
            else if (stage == 5)
            {
                bool last = target >= crystals.Count - 1;
                float landing = boss.Clip("body-landing-in").GetLength();
                if (last)
                {
                    // 마지막 착지는 세 번째 동작에서 멈추지 않고 끝 동작까지 이어짐
                    if (time < landing) return;
                    SetStage(7);
                    boss.SetCounter(false);
                    boss.SetBodyGold(false);
                    boss.PlayBody("body-landing-out");
                    return;
                }
                if (time < landing + boss.CrystalHold) return;
                SetStage(6);
                boss.PlayBody("vanish-landing3");
            }
            else if (stage == 6)
            {
                if (time < Frame * 3f) return;
                boss.SetHidden(true);
                target++;
                BeginStrike();
            }
            else if (stage == 7 && time >= boss.Clip("body-landing-out").GetLength() && !boss.AnyWarningVisible())
            {
                boss.ResetPresentation();
                Complete();
            }
        }

        /// <summary>남은 수정과 예고를 지우고 기본 자세로 복원</summary>
        protected override void OnCancel()
        {
            if (!countered) crystals.Clear();
            boss.ResetPresentation();
        }

        /// <summary>단계와 그 시작 시간을 함께 기록</summary>
        private void SetStage(int value)
        {
            stage = value;
            stageStart = Elapsed;
        }

        /// <summary>예고가 끝난 자리에서 수정이 솟고 그 순간 겹친 Player에게 피해 전달</summary>
        private void Raise(int index)
        {
            Crystal crystal = crystals[index];
            crystal.Risen = true;
            boss.GetWarning(index).Hide();
            crystal.Structure = boss.CreateStructure("Crystal", crystal.Position, new Vector2(0.9f, 0.9f), new Vector2(0f, 0.4f), 200 + index);
            crystal.Body = boss.CreatePlayer("Body", crystal.Position, BackOrder, crystal.Structure.transform);
            crystal.Body.Play(boss.Clip("crystal-rise"));
            crystal.Shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            crystal.Shadow.transform.SetParent(crystal.Structure.transform, false);
            crystal.Shadow.sharedMaterial = boss.SpriteMaterial;
            crystal.Shadow.sprite = boss.CrystalShadowSprite;
            DepthSorter.DrawOnGround(crystal.Shadow.gameObject, ShadowOrder);
            boss.SpawnFx("p5-eruption-back", crystal.Position, GroundFxOrder);
            boss.DamageEllipse(crystal.Position, boss.PillarHitSize, boss.PatternDamage, DamageInstance.NextAttackId());
        }

        /// <summary>다음 수정의 내려찍기 범위를 예고하고 마지막 수정이면 금빛 강조와 카운터 허용</summary>
        private void BeginStrike()
        {
            SetStage(3);
            Crystal crystal = crystals[target];
            AreaWarningVfx warning = boss.GetWarning(target);
            warning.ShowEllipse(crystal.Position, boss.CrystalBlastSize);
            warning.BeginImminent(boss.CrystalStrikeWarning);
            if (target < crystals.Count - 1) return;
            crystal.Body.GetComponent<SpriteRenderer>().sharedMaterial = boss.GoldMaterial;
            crystal.Structure.Reset(1f);
        }

        /// <summary>착지 순간 범위 피해와 수정 파편·충격파를 처리하고 여덟 방향으로 탄환을 쏘며 마지막 착지에서는 카운터 허용</summary>
        private void Land()
        {
            Crystal crystal = crystals[target];
            bool last = target >= crystals.Count - 1;
            boss.BodyHitbox.SetEnabled(true);
            boss.GetWarning(target).Hide();
            attackId = DamageInstance.NextAttackId();
            boss.DamageEllipse(crystal.Position, boss.CrystalBlastSize, boss.HeavyDamage, attackId);
            Vector2 foot = boss.transform.position;
            boss.SpawnFx("p5-impact-ground", foot, GroundFxOrder);
            boss.SpawnFx("p5-impact-stamp", foot, GroundFxOrder + 1);
            boss.SpawnFx("crystal-explosion", crystal.Position, FrontFxOrder);
            ImpactDistortionFx.Play(crystal.Position);
            boss.PlaySound(boss.LandSound);
            boss.PlaySound(boss.CrystalBurstSound);
            Break(crystal);
            boss.PlayBody("body-landing-in");

            // 수정이 부서진 자리에서 고르게 나눈 여덟 방향으로 탄환 발사
            for (int i = 0; i < boss.CrystalBulletCount; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, 360f * i / boss.CrystalBulletCount) * Vector2.right;
                boss.Fire(boss.NormalPrefab, crystal.Position, direction, boss.CrystalBulletSpeed, 8f, boss.PatternDamage);
            }
            if (!last) return;

            // 마지막 착지는 금빛으로 강조한 채 Slash 카운터를 받음
            boss.SetBodyGold(true);
            boss.SetCounter(true, Counter);
        }

        /// <summary>수정 하나를 파편으로 바꾸고 판정과 그림 제거</summary>
        private void Break(Crystal crystal)
        {
            if (crystal.Broken || !crystal.Risen) return;
            crystal.Broken = true;
            boss.SpawnFx("crystal-shatter", crystal.Position, FrontFxOrder + 1);
            if (crystal.Structure != null) crystal.Structure.gameObject.SetActive(false);
        }

        /// <summary>카운터 성공: 남은 수정을 폭발 없이 모두 부수고 보스를 착지 자리에 드러냄</summary>
        private void Counter()
        {
            countered = true;
            foreach (Crystal crystal in crystals) Break(crystal);
            crystals.Clear();
        }

        /// <summary>솟는 동작이 끝난 수정을 반복 동작으로 바꾸고 금빛 수정이 Slash에 지워졌는지 확인</summary>
        private void Sort()
        {
            for (int i = 0; i < crystals.Count; i++)
            {
                Crystal crystal = crystals[i];
                if (!crystal.Risen || crystal.Broken) continue;

                // 솟는 동작이 끝나면 반복 동작으로 전환
                SpriteClip rise = boss.Clip("crystal-rise");
                if (crystal.Body.Clip == rise && (!crystal.Body.IsPlaying || crystal.Body.Elapsed >= rise.GetLength() - 0.017f))
                    crystal.Body.Play(boss.Clip("crystal-idle"));
            }

            // 예고 중 금빛 수정을 Slash로 지우면 보스가 그 자리에 떨어지지 못하고 그로기에 빠짐
            if (stage != 3 || target != crystals.Count - 1 || crystals.Count == 0) return;
            Crystal lastCrystal = crystals[target];
            if (lastCrystal.Structure == null || lastCrystal.Structure.IsActive) return;
            Vector2 foot = lastCrystal.Position + boss.CrystalLandingOffset;
            Vector2 point = lastCrystal.Position + new Vector2(0f, 0.5f);
            boss.transform.position = new Vector3(foot.x, foot.y, boss.transform.position.z);
            Counter();
            boss.SpawnFx("counter-gold-front", point, FrontFxOrder);
            boss.EnterGroggy();
        }
    }

    /// <summary>패턴 6: 공중에서 무작위 위치에 차원문을 차례로 열고 Player를 향해 황금 특수탄을 한 발씩 발사</summary>
    private class TrackingPortalPattern : BossPattern
    {
        private readonly Boss_Last boss; // 등록 때 연결한 자기 보스
        private readonly Rift[] rifts = new Rift[32]; // 차례로 연 차원문
        private readonly Vector2[] origins = new Vector2[32]; // 차원문의 위치
        private readonly Vector2[] aims = new Vector2[32]; // 차원문이 생긴 직후 확정한 조준 지점
        private readonly bool[] fired = new bool[32]; // 차원문마다 발사를 마쳤는지 여부
        private readonly List<Vector2> points = new List<Vector2>(); // 서로 떨어뜨려 고른 차원문 위치
        private Rect arena; // 이번 실행에서 쓸 전투 구역
        private int opened; // 지금까지 연 차원문 수
        private float finishedAt; // 마지막 발사를 마친 패턴 시간

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public TrackingPortalPattern(Boss_Last owner) : base("F-B12-06")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 유효한 차원문 설정 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.TrackCooldown;
            return base.CanRun(owner) && boss.CanStart(owner, this, 6) && boss.TrackPortalCount > 0 && boss.TrackPortalCount <= rifts.Length
                && boss.TrackPortalInterval > 0f && boss.TrackBulletSpeed > 0f;
        }

        /// <summary>하늘로 손을 든 채 떠오르고 하늘색 손 효과 시작</summary>
        protected override void OnBegin()
        {
            arena = boss.GetArena();
            opened = 0;
            finishedAt = float.PositiveInfinity;
            points.Clear();
            for (int i = 0; i < rifts.Length; i++)
            {
                rifts[i] = null;
                fired[i] = false;
            }
            boss.PlayBody("body-wave-begin");
            boss.PlayHandFx("p6-sky-back", "p6-sky-cast");
            boss.SetAirborne(true);
        }

        /// <summary>정해진 간격으로 차원문을 열고 각자의 예고가 끝나면 발사</summary>
        protected override void OnTick(float elapsed)
        {
            // 한 프레임에 하나씩 열어 지연된 프레임에서도 순서와 간격 유지
            if (opened < boss.TrackPortalCount && Elapsed + 0.00001f >= opened * boss.TrackPortalInterval) Open();
            Vector2 player = boss.Target.transform.position;
            for (int i = 0; i < opened; i++)
            {
                boss.TickRift(rifts[i], elapsed);
                if (fired[i]) continue;

                // 차원문은 제자리에서 Player 쪽을 실시간으로 바라봄
                if (rifts[i] != null && rifts[i].Player != null)
                {
                    Vector2 look = player - origins[i];
                    if (look.sqrMagnitude > 0.01f) rifts[i].Player.transform.rotation = Quaternion.FromToRotation(Vector3.right, look);
                }
                if (Elapsed < i * boss.TrackPortalInterval + boss.TrackPortalWarning) continue;
                FireFrom(i);
                if (i == boss.TrackPortalCount - 1) finishedAt = Elapsed;
            }
            if (Elapsed < finishedAt + boss.TrackRecover) return;
            if (boss.AnyWarningVisible() && Elapsed < finishedAt + boss.TrackRecover + 2f) return;
            boss.ResetPresentation();
            Complete();
        }

        /// <summary>남은 차원문과 예고를 지우고 지상으로 복귀하며 이미 쏜 탄환은 유지</summary>
        protected override void OnCancel()
        {
            boss.ResetPresentation();
        }

        /// <summary>무작위 위치에 차원문을 열고 그 순간의 Player 위치로 경로를 확정해 예고B 시작</summary>
        private void Open()
        {
            int index = opened++;
            Vector2 player = boss.Target.transform.position;
            Vector2 origin = boss.GetRandomPoint(0.8f, player, 3f, points, 1.5f);
            points.Add(origin);
            origins[index] = origin;
            aims[index] = player;
            Vector2 direction = player - origin;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;
            direction.Normalize();
            rifts[index] = boss.OpenRift(origin, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, false, FrontFxOrder);
            AreaWarningVfx warning = boss.GetWarning(index);
            warning.ShowOpenPath(origin, origin + direction * Mathf.Max(1f, ExitDistance(arena, origin, direction)), boss.TrackPathWidth);
            warning.BeginImminent(boss.TrackPortalWarning);
        }

        /// <summary>확정해 둔 방향으로 황금 특수탄을 쏘고 경로를 예고C로 전환</summary>
        private void FireFrom(int index)
        {
            fired[index] = true;
            Vector2 direction = aims[index] - origins[index];
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;
            Projectile bullet = boss.Fire(boss.GoldFastPrefab, origins[index], direction, boss.TrackBulletSpeed, 0f, boss.PatternDamage);
            if (bullet != null) boss.GetWarning(index).BeginDanger(bullet);
            else boss.GetWarning(index).Hide();
            boss.ReleaseRift(rifts[index]);
            rifts[index] = null;
        }
    }

    /// <summary>패턴 7: 사라진 뒤 Player 위 하늘의 차원문에서 네 번 내려찍으며 착지한 동안 Slash에 맞으면 그로기</summary>
    private class PortalSlamPattern : BossPattern
    {
        private readonly Boss_Last boss; // 등록 때 연결한 자기 보스
        private int stage; // 0 준비, 1 소실, 2 숨은 대기, 3 추적, 4 확정, 5 낙하, 6 착지 유지, 7 다시 소실, 8 마무리
        private float stageStart; // 현재 단계가 시작된 패턴 시간
        private int slam; // 지금까지 마친 내려찍기 횟수
        private Vector2 aim; // 예고가 따라가다 고정한 착지 지점
        private Rift portal; // 착지 지점 위 하늘에 여는 차원문
        private int attackId; // 착지 피해를 구분할 번호

        /// <summary>패턴 식별자와 자기 보스 연결</summary>
        public PortalSlamPattern(Boss_Last owner) : base("F-B12-07")
        {
            boss = owner;
        }

        /// <summary>살아 있는 전투 대상과 유효한 횟수 확인</summary>
        public override bool CanRun(BossCharacter owner)
        {
            Cooldown = boss.SlamCooldown;
            return base.CanRun(owner) && boss.CanStart(owner, this, 7) && boss.SlamCount > 0;
        }

        /// <summary>준비 동작을 취하며 사라질 준비</summary>
        protected override void OnBegin()
        {
            slam = 0;
            portal = null;
            SetStage(0);
            boss.FaceTowards(boss.Target.transform.position);
            boss.PlayBody("body-attack-ready");
        }

        /// <summary>소실 → 추적 → 확정 → 낙하 → 착지를 정해진 횟수만큼 반복</summary>
        protected override void OnTick(float elapsed)
        {
            float time = Elapsed - stageStart;
            boss.TickRift(portal, elapsed);
            if (stage == 0)
            {
                // 사라지는 데 걸리는 전체 시간의 끝 세 장 동안 가로선으로 흩어짐
                if (time < boss.SlamVanish - Frame * 3f) return;
                SetStage(1);
                boss.PlayBody("vanish-ready8");
            }
            else if (stage == 1)
            {
                if (time < Frame * 3f) return;
                SetStage(2);
                boss.SetHidden(true);
            }
            else if (stage == 2)
            {
                if (time >= boss.SlamHideDelay) BeginTrack();
            }
            else if (stage == 3)
            {
                // 예고A와 하늘의 차원문이 Player를 따라가다 끝나는 순간의 위치에 고정
                aim = boss.Target.transform.position;
                boss.GetWarning(0).ShowEllipse(aim, boss.SlamBlastSize);
                if (portal != null && portal.Player != null) portal.Player.transform.position = aim + new Vector2(0f, boss.FallHeight);
                if (time < boss.SlamTrack) return;
                SetStage(4);
                boss.GetWarning(0).BeginImminent(boss.SlamLock);
            }
            else if (stage == 4)
            {
                if (time < boss.SlamLock) return;

                // 차원문에서 나타나 곧바로 내려찍음
                SetStage(5);
                boss.transform.position = new Vector3(aim.x, aim.y, boss.transform.position.z);
                boss.Body.flipX = false;
                boss.SetAltitude(boss.FallHeight);
                boss.SetHidden(false);
                boss.BodyHitbox.SetEnabled(false);
                boss.PlayBody("body-wave-fall");
                boss.ReleaseRift(portal);
                portal = null;
            }
            else if (stage == 5)
            {
                if (!boss.TickFall(time)) return;
                SetStage(6);
                Land();
            }
            else if (stage == 6)
            {
                if (time < boss.Clip("body-landing-in").GetLength() + boss.SlamHold) return;
                slam++;
                if (slam >= boss.SlamCount)
                {
                    // 마지막 착지 뒤에는 끝 동작으로 일어나 대기로 복귀
                    SetStage(8);
                    boss.SetCounter(false);
                    boss.PlayBody("body-landing-out");
                    return;
                }
                SetStage(7);
                boss.PlayBody("vanish-landing3");
            }
            else if (stage == 7)
            {
                // 다시 사라지는 순간 카운터를 닫고 곧바로 다음 추적 시작
                if (time < Frame * 3f) return;
                boss.SetCounter(false);
                boss.SetHidden(true);
                BeginTrack();
            }
            else if (stage == 8 && time >= boss.Clip("body-landing-out").GetLength())
            {
                boss.ResetPresentation();
                Complete();
            }
        }

        /// <summary>남은 차원문과 예고를 지우고 현재 자리에서 다시 나타남</summary>
        protected override void OnCancel()
        {
            portal = null;
            boss.ResetPresentation();
        }

        /// <summary>단계와 그 시작 시간을 함께 기록</summary>
        private void SetStage(int value)
        {
            stage = value;
            stageStart = Elapsed;
        }

        /// <summary>Player를 따라가는 예고A와 하늘의 차원문 시작</summary>
        private void BeginTrack()
        {
            SetStage(3);
            aim = boss.Target.transform.position;
            AreaWarningVfx warning = boss.GetWarning(0);
            warning.ShowEllipse(aim, boss.SlamBlastSize);
            warning.BeginNotice();
            portal = boss.OpenRift(aim + new Vector2(0f, boss.FallHeight), 0f, true, FrontFxOrder);
        }

        /// <summary>착지 순간 범위 피해와 충격파를 처리하고 금빛 강조와 함께 Slash 카운터 허용</summary>
        private void Land()
        {
            boss.BodyHitbox.SetEnabled(true);
            boss.GetWarning(0).Hide();
            attackId = DamageInstance.NextAttackId();
            boss.DamageEllipse(aim, boss.SlamBlastSize, boss.HeavyDamage, attackId);
            Vector2 foot = boss.transform.position;
            boss.SpawnFx("p7-impact-ground", foot, GroundFxOrder);
            boss.SpawnFx("p7-impact-stamp", foot, GroundFxOrder + 1);
            boss.SpawnFx("p7-impact-burst", foot, FrontFxOrder);
            ImpactDistortionFx.Play(foot);
            boss.PlaySound(boss.LandSound);
            boss.PlayBody("body-landing-in");
            boss.SetBodyGold(true, boss.SlamHold);
            boss.SetCounter(true);
        }
    }
}
