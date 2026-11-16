using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>요청된 Slash 연출 아트·화면 효과·초기 확대 목표를 기존 프리팹에 연결</summary>
public static class SlashPresentationSetup
{
    /// <summary>발도 R02와 공간참을 연결하고 현재 기본 화면 배율은 보존</summary>
    [MenuItem("Tools/Chrono/Connect Slash Presentation")]
    public static void Run()
    {
        // 현재 선택 아트와 월드 전용 렌더 패스·자체 제작음을 최소 범위로 연결
        PlayerArtSetup.ApplySlash();
        SlashPresentationFxSetup.Run();

        // 기본 추적·Draw·사망 설정을 유지하고 새 Slash의 두 확대 목표만 초기 연결
        const string path = "Assets/Prefabs/Startup/WorldCamera.prefab";
        if (File.ReadAllText(path).Contains("  SlashFastZoom:")) return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GameCamera camera = root.GetComponent<GameCamera>();
            camera.SlashFastZoom = 3.2f;
            camera.SlashZoom = 2.8f;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    /// <summary>자산 연결·가져오기 뒤 실제 Play Mode의 단계·판정·화면·취소 검사</summary>
    [MenuItem("Tools/Chrono/Check Slash Presentation")]
    public static void RunChecks()
    {
        Run();
        EditorApplication.delayCall += BeginChecksWhenReady; // 자산 가져오기가 끝난 다음 갱신에서 Play 검사 시작
    }

    /// <summary>컴파일과 자산 재로드가 끝나면 기존 격리 저장 검사 실행기에 전달</summary>
    private static void BeginChecksWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BeginChecksWhenReady; // 진행 중인 자산 가져오기가 끝날 때까지 재요청
            return;
        }
        EditorApplication.isPaused = false;
        StartupFlowChecks.RunSlash();
    }
}
