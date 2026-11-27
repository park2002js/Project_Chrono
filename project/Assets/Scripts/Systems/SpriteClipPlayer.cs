using UnityEngine;

/// <summary>SpriteClip 한 개를 게임 시간으로 재생하고 끝나면 숨기거나 제거하는 효과 재생기</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteClipPlayer : MonoBehaviour
{
    public SpriteClip Clip; // 활성화할 때 자동 재생하거나 마지막으로 재생한 클립
    public bool PlayOnEnable; // 활성화 순간 Clip을 처음부터 재생할지 설정
    public bool DestroyOnFinish; // 단발 재생이 끝나면 이 객체를 제거할지 설정
    public float Speed = 1f; // 클립 시간에 곱할 재생 배율
    public bool IsPlaying { get; private set; } // 현재 프레임을 계속 갱신하는 재생 상태
    public float Elapsed { get; private set; } // 이번 재생에서 진행한 클립 시간

    private SpriteRenderer spriteRenderer; // 프레임을 표시할 자기 렌더러

    /// <summary>자기 렌더러를 한 번 확보</summary>
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (PlayOnEnable && Clip != null) Play(Clip, Speed);
    }

    /// <summary>게임 시간으로 프레임을 진행하고 정지 중에는 현재 프레임 유지</summary>
    private void Update()
    {
        if (!IsPlaying || Time.timeScale <= 0f || Time.deltaTime <= 0f) return;
        Tick(Time.deltaTime);
    }

    private void OnDisable()
    {
        IsPlaying = false;
    }

    /// <summary>지정한 클립을 처음부터 재생</summary>
    public void Play(SpriteClip clip, float speed = 1f)
    {
        // 재생할 내용이 없으면 표시만 숨김
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        Clip = clip;
        Speed = speed;
        Elapsed = 0f;
        IsPlaying = clip != null && clip.FrameCount > 0 && speed > 0f;
        if (!IsPlaying)
        {
            spriteRenderer.enabled = false;
            return;
        }
        Apply();
    }

    /// <summary>클립 전체가 지정한 시간에 맞도록 배율을 계산해 재생</summary>
    public void PlayFor(SpriteClip clip, float duration)
    {
        float length = clip != null ? clip.GetLength() : 0f;
        Play(clip, duration > 0f && length > 0f ? length / duration : 1f);
    }

    /// <summary>재생을 멈추고 표시를 숨김</summary>
    public void Stop()
    {
        IsPlaying = false;
        Elapsed = 0f;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    /// <summary>외부에서 전달한 게임 시간만큼 재생을 진행</summary>
    public void Tick(float gameDelta)
    {
        if (!IsPlaying || gameDelta <= 0f) return;
        Elapsed += gameDelta * Speed;
        Apply();
    }

    /// <summary>월드의 한 지점에 단발 효과를 만들고 재생이 끝나면 스스로 제거</summary>
    public static SpriteClipPlayer Spawn(SpriteClip clip, Vector3 position, int sortingOrder, Material material, Transform parent = null, float speed = 1f)
    {
        // 클립이 없으면 빈 객체를 남기지 않음
        if (clip == null || clip.FrameCount == 0) return null;
        var effect = new GameObject(clip.name);
        effect.transform.SetParent(parent, false);
        effect.transform.position = position;
        var renderer = effect.AddComponent<SpriteRenderer>();
        if (material != null) renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        var player = effect.AddComponent<SpriteClipPlayer>();
        player.DestroyOnFinish = true;
        player.Play(clip, speed);
        return player;
    }

    /// <summary>현재 경과 시간의 프레임을 표시하고 단발 재생의 끝을 처리</summary>
    private void Apply()
    {
        int index = Clip.GetFrameIndex(Elapsed);
        if (index < 0)
        {
            // 끝난 단발 효과는 숨기고 필요하면 객체까지 정리
            IsPlaying = false;
            spriteRenderer.enabled = false;
            if (DestroyOnFinish) Destroy(gameObject);
            return;
        }
        Sprite frame = Clip.Frames[index];
        spriteRenderer.sprite = frame;
        spriteRenderer.enabled = frame != null;
    }
}
