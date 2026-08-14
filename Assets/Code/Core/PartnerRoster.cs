using System.Collections.Generic;
using UnityEngine;

/// <summary>지금 데리고 다니는 동료 하나. 층을 넘어가도 이 값들이 남는다.</summary>
public class PartnerMember
{
    /// <summary>partner 시트의 id.</summary>
    public int Id;

    /// <summary>남은 체력. 층을 넘어도 회복되지 않는다.</summary>
    public int Hp;

    /// <summary>최대 체력. 시트에서 왔지만 여기 들고 있다 — 표를 고쳐도 판 도중에는 안 흔들린다.</summary>
    public int MaxHp;

    /// <summary>동료 자신의 레벨. 플레이어와 따로 오른다.</summary>
    public int Level = 1;

    /// <summary>지금 레벨에서 모은 경험치.</summary>
    public int Exp;

    /// <summary>다음 레벨까지 필요한 총량.</summary>
    public int ExpToNextLevel = PlayerStats.InitialExpToNextLevel;

    /// <summary>다음 레벨까지 앞으로 얼마나 더 모아야 하는가.</summary>
    public int ExpRemaining => Mathf.Max(0, ExpToNextLevel - Exp);

    /// <summary>
    /// 이 턴이 지나야 특기를 다시 청할 수 있다.
    ///
    /// 남은 턴 수가 아니라 <b>언제부터인가</b>로 들고 있다. 남은 수로 두면 매 턴
    /// 누군가 줄여 주어야 하는데, 그 자리를 빠뜨리면 영영 식지 않는다.
    /// </summary>
    public int ActionReadyTurn;
}

/// <summary>
/// 데리고 다니는 동료들.
///
/// ── 왜 오브젝트가 아니라 목록인가 ─────────────────────────────────
/// 층을 넘을 때 씬을 다시 불러오므로 <b>동료 오브젝트도 플레이어처럼 사라진다.</b>
/// 그래서 "누가 몇 체력으로 따라오고 있는가" 만 여기 남기고, 새 층에서 그 값으로
/// 다시 만든다. 플레이어 수치(PlayerStats)를 static 으로 들고 있는 것과 같은 방식이다.
///
/// static 이므로 <b>판이 끝나면 반드시 비워야 한다</b>(GameManager.ResetRun).
/// 비우지 않으면 죽은 판의 동료가 다음 판까지 따라온다 — 에디터에서는 도메인이
/// 다시 로드돼 가려지고 빌드에서만 드러나는 종류다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class PartnerRoster
{
    private readonly List<PartnerMember> members = new List<PartnerMember>();

    public IReadOnlyList<PartnerMember> Members => members;

    public int Count => members.Count;
    public bool IsEmpty => members.Count == 0;

    /// <summary>이미 데리고 있는 동료인가. 같은 종류를 둘 받지 않기 위해 쓴다.</summary>
    public bool Contains(int id) => Find(id) != null;

    public PartnerMember Find(int id)
    {
        foreach (PartnerMember member in members)
        {
            if (member.Id == id) return member;
        }
        return null;
    }

    /// <summary>
    /// 동료를 들인다. 이미 있으면 아무 일도 하지 않고 false.
    /// 체력은 시트의 값으로 가득 채운 채로 시작한다.
    /// </summary>
    public bool TryAdd(int id, int maxHp)
    {
        if (id == 0 || maxHp <= 0) return false;
        if (Contains(id)) return false;

        members.Add(new PartnerMember { Id = id, Hp = maxHp, MaxHp = maxHp });
        return true;
    }

    /// <summary>쓰러졌거나 헤어졌다. 없던 번호면 false.</summary>
    public bool Remove(int id)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].Id != id) continue;

            members.RemoveAt(i);
            return true;
        }
        return false;
    }

    /// <summary>새 판을 시작할 때.</summary>
    public void Clear() => members.Clear();

    /// <summary>
    /// 경험치를 준다. 오른 레벨 수를 돌려준다(0 이면 그대로).
    ///
    /// <b>동료가 쓰러뜨린 적의 경험치는 동료만 받는다.</b> 플레이어가 함께 받으면
    /// 데리고 다니는 것이 무조건 이득이 되어 고를 이유가 사라진다. 대신 동료가
    /// 자라므로 "누구를 키울 것인가" 가 판단거리가 된다.
    ///
    /// 오르는 곡선은 플레이어와 같다(PlayerStats.AddExp). 둘을 다르게 두면
    /// 어느 쪽이 왜 빨리 크는지 설명할 길이 없다.
    /// </summary>
    public int AddExp(int id, int amount)
    {
        PartnerMember member = Find(id);
        if (member == null || amount <= 0) return 0;

        member.Exp += amount;

        int gained = 0;
        while (member.Exp >= member.ExpToNextLevel)
        {
            member.Exp -= member.ExpToNextLevel;
            member.ExpToNextLevel += member.ExpToNextLevel / 2;

            member.Level++;
            gained++;

            // 레벨이 오르면 체력도 늘고 그만큼 회복된다. 안 그러면 자라도
            // 한 번 깎인 체력이 판이 끝날 때까지 그대로 남는다.
            member.MaxHp += LevelUpHp;
            member.Hp += LevelUpHp;
        }

        return gained;
    }

    /// <summary>레벨이 오를 때마다 늘어나는 최대 체력.</summary>
    public const int LevelUpHp = 3;

    /// <summary>
    /// 모두를 조금씩 회복시킨다. 플레이어의 자연 회복과 같은 자리에서 불린다.
    ///
    /// 동료에게는 회복 수단이 이것뿐이다 — 가방이 없어 약을 못 먹고, 층을
    /// 넘어도 차지 않는다. 이것이 없으면 오래 데리고 다닐수록 반드시 쓰러진다.
    /// </summary>
    public void HealAll(int amount)
    {
        if (amount <= 0) return;

        foreach (PartnerMember member in members)
            member.Hp = Mathf.Min(member.Hp + amount, member.MaxHp);
    }

    /// <summary>회복시킨다. 최대를 넘지 않는다.</summary>
    public void Heal(int id, int amount)
    {
        PartnerMember member = Find(id);
        if (member == null || amount <= 0) return;

        member.Hp = Mathf.Min(member.Hp + amount, member.MaxHp);
    }

    /// <summary>특기를 썼다. 쿨타임이 끝나는 턴을 적어 둔다.</summary>
    public void StartActionCooldown(int id, int currentTurn)
    {
        PartnerMember member = Find(id);
        if (member == null) return;

        member.ActionReadyTurn = currentTurn + PartnerPlan.ActionCooldownTurns;
    }

    /// <summary>지금 특기를 청할 수 있는가.</summary>
    public bool IsActionReady(int id, int currentTurn)
    {
        PartnerMember member = Find(id);
        return member != null && currentTurn >= member.ActionReadyTurn;
    }

    /// <summary>
    /// 피해를 입힌다. 0 이하가 되면 목록에서 빠지고 true 를 돌려준다(쓰러졌다).
    ///
    /// 쓰러뜨리는 판단을 부르는 쪽에 맡기지 않는 이유는, 체력을 깎는 곳과
    /// 목록에서 빼는 곳이 갈리면 한쪽만 부르는 경로가 반드시 생기기 때문이다.
    /// </summary>
    public bool Damage(int id, int amount)
    {
        PartnerMember member = Find(id);
        if (member == null) return false;

        member.Hp -= amount;
        if (member.Hp > 0) return false;

        Remove(id);
        return true;
    }

    /// <summary>
    /// 계단을 내려갈 때, <b>따라오지 못한 동료를 목록에서 뺀다.</b>
    /// 돌려주는 것은 두고 온 동료들의 번호다(알림에 쓴다).
    ///
    /// ── 걸음 수가 아니라 '같은 방인가' 로 가른다 ──────────────────────
    /// 걸음 수로 자르면 경계가 화면에 보이지 않는다. 세 걸음인지 네 걸음인지는
    /// 플레이어가 눈으로 셀 수 없어서, 두고 온 이유를 알 수 없는 채로 동료를 잃는다.
    /// <b>방은 눈에 보인다.</b> 같은 방에 있으면 따라오고 통로나 다른 방에 있으면
    /// 두고 가므로, 계단 앞에서 "지금 내려가면 두고 간다" 가 한눈에 보인다.
    ///
    /// 통로에 선 동료는 어느 방에도 없으므로 두고 간다. 계단은 늘 방 안에 놓이니
    /// (DungeonPopulator.PlaceStairs) 계단 쪽에서는 이 경우가 없다.
    /// </summary>
    /// <param name="stairsRoom">계단이 있는 방의 안쪽 칸 영역.</param>
    /// <param name="partnerCells">동료 번호 → 지금 선 칸. 목록에 없으면 두고 간다.</param>
    public List<int> DropLeftBehind(
        RectInt stairsRoom, IReadOnlyDictionary<int, Vector3Int> partnerCells)
    {
        var left = new List<int>();

        // 방을 못 찾았다(넓이가 0). 이때는 아무도 두고 가지 않는다 —
        // 동료를 잃는 것은 되돌릴 수 없어서, 판단할 수 없을 때는 잃지 않는 쪽으로 둔다.
        if (stairsRoom.width <= 0 || stairsRoom.height <= 0) return left;

        for (int i = members.Count - 1; i >= 0; i--)
        {
            int id = members[i].Id;

            bool sameRoom = partnerCells != null
                            && partnerCells.TryGetValue(id, out Vector3Int cell)
                            && stairsRoom.Contains(new Vector2Int(cell.x, cell.y));

            if (sameRoom) continue;

            members.RemoveAt(i);
            left.Add(id);
        }

        // 뒤에서부터 지웠으므로 차례가 뒤집혀 있다. 알림이 목록 순서대로 나와야
        // "누가 남았는지" 를 읽는 사람이 헷갈리지 않는다.
        left.Reverse();
        return left;
    }
}
