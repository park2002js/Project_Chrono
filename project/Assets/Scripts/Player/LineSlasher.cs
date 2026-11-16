using UnityEngine;

/// <summary>확정 Line의 실행 기하와 한 번의 사각 타격·중단 상태 관리</summary>
public class LineSlasher
{
    public bool IsRunning { get; private set; } // Slash 연출과 마지막 한 번의 판정이 진행 중인 상태
    public int AttackId { get; private set; } // 이번 Slash의 피해와 여러 부위를 연결할 번호
    public Line ActiveLine { get; private set; } // Player에서 소비한 뒤 이번 실행 동안만 유지할 고정 기하
    public float HitWidth { get; private set; } // 시전 시작 때 고정한 선 오라 전체를 포함할 직사각 판정 폭
    public CollisionBox HitBox { get; } = new CollisionBox(); // 재사용 사각 Cast와 대상별 중복 기록

    private bool applied; // 같은 실행의 판정이 반복되지 않도록 기록

    /// <summary>Skill이 소비한 유효 경로와 피해로 이번 실행 준비</summary>
    public void Execute(Line line, float hitWidth, DamageInstance damage, int layerMask)
    {
        ActiveLine = line;
        HitWidth = hitWidth;
        AttackId = damage.AttackId;
        applied = false;
        IsRunning = true;
        HitBox.Begin(damage, layerMask);
    }

    /// <summary>마지막 정지 시점에 Line 전체 사각을 한 번 조회</summary>
    public int ApplyHits(PlayerCharacter player, BossCharacter boss)
    {
        if (!IsRunning || applied) return 0;
        applied = true;
        Physics2D.SyncTransforms();
        Vector2 center = (ActiveLine.StartPoint + ActiveLine.EndPoint) * 0.5f;
        float angle = Mathf.Atan2(ActiveLine.Direction.y, ActiveLine.Direction.x) * Mathf.Rad2Deg;
        return HitBox.QuerySlash(center, new Vector2(ActiveLine.Length, HitWidth), angle, player, boss);
    }

    /// <summary>실행을 마치고 다음 Slash에 판정·적중 기록이 남지 않도록 정리</summary>
    public void Complete() => Cancel(); // 같은 기능이지만 호출하는 쪽에서는 구분하기 쉽도록 다음과 같이 정의

    /// <summary>사망·컷씬·외부 차단에서 남은 판정과 실행 기하 해제</summary>
    public void Cancel()
    {
        IsRunning = false;
        HitBox.End();
        HitBox.ClearHits();
        ActiveLine = default;
        HitWidth = 0f;
    }
}
