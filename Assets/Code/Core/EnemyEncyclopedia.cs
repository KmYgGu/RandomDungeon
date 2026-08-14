using System.Collections.Generic;

/// <summary>
/// 적을 종류마다 몇 마리나 쓰러뜨렸는지 적어 두는 수첩.
///
/// ── 아이템 도감과 다른 점 ─────────────────────────────────────────
/// 아이템 도감(ItemEncyclopedia)은 "봤다/못 봤다" 둘뿐이라 번호만 모아 두면 됐다.
/// 적은 <b>몇 번 쓰러뜨렸는가</b>가 곧 도감에 얼마나 열리는지를 정하므로
/// (EnemyEncyclopediaReport) 횟수까지 센다.
///
/// 한 번도 못 쓰러뜨린 적은 목록에서 물음표로 가려진다. 만나 본 것과
/// 쓰러뜨린 것을 따로 세지 않는 이유는, 이 게임에서 적을 자세히 들여다볼 수 있는
/// 순간이 결국 싸워서 이겼을 때이기 때문이다.
///
/// 판이 아니라 <b>모험 전체</b>에 걸쳐 남는다(GameManager.ResetRun 이 건드리지 않는다).
/// 저장하는 법은 모른다 — 그 일은 EncyclopediaStorage 가 한다.
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class EnemyEncyclopedia
{
    private readonly Dictionary<int, int> defeats = new Dictionary<int, int>();

    /// <summary>이 종류를 몇 마리 쓰러뜨렸는가. 한 번도 없으면 0.</summary>
    public int DefeatsOf(int monsterId)
        => defeats.TryGetValue(monsterId, out int count) ? count : 0;

    /// <summary>한 번이라도 쓰러뜨려 본 적이 있는가. 도감에 이름이 뜨는 조건이다.</summary>
    public bool IsDiscovered(int monsterId) => DefeatsOf(monsterId) > 0;

    /// <summary>
    /// 한 마리 쓰러뜨렸다고 적는다. 적고 난 뒤의 횟수를 돌려준다.
    ///
    /// 번호가 0 인 것은 정의가 연결되지 않은 적이라 받지 않는다(0 을 돌려준다) —
    /// 받아 두면 서로 다른 적들이 0 번 하나를 나눠 갖는다. MarkBook 과 같은 규칙이다.
    /// </summary>
    public int Record(int monsterId)
    {
        if (monsterId == 0) return 0;

        int next = DefeatsOf(monsterId) + 1;
        defeats[monsterId] = next;
        return next;
    }

    /// <summary>저장할 글자. "번호:횟수" 를 쉼표로 늘어놓는다.</summary>
    public string Save()
    {
        var pieces = new List<string>();
        foreach (KeyValuePair<int, int> pair in defeats)
            pieces.Add($"{pair.Key}:{pair.Value}");

        return string.Join(",", pieces);
    }

    /// <summary>저장된 글자에서 되살린다. 못 읽는 조각은 조용히 건너뛴다.</summary>
    public static EnemyEncyclopedia Load(string saved)
    {
        var book = new EnemyEncyclopedia();
        if (string.IsNullOrEmpty(saved)) return book;

        foreach (string piece in saved.Split(','))
        {
            string[] parts = piece.Split(':');
            if (parts.Length != 2) continue;

            if (!int.TryParse(parts[0], out int id) || id == 0) continue;
            if (!int.TryParse(parts[1], out int count) || count <= 0) continue;

            book.defeats[id] = count;
        }
        return book;
    }
}
