using UnityEngine;

/// <summary>
/// 빈 공간에 물길을 흘려 층을 덜 밋밋하게 만든다.
///
/// 주의: 이것은 순수한 장식이다. 물은 isSpecialState 인 대상만 지날 수 있는데
/// 게임 안에서 그 상태를 켜는 코드가 아직 없으므로, 물은 벽과 똑같이 막힌 칸이다.
/// 그래서 이미 통로나 방이 있는 칸은 절대 건드리지 않는다 —
/// 건드리면 길이 끊긴다.
/// </summary>
public class WaterwayDecorator
{
    /// <summary>한 층에 물길이 생길 확률.</summary>
    private const float WaterwayChance = 0.9f;

    private readonly DungeonTilePainter painter;
    private readonly DungeonLayout layout;

    public WaterwayDecorator(DungeonTilePainter painter, DungeonLayout layout)
    {
        this.painter = painter;
        this.layout = layout;
    }

    public void Decorate()
    {
        if (layout.Rooms.Count < 2) return;
        if (Random.value >= WaterwayChance) return;

        int fromIndex = Random.Range(0, layout.Rooms.Count);
        int toIndex = Random.Range(0, layout.Rooms.Count);
        if (fromIndex == toIndex) return;

        Vector3Int cursor = layout.Rooms[fromIndex].Center;
        Vector3Int target = layout.Rooms[toIndex].Center;

        FlowAlongAxis(ref cursor, horizontal: true, target: target.x);
        FlowAlongAxis(ref cursor, horizontal: false, target: target.y);
    }

    private void FlowAlongAxis(ref Vector3Int cursor, bool horizontal, int target)
    {
        int current = horizontal ? cursor.x : cursor.y;
        int step = target > current ? 1 : (target < current ? -1 : 0);
        if (step == 0) return;

        while (current != target)
        {
            current += step;

            cursor = horizontal
                ? new Vector3Int(current, cursor.y, 0)
                : new Vector3Int(cursor.x, current, 0);

            if (!painter.IsEmpty(cursor)) continue;

            painter.PaintWater(cursor);
            SpreadSideways(cursor, spreadVertically: horizontal, range: Random.Range(1, 3));
        }
    }

    /// <summary>물길 좌우(또는 위아래)로 번지게 한다.</summary>
    private void SpreadSideways(Vector3Int center, bool spreadVertically, int range)
    {
        for (int distance = 1; distance <= range; distance++)
        {
            Vector3Int offset = spreadVertically
                ? new Vector3Int(0, distance, 0)
                : new Vector3Int(distance, 0, 0);

            PaintIfEmpty(center + offset);
            PaintIfEmpty(center - offset);
        }
    }

    private void PaintIfEmpty(Vector3Int position)
    {
        if (painter.IsEmpty(position))
            painter.PaintWater(position);
    }
}
