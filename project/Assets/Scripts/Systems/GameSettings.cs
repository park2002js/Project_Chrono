using UnityEngine;

/// <summary>슬롯 진행과 독립된 BGM·SFX 음량과 320×180 정수배 해상도를 보관하고 저장·적용</summary>
public static class GameSettings
{
    public const int BasePixelWidth = 320; // 16PPU 도트로 본 기준 시야의 가로 칸 수
    public const int BasePixelHeight = 180; // 기준 시야의 세로 칸 수이며 화면 높이 ÷ 180이 도트 한 칸의 화면 px
    public const int MinPixelScale = 3; // 목록에 올릴 가장 작은 도트 배율(960×540)
    public static float BgmVolume { get; private set; } = 1f; // 배경음 재생기가 자기 음량에 곱할 0~1 배율
    public static float SfxVolume { get; private set; } = 1f; // 효과음 재생 지점이 자기 음량에 곱할 0~1 배율
    public static int PixelScale { get; private set; } = 6; // 마지막으로 요청한 도트 한 칸의 화면 px(해상도 = 320×180 × 배율)
    public static bool IsFullScreen { get; private set; } = true; // 마지막으로 요청한 전체 화면 여부
    public static bool HasResolutionChoice { get; private set; } // 사용자가 설정에서 해상도·화면 모드를 고른 적이 있는지 여부

    private const string BgmVolumeKey = "Settings.BgmVolume"; // PlayerPrefs의 배경음 배율 키
    private const string SfxVolumeKey = "Settings.SfxVolume"; // PlayerPrefs의 효과음 배율 키
    private const string PixelScaleKey = "Settings.PixelScale"; // PlayerPrefs의 도트 배율 키
    private const string FullScreenKey = "Settings.FullScreen"; // PlayerPrefs의 전체 화면 여부 키

    /// <summary>첫 씬을 불러오기 전에 저장된 음량을 읽고, 고른 적 있는 해상도만 다시 적용</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        // 음량은 저장값이 없으면 기존 음향 조정을 그대로 쓰는 100%
        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));

        // 고른 적이 없으면 실행 시 화면을 건드리지 않고 현재 높이로 배율만 추정
        HasResolutionChoice = false;
        IsFullScreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        PixelScale = Mathf.Max(MinPixelScale, Screen.height / BasePixelHeight);
        if (!PlayerPrefs.HasKey(PixelScaleKey))
            return;

        // 모니터가 바뀌어 저장 배율이 들어가지 않으면 들어가는 최대 배율로 낮춤
        int scale = Mathf.Min(PlayerPrefs.GetInt(PixelScaleKey, PixelScale), GetMaxPixelScale());
        ApplyResolution(scale, PlayerPrefs.GetInt(FullScreenKey, 1) == 1);
    }

    /// <summary>배경음 배율을 0~1로 바꾸며 BattleMusic이 다음 프레임부터 읽음</summary>
    public static void SetBgmVolume(float volume)
    {
        BgmVolume = Mathf.Clamp01(volume);
    }

    /// <summary>효과음 배율을 0~1로 바꾸며 이후 재생하는 효과음부터 적용</summary>
    public static void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
    }

    /// <summary>도트 배율로 320×180의 정수배 해상도와 화면 모드를 요청</summary>
    public static void ApplyResolution(int pixelScale, bool fullScreen)
    {
        PixelScale = Mathf.Max(MinPixelScale, pixelScale);
        IsFullScreen = fullScreen;
        HasResolutionChoice = true;
        Screen.SetResolution(BasePixelWidth * PixelScale, BasePixelHeight * PixelScale, fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    /// <summary>현재 모니터에 들어가는 도트 배율을 작은 순서로 반환하며 현재 배율은 항상 포함</summary>
    public static int[] GetPixelScales()
    {
        int max = Mathf.Max(GetMaxPixelScale(), PixelScale);
        int[] scales = new int[max - MinPixelScale + 1];
        for (int i = 0; i < scales.Length; i++)
            scales[i] = MinPixelScale + i;
        return scales;
    }

    /// <summary>음량과 사용자가 고른 해상도만 슬롯 파일과 별도로 PlayerPrefs에 기록</summary>
    public static void Save()
    {
        PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        if (HasResolutionChoice)
        {
            PlayerPrefs.SetInt(PixelScaleKey, PixelScale);
            PlayerPrefs.SetInt(FullScreenKey, IsFullScreen ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    /// <summary>주 모니터의 기본 해상도에 가로·세로 모두 들어가는 가장 큰 도트 배율</summary>
    private static int GetMaxPixelScale()
    {
        int width = Display.main.systemWidth, height = Display.main.systemHeight;
        if (width <= 0 || height <= 0)
        {
            width = Screen.currentResolution.width;
            height = Screen.currentResolution.height;
        }
        return Mathf.Max(MinPixelScale, Mathf.Min(width / BasePixelWidth, height / BasePixelHeight));
    }
}
