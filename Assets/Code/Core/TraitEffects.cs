using System.Collections.Generic;

/// <summary>
/// 특성이 실제로 무엇을 하는가. <b>번호 → 성질 표</b>다.
///
/// ── 왜 미리 만들어 두는가 ─────────────────────────────────────────
/// trait 시트에 19 종이 있는데 코드는 아직 하나도 없다. 이대로 두면 특성을
/// 만들 때마다 <c>if (특성 == 신속)</c> 같은 것이 Partner · Enemy · Equipment 에
/// 흩어진다. 상태이상이 이미 그 조짐이었다 — HasEffect 를 부르는 곳이 늘어난다.
///
/// 그래서 <b>묻는 자리를 하나로 모은다.</b> 특성을 만드는 사람은 이 표에 한 줄을
/// 더하고, 쓰는 쪽은 "이 특성 묶음이 이런 성질을 가졌는가" 만 묻는다.
///
/// ── 왜 동작이 아니라 성질인가 ─────────────────────────────────────
/// 아이템·함정은 "무슨 일이 일어난다" 라서 동작(delegate)이 어울린다. 특성은
/// 대부분 <b>이미 있는 계산을 살짝 바꾸는 것</b>이다 — 두 번 때린다, 반드시
/// 맞는다, 함정을 안 밟는다. 그래서 동작을 담지 않고 <b>성질을 묻는 표</b>로 둔다.
///
/// 숫자가 필요한 성질(예: 추가 회피율)이 생기면 Traits 에 값을 담는 항목을
/// 하나 더하면 된다. 지금 없는 것을 미리 만들지는 않는다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class TraitEffects
{
    // ── trait 시트의 번호 ──────────────────────────────────────────────────
    //
    // 시트에서 이름을 다듬어도 게임이 바뀌지 않도록 번호로 가리킨다.
    // 옆의 주석은 사람이 읽으라고 붙인 것이다.

    public const int Focus = 101;          // 혼신 — 치명타시 방어력 무시
    public const int IronSafe = 102;       // 철제금고 — 아이템을 빼앗기지 않음
    public const int Nimble = 103;         // 기민 — 근접 회피
    public const int Swift = 104;          // 신속 — 2회 행동
    public const int Healthy = 105;        // 건강 — 상태이상 회복이 빠름
    public const int Adventurer = 106;     // 모험가 — 걸을 때마다 경험치
    public const int Insight = 107;        // 탐구 — 체력% 표시
    public const int EagleEye = 108;       // 이글아이 — 원거리 회피·아이템 캐치
    public const int DoubleStrike = 109;   // 연격 — 공격 2회
    public const int SureHit = 110;        // 필중 — 근접 반드시 명중
    public const int TrapExpert = 111;     // 함정전문가 — 밟은 함정이 안 터짐
    public const int Appraise = 112;       // 가치부여 — 지닌 아이템이 비싸짐
    public const int Hoarder = 113;        // 수집광 — 아이템 수만큼 최대 HP
    public const int Drain = 114;          // 흡혈 — 치명타시 절반 회복
    public const int Pure = 115;           // 순결 — 체력 100%면 효과 무시
    public const int LuckyBag = 116;       // 복주머니 — 층마다 소모품 하나
    public const int Marksman = 117;       // 명사수 — 던진 것 필중·관통
    public const int Guard = 118;          // 보호 — 플레이어 대신 맞음
    public const int Fighting = 119;       // 투지 — 체력이 낮을수록 치명타율

    /// <summary>
    /// 지금 코드가 실제로 보고 있는 성질들.
    ///
    /// <b>여기 없는 특성은 아직 아무 일도 하지 않는다.</b> 시트가 코드보다 앞서
    /// 있는 것은 이 프로젝트에서 정상이다 — 상태이상 20 종 중 다섯만 도는 것과 같다.
    /// </summary>
    private readonly struct Traits
    {
        /// <summary>근접 공격이 반드시 맞는다(필중).</summary>
        public readonly bool AlwaysHits;

        /// <summary>한 번의 공격에 두 번 때린다(연격).</summary>
        public readonly bool StrikesTwice;

        /// <summary>한 턴에 두 번 움직인다(신속).</summary>
        public readonly bool ActsTwice;

        /// <summary>밟은 함정이 작동하지 않는다(함정전문가).</summary>
        public readonly bool IgnoresTraps;

        public Traits(
            bool alwaysHits = false,
            bool strikesTwice = false,
            bool actsTwice = false,
            bool ignoresTraps = false)
        {
            AlwaysHits = alwaysHits;
            StrikesTwice = strikesTwice;
            ActsTwice = actsTwice;
            IgnoresTraps = ignoresTraps;
        }
    }

    private static readonly Dictionary<int, Traits> table =
        new Dictionary<int, Traits>
        {
            [SureHit] = new Traits(alwaysHits: true),
            [DoubleStrike] = new Traits(strikesTwice: true),
            [Swift] = new Traits(actsTwice: true),
            [TrapExpert] = new Traits(ignoresTraps: true),
        };

    /// <summary>이 특성 묶음에 <b>동작이 붙어 있는 것</b>이 하나라도 있는가.</summary>
    public static bool IsImplemented(int traitId) => table.ContainsKey(traitId);

    /// <summary>근접 공격이 반드시 맞는가(필중).</summary>
    public static bool AlwaysHits(IReadOnlyList<int> traitIds)
        => Any(traitIds, t => t.AlwaysHits);

    /// <summary>한 번의 공격에 두 번 때리는가(연격).</summary>
    public static bool StrikesTwice(IReadOnlyList<int> traitIds)
        => Any(traitIds, t => t.StrikesTwice);

    /// <summary>한 턴에 두 번 움직이는가(신속).</summary>
    public static bool ActsTwice(IReadOnlyList<int> traitIds)
        => Any(traitIds, t => t.ActsTwice);

    /// <summary>밟은 함정이 작동하지 않는가(함정전문가).</summary>
    public static bool IgnoresTraps(IReadOnlyList<int> traitIds)
        => Any(traitIds, t => t.IgnoresTraps);

    /// <summary>
    /// 하나라도 그 성질을 가졌는가.
    ///
    /// 여러 특성이 같은 성질을 가지면 <b>하나만 있어도 켜진다.</b> 겹쳐서 두 배가
    /// 되는 성질은 지금 없고, 생기면 그때는 값을 담는 항목으로 만들어야 한다.
    /// </summary>
    private static bool Any(IReadOnlyList<int> traitIds, System.Func<Traits, bool> has)
    {
        if (traitIds == null) return false;

        foreach (int id in traitIds)
        {
            if (table.TryGetValue(id, out Traits traits) && has(traits)) return true;
        }

        return false;
    }
}
