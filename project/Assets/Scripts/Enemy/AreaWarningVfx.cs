using System;
using UnityEngine;

/// <summary>피해 판정과 분리해 바닥에 표시할 공격 예고의 형태</summary>
public enum WarningShape
{
    OpenPath, // 양 끝이 열린 직선 경로
    Rectangle, // 네 변이 닫힌 직사각형
    Ellipse, // 원과 타원
    Sector // 꼭짓점에서 펼쳐지는 부채꼴
}

/// <summary>같은 공격 경로를 알리는 예고의 진행 단계</summary>
public enum WarningPhase
{
    Hidden, // 표시하지 않는 대기 상태
    Notice, // 예고A: 경로를 그대로 표시
    Imminent, // 예고B: 공격이 적용되는 순간에 맞춰 안쪽이 가운데에서 가장자리까지 차오름
    Danger, // 예고C: 위험이 지나갈 때까지 진동
    Fading // 위험이 지나간 뒤 투명해지는 구간
}

/// <summary>붉은 1px 테두리와 반투명 내부로 공격 경로를 바닥에 그리고 예고A·B·C 단계를 재생</summary>
public class AreaWarningVfx : MonoBehaviour
{
    public Sprite RectangleSprite; // 사방 1px 테두리를 9분할로 늘릴 닫힌 사각 마스크
    public Sprite OpenLineSprite; // 양 끝 2px가 옅어지는 1×5px 길이 방향 마스크
    public Material SpriteMaterial; // 다른 월드 Sprite와 같은 방식으로 그릴 공용 재질
    public Color WarningColor = new Color32(255, 21, 89, 168); // 흰 마스크에 입힐 적대 예고색
    public int SortingOrder = 12; // 실험실 바닥 레이어(최대 10)보다 위, 캐릭터(19 이상)보다 아래에 놓을 정렬 순서
    public float WarningFadeDuration = 0.2f; // 위험이 지나간 뒤 사라지는 데 걸리는 게임 시간
    [Range(0f, 1f)] public float GaugeOpacity = 1f; // 예고B에서 차오르는 안쪽 면의 농도이며 테두리 농도에 대한 비율
    public float ShakePixels = 1f; // 예고C에서 좌우로 흔들 원본 픽셀 수
    public float ShakeFrequency = 30f; // 예고C에서 초당 좌우를 바꿀 횟수
    public WarningShape Shape { get; private set; } // 현재 표시 중인 형태
    public WarningPhase Phase { get; private set; } // 현재 진행 중인 예고 단계
    public bool IsVisible => Phase != WarningPhase.Hidden; // 화면에 경로를 표시하고 있는지 여부

    private const float Pixel = 1f / PixelShapeTexture.PixelsPerUnit; // 원본 1px의 월드 길이
    private SpriteRenderer fill; // 열린 경로의 내부와 닫힌 사각형을 9분할로 늘려 그릴 렌더러
    private SpriteRenderer shape; // 최종 격자에서 만든 타원·부채꼴을 원래 크기로 그릴 렌더러
    private SpriteRenderer lineA; // 열린 경로의 한쪽 측선
    private SpriteRenderer lineB; // 열린 경로의 반대쪽 측선
    private SpriteRenderer gauge; // 예고B에서 가운데부터 차오르는 안쪽 면을 그릴 렌더러
    private Transform visual; // 예고C에서 흔들 표시 묶음
    private Texture2D gaugeTexture; // 타원·부채꼴의 차오르는 면을 원본 격자로 다시 그릴 자기 텍스처
    private Sprite gaugeSprite; // 위 텍스처를 감싼 Sprite
    private Color32[] gaugePixels; // 텍스처에 올릴 칸을 다시 쓰는 버퍼
    private Vector2Int maskSize; // 타원·부채꼴 마스크의 원본 픽셀 크기
    private float sectorAngle = 360f; // 부채꼴의 중심각
    private Vector2 barSize; // 열린 경로와 사각형에서 테두리 안쪽의 가로·세로 길이
    private int gaugeStep = -1; // 마지막으로 그린 차오름 단계이며 다시 그려야 하면 -1
    private Projectile[] watched = Array.Empty<Projectile>(); // 모두 사라지면 위험이 지나간 것으로 볼 탄환
    private int watchedCount; // 이번 위험 구간에서 지켜볼 탄환 수
    private float currentOpacity = 1f; // 소멸이 예고색 알파에 곱하는 현재 비율
    private float phaseElapsed; // 현재 단계에서 지난 게임 시간
    private float imminentDuration; // 예고B 전체 길이
    private bool prepared; // 자식 렌더러를 만들었는지 여부

    /// <summary>표시에 쓸 자식 렌더러를 준비하고 숨김 상태로 시작</summary>
    private void Awake()
    {
        Prepare();
        Hide();
    }

    /// <summary>게임 시간으로 차오름·진동·소멸을 진행</summary>
    private void Update()
    {
        if (Phase == WarningPhase.Hidden || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        phaseElapsed += Time.deltaTime;
        switch (Phase)
        {
            case WarningPhase.Imminent:
                TickImminent();
                break;
            case WarningPhase.Danger:
                TickDanger();
                break;
            case WarningPhase.Fading:
                // 남은 시간에 비례해 투명해진 뒤 숨김
                float remaining = WarningFadeDuration > 0f ? 1f - phaseElapsed / WarningFadeDuration : 0f;
                if (remaining <= 0f) Hide();
                else SetOpacity(remaining);
                break;
        }
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        ReleaseGaugeTexture();
    }

    /// <summary>시작점에서 끝점까지 이어지는 양 끝이 열린 직선 경로 배치</summary>
    public void ShowOpenPath(Vector2 start, Vector2 end, float width)
    {
        // 길이 방향을 로컬 +Y로 두고 경로 중앙에 배치
        Prepare();
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0f || width <= 0f) return;
        Shape = WarningShape.OpenPath;
        float lengthSize = Mathf.Max(Pixel, Mathf.Round(length / Pixel) * Pixel);
        float widthSize = Mathf.Max(Pixel * 3f, Mathf.Round(width / Pixel) * Pixel);
        transform.SetPositionAndRotation((start + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, delta));
        transform.localScale = Vector3.one;

        // 1px 측선 두 개와 그 사이의 채움에 같은 길이 방향 마스크 적용
        SetSliced(fill, OpenLineSprite, new Vector2(widthSize - Pixel * 2f, lengthSize), Vector2.zero);
        SetSliced(lineA, OpenLineSprite, new Vector2(Pixel, lengthSize), new Vector2(-(widthSize - Pixel) * 0.5f, 0f));
        SetSliced(lineB, OpenLineSprite, new Vector2(Pixel, lengthSize), new Vector2((widthSize - Pixel) * 0.5f, 0f));
        shape.enabled = false;
        barSize = new Vector2(widthSize - Pixel * 2f, lengthSize);
        gaugeStep = -1;
        ApplyBaseColors();
    }

    /// <summary>중심·크기·각도로 네 변이 닫힌 직사각형 배치</summary>
    public void ShowRectangle(Vector2 center, Vector2 size, float angle)
    {
        Prepare();
        if (size.x <= 0f || size.y <= 0f) return;
        Shape = WarningShape.Rectangle;
        transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
        transform.localScale = Vector3.one;
        SetSliced(fill, RectangleSprite, new Vector2(SnapSize(size.x), SnapSize(size.y)), Vector2.zero);
        lineA.enabled = lineB.enabled = shape.enabled = false;
        barSize = new Vector2(SnapSize(size.x) - Pixel * 2f, SnapSize(size.y) - Pixel * 2f);
        gaugeStep = -1;
        ApplyBaseColors();
    }

    /// <summary>중심과 가로·세로 크기로 원 또는 타원 배치</summary>
    public void ShowEllipse(Vector2 center, Vector2 size)
    {
        Prepare();
        if (size.x <= 0f || size.y <= 0f) return;
        Shape = WarningShape.Ellipse;
        int width = Mathf.Max(3, Mathf.RoundToInt(size.x / Pixel)), height = Mathf.Max(3, Mathf.RoundToInt(size.y / Pixel));
        transform.SetPositionAndRotation(SnapPosition(center, width, height), Quaternion.identity);
        transform.localScale = Vector3.one;
        shape.sprite = PixelShapeTexture.GetEllipseMask(width, height);
        shape.enabled = true;
        fill.enabled = lineA.enabled = lineB.enabled = false;
        maskSize = new Vector2Int(width, height);
        gaugeStep = -1;
        ApplyBaseColors();
    }

    /// <summary>꼭짓점·조준 방향·중심각·반지름으로 부채꼴 배치</summary>
    public void ShowSector(Vector2 origin, Vector2 direction, float angle, float radius)
    {
        Prepare();
        if (direction.sqrMagnitude <= 0f || angle <= 0f || radius <= 0f) return;
        Shape = WarningShape.Sector;
        transform.SetPositionAndRotation(origin, Quaternion.FromToRotation(Vector3.right, direction));
        transform.localScale = Vector3.one;
        int pixels = Mathf.Max(2, Mathf.RoundToInt(radius / Pixel));
        shape.sprite = PixelShapeTexture.GetSectorMask(pixels, angle);
        shape.enabled = true;
        fill.enabled = lineA.enabled = lineB.enabled = false;
        maskSize = new Vector2Int(pixels * 2, pixels * 2);
        sectorAngle = Mathf.Clamp(angle, 1f, 360f);
        gaugeStep = -1;
        ApplyBaseColors();
    }

    /// <summary>예고A: 배치한 경로를 그대로 표시</summary>
    public void BeginNotice()
    {
        Prepare();
        Phase = WarningPhase.Notice;
        phaseElapsed = 0f;
        watchedCount = 0;
        visual.localPosition = Vector3.zero;
        visual.gameObject.SetActive(true);
        gauge.enabled = false;
        SetOpacity(1f);
    }

    /// <summary>예고B: 지정한 시간이 끝나는 순간 안쪽이 다 차도록 가운데에서 가장자리까지 채워 공격이 적용될 때를 알림</summary>
    public void BeginImminent(float duration)
    {
        Prepare();
        Phase = WarningPhase.Imminent;
        phaseElapsed = 0f;
        imminentDuration = Mathf.Max(0.01f, duration);
        watchedCount = 0;
        gaugeStep = -1;
        visual.localPosition = Vector3.zero;
        visual.gameObject.SetActive(true);
        SetOpacity(1f);
        TickImminent();
    }

    /// <summary>예고C: 위험이 지나갔다고 알릴 때까지 경로를 진동</summary>
    public void BeginDanger()
    {
        Prepare();
        Phase = WarningPhase.Danger;
        phaseElapsed = 0f;
        watchedCount = 0;
        visual.gameObject.SetActive(true);
        gauge.enabled = false;
        SetOpacity(1f);
    }

    /// <summary>예고C를 시작하고 지정한 탄환이 모두 경로를 떠나면 스스로 사라지도록 설정</summary>
    public void BeginDanger(Projectile first, Projectile second = null)
    {
        BeginDanger();
        if (watched.Length < 2) watched = new Projectile[2];
        watched[0] = first;
        watched[1] = second;
        watchedCount = 2;
    }

    /// <summary>위험이 지나간 뒤 FadeDuration에 걸쳐 투명해지며 사라지기 시작</summary>
    public void EndDanger()
    {
        if (Phase == WarningPhase.Hidden || Phase == WarningPhase.Fading) return;
        Phase = WarningPhase.Fading;
        phaseElapsed = 0f;
        watchedCount = 0;
        visual.localPosition = Vector3.zero;
    }

    /// <summary>진행 중인 단계를 버리고 즉시 숨김</summary>
    public void Hide()
    {
        Phase = WarningPhase.Hidden;
        phaseElapsed = 0f;
        watchedCount = 0;
        if (!prepared) return;
        visual.localPosition = Vector3.zero;
        gauge.enabled = false;
        visual.gameObject.SetActive(false);
    }

    /// <summary>자식 렌더러를 한 번만 생성</summary>
    private void Prepare()
    {
        if (prepared) return;
        var root = new GameObject("Visual");
        visual = root.transform;
        visual.SetParent(transform, false);
        fill = CreateRenderer("Fill", SortingOrder);
        shape = CreateRenderer("Shape", SortingOrder);
        lineA = CreateRenderer("LineA", SortingOrder + 1);
        lineB = CreateRenderer("LineB", SortingOrder + 1);
        gauge = CreateRenderer("Gauge", SortingOrder + 1);
        gauge.enabled = false;
        prepared = true;
    }

    /// <summary>표시 묶음 아래에 Sprite 렌더러 하나 생성</summary>
    private SpriteRenderer CreateRenderer(string objectName, int order)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(visual, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        if (SpriteMaterial != null) renderer.sharedMaterial = SpriteMaterial;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>테두리 영역은 유지하고 가운데만 늘리는 방식으로 Sprite 크기 지정</summary>
    private static void SetSliced(SpriteRenderer renderer, Sprite sprite, Vector2 size, Vector2 localPosition)
    {
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.transform.localPosition = localPosition;
        renderer.enabled = sprite != null && size.x > 0f && size.y > 0f;
    }

    /// <summary>형태별 기본 농도: 측선은 그대로, 열린 경로의 채움은 마스크 내부 알파로 낮춤</summary>
    private void ApplyBaseColors()
    {
        SetOpacity(currentOpacity);
    }

    /// <summary>예고색 알파에 단계별 비율을 곱해 모든 렌더러에 적용</summary>
    private void SetOpacity(float opacity)
    {
        currentOpacity = Mathf.Clamp01(opacity);
        Color line = WarningColor;
        line.a *= currentOpacity;
        Color inner = line;

        // 열린 경로의 채움은 1×5 마스크가 불투명하므로 내부 알파 128/255를 색으로 적용
        if (Shape == WarningShape.OpenPath) inner.a *= PixelShapeTexture.FillAlpha / 255f;
        fill.color = inner;
        shape.color = lineA.color = lineB.color = line;

        // 차오르는 면은 반투명 내부 위에 테두리와 같은 색으로 겹쳐 찬 곳과 덜 찬 곳을 구분
        Color filled = line;
        filled.a *= GaugeOpacity;
        gauge.color = filled;
    }

    /// <summary>예고B의 진행에 맞춰 안쪽 면을 가운데에서 가장자리까지 원본 픽셀 단위로 넓힘</summary>
    private void TickImminent()
    {
        float progress = Mathf.Clamp01(phaseElapsed / imminentDuration);
        if (Shape == WarningShape.OpenPath || Shape == WarningShape.Rectangle) DrawBarGauge(progress);
        else DrawMaskGauge(progress);
    }

    /// <summary>열린 경로는 폭 방향으로, 닫힌 사각형은 가로세로를 함께 가운데에서 넓혀 채움</summary>
    private void DrawBarGauge(float progress)
    {
        // 가운데가 어긋나지 않게 두 칸씩 넓히고 넓이가 바뀔 때만 다시 배치
        int fullWidth = Mathf.RoundToInt(barSize.x / Pixel), fullHeight = Mathf.RoundToInt(barSize.y / Pixel);
        int width = GrowCentered(fullWidth, progress);
        int height = Shape == WarningShape.Rectangle ? GrowCentered(fullHeight, progress) : fullHeight;
        int step = width * 8192 + height;
        if (step == gaugeStep) return;
        gaugeStep = step;
        if (width <= 0 || height <= 0)
        {
            gauge.enabled = false;
            return;
        }
        if (Shape == WarningShape.OpenPath)
        {
            // 양 끝이 옅어지는 길이 방향 마스크를 그대로 써서 경로의 채움과 같은 끝 모양 유지
            gauge.transform.localScale = Vector3.one;
            SetSliced(gauge, OpenLineSprite, new Vector2(width * Pixel, height * Pixel), Vector2.zero);
            return;
        }
        gauge.drawMode = SpriteDrawMode.Simple;
        gauge.sprite = PixelShapeTexture.GetSolidEllipse(1, 1);
        gauge.transform.localPosition = Vector3.zero;
        gauge.transform.localScale = new Vector3(width, height, 1f);
        gauge.enabled = true;
    }

    /// <summary>타원은 가운데에서, 부채꼴은 꼭짓점에서 바깥으로 넓어지는 안쪽 면을 자기 텍스처에 원본 격자로 다시 그림</summary>
    private void DrawMaskGauge(float progress)
    {
        int width = maskSize.x, height = maskSize.y;
        if (width <= 0 || height <= 0)
        {
            gauge.enabled = false;
            return;
        }

        // 한 칸 넓어질 때만 다시 그림
        int reach = Mathf.Max(1, Mathf.Max(width, height) / 2);
        int step = Mathf.FloorToInt(progress * reach);
        if (step == gaugeStep) return;
        gaugeStep = step;
        PrepareGaugeTexture(width, height);

        // 테두리 한 칸 안쪽까지를 다 찬 크기로 삼고 그 비율만큼의 칸을 채움
        float grown = (float)step / reach;
        float radiusX = width * 0.5f, radiusY = height * 0.5f;
        float innerX = Mathf.Max(0.5f, radiusX - 1f), innerY = Mathf.Max(0.5f, radiusY - 1f);
        bool sector = Shape == WarningShape.Sector && sectorAngle < 360f;
        float cosHalf = Mathf.Cos(sectorAngle * 0.5f * Mathf.Deg2Rad);
        var on = new Color32(255, 255, 255, 255);
        var off = new Color32(0, 0, 0, 0);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = x + 0.5f - radiusX, dy = y + 0.5f - radiusY;
                float nx = dx / innerX, ny = dy / innerY;
                bool inside = nx * nx + ny * ny <= grown * grown;
                if (inside && sector) inside = dx >= Mathf.Sqrt(dx * dx + dy * dy) * cosHalf;
                gaugePixels[y * width + x] = inside ? on : off;
            }
        gaugeTexture.SetPixels32(gaugePixels);
        gaugeTexture.Apply(false, false);
        gauge.drawMode = SpriteDrawMode.Simple;
        gauge.sprite = gaugeSprite;
        gauge.transform.localPosition = Vector3.zero;
        gauge.transform.localScale = Vector3.one;
        gauge.enabled = step > 0;
    }

    /// <summary>다 찼을 때의 칸 수와 홀짝이 같은 값으로만 넓혀 가운데를 유지</summary>
    private static int GrowCentered(int full, float progress)
    {
        int grown = Mathf.FloorToInt(full * progress);
        if ((full - grown) % 2 != 0) grown--;
        return Mathf.Clamp(grown, 0, full);
    }

    /// <summary>차오르는 면을 그릴 자기 텍스처를 지금 도형의 크기로 준비</summary>
    private void PrepareGaugeTexture(int width, int height)
    {
        if (gaugeTexture != null && gaugeTexture.width == width && gaugeTexture.height == height) return;
        ReleaseGaugeTexture();
        gaugeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        gaugeSprite = Sprite.Create(gaugeTexture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), PixelShapeTexture.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        gaugeSprite.hideFlags = HideFlags.HideAndDontSave;
        gaugePixels = new Color32[width * height];
    }

    /// <summary>차오르는 면에 쓰던 자기 텍스처와 Sprite 제거</summary>
    private void ReleaseGaugeTexture()
    {
        if (gaugeSprite != null) Destroy(gaugeSprite);
        if (gaugeTexture != null) Destroy(gaugeTexture);
        gaugeSprite = null;
        gaugeTexture = null;
    }

    /// <summary>지켜보는 탄환이 모두 떠났는지 확인하고 남아 있으면 경로를 좌우로 진동</summary>
    private void TickDanger()
    {
        if (watchedCount > 0)
        {
            // 제거됐거나 Slash로 반사돼 경로를 벗어난 탄환은 지나간 위험으로 처리
            bool cleared = true;
            for (int i = 0; i < watchedCount; i++)
                if (watched[i] != null && watched[i].IsActive && !watched[i].IsReflected) cleared = false;
            if (cleared)
            {
                EndDanger();
                return;
            }
        }
        float side = Mathf.FloorToInt(phaseElapsed * Mathf.Max(1f, ShakeFrequency)) % 2 == 0 ? 1f : -1f;
        visual.localPosition = new Vector3(side * ShakePixels * Pixel, 0f, 0f);
    }

    /// <summary>길이를 원본 픽셀의 배수로 맞춤</summary>
    private static float SnapSize(float value)
    {
        return Mathf.Max(Pixel * 3f, Mathf.Round(value / Pixel) * Pixel);
    }

    /// <summary>도형의 가장자리가 픽셀 경계에 놓이도록 중심 위치 보정</summary>
    private static Vector3 SnapPosition(Vector2 center, int width, int height)
    {
        float x = Mathf.Round((center.x - width * 0.5f * Pixel) / Pixel) * Pixel + width * 0.5f * Pixel;
        float y = Mathf.Round((center.y - height * 0.5f * Pixel) / Pixel) * Pixel + height * 0.5f * Pixel;
        return new Vector3(x, y, 0f);
    }
}
