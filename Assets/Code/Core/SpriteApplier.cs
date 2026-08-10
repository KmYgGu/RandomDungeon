using UnityEngine;

/// <summary>
/// 정의 애셋이 들고 있는 그림을 실제 오브젝트에 입힌다.
///
/// 그림을 프리팹과 정의 애셋 양쪽에 두면 둘이 어긋날 수 있다. 글자 데이터에
/// 대해 "사본을 두지 않는다" 로 정한 것과 같은 이유로, 원본은 정의 애셋 한 곳에
/// 두고 프리팹은 실행할 때 받아 간다. 그래서 프리팹마다 그림을 넣어 둘 필요가 없다.
///
/// ── 왜 GetComponentInChildren 인가 ────────────────────────────────
/// MovingObject 를 물려받은 것들(적)은 SpriteRenderer 가 자식에 달려 있다
/// (MovingObject.Start 가 자식에서 찾는다). 함정은 자기 자신에 달려 있다.
/// GetComponentInChildren 은 자기 자신부터 보므로 둘 다 이 한 줄로 된다.
/// </summary>
public static class SpriteApplier
{
    /// <summary>
    /// 그림을 입힌다. 애셋에 그림이 없으면 <b>아무것도 하지 않는다.</b>
    ///
    /// 비어 있을 때 그대로 대입하면 프리팹에 이미 들어 있던 그림까지 지워져
    /// 오브젝트가 화면에서 사라진다. 애셋을 방금 만들어 그림을 아직 안 넣은
    /// 상태가 흔하므로, 그때는 프리팹 것을 그대로 쓰게 둔다.
    /// </summary>
    public static void Apply(GameObject target, Sprite sprite)
    {
        if (target == null || sprite == null) return;

        SpriteRenderer renderer = target.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) renderer.sprite = sprite;
    }
}
