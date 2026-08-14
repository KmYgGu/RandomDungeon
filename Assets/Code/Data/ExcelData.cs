using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 엑셀에서 만들어지는 데이터 덩어리.
///
/// Assets/Resources/ExcelData.xlsx 를 저장하면 ExcelImporter 가 이 클래스로 옮겨 담아
/// ExcelData.asset 을 만들어 준다. 게임은 그 애셋만 읽으므로 실행 중에 엑셀을 뜯지 않는다.
///
/// ── 이름 규칙(라이브러리가 정한 것) ───────────────────────────────
///   클래스 이름 = 엑셀 파일 이름   (ExcelData.xlsx  -> class ExcelData)
///   필드 이름   = 시트 이름        (시트 Conditions -> List Conditions)
///   필드 이름   = 열 이름          (열 explanation  -> string explanation)
/// 셋 중 하나라도 어긋나면 조용히 비어 있게 되므로 함부로 바꾸지 않는다.
///
/// ── 시트를 늘릴 때 ────────────────────────────────────────────────
/// 임포터는 시트를 훑지 않고 **이 클래스의 필드를** 훑는다(ExcelImporter.ImportExcel).
/// 그래서 엑셀에 시트만 더하면 아무 일도 일어나지 않는다. 경고조차 없다.
/// 실제로 Trap · Monster · item 세 시트가 여기 필드가 없다는 이유만으로
/// 한동안 통째로 버려지고 있었다. 시트를 늘리면 여기 필드도 반드시 함께 늘린다.
/// </summary>
// 저장할 때마다 "Imported N sheets" 로그가 뜬다. N 이 시트 수(지금 4)보다 적으면
// 필드 이름과 시트 이름이 어긋난 것이다 — 임포터는 그 경우 아무 말도 하지 않으므로,
// 이 로그가 조용한 실패를 알아채는 유일한 방법이다.
[ExcelAsset(LogOnImport = true)]
public class ExcelData : ScriptableObject
{
    /// <summary>상태이상 표(시트 "Conditions").</summary>
    public List<ConditionEntity> Conditions;

    /// <summary>함정 표(시트 "Trap").</summary>
    public List<TrapEntity> Trap;

    /// <summary>적 표(시트 "Monster").</summary>
    public List<MonsterEntity> Monster;

    /// <summary>
    /// 아이템 표(시트 "item").
    ///
    /// 이 필드만 소문자인 것은 시트 이름이 소문자여서다. 시트 이름을 Items 로
    /// 바꾸면 여기도 함께 바꿀 것 — 지금은 시트 쪽을 따라간다.
    /// </summary>
    public List<ItemEntity> item;

    /// <summary>동료 표(시트 "partner").</summary>
    public List<PartnerEntity> partner;

    /// <summary>특성 표(시트 "trait").</summary>
    public List<TraitEntity> trait;

    /// <summary>동료 대사 표(시트 "partnerline").</summary>
    public List<PartnerLineEntity> partnerline;
}

/// <summary>
/// 상태이상 표의 한 줄. 필드 이름이 곧 엑셀의 열 이름이다.
///
/// 엑셀에서 열 이름을 바꾸면 여기도 함께 바꿔야 한다. 이름이 어긋난 열은
/// 조용히 비어 버리므로, 열을 늘릴 때는 반드시 짝을 맞춘다.
/// </summary>
[Serializable]
public class ConditionEntity
{
    /// <summary>표 번호. 10000번대가 이로움, 20000번대가 해로움.</summary>
    public int id;

    /// <summary>
    /// 코드가 가리키는 이름(hungry, poison …).
    ///
    /// 게임 코드는 번호가 아니라 이쪽을 쓴다. 번호는 표를 정리하는 사람의 것이고,
    /// 코드에 20002 라고 적혀 있으면 그게 무엇인지 알 길이 없기 때문이다.
    /// </summary>
    public string id2;

    public string name;
    public string explanation;
    public string type;
    public string effect;
    public int value;
    public int turn;
}

/// <summary>함정 표의 한 줄.</summary>
[Serializable]
public class TrapEntity
{
    /// <summary>표 번호.</summary>
    public int id;

    /// <summary>코드가 가리키는 이름(dart, gas …).</summary>
    public string id2;

    public string name;

    /// <summary>
    /// 작동했을 때 메시지 창에 그대로 띄우는 문구("어디선가 독침이 날라왔다!").
    ///
    /// 상태이상 표의 같은 이름 열과 쓰임이 같다 — 둘 다 플레이어가 읽는 글이다.
    /// </summary>
    public string explanation;

    /// <summary>작동할 때 캐릭터가 취하는 몸짓. TrapSheet 이 뜻을 정한다.</summary>
    public string charani;

    /// <summary>
    /// 무슨 일이 일어나야 하는지 적어 둔 설정("힘이 감소").
    /// 사람이 읽는 메모라서 코드가 이 글자를 보고 갈래를 나누지는 않는다.
    /// </summary>
    public string note;
}

/// <summary>적 표의 한 줄.</summary>
[Serializable]
public class MonsterEntity
{
    public int id;
    public string name;
    public int hp;
    public int atk;
    public int def;
    public int exp;

    /// <summary>아직 뜻이 정해지지 않은 열(값 예: "중").</summary>
    public string rate;

    public string category;

    /// <summary>도감에 띄울 글.</summary>
    public string explanation;

    /// <summary>
    /// 이 적이 치명타를 내는가. <b>1 이면 낸다, 0 이면 안 낸다.</b>
    ///
    /// 확률이 아니라 켜고 끄는 값이다. 확률은 코드가 한 곳에서 정한다
    /// (Player.IncomingCriticalPercent) — 표에 적마다 숫자를 적게 하면 균형을
    /// 다시 잡을 때 표의 모든 줄을 만져야 하고, 그러다 어긋난 줄이 생긴다.
    ///
    /// 모든 적이 치명타를 내면 맞는 쪽에서는 그냥 피해 폭이 넓은 것으로만 보인다.
    /// 내는 적이 정해져 있어야 "저 녀석은 조심해야 한다" 가 성립한다.
    /// </summary>
    public int critical;

    /// <summary>사람이 읽는 메모.</summary>
    public string note;
}

/// <summary>아이템 표의 한 줄.</summary>
[Serializable]
public class ItemEntity
{
    public int id;
    public string name;

    /// <summary>설명문. 시트의 오타(descripttion)를 고치면서 이름을 맞췄다.</summary>
    public string explanation;

    public int sellprice;

    /// <summary>food / expendables …</summary>
    public string itemtype;

    /// <summary>none 이면 장비가 아니다.</summary>
    public string equipslot;

    /// <summary>장비의 기본 성능. 장비가 아니면 0.</summary>
    public int basepower;

    /// <summary>
    /// 합성할 때 베이스로만 쓸 수 있는가(1 이면 그렇다).
    /// 재료로는 쓸 수 없다는 뜻이다.
    /// </summary>
    public int baseonly;

    /// <summary>장비의 특수 능력. 코드가 아는 이름만 쓸 수 있다(EquipAbility).</summary>
    public string ability;

    /// <summary>특수 능력이 쓰는 숫자.</summary>
    public int abilityvalue;

    /// <summary>바닥에 나올 등급(A+ · A · B- …). 뒤 글자일수록 드물다.</summary>
    public string droprate;
}

/// <summary>동료 표의 한 줄.</summary>
[Serializable]
public class PartnerEntity
{
    public int id;
    public string name;
    public int hp;
    public int atk;
    public int def;

    /// <summary>이 동료가 들고 다닐 수 있는 가방 칸 수.</summary>
    public int bag;

    /// <summary>
    /// 특성 번호. trait 시트의 id 를 가리킨다. 0(빈칸)이면 그 자리는 없는 것이다.
    ///
    /// 여섯 칸을 열로 펼쳐 둔 것은 엑셀에서 채우기 쉬워서다. 코드는
    /// PartnerDefinition.TraitIds 하나의 목록으로 받아 쓴다.
    /// <b>열을 늘리면 여기와 PartnerSheet.CollectTraits 를 함께 늘려야 한다</b> —
    /// 실제로 skill6 을 더했을 때 여기가 없어서 그 칸이 통째로 버려지고 있었다.
    /// </summary>
    public int skill1;
    public int skill2;
    public int skill3;
    public int skill4;
    public int skill5;
    public int skill6;

    /// <summary>성별("남성" · "여성").</summary>
    public string gender;

    /// <summary>종족("인간" · "엘프" · "언데드" · "요정").</summary>
    public string category;

    /// <summary>도감·소개에 띄울 글.</summary>
    public string explanation;

    /// <summary>
    /// 사람이 읽는 메모. 지금은 앞으로 만들 고유 능력이 적혀 있다.
    /// 다른 표와 마찬가지로 <b>코드가 이 글자를 보고 갈래를 나누지 않는다.</b>
    /// 고유 능력을 만들 때는 코드가 아는 목록을 고르는 열을 따로 둔다.
    /// </summary>
    public string note;
}

/// <summary>특성 표의 한 줄.</summary>
[Serializable]
public class TraitEntity
{
    /// <summary>특성 번호(101~). partner 시트의 skill 열이 이 번호를 가리킨다.</summary>
    public int id;

    public string name;

    /// <summary>무엇을 하는 특성인지 사람에게 보여 주는 글.</summary>
    public string explanation;
}

/// <summary>
/// 동료 대사 표의 한 줄.
///
/// 체력 구간마다 대사가 하나씩이다. 열 이름의 숫자가 그 구간의 <b>아래 끝</b>이다
/// (PartnerLineSheet.LineForPercent 가 뜻을 정한다).
/// </summary>
[Serializable]
public class PartnerLineEntity
{
    /// <summary>partner 시트의 id 와 같은 번호.</summary>
    public int id;

    /// <summary>표를 눈으로 맞춰 보기 위한 이름. 코드는 번호로만 잇는다.</summary>
    public string name;

    /// <summary>처음 만났을 때.</summary>
    public string meet;

    public string hp100;
    public string hp80;
    public string hp60;
    public string hp30;
    public string hp10;
    public string hp1;

    /// <summary>고유 행동을 시작할 때.</summary>
    public string action;

    /// <summary>고유 행동의 결과별 대사.</summary>
    public string actioncase1;
    public string actioncase2;
    public string actioncase3;
}
