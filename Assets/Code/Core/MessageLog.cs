using System.Collections.Generic;
using System.Text;

/// <summary>
/// 메시지 창을 지나간 글의 이력. 상태창의 '로그' 가 이것을 읽는다.
///
/// 메시지 창은 3초면 저절로 닫히고 다음 메시지에 쉽게 덮인다. 달리는 중에는
/// 연출 대기까지 건너뛰므로 여러 줄이 눈 깜짝할 사이에 지나간다. 무엇에 맞았는지
/// 놓치고 나면 확인할 길이 없어서, 지나간 것을 여기에 쌓아 둔다.
///
/// ── 줄 단위로 쌓는 이유 ────────────────────────────────────────────────
/// 메시지 하나가 두 줄인 것이 있다("…를 쓰러뜨렸다.\n…경험치를 얻었다.").
/// 그것을 한 칸으로 세면 판에 몇 줄이 들어가는지 계산이 어긋나, 스크롤 끝에서
/// 마지막 줄이 잘리거나 빈 줄이 남는다. 그래서 넣을 때 미리 줄로 쪼갠다.
///
/// 차례는 오래된 것이 앞이다. 화면도 그대로 위에서 아래로 읽으면 되고,
/// 두 줄짜리 메시지의 앞뒤가 뒤집히지 않는다. 대신 여는 쪽에서 맨 끝으로
/// 내려 놓아, 열자마자 방금 일어난 일이 보이게 한다.
///
/// 화면도 입력도 모르므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class MessageLog
{
    /// <summary>
    /// 남겨 두는 줄 수. 넘치면 오래된 것부터 버린다.
    ///
    /// 한 판이 수천 턴까지 가므로 전부 들고 있으면 계속 늘어나기만 한다.
    /// 되짚어 보는 것은 대개 방금 몇 수라, 이 정도면 넉넉하다.
    /// </summary>
    public const int MaxLines = 200;

    /// <summary>아직 아무것도 지나가지 않았을 때.</summary>
    public const string EmptyText = "아직 아무 일도 일어나지 않았다.";

    // 오래된 것이 앞.
    private readonly List<string> lines = new List<string>();

    /// <summary>쌓여 있는 줄 수. 스크롤 범위를 정하는 데 쓴다.</summary>
    public int Count => lines.Count;

    /// <summary>
    /// 지나간 메시지 하나를 기록한다.
    ///
    /// 창에 실제로 뜬 것만 부른다. 뜨지 못하고 무시된 메시지까지 남기면
    /// 플레이어가 본 적 없는 줄이 이력에 섞인다.
    /// </summary>
    public void Add(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        foreach (string piece in message.Split('\n'))
        {
            string line = piece.Trim();
            if (line.Length == 0) continue;
            lines.Add(line);
        }

        if (lines.Count > MaxLines)
            lines.RemoveRange(0, lines.Count - MaxLines);
    }

    /// <summary>새 판을 시작할 때 지난 판의 이력을 비운다.</summary>
    public void Clear() => lines.Clear();

    /// <summary>
    /// 판에 채울 글자. offset 번째 줄부터 capacity 줄까지.
    ///
    /// 상태 화면과 달리 번호를 붙이지 않는다. 몇 번째 줄인지가 아니라
    /// 무슨 일이 있었는지를 읽는 화면이고, 번호가 앞에 붙으면 두 줄짜리
    /// 메시지의 뒷줄만 번호를 달고 따로 노는 것처럼 보인다.
    /// </summary>
    public string BuildText(int offset, int capacity)
    {
        if (lines.Count == 0) return EmptyText;

        int start = PageScroll.ClampOffset(offset, lines.Count, capacity);
        int end = System.Math.Min(start + System.Math.Max(capacity, 1), lines.Count);

        var sb = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            if (i > start) sb.Append('\n');
            sb.Append(lines[i]);
        }
        return sb.ToString();
    }
}
