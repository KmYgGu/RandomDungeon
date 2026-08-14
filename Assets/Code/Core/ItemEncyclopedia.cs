using System.Collections.Generic;

/// <summary>
/// 지금까지 만나 본 아이템 종류를 적어 두는 수첩.
///
/// MarkBook(표식)과 달리 판이 아니라 <b>모험 전체</b>에 걸쳐 남는다. 도감은
/// 원래 그런 것이라, 새 판을 시작해도 지워지지 않는다(GameManager.ResetRun 이
/// 이 값은 건드리지 않는다).
///
/// 그래서 이 값은 판 상태가 아니라 기기에 남는 값(PlayerPrefs)으로 다룬다.
/// 이 클래스 자신은 그 사실을 모른다 — 저장하고 불러올 글자만 주고받으면
/// 되므로, Unity 없이 그대로 확인할 수 있다. 실제로 PlayerPrefs 를 읽고 쓰는
/// 일은 EncyclopediaStorage 가 한다.
/// </summary>
public class ItemEncyclopedia
{
    private readonly HashSet<int> discovered = new HashSet<int>();

    /// <summary>이 종류를 만나 본 적이 있는가.</summary>
    public bool IsDiscovered(int itemId) => discovered.Contains(itemId);

    /// <summary>
    /// 만나 봤다고 적는다. 처음 아는 것이면 true, 이미 알던 것이면 false.
    ///
    /// 번호가 0인 것은 정의가 연결되지 않은 아이템이라 받지 않는다 —
    /// 받아 두면 서로 다른 미완성 아이템들이 0번 하나를 나눠 갖게 된다.
    /// </summary>
    public bool Record(int itemId)
    {
        if (itemId == 0) return false;
        return discovered.Add(itemId);
    }

    /// <summary>저장할 글자. 번호를 쉼표로 늘어놓는다.</summary>
    public string Save() => string.Join(",", discovered);

    /// <summary>저장된 글자에서 되살린다. 못 읽는 조각은 조용히 건너뛴다.</summary>
    public static ItemEncyclopedia Load(string saved)
    {
        var book = new ItemEncyclopedia();
        if (string.IsNullOrEmpty(saved)) return book;

        foreach (string piece in saved.Split(','))
        {
            if (int.TryParse(piece, out int id)) book.discovered.Add(id);
        }
        return book;
    }
}
