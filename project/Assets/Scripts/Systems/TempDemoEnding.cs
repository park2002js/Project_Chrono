using System.Collections;
using UnityEngine;

/// <summary>경비 로봇 사망 사건을 받아 임시 데모 종료 화면과 Title 복귀 처리</summary>
public class TempDemoEnding : MonoBehaviour
{
    public float FadeDuration = 2f; // 검은 화면으로 천천히 전환할 실제 시간
    public float MessageDuration = 3f; // 완전 암전 뒤 중앙 문구를 유지할 실제 시간
    public bool IsPlaying { get; private set; } // 중복 사망 통지와 종료 화면 표시를 제어할 상태
    public float FadeAlpha { get; private set; } // 화면 전체 검정 덮개의 현재 불투명도
    public bool IsMessageVisible { get; private set; } // 암전 완료 뒤에만 문구를 표시할 상태

    private GameManager manager; // 앱 수명 동안 Title 복귀와 시간 정지를 요청할 관리자
    private StageManager stage; // 기존 사망 연출을 정리할 같은 앱의 세션 관리자
    private BossCharacter boss; // 이번 세션에 사망 사건을 구독한 경비 로봇
    private PlayerCharacter player; // 엔딩 동안 행동을 제한할 이번 세션의 Player
    private PlayerInputController input; // 세션 연결 때 한 번 확보할 사용자 입력
    private UIController ui; // 중앙 안내와 겹치는 HUD·Title 버튼을 숨길 씬 UI
    private Coroutine ending; // 세션 중단 때 종료할 암전·안내 코루틴
    private GUIStyle messageStyle; // 첫 GUI 문맥에서 준비해 재사용할 중앙 안내 스타일
    private bool previousInputBlock; // 엔딩 중단 때 돌려놓을 기존 입력 차단
    private bool previousCutscene; // 엔딩 중단 때 돌려놓을 기존 Player 컷씬 상태
    private bool previousDeathMode; // 엔딩 중단 때 돌려놓을 기존 HUD 숨김 상태
    private bool ownsPause; // 암전 완료 뒤 적용한 시간 정지를 해제할 소유 표시

    /// <summary>자기 앱 객체의 관리자는 생성 시 한 번 확보</summary>
    private void Awake()
    {
        manager = GetComponent<GameManager>();
        stage = GetComponent<StageManager>();
    }

    private void OnDisable() => Unbind();

    /// <summary>기존 한글 UI 폰트로 화면 전체 암전과 정확한 중앙 안내 표시</summary>
    private void OnGUI()
    {
        if (!IsPlaying) return;

        // 다른 게임 UI 위에 화면 크기와 무관한 검정 덮개 배치
        int depth = GUI.depth;
        Color color = GUI.color;
        GUI.depth = -200;
        GUI.color = new Color(0f, 0f, 0f, FadeAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        // 완전히 어두워진 뒤 읽을 수 있는 크기의 흰색 문구 표시
        if (IsMessageVisible)
        {
            if (messageStyle == null)
                messageStyle = new GUIStyle(GUI.skin.label) { font = ui.UIFont, alignment = TextAnchor.MiddleCenter };
            messageStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.height / 30f));
            messageStyle.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "Demo는 여기까지입니다..", messageStyle);
        }
        GUI.color = color;
        GUI.depth = depth;
    }

    /// <summary>Player와 씬 서비스가 준비된 세션 경계에서만 경비 사망 구독</summary>
    public void Bind()
    {
        // 이전 대상의 사건과 표시 소유를 닫은 뒤 현재 세션 참조 확보
        Unbind();
        boss = manager.CurrentBoss;
        if (boss == null || boss.BossId != GameManager.SubmittedBossId) return;
        player = manager.CurrentPlayer;
        input = PlayerInputController.Instance;
        ui = UIController.Instance;
        messageStyle = null;
        boss.OnDie += HandleBossDeath; // 기존 처치 저장 이후 임시 데모 엔딩 시작
    }

    /// <summary>Title 복귀·실패·비활성화에서 이전 Boss 사건과 엔딩 소유 해제</summary>
    public void Unbind()
    {
        // 사망 통지와 진행 중 코루틴이 다음 세션으로 넘어가지 않도록 정리
        if (boss != null) boss.OnDie -= HandleBossDeath; // 이전 경비 사망 구독 해제
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
        boss = null;
        player = null;
        input = null;
        ui = null;
        messageStyle = null;
    }

    /// <summary>처치 기록과 위험물 정리는 기존 구독자에 맡기고 엔딩 제어만 인계</summary>
    private void HandleBossDeath()
    {
        if (IsPlaying || !isActiveAndEnabled || player == null || input == null || ui == null) return;

        // 기존 짧은 사망 연출의 복구가 엔딩 잠금을 덮어쓰지 않도록 먼저 종료
        stage.CancelBossSequence();
        previousCutscene = player.IsCutScene;
        previousInputBlock = input.BlockInput;
        previousDeathMode = ui.IsDeathMode;
        IsPlaying = true;
        player.IsCutScene = true;
        input.BlockInput = true;
        ui.SetDeathMode(true);
        ending = StartCoroutine(PlayEnding()); // 실제 시간으로 암전·중앙 안내 후 Title 복귀
    }

    /// <summary>천천히 암전한 뒤 시간을 멈추고 안내를 유지하다 기존 Title 경로 사용</summary>
    private IEnumerator PlayEnding()
    {
        // 공격·Draw·Slash는 컷씬 진입 사건으로 닫고 암전은 실제 시간으로 진행
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            FadeAlpha = Mathf.SmoothStep(0f, 1f, elapsed / FadeDuration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        FadeAlpha = 1f;

        // 완전 암전 뒤 게임 시간을 정지하고 안내 문구를 읽을 시간 확보
        ownsPause = true;
        manager.SetBossCutscenePaused(true);
        IsMessageVisible = true;
        yield return new WaitForSecondsRealtime(MessageDuration); // 게임 시간이 정지된 상태에서 중앙 문구 유지

        // 기존 저장 슬롯·세션 해제·씬 전환 계약을 그대로 사용
        ending = null;
        manager.ReturnToTitle();
    }
}
