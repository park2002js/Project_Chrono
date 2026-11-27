using UnityEngine;

/// <summary>문으로 나뉜 방과 앞뒤 통로를 Player 위치와 문의 여닫힘에 따라 밝히거나 검게 덮는 표시 담당</summary>
public class RoomDarkness : MonoBehaviour
{
    public MapDoor EntryDoor; // Player가 들어오는 쪽 문
    public MapDoor ExitDoor; // Boss 뒤의 나가는 쪽 문
    public Rect InteriorArea; // 이 객체 기준으로 방 안쪽을 덮을 범위
    public Rect EntryArea; // 이 객체 기준으로 입구 문과 그 바깥 통로를 덮을 범위이며 위쪽 끝이 방과 맞닿음
    public Rect ExitArea; // 이 객체 기준으로 출구 문 바깥 통로를 덮을 범위이며 아래쪽 끝이 방과 맞닿음
    public float EntryLineOffset; // 이 객체에서 입구 문턱까지의 세로 거리이며 이보다 위에 있으면 방 안으로 봄
    public float ExitLineOffset; // 이 객체에서 출구 문턱까지의 세로 거리이며 이보다 위에 있으면 출구 통로로 봄
    public float FadeDuration = 0.35f; // 밝아지거나 어두워지는 데 걸리는 실제 시간
    public float EdgeFadeLength = 1.8125f; // 통로 덮개가 방과 맞닿은 끝에서 완전히 검어질 때까지의 거리
    public Material SpriteMaterial; // 덮개를 다른 월드 Sprite와 같게 그릴 재질
    public int CoverOrder = 90; // 덮개를 캐릭터와 효과 위에 놓을 정렬 순서

    private enum Area
    {
        Entry, // 입구 문 바깥의 통로
        Room, // 두 문 사이의 방 안쪽
        Exit // 출구 문 바깥의 통로
    }

    private const float Pixel = 1f / PixelShapeTexture.PixelsPerUnit; // 원본 1px의 월드 길이
    private readonly SpriteRenderer[] covers = new SpriteRenderer[3]; // 방 안쪽·입구 통로·출구 통로 순서의 검은 덮개
    private readonly float[] darkness = new float[3]; // 덮개별 현재 불투명도
    private PlayerCharacter player; // 위치를 읽을 현재 세션의 Player
    private Area lastArea; // 직전 프레임의 Player 위치 구분
    private bool entered; // 이번 도전에서 Player가 방 안에 들어온 적이 있는지 여부
    private bool wasEntryOpen; // 직전 프레임에 입구 문이 열려 있었는지 여부
    private bool applied; // 첫 프레임의 상태를 전환 없이 반영했는지 여부

    /// <summary>세 구역의 검은 덮개를 만들고 처음에는 모두 가린 상태로 준비</summary>
    private void Awake()
    {
        // 방 안쪽은 고르게, 통로는 방과 맞닿은 끝에서 바깥으로 갈수록 짙어지게 덮음
        covers[0] = CreateCover("InteriorCover", InteriorArea, 0f, false);
        covers[1] = CreateCover("EntryCover", EntryArea, EdgeFadeLength, true);
        covers[2] = CreateCover("ExitCover", ExitArea, EdgeFadeLength, false);
        for (int i = 0; i < darkness.Length; i++) darkness[i] = 1f;
    }

    /// <summary>Player 이동과 문 상태가 정해진 뒤 구역별 밝기를 목표값으로 전환</summary>
    private void LateUpdate()
    {
        // Player가 교체되거나 아직 없으면 현재 세션의 Player를 다시 확보
        if (player == null || !player.isActiveAndEnabled)
            player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        Area area = Area.Entry;
        if (player != null)
        {
            float height = player.transform.position.y - transform.position.y;
            area = height < EntryLineOffset ? Area.Entry : height > ExitLineOffset ? Area.Exit : Area.Room;
        }
        bool entryOpen = EntryDoor != null && EntryDoor.WantsOpen;
        bool exitOpen = ExitDoor != null && ExitDoor.WantsOpen;

        // 닫힌 입구 너머로 옮겨진 재도전은 처음 들어오는 상태로 되돌림
        if (area == Area.Entry && lastArea == Area.Room && !wasEntryOpen) entered = false;
        if (area == Area.Room) entered = true;
        lastArea = area;
        wasEntryOpen = entryOpen;

        // 방은 들어온 뒤에만, 통로는 그 안에 있거나 그쪽 문이 열려 있을 때만 보임
        bool interiorLit = entered && (area == Area.Room || (area == Area.Entry ? entryOpen : exitOpen));
        bool entryLit = area == Area.Entry || entryOpen;
        bool exitLit = area == Area.Exit || exitOpen;
        float step = applied && FadeDuration > 0f ? Time.unscaledDeltaTime / FadeDuration : 1f;
        applied = true;
        Fade(0, interiorLit, step);
        Fade(1, entryLit, step);
        Fade(2, exitLit, step);
    }

    private void OnDestroy()
    {
        // 이 객체가 만든 덮개 텍스처와 Sprite 제거
        foreach (SpriteRenderer cover in covers)
        {
            if (cover == null || cover.sprite == null) continue;
            Texture2D texture = cover.sprite.texture;
            Destroy(cover.sprite);
            if (texture != null) Destroy(texture);
        }
    }

    /// <summary>범위 하나를 덮는 검은 면 생성. fadeLength가 있으면 방과 맞닿은 끝의 그 거리만큼은 원본 1px 단위로 점점 짙어짐</summary>
    private SpriteRenderer CreateCover(string coverName, Rect area, float fadeLength, bool roomAtTop)
    {
        // 세로 1px 줄마다 불투명도를 정한 폭 1px 텍스처를 가로로 늘려 사용
        int rows = Mathf.Max(1, Mathf.RoundToInt(area.height / Pixel));
        int fadeRows = Mathf.Clamp(Mathf.RoundToInt(fadeLength / Pixel), 0, rows);
        var pixels = new Color32[rows];
        for (int y = 0; y < rows; y++)
        {
            // 텍스처의 0행은 아래쪽이므로 방과 맞닿은 끝에서 떨어진 줄 수로 환산
            int fromRoom = roomAtTop ? rows - 1 - y : y;
            float t = fadeRows > 0 ? Mathf.Clamp01((fromRoom + 0.5f) / fadeRows) : 1f;
            pixels[y] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * t * t));
        }
        var texture = new Texture2D(1, rows, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, rows), new Vector2(0.5f, 0.5f), PixelShapeTexture.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;

        var child = new GameObject(coverName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = new Vector3(area.center.x, area.center.y, 0f);
        child.transform.localScale = new Vector3(area.width / Pixel, area.height / (rows * Pixel), 1f);
        var cover = child.AddComponent<SpriteRenderer>();
        if (SpriteMaterial != null) cover.sharedMaterial = SpriteMaterial;
        cover.sprite = sprite;
        cover.sortingOrder = CoverOrder;
        cover.color = Color.black;
        return cover;
    }

    /// <summary>덮개 하나의 불투명도를 밝음 0·어둠 1의 목표값으로 한 걸음 이동</summary>
    private void Fade(int index, bool lit, float step)
    {
        darkness[index] = Mathf.MoveTowards(darkness[index], lit ? 0f : 1f, step);
        covers[index].color = new Color(0f, 0f, 0f, darkness[index]);
        covers[index].enabled = darkness[index] > 0f;
    }
}
