using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데리고 다니는 동료 하나.
///
/// ── 판단은 여기 없다 ──────────────────────────────────────────────
/// 이번 턴에 무엇을 할지는 <see cref="PartnerPlan"/> 이 정하고, 이 클래스는
/// 그 결정을 실행만 한다. 적(Enemy)은 판단과 이동이 한 덩어리라 에디터를
/// 켜야만 확인할 수 있었고, 그 탓에 "논리는 갈 수 있다는데 물리가 막는"
/// 버그를 두 번 냈다. 같은 길을 가지 않으려고 갈라 두었다.
///
/// ── 층을 넘으면 사라진다 ──────────────────────────────────────────
/// 씬을 다시 부르므로 이 오브젝트는 플레이어와 함께 없어진다. 누가 몇 체력으로
/// 따라오는지는 <see cref="GameManager.Party"/> 가 들고 있고, 새 층에서 그 값으로
/// 다시 만들어진다. 그래서 이 클래스는 판 단위 상태를 스스로 갖지 않는다.
/// </summary>
public class Partner : MovingObject
{
    /// <summary>어느 동료인가. 프리팹에서 연결한다.</summary>
    public PartnerDescription description;

    /// <summary>같은 칸으로 볼 거리.</summary>
    private const float SameCellThreshold = 0.1f;

    /// <summary>공격이 맞을 확률(10%는 빗나감). 플레이어·적과 같은 값이다.</summary>
    private const float HitChance = 0.9f;

    /// <summary>이번 턴에 자리를 옮겼는가. 적의 MovedThisTurn 과 같은 뜻이다.</summary>
    public bool MovedThisTurn { get; private set; }

    /// <summary>이번 턴의 결정. 정하는 단계와 실행하는 단계가 나뉘어 있다.</summary>
    private PartnerTurn plan;

    public int Id => description != null ? description.id : 0;

    /// <summary>화면에 보여 줄 이름.</summary>
    public string DisplayName => description != null ? description.partnerName : name;

    /// <summary>명단에 적힌 이 동료의 몫. 없으면 null(중립이거나 쓰러졌다).</summary>
    private PartnerMember Member => GameManager.Party.Find(Id);

    /// <summary>
    /// 함께 다니기로 한 사이인가. <b>명단에 있으면 동료, 없으면 중립</b>이다.
    ///
    /// 상태를 이 오브젝트에 따로 두지 않는 이유가 있다. 층을 넘으면 오브젝트는
    /// 사라지고 명단만 남으므로, 오브젝트에 적어 두면 그 값이 층마다 초기화된다.
    /// 명단 하나가 곧 답이면 어긋날 자리가 없다.
    ///
    /// ── 중립일 때 ─────────────────────────────────────────────────────
    /// 함께 싸우지 않는다(제자리에 선다). 적도 때리지 않는다(Enemy.AdjacentPartner).
    /// 말을 걸어 승낙을 받아야 비로소 동료가 된다.
    /// </summary>
    public bool Joined => Member != null;

    protected override void Start()
    {
        GameManager.instance.AddPartner(this);

        // 방 목록은 층을 만든 쪽이 들고 있다. 적도 같은 것을 받아 쓴다.
        RandomDung dungeon = FindObjectOfType<RandomDung>();
        if (dungeon != null)
        {
            roomdata = dungeon.RoomData;
            exitPoints = dungeon.edgePoints;
        }

        // base.Start() 가 자식에서 SpriteRenderer 를 찾아 두므로 그 뒤에 입힌다.
        base.Start();

        if (description == null)
        {
            // 비어 있으면 이름도 수치도 나오지 않는다. 조용히 넘어가면
            // "왜 안 따라오지" 를 엉뚱한 곳에서 찾게 된다.
            Debug.LogWarning($"{name}: 동료 정의(description)가 비어 있습니다.", this);
            return;
        }

        SpriteApplier.Apply(gameObject, description.sprite);
    }

    private void OnDestroy()
    {
        if (GameManager.instance != null) GameManager.instance.RemovePartner(this);
    }

    // ── 한 턴 ───────────────────────────────────────────────────────────────

    /// <summary>맞은 턴 수. 무거움(두 턴에 한 걸음)을 세는 데 쓴다.</summary>
    private int turnsSeen;

    /// <summary>직전에 서 있던 칸. 서성일 때 되돌아가지 않으려고 기억한다.</summary>
    private Vector3Int? cameFrom;

    /// <summary>
    /// 지금 플레이어를 따라가는 중인가. 놓치면 탐색으로 돌아간다.
    ///
    /// 적의 currentMode(Chase/Patrol)와 같은 자리다. 층을 넘으면 오브젝트가
    /// 새로 만들어지므로 <b>새 층에서는 다시 찾는 것부터 시작한다</b> —
    /// 계단을 함께 내려온 동료는 곁에 서므로 첫 턴에 곧바로 눈에 든다.
    /// </summary>
    private bool following;

    /// <summary>RandomDung 이 만든 방들. 플레이어가 보이는지 판단하는 데 쓴다.</summary>
    private List<Rect> roomdata;

    /// <summary>방에서 통로로 빠지는 지점들. 탐색의 목표가 여기서 나온다.</summary>
    private List<Vector3> exitPoints;

    /// <summary>지금 가 보려는 자리. 닿거나 막히면 새로 고른다.</summary>
    private Vector3Int? searchTarget;

    /// <summary>이미 다녀온 문들. 두 문 사이를 오가지 않으려고 기억한다.</summary>
    private readonly List<Vector2> visitedExits = new List<Vector2>();

    /// <summary>목표가 없을 때 계속 나아갈 방향. 통로에서 되돌아서지 않게 한다.</summary>
    private Vector2 lastHeading = Vector2.down;

    /// <summary>이번 턴의 이동 기록을 지운다. GameManager 가 턴을 시작할 때 부른다.</summary>
    public void BeginTurn() => MovedThisTurn = false;

    /// <summary>
    /// 이번 턴에 무엇을 할지 정한다. <b>아직 움직이지 않는다.</b>
    ///
    /// 모두가 먼저 정한 뒤에 움직여야 목록에서의 차례가 규칙이 되지 않는다.
    /// 적의 DecideAttack 이 같은 이유로 이동보다 먼저 몰려 있다.
    /// </summary>
    public void DecideTurn()
    {
        bool joined = Joined;

        // 맞은 턴을 세는 것은 여기다. 세고 나서 판단해야 첫 턴부터 움직인다 —
        // BeginTurn 에서 세면 첫 턴이 1 이 되어 무거움에 걸려 한 턴을 서 있는다.
        int turn = turnsSeen++;

        // 아직 남이면 두 턴에 한 걸음이다(무거움). 플레이어가 쫓아가서 말을
        // 걸 수 있어야 하는데, 같은 속도로 서성이면 영영 따라잡지 못한다.
        if (!PartnerPlan.ActsThisTurn(turn, joined))
        {
            plan = PartnerTurn.Stay();
            return;
        }

        // 남은 플레이어를 쫓지도 적을 치지도 않고 제자리 근처를 서성인다.
        // 아직 말을 안 걸어 본 상대라 <b>제자리를 지키는 편</b>이 낫다 —
        // 층을 뒤지고 다니면 플레이어가 찾아가 말을 걸 수가 없다.
        if (!joined)
        {
            plan = PartnerPlan.DecideNeutral(Cell, CanEnter, Random.value, cameFrom);
            return;
        }

        // 동료라도 플레이어를 놓쳤으면 <b>탐색으로 돌아간다.</b>
        //
        // 예전에는 걸음 수 지도만 보고 어디에 있든 정확히 찾아왔다. 워프로 맵
        // 반대편에 떨어져도 곧장 오는 것이 개연성이 없었다 — 동료는 지도를
        // 들고 있는 것이 아니라 눈으로 보고 따라오는 것이다.
        following = PartnerPlan.KeepsFollowing(following, CanSeePlayer, StepsToPlayer(Cell));

        if (!following)
        {
            plan = DecideSearch();
            return;
        }

        plan = PartnerPlan.Decide(
            Cell,
            ToCell(GameManager.instance.PlayerPosition),
            EnemyCells(),
            CanEnter,
            StepsToPlayer);
    }

    /// <summary>이번 턴이 공격인가. 공격만 한 명씩 차례로 돌린다(연출이 있어서).</summary>
    public bool WillAttack => plan.Action == PartnerAction.Attack;

    /// <summary>이번 턴의 이동. 기다리지 않는다.</summary>
    public void TakeStep()
    {
        if (plan.Action != PartnerAction.Move) return;

        cameFrom = Cell;

        Vector3 destination = GridUtil.CellCenter(plan.Target);
        MoveTo(destination);
        MovedThisTurn = true;

        // 같은 턴의 적·동료가 이 칸을 노리지 않도록 찜해 둔다.
        // 적과 같은 목록을 쓴다 — 나뉘어 있으면 서로의 자리를 모른 채 겹친다.
        GameManager.instance.enemyPositions.Add(destination);
    }

    /// <summary>이번 턴의 공격. 찌르는 연출이 있어 한 명씩 차례로 돈다.</summary>
    public IEnumerator AttackTurn()
    {
        Enemy target = EnemyAt(plan.Target);
        if (target == null) yield break;

        Vector2 direction = GridUtil.CellCenter(plan.Target) - transform.position;

        if (!GameManager.FastForward)
            yield return LungeTowards(direction);

        // 연출 도중에 다른 곳에서 쓰러졌을 수 있다.
        if (target == null || !target.gameObject.activeInHierarchy) yield break;

        Strike(target);
    }

    /// <summary>이 동료가 가진 특성 번호들. 정의가 없으면 빈 목록.</summary>
    private IReadOnlyList<int> TraitIds =>
        description != null ? description.TraitIds : System.Array.Empty<int>();

    private void Strike(Enemy target)
    {
        // 특성이 규칙을 바꾼다. 무엇을 바꾸는지는 TraitEffects 가 알고,
        // 여기서는 "이 특성 묶음이 그런가" 만 묻는다 — 특성이 늘어도 이 파일은 그대로다.
        if (!TraitEffects.AlwaysHits(TraitIds) && Random.value >= HitChance)
        {
            GameManager.instance.ShowMessage(GameMessage.AttackMissed);
            return;
        }

        // 동료의 공격력은 시트의 값 그대로다. 플레이어와 달리 장비를 차지 않는다.
        int attack = description != null ? description.Attack : 1;

        target.TakeDamageFromPartner(attack, this);

        // 연격은 한 번의 공격에 두 번 때린다. 첫 방에 쓰러졌으면 두 번째는 없다.
        if (TraitEffects.StrikesTwice(TraitIds)
            && target != null && target.gameObject.activeInHierarchy)
        {
            target.TakeDamageFromPartner(attack, this);
        }
    }

    // ── 맞는 쪽 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 적에게 맞았다. 체력은 명단이 들고 있으므로 그쪽을 깎는다.
    ///
    /// 쓰러지면 명단에서 빠지고 오브젝트도 사라진다. 두 가지를 한곳에서 하는
    /// 이유는, 갈라 두면 한쪽만 부르는 경로가 반드시 생기기 때문이다.
    /// </summary>
    public void Damaged(int attack)
    {
        PartnerMember member = Member;
        if (member == null) return;

        int defense = description != null ? description.Defense : 0;
        int damage = Mathf.Max(1, attack - defense);

        if (GameManager.Party.Damage(Id, damage))
        {
            GameManager.instance.ShowMessage(GameMessage.PartnerFell, DisplayName);
            Destroy(gameObject);
            return;
        }

        GameManager.instance.ShowMessage(GameMessage.PartnerDamaged, DisplayName, damage);
    }

    /// <summary>
    /// 방어력을 무시하고 그대로 들어가는 피해. 날려져 부딪혔을 때 쓴다.
    ///
    /// 적의 TakeTrueDamage 와 짝이다. 부딪힘은 공격이 아니라 사고라서,
    /// 갑옷이 두껍다고 덜 아플 이유가 없다.
    /// </summary>
    public void TakeTrueDamage(int amount)
    {
        PartnerMember member = Member;
        if (member == null || amount <= 0) return;

        if (GameManager.Party.Damage(Id, amount))
        {
            GameManager.instance.ShowMessage(GameMessage.PartnerFell, DisplayName);
            Destroy(gameObject);
            return;
        }

        GameManager.instance.ShowMessage(GameMessage.PartnerDamaged, DisplayName, amount);
    }

    /// <summary>회복 아이템을 맞았다. 최대를 넘지 않는다.</summary>
    public void HealedBy(int amount)
    {
        if (Member == null || amount <= 0) return;

        GameManager.Party.Heal(Id, amount);
        GameManager.instance.ShowMessage(GameMessage.PartnerHealed, DisplayName);
    }

    // ── 자리 ────────────────────────────────────────────────────────────────

    /// <summary>지금 선 칸.</summary>
    public Vector3Int Cell => ToCell(transform.position);

    /// <summary>
    /// 말을 건다. 승낙하면 명단에 올라 동료가 되고, 그때부터 함께 싸운다.
    /// 이미 동료면 아무 일도 하지 않고 false.
    /// </summary>
    public bool TryJoin()
    {
        if (Joined || description == null) return false;

        if (!GameManager.Party.TryAdd(description.id, description.Hp)) return false;

        // 도감에 "만나 봤다" 를 적는다. 목록에 이름이 뜨는 조건이 이것이다.
        // 승낙한 뒤에만 적는다 — 물어보기만 하고 거절당한 것은 동행이 아니다.
        GameManager.RecordPartnerMet(description.id);
        return true;
    }

    /// <summary>대사 묶음. 아직 안 적어 둔 동료면 null 이고, 그때는 말하지 않는다.</summary>
    private PartnerLineSet Lines => description != null ? description.Lines : null;

    /// <summary>처음 만났을 때의 대사. 적어 두지 않은 동료면 빈 문자열.</summary>
    public string MeetLine => Lines != null ? Lines.Meet : string.Empty;

    /// <summary>
    /// 지금 체력에 맞는 대사. 다친 정도가 말투로 드러난다.
    ///
    /// 체력을 숫자로 보여 주지 않는 이유는, 동료의 체력이 HUD 에 늘 떠 있는
    /// 값이 아니라 <b>말을 걸어야 알 수 있는 것</b>이기 때문이다. 숫자를 함께
    /// 띄우면 대사는 장식이 되고 숫자만 읽게 된다.
    /// </summary>
    public string HpLine
    {
        get
        {
            PartnerMember member = Member;
            if (member == null || Lines == null) return string.Empty;

            return PartnerLineSheet.LineForPercent(
                Lines, PartnerLineSheet.PercentOf(member.Hp, member.MaxHp));
        }
    }

    /// <summary>특기를 청하는 대사. 특기 자체는 아직 없다.</summary>
    public string ActionLine => Lines != null ? Lines.Action : string.Empty;

    /// <summary>다음 레벨까지 남은 경험치. 명단에 없으면 0.</summary>
    public int ExpRemaining
    {
        get
        {
            PartnerMember member = Member;
            return member != null ? member.ExpRemaining : 0;
        }
    }

    /// <summary>
    /// 자리를 바꾼다. 플레이어가 달리기 버튼을 누른 채 걸어 들어왔을 때 쓴다.
    ///
    /// 검사는 부르는 쪽(Player)이 이미 했다. 여기서 다시 막으면 "바꿀 수 있다고
    /// 했는데 안 바뀌는" 어긋남이 생긴다.
    /// </summary>
    public void SwapTo(Vector3 destination)
    {
        MoveTo(destination);
        MovedThisTurn = true;
    }

    private static Vector3Int ToCell(Vector3 world)
        => new Vector3Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), 0);

    /// <summary>
    /// 판단에 넘길 적들의 칸.
    ///
    /// <b>붙어 있지만 무기가 안 닿는 적은 뺀다.</b> 벽 두 개가 맞물린 대각선 틈으로는
    /// 손이 들어가지 않는데(TileRules.CanReachWithMelee), 그대로 넘기면 동료가
    /// 벽을 뚫고 때린다. 플레이어도 적도 지키는 규칙이라 동료만 예외일 수 없다.
    ///
    /// 빼 두면 판단 쪽은 그 적을 "아직 못 붙은 적" 으로 보고 다가간다 — 모서리를
    /// 돌아서 치게 되므로 멈춰 서 버리지 않는다.
    ///
    /// <b>원거리 무기를 든 동료가 생기면 이 걸러 내기를 안 하면 된다.</b>
    /// 판단하는 쪽(PartnerPlan)에 무기 규칙을 심지 않은 이유가 그것이다 —
    /// 거기 심었으면 원거리 동료마다 예외를 파야 한다.
    /// </summary>
    private List<Vector3Int> EnemyCells()
    {
        var cells = new List<Vector3Int>();
        Vector3 origin = transform.position;

        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            Vector3Int cell = ToCell(enemy.transform.position);

            if (PartnerPlan.IsAdjacent(Cell, cell)
                && !TileRules.CanReachWithMelee(
                       tilemap, origin, GridUtil.CellCenter(cell) - origin))
                continue;

            cells.Add(cell);
        }

        return cells;
    }

    private Enemy EnemyAt(Vector3Int cell)
    {
        Vector3 center = GridUtil.CellCenter(cell);

        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (Vector2.Distance(enemy.transform.position, center) < SameCellThreshold)
                return enemy;
        }

        return null;
    }

    /// <summary>
    /// 그 칸에 들어갈 수 있는가.
    ///
    /// 지형과 <b>다른 생물</b>을 함께 본다. 물리에 묻지 않는 것이 중요하다 —
    /// 한 턴의 이동은 모두 같은 프레임에 일어나고 Rigidbody2D 는 다음 물리
    /// 갱신에야 좌표를 옮기므로, 콜라이더로 판단하면 이미 비운 칸도 막힌 것으로
    /// 보인다. 적이 그 문제로 한 칸씩 뒤처졌었다(Enemy.TryMoveAlong 주석).
    /// </summary>
    private bool CanEnter(Vector3Int cell)
    {
        Vector3 center = GridUtil.CellCenter(cell);

        if (IsBlockedByTile(center)) return false;
        if (IsBlockedByWater(center)) return false;
        if (!CanMoveDiagonally(transform.position, center - transform.position)) return false;

        // 플레이어 자리. 동료가 밀고 들어가면 플레이어가 튕겨 나간다.
        if (Vector2.Distance(GameManager.instance.PlayerPosition, center) < SameCellThreshold)
            return false;

        // 이번 턴에 누군가 찜한 칸(적·동료가 같은 목록을 쓴다).
        foreach (Vector2 reserved in GameManager.instance.enemyPositions)
        {
            if (Vector2.Distance(center, reserved) < SameCellThreshold) return false;
        }

        // 아직 안 움직인 것들이 서 있는 자리. 이미 옮긴 것은 찜한 칸으로 표현된다.
        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            if (enemy.MovedThisTurn) continue;

            if (Vector2.Distance(center, enemy.transform.position) < SameCellThreshold)
                return false;
        }

        foreach (Partner other in GameManager.instance.partners)
        {
            if (other == null || other == this) continue;
            if (!other.gameObject.activeInHierarchy || other.MovedThisTurn) continue;

            if (Vector2.Distance(center, other.transform.position) < SameCellThreshold)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 플레이어를 놓쳤을 때의 한 걸음. <b>서성이지 않고 목표를 정해 걷는다.</b>
    ///
    /// 목표는 둘 중 하나다 —
    ///   들리는 소리가 있으면 그쪽. 싸우는 소리가 나면 가 보는 것이 자연스럽고,
    ///   무엇보다 <b>왜 저기로 가는지가 화면에서 설명된다.</b>
    ///   없으면 방에서 통로로 빠지는 지점 하나. 적의 순찰이 쓰는 것과 같은 목록이다.
    ///
    /// 목표에 닿거나 길이 막히면 새로 고른다. 서성이던 때와 달리 층을 실제로
    /// 훑고 다니므로, 놓친 플레이어와 다시 만나는 것이 운에만 달려 있지 않다.
    /// </summary>
    private PartnerTurn DecideSearch()
    {
        // 소리가 들리면 그쪽이 목표다. 싸우는 소리가 나면 가 보는 것이 자연스럽고,
        // 무엇보다 왜 저기로 가는지가 화면에서 설명된다.
        if (GameManager.Noise.TryGetLatest(GameManager.TurnCount, out Vector3Int heard))
            searchTarget = heard;

        // 목표가 없거나 닿았으면 <b>지금 방의 문</b> 중에서 새로 고른다.
        // 층 전체에서 고르면 벽 너머가 목표가 되어 영영 닿지 못한다(PatrolPlan).
        if (!searchTarget.HasValue || searchTarget.Value == Cell)
        {
            if (PatrolPlan.TryPickTarget(
                    transform.position, roomdata, exitPoints, visitedExits,
                    Random.value, out Vector3 picked))
            {
                searchTarget = new Vector3Int(
                    Mathf.FloorToInt(picked.x), Mathf.FloorToInt(picked.y), 0);
                visitedExits.Add(new Vector2(picked.x, picked.y));
            }
            else
            {
                // 통로 한가운데라 고를 수 없다. 방에 들어설 때까지 그냥 나아간다.
                searchTarget = null;
            }
        }

        // 방향을 차례로 물러서 가며 시도한다. 한 칸만 보고 고르면 벽 앞에서
        // 갈 곳이 없어져 그 자리에 서 버린다(적이 이 사다리를 쓰는 이유).
        IEnumerable<Vector2> wanted = searchTarget.HasValue
            ? MoveIntent.Toward(Cell, searchTarget.Value)
            : MoveIntent.Toward(lastHeading);

        foreach (Vector2 direction in wanted)
        {
            var candidate = new Vector3Int(
                Cell.x + Mathf.RoundToInt(direction.x),
                Cell.y + Mathf.RoundToInt(direction.y), 0);

            if (!CanEnter(candidate)) continue;

            lastHeading = direction;
            return PartnerTurn.MoveTo(candidate);
        }

        // 어느 쪽으로도 못 갔다. 목표를 버리고 다음 턴에 새로 고른다.
        searchTarget = null;
        return PartnerTurn.Stay();
    }

    /// <summary>
    /// 플레이어가 보이는가. <b>같은 방에 있으면 보인다</b>고 본다.
    ///
    /// 적이 플레이어를 발견하는 규칙과 같다(Enemy.UpdateMode). 둘이 다르면
    /// "적은 쫓아오는데 동료는 못 보는" 자리가 생겨 화면에서 설명이 안 된다.
    ///
    /// 통로에서는 둘 다 어느 방에도 없어 false 가 된다. 그래서 이 값만으로
    /// 따라갈지 정하지 않는다 — KeepsFollowing 이 "한 번 만나면 이어 간다" 로 받는다.
    /// </summary>
    private bool CanSeePlayer
    {
        get
        {
            if (roomdata == null) return false;

            Vector2 here = transform.position;
            Vector2 player = GameManager.instance.PlayerPosition;

            foreach (Rect room in roomdata)
            {
                if (room.Contains(here) && room.Contains(player)) return true;
            }

            return false;
        }
    }

    /// <summary>그 칸에서 플레이어까지의 걸음 수. 갈 수 없으면 음수.</summary>
    private int StepsToPlayer(Vector3Int cell)
    {
        if (!GameManager.Chase.IsReady) return -1;

        return GameManager.Chase.TryGetDistance(GridUtil.CellCenter(cell), out int steps)
            ? steps
            : -1;
    }
}
