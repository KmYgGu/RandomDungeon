using UnityEngine;

/// <summary>
/// 도감과 기록을 PlayerPrefs 에 넣고 꺼내는 곳.
///
/// 수첩들은 저장하는 법을 몰라야 Unity 없이 확인할 수 있으므로
/// (ItemEncyclopedia 주석 참고), PlayerPrefs 를 다루는 일은 전부 여기로 뺐다.
/// </summary>
public static class EncyclopediaStorage
{
    private const string ItemKey = "Encyclopedia.Item";
    private const string EnemyKey = "Encyclopedia.Enemy";
    private const string PartnerKey = "Encyclopedia.Partner";

    public static ItemEncyclopedia LoadItems()
        => ItemEncyclopedia.Load(PlayerPrefs.GetString(ItemKey, string.Empty));

    /// <summary>바로 디스크에 씌운다. 발견은 자주 있는 일이 아니라 그때마다 저장해도 된다.</summary>
    public static void SaveItems(ItemEncyclopedia book)
    {
        PlayerPrefs.SetString(ItemKey, book.Save());
        PlayerPrefs.Save();
    }

    public static EnemyEncyclopedia LoadEnemies()
        => EnemyEncyclopedia.Load(PlayerPrefs.GetString(EnemyKey, string.Empty));

    /// <summary>
    /// 적을 쓰러뜨릴 때마다 씌운다.
    ///
    /// 아이템보다 잦은 일이지만 그래도 그때마다 저장한다. 한 층에 몇십 번이고,
    /// 판이 끝나는 순간(죽음)에 몰아서 저장하려 들면 <b>죽은 판의 기록이 통째로
    /// 날아가는</b> 길이 생긴다 — 도감은 죽어도 남는 것이 요점이라 그 구멍이 치명적이다.
    /// </summary>
    public static void SaveEnemies(EnemyEncyclopedia book)
    {
        PlayerPrefs.SetString(EnemyKey, book.Save());
        PlayerPrefs.Save();
    }

    public static PartnerEncyclopedia LoadPartners()
        => PartnerEncyclopedia.Load(PlayerPrefs.GetString(PartnerKey, string.Empty));

    public static void SavePartners(PartnerEncyclopedia book)
    {
        PlayerPrefs.SetString(PartnerKey, book.Save());
        PlayerPrefs.Save();
    }

    private const string RecordKey = "Record.Run";

    public static RecordBook LoadRecords()
        => RecordBook.Load(PlayerPrefs.GetString(RecordKey, string.Empty));

    /// <summary>
    /// 모험이 끝나는 순간에만 불린다. 그 뒤 곧바로 씬이 다시 불러와지므로
    /// <b>여기서 저장하지 않으면 그 판의 기록이 통째로 사라진다.</b>
    /// </summary>
    public static void SaveRecords(RecordBook book)
    {
        PlayerPrefs.SetString(RecordKey, book.Save());
        PlayerPrefs.Save();
    }
}
