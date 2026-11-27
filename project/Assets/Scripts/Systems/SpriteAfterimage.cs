using UnityEngine;

/// <summary>남긴 순간의 Sprite를 그 자리에 두고 게임 시간으로 옅어지다가 사라지는 잔상</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAfterimage : MonoBehaviour
{
    public float Duration = 0.3f; // 완전히 사라질 때까지의 게임 시간
    public float StartOpacity = 0.55f; // 남긴 순간의 알파

    private SpriteRenderer spriteRenderer; // 옅어질 자기 렌더러
    private Color baseColor; // 알파만 바꿔 쓸 기준색
    private float elapsed; // 남긴 뒤 지난 게임 시간

    /// <summary>원본 렌더러의 현재 그림·반전·위치를 복사한 잔상 하나 생성</summary>
    public static SpriteAfterimage Spawn(SpriteRenderer source, Color tint, float duration, int sortingOrder, Material material, Transform parent)
    {
        // 그릴 그림이 없으면 빈 객체를 남기지 않음
        if (source == null || source.sprite == null) return null;
        var ghost = new GameObject("Afterimage");
        ghost.transform.SetParent(parent, false);
        ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        ghost.transform.localScale = source.transform.lossyScale;
        var renderer = ghost.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.flipX = source.flipX;
        renderer.sortingOrder = sortingOrder;
        if (material != null) renderer.sharedMaterial = material;
        var image = ghost.AddComponent<SpriteAfterimage>();
        image.Duration = duration;
        image.baseColor = tint;
        image.StartOpacity = tint.a;
        image.spriteRenderer = renderer;
        renderer.color = tint;
        return image;
    }

    /// <summary>게임 시간으로 알파를 줄이고 끝나면 제거</summary>
    private void Update()
    {
        if (Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        elapsed += Time.deltaTime;
        if (spriteRenderer == null || Duration <= 0f || elapsed >= Duration)
        {
            Destroy(gameObject);
            return;
        }
        Color color = baseColor;
        color.a = StartOpacity * (1f - elapsed / Duration);
        spriteRenderer.color = color;
    }
}
