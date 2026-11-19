using UnityEngine;

/// <summary>
/// 도감과 기록을 PlayerPrefs 에 넣고 꺼내는 곳.
///
/// 수첩들은 저장하는 법을 몰라야 Unity 없이 확인할 수 있으므로
/// (ItemEncyclopedia 주석 참고), PlayerPrefs 를 다루는 일은 전부 여기로 뺐다.
///
/// ── 적는 것과 디스크에 내리는 것은 따로다 ─────────────────────────
/// 예전에는 적을 때마다 PlayerPrefs.Save() 까지 불렀다. 그 호출은 디스크에
/// 다 쓸 때까지 돌아오지 않는데, 적을 쓰러뜨릴 때 · 공격이 빗나갈 때 · 돈을
/// 주울 때마다 불렸다. 모바일에서는 그때마다 프레임이 걸린다.
///
/// 지금은 적는 순간에는 <b>PlayerPrefs 에 값만 넣고</b>(메모리), 디스크에
/// 내리는 일은 <see cref="Flush"/> 가 한다. Flush 는 끊기기 쉬운 고비마다 불린다 —
/// 층을 넘을 때 · 판이 끝날 때 · 앱이 뒤로 가거나 꺼질 때(GameManager).
///
/// <b>죽어도 남는다는 요점은 그대로다.</b> 값은 죽기 전에 이미 PlayerPrefs 에
/// 들어가 있고, 판이 끝나는 자리(FinishRun)에서 곧바로 내린다. 잃을 수 있는
/// 것은 프로세스가 통째로 죽었을 때 그 층에서 적은 몫뿐이다.
/// </summary>
public static class EncyclopediaStorage
{
    private const string ItemKey = "Encyclopedia.Item";
    private const string EnemyKey = "Encyclopedia.Enemy";
    private const string PartnerKey = "Encyclopedia.Partner";
    private const string RecordKey = "Record.Run";

    /// <summary>적어 두고 아직 디스크에 내리지 않은 것이 있는가.</summary>
    private static bool dirty;

    private static void Put(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        dirty = true;
    }

    /// <summary>
    /// 적어 둔 것을 디스크에 내린다. 바뀐 것이 없으면 아무 일도 하지 않는다.
    /// </summary>
    public static void Flush()
    {
        if (!dirty) return;

        PlayerPrefs.Save();
        dirty = false;
    }

    public static ItemEncyclopedia LoadItems()
        => ItemEncyclopedia.Load(PlayerPrefs.GetString(ItemKey, string.Empty));

    public static void SaveItems(ItemEncyclopedia book) => Put(ItemKey, book.Save());

    public static EnemyEncyclopedia LoadEnemies()
        => EnemyEncyclopedia.Load(PlayerPrefs.GetString(EnemyKey, string.Empty));

    public static void SaveEnemies(EnemyEncyclopedia book) => Put(EnemyKey, book.Save());

    public static PartnerEncyclopedia LoadPartners()
        => PartnerEncyclopedia.Load(PlayerPrefs.GetString(PartnerKey, string.Empty));

    public static void SavePartners(PartnerEncyclopedia book) => Put(PartnerKey, book.Save());

    public static RecordBook LoadRecords()
        => RecordBook.Load(PlayerPrefs.GetString(RecordKey, string.Empty));

    public static void SaveRecords(RecordBook book) => Put(RecordKey, book.Save());
}
