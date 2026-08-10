using UnityEngine;

/// <summary>
/// 여러 줄을 큰 판에 나눠 보여 줄 때의 스크롤 계산.
///
/// 상태 화면과 로그 화면이 같은 창을 쓰므로 넘기는 규칙도 같아야 한다.
/// 한쪽에만 규칙을 고치면 같은 창인데 손맛이 달라져, 어느 화면에서
/// 무엇이 되는지를 플레이어가 따로 외워야 한다. 그래서 한곳에 둔다.
///
/// 화면도 입력도 모르므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class PageScroll
{
    /// <summary>
    /// 스크롤 위치를 목록 안으로 되돌린다.
    ///
    /// 위로 더 올릴 것이 없으면 0, 아래로 더 내릴 것이 없으면 화면이 꽉 차는 자리에 멈춘다.
    /// 다 들어가는 짧은 목록은 아예 움직이지 않는다.
    ///
    /// int.MaxValue 를 넣으면 '맨 끝' 이 된다. 로그처럼 최근 것부터 보여 줘야 하는
    /// 화면이 이것을 시작 자리로 쓴다.
    /// </summary>
    public static int ClampOffset(int offset, int count, int capacity)
    {
        if (capacity <= 0) return 0;
        return Mathf.Clamp(offset, 0, Mathf.Max(0, count - capacity));
    }
}
