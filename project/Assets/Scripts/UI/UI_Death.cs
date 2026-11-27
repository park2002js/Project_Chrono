using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>생성·사망 연출의 Player를 제외한 월드 흑백·암전 표시</summary>
public class UI_Death : MonoBehaviour
{
    public bool IsVisible { get; private set; } // 생성·사망 표시가 열려 있는 상태
    public bool IsDeath { get; private set; } // 사망 흑백을 적용하는 현재 표시 모드
    public Material GrayscaleMaterial; // Sprite 텍스처와 tint를 함께 흑백으로 표시할 공용 재질
    private UIController ui; // 자기 HUD 표시 모드를 전환할 컴포넌트
    private UI_Draw shade; // 편집 안내 없이 재사용할 월드 암전
    private CharacterRendering rendering; // 암전 위에 Player를 표시할 자기 세션 표현
    private Transform worldRoot; // 같은 세션의 월드 Sprite를 한 번 수집할 부모
    private Transform playerRoot; // 월드 흑백에서 제외할 Player 부모
    private Renderer[] worldRenderers = Array.Empty<Renderer>(); // 사망 순간의 Sprite·Tilemap 월드 표현 목록
    private Material[] worldMaterials = Array.Empty<Material>(); // 사망 표시를 닫을 때 복원할 표시별 원본 재질
    private bool previousHighlight; // 자기 연출 전 Player 표시 순서 복원 기준

    /// <summary>자기 UI 참조만 준비하고 Actor는 이후 Bind에서 연결</summary>
    private void Awake()
    {
        ui = GetComponent<UIController>();
        shade = GetComponent<UI_Draw>();
    }

    private void OnDisable() => Hide();

    /// <summary>준비된 같은 세션의 Player·월드 표현 참조를 한 번 연결</summary>
    public void Bind(PlayerCharacter player, Transform root)
    {
        playerRoot = player.transform;
        rendering = player.GetComponent<CharacterRendering>();
        worldRoot = root;
    }

    /// <summary>Player 강조·암전과 사망 때의 월드 흑백만 표시</summary>
    public void Show(bool dead)
    {
        if (!IsVisible)
        {
            previousHighlight = rendering.IsDrawHighlighted;
            ui.SetDeathMode(true);
            shade.SetVisible(true, null);
            rendering.SetDrawHighlight(true);
        }
        if (dead && !IsDeath && worldRoot != null)
        {
            worldRenderers = worldRoot.GetComponentsInChildren<Renderer>(true)
                .Where(value => value is SpriteRenderer || value is TilemapRenderer).ToArray();
            worldMaterials = new Material[worldRenderers.Length];
            for (int i = 0; i < worldRenderers.Length; i++)
            {
                Renderer renderer = worldRenderers[i];
                worldMaterials[i] = renderer.sharedMaterial;
                if (!renderer.transform.IsChildOf(playerRoot)) renderer.sharedMaterial = GrayscaleMaterial;
            }
        }
        IsVisible = true;
        IsDeath = dead;
    }

    /// <summary>자기 연출이 바꾼 재질·정렬·암전·HUD만 원복</summary>
    public void Hide()
    {
        if (!IsVisible) return;
        IsVisible = IsDeath = false;
        for (int i = 0; i < worldRenderers.Length; i++)
            if (worldRenderers[i] != null) worldRenderers[i].sharedMaterial = worldMaterials[i];
        worldRenderers = Array.Empty<Renderer>();
        worldMaterials = Array.Empty<Material>();
        if (rendering != null) rendering.SetDrawHighlight(previousHighlight);
        if (shade != null) shade.SetVisible(false, null);
        if (ui != null) ui.SetDeathMode(false);
    }
}
