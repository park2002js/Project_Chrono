using UnityEngine;

/// <summary>교체 가능한 단위 규격 프리팹으로 Draw의 점·임시 경로·확정 경로 표시</summary>
public class DrawVfx : MonoBehaviour
{
    public GameObject SlashPrefab; // Slash 판정·정지 해제 직후 번쩍일 교체 가능한 검격 프리팹
    public GameObject SlashObject { get; private set; } // 검격마다 다시 생성하지 않고 재사용할 월드 표현
    public bool IsSlashPlaying { get; private set; } // 실제 시간으로 진행하는 검격 표현의 실행 여부
    public float SlashLengthMultiplier = 1.15f; // 원래 Line보다 길게 양 끝을 확장할 임시 표현 배율
    public float SlashWidthMultiplier = 0.35f; // 원래 Line보다 얇게 표시할 임시 표현 배율
    public float SlashFlashDuration = 0.12f; // 판정·정지 해제 순간 번쩍일 검격의 실제 재생 시간
    public GameObject PointPrefab; // 월드 좌표에 표시할 점 연출을 교체할 프리팹
    public GameObject PreviewLinePrefab; // 단위 X 길이·단위 Y 폭의 미리보기 연출이며 없으면 LinePrefab 재사용
    public GameObject LinePrefab; // 단위 X 길이·단위 Y 폭으로 만든 확정 경로 연출 프리팹
    public GameObject ExpiryLinePrefab; // 자연 만료 전용 경로 표현이며 없으면 LinePrefab 재사용
    public GameObject[] PointObjects { get; private set; } = new GameObject[2]; // 편집마다 재사용하는 두 입력 점의 표현 객체
    public GameObject PreviewObject { get; private set; } // 커서 미리보기와 선택 완료 경로를 표시할 재사용 객체
    public GameObject LineObject { get; private set; } // 편집 중에도 기존 확정 경로를 유지할 별도 표현 객체
    public GameObject ExpiryObject { get; private set; } // 자연 만료 때만 재사용할 별도 소멸 표현 객체
    public bool IsPrepared { get; private set; } // 같은 점·선 객체를 중복 생성하지 않도록 준비 완료 상태 보관
    public float WarningFraction = 0.35f; // 남은 수명이 이 비율 이하일 때 시작할 깜박임 예고
    public float SlowBlinkPeriod = 0.5f; // 예고 시작 시 깜박임 한 주기의 초기 게임 시간
    public float FastBlinkPeriod = 0.08f; // 만료 직전 깜박임 한 주기의 초기 게임 시간
    public float ExpiryDuration = 0.25f; // 게임 경로가 사라진 뒤 남길 소멸 표현의 초기 재생 시간
    public float LineRemainingRatio { get; private set; } // Skill이 전달한 현재 경로의 잔여 수명 비율
    public float BlinkInterval { get; private set; } // 현재 예고의 한 주기이며 예고 전에는 0
    public float LifePhase { get; private set; } // 게임 시간으로만 진행하는 깜박임 주기의 0~1 위상
    public bool IsLineVisible { get; private set; } // 현재 깜박임이 요구하는 확정 선의 표시 상태
    public bool IsExpiryPlaying => expiryRemaining > 0f; // 자연 만료 후 소멸 표현이 재생 중인지 조회

    private bool hasLineVisual; // 게임 데이터와 별개로 현재 확정 선 표현이 준비됐는지 보관
    private bool lineSuspended; // 능력 비활성화 중 표현을 숨기되 위상은 보존할 상태
    private float expiryRemaining; // 게임 Line 수명과 별개인 소멸 표현의 남은 재생 시간
    private float expiryTotal; // 재생 시작 순간에 고정한 소멸 표현의 전체 시간
    private float expiryWidth; // 소멸 시작 전 선 폭을 기준으로 축소할 표현 크기
    private float slashWidth; // 검격 시작 순간의 표현 폭이며 진행에 따라 축소할 기준값
    private float slashVisualRemaining; // 게임 논리와 별개로 끝까지 재생할 검격 표현의 남은 실제 시간
    private float slashVisualTotal; // 이번 검격 표현 시작 순간에 고정한 전체 재생 시간

    /// <summary>자기 프리팹 참조만 사용해 편집과 확정 경로의 표현 객체를 한 번 준비</summary>
    private void Awake()
    {
        Prepare();
    }

    /// <summary>게임 정지와 무관하게 판정 순간 시작한 검격 표현만 진행</summary>
    private void Update()
    {
        if (!IsSlashPlaying) return;
        slashVisualRemaining = Mathf.Max(0f, slashVisualRemaining - Time.unscaledDeltaTime);
        SetSlashProgress(1f - slashVisualRemaining / slashVisualTotal);
        if (slashVisualRemaining <= 0f) ClearSlash();
    }

    private void OnDisable()
    {
        ClearEditing();
        ApplyLineVisibility();
    }

    private void OnEnable()
    {
        ApplyLineVisibility();
    }

    private void OnDestroy()
    {
        // Player와 독립된 월드 표현도 소유자가 제거될 때 함께 정리
        for (int i = 0; i < PointObjects.Length; i++)
            if (PointObjects[i] != null) Destroy(PointObjects[i]);
        if (PreviewObject != null) Destroy(PreviewObject);
        if (LineObject != null) Destroy(LineObject);
        if (ExpiryObject != null) Destroy(ExpiryObject);
        if (SlashObject != null) Destroy(SlashObject);
    }

    /// <summary>렌더러 종류를 고정하지 않고 지정한 표현 프리팹을 한 번씩만 생성</summary>
    public bool Prepare()
    {
        // 미연결 상태를 숨긴 채 Draw가 시작되지 않도록 준비 실패 보고
        if (IsPrepared) return true;
        if (PointPrefab == null || LinePrefab == null)
        {
            Debug.LogError($"{name}: DrawVfx requires PointPrefab and LinePrefab.", this);
            enabled = false;
            return false;
        }

        // Player 이동·회전에 확정 경로가 끌려가지 않도록 씬 루트에 생성
        for (int i = 0; i < PointObjects.Length; i++)
        {
            PointObjects[i] = Instantiate(PointPrefab, transform.position, Quaternion.identity);
            PointObjects[i].name = $"DrawPoint_{i + 1}";
        }
        PreviewObject = Instantiate(PreviewLinePrefab != null ? PreviewLinePrefab : LinePrefab, transform.position, Quaternion.identity);
        PreviewObject.name = "DrawPreviewLine";
        LineObject = Instantiate(LinePrefab, transform.position, Quaternion.identity);
        LineObject.name = "DrawConfirmedLine";
        ExpiryObject = Instantiate(ExpiryLinePrefab != null ? ExpiryLinePrefab : LinePrefab, transform.position, Quaternion.identity);
        ExpiryObject.name = "DrawExpiredLine";
        SlashObject = Instantiate(SlashPrefab != null ? SlashPrefab : LinePrefab, transform.position, Quaternion.identity);
        SlashObject.name = "SlashFlash";

        // 첫 편집 전에는 모든 슬롯을 숨기고 이후 동일한 객체 재사용
        IsPrepared = true;
        ClearAll();
        return true;
    }

    /// <summary>임시 점·경로만 갱신하고 기존 확정 경로의 표현은 유지</summary>
    public void ShowEditing(LineDrawer drawer)
    {
        // 준비되지 않거나 비활성화된 표현은 표시 요청 생략
        if (!IsPrepared || !isActiveAndEnabled) return;
        if (drawer == null || !drawer.IsDrawing)
        {
            ClearEditing();
            return;
        }

        // 선택한 두 입력 점의 좌표와 표시 상태 반영
        ShowPoint(PointObjects[0], drawer.PointCount > 0, drawer.StartPoint);
        ShowPoint(PointObjects[1], drawer.PointCount > 1, drawer.EndPoint);

        // 실제 경로 끝점은 두 번째 입력 위치 대신 고정 길이로 표시
        ShowPath(PreviewObject, drawer.PreviewLine);
    }

    /// <summary>소유자가 보관한 확정 경로만 반영하고 편집 표현과 별도로 유지</summary>
    public void ShowLine(Line? line)
    {
        if (!IsPrepared) return;
        ClearLineEffects();
        hasLineVisual = line.HasValue;
        LineRemainingRatio = line.HasValue ? 1f : 0f;
        IsLineVisible = line.HasValue;
        ShowPath(LineObject, line, false);
        ApplyLineVisibility();
    }

    /// <summary>Skill의 잔여 수명을 받아 후반으로 갈수록 빠르게 깜박이는 표현 적용</summary>
    public void UpdateLineLifetime(float remaining, float total, float deltaTime)
    {
        // 게임 수명은 저장하지 않고 전달받은 비율만 표시 상태에 반영
        if (!IsPrepared || !hasLineVisual || total <= 0f || deltaTime <= 0f) return;
        LineRemainingRatio = Mathf.Clamp01(remaining / total);
        float warning = Mathf.Clamp01(WarningFraction);

        // 예고 전에는 선을 유지하고 예고 구간에서는 같은 게임 시간으로 위상 진행
        if (warning <= 0f || LineRemainingRatio > warning)
        {
            BlinkInterval = 0f;
            LifePhase = 0f;
            IsLineVisible = true;
        }
        else
        {
            float progress = 1f - LineRemainingRatio / warning;
            BlinkInterval = Mathf.Lerp(Mathf.Max(0.01f, SlowBlinkPeriod), Mathf.Max(0.01f, FastBlinkPeriod), progress);
            LifePhase = Mathf.Repeat(LifePhase + deltaTime / BlinkInterval, 1f);
            IsLineVisible = LifePhase < 0.5f;
        }
        ApplyLineVisibility();
    }

    /// <summary>자연 만료한 경로의 기하로 전용 소멸 표현 재생</summary>
    public void PlayLineExpiration(Line line)
    {
        if (!IsPrepared) return;
        expiryTotal = Mathf.Max(0f, ExpiryDuration);
        expiryRemaining = expiryTotal;
        expiryWidth = line.Width;
        ShowPath(ExpiryObject, line, false);
        ApplyLineVisibility();
    }

    /// <summary>Skill이 전달한 게임 시간으로 소멸 표현만 진행하고 끝에서 숨김</summary>
    public void TickVisuals(float deltaTime)
    {
        if (!IsPrepared || deltaTime <= 0f || !IsExpiryPlaying) return;
        expiryRemaining = Mathf.Max(0f, expiryRemaining - deltaTime);
        Vector3 scale = ExpiryObject.transform.localScale;
        scale.y = expiryTotal > 0f ? expiryWidth * expiryRemaining / expiryTotal : 0f;
        ExpiryObject.transform.localScale = scale;
        ApplyLineVisibility();
    }

    /// <summary>새 확정·수동 해제·사망에서 이전 경고와 소멸 표현만 초기화</summary>
    public void ClearLineEffects()
    {
        LifePhase = 0f;
        BlinkInterval = 0f;
        expiryRemaining = expiryTotal = 0f;
        if (ExpiryObject != null) ExpiryObject.SetActive(false);
    }

    /// <summary>능력 비활성화에서 데이터를 지우지 않고 선 표현만 숨기거나 복구</summary>
    public void SetLineSuspended(bool suspended)
    {
        lineSuspended = suspended;
        ApplyLineVisibility();
    }

    /// <summary>확정 경로를 남긴 채 두 점과 편집 미리보기만 숨김</summary>
    public void ClearEditing()
    {
        for (int i = 0; i < PointObjects.Length; i++)
            if (PointObjects[i] != null) PointObjects[i].SetActive(false);
        if (PreviewObject != null) PreviewObject.SetActive(false);
    }

    /// <summary>소유자 종료·비활성화에서 모든 점·경로 표현 숨김</summary>
    public void ClearAll()
    {
        ClearSlash();
        ClearEditing();
        ClearLineEffects();
        hasLineVisual = false;
        IsLineVisible = false;
        LineRemainingRatio = 0f;
        if (LineObject != null) LineObject.SetActive(false);
    }

    /// <summary>판정 기하는 바꾸지 않고 같은 중심을 따라 더 얇고 긴 검격 표시</summary>
    public void PlaySlash(Line line)
    {
        if (!IsPrepared) return;
        float length = line.Length * Mathf.Max(1.01f, SlashLengthMultiplier);
        slashWidth = line.Width * Mathf.Clamp(SlashWidthMultiplier, 0.01f, 0.99f);
        Line flash = new Line(line.StartPoint - line.Direction * ((length - line.Length) * 0.5f), line.Direction, length, slashWidth);
        IsSlashPlaying = true;
        slashVisualTotal = Mathf.Max(0.01f, SlashFlashDuration);
        slashVisualRemaining = slashVisualTotal;
        ShowPath(SlashObject, flash, false);
        ApplyLineVisibility();
    }

    /// <summary>Skill이 전달한 실제 시간 진행률로 검격 폭 축소</summary>
    public void SetSlashProgress(float progress)
    {
        if (!IsPrepared || !IsSlashPlaying) return;
        Vector3 scale = SlashObject.transform.localScale;
        scale.y = slashWidth * (1f - Mathf.Clamp01(progress));
        SlashObject.transform.localScale = scale;
    }

    /// <summary>완료·중단에서 재사용 검격 표현 숨김</summary>
    public void ClearSlash()
    {
        IsSlashPlaying = false;
        slashVisualRemaining = 0f;
        if (SlashObject != null) SlashObject.SetActive(false);
    }

    /// <summary>깜박임과 컴포넌트 활성 상태를 합쳐 최신 표현의 표시 여부 결정</summary>
    private void ApplyLineVisibility()
    {
        bool visible = isActiveAndEnabled && !lineSuspended;
        if (LineObject != null) LineObject.SetActive(visible && hasLineVisual && IsLineVisible);
        if (ExpiryObject != null) ExpiryObject.SetActive(visible && IsExpiryPlaying);
        if (SlashObject != null) SlashObject.SetActive(visible && IsSlashPlaying);
    }

    /// <summary>점 연출의 월드 좌표를 먼저 반영한 뒤 표시 상태 변경</summary>
    private static void ShowPoint(GameObject point, bool visible, Vector2 position)
    {
        if (visible) point.transform.position = position;
        point.SetActive(visible);
    }

    /// <summary>단위 규격 프리팹에 경로의 월드 시작점·방향·길이·폭만 적용</summary>
    private static void ShowPath(GameObject visual, Line? line, bool visible = true)
    {
        // 확정 값이나 미리보기가 없으면 기존 객체만 숨김
        if (!line.HasValue)
        {
            visual.SetActive(false);
            return;
        }

        // 렌더러를 조회하지 않고 규격화한 프리팹의 Transform만 갱신
        Line value = line.Value;
        float angle = Mathf.Atan2(value.Direction.y, value.Direction.x) * Mathf.Rad2Deg;
        visual.transform.SetPositionAndRotation(value.StartPoint, Quaternion.Euler(0f, 0f, angle));
        visual.transform.localScale = new Vector3(value.Length, value.Width, 1f);
        visual.SetActive(visible);
    }
}
