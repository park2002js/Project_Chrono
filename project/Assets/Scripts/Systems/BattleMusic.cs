using UnityEngine;

/// <summary>Boss 전투 동안 지정한 배경음을 반복 재생하고, Draw 중에는 그래프대로 낮고 느리게 바꿨다가 되돌리며, 전투가 끝나면 서서히 끄는 공용 재생기</summary>
public class BattleMusic : MonoBehaviour
{
    [Range(0f, 1f)] public float Volume = 0.5f; // 배경음이 다 켜졌을 때의 음량
    public float FadeInDuration = 0.5f; // 전투 시작에서 음량이 다 켜질 때까지의 실제 시간
    public float FadeOutDuration = 1.5f; // 전투 종료에서 음량이 다 꺼질 때까지의 실제 시간
    public AnimationCurve DrawPitchCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(0.4f, 0.5f, 0f, 0f)); // Draw 진입부터 지난 실제 초(가로)에 따른 재생 배속(세로)이며 배속이 낮을수록 음이 낮고 느려짐
    [Min(0.01f)] public float DrawRestoreSpeed = 1.5f; // Draw 해제 뒤 같은 그래프를 거꾸로 되짚어 원래 음으로 돌아올 속도 배율
    public bool IsPlaying => source != null && source.isPlaying; // 켜지는 중과 꺼지는 중을 포함해 소리가 나고 있는지 여부
    public AudioClip CurrentClip => source != null ? source.clip : null; // 지금 재생기에 올라 있는 배경음
    public float CurrentPitch => source != null ? source.pitch : 1f; // 지금 적용 중인 재생 배속

    private AudioSource source; // 배경음만 재생할 자기 오디오 소스
    private GameManager gameManager; // Draw 정지 상태를 매 프레임 읽을 앱 관리자
    private float level; // 현재 켜진 정도이며 0은 꺼짐, 1은 다 켜짐
    private float drawTime; // Draw 그래프에서 현재 위치한 실제 초이며 진입 중에는 늘고 해제 뒤에는 줄어듦
    private bool wanted; // 전투가 이어져 배경음을 켜 두어야 하는지 여부

    /// <summary>배경음 전용 2D 오디오 소스를 한 번 준비</summary>
    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    /// <summary>앱 수명 동안 유지되는 GameManager를 다른 서비스의 Awake 이후 한 번 확보</summary>
    private void Start()
    {
        gameManager = GameManager.Instance;
    }

    /// <summary>게임 시간이 멈춰 있어도 실제 시간으로 음량과 Draw 배속을 바꾸고 다 꺼지면 재생 정지</summary>
    private void Update()
    {
        if (source == null || (!wanted && !source.isPlaying)) return;

        // 전투 시작·종료에 맞춰 음량을 켜거나 끔
        float duration = wanted ? FadeInDuration : FadeOutDuration;
        float goal = wanted ? 1f : 0f;
        level = duration > 0f ? Mathf.MoveTowards(level, goal, Time.unscaledDeltaTime / duration) : goal;
        source.volume = Mathf.Clamp01(Volume) * level * GameSettings.BgmVolume; // 설정의 현재 BGM 배율을 매 프레임 반영

        // Draw 중에는 그래프를 따라가고 해제 뒤에는 같은 그래프를 거꾸로 되짚어 원래 배속으로 복귀
        bool drawing = gameManager != null && gameManager.IsDrawPaused;
        float end = CurveEnd();
        drawTime = Mathf.Clamp(drawTime + Time.unscaledDeltaTime * (drawing ? 1f : -Mathf.Max(0.01f, DrawRestoreSpeed)), 0f, end);
        source.pitch = SamplePitch(drawTime);
        if (!wanted && level <= 0f) StopImmediately();
    }

    private void OnDisable()
    {
        // 객체가 꺼지면 남은 소리 없이 즉시 정지
        wanted = false;
        level = 0f;
        StopImmediately();
    }

    /// <summary>지정한 배경음을 처음부터 반복 재생하며 같은 곡이 아직 나고 있으면 그대로 이어서 다시 켬</summary>
    public void Play(AudioClip clip)
    {
        if (clip == null || source == null)
        {
            Stop();
            return;
        }
        wanted = true;
        if (source.clip == clip && source.isPlaying) return;
        source.clip = clip;
        level = 0f;
        source.volume = 0f;
        source.Play();
    }

    /// <summary>배경음을 서서히 끄도록 요청</summary>
    public void Stop()
    {
        wanted = false;
    }

    /// <summary>재생을 즉시 멈추고 Draw 배속을 원래대로 초기화</summary>
    private void StopImmediately()
    {
        drawTime = 0f;
        if (source == null) return;
        source.Stop();
        source.pitch = 1f;
    }

    /// <summary>그래프 마지막 키의 실제 초이며 키가 없으면 0</summary>
    private float CurveEnd()
    {
        return DrawPitchCurve != null && DrawPitchCurve.length > 0 ? Mathf.Max(0f, DrawPitchCurve[DrawPitchCurve.length - 1].time) : 0f;
    }

    /// <summary>그래프의 배속을 소리가 끊기지 않는 범위로 제한해 조회하며 그래프가 비면 원래 배속</summary>
    private float SamplePitch(float time)
    {
        if (DrawPitchCurve == null || DrawPitchCurve.length == 0) return 1f;
        float pitch = DrawPitchCurve.Evaluate(time);
        return float.IsNaN(pitch) || float.IsInfinity(pitch) ? 1f : Mathf.Clamp(pitch, 0.1f, 3f);
    }
}
