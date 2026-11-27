using UnityEngine;

/// <summary>Draw의 임시 암전과 점 편집 안내만 표시하는 전용 화면</summary>
[DefaultExecutionOrder(500)]
public class UI_Draw : MonoBehaviour
{
    public GameObject ShadePrefab; // Player와 점·선 아래에 그릴 교체 가능한 임시 암전 프리팹
    public Font UIFont; // 키 안내 문구에 사용할 PF스타더스트
    public Sprite ResetKey; // 점 초기화에 연결한 R 키 그림
    public Sprite ConfirmKey; // 선 확정에 연결한 F 키 그림
    public Sprite CancelKey; // Draw 취소에 연결한 Shift 키 그림
    public bool IsVisible { get; private set; } // Draw 안내·암전의 현재 표시 여부
    public SpriteRenderer ScreenShade { get; private set; } // 카메라 시야에 맞춰 크기를 조절할 암전 Sprite

    private Camera worldCamera; // Start에 한 번 확보할 MainWorld 카메라
    private SkillController source; // 화면 안내에서 편집 상태를 조회할 현재 Draw 능력
    private GameObject shade; // 카메라 자식으로 한 번 생성해 재사용할 암전 객체
    private GUIStyle guideStyle; // 첫 GUI 호출에서 준비할 흰색 키 안내 글자 스타일

    /// <summary>카메라의 Awake 이후 임시 암전을 한 번 준비</summary>
    private void Start()
    {
        GameCamera gameCamera = GameCamera.Instance;
        worldCamera = gameCamera != null ? gameCamera.Camera : null;
        if (worldCamera == null || ShadePrefab == null)
        {
            Debug.LogError($"{name}: Draw UI requires the camera and shade prefab.", this);
            return;
        }
        shade = Instantiate(ShadePrefab, worldCamera.transform);
        shade.transform.localPosition = new Vector3(0f, 0f, 1f);
        ScreenShade = shade.GetComponent<SpriteRenderer>();
        shade.SetActive(false);
    }

    /// <summary>정지 시간에도 카메라 줌에 맞춰 암전이 시야를 덮도록 갱신</summary>
    private void LateUpdate()
    {
        if (!IsVisible || shade == null || worldCamera == null) return;
        float height = worldCamera.orthographicSize * 2f;
        shade.transform.localScale = new Vector3(height * worldCamera.aspect, height, 1f);
    }

    /// <summary>좌하단에 Draw 전용 조작과 기능 이름 표시</summary>
    private void OnGUI()
    {
        if (!IsVisible || source == null) return;

        // 첫 표시에서 스타일을 준비하고 1080 기준 크기를 현재 화면 높이에 맞춤
        float scale = Screen.height / 1080f;
        if (guideStyle == null)
        {
            guideStyle = new GUIStyle(GUI.skin.label) { font = UIFont, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            guideStyle.normal.textColor = Color.white;
        }
        guideStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(32f * scale));

        // 아래 여백부터 위로 네 줄을 쌓고, 점이 모자란 동안 확정 줄은 흐리게 표시
        GUI.Label(new Rect(36f * scale, Screen.height - 264f * scale, 400f * scale, 48f * scale), "좌클릭 - 점 찍기", guideStyle); // 마우스 그림이 없어 글자만 표시
        DrawKeyGuide(ResetKey, "점 초기화", 2, scale, 1f);
        DrawKeyGuide(ConfirmKey, "선 확정", 1, scale, source.Drawer.CanConfirm ? 1f : 0.4f);
        DrawKeyGuide(CancelKey, "취소", 0, scale, 1f);
    }

    private void OnDestroy()
    {
        if (shade != null) Destroy(shade);
    }

    /// <summary>능력 편집과 분리된 암전·안내 표시 여부 변경</summary>
    public void SetVisible(bool visible, SkillController skill)
    {
        IsVisible = visible;
        source = visible ? skill : null;
        if (shade != null) shade.SetActive(visible);
    }

    /// <summary>아래에서 지정한 번째 줄에 키 그림 한 개와 기능 이름 표시</summary>
    private void DrawKeyGuide(Sprite key, string label, int rowFromBottom, float scale, float alpha)
    {
        // 16px 키 원본을 HUD 키 안내와 같은 3배 크기로 배치
        float size = 48f * scale;
        float x = 36f * scale;
        float y = Screen.height - 36f * scale - size - rowFromBottom * 60f * scale;

        // 해당 줄의 농도를 적용해 그린 뒤 GUI 색 복원
        Color color = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        if (key != null) GUI.DrawTexture(new Rect(x, y, size, size), key.texture);
        GUI.Label(new Rect(x + size + 14f * scale, y, 320f * scale, size), label, guideStyle);
        GUI.color = color;
    }
}
