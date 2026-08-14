using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에 놓여 있는 물건(아이템 / 계단 / 함정)의 위치 목록.
///
/// 예전에는 GameManager 의 public static List&lt;Vector3&gt; GroundSetPositions 라서
/// RandomDung, Player, GameManager 어디서든 Add / Remove 를 직접 호출했다.
/// 어떤 규칙으로 채워지고 비워지는지 한 화면에서 볼 수 없었다.
/// </summary>
public class GroundObjectRegistry
{
    // 같은 칸에 두 번 등록되지 않도록 비교 오차를 둔다.
    private const float SamePositionThreshold = 0.1f;

    /// <summary>
    /// 위 오차의 제곱. 견줄 때는 이쪽을 쓴다.
    ///
    /// Vector3.Distance 는 제곱근을 뽑는데, "가까운가" 를 묻는 데에는 제곱근이
    /// 필요 없다 — 제곱근은 커지는 순서를 바꾸지 않으므로 양쪽을 제곱한 채로
    /// 견줘도 답이 같다. 목록을 통째로 훑는 자리라(Add 가 매번 Contains 를 부른다)
    /// 비교 한 번마다 제곱근이 하나씩 붙어 있었다.
    ///
    /// 실제로 들어오는 자리는 칸 중심이라 서로 같거나 한 칸(1.0) 넘게 떨어져
    /// 있고, 경계인 0.1 근처에 놓이는 일이 없다. 부동소수 반올림이 답을 가를
    /// 여지가 없는 이유다.
    /// </summary>
    private const float SamePositionThresholdSqr = SamePositionThreshold * SamePositionThreshold;

    private readonly List<Vector3> positions = new List<Vector3>();

    /// <summary>인스펙터 표시나 디버그용 읽기 전용 뷰.</summary>
    public IReadOnlyList<Vector3> Positions => positions;

    public int Count => positions.Count;

    public void Clear() => positions.Clear();

    public void Add(Vector3 position)
    {
        if (!Contains(position))
            positions.Add(position);
    }

    public bool Contains(Vector3 position)
    {
        foreach (Vector3 p in positions)
        {
            if ((p - position).sqrMagnitude < SamePositionThresholdSqr)
                return true;
        }
        return false;
    }

    public bool Remove(Vector3 position)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            if ((positions[i] - position).sqrMagnitude < SamePositionThresholdSqr)
            {
                positions.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>디버그용 필드에 넣기 위한 복사본.</summary>
    public List<Vector3> ToList() => new List<Vector3>(positions);
}
