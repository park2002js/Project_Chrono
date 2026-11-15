using System;
using UnityEngine;

/// <summary>장판의 전조·일회 피해·수명을 공유하고 회전한 사각 범위를 사용하는 레이저</summary>
public class LaserProjectile : AreaProjectile
{
    public float Length = 6f; // 원점 중심으로 양쪽에 펼칠 레이저 전체 길이
    public float Width = 0.4f; // 레이저 방향에 수직인 피해 범위의 폭

    /// <summary>레이저의 사각 Collider를 최초 한 번 확보</summary>
    protected override Collider2D PrepareShape()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        return box != null ? box : gameObject.AddComponent<BoxCollider2D>();
    }

    /// <summary>레이저의 길이·폭을 발사 전에 확인</summary>
    protected override void ValidateShape()
    {
        if (!Nonnegative(Length) || Length <= 0f || !Nonnegative(Width) || Width <= 0f)
            throw new ArgumentException("A laser requires positive finite length and width.");
    }

    /// <summary>발사 방향으로 회전한 사각 판정과 동일 크기의 표시 설정</summary>
    protected override void ConfigureShape()
    {
        // 원점을 빔 중심으로 사용해 방향·표시·판정을 함께 정렬
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        BoxCollider2D box = (BoxCollider2D)shape;
        box.size = new Vector2(Length, Width);
        box.offset = Vector2.zero;
        if (visual != null)
        {
            visual.drawMode = SpriteDrawMode.Sliced;
            visual.size = box.size;
        }
    }
}
