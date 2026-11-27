using UnityEngine;

/// <summary>씬 준비 중 로딩 화면과 실패 이유 표시</summary>
public class UI_Loading : MonoBehaviour
{
    public bool IsVisible; // 씬 준비 중 로딩 화면 표시 여부
    public string FailureMessage; // 로딩 문구 대신 표시할 오류, 비어 있으면 준비 중 안내

    private static readonly string[] loadingMessages = { "Loading", "Loading.", "Loading..", "Loading..." }; // 매 GUI 호출에서 새 문자열을 만들지 않고 재사용할 준비 표시 네 단계

    /// <summary>게임 시간 정지와 무관한 준비 표시와 실패 안내</summary>
    private void OnGUI()
    {
        // 로딩 화면이 필요할 때만 출력
        if (!IsVisible)
            return;

        // 뒤에서 바뀌는 씬과 맵이 비치지 않게 검은 면으로 화면을 완전히 덮고 준비 중 또는 실패 문구 표시
        GUI.depth = -100;
        Color color = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
        string message = string.IsNullOrEmpty(FailureMessage)
            ? loadingMessages[(int)(Time.realtimeSinceStartup * 2f) % loadingMessages.Length] : FailureMessage;
        GUI.Label(new Rect(24f, Screen.height - 90f, Screen.width - 48f, 60f), message);
    }

    /// <summary>이전 오류를 지우고 준비 화면 표시</summary>
    public void Show()
    {
        IsVisible = true;
        FailureMessage = null;
    }

    /// <summary>준비 완료 후 로딩 화면 숨김</summary>
    public void Hide()
    {
        IsVisible = false;
    }

    /// <summary>Title로 복귀할 수 없는 씬 오류를 화면에 표시</summary>
    public void ShowFailure(string message)
    {
        IsVisible = true;
        FailureMessage = message;
    }
}
