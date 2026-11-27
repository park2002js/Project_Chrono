using System.Collections.Generic;
using UnityEngine;

/// <summary>최종 픽셀 격자에서 타원·부채꼴을 계산해 Texture2D에 그리고 같은 크기는 다시 쓰는 도형 생성기</summary>
public static class PixelShapeTexture
{
    public const float PixelsPerUnit = 16f; // 월드 1유닛에 놓을 원본 픽셀 수
    public const byte FillAlpha = 128; // 공격 예고 마스크의 내부 알파
    public const byte OutlineAlpha = 255; // 공격 예고 마스크의 1px 테두리 알파

    private const int CacheLimit = 96; // 크기가 계속 바뀌는 요청으로 텍스처가 쌓이지 않게 비울 기준
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>(); // 도형 종류와 크기별로 만든 Sprite

    /// <summary>플레이 시작마다 이전 실행에서 남은 생성 텍스처 정리</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        Clear();
    }

    /// <summary>테두리 1px와 반투명 내부를 가진 타원 예고 마스크 조회</summary>
    public static Sprite GetEllipseMask(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        string key = "E" + width + "x" + height;
        if (TryGet(key, out Sprite sprite)) return sprite;

        // 픽셀 중심이 타원 안에 있는지 구한 뒤 바깥과 맞닿은 칸만 테두리로 표시
        var inside = new bool[width * height];
        float radiusX = width * 0.5f, radiusY = height * 0.5f;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f - radiusX) / radiusX, dy = (y + 0.5f - radiusY) / radiusY;
                inside[y * width + x] = dx * dx + dy * dy <= 1f;
            }
        return Store(key, BuildMask(inside, width, height, true), new Vector2(0.5f, 0.5f));
    }

    /// <summary>꼭짓점을 기준점으로 +X 방향을 향해 펼친 부채꼴 예고 마스크 조회</summary>
    public static Sprite GetSectorMask(int radius, float angle)
    {
        radius = Mathf.Max(1, radius);
        angle = Mathf.Clamp(angle, 1f, 360f);
        string key = "S" + radius + "a" + Mathf.RoundToInt(angle * 10f);
        if (TryGet(key, out Sprite sprite)) return sprite;

        // 꼭짓점을 캔버스 중앙에 두고 반지름과 중심각 안의 픽셀 중심만 채움
        int size = radius * 2;
        var inside = new bool[size * size];
        float cosHalf = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
        bool full = angle >= 360f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                inside[y * size + x] = distance <= radius && (full || dx >= distance * cosHalf);
            }
        return Store(key, BuildMask(inside, size, size, true), new Vector2(0.5f, 0.5f));
    }

    /// <summary>그림자와 광점에 쓸 테두리 없는 불투명 타원 조회</summary>
    public static Sprite GetSolidEllipse(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        string key = "F" + width + "x" + height;
        if (TryGet(key, out Sprite sprite)) return sprite;
        var inside = new bool[width * height];
        float radiusX = width * 0.5f, radiusY = height * 0.5f;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f - radiusX) / radiusX, dy = (y + 0.5f - radiusY) / radiusY;
                inside[y * width + x] = dx * dx + dy * dy <= 1f;
            }
        return Store(key, BuildMask(inside, width, height, false), new Vector2(0.5f, 0.5f));
    }

    /// <summary>만들어 둔 도형 텍스처와 Sprite를 모두 제거</summary>
    public static void Clear()
    {
        foreach (Sprite sprite in cache.Values)
        {
            if (sprite == null) continue;
            Texture2D texture = sprite.texture;
            Object.Destroy(sprite);
            if (texture != null) Object.Destroy(texture);
        }
        cache.Clear();
    }

    /// <summary>아직 살아 있는 같은 도형의 Sprite를 조회</summary>
    private static bool TryGet(string key, out Sprite sprite)
    {
        return cache.TryGetValue(key, out sprite) && sprite != null;
    }

    /// <summary>안팎 판정 결과를 흰색 마스크 텍스처로 변환</summary>
    private static Texture2D BuildMask(bool[] inside, int width, int height, bool outlined)
    {
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (!inside[index]) continue;

                // 캔버스 끝이거나 상하좌우 중 한 칸이라도 바깥이면 테두리
                bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1
                    || !inside[index - 1] || !inside[index + 1] || !inside[index - width] || !inside[index + width];
                pixels[index] = new Color32(255, 255, 255, !outlined || edge ? OutlineAlpha : FillAlpha);
            }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>텍스처를 16PPU Sprite로 감싸 보관</summary>
    private static Sprite Store(string key, Texture2D texture, Vector2 pivot)
    {
        if (cache.Count >= CacheLimit) Clear();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        cache[key] = sprite;
        return sprite;
    }
}
