using UnityEngine;

/// <summary>
/// 적 한 종류의 정의 애셋. 사람이 채우는 것은 <b>번호와 그림뿐</b>이다.
///
/// 이름·체력·공격력·경험치는 엑셀(Monster 시트)에서 읽는다.
/// 애셋에 사본을 두지 않는 이유는 TrapDescription 과 같다.
///
/// 지금까지 적의 수치는 DungeonPopulator 안에 SetStats(5, power, power - stage)
/// 로 박혀 있었다. 그래서 시트에 hp/atk/def/exp 를 적어 두어도 아무 데도
/// 쓰이지 않았고, 적을 한 마리 더 만들려면 코드를 고쳐야 했다.
/// </summary>
[CreateAssetMenu(fileName = "NewMonsterDescription", menuName = "Dungeon/Monster Description")]
public class MonsterDescription : ScriptableObject
{
    /// <summary>Monster 시트의 id 열.</summary>
    public int id;

    /// <summary>
    /// 이 적을 대표하는 그림 한 장. 도감과 인스펙터 미리보기가 쓴다.
    ///
    /// 움직이는 모습은 앞으로 8방향 모션(이동·공격·피격)과 정면 수면 모션이
    /// 따로 붙는다. 방향 인덱스는 MovingObject.directionMap 이 이미 정해 두었고
    /// (0 아래 · 1 위 · 2 왼 · 3 오른 · 4 왼아래 · 5 오아래 · 6 왼위 · 7 오른위),
    /// 플레이어가 그 규칙을 쓰고 있으므로 적도 같은 순서를 따른다.
    ///
    /// directionalSprites가 있으면 게임에서는 방향별 그림을 쓴다.
    /// 방향별 그림이 없는 적은 이 대표 그림을 실제 화면에도 쓴다.
    /// </summary>
    public Sprite sprite;

    /// <summary>아래·위·왼쪽·오른쪽·왼아래·오른아래·왼위·오른위 순서. 비어 있으면 대표 그림을 쓴다.</summary>
    public Sprite[] directionalSprites;

    /// <summary>시트에서 찾은 줄. 번호가 표에 없으면 null.</summary>
    public MonsterDefinition Definition => MonsterCatalog.Find(id);

    /// <summary>플레이어에게 보이는 이름. 표에서 못 찾으면 번호를 보여 준다.</summary>
    public string DisplayName => Definition?.Name ?? $"?{id}";

    public int Hp => Definition?.Hp ?? 0;
    public int Attack => Definition?.Attack ?? 0;
    public int Defense => Definition?.Defense ?? 0;
    public int Exp => Definition?.Exp ?? 0;

    /// <summary>치명타를 내는 적인가. 확률은 Player 가 정한다.</summary>
    public bool DealsCritical => Definition?.DealsCritical ?? false;

    /// <summary>아이템 드롭 확률. 표에 없으면 '중' 으로 본다.</summary>
    public ItemDropRate Rate => Definition?.Rate ?? ItemDropRate.Medium;

    /// <summary>갈래(종족). 도감이 보여 준다.</summary>
    public string Category => Definition?.Category ?? string.Empty;

    /// <summary>도감에 띄울 글.</summary>
    public string Description => Definition?.Description ?? string.Empty;
}
