using System;
using System.Collections;
using UnityEngine;

/// <summary>맵에 처음 들어온 실제 Player를 시작 위치에서 깨우거나 입구 아래에서 걸어오게 한 뒤 사용자 조작으로 인계</summary>
public class TutorialController : MonoBehaviour
{
    public float WakeHoldSeconds = 1.2f; // 깨어나는 입장에서 누운 첫 자세를 유지할 실제 시간
    public float WakeReturnSeconds = 1.6f; // 일어난 뒤 화면이 가까운 시야에서 평소 시야로 돌아오는 실제 시간
    public bool HasPendingEntrance { get; private set; } // 이 맵의 최초 입장을 준비해 일반 기상 대신 실행할지 표시
    public bool WalksIn { get; private set; } // 준비한 입장이 입구 아래에서 걸어 들어오는 방식인지 표시이며 false면 시작 위치에서 깨어남
    public bool IsPlaying { get; private set; } // 입장 연출과 입력 차단을 소유한 상태
    public bool WasCompleted { get; private set; } // 입장 연출과 최초 입장 저장을 마친 상태
    public string LastError { get; private set; } // 잘못된 경로나 중단으로 입장을 끝내지 못한 이유

    private GameManager manager; // 앱 수명 동안 재사용할 자기 월드 관리자
    private PlayerCharacter player; // 이번 세션에 생성된 실제 입장 대상
    private PlayerInputController input; // 입장 동안 일반 사용자 입력을 차단할 씬 서비스
    private GameCamera gameCamera; // 세션 준비 때 한 번 확보할 입장 화면의 카메라
    private Transform entranceCameraTarget; // 걸어 들어오는 동안 카메라 중앙에 고정할 도착 지점
    private Transform previousCameraTarget; // 정상 완료·중단에서 돌려놓을 기존 추적 대상
    private SaveData save; // 정상 입장 완료를 기록할 이번 슬롯의 저장 객체
    private Vector2 stopPoint; // 맵이 지정한 조작 권한 인계 위치
    private bool ownsControl; // 중단·완료에서 자기 잠금만 한 번 복구할 표시
    private bool previousInputBlock; // 입장 전에 적용돼 있던 입력 차단 상태
    private bool previousCutscene; // 입장 전에 적용돼 있던 Player 컷씬 상태
    private int version; // 중단된 입장 코루틴의 뒤늦은 저장·인계 방지
    private const float StopDistance = 0.00001f; // 마지막 보행 변위를 적용한 뒤 남을 수 있는 부동소수점 위치 오차

    /// <summary>씬과 무관하게 유지되는 자기 앱 관리자만 한 번 확보</summary>
    private void Awake() => manager = GetComponent<GameManager>();

    private void OnDisable() => Cancel();

    /// <summary>월드 생성·Player 연결 뒤 이 맵의 최초 입장 자격과 방식을 정하고 걸어 들어오는 맵이면 출발 위치 준비</summary>
    public void Prepare()
    {
        // 이전 세션 소유를 정리하고 맵마다 다시 꺼지는 최초 입장 표식 확인
        Cancel();
        LastError = null;
        save = SaveManager.Current;
        HasPendingEntrance = save != null && save.Values.ContainsKey("EntranceCompleted") && !save.Get<bool>("EntranceCompleted");
        if (!HasPendingEntrance) return;

        // 맵·Player 생성 이후에만 씬 수명 참조 확보
        MapManager map = MapManager.Instance;
        player = manager != null ? manager.CurrentPlayer : null;
        input = PlayerInputController.Instance;
        gameCamera = GameCamera.Instance;
        if (map == null || !map.IsInitialized || player == null || input == null || gameCamera == null || gameCamera.Camera == null)
            throw new InvalidOperationException("First entrance requires an initialized Map, Player, input and camera.");

        // 깨어나는 맵은 저장 체크포인트에 놓인 그대로 두고 기상 연출에 맡김
        WalksIn = !map.WakesOnFirstEntry;
        if (!WalksIn) return;

        // 걸어 들어오는 맵은 출발·도착 두 지점의 연결 확인
        if (map.EntranceSpawnPoint == null || map.EntranceStopPoint == null
            || !Finite(map.EntranceSpawnPoint.position) || !Finite(map.EntranceStopPoint.position))
            throw new InvalidOperationException("Walking entrance requires two entrance points.");
        stopPoint = map.EntranceStopPoint.position;
        if (stopPoint.y <= map.EntranceSpawnPoint.position.y)
            throw new InvalidOperationException("First entrance must walk upward from spawn to handoff.");

        // 로딩 화면 아래에서 최초 위치만 바꾸고 저장 체크포인트·부활 위치는 유지
        previousInputBlock = input.BlockInput;
        previousCutscene = player.IsCutScene;
        ownsControl = true;
        input.BlockInput = true;
        player.Spawner.PlacePlayer(map.EntranceSpawnPoint);
        player.IsCutScene = true;
        player.SetAimPosition((Vector2)player.transform.position + Vector2.up);

        // 첫 화면은 비워 두고 Player가 아래에서 중앙으로 들어오도록 고정 초점 확보
        player.SetCutsceneMoveTarget(stopPoint);
        player.SetCutsceneMoveTarget(null);
        previousCameraTarget = gameCamera.Target;
        entranceCameraTarget = map.EntranceStopPoint;
        gameCamera.Follow(entranceCameraTarget, true);
    }

    /// <summary>준비한 방식대로 깨어나거나 걸어 들어오고 정상 완료 때만 최초 입장 저장</summary>
    public IEnumerator PlayEntrance()
    {
        if (!HasPendingEntrance || player == null) yield break;
        if (WalksIn) yield return WalkIn(); // 입구 아래에서 도착 지점까지의 실제 보행과 완료 기록까지 대기
        else yield return WakeUp(); // 누운 자세·기상·화면 복귀와 완료 기록까지 대기
    }

    /// <summary>Title 복귀·생성 실패·비활성화에서 미완 입장을 저장하지 않고 자기 제어 해제</summary>
    public void Cancel()
    {
        version++;
        HasPendingEntrance = WalksIn = IsPlaying = WasCompleted = false;
        RestoreControl();
        player = null;
        input = null;
        save = null;
        gameCamera = null;
        entranceCameraTarget = previousCameraTarget = null;
    }

    /// <summary>시작 위치에서 천천히 깨어나는 연출이 끝까지 재생된 때만 최초 입장 저장</summary>
    private IEnumerator WakeUp()
    {
        int ticket = version;
        PlayerCharacter actor = player;
        IsPlaying = true;
        yield return actor.Spawner.PlayWakeUp(WakeHoldSeconds, WakeReturnSeconds); // 누운 자세 유지·기상·화면 복귀까지 대기
        if (ticket != version || !IsPlaying) yield break;
        if (actor == null || !actor.isActiveAndEnabled || actor.IsDead)
        {
            LastError = "Player wake-up was interrupted before it finished.";
            Cancel();
            yield break;
        }

        // 정상 완료만 저장 요청하고 월드 관리자의 입력 인계 허용
        save.Set("EntranceCompleted", true);
        SaveManager.Save();
        HasPendingEntrance = IsPlaying = false;
        WasCompleted = true;
    }

    /// <summary>게임 시간이 흐르는 동안 실제 이동 경로로 걷고 정상 도착 때만 최초 입장 저장</summary>
    private IEnumerator WalkIn()
    {
        if (!ownsControl) yield break;
        int ticket = version;
        PlayerCharacter actor = player;
        IsPlaying = true;
        yield return null;
        if (ticket != version || !IsPlaying || actor == null) yield break;
        actor.SetCutsceneMoveTarget(stopPoint);
        float remaining = Vector2.Distance(actor.transform.position, stopPoint) / Mathf.Max(0.1f, actor.SP) * 2f + 3f;

        // 위치를 직접 보간하지 않고 Player가 충돌·속도·몸체 모션을 처리하도록 대기
        while (ticket == version && IsPlaying && actor != null && actor.isActiveAndEnabled
            && !actor.IsDead && actor.IsCutScene && Vector2.Distance(actor.transform.position, stopPoint) > StopDistance)
        {
            yield return null;
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                LastError = "Player entrance could not reach its handoff point.";
                Cancel();
                yield break;
            }
        }
        if (ticket != version || !IsPlaying) yield break;
        if (actor == null || !actor.isActiveAndEnabled || actor.IsDead || !actor.IsCutScene)
        {
            LastError = "Player entrance was interrupted before arrival.";
            Cancel();
            yield break;
        }

        // 정상 도착만 저장 요청하고 이동을 멈춘 뒤 월드 관리자의 입력 인계 허용
        actor.SetCutsceneMoveTarget(null);
        save.Set("EntranceCompleted", true);
        SaveManager.Save();
        HasPendingEntrance = IsPlaying = false;
        WasCompleted = true;
        RestoreControl();
    }

    /// <summary>자기 이동 명령·컷씬 권한을 끝내고 교체된 Player의 연결은 보존</summary>
    private void RestoreControl()
    {
        if (!ownsControl) return;
        ownsControl = false;
        if (player != null)
        {
            player.SetCutsceneMoveTarget(null);
            player.SetMoveInput(Vector2.zero);
            player.IsCutScene = previousCutscene;
        }
        if (input != null && input.Player == player) input.BlockInput = previousInputBlock;

        // 다른 연출이 새로 가져간 초점은 보존하고 자기 고정 화면만 일반 추적으로 복구
        if (gameCamera != null && gameCamera.Target == entranceCameraTarget)
            gameCamera.Follow(previousCameraTarget);
        entranceCameraTarget = previousCameraTarget = null;
    }

    /// <summary>배치 파일의 비정상 좌표로 무한 이동이 시작되지 않도록 검사</summary>
    private static bool Finite(Vector3 point)
    {
        return !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y);
    }
}
