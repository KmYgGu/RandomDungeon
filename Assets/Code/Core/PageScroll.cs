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

    // ── 페이지로 넘겨 보는 목록(도감) ──────────────────────────────────────
    //
    // 로그·상태 화면은 줄이 이어진 두루마리라 자유롭게 스크롤한다. 도감은 다르다 —
    // <b>한 판이 한 페이지</b>이고, 위/아래는 그 페이지 안에서만 커서를 옮기며
    // 좌/우로만 페이지가 넘어간다.
    //
    // 스크롤과 섞으면 "지금 몇 페이지인가" 가 사라진다. 위/아래로도 화면이 밀리면
    // 좌/우로 넘긴 페이지 경계가 곧 흐트러져서, 페이지를 나눈 뜻이 없어진다.

    /// <summary>모두 몇 페이지인가. 목록이 비어 있어도 한 페이지는 있다.</summary>
    public static int PageCount(int count, int capacity)
    {
        if (capacity <= 0) return 1;
        return Mathf.Max(1, (count + capacity - 1) / capacity);
    }

    /// <summary>페이지 번호를 있는 범위 안으로 되돌린다.</summary>
    public static int ClampPage(int page, int count, int capacity)
        => Mathf.Clamp(page, 0, PageCount(count, capacity) - 1);

    /// <summary>이 페이지에 실제로 놓인 줄 수. 마지막 페이지는 덜 찬다.</summary>
    public static int RowsOnPage(int page, int count, int capacity)
    {
        if (capacity <= 0) return 0;

        int start = ClampPage(page, count, capacity) * capacity;
        return Mathf.Clamp(count - start, 0, capacity);
    }

    /// <summary>
    /// 페이지 안에서 커서를 옮긴다. <b>페이지를 벗어나지 않고 위아래로 감싸 돈다.</b>
    ///
    /// 끝에서 다음 페이지로 넘어가지 않는 것이 중요하다. 그러면 위/아래만으로도
    /// 목록 전체를 지나갈 수 있게 되어, 좌/우로 넘기는 규칙과 둘 중 어느 것이
    /// 진짜인지 손이 알 수 없다.
    /// </summary>
    public static int StepWithinPage(int selected, int step, int page, int count, int capacity)
    {
        int rows = RowsOnPage(page, count, capacity);
        if (rows <= 0) return 0;

        int start = ClampPage(page, count, capacity) * capacity;
        int within = selected - start;

        // 창 크기가 바뀌어 커서가 이 페이지 밖에 있으면 맨 위로 되돌린다.
        if (within < 0 || within >= rows) return start;

        return start + ((within + step) % rows + rows) % rows;
    }
}
