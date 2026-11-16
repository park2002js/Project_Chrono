using UnityEngine;

/// <summary>Draw의 임시 암전과 점 편집 안내만 표시하는 전용 화면</summary>
[DefaultExecutionOrder(500)]
public class UI_Draw : MonoBehaviour
{
    public GameObject ShadePrefab; // Player와 점·선 아래에 그릴 교체 가능한 임시 암전 프리팹
    public bool IsVisible { get; private set; } // Draw 안내·암전의 현재 표시 여부
    public SpriteRenderer ScreenShade { get; private set; } // 카메라 시야에 맞춰 크기를 조절할 암전 Sprite

    private Camera worldCamera; // Start에 한 번 확보할 MainWorld 카메라
    private SkillController source; // 화면 안내에서 편집 상태를 조회할 현재 Draw 능력
    private GameObject shade; // 카메라 자식으로 한 번 생성해 재사용할 암전 객체

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

    /// <summary>현재 점 개수와 Draw 전용 조작 안내 표시</summary>
    private void OnGUI()
    {
        if (!IsVisible || source == null) return;
        string confirm = source.Drawer.CanConfirm ? "F: Confirm" : "F: Need 2 points";
        GUI.Box(new Rect(Screen.width * 0.5f - 250f, Screen.height - 100f, 500f, 70f),
            $"DRAW   {source.Drawer.PointCount}/2\nM1: Point   R: Reset   {confirm}   Shift: Cancel");
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
}
