using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Boss의 저장된 처치 상태·기본 위치·조우 영역 초기화</summary>
public class BossSpawner : MonoBehaviour
{
    public BossCharacter Boss; // 저장된 처치 상태와 배치 위치를 적용할 Boss 인스턴스
    public Collider2D EncounterTrigger; // 월드 준비 완료 후 조우를 허용할 Map의 Boss 감지 영역
    public Vector3 BasePosition; // Boss 배치와 기본 위치 설정에 함께 사용할 월드 좌표
    private Collider2D[] bodies; // 재도전에서 기본 충돌 상태로 복원할 몸체
    private SpriteRenderer[] sprites; // 처치 표시와 기본색을 전환할 자기 Sprite
    private Color[] colors; // 최초 준비 때 보관할 기본 Sprite 색
    private bool[] bodyStates; // 최초 준비 때 보관할 Collider 활성 상태
    private BossStructure[] structures; // 재도전에서 같은 부위 객체를 복원할 목록
    private float[] structureHealth; // 최초 준비 때 보관할 각 부위의 기본 체력

    /// <summary>같은 Boss에서 재사용할 몸체·표시 참조를 한 번 확보</summary>
    private void Awake()
    {
        Boss = GetComponent<BossCharacter>();
        bodies = GetComponentsInChildren<Collider2D>(true);
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        colors = new Color[sprites.Length];
        bodyStates = new bool[bodies.Length];
        structures = GetComponentsInChildren<BossStructure>(true);
        structureHealth = new float[structures.Length];
        for (int i = 0; i < structures.Length; i++) structureHealth[i] = structures[i].HP;
        for (int i = 0; i < sprites.Length; i++) colors[i] = sprites[i].color;
        for (int i = 0; i < bodies.Length; i++) bodyStates[i] = bodies[i].enabled;
    }

    /// <summary>Map의 배치와 SaveData의 처치 기록을 기존 Boss 인스턴스에 적용</summary>
    public void Initialize(MapManager map, SaveData save)
    {
        // 초기화할 Boss와 배치·저장 데이터 확인
        if (Boss == null || !Boss.PatternsInitialized || map == null || save == null)
            throw new InvalidOperationException("Boss initialization requires Boss, Map, and save data.");

        // 기본 위치를 적용하고 월드 준비 완료 전 조우 차단
        PlaceAt(map.GetBossPosition(Boss.BossId));
        EncounterTrigger = map.GetEncounterArea(Boss.BossId);
        EncounterTrigger.enabled = false;

        // 처치 기록에 따라 체력을 복원하고 전투·그로기 상태 초기화
        bool defeated = save.Get<List<string>>("DefeatedBosses").Contains(Boss.BossId);
        Boss.InitializeHealth(defeated ? 0f : Boss.maxHP);
        Boss.ResetCombatState();

        // 처치된 Boss도 프리팹은 유지하고 충돌 비활성화·임시 색상으로 상태 구별
        if (defeated)
            ShowDefeated();
    }

    /// <summary>MapManager가 제공한 위치를 Boss와 재배치 기준에 적용</summary>
    public void PlaceAt(Transform point)
    {
        // 배치 대상과 위치가 없으면 초기화 실패로 전달
        if (point == null || Boss == null)
            throw new InvalidOperationException("Boss placement requires a spawn point and Boss.");

        // Boss의 현재 위치·회전과 재배치 기준 동기화
        BasePosition = point.position;
        Boss.BasePosition = BasePosition;
        transform.position = BasePosition;
        transform.rotation = point.rotation;
    }

    /// <summary>세션 해제 시 조우 영역과 Boss 참조 정리</summary>
    public void Release()
    {
        if (Boss != null) Boss.EndEncounter();
        if (EncounterTrigger != null)
            EncounterTrigger.enabled = false;
        EncounterTrigger = null;
        Boss = null;
    }

    /// <summary>처치 기록은 유지하고 같은 Boss의 재도전 수치·위치 복원</summary>
    public void ResetEncounter()
    {
        if (Boss == null) return;
        Boss.ResetCombatState();
        bool defeated = SaveManager.Current.Get<List<string>>("DefeatedBosses").Contains(Boss.BossId);
        Boss.InitializeHealth(defeated ? 0f : Boss.maxHP);
        transform.position = BasePosition;
        for (int i = 0; i < structures.Length; i++)
            if (structures[i] != null)
            {
                structures[i].Reset(structureHealth[i]);
                if (defeated) structures[i].Remove();
            }
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null) bodies[i].enabled = !defeated && bodyStates[i];
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null) sprites[i].color = defeated ? new Color(0.4f, 0.4f, 0.4f, colors[i].a) : colors[i];
        if (EncounterTrigger != null) EncounterTrigger.enabled = !defeated;
    }

    /// <summary>처치 사건에서 몸체·피격을 닫고 같은 Boss를 임시 사체색으로 표시</summary>
    public void ShowDefeated()
    {
        DisableCombatColliders();
        foreach (SpriteRenderer sprite in sprites)
            if (sprite != null) sprite.color = new Color(0.4f, 0.4f, 0.4f, sprite.color.a);
        if (EncounterTrigger != null) EncounterTrigger.enabled = false;
    }

    /// <summary>죽는 순간부터 몸체·부위·피격을 닫고 시각 사체 전환은 연출 뒤 수행</summary>
    public void DisableCombatColliders()
    {
        foreach (Collider2D shape in bodies) if (shape != null) shape.enabled = false;
        foreach (BossStructure part in structures) if (part != null) part.Remove();
        if (EncounterTrigger != null) EncounterTrigger.enabled = false;
    }
}
