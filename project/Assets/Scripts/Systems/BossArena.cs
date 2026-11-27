using UnityEngine;

/// <summary>보스 전투 동안 걸을 수 있는 범위를 조절식 사각 구역으로 좁히는 전투 구역</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossArena : MonoBehaviour
{
    public Vector2 Size = new Vector2(19.5f, 8.125f); // 전투 중 걸을 수 있는 구역의 가로·세로 크기
    public Vector2 TopCenterOffset = new Vector2(0f, 9f); // 이 객체에서 구역 윗변 가운데까지의 거리
    public float EntryInset = 1f; // 벽이 생길 때 Player가 끼지 않도록 조우 감지 영역을 아랫변에서 안쪽으로 들일 거리
    public float SideInset = 0.5f; // 조우 감지 영역을 좌우 변에서 안쪽으로 들일 거리
    public float WallThickness = 1f; // 구역 바깥쪽으로 세울 벽 충돌의 두께
    public Material SpriteMaterial; // 바깥 가림막과 테두리를 다른 월드 Sprite와 같게 그릴 재질
    public Color OutsideColor = new Color(0f, 0f, 0f, 0.6f); // 전투 중 구역 바깥을 덮을 가림막 색
    public Color EdgeColor = new Color(0.82f, 0.88f, 1f, 0.9f); // 구역 경계를 따라 그릴 테두리 색
    public float EdgePixels = 2f; // 테두리의 원본 픽셀 두께
    public float OutsideExtent = 30f; // 가림막이 구역 바깥으로 덮을 거리
    public int OverlayOrder = 11; // 가림막을 바닥 레이어 위, 공격 예고 아래에 놓을 정렬 순서
    public bool ShowOverlay = true; // 가림막과 테두리를 그릴지 여부이며 방 그림이 경계를 직접 보여 주면 끔
    public bool IsActive { get; private set; } // 벽·가림막·테두리가 켜진 전투 상태
    public Rect Zone => new Rect(TopCenter.x - Size.x * 0.5f, TopCenter.y - Size.y, Size.x, Size.y); // 걸을 수 있는 구역의 월드 사각형

    private const float Pixel = 1f / PixelShapeTexture.PixelsPerUnit; // 원본 1px의 월드 길이
    private readonly BoxCollider2D[] walls = new BoxCollider2D[4]; // 왼쪽·오른쪽·아래·위 순서의 구역 벽
    private readonly SpriteRenderer[] covers = new SpriteRenderer[4]; // 왼쪽·오른쪽·아래·위 순서의 바깥 가림막
    private readonly SpriteRenderer[] edges = new SpriteRenderer[4]; // 왼쪽·오른쪽·아래·위 순서의 경계 테두리
    private BoxCollider2D trigger; // 조우 진입을 감지하는 자기 Collider
    private BossCharacter boss; // 전투 상태를 읽을 이 구역의 보스
    private Transform root; // 벽·가림막·테두리를 묶어 둘 자식
    private Vector2 appliedSize; // 마지막으로 배치에 반영한 구역 크기
    private Vector4 appliedMargins; // 마지막으로 반영한 감지 여백과 벽 두께
    private bool prepared; // 자식 객체를 만들었는지 여부
    private bool appliedOverlay; // 마지막으로 반영한 가림막 표시 여부

    private Vector2 TopCenter => (Vector2)transform.position + TopCenterOffset; // 구역 윗변 가운데의 월드 위치

    /// <summary>Map이 벽 목록을 모으기 전에 벽과 표시 객체를 만들고 감지 영역을 구역에 맞춤</summary>
    private void Awake()
    {
        Prepare();
        ApplyLayout();
        SetActive(false);
    }

    /// <summary>보스의 전투 상태에 맞춰 구역을 켜고 끄며 Inspector에서 바꾼 크기를 즉시 반영</summary>
    private void Update()
    {
        if (!prepared) return;
        if (appliedSize != Size || appliedMargins != new Vector4(EntryInset, SideInset, WallThickness, OutsideExtent)) ApplyLayout();
        bool fighting = boss != null && boss.isActiveAndEnabled && !boss.IsDead && boss.IsInCombat;
        if (fighting != IsActive || appliedOverlay != ShowOverlay) SetActive(fighting);
    }

    private void OnDisable()
    {
        SetActive(false);
    }

    /// <summary>이 구역에서 싸울 보스를 연결</summary>
    public void Bind(BossCharacter owner)
    {
        boss = owner;
    }

    /// <summary>연결된 보스가 정리될 때 구역을 끄고 연결 해제</summary>
    public void Unbind(BossCharacter owner)
    {
        if (boss != owner) return;
        boss = null;
        SetActive(false);
    }

    /// <summary>벽·가림막·테두리 객체를 한 번만 생성</summary>
    private void Prepare()
    {
        if (prepared) return;
        trigger = GetComponent<BoxCollider2D>();
        root = new GameObject("ArenaBounds").transform;
        root.SetParent(transform, false);
        Sprite pixel = PixelShapeTexture.GetSolidEllipse(1, 1);
        string[] names = { "Left", "Right", "Bottom", "Top" };
        for (int i = 0; i < 4; i++)
        {
            // 벽은 꺼 둔 채로 만들어 Map의 벽 목록에 들어가게 함
            var wall = new GameObject("Wall" + names[i]);
            wall.transform.SetParent(root, false);
            wall.layer = gameObject.layer;
            walls[i] = wall.AddComponent<BoxCollider2D>();
            walls[i].enabled = false;
            covers[i] = CreateSprite("Cover" + names[i], pixel, OverlayOrder);
            edges[i] = CreateSprite("Edge" + names[i], pixel, OverlayOrder + 1);
        }
        prepared = true;
    }

    /// <summary>1px 흰 Sprite를 늘려 쓸 표시 객체 하나 생성</summary>
    private SpriteRenderer CreateSprite(string objectName, Sprite sprite, int order)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(root, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        if (SpriteMaterial != null) renderer.sharedMaterial = SpriteMaterial;
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.enabled = false;
        return renderer;
    }

    /// <summary>현재 구역 크기로 감지 영역·벽·가림막·테두리의 위치와 크기를 다시 계산</summary>
    private void ApplyLayout()
    {
        appliedSize = Size;
        appliedMargins = new Vector4(EntryInset, SideInset, WallThickness, OutsideExtent);
        Rect zone = Zone;

        // 조우 감지 영역은 구역보다 안쪽에 두어 벽이 생길 때 Player가 벽과 겹치지 않게 함
        float triggerHeight = Mathf.Max(0.5f, zone.height - Mathf.Max(0f, EntryInset));
        float triggerWidth = Mathf.Max(0.5f, zone.width - Mathf.Max(0f, SideInset) * 2f);
        trigger.size = new Vector2(triggerWidth, triggerHeight);
        trigger.offset = new Vector2(zone.center.x, zone.yMax - triggerHeight * 0.5f) - (Vector2)transform.position;

        // 왼쪽·오른쪽·아래·위 순서로 구역 바깥에 붙는 띠의 중심과 크기 계산
        float thick = Mathf.Max(0.1f, WallThickness), far = Mathf.Max(thick, OutsideExtent), edge = Mathf.Max(1f, EdgePixels) * Pixel;
        for (int i = 0; i < 4; i++)
        {
            bool vertical = i < 2;
            float sign = i == 0 || i == 2 ? -1f : 1f;
            Vector2 anchor = vertical ? new Vector2(sign < 0f ? zone.xMin : zone.xMax, zone.center.y) : new Vector2(zone.center.x, sign < 0f ? zone.yMin : zone.yMax);
            Vector2 outward = vertical ? new Vector2(sign, 0f) : new Vector2(0f, sign);
            Place(walls[i].transform, anchor + outward * (thick * 0.5f), Vector3.one);
            walls[i].size = vertical ? new Vector2(thick, zone.height + thick * 2f) : new Vector2(zone.width + thick * 2f, thick);

            // 가림막은 좌우가 위아래 띠와 겹치지 않도록 좌우 띠만 세로로 길게 덮음
            Vector2 coverSize = vertical ? new Vector2(far, zone.height + far * 2f) : new Vector2(zone.width, far);
            Place(covers[i].transform, anchor + outward * (far * 0.5f), new Vector3(coverSize.x / Pixel, coverSize.y / Pixel, 1f));
            Vector2 edgeSize = vertical ? new Vector2(edge, zone.height + edge * 2f) : new Vector2(zone.width, edge);
            Place(edges[i].transform, anchor + outward * (edge * 0.5f), new Vector3(edgeSize.x / Pixel, edgeSize.y / Pixel, 1f));
            covers[i].color = OutsideColor;
            edges[i].color = EdgeColor;
        }
    }

    /// <summary>자식 객체를 월드 위치에 놓고 크기 배율 적용</summary>
    private void Place(Transform target, Vector2 position, Vector3 scale)
    {
        target.position = new Vector3(position.x, position.y, transform.position.z);
        target.localScale = scale;
    }

    /// <summary>벽 충돌·가림막·테두리를 함께 켜거나 끔</summary>
    private void SetActive(bool active)
    {
        IsActive = active;
        appliedOverlay = ShowOverlay;
        if (!prepared) return;
        for (int i = 0; i < 4; i++)
        {
            walls[i].enabled = active;
            covers[i].enabled = active && ShowOverlay;
            edges[i].enabled = active && ShowOverlay;
        }
    }
}
