using UnityEngine;
using UnityEngine.UI;

/// <summary>그로기 게이지에서 나온 구슬의 포물선 비행과 도착 접점·전신 흰빛·머리 위 별을 재생하는 기절효과</summary>
[RequireComponent(typeof(BossCharacter))]
public class BossGroggyFx : MonoBehaviour
{
    public SpriteClip OrbClip; // 비행 중 반복할 청록 구슬
    public SpriteClip ContactClip; // 구슬이 닿은 자리에서 한 번 재생할 접점 효과
    public SpriteClip StarClip; // 기절 동안 머리 위에서 반복할 별 두 개
    public Material SpriteMaterial; // 구슬·접점·별을 다른 월드 Sprite와 같게 그릴 재질
    public Material FlashMaterial; // 중심에서 퍼지는 흰빛을 그릴 재질
    public SpriteRenderer[] FlashTargets = new SpriteRenderer[0]; // 흰빛을 입힐 보스의 몸 렌더러
    public Vector2 CenterOffset = new Vector2(0f, 1.5f); // 보스 기준점에서 구슬이 도착할 몸 중심까지의 거리
    public Vector2 StarOffset = new Vector2(0f, 3.5f); // 보스 기준점에서 별을 놓을 머리 위까지의 거리
    public float GroggyTravelDuration = 1.6f; // 게이지 충족부터 구슬 도착까지의 게임 시간
    public float ArcHeight = 2.5f; // 직선 경로보다 위로 솟는 포물선 꼭짓점의 높이
    [Range(0.1f, 0.9f)] public float ApexPoint = 0.3f; // 전체 경로에서 꼭짓점이 놓이는 지점의 비율
    [Range(0.1f, 0.9f)] public float ApexTime = 0.5f; // 전체 비행 시간에서 꼭짓점에 닿는 시점의 비율
    public float SpreadDuration = 0.25f; // 흰빛이 중심에서 전신 끝까지 퍼지는 게임 시간
    public float FlashFadeDuration = 0.15f; // 전신이 밝아진 뒤 원래 색으로 돌아오는 게임 시간
    public int OrbSortingOrder = 60; // 구슬과 접점을 캐릭터 위에 그릴 정렬 순서
    public int StarSortingOrder = 45; // 별을 보스 머리 위에 그릴 정렬 순서
    public bool IsTraveling { get; private set; } // 구슬이 보스로 날아가는 중인지 여부
    public float TravelElapsed { get; private set; } // 이번 비행에서 지난 게임 시간

    private static readonly int FlashCenterId = Shader.PropertyToID("_FlashCenter"); // 흰빛 중심의 월드 좌표
    private static readonly int FlashRadiusId = Shader.PropertyToID("_FlashRadius"); // 현재 흰빛 반경
    private static readonly int FlashStrengthId = Shader.PropertyToID("_FlashStrength"); // 흰빛 농도
    private BossCharacter boss; // 그로기 진입과 상태 조회에 쓸 자기 보스
    private SpriteClipPlayer orb; // 비행 구슬 재생기
    private SpriteClipPlayer stars; // 머리 위 별 재생기
    private Material flashInstance; // 이 보스만 쓰는 흰빛 재질 사본
    private Material[] originalMaterials; // 흰빛이 끝난 뒤 되돌릴 원래 재질
    private float flashElapsed; // 흰빛 확산을 시작한 뒤 지난 게임 시간
    private float flashRadius; // 몸 전체를 덮는 최종 반경
    private bool flashing; // 흰빛 재질을 적용한 상태
    private bool prepared; // 표시 객체를 만들었는지 여부

    /// <summary>자기 보스와 표시 객체를 준비</summary>
    private void Awake()
    {
        Prepare();
    }

    /// <summary>게임 시간으로 구슬 비행·흰빛 확산·별 표시를 진행</summary>
    private void Update()
    {
        if (!prepared || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        if (IsTraveling) TickTravel(Time.deltaTime);
        if (flashing) TickFlash(Time.deltaTime);

        // 기절이 풀리면 별과 꼬리를 함께 숨김
        bool showStars = boss.IsGroggy && !boss.IsDead;
        if (stars.gameObject.activeSelf != showStars)
        {
            stars.gameObject.SetActive(showStars);
            if (showStars) stars.Play(StarClip);
        }
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnDestroy()
    {
        if (orb != null) Destroy(orb.gameObject);
        if (flashInstance != null) Destroy(flashInstance);
    }

    /// <summary>게이지가 찬 순간 구슬 비행을 시작하고 이미 비행 중이면 그대로 유지</summary>
    public bool Begin()
    {
        Prepare();
        if (!isActiveAndEnabled || boss.IsDead || OrbClip == null || GroggyTravelDuration <= 0f) return false;
        if (IsTraveling) return true;
        IsTraveling = true;
        TravelElapsed = 0f;
        orb.transform.position = GetStartPosition();
        orb.gameObject.SetActive(true);
        orb.Play(OrbClip);
        return true;
    }

    /// <summary>비행 중인 구슬과 흰빛·별을 정리</summary>
    public void Cancel()
    {
        IsTraveling = false;
        TravelElapsed = 0f;
        if (!prepared) return;
        if (orb != null) orb.gameObject.SetActive(false);
        if (stars != null) stars.gameObject.SetActive(false);
        EndFlash();
    }

    /// <summary>구슬·별 객체와 흰빛 재질 사본을 한 번만 생성</summary>
    private void Prepare()
    {
        if (prepared) return;
        boss = GetComponent<BossCharacter>();

        // 구슬은 보스가 움직여도 경로 계산대로 놓이도록 보스 밖에 생성
        orb = CreatePlayer("GroggyOrb", transform.parent, OrbSortingOrder);
        stars = CreatePlayer("GroggyStars", transform, StarSortingOrder);
        stars.transform.localPosition = StarOffset;
        if (FlashMaterial != null) flashInstance = new Material(FlashMaterial);
        originalMaterials = new Material[FlashTargets.Length];
        prepared = true;
    }

    /// <summary>꺼진 상태의 Sprite 재생 객체 하나 생성</summary>
    private SpriteClipPlayer CreatePlayer(string objectName, Transform parent, int order)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        if (SpriteMaterial != null) renderer.sharedMaterial = SpriteMaterial;
        renderer.sortingOrder = order;
        var player = child.AddComponent<SpriteClipPlayer>();
        child.SetActive(false);
        return player;
    }

    /// <summary>꼭짓점까지는 일정하게, 그 뒤로는 점점 빠르게 이동시키고 도착하면 기절 시작</summary>
    private void TickTravel(float gameDelta)
    {
        // 보스가 죽거나 전투가 끝나면 날아가던 구슬만 정리
        if (boss.IsDead)
        {
            IsTraveling = false;
            orb.gameObject.SetActive(false);
            return;
        }
        TravelElapsed += gameDelta;
        float time = Mathf.Clamp01(TravelElapsed / GroggyTravelDuration);

        // 꼭짓점 이후 남은 거리를 등가속으로 채우는 진행률 계산
        float baseSpeed = ApexPoint / ApexTime;
        float progress;
        if (time <= ApexTime) progress = baseSpeed * time;
        else
        {
            float after = time - ApexTime, span = 1f - ApexTime;
            float acceleration = 2f * (1f - ApexPoint - baseSpeed * span) / (span * span);
            progress = ApexPoint + baseSpeed * after + 0.5f * acceleration * after * after;
        }
        progress = Mathf.Clamp01(progress);

        // 화면에 고정된 게이지와 보스의 현재 위치를 매 프레임 다시 읽어 같은 시간에 도착
        Vector2 start = GetStartPosition(), end = GetCenter();
        float side = progress <= ApexPoint ? (ApexPoint - progress) / ApexPoint : (progress - ApexPoint) / (1f - ApexPoint);
        Vector2 position = Vector2.Lerp(start, end, progress) + Vector2.up * (ArcHeight * (1f - side * side));
        orb.transform.position = position;
        if (time < 1f) return;

        // 도착 순간 접점 효과와 패턴 취소·기절 진입·흰빛 확산을 같은 프레임에 시작
        IsTraveling = false;
        orb.gameObject.SetActive(false);
        SpriteClipPlayer.Spawn(ContactClip, end, OrbSortingOrder, SpriteMaterial, transform.parent);
        boss.EnterGroggy();
        if (boss.IsGroggy) BeginFlash(end);
    }

    /// <summary>몸 렌더러에 흰빛 재질을 입히고 중심에서 확산 시작</summary>
    private void BeginFlash(Vector2 center)
    {
        if (flashInstance == null || FlashTargets.Length == 0) return;
        EndFlash();

        // 중심에서 가장 먼 몸 끝까지의 거리를 최종 반경으로 사용
        flashRadius = 0f;
        for (int i = 0; i < FlashTargets.Length; i++)
        {
            SpriteRenderer target = FlashTargets[i];
            if (target == null) continue;
            Bounds bounds = target.bounds;
            flashRadius = Mathf.Max(flashRadius, Vector2.Distance(center, bounds.min), Vector2.Distance(center, bounds.max),
                Vector2.Distance(center, new Vector2(bounds.min.x, bounds.max.y)), Vector2.Distance(center, new Vector2(bounds.max.x, bounds.min.y)));
            originalMaterials[i] = target.sharedMaterial;
            target.sharedMaterial = flashInstance;
        }
        flashInstance.SetVector(FlashCenterId, center);
        flashInstance.SetFloat(FlashRadiusId, 0f);
        flashInstance.SetFloat(FlashStrengthId, 1f);
        flashElapsed = 0f;
        flashing = true;
    }

    /// <summary>반경을 넓힌 뒤 농도를 낮추고 끝나면 원래 재질 복원</summary>
    private void TickFlash(float gameDelta)
    {
        flashElapsed += gameDelta;
        float spread = SpreadDuration > 0f ? Mathf.Clamp01(flashElapsed / SpreadDuration) : 1f;
        flashInstance.SetFloat(FlashRadiusId, flashRadius * spread);
        float fade = FlashFadeDuration > 0f ? Mathf.Clamp01((flashElapsed - SpreadDuration) / FlashFadeDuration) : 1f;
        flashInstance.SetFloat(FlashStrengthId, 1f - fade);
        if (flashElapsed >= SpreadDuration + FlashFadeDuration) EndFlash();
    }

    /// <summary>흰빛 재질을 걷고 몸 렌더러의 원래 재질 복원</summary>
    private void EndFlash()
    {
        if (!flashing) return;
        flashing = false;
        for (int i = 0; i < FlashTargets.Length; i++)
            if (FlashTargets[i] != null && originalMaterials[i] != null) FlashTargets[i].sharedMaterial = originalMaterials[i];
    }

    /// <summary>구슬이 도착할 보스 몸 중심의 현재 월드 위치</summary>
    private Vector2 GetCenter()
    {
        return (Vector2)transform.position + CenterOffset;
    }

    /// <summary>HUD 그로기 게이지의 화면 위치를 월드 좌표로 바꾸고 HUD가 없으면 Player 위치 사용</summary>
    private Vector2 GetStartPosition()
    {
        UIController hud = UIController.Instance;
        Camera worldCamera = GameCamera.Instance != null ? GameCamera.Instance.Camera : Camera.main;
        Image gauge = hud != null ? hud.GroggyFill : null;
        if (gauge == null || worldCamera == null || !gauge.gameObject.activeInHierarchy)
            return boss.Target != null ? (Vector2)boss.Target.transform.position : GetCenter() + Vector2.down * 3f;

        // 오버레이 캔버스는 카메라 없이, 카메라 캔버스는 그 카메라로 화면 좌표 계산
        Canvas canvas = gauge.canvas;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransform rect = gauge.rectTransform;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
        Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(worldCamera.transform.position.z - transform.position.z)));
        return world;
    }
}
