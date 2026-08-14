using System.Collections.Generic;

/// <summary>
/// 만나 본 동료와 그들의 호감도를 적어 두는 수첩.
///
/// ── 호감도는 아직 오르지 않는다 ───────────────────────────────────
/// <b>무엇으로 오를지는 아직 정하지 않았다.</b> 동행한 턴 수인지, 말을 건 횟수인지,
/// 함께 잡은 적 수인지에 따라 눈금 크기가 통째로 달라져서(턴이면 수백, 말 걸기면
/// 한 자릿수) 문턱도 함께 정해야 하기 때문이다. 그 결정을 미룬 자리다.
///
/// 그래서 <see cref="AddAffinity"/> 를 부르는 곳이 지금은 하나도 없다. 나중에
/// 호감도를 만들 때 <b>그 한 곳만</b> 이으면 도감·문턱·저장이 저절로 따라온다.
/// 적이 아직 변장하지 않는데도 Enemy.IsDisguised 를 미리 둔 것과 같은 방식이다.
///
/// 목록에 이름이 뜨는 조건은 호감도가 아니라 <b>동행해 본 적이 있는가</b>다
/// (RecordMet). 호감도로 걸면 지금은 아무도 뜨지 않아 도감이 통째로 빈 화면이 된다.
///
/// 판이 아니라 모험 전체에 걸쳐 남는다. 저장하는 법은 모른다 —
/// 그 일은 EncyclopediaStorage 가 한다. Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class PartnerEncyclopedia
{
    /// <summary>동행해 본 적이 있는 동료의 번호 → 호감도.</summary>
    private readonly Dictionary<int, int> met = new Dictionary<int, int>();

    /// <summary>한 번이라도 동행해 본 적이 있는가. 도감에 이름이 뜨는 조건이다.</summary>
    public bool IsDiscovered(int partnerId) => met.ContainsKey(partnerId);

    /// <summary>이 동료의 호감도. 만난 적이 없으면 0.</summary>
    public int AffinityOf(int partnerId)
        => met.TryGetValue(partnerId, out int affinity) ? affinity : 0;

    /// <summary>
    /// 동행하기 시작했다고 적는다. 처음 만난 것이면 true.
    ///
    /// 이미 아는 동료면 호감도를 <b>건드리지 않는다.</b> 다시 동행했다고 지난 판에
    /// 쌓아 둔 것이 0 으로 돌아가면 모아 온 것이 사라진다.
    ///
    /// 번호 0 은 정의가 연결되지 않은 동료라 받지 않는다(MarkBook 과 같은 규칙).
    /// </summary>
    public bool RecordMet(int partnerId)
    {
        if (partnerId == 0 || met.ContainsKey(partnerId)) return false;

        met[partnerId] = 0;
        return true;
    }

    /// <summary>
    /// 호감도를 올린다. 올린 뒤의 값을 돌려준다.
    ///
    /// <b>아직 부르는 곳이 없다</b>(이 클래스 주석 참고). 호감도가 무엇으로 오를지
    /// 정해지면 그것을 세는 곳에서 이 하나를 부르면 된다.
    ///
    /// 만난 적 없는 동료에게는 붙지 않는다 — 동행한 적도 없는데 호감도만 쌓이면
    /// 목록에는 물음표인데 값은 차 있는 어긋난 상태가 된다.
    /// </summary>
    public int AddAffinity(int partnerId, int amount)
    {
        if (partnerId == 0 || amount <= 0 || !met.ContainsKey(partnerId))
            return AffinityOf(partnerId);

        met[partnerId] += amount;
        return met[partnerId];
    }

    /// <summary>저장할 글자. "번호:호감도" 를 쉼표로 늘어놓는다.</summary>
    public string Save()
    {
        var pieces = new List<string>();
        foreach (KeyValuePair<int, int> pair in met)
            pieces.Add($"{pair.Key}:{pair.Value}");

        return string.Join(",", pieces);
    }

    /// <summary>
    /// 저장된 글자에서 되살린다. 못 읽는 조각은 조용히 건너뛴다.
    ///
    /// 호감도 0 은 <b>멀쩡한 값</b>이다(만났지만 아직 안 쌓인 상태). 적 도감이
    /// 횟수 0 을 버리는 것과 다른 점이라, 그쪽 규칙을 그대로 옮겨 오면 안 된다.
    /// </summary>
    public static PartnerEncyclopedia Load(string saved)
    {
        var book = new PartnerEncyclopedia();
        if (string.IsNullOrEmpty(saved)) return book;

        foreach (string piece in saved.Split(','))
        {
            string[] parts = piece.Split(':');
            if (parts.Length != 2) continue;

            if (!int.TryParse(parts[0], out int id) || id == 0) continue;
            if (!int.TryParse(parts[1], out int affinity) || affinity < 0) continue;

            book.met[id] = affinity;
        }
        return book;
    }
}
