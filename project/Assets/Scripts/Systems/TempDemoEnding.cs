using System.Collections;
using UnityEngine;

/// <summary>마지막 맵의 문에 들어온 사실을 받아 임시 데모 종료 화면과 Title 복귀 처리</summary>
public class TempDemoEnding : MonoBehaviour
{
    public float FadeDuration = 2f; // 화면을 천천히 덮을 실제 시간
    public float MessageDuration = 3f; // 완전히 덮은 뒤 중앙 문구를 유지할 실제 시간
    public Color FadeColor = Color.white; // 빛의 문 속으로 들어간 화면을 덮을 색
    public Color MessageColor = new Color(0.106f, 0.094f, 0.149f); // 덮인 화면 위에 띄울 문구 색
    public bool IsPlaying { get; private set; } // 중복 진입 통지와 종료 화면 표시를 제어할 상태
    public float FadeAlpha { get; private set; } // 화면 전체 덮개의 현재 불투명도
    public bool IsMessageVisible { get; private set; } // 완전히 덮은 뒤에만 문구를 표시할 상태

    private GameManager manager; // 앱 수명 동안 Title 복귀와 시간 정지를 요청할 관리자
    private PlayerCharacter player; // 엔딩 동안 행동을 제한할 이번 세션의 Player
    private PlayerInputController input; // 세션 연결 때 한 번 확보할 사용자 입력
    private UIController ui; // 중앙 안내와 겹치는 HUD·Title 버튼을 숨길 씬 UI
    private Coroutine ending; // 세션 중단 때 종료할 덮개·안내 코루틴
    private GUIStyle messageStyle; // 첫 GUI 문맥에서 준비해 재사용할 중앙 안내 스타일
    private bool previousInputBlock; // 엔딩 중단 때 돌려놓을 기존 입력 차단
    private bool previousCutscene; // 엔딩 중단 때 돌려놓을 기존 Player 컷씬 상태
    private bool previousDeathMode; // 엔딩 중단 때 돌려놓을 기존 HUD 숨김 상태
    private bool ownsPause; // 완전히 덮은 뒤 적용한 시간 정지를 해제할 소유 표시

    /// <summary>자기 앱 객체의 관리자는 생성 시 한 번 확보</summary>
    private void Awake()
    {
        manager = GetComponent<GameManager>();
    }

    private void OnDisable() => Unbind();

    /// <summary>기존 한글 UI 폰트로 화면 전체 덮개와 정확한 중앙 안내 표시</summary>
    private void OnGUI()
    {
        if (!IsPlaying) return;

        // 다른 게임 UI 위에 화면 크기와 무관한 한 색 덮개 배치
        int depth = GUI.depth;
        Color color = GUI.color;
        GUI.depth = -200;
        GUI.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, FadeAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        // 완전히 덮은 뒤 읽을 수 있는 크기의 문구 표시
        if (IsMessageVisible)
        {
            if (messageStyle == null)
                messageStyle = new GUIStyle(GUI.skin.label) { font = ui.UIFont, alignment = TextAnchor.MiddleCenter };
            messageStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height / 30f));
            messageStyle.normal.textColor = MessageColor;
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "Demo는 여기까지입니다..", messageStyle);
        }
        GUI.color = color;
        GUI.depth = depth;
    }

    /// <summary>Player와 씬 서비스가 준비된 세션 경계에서 엔딩에 쓸 참조만 확보</summary>
    public void Bind()
    {
        // 이전 대상의 표시 소유를 닫은 뒤 현재 세션 참조 확보
        Unbind();
        player = manager.CurrentPlayer;
        input = PlayerInputController.Instance;
        ui = UIController.Instance;
        messageStyle = null;
    }

    /// <summary>Title 복귀·실패·비활성화에서 진행 중 엔딩과 그 소유 해제</summary>
    public void Unbind()
    {
        // 진행 중 코루틴이 다음 세션으로 넘어가지 않도록 정리
        if (ending != null) StopCoroutine(ending); // 취소된 엔딩의 뒤늦은 Title 복귀 차단
        ending = null;
        if (IsPlaying)
        {
            if (player != null) player.IsCutScene = previousCutscene;
            if (input != null) input.BlockInput = previousInputBlock;
            if (ui != null) ui.SetDeathMode(previousDeathMode);
        }
        if (ownsPause && manager != null) manager.SetBossCutscenePaused(false);

        // 앱 객체는 재사용하고 세션 대상과 표시 상태만 초기화
        ownsPause = IsPlaying = IsMessageVisible = false;
        FadeAlpha = 0f;
        player = null;
        input = null;
        ui = null;
        messageStyle = null;
    }

    /// <summary>마지막 맵의 문에 들어온 Player를 멈추고 완료 기록 뒤 종료 화면 시작</summary>
    public void Play()
    {
        if (IsPlaying || !isActiveAndEnabled || player == null || input == null || ui == null) return;

        // 데모의 끝까지 본 슬롯으로 기록해 다음 선택에서 새 게임 확인을 받게 함
        if (SaveManager.Current != null)
        {
            SaveManager.Current.Set("DemoCompleted", true);
            SaveManager.Save();
        }

        // 엔딩 잠금 전의 입력·컷씬·HUD 상태를 보관하고 조작 차단
        previousCutscene = player.IsCutScene;
        previousInputBlock = input.BlockInput;
        previousDeathMode = ui.IsDeathMode;
        IsPlaying = true;
        player.IsCutScene = true;
        input.BlockInput = true;
        ui.SetDeathMode(true);
        ending = StartCoroutine(PlayEnding()); // 실제 시간으로 덮개·중앙 안내 후 Title 복귀
    }

    /// <summary>천천히 화면을 덮은 뒤 시간을 멈추고 안내를 유지하다 기존 Title 경로 사용</summary>
    private IEnumerator PlayEnding()
    {
        // 공격·Draw·Slash는 컷씬 진입 사건으로 닫고 덮개는 실제 시간으로 진행
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            FadeAlpha = Mathf.SmoothStep(0f, 1f, elapsed / FadeDuration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        FadeAlpha = 1f;

        // 완전히 덮은 뒤 게임 시간을 정지하고 안내 문구를 읽을 시간 확보
        ownsPause = true;
        manager.SetBossCutscenePaused(true);
        IsMessageVisible = true;
        yield return new WaitForSecondsRealtime(MessageDuration); // 게임 시간이 정지된 상태에서 중앙 문구 유지

        // 기존 저장 슬롯·세션 해제·씬 전환 계약을 그대로 사용
        ending = null;
        manager.ReturnToTitle();
    }
}
