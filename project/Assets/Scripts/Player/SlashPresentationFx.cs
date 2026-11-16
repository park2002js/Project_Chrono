using UnityEngine;

/// <summary>Slash의 칼날 섬광·납도음·공간참 화면 효과를 실제 시간으로 재생</summary>
[RequireComponent(typeof(AudioSource))]
public class SlashPresentationFx : MonoBehaviour
{
    public Material SpaceSlashMaterial; // 월드 화면을 Line 양쪽으로 어긋나게 할 원본 재질
    public Material BladeGlintMaterial; // 일부 뽑힌 칼날 중앙에 표시할 섬광 원본 재질
    public AudioClip SheathClip; // 납도 마지막 프레임에 맞출 짧은 검집 닫힘 제작음
    public AudioClip SpaceSlashClip; // 지연 뒤 실제 공간참 순간에 맞출 절단 제작음
    [Range(0f, 1f)] public float Volume = 0.8f; // 납도와 공간참 효과음의 공통 재생 음량
    public float SpaceSlashDuration = 0.28f; // 공간이 어긋난 뒤 원상태로 돌아올 실제 시간
    public float SpaceDisplacementPixels = 18f; // Line 양쪽 월드 화면이 각각 이동할 최대 화면 픽셀
    public float CutGlowPixels = 2.5f; // 실제 판정 폭 안에서 가는 절단빛 주변의 잔광을 펼칠 화면 픽셀 폭
    public Color CutColor = new Color(1f, 212f / 255f, 59f / 255f, 1f); // 단절된 두 화면에 걸쳐 남는 절단빛의 노란 잔광색
    public float BladeGlintSize = 0.65f; // 칼날 중앙 섬광의 월드 공간 한 변 길이
    public Color BladeGlintColor = new Color(0.75f, 0.95f, 1f, 1f); // 칼날 섬광의 흰 중심 주변 강조색
    public Camera BoundCamera { get; private set; } // 준비 단계에서 한 번 연결한 월드 전용 카메라
    public Material SpaceMaterial { get; private set; } // 다른 Player의 연출 값을 덮지 않을 실행 전용 재질
    public bool IsSpaceSlashPlaying => spaceRemaining > 0f; // 월드 화면의 어긋남 복원이 진행 중인지 조회
    public bool IsBladeGlintPlaying => glintRemaining > 0f; // 칼날 중앙 섬광의 실제 시간 재생 여부
    public static SlashPresentationFx ActiveSpaceSlash { get; private set; } // 렌더러가 현재 공간참 소유자와 카메라만 확인할 창구

    private AudioSource audioSource; // 게임 시간 정지 중에도 효과음을 재생할 자기 오디오 소스
    private Material glintMaterial; // 섬광 강도만 독립적으로 갱신할 실행 전용 재질
    private GameObject glintObject; // Slash마다 생성하지 않고 위치와 표시만 바꿀 섬광 객체
    private Mesh glintMesh; // 칼날 섬광의 절차적 셰이더를 그릴 재사용 사각 메시
    private Line spaceLine; // 이번 공간참의 카메라 사영에 사용할 확정 Line 스냅샷
    private float spaceRemaining; // 월드 화면 어긋남이 끝나기까지 남은 실제 시간
    private float spaceTotal; // 중간 Inspector 변경과 구분한 이번 공간참의 전체 시간
    private float glintRemaining; // 칼날 중앙 섬광이 끝나기까지 남은 실제 시간
    private float glintTotal; // 이번 칼날 섬광의 전체 실제 시간
    private bool prepared; // 메시·재질·외부 카메라의 중복 준비 방지

    /// <summary>자기 오디오 소스만 준비하고 외부 카메라 연결은 요청자에게 맡김</summary>
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
    }

    /// <summary>게임 정지와 무관하게 칼날 섬광과 공간 어긋남의 수명 진행</summary>
    private void Update()
    {
        // 완료된 공간참은 자기 등록만 해제하고 다른 실행의 등록을 보존
        if (spaceRemaining > 0f)
        {
            spaceRemaining = Mathf.Max(0f, spaceRemaining - Time.unscaledDeltaTime);
            if (spaceRemaining <= 0f && ActiveSpaceSlash == this) ActiveSpaceSlash = null;
        }

        // 섬광은 빠르게 나타난 뒤 짧은 여운을 남기고 같은 객체를 숨김
        if (glintRemaining <= 0f) return;
        glintRemaining = Mathf.Max(0f, glintRemaining - Time.unscaledDeltaTime);
        float progress = 1f - glintRemaining / glintTotal;
        glintMaterial.SetFloat("_Intensity", Mathf.Sin(Mathf.PI * Mathf.Sqrt(Mathf.Clamp01(progress))));
        glintObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, BladeGlintSize) * Mathf.Lerp(0.75f, 1.2f, progress);
        if (glintRemaining <= 0f) glintObject.SetActive(false);
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnDestroy()
    {
        Cancel();
        if (glintObject != null) Destroy(glintObject);
        if (glintMesh != null) Destroy(glintMesh);
        if (glintMaterial != null) Destroy(glintMaterial);
        if (SpaceMaterial != null) Destroy(SpaceMaterial);
    }

    /// <summary>준비된 월드 카메라와 자기 재질·메시를 한 번 연결</summary>
    public bool Prepare(Camera camera)
    {
        // 연출 필수 참조가 빠진 경우 조용히 일부만 성공시키지 않음
        if (prepared) return BoundCamera == camera;
        if (camera == null || SpaceSlashMaterial == null || BladeGlintMaterial == null || SheathClip == null || SpaceSlashClip == null)
        {
            Debug.LogError($"{name}: Slash presentation requires the world camera, two materials and two audio clips.", this);
            return false;
        }

        // 월드 카메라 수명 동안 재질과 섬광 객체를 재사용
        BoundCamera = camera;
        SpaceMaterial = new Material(SpaceSlashMaterial);
        glintMaterial = new Material(BladeGlintMaterial);
        glintObject = new GameObject("SlashBladeGlint", typeof(MeshFilter), typeof(MeshRenderer));
        glintMesh = new Mesh { name = "SlashBladeGlintQuad" };
        glintMesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) };
        glintMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        glintMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        glintMesh.RecalculateBounds();
        glintObject.GetComponent<MeshFilter>().sharedMesh = glintMesh;
        MeshRenderer glintRenderer = glintObject.GetComponent<MeshRenderer>();
        glintRenderer.sharedMaterial = glintMaterial;
        glintRenderer.sortingOrder = 2000;
        glintObject.SetActive(false);
        prepared = true;
        return true;
    }

    /// <summary>요청한 칼날 중앙 월드 위치에서 짧은 섬광 시작</summary>
    public void PlayBladeGlint(Vector3 worldPosition, float duration)
    {
        if (!prepared || !isActiveAndEnabled) return;
        glintTotal = glintRemaining = Mathf.Max(0.01f, duration);
        glintObject.transform.position = worldPosition;
        glintObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, BladeGlintSize);
        glintMaterial.SetColor("_Tint", BladeGlintColor);
        glintMaterial.SetFloat("_Intensity", 1f);
        glintObject.SetActive(true);
    }

    /// <summary>칼이 완전히 닫힌 사건에서 납도 효과음 재생</summary>
    public void PlaySheathSound()
    {
        if (prepared && isActiveAndEnabled) audioSource.PlayOneShot(SheathClip, Mathf.Clamp01(Volume));
    }

    /// <summary>지연 뒤 전달받은 Line 위치에서 절단음과 월드 화면 어긋남 시작</summary>
    public void PlaySpaceSlash(Line line)
    {
        if (!prepared || !isActiveAndEnabled) return;
        if (ActiveSpaceSlash != null && ActiveSpaceSlash != this) ActiveSpaceSlash.Cancel();
        spaceLine = line;
        spaceTotal = spaceRemaining = PositiveValue(SpaceSlashDuration, 0.28f);
        ActiveSpaceSlash = this;
        audioSource.PlayOneShot(SpaceSlashClip, Mathf.Clamp01(Volume));
    }

    /// <summary>월드 카메라 렌더 직전에 현재 Line 사영과 복귀 강도를 재질에 반영</summary>
    public void UpdateSpaceMaterial()
    {
        // 카메라가 움직여도 화면에 고정된 옛 절단선으로 바뀌지 않도록 현재 사영 사용
        if (!prepared || !IsSpaceSlashPlaying || BoundCamera == null) return;
        Vector3 start = BoundCamera.WorldToViewportPoint(spaceLine.StartPoint);
        Vector3 end = BoundCamera.WorldToViewportPoint(spaceLine.EndPoint);
        Vector2 center = (spaceLine.StartPoint + spaceLine.EndPoint) * 0.5f;
        Vector2 normal = new Vector2(-spaceLine.Direction.y, spaceLine.Direction.x);
        Vector3 middle = BoundCamera.WorldToViewportPoint(center);
        Vector3 edge = BoundCamera.WorldToViewportPoint(center + normal * (spaceLine.Width * 0.5f));
        float halfWidth = Vector2.Scale((Vector2)(edge - middle), new Vector2(BoundCamera.pixelWidth, BoundCamera.pixelHeight)).magnitude;
        float progress = 1f - spaceRemaining / spaceTotal;
        float restore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, progress));

        // 해상도와 Line 방향에 관계없이 이동량·경계 폭은 화면 픽셀 단위로 유지
        SpaceMaterial.SetVector("_CutStart", new Vector4(start.x, start.y, 0f, 0f));
        SpaceMaterial.SetVector("_CutEnd", new Vector4(end.x, end.y, 0f, 0f));
        SpaceMaterial.SetVector("_SlashScreenSize", new Vector4(BoundCamera.pixelWidth, BoundCamera.pixelHeight, 0f, 0f));
        SpaceMaterial.SetFloat("_Displacement", PositiveValue(SpaceDisplacementPixels, 18f) * (1f - restore));
        SpaceMaterial.SetFloat("_CutGlow", PositiveValue(CutGlowPixels, 2.5f));
        SpaceMaterial.SetFloat("_CutHalfWidthPixels", Mathf.Max(0.1f, halfWidth));
        SpaceMaterial.SetFloat("_CutProgress", progress);
        SpaceMaterial.SetFloat("_Flash", (1f - progress) * (1f - progress));
        SpaceMaterial.SetColor("_CutColor", CutColor);
    }

    /// <summary>정상 납도 완료에서는 준비 표시만 정리하고 납도음의 꼬리 보존</summary>
    public void EndPreparation()
    {
        glintRemaining = 0f;
        if (glintObject != null) glintObject.SetActive(false);
    }

    /// <summary>사망·중단·Title 복귀에서 자기 표현과 재생음만 정리</summary>
    public void Cancel()
    {
        spaceRemaining = glintRemaining = 0f;
        if (ActiveSpaceSlash == this) ActiveSpaceSlash = null;
        if (glintObject != null) glintObject.SetActive(false);
        if (audioSource != null) audioSource.Stop();
    }

    /// <summary>Inspector의 비유한 수치가 재질·효과 수명에 남지 않도록 실제 재생값 제한</summary>
    private static float PositiveValue(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0.01f, value);
    }
}
