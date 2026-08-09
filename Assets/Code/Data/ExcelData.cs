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
/// 둘 중 하나라도 어긋나면 조용히 비어 있게 되므로 함부로 바꾸지 않는다.
/// </summary>
[ExcelAsset]
public class ExcelData : ScriptableObject
{
    /// <summary>상태이상 표(시트 이름 "Conditions").</summary>
    public List<ConditionEntity> Conditions;
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
    public string id;
    public string name;
    public string explanation;
    public string type;
    public string effect;
    public int value;
    public int turn;
}
