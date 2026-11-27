using UnityEngine;

/// <summary>Slash의 칼날 섬광·납도음·공간참 화면 효과를 실제 시간으로 재생</summary>
[RequireComponent(typeof(AudioSource))]
public class SlashPresentationFx : MonoBehaviour
{
    public Material SpaceSlashMaterial; // 월드 화면을 Line 양쪽으로 어긋나게 할 원본 재질
    public Material BladeGlintMaterial; // 일부 뽑힌 칼날 중앙에 표시할 섬광 원본 재질
    public AudioClip SheathClip; // 칼날이 미끄러져 들어간 뒤 탁 닫히는 납도음
    [Min(0f)] public float SheathSoundLead = 0.56f; // 납도음 클립 시작부터 탁 소리 최고점까지의 실제 시간이며 그만큼 닫힘보다 먼저 재생
    public AudioClip SpaceSlashClip; // 지연 뒤 실제 공간참 순간에 맞출 절단음
    [Range(0f, 1f)] public float Volume = 0.8f; // 납도와 공간참 효과음의 공통 재생 음량
    public float SpaceSlashDuration = 0.7f; // 공간이 어긋난 뒤 원상태로 돌아올 실제 시간
    [Range(0f, 1f)] public float SpaceHoldFraction = 0.35f; // 전체 어긋남 시간 중 최대 크기로 멈춰 있을 앞쪽 비율
    [Min(0f)] public float SpaceShearArtPixels = 12f; // 선 양쪽 화면이 선 방향으로 서로 반대로 밀릴 원본 px 거리
    public float CutNegativeDuration = 0.025f; // 첫 반전 프레임(검은 심·흰 테)을 유지할 실제 시간
    public float CutImpactDuration = 0.06f; // 반전 뒤 두꺼운 흰 심·검은 가장자리와 국소 섬광까지 유지할 실제 시각
    public float CutCloseEnd = 0.4f; // 임팩트부터 찢긴 틈이 모양째 오므라드는 실제 시각
    public float CutLightDuration = 0.8f; // 임팩트부터 오므라든 틈이 테두리와 함께 사라질 때까지의 실제 시간
    [Min(1f)] public float CutCoreArtPixels = 5f; // 임팩트 흰 심의 원본 px 두께
    [Min(1f)] public float CutHaloArtPixels = 11f; // 임팩트 색 테를 포함한 전체 원본 px 두께
    [Min(0f)] public float CutRimArtPixels = 1.5f; // 색 테 바깥 검은 가장자리의 원본 px 두께
    [Min(0f)] public float CutTearArtPixels = 28f; // 피해 선분 양 끝에서 톱니가 깊어지며 뾰족하게 모이는 찢김 구간의 원본 px 길이
    [Min(0f)] public float CutRoughness = 1.6f; // 찢긴 경계 톱니의 깊이·촘촘함·끝 휘어짐을 함께 키울 거칠기
    [Min(0f)] public float CutVoidScale = 0.85f; // 임팩트 직후 벌어진 검은 틈의 크기를 임팩트 모양에 대한 비율로 지정
    [Range(0f, 1f)] public float CutScarGap = 0.15f; // 틈이 통째로 오므라든 뒤 사라지기 직전 크기를 열린 틈에 대한 비율로 지정
    [Range(0f, 1f)] public float CutImpactFlash = 0.35f; // 임팩트 동안 피해 영역 주변만 흰색으로 밝힐 비율
    [Range(0f, 1f)] public float ScreenDesaturation = 0.85f; // 공간참 순간 화면 전체에서 뺄 채도 비율
    public float ScreenDesaturationHold = 0.12f; // 최대 채도 감소를 유지할 실제 시간
    public float ScreenDesaturationDuration = 0.6f; // 공간참부터 원래 채도로 돌아올 때까지의 실제 시간
    [Range(0f, 1f)] public float ScreenImpactFlash = 0.22f; // 반전 뒤 임팩트 동안 화면 전체를 흰색으로 밝힐 비율
    public Color CutColor = new Color(64f / 255f, 217f / 255f, 1f, 1f); // Draw 선의 청록 테와 같은 절단빛 색 테와 마지막 선의 색
    public Color VoidColor = new Color(0.01f, 0.015f, 0.04f, 1f); // 반전 심·검은 가장자리·벌어진 틈에 쓸 공허색
    public float BladeGlintSize = 0.65f; // 칼날 중앙 섬광의 월드 공간 한 변 길이
    public Color BladeGlintColor = new Color(0.75f, 0.95f, 1f, 1f); // 칼날 섬광의 흰 중심 주변 강조색
    public Camera BoundCamera { get; private set; } // 준비 단계에서 한 번 연결한 월드 전용 카메라
    public Material SpaceMaterial { get; private set; } // 다른 Player의 연출 값을 덮지 않을 실행 전용 재질
    public bool IsSpaceSlashPlaying => spaceElapsed < spaceTotal; // 월드 화면의 어긋남이나 절단빛이 진행 중인지 조회
    public static SlashPresentationFx ActiveSpaceSlash { get; private set; } // 렌더러가 현재 공간참 소유자와 카메라만 확인할 창구

    private AudioSource audioSource; // 게임 시간 정지 중에도 효과음을 재생할 자기 오디오 소스
    private Material glintMaterial; // 섬광 강도만 독립적으로 갱신할 실행 전용 재질
    private GameObject glintObject; // Slash마다 생성하지 않고 위치와 표시만 바꿀 섬광 객체
    private Mesh glintMesh; // 칼날 섬광의 절차적 셰이더를 그릴 재사용 사각 메시
    private Line spaceLine; // 이번 공간참의 카메라 사영에 사용할 확정 Line 스냅샷
    private float spaceElapsed; // 이번 공간참 임팩트부터 진행한 실제 시간
    private float spaceTotal; // 어긋남과 절단빛 중 긴 쪽으로 정한 이번 공간참의 전체 시간
    private float shearTotal; // 중간 Inspector 변경과 구분한 이번 어긋남 복원 시간
    private float negativeEnd; // 이번 실행에서 반전 프레임이 끝날 시각
    private float impactEnd; // 이번 실행에서 두꺼운 임팩트가 끝날 시각
    private float closeEnd; // 이번 실행에서 찢긴 틈이 닫힐 시각
    private float lightEnd; // 이번 실행에서 테두리 잔상이 사라질 시각
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
        if (IsSpaceSlashPlaying)
        {
            spaceElapsed = Mathf.Min(spaceTotal, spaceElapsed + Time.unscaledDeltaTime);
            if (!IsSpaceSlashPlaying && ActiveSpaceSlash == this) ActiveSpaceSlash = null;
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

    /// <summary>탁 소리 최고점이 칼이 닫히는 순간에 오도록 요청자가 앞당긴 시각에 납도 효과음 재생</summary>
    public void PlaySheathSound()
    {
        if (prepared && isActiveAndEnabled) audioSource.PlayOneShot(SheathClip, Mathf.Clamp01(Volume) * GameSettings.SfxVolume);
    }

    /// <summary>지연 뒤 전달받은 Line 위치에서 절단음과 월드 화면 어긋남 시작</summary>
    public void PlaySpaceSlash(Line line)
    {
        if (!prepared || !isActiveAndEnabled) return;
        if (ActiveSpaceSlash != null && ActiveSpaceSlash != this) ActiveSpaceSlash.Cancel();
        spaceLine = line;
        shearTotal = PositiveValue(SpaceSlashDuration, 0.7f);
        negativeEnd = PositiveValue(CutNegativeDuration, 0.025f);
        impactEnd = Mathf.Max(negativeEnd, PositiveValue(CutImpactDuration, 0.06f));
        closeEnd = Mathf.Max(impactEnd, PositiveValue(CutCloseEnd, 0.4f));
        lightEnd = Mathf.Max(closeEnd, PositiveValue(CutLightDuration, 0.8f));
        spaceTotal = Mathf.Max(Mathf.Max(shearTotal, lightEnd), PositiveValue(ScreenDesaturationDuration, 0.6f));
        spaceElapsed = 0f;
        ActiveSpaceSlash = this;
        audioSource.PlayOneShot(SpaceSlashClip, Mathf.Clamp01(Volume) * GameSettings.SfxVolume);
    }

    /// <summary>월드 카메라 렌더 직전에 현재 Line 사영과 복귀 강도를 재질에 반영</summary>
    public void UpdateSpaceMaterial()
    {
        // 카메라가 움직여도 화면에 고정된 옛 절단선으로 바뀌지 않도록 현재 사영 사용
        if (!prepared || !IsSpaceSlashPlaying || BoundCamera == null) return;
        Vector3 start = BoundCamera.WorldToViewportPoint(spaceLine.StartPoint);
        Vector3 end = BoundCamera.WorldToViewportPoint(spaceLine.EndPoint);
        float artPixel = BoundCamera.pixelHeight / Mathf.Max(0.01f, BoundCamera.orthographicSize * 2f * PixelShapeTexture.PixelsPerUnit);
        float shearProgress = Mathf.Clamp01(spaceElapsed / shearTotal);
        float restore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Clamp01(SpaceHoldFraction), 1f, shearProgress));

        // 화면 끝까지 이어지는 어긋남은 원본 px를 현재 화면 px로 바꿔 유지 뒤 복원
        SpaceMaterial.SetVector("_CutStart", new Vector4(start.x, start.y, 0f, 0f));
        SpaceMaterial.SetVector("_CutEnd", new Vector4(end.x, end.y, 0f, 0f));
        SpaceMaterial.SetVector("_SlashScreenSize", new Vector4(BoundCamera.pixelWidth, BoundCamera.pixelHeight, 0f, 0f));
        SpaceMaterial.SetFloat("_ArtPixel", artPixel);
        SpaceMaterial.SetFloat("_Shear", SpaceShearArtPixels * artPixel * (1f - restore));

        // 화면 전체 채도는 잠깐 유지한 뒤 복원하고, 흰 섬광은 반전 뒤 임팩트 동안만 적용
        float desaturationEnd = Mathf.Max(PositiveValue(ScreenDesaturationDuration, 0.6f), ScreenDesaturationHold + 0.01f);
        float desaturationRestore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Max(0f, ScreenDesaturationHold), desaturationEnd, spaceElapsed));
        bool screenFlash = spaceElapsed > negativeEnd && spaceElapsed <= impactEnd;
        SpaceMaterial.SetFloat("_Desaturate", Mathf.Clamp01(ScreenDesaturation) * (1f - desaturationRestore));
        SpaceMaterial.SetFloat("_ScreenFlash", screenFlash ? Mathf.Clamp01(ScreenImpactFlash) : 0f);

        // 손톱자국처럼 찢긴 모양 하나로 그릴 반전 → 임팩트 → 닫히는 틈 → 양옆 잔상의 단계 시각과 크기
        SpaceMaterial.SetFloat("_CutTime", spaceElapsed);
        SpaceMaterial.SetFloat("_NegativeEnd", negativeEnd);
        SpaceMaterial.SetFloat("_ImpactEnd", impactEnd);
        SpaceMaterial.SetFloat("_CloseEnd", closeEnd);
        SpaceMaterial.SetFloat("_LightEnd", lightEnd);
        SpaceMaterial.SetFloat("_CoreThickness", Mathf.Max(1f, CutCoreArtPixels));
        SpaceMaterial.SetFloat("_HaloThickness", Mathf.Max(CutCoreArtPixels, CutHaloArtPixels));
        SpaceMaterial.SetFloat("_RimThickness", CutRimArtPixels);
        SpaceMaterial.SetFloat("_TearLength", CutTearArtPixels);
        SpaceMaterial.SetFloat("_Roughness", CutRoughness);
        SpaceMaterial.SetFloat("_VoidScale", CutVoidScale);
        SpaceMaterial.SetFloat("_ScarGap", Mathf.Clamp01(CutScarGap));
        SpaceMaterial.SetFloat("_ImpactFlash", Mathf.Clamp01(CutImpactFlash));
        SpaceMaterial.SetColor("_CutColor", CutColor);
        SpaceMaterial.SetColor("_VoidColor", VoidColor);
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
        spaceElapsed = spaceTotal;
        glintRemaining = 0f;
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
