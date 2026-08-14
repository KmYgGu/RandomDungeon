using System;
using System.Collections.Generic;

/// <summary>
/// 시트에서 옮겨 담은 줄들을 번호나 이름으로 곧바로 찾을 수 있게 색인한다.
///
/// ── 왜 필요해졌는가 ──────────────────────────────────────────────────
/// 카탈로그마다 foreach 로 목록을 훑고 있었다. 표가 일곱 줄일 때는 싼 일이지만,
/// <see cref="ItemDescription"/> 의 이름·자리·성능이 전부 프로퍼티라 <b>한 줄을
/// 그리는 데만도 표를 여러 번 훑고</b>, 가방 목록이 그것을 아이템 수만큼
/// 되풀이한다. 표가 자라면 그 곱이 함께 자란다.
///
/// ── 사본이 아니다 ────────────────────────────────────────────────────
/// 값을 복사해 두는 것이 아니라 <b>줄 자체를 가리키는 색인</b>이다. 그래서
/// 10절의 "사본을 두지 않는다" 와 어긋나지 않는다 — 표를 고치면 카탈로그가
/// 목록과 색인을 함께 버리고 다시 만든다(<c>Reload</c>).
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class SheetIndex
{
    /// <summary>
    /// 열쇠 → 줄. 줄이 없거나 열쇠가 비면 건너뛴다.
    ///
    /// <b>같은 열쇠가 두 줄이면 앞의 것을 남긴다.</b> 훑어서 처음 만난 것을
    /// 돌려주던 예전과 답이 같아야 한다 — 색인으로 바꾼 것 때문에 표가 같은데
    /// 게임이 달라지면, 표를 고친 사람은 무엇이 바뀐 건지 알 수가 없다.
    /// (표에 번호가 겹쳐 적힌 것은 그것대로 잘못이지만, 그건 시트를 옮겨 담는
    ///  쪽이 알려 줄 일이다. 여기서 답을 바꿔 버리면 그 알림과 어긋난다.)
    /// </summary>
    public static Dictionary<TKey, TValue> Build<TKey, TValue>(
        IReadOnlyList<TValue> rows, Func<TValue, TKey> keyOf)
    {
        var index = new Dictionary<TKey, TValue>();
        if (rows == null || keyOf == null) return index;

        foreach (TValue row in rows)
        {
            if (row == null) continue;

            TKey key = keyOf(row);

            // 열쇠가 없는 줄은 어차피 찾을 수 없다. Dictionary 는 null 열쇠를
            // 받지 않으므로, 걸러 내지 않으면 여기서 터진다.
            if (key == null) continue;

            if (index.ContainsKey(key)) continue;

            index[key] = row;
        }

        return index;
    }
}
