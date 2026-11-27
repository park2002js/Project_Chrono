using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Title의 세 슬롯과 선택 데이터를 관리하고 저장 복사본을 순서대로 기록</summary>
public static class SaveManager
{
    public static SaveData[] Slots = new SaveData[3]; // Title에서 선택할 세 슬롯의 데이터를 보관하며 빈 슬롯은 null
    public static int SelectSlot { get; private set; } = -1; // 마지막으로 선택한 슬롯 번호이며 최초 선택 전에는 -1
    public static SaveData Current => current; // 외부에서 조회·Set할 선택 데이터를 제공하며 Title 복귀에도 같은 참조를 유지
    public static bool IsLoading { get; private set; } // 공용 슬롯 데이터를 읽는 중인지 Title의 자체 준비 과정에 전달
    public static bool IsReady { get; private set; } // Title에서 세 슬롯을 표시·선택할 수 있는 준비 상태
    public static string LoadError { get; private set; } // Title의 재시도 안내에 사용할 슬롯 로드 실패 이유

    private static string saveDirectory; // 슬롯 파일을 읽고 쓸 공통 저장 폴더
    private static SaveData current; // 선택한 슬롯의 원본 참조로 Current를 통해 외부에 제공
    private static bool isSaving; // 저장 큐 처리 중복 실행과 조기 종료를 막는 상태
    private static Queue<SaveData> PendingSaves = new Queue<SaveData>(); // 요청 순서대로 기록할 독립 저장 복사본 큐

    /// <summary>GameManager의 최초 부팅에서 저장 경로와 메모리 상태를 준비</summary>
    public static void Initialize(string directory)
    {
        // 진행 중 파일 작업이 참조하는 경로의 교체 방지
        if (isSaving || IsLoading)
            throw new InvalidOperationException("저장 데이터를 처리 중에는 저장 경로를 변경할 수 없음");

        // 최초 부팅의 슬롯·선택 데이터·대기 큐 초기화
        saveDirectory = directory;
        Slots = new SaveData[3];
        current = null;
        SelectSlot = -1;
        IsReady = false;
        LoadError = null;
        PendingSaves.Clear();
    }

    /// <summary>부팅·Title 복귀에서 최신 슬롯 목록을 읽으며 선택 데이터 참조는 유지</summary>
    public static async UniTask LoadSlotsAsync()
    {
        // 중복 로드 요청은 기존 로드가 끝날 때까지 대기
        if (IsLoading)
        {
            await UniTask.WaitUntil(() => !IsLoading);
            return;
        }

        // Title에 준비 중 상태를 알리고 파일 작업 준비
        IsLoading = true;
        IsReady = false;
        LoadError = null;
        try
        {
            EnsureInitialized();
            await FlushAsync(); // 이전 저장이 끝난 최신 파일을 읽기 위한 대기
            saveDirectory = Path.GetFullPath(saveDirectory);
            Directory.CreateDirectory(saveDirectory);
            Directory.GetFiles(saveDirectory);

            // 각 슬롯의 sav·bak 복구를 시도하고 읽힌 데이터만 수집
            var loadedSlots = new SaveData[3];
            for (int index = 0; index < loadedSlots.Length; index++)
            {
                var data = new SaveData(saveDirectory, index);
                if (await data.LoadAsync())
                    loadedSlots[index] = data;
                if (!string.IsNullOrEmpty(data.LastError))
                    Debug.LogWarning($"[SaveManager] {data.LastError}");
            }

            // 로드 결과를 한 번에 공개하여 Title의 표시·선택 허용
            Slots = loadedSlots;
            IsReady = true;
        }
        catch (Exception exception)
        {
            LoadError = exception.Message;
            Debug.LogError($"[SaveManager] 슬롯 준비 실패: {LoadError}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Title에서 선택한 슬롯의 데이터를 다음 MainWorld가 사용할 참조로 지정</summary>
    public static void SetCurrent(int index)
    {
        // 유효한 기존 슬롯만 현재 선택 데이터로 허용
        EnsureSlotIndex(index);
        if (Slots[index] == null)
            throw new InvalidOperationException("빈 슬롯은 NewSaveFile로 생성해야 함");

        // 슬롯 배열을 재조회하지 않고 선택한 객체의 참조 보관
        current = Slots[index];
        SelectSlot = index;
    }

    /// <summary>빈 슬롯 또는 새 게임 확인을 받은 슬롯을 초기화하고 최초 저장을 요청</summary>
    public static SaveData NewSaveFile(int index, int stageIndex = 0)
    {
        // 진행 중 저장과 새 슬롯 생성의 충돌 방지
        EnsureInitialized();
        EnsureSlotIndex(index);
        if (isSaving)
            throw new InvalidOperationException("슬롯 초기화 전에 진행 중인 저장을 기다려야 함");

        // 새 게임의 체력·진행·플레이 시간 기본값 적용
        Slots[index] = new SaveData(saveDirectory, index);
        SetCurrent(index);
        current.Set("PlayerHP", 100f);
        current.Set("DefeatedBosses", new List<string>());
        current.Set("CheckpointId", "start");
        current.Set("ActivatedCheckpoints", new List<string> { "start" });
        current.Set("DeathCount", 0);
        current.Set("PlayTime", 0d);
        current.Set("LastSavedAt", string.Empty);
        current.Set("EncounteredBosses", new List<string>());
        current.Set("EntranceCompleted", false);
        current.Set("StageIndex", stageIndex);
        current.Set("DemoCompleted", false);

        // 최초 저장을 요청하고 MainWorld에서 사용할 선택 데이터 반환
        Save();
        return current;
    }

    /// <summary>현재 값의 저장을 요청받으며 이후 성공·실패와 복구는 내부에서 처리</summary>
    public static void Save(int backupCount = 1)
    {
        try
        {
            // 저장 경로와 현재 선택 데이터의 준비 여부 확인
            EnsureInitialized();
            if (current == null)
                throw new InvalidOperationException("저장할 슬롯이 선택되지 않음");

            // 게임 데이터의 이후 변경과 분리한 저장 시각·값 복사본 생성
            current.Set("LastSavedAt", DateTimeOffset.UtcNow.ToString("O"));
            PendingSaves.Enqueue(current.Copy());

            // 기존 처리기가 없을 때만 저장 큐 처리 시작
            if (isSaving)
                return;
            isSaving = true;
            ProcessSavesAsync().Forget(); // 호출자의 게임 흐름을 막지 않고 저장 큐 처리 시작
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveManager] 저장 요청 실패: {exception.Message}");
        }
    }

    /// <summary>Title에서 확인받은 슬롯의 로컬 sav·bak·임시 파일을 지우고 슬롯을 비운 성공 여부 반환</summary>
    public static async UniTask<bool> DeleteSlotAsync(int index)
    {
        // 대기 중인 저장이 삭제한 파일을 다시 만들지 않도록 완료 대기
        EnsureInitialized();
        EnsureSlotIndex(index);
        await FlushAsync(); // 요청된 저장·백업이 모두 끝날 때까지 삭제 보류

        // sav가 먼저 사라진 뒤 bak로 되살아나지 않도록 백업부터 삭제
        var data = new SaveData(saveDirectory, index);
        try
        {
            foreach (string path in new[] { data.BackupPath + ".tmp", data.FilePath + ".tmp", data.BackupPath, data.FilePath })
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogError($"[SaveManager] {data.FileName} 삭제 실패: {exception.Message}");
            return false;
        }

        // 파일 삭제에 성공한 슬롯만 목록과 선택 데이터에서 제거
        Slots[index] = null;
        if (SelectSlot == index)
        {
            current = null;
            SelectSlot = -1;
        }
        return true;
    }

    /// <summary>Title 재로드·슬롯 초기화·앱 종료에서 진행 중인 기록과 복구 완료까지 대기</summary>
    public static async UniTask FlushAsync()
    {
        while (isSaving)
            await UniTask.Yield();
    }

    /// <summary>요청 순서대로 기록하고 실패한 요청 이후에도 저장 처리 계속</summary>
    private static async UniTask ProcessSavesAsync()
    {
        try
        {
            while (PendingSaves.Count > 0)
            {
                SaveData snapshot = PendingSaves.Dequeue();
                try
                {
                    await snapshot.SaveAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[SaveManager] {exception.Message}");
                }
            }
        }
        finally
        {
            isSaving = false;
        }
    }

    /// <summary>초기화 전에 파일 API를 사용하는 연결 오류를 전달</summary>
    private static void EnsureInitialized()
    {
        if (string.IsNullOrEmpty(saveDirectory))
            throw new InvalidOperationException("SaveManager.Initialize가 먼저 필요함");
    }

    /// <summary>Title이나 테스트에서 전달한 잘못된 슬롯 번호 거부</summary>
    private static void EnsureSlotIndex(int index)
    {
        if (index < 0 || index >= 3)
            throw new ArgumentOutOfRangeException(nameof(index));
    }
}
