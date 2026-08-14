using UnityEngine;

/// <summary>
/// 동료 한 종류의 정의 애셋. 사람이 채우는 것은 <b>번호와 그림뿐</b>이다.
///
/// 이름·수치·특성·대사는 여기 적지 않고 엑셀(partner · trait · partnerline 시트)에서
/// 읽는다. 애셋에 사본을 두면 시트를 고쳤을 때 조용히 어긋난다 —
/// MonsterDescription · TrapDescription 과 같은 이유다(10절).
/// </summary>
[CreateAssetMenu(fileName = "NewPartnerDescription", menuName = "Partner/Description")]
public class PartnerDescription : ScriptableObject
{
    /// <summary>partner 시트의 id 열. 이 번호로 시트에서 나머지를 찾아온다.</summary>
    public int id;

    /// <summary>화면에 그릴 그림.</summary>
    public Sprite sprite;

    /// <summary>시트에서 찾은 줄. 번호가 표에 없으면 null.</summary>
    public PartnerDefinition Definition => PartnerCatalog.Find(id);

    /// <summary>
    /// 화면에 보이는 이름. 표에서 못 찾으면 번호를 그대로 보여 준다 —
    /// 빈칸으로 두면 무엇이 잘못됐는지 알 수 없다.
    /// </summary>
    public string partnerName => Definition?.Name ?? $"?{id}";

    public int Hp => Definition?.Hp ?? 0;
    public int Attack => Definition?.Attack ?? 0;
    public int Defense => Definition?.Defense ?? 0;

    /// <summary>이 동료가 들고 다닐 수 있는 가방 칸 수. 가방은 아직 없다.</summary>
    public int BagSize => Definition?.BagSize ?? 0;

    /// <summary>가진 특성 번호들. 동작은 아직 하나도 붙어 있지 않다.</summary>
    public System.Collections.Generic.IReadOnlyList<int> TraitIds
        => (System.Collections.Generic.IReadOnlyList<int>)Definition?.TraitIds
           ?? System.Array.Empty<int>();

    public string Gender => Definition?.Gender ?? string.Empty;
    public string Category => Definition?.Category ?? string.Empty;
    public string description => Definition?.Description ?? string.Empty;

    /// <summary>대사 묶음. 아직 대사를 안 적은 동료면 null 이고, 그때는 말하지 않는다.</summary>
    public PartnerLineSet Lines => PartnerLineCatalog.Find(id);
}
