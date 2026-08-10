using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장비가 주는 특수 능력. 코드가 아는 것만 시트에서 고를 수 있다.
///
/// basepower 는 공격력·방어력에 더해지는 <b>숫자</b>다. 그것만으로는 "치명타를
/// 무효화한다" 같은 것을 담을 수 없다 — 그건 데이터가 아니라 동작이기 때문이다.
/// 그래서 ConditionEffect · TrapAnimation 과 같은 방식으로, 코드가 아는 목록을
/// 시트가 골라 쓰게 했다.
/// </summary>
public enum EquipAbility
{
    /// <summary>특수 능력이 없다. 수치만 더해 준다.</summary>
    None,

    /// <summary>적을 쓰러뜨릴 때 경험치를 값만큼 더 받는다.</summary>
    BonusExp,

    /// <summary>받는 치명타를 평범한 공격으로 만든다. 값은 쓰지 않는다.</summary>
    NegateCritical,

    /// <summary>만복도가 줄어드는 간격이 값만큼 늘어난다.</summary>
    SlowHunger,
}

/// <summary>시트 한 줄. 아이템 하나의 정의.</summary>
public class ItemDefinition
{
    /// <summary>표 번호. 코드와 프리팹이 이 번호로 서로를 찾는다.</summary>
    public int Id;

    /// <summary>플레이어에게 보이는 이름("사과").</summary>
    public string Name;

    /// <summary>가방에서 '확인한다' 로 볼 설명.</summary>
    public string Description;

    public int SellPrice;

    /// <summary>먹는 것인지 쓰는 것인지. '쓴다' 자리의 말이 이걸로 갈린다.</summary>
    public ItemKind Kind;

    /// <summary>어느 자리에 끼우는가. None 이면 장비가 아니다.</summary>
    public EquipmentSlot Slot;

    /// <summary>강화하지 않은 상태의 기본 성능. 장비가 아니면 0.</summary>
    public int BasePower;

    /// <summary>장비가 주는 특수 능력.</summary>
    public EquipAbility Ability;

    /// <summary>그 능력이 쓰는 숫자. 능력에 따라 뜻이 다르다.</summary>
    public int AbilityValue;

    /// <summary>
    /// 합성할 때 <b>베이스로만</b> 쓸 수 있는가.
    ///
    /// 켜져 있으면 다른 장비의 능력을 받아 오는 쪽은 될 수 있지만, 자기 능력을
    /// 남에게 넘겨 주는 재료로는 쓸 수 없다.
    ///
    /// 합성 자체는 아직 없다. 표가 앞서 나가 있는 것이고, 만들 때 이 값을 보면 된다.
    /// </summary>
    public bool BaseOnly;

    /// <summary>
    /// 바닥에 나올 가중치. 클수록 자주 나온다. 0 이면 나오지 않는다.
    ///
    /// 시트에는 숫자가 아니라 등급(A+ · A · B- …)으로 적는다. 그 편이 표를
    /// 채울 때 서로 견주기 쉽다 — 숫자로 적으면 "3 이 흔한 건가 드문 건가" 를
    /// 다른 줄과 대 봐야 알 수 있다.
    ///
    /// 예전에는 던전 생성 쪽 인스펙터에 프리팹과 <b>번호로 짝지은</b> 배열로
    /// 있었다. 프리팹을 늘리고 가중치를 안 늘리면 새 아이템이 영영 안 나왔는데,
    /// 실제로 장비 3종을 더했을 때 그렇게 됐다. 값이 아이템에 붙어 다니면
    /// 짝을 맞출 일이 없다.
    /// </summary>
    public float DropRate;

    /// <summary>실제로 장비로 쓸 수 있는가.</summary>
    public bool IsEquipment => Slot != EquipmentSlot.None;
}

/// <summary>
/// 엑셀에서 읽어 온 아이템 줄을 게임이 쓰는 모습으로 옮겨 담는다.
///
/// ConditionSheet · TrapSheet · MonsterSheet 과 같은 규칙을 따른다.
///
/// ── 열 이름이 시트를 따라간다 ──────────────────────────────────────
/// ItemEntity 의 필드 이름(sellprice · equipslot …)은 엑셀 열 이름 그대로다.
/// 임포터가 글자까지 같은 이름만 채우기 때문이다. 그 지저분함은 여기까지만이고,
/// ItemDefinition 부터는 제대로 된 이름을 쓴다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class ItemSheet
{
    public static List<ItemDefinition> FromEntities(
        IEnumerable<ItemEntity> rows, List<string> problems = null)
    {
        var result = new List<ItemDefinition>();
        if (rows == null) return result;

        foreach (ItemEntity row in rows)
        {
            if (row == null) continue;

            // 번호가 없는 줄은 프리팹이 찾을 길이 없으므로 버린다.
            if (row.id == 0) continue;

            string name = Clean(row.name);
            if (name.Length == 0)
                problems?.Add($"{row.id} 번의 이름(name)이 비어 있습니다.");

            EquipmentSlot slot = ParseSlot(row.equipslot, row.id, problems);

            // 장비가 아닌데 성능이 적혀 있으면 둘 중 하나가 잘못된 것이다.
            // 조용히 무시하면 표를 고친 사람은 값이 먹은 줄 안다.
            if (slot == EquipmentSlot.None && row.basepower != 0)
                problems?.Add(
                    $"{row.id} 번은 장비가 아닌데 basepower 가 {row.basepower} 입니다. 0 으로 봅니다.");

            EquipAbility ability = ParseAbility(row.ability, row.id, problems);

            // 장비가 아닌데 능력이 적혀 있으면 둘 중 하나가 잘못된 것이다.
            if (slot == EquipmentSlot.None && ability != EquipAbility.None)
                problems?.Add($"{row.id} 번은 장비가 아닌데 능력 '{Clean(row.ability)}' 가 적혀 있습니다.");

            // 숫자를 쓰는 능력인데 0 이면 아무 일도 일어나지 않는다.
            if (NeedsValue(ability) && row.abilityvalue <= 0)
                problems?.Add($"{row.id} 번의 능력 '{Clean(row.ability)}' 에 abilityvalue 가 없습니다.");

            result.Add(new ItemDefinition
            {
                Id = row.id,
                Name = name,
                Description = Clean(row.explanation),
                SellPrice = row.sellprice,
                Kind = ParseKind(row.itemtype, row.id, problems),
                Slot = slot,
                BasePower = slot == EquipmentSlot.None ? 0 : row.basepower,
                Ability = slot == EquipmentSlot.None ? EquipAbility.None : ability,
                AbilityValue = row.abilityvalue,
                BaseOnly = row.baseonly != 0,
                DropRate = ParseDropRate(row.droprate, row.id, problems),
            });
        }

        return result;
    }

    /// <summary>칸 끝에 남은 탭·공백·줄바꿈을 털어 낸다.</summary>
    private static readonly char[] Junk =
        { '﻿', ' ', '\t', '\r', '\n' };

    private static string Clean(string cell)
        => cell == null ? string.Empty : cell.Trim(Junk);

    /// <summary>
    /// 갈래. 엑셀은 영어로 적혀 있어 대소문자를 가리지 않는다.
    /// 표를 채우는 사람이 Food 라고 써도 food 라고 써도 같게 읽힌다.
    /// </summary>
    public static ItemKind ParseKind(string cell, int id, List<string> problems = null)
    {
        switch (Clean(cell).ToLowerInvariant())
        {
            case "food": return ItemKind.Food;
            case "expendables": return ItemKind.Expendables;
            case "equipment": return ItemKind.Equipment;
            case "special": return ItemKind.Special;
            default:
                problems?.Add($"{id} 번의 갈래 '{Clean(cell)}' 를 모릅니다. 'expendables' 로 봅니다.");
                return ItemKind.Expendables;
        }
    }

    /// <summary>
    /// 드랍 등급을 가중치로 바꾼다. 알아볼 수 없으면 0(안 나옴)이고 알려 준다.
    ///
    /// ── 사다리 ────────────────────────────────────────────────────
    /// 글자 하나 내려갈 때마다 3 씩 줄고, `+` 는 1 올리고 `-` 는 1 내린다.
    ///
    ///   A+ 10 · A  9 · A- 8
    ///   B+  7 · B  6 · B-  5
    ///   C+  4 · C  3 · C-  2
    ///   D+  3 · D  2 · D-  1        (아래로는 1 에서 멈춘다)
    ///
    /// 이렇게 두면 <b>B- 가 C+ 보다 반드시 흔하다.</b> 등급 사이가 뒤집히면
    /// 표를 채우는 사람이 등급으로 견줄 수가 없어진다.
    /// </summary>
    public static float ParseDropRate(string cell, int id, List<string> problems = null)
    {
        string grade = Clean(cell).ToUpperInvariant();

        if (grade.Length == 0)
        {
            problems?.Add($"{id} 번의 droprate 가 비어 있습니다. 바닥에 나오지 않습니다.");
            return 0f;
        }

        char letter = grade[0];
        if (letter < 'A' || letter > 'Z')
        {
            problems?.Add($"{id} 번의 droprate '{grade}' 를 모릅니다. 바닥에 나오지 않습니다.");
            return 0f;
        }

        int sign = 0;
        if (grade.Length > 1)
        {
            if (grade[1] == '+') sign = 1;
            else if (grade[1] == '-') sign = -1;
            else
            {
                problems?.Add($"{id} 번의 droprate '{grade}' 뒤가 이상합니다. '+' 나 '-' 만 붙습니다.");
                return 0f;
            }
        }

        return Mathf.Max(1, 9 - 3 * (letter - 'A') + sign);
    }

    /// <summary>값이 있어야 뜻이 서는 능력인가.</summary>
    public static bool NeedsValue(EquipAbility ability)
        => ability == EquipAbility.BonusExp || ability == EquipAbility.SlowHunger;

    public static EquipAbility ParseAbility(string cell, int id, List<string> problems = null)
    {
        switch (Clean(cell))
        {
            case "":
            case "없음": return EquipAbility.None;
            case "경험치증가": return EquipAbility.BonusExp;
            case "치명타무효": return EquipAbility.NegateCritical;
            case "만복도절약": return EquipAbility.SlowHunger;
            default:
                problems?.Add($"{id} 번의 능력 '{Clean(cell)}' 를 모릅니다. '없음' 으로 봅니다.");
                return EquipAbility.None;
        }
    }

    public static EquipmentSlot ParseSlot(string cell, int id, List<string> problems = null)
    {
        switch (Clean(cell).ToLowerInvariant())
        {
            case "":
            case "none": return EquipmentSlot.None;
            case "weapon": return EquipmentSlot.Weapon;
            case "armor": return EquipmentSlot.Armor;
            // 복수형도 받는다. 표를 채우는 사람이 "accessories" 라고 쓰는 편이
            // 자연스러운데, 그것 하나 때문에 장비가 아닌 것으로 읽히면 곤란하다.
            case "accessory":
            case "accessories": return EquipmentSlot.Accessory;
            default:
                problems?.Add($"{id} 번의 장비 자리 '{Clean(cell)}' 를 모릅니다. 장비가 아닌 것으로 봅니다.");
                return EquipmentSlot.None;
        }
    }
}
