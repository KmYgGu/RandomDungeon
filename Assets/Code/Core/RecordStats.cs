/// <summary>
/// 기록에 남기는 것들. 늘릴 때는 여기와 RecordCatalog.All 두 곳만 고치면 된다.
/// </summary>
public enum RecordStat
{
    // ── 모험 ──
    RunsStarted,
    Deaths,
    Clears,

    // ── 점수 ──
    BestScore,
    LastScore,

    // ── 얼마나 갔나 ──
    DeepestStage,
    LastStage,
    LongestRun,

    // ── 싸움 ──
    BiggestHitDealt,
    BiggestHitTaken,
    LongestMissStreak,
    EnemiesDefeated,

    // ── 그 밖 ──
    TrapsTriggered,
    FoodEaten,
    ItemsPicked,
    BestEnhancement,
    MostGold,
    HighestLevel,
}

/// <summary>기록을 어떻게 쌓는가.</summary>
public enum RecordKind
{
    /// <summary>가장 큰 값만 남긴다("가장 크게 준 데미지").</summary>
    Max,

    /// <summary>들어온 만큼 더한다("함정에 걸린 횟수").</summary>
    Total,

    /// <summary>마지막 값으로 덮는다("지난 점수").</summary>
    Last,
}

/// <summary>기록 한 줄의 정의.</summary>
public class RecordDefinition
{
    /// <summary>
    /// 저장에 쓰는 이름. <b>enum 이름이나 순서를 쓰지 않는다.</b>
    ///
    /// 순서로 저장하면 항목을 중간에 하나 끼워 넣는 순간 지난 기록이 통째로
    /// 밀린다 — 함정 횟수 자리에 점수가 들어가는 식인데, 에러는 나지 않고
    /// 숫자만 엉뚱해진다. 이름으로 저장하면 순서를 바꾸든 항목을 지우든 안전하다.
    /// </summary>
    public readonly string Id;

    /// <summary>화면에 보이는 이름.</summary>
    public readonly string Label;

    public readonly RecordStat Stat;
    public readonly RecordKind Kind;

    /// <summary>이 항목 앞에서 한 줄 띄운다. 갈래를 눈으로 가르는 데만 쓴다.</summary>
    public readonly bool StartsGroup;

    public RecordDefinition(
        RecordStat stat, string id, string label, RecordKind kind, bool startsGroup = false)
    {
        Stat = stat;
        Id = id;
        Label = label;
        Kind = kind;
        StartsGroup = startsGroup;
    }
}

/// <summary>
/// 기록 항목들의 표.
///
/// 순서가 화면에 보이는 순서다. 새 기록을 늘릴 때는 여기 한 줄을 더하고,
/// 그것을 세는 자리에서 GameManager.ReportRecord 를 부르면 끝이다 —
/// 저장·불러오기·화면은 저절로 따라온다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class RecordCatalog
{
    public static readonly RecordDefinition[] All =
    {
        new RecordDefinition(RecordStat.RunsStarted, "runs", "던전 시도 횟수", RecordKind.Total),
        new RecordDefinition(RecordStat.Deaths, "deaths", "죽은 횟수", RecordKind.Total),
        new RecordDefinition(RecordStat.Clears, "clears", "클리어 횟수", RecordKind.Total),

        new RecordDefinition(RecordStat.BestScore, "score.best", "최고 점수", RecordKind.Max, true),
        new RecordDefinition(RecordStat.LastScore, "score.last", "지난 점수", RecordKind.Last),

        new RecordDefinition(RecordStat.DeepestStage, "stage.deepest", "최고 도달 층", RecordKind.Max, true),
        new RecordDefinition(RecordStat.LastStage, "stage.last", "지난 도달 층", RecordKind.Last),
        new RecordDefinition(RecordStat.LongestRun, "turns.most", "최대 턴 수", RecordKind.Max),

        new RecordDefinition(RecordStat.BiggestHitDealt, "damage.dealt", "가장 크게 준 피해", RecordKind.Max, true),
        new RecordDefinition(RecordStat.BiggestHitTaken, "damage.taken", "가장 크게 받은 피해", RecordKind.Max),
        new RecordDefinition(RecordStat.LongestMissStreak, "miss.streak", "최대 연속 빗나감", RecordKind.Max),
        new RecordDefinition(RecordStat.EnemiesDefeated, "kills", "쓰러뜨린 적", RecordKind.Total),

        new RecordDefinition(RecordStat.TrapsTriggered, "traps", "함정에 걸린 횟수", RecordKind.Total, true),
        new RecordDefinition(RecordStat.FoodEaten, "food", "음식을 먹은 횟수", RecordKind.Total),
        new RecordDefinition(RecordStat.ItemsPicked, "items.picked", "주운 아이템", RecordKind.Total),
        new RecordDefinition(RecordStat.BestEnhancement, "enhance.best", "장비 강화 최고 수치", RecordKind.Max),
        new RecordDefinition(RecordStat.MostGold, "gold.most", "최고 소지금", RecordKind.Max),
        new RecordDefinition(RecordStat.HighestLevel, "level.most", "최고 레벨", RecordKind.Max),
    };

    /// <summary>표에서 찾는다. 없으면 null.</summary>
    public static RecordDefinition Find(RecordStat stat)
    {
        foreach (RecordDefinition definition in All)
        {
            if (definition.Stat == stat) return definition;
        }
        return null;
    }

    /// <summary>저장된 이름으로 찾는다. 모르는 이름이면 null(지워진 옛 항목).</summary>
    public static RecordDefinition Find(string id)
    {
        foreach (RecordDefinition definition in All)
        {
            if (definition.Id == id) return definition;
        }
        return null;
    }
}
