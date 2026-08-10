using UnityEngine;

/// <summary>
/// 함정 한 종류의 정의 애셋. 사람이 채우는 것은 <b>번호와 그림뿐</b>이다.
///
/// 이름·문구·몸짓은 여기에 적지 않고 엑셀(Trap 시트)에서 읽는다. 애셋에 값을
/// 복사해 두면 시트를 고쳤을 때 조용히 어긋난다 — 실제로 ExcelData.asset 이
/// 코드보다 낡아 상태이상이 통째로 사라진 적이 있다. 원본이 한 곳이면
/// 어긋날 수가 없다.
///
/// 그래서 아래 값들은 필드가 아니라 프로퍼티다. 인스펙터에 보이지 않으므로
/// TrapDescriptionEditor 가 읽기 전용으로 그려 준다.
/// </summary>
[CreateAssetMenu(fileName = "NewTrapDescription", menuName = "Dungeon/Trap Description")]
public class TrapDescription : ScriptableObject
{
    /// <summary>Trap 시트의 id 열. 이 번호로 시트에서 나머지를 찾아온다.</summary>
    public int id;

    /// <summary>바닥에 놓일 때의 그림.</summary>
    public Sprite sprite;

    /// <summary>
    /// 밟았을 때 작동할 확률.
    ///
    /// 시트에 확률 열이 없어서 아직 여기에 둔다. Trap 시트에 chance 열이
    /// 생기면 이 필드를 지우고 프로퍼티로 바꾼다.
    /// </summary>
    [Range(0f, 1f)]
    public float triggerChance = 0.5f;

    /// <summary>시트에서 찾은 줄. 번호가 표에 없으면 null.</summary>
    public TrapDefinition Definition => TrapCatalog.Find(id);

    /// <summary>
    /// 코드가 쓰는 이름(dart, gas …). 효과를 만들 때 이것으로 갈래를 나눈다.
    /// 표에 없으면 빈 문자열이라 어떤 갈래에도 걸리지 않는다.
    /// </summary>
    public string TrapId => Definition?.Id ?? string.Empty;

    /// <summary>
    /// 플레이어에게 보이는 이름("독침").
    ///
    /// 표에서 못 찾으면 번호를 그대로 보여 준다. 빈칸으로 두면 "이(가)
    /// 작동했다!" 처럼 문장만 남아 무엇이 잘못됐는지 알 수 없다.
    /// </summary>
    public string DisplayName => Definition?.Name ?? $"?{id}";

    /// <summary>작동했을 때 띄우는 문구. 없으면 빈 문자열(그 줄을 건너뛴다).</summary>
    public string Message => Definition?.Message ?? string.Empty;

    /// <summary>작동할 때 캐릭터가 취하는 몸짓.</summary>
    public TrapAnimation Animation => Definition?.Animation ?? TrapAnimation.None;
}
