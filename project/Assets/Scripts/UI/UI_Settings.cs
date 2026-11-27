using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>저장 슬롯 프레임 안에 사운드·화면 묶음을 두고 음량은 즉시, 해상도·화면 모드는 확정 때 적용하는 설정 창</summary>
public class UI_Settings : MonoBehaviour
{
    public Font SettingsFont; // 제목·항목·버튼에 사용할 PF스타더스트
    public Texture2D Frame; // 12px 모서리를 보존해 창 겉틀로 늘려 쓸 저장 슬롯 픽셀 프레임
    [Range(0.01f, 0.25f)] public float WheelStep = 0.05f; // 음량 줄 위에서 마우스 휠 한 칸에 바뀌는 배율
    public bool IsOpen { get; private set; } // 창 표시와 입력 수락 여부
    public event Action OnClosed; // 창을 닫아 설정을 저장한 뒤 여는 쪽에 알릴 사건

    private static readonly Color32 TextColor = new Color32(161, 171, 195, 255); // 항목 이름과 음량 백분율 색
    private static readonly Color32 AccentColor = new Color32(122, 245, 255, 255); // 확정 전 값·켜진 확정 버튼·마우스를 올린 화살표 색
    private static readonly Color32 DimColor = new Color32(91, 102, 128, 255); // 꺼진 확정 버튼과 안내 문구 색
    private static readonly Color32 EdgeColor = new Color32(28, 47, 76, 255); // 구분선·항목 칸 테두리·화살표 바탕 색
    private static readonly Color32 EdgeLitColor = new Color32(37, 62, 96, 255); // 확정 전 값이 있는 해상도 칸의 밝은 테두리 색
    private static readonly Color32 InkColor = new Color32(7, 10, 18, 255); // 막대·버튼 바깥 윤곽 색
    private static readonly Color32 CellColor = new Color32(12, 19, 33, 255); // 항목 칸과 버튼 안쪽 색
    private static readonly Color32 FillColor = new Color32(62, 150, 182, 255); // 현재 음량까지 채운 막대 색
    private static readonly Color32 FillTopColor = new Color32(96, 190, 215, 255); // 채운 막대 윗줄의 밝은 색
    private static readonly Color32 HandleShadeColor = new Color32(190, 200, 220, 255); // 막대 손잡이 아래쪽 그늘 색
    private static readonly Vector2Int[] ChevronRight = { new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2), new Vector2Int(1, 3), new Vector2Int(0, 4) }; // 오른쪽 화살표 꺾쇠의 2px 폭 시작점

    private InputAction _backAction; // 창이 열린 동안에만 Esc 닫기를 받을 자기 입력
    private int[] _pixelScales; // 창을 열 때 모니터 크기로 계산한 선택 가능한 도트 배율
    private int _pendingScale; // 화살표로 고른 뒤 확정을 기다리는 도트 배율
    private bool _pendingFullScreen; // 화살표로 고른 뒤 확정을 기다리는 전체 화면 여부
    private bool _resolutionTouched; // 이번에 연 창에서 해상도 화살표를 누른 적이 있는지 여부
    private GUIStyle _headingStyle; // 상단 settings 제목 스타일
    private GUIStyle _sectionStyle; // 사운드·화면 묶음 제목 스타일
    private GUIStyle _labelStyle; // 항목 이름의 왼쪽 정렬 스타일
    private GUIStyle _percentStyle; // 음량 백분율의 오른쪽 정렬 스타일
    private GUIStyle _valueStyle; // 해상도·화면 모드 값의 가운데 정렬 스타일
    private GUIStyle _hintStyle; // 확정 안내 문구의 작은 글자 스타일
    private GUIStyle _frameStyle; // 저장 슬롯과 같은 픽셀 프레임 창 스타일
    private GUIStyle _hitStyle; // 직접 그린 버튼 위에서 클릭만 받는 보이지 않는 스타일

    /// <summary>자기 창에서만 사용할 Esc 입력 액션을 한 번 준비</summary>
    private void Awake()
    {
        _backAction = new InputAction("SettingsBack", InputActionType.Button, "<Keyboard>/escape");
    }

    /// <summary>열린 동안 다른 화면 위에 640×360 원본 픽셀을 3배로 확대한 설정 창 표시</summary>
    private void OnGUI()
    {
        // 닫힌 창은 그리지 않고 GUI.skin을 쓸 수 있는 첫 호출에서 스타일 준비
        if (!IsOpen)
            return;
        if (_labelStyle == null)
            CreateStyles();

        // 다른 OnGUI보다 위에 그리고 먼저 입력을 받으며, 원래 GUI 상태를 보관
        GUI.depth = -10;
        Matrix4x4 originalMatrix = GUI.matrix;
        Color originalColor = GUI.color;
        bool originalEnabled = GUI.enabled;
        GUI.enabled = true;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Title과 같이 1920×1080을 비율 유지로 맞춘 뒤 원본 픽셀 3배로 제목과 겉틀 표시
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920f * scale) * 0.5f, (Screen.height - 1080f * scale) * 0.5f, 0f), Quaternion.identity, Vector3.one * scale * 3f);
        GUI.Label(new Rect(80f, 34f, 480f, 36f), "settings", _headingStyle);
        GUI.Box(new Rect(152f, 76f, 336f, 216f), GUIContent.none, _frameStyle);

        // 사운드 묶음은 막대를 움직이는 즉시 반영
        GUI.Label(new Rect(178f, 88f, 120f, 16f), "사운드", _sectionStyle);
        FillRect(176f, 106f, 288f, 1f, EdgeColor);
        GameSettings.SetBgmVolume(DrawVolumeRow(112f, "배경음", GameSettings.BgmVolume));
        GameSettings.SetSfxVolume(DrawVolumeRow(136f, "효과음", GameSettings.SfxVolume));

        // 화면 묶음은 화살표로 값만 고르고 확정 전까지 실제 화면 유지
        GUI.Label(new Rect(178f, 164f, 120f, 16f), "화면", _sectionStyle);
        FillRect(176f, 182f, 288f, 1f, EdgeColor);
        bool resolutionPending = IsResolutionPending();
        bool modePending = _pendingFullScreen != GameSettings.IsFullScreen;
        string resolution = _resolutionTouched || GameSettings.HasResolutionChoice ? $"{GameSettings.BasePixelWidth * _pendingScale} x {GameSettings.BasePixelHeight * _pendingScale}" : $"{Screen.width} x {Screen.height}";
        int step = DrawChoiceRow(188f, "해상도", resolution, resolutionPending);
        if (step != 0)
            StepResolution(step);
        if (DrawChoiceRow(212f, "화면 모드", _pendingFullScreen ? "전체 화면" : "창 모드", modePending) != 0)
            _pendingFullScreen = !_pendingFullScreen;

        // 확정은 고른 값이 있을 때만 켜지고, 닫기는 고르던 값을 버린 채 창을 닫음
        bool pending = resolutionPending || modePending;
        if (DrawPillButton(new Rect(236f, 244f, 76f, 20f), "확정", pending, pending))
            ConfirmDisplay();
        if (DrawPillButton(new Rect(328f, 244f, 76f, 20f), "닫기", true, false))
            Close();
        if (pending)
            GUI.Label(new Rect(172f, 266f, 296f, 10f), "확정을 누르면 해상도와 화면 모드가 바뀝니다", _hintStyle);

        // 창 아래 화면의 버튼이 같은 클릭·휠을 받지 않도록 남은 마우스 사건 소비
        if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel)
            Event.current.Use();

        // 다른 화면이 사용할 GUI 공통 상태 복원
        GUI.matrix = originalMatrix;
        GUI.color = originalColor;
        GUI.enabled = originalEnabled;
    }

    private void OnEnable()
    {
        _backAction.performed += HandleBack; // 활성 창의 Esc 닫기 연결
        if (IsOpen)
            _backAction.Enable();
    }

    private void OnDisable()
    {
        _backAction.Disable();
        _backAction.performed -= HandleBack; // 비활성 창이 Esc를 처리하지 않도록 연결 해제
        if (IsOpen)
            GameSettings.Save();
    }

    private void OnDestroy()
    {
        _backAction.Dispose();
    }

    /// <summary>모니터에 맞는 해상도 목록과 현재 적용값으로 고르는 값을 초기화하고 창과 Esc 닫기를 엶</summary>
    public void Open()
    {
        if (IsOpen)
            return;
        _pixelScales = GameSettings.GetPixelScales();
        _pendingScale = GameSettings.PixelScale;
        _pendingFullScreen = GameSettings.IsFullScreen;
        _resolutionTouched = false;
        IsOpen = true;
        _backAction.Enable();
    }

    /// <summary>확정하지 않은 해상도·화면 모드는 버리고 음량을 저장한 뒤 여는 쪽에 알림</summary>
    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _backAction.Disable();
        GameSettings.Save();
        OnClosed?.Invoke();
    }

    /// <summary>Input System의 Esc 누름을 창 닫기 요청으로 전달</summary>
    private void HandleBack(InputAction.CallbackContext context)
    {
        Close();
    }

    /// <summary>고른 해상도·화면 모드를 실제 화면에 적용하고 저장</summary>
    private void ConfirmDisplay()
    {
        GameSettings.ApplyResolution(_pendingScale, _pendingFullScreen);
        GameSettings.Save();
        _resolutionTouched = false;
    }

    /// <summary>고른 해상도가 현재 적용된 해상도와 다른지 확인</summary>
    private bool IsResolutionPending()
    {
        return _resolutionTouched && (!GameSettings.HasResolutionChoice || _pendingScale != GameSettings.PixelScale);
    }

    /// <summary>고르는 배율에서 가장 가까운 목록 위치를 찾아 한 칸 옮긴 값을 확정 대기로 보관</summary>
    private void StepResolution(int step)
    {
        // 목록에 없는 배율은 가장 가까운 칸을 기준으로 이동
        int index = 0;
        for (int i = 1; i < _pixelScales.Length; i++)
        {
            if (Mathf.Abs(_pixelScales[i] - _pendingScale) < Mathf.Abs(_pixelScales[index] - _pendingScale))
                index = i;
        }

        // 고른 적 없는 화면은 첫 클릭에서 가까운 정수배를 보여 주고 이후 한 칸씩 이동
        bool exact = (_resolutionTouched || GameSettings.HasResolutionChoice) && _pixelScales[index] == _pendingScale;
        _pendingScale = _pixelScales[Mathf.Clamp(exact ? index + step : index, 0, _pixelScales.Length - 1)];
        _resolutionTouched = true;
    }

    /// <summary>항목 칸·이름·막대·백분율 한 줄을 그리고 끌기와 마우스 휠로 바뀐 1% 단위 음량 반환</summary>
    private float DrawVolumeRow(float y, string label, float value)
    {
        // 줄 전체에서 받은 휠은 막대를 끌지 않아도 음량을 한 칸씩 조절
        Event current = Event.current;
        if (current.type == EventType.ScrollWheel && new Rect(172f, y, 296f, 20f).Contains(current.mousePosition))
        {
            value = Mathf.Clamp01(value - Mathf.Sign(current.delta.y) * WheelStep);
            current.Use();
        }

        // 막대 위 누름과 끌기는 이 줄의 컨트롤이 잡고 놓을 때까지 손잡이 위치를 따라감
        Rect track = new Rect(252f, y + 7f, 160f, 6f);
        int control = GUIUtility.GetControlID(FocusType.Passive);
        switch (current.GetTypeForControl(control))
        {
            case EventType.MouseDown:
                if (current.button == 0 && new Rect(track.x - 4f, y, track.width + 8f, 20f).Contains(current.mousePosition))
                {
                    GUIUtility.hotControl = control;
                    value = Mathf.Clamp01((current.mousePosition.x - track.x - 1f) / (track.width - 2f));
                    current.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == control)
                {
                    value = Mathf.Clamp01((current.mousePosition.x - track.x - 1f) / (track.width - 2f));
                    current.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == control)
                {
                    GUIUtility.hotControl = 0;
                    current.Use();
                }
                break;
        }
        value = Mathf.Round(value * 100f) / 100f;

        // 항목 칸 위에 빈 막대·채운 부분·흰 손잡이 순서로 그림
        DrawCell(172f, y, false);
        GUI.Label(new Rect(182f, y, 64f, 20f), label, _labelStyle);
        float fill = Mathf.Round((track.width - 2f) * value);
        FillRect(track.x, track.y, track.width, track.height, InkColor);
        FillRect(track.x + 1f, track.y + 1f, track.width - 2f, track.height - 2f, EdgeColor);
        FillRect(track.x + 1f, track.y + 1f, fill, track.height - 2f, FillColor);
        FillRect(track.x + 1f, track.y + 1f, fill, 1f, FillTopColor);
        float handle = track.x + 1f + fill - 2f;
        FillRect(handle - 1f, track.y - 4f, 6f, 14f, InkColor);
        FillRect(handle, track.y - 3f, 4f, 12f, Color.white);
        FillRect(handle, track.y + 7f, 4f, 2f, HandleShadeColor);
        GUI.Label(new Rect(418f, y, 44f, 20f), $"{Mathf.RoundToInt(value * 100f)}%", _percentStyle);
        return value;
    }

    /// <summary>항목 칸·이름과 좌우 화살표 사이의 값을 그리고 누른 방향(-1·0·1) 반환</summary>
    private int DrawChoiceRow(float y, string label, string value, bool pending)
    {
        DrawCell(172f, y, pending);
        GUI.Label(new Rect(182f, y, 64f, 20f), label, _labelStyle);
        int step = 0;
        if (DrawArrowButton(new Rect(252f, y + 4f, 12f, 12f), false))
            step = -1;
        if (DrawArrowButton(new Rect(400f, y + 4f, 12f, 12f), true))
            step = 1;
        _valueStyle.normal.textColor = pending ? (Color)AccentColor : Color.white;
        GUI.Label(new Rect(266f, y, 132f, 20f), value, _valueStyle);
        return step;
    }

    /// <summary>모서리 1px를 깎은 항목 칸을 그리고 확정 전 값이 있으면 밝은 테두리 사용</summary>
    private void DrawCell(float x, float y, bool lit)
    {
        Color edge = lit ? EdgeLitColor : EdgeColor;
        FillRect(x + 1f, y, 294f, 20f, edge);
        FillRect(x, y + 1f, 296f, 18f, edge);
        FillRect(x + 1f, y + 1f, 294f, 18f, CellColor);
    }

    /// <summary>둥근 12px 바탕에 2px 꺾쇠를 그리고 마우스를 올리면 청록으로 바꾼 화살표 버튼</summary>
    private bool DrawArrowButton(Rect rect, bool right)
    {
        // 바깥 윤곽과 바탕
        FillRect(rect.x + 1f, rect.y, 10f, 12f, InkColor);
        FillRect(rect.x, rect.y + 1f, 12f, 10f, InkColor);
        FillRect(rect.x + 1f, rect.y + 1f, 10f, 10f, EdgeColor);

        // 방향에 맞춰 좌우를 뒤집은 꺾쇠
        Color color = rect.Contains(Event.current.mousePosition) ? (Color)AccentColor : TextColor;
        foreach (Vector2Int point in ChevronRight)
            FillRect(rect.x + 4f + (right ? point.x : 2 - point.x), rect.y + 4f + point.y, 2f, 1f, color);
        return GUI.Button(rect, GUIContent.none, _hitStyle);
    }

    /// <summary>모서리를 깎은 알약 모양 버튼을 그리고 켜짐·청록 강조에 따라 테두리와 글자색 선택</summary>
    private bool DrawPillButton(Rect rect, string label, bool enabled, bool accent)
    {
        // 바깥 윤곽·테두리·안쪽 칸
        Color edge = accent ? (Color)AccentColor : enabled ? TextColor : DimColor;
        FillRect(rect.x + 2f, rect.y, rect.width - 4f, rect.height, InkColor);
        FillRect(rect.x, rect.y + 2f, rect.width, rect.height - 4f, InkColor);
        FillRect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f, InkColor);
        FillRect(rect.x + 2f, rect.y + 1f, rect.width - 4f, rect.height - 2f, edge);
        FillRect(rect.x + 1f, rect.y + 2f, rect.width - 2f, rect.height - 4f, edge);
        FillRect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f, CellColor);

        // 켜진 버튼만 마우스를 올리면 글자를 청록으로 바꾸고 클릭 수락
        bool hover = enabled && rect.Contains(Event.current.mousePosition);
        _valueStyle.normal.textColor = accent || hover ? (Color)AccentColor : enabled ? Color.white : DimColor;
        GUI.Label(rect, label, _valueStyle);
        return enabled && GUI.Button(rect, GUIContent.none, _hitStyle);
    }

    /// <summary>흰 텍스처를 지정 색으로 칠해 원본 픽셀 단위 사각형을 그리고 GUI 색 복원</summary>
    private void FillRect(float x, float y, float width, float height, Color color)
    {
        if (width <= 0f || height <= 0f)
            return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    /// <summary>Title 저장 화면과 같은 글자 크기·색·프레임으로 창 스타일을 한 번 생성</summary>
    private void CreateStyles()
    {
        // 글자는 배경 없이 그리고 정렬·색만 용도별로 나눔
        _labelStyle = new GUIStyle { font = SettingsFont, fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        _labelStyle.normal.textColor = TextColor;
        _percentStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleRight };
        _valueStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter };
        _sectionStyle = new GUIStyle(_labelStyle);
        _sectionStyle.normal.textColor = Color.white;
        _headingStyle = new GUIStyle(_sectionStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        _hintStyle = new GUIStyle(_valueStyle) { fontSize = 8 };
        _hintStyle.normal.textColor = DimColor;

        // 겉틀은 12px 모서리를 보존해 늘리고, 직접 그린 버튼은 보이지 않는 스타일로 클릭만 받음
        _frameStyle = new GUIStyle { border = new RectOffset(12, 12, 12, 12) };
        _frameStyle.normal.background = Frame;
        _hitStyle = new GUIStyle();
    }
}
