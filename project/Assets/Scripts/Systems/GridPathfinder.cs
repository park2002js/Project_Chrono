using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>사각 구역을 격자로 나눠 막힌 칸을 피하는 A* 최단 경로의 다음 지점을 구하는 탐색기</summary>
public class GridPathfinder
{
    public Rect Area { get; private set; } // 격자로 나눈 월드 사각 구역
    public float CellSize { get; private set; } // 격자 한 칸의 월드 길이
    public int Width { get; private set; } // 가로 칸 수
    public int Height { get; private set; } // 세로 칸 수

    private const float Diagonal = 1.41421356f; // 대각 이동 한 번의 비용
    private bool[] blocked = Array.Empty<bool>(); // 칸마다 지나갈 수 없는지 여부
    private float[] cost = Array.Empty<float>(); // 출발 칸에서 각 칸까지 찾은 최소 비용
    private int[] parent = Array.Empty<int>(); // 최단 경로에서 각 칸의 직전 칸
    private int[] visit = Array.Empty<int>(); // 이번 탐색에서 비용을 기록한 칸인지 구분할 세대 번호
    private readonly List<int> open = new List<int>(); // 아직 확장하지 않은 후보 칸
    private readonly List<float> openScore = new List<float>(); // 후보 칸의 비용과 남은 거리 추정의 합
    private int generation; // 탐색마다 올려 이전 기록을 지우지 않고 무효화할 번호

    /// <summary>구역과 칸 크기를 정하고 막힌 칸을 한 번 조사</summary>
    public void Build(Rect area, float cellSize, Func<Vector2, bool> isBlocked)
    {
        // 구역을 덮는 칸 수와 탐색용 배열 준비
        if (cellSize <= 0f || area.width <= 0f || area.height <= 0f || isBlocked == null)
            throw new ArgumentException("A path grid requires a positive area, cell size and a blocking test.");
        Area = area;
        CellSize = cellSize;
        Width = Mathf.Max(1, Mathf.CeilToInt(area.width / cellSize));
        Height = Mathf.Max(1, Mathf.CeilToInt(area.height / cellSize));
        int count = Width * Height;
        blocked = new bool[count];
        cost = new float[count];
        parent = new int[count];
        visit = new int[count];
        generation = 0;

        // 칸 중심이 벽에 걸치는지 한 번만 조사
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                blocked[y * Width + x] = isBlocked(GetCenter(x, y));
    }

    /// <summary>출발점에서 도착점으로 가는 최단 경로에서 다음에 향할 칸의 중심을 구하고 길이 없으면 실패</summary>
    public bool TryGetNextPoint(Vector2 from, Vector2 to, out Vector2 next)
    {
        // 같은 칸이거나 격자가 없으면 도착점으로 곧장 이동
        next = to;
        if (blocked.Length == 0) return false;
        int start = GetIndex(from), goal = GetIndex(to);
        if (start == goal) return true;
        if (blocked[goal]) return false;

        // 비용과 추정 거리의 합이 가장 작은 칸부터 확장
        generation++;
        open.Clear();
        openScore.Clear();
        cost[start] = 0f;
        parent[start] = -1;
        visit[start] = generation;
        open.Add(start);
        openScore.Add(Estimate(start, goal));
        while (open.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (openScore[i] < openScore[best]) best = i;
            int current = open[best];
            int last = open.Count - 1;
            open[best] = open[last];
            openScore[best] = openScore[last];
            open.RemoveAt(last);
            if (current == goal)
            {
                // 도착 칸에서 거슬러 올라가 출발 칸 바로 다음 칸을 반환
                int step = goal;
                while (parent[step] != start && parent[step] >= 0) step = parent[step];
                next = step == goal ? to : GetCenter(step % Width, step / Width);
                return true;
            }
            int cx = current % Width, cy = current / Width;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                    int neighbor = ny * Width + nx;
                    if (blocked[neighbor]) continue;

                    // 대각 이동은 양옆 칸이 모두 열려 있을 때만 허용해 모서리를 뚫지 않음
                    if (dx != 0 && dy != 0 && (blocked[cy * Width + nx] || blocked[ny * Width + cx])) continue;
                    float reached = cost[current] + (dx != 0 && dy != 0 ? Diagonal : 1f);
                    if (visit[neighbor] == generation && reached >= cost[neighbor]) continue;
                    visit[neighbor] = generation;
                    cost[neighbor] = reached;
                    parent[neighbor] = current;
                    open.Add(neighbor);
                    openScore.Add(reached + Estimate(neighbor, goal));
                }
        }
        return false;
    }

    /// <summary>월드 좌표가 놓인 칸이 지나갈 수 없는 칸인지 조회</summary>
    public bool IsBlocked(Vector2 point)
    {
        return blocked.Length == 0 || blocked[GetIndex(point)];
    }

    /// <summary>가로·세로 번호의 칸 중심 월드 좌표</summary>
    private Vector2 GetCenter(int x, int y)
    {
        return new Vector2(Area.xMin + (x + 0.5f) * CellSize, Area.yMin + (y + 0.5f) * CellSize);
    }

    /// <summary>월드 좌표를 구역 안의 칸 번호로 변환</summary>
    private int GetIndex(Vector2 point)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((point.x - Area.xMin) / CellSize), 0, Width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((point.y - Area.yMin) / CellSize), 0, Height - 1);
        return y * Width + x;
    }

    /// <summary>8방향 이동에서 두 칸 사이에 남은 최소 비용 추정</summary>
    private float Estimate(int from, int to)
    {
        int dx = Mathf.Abs(from % Width - to % Width), dy = Mathf.Abs(from / Width - to / Width);
        return Mathf.Max(dx, dy) + (Diagonal - 1f) * Mathf.Min(dx, dy);
    }
}
