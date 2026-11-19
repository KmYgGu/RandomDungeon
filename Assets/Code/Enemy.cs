using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적의 행동. 플레이어를 발견하면 쫓고(Chase), 아니면 통로 쪽 출구를 향해 돌아다닌다(Patrol).
///
/// 예전에는 MoveEnemy() 하나가 200줄짜리 코루틴이었고,
/// "방향을 고르고 → 갈 수 있는지 보고 → 옮기고 → 턴을 끝낸다" 는 같은 흐름이
/// Chase / Patrol / 도착 후 배회 / 막혔을 때의 우회 이렇게 네 번 복사돼 있었다.
/// 그래서 EnemyTurnCompleted() 호출이 여덟 군데 흩어졌고, 어느 한 경로라도
/// 빠뜨리면 게임이 그대로 멈춰 버리는 구조였다.
///
/// 지금은 그 흐름이 TryMoveAlong() 한 곳에 있다.
///
/// 한 턴은 바깥(TurnLoop)이 세 단계로 나누어 돌린다.
///   DecideAttack() - 무엇을 할지 정한다. 모든 적이 먼저 정한 뒤에야 움직인다.
///   TakeStep()     - 이동. 기다림이 없어 마릿수와 무관하게 같은 프레임에 끝난다.
///   AttackTurn()   - 공격. 연출이 있어 한 마리씩 차례로 돈다.
///
/// ── 판단은 EnemyPlan 이 한다 ──────────────────────────────────────
/// 어떤 모드로 다닐지(추격 · 순찰 · 확인 · 집결), 누구를 때릴지는 EnemyPlan 이
/// 정한다. 이 클래스는 씬에서 사실을 모아 넘기고(EnemySight) 답대로 움직인다.
/// 동료(PartnerPlan · Partner)와 같은 나눔이고, 그래서 규칙은 Unity 없이 검사된다.
/// 모드 자체(EnemyMode)도 그쪽으로 옮겼다.
/// </summary>
public class Enemy : MovingObject
{
    /// <summary>치명타가 나올 확률과 그 배율.</summary>
    private const float CriticalChance = 0.1f;
    private const double CriticalMultiplier = 1.8;

    /// <summary>공격이 맞을 확률(10%는 빗나감).</summary>
    private const float HitChance = 0.9f;

    private static readonly Vector2[] CardinalDirections =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
    };

    /// <summary>RandomDung 이 만든 방들의 영역.</summary>
    public List<Rect> roomdata;

    /// <summary>방에서 통로로 빠져나갈 수 있는 지점들.</summary>
    public List<Vector3> connectionPoints;

    public int ThisHp;
    private int maxHp;
    private int attackPower;
    private int defense;

    [SerializeField] private EnemyMode currentMode = EnemyMode.Patrol;
    [SerializeField] private bool isPatrolTargetSet;
    [SerializeField] private Vector3 patrolTarget;

    /// <summary>순찰 목표에 도착해서, 이제 아무 데나 헤매는 중인가.</summary>
    private bool isWandering;

    /// <summary>
    /// 지금 순찰 목표까지의 걸음 수 지도. <b>내 것이 아니라 빌려 온 것이다</b> —
    /// 같은 문을 향하는 적들이 한 장을 함께 본다(RouteCache). 읽기만 한다.
    /// </summary>
    private ChaseMap patrolRoute;

    /// <summary>이번 턴에 막혀 있던 출구들. 턴마다 비워 다시 쓴다.</summary>
    private readonly HashSet<Vector2> blockedExits = new HashSet<Vector2>();

    /// <summary>배회할 때 네 방향을 섞어 담는 자리. 턴마다 새로 만들지 않는다.</summary>
    private readonly Vector2[] shuffledDirections = new Vector2[CardinalDirections.Length];
    private readonly EnemyCuriosity curiosity = new EnemyCuriosity();
    private readonly EnemySleep sleep = new EnemySleep();
    private MonsterHouseRally rally;
    private readonly EnemyDropPlan drops = new EnemyDropPlan();
    public bool IsSleeping => sleep.IsSleeping;

    public void ConfigureSpawnSleep(float sleepRoll, float typeRoll)
        => sleep.OnSpawn(sleepRoll, typeRoll);

    // ── 상인 ────────────────────────────────────────────────────────────────
    //
    // 상인은 따로 만든 것이 아니라 <b>처음에 중립인 적</b>이다. Merchant 컴포넌트가
    // 붙어 있고 아직 돌아서지 않은 동안에는 싸우지 않고 가게를 본다. 돌아서면
    // 아래 값이 바뀌는 것만으로 평범한 적의 추격이 그대로 돈다.

    /// <summary>상인이면 그 컴포넌트. 보통 적이면 null.</summary>
    public Merchant Merchant { get; private set; }

    /// <summary>
    /// 싸움의 상대인가. 중립인 상인만 아니다.
    ///
    /// 조준 · 달리기 정지 · 동료의 표적 · 근접 공격이 전부 이것을 본다. 그래서
    /// 중립 상인은 옆에 있어도 달리기가 멈추지 않고, 때리면 말을 건다(Damaged).
    /// </summary>
    public bool IsCombatTarget => Merchant == null || Merchant.Hostile;

    public void ConfigureMerchant(Merchant merchant) => Merchant = merchant;

    /// <summary>상인이 제 길(문 · 계산대)을 따라 한 걸음 걷는다. 판정은 적의 이동과 같다.</summary>
    public bool StepAlong(IEnumerable<Vector2> directions)
        => TryMoveAlong(directions, rememberPath: false);

    /// <summary>
    /// 걷지 않고 곧바로 그 자리에 선다. 상인이 문에서 비켜설 때 쓴다.
    ///
    /// 몸(Rigidbody2D)과 겉(transform)을 함께 옮기고 물리에 바로 알린다. 한쪽만
    /// 옮기면 다음 물리 갱신에서 몸이 겉을 옛 자리로 끌고 돌아간다.
    /// </summary>
    public void Relocate(Vector3 position)
    {
        if (rb2D == null) rb2D = GetComponent<Rigidbody2D>();

        rb2D.position = position;
        transform.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>층의 타일맵 · 방 · 문 목록을 다시 받고, 적 목록에 없으면 올린다.</summary>
    public void RefreshFloorBindings()
    {
        tilemap = GameManager.FloorTilemap;
        roomdata = GameManager.instance.MapMake.RoomData;
        connectionPoints = GameManager.instance.MapMake.edgePoints;

        if (!GameManager.instance.enemies.Contains(this))
            GameManager.instance.AddEnemyToList(this);
    }

    /// <summary>상인이 돌아섰다. 하던 일을 전부 놓고 추격으로 넘어간다.</summary>
    public void WakeMerchant()
    {
        sleep.Wake();
        WakeFromSleep();
        rally = null;
        currentMode = EnemyMode.Chase;
    }

    /// <summary>멀리서 날아온 것에 맞았다고 상점에 알린다. 상인이 아니면 아무 일도 없다.</summary>
    public void NotifyRangedHit(bool fromEnemy = false)
    {
        if (Merchant != null && Merchant.Shop != null) Merchant.Shop.RangedHit(fromEnemy);
    }

    public void ReceiveHouseAlarm(MonsterHouseRally destination)
    {
        if (ThisHp <= 0 || destination == null || Merchant != null) return;
        sleep.Wake();
        WakeFromSleep();
        rally = destination;
        currentMode = EnemyMode.Rally;
    }

    private void WakeFromSleep()
    {
        curiosity.Stop();
        currentMode = EnemyMode.Patrol;
        ResetPatrol(startWandering: false);
    }

    private void WakeOnDamage(int damage)
    {
        if (sleep.TryWakeOnDamage(damage)) WakeFromSleep();
    }

    /// <summary>같은 자리를 맴돌지 않도록 이미 지나온 칸을 기억한다.</summary>
    private readonly List<Vector2> visited = new List<Vector2>();

    /// <summary>
    /// 이번 턴에 이미 자리를 옮겼는가. 턴이 시작될 때 바깥에서 지운다.
    ///
    /// 옮긴 적은 <b>찜해 둔 칸</b>으로 표현되므로, 옛 자리는 비어 있는 것으로 봐야
    /// 뒤따르는 적이 그 칸을 이어받을 수 있다. 이것이 없으면 도망치는 플레이어를
    /// 쫓을 때 두 번째 적이 한 칸씩 뒤처진 채로 따라온다.
    /// </summary>
    public bool MovedThisTurn { get; private set; }

    /// <summary>이번 턴의 이동 기록을 지운다. GameManager 가 턴을 시작할 때 부른다.</summary>
    public void BeginTurn() => MovedThisTurn = false;

    /// <summary>플레이어까지의 걸음 수. 지도가 없으면 직선 거리로 어림한다.</summary>
    public float StepsToPlayer
    {
        get
        {
            if (rally != null && !rally.HasArrived(GameManager.FloorTilemap.WorldToCell(transform.position)) &&
                rally.TryGetDistance(transform.position, out int rallySteps)) return rallySteps;
            if (GameManager.Chase.IsReady
                && GameManager.Chase.TryGetDistance(transform.position, out int steps))
                return steps;

            return Vector2.Distance(transform.position, PlayerPosition);
        }
    }

    protected override void Start()
    {
        // 층을 만든 쪽은 GameManager 가 이미 들고 있다. 예전에는 적마다 씬 전체를
        // 뒤져 찾았는데(FindObjectOfType), 몬스터 하우스처럼 여럿이 한꺼번에 생기는
        // 순간에 그 탐색이 마릿수만큼 겹쳤다.
        RandomDung dungeon = GameManager.instance.MapMake;
        if (dungeon != null)
        {
            connectionPoints = dungeon.edgePoints;
            roomdata = dungeon.RoomData;
        }

        GameManager.instance.AddEnemyToList(this);

        // base.Start() 가 자식에서 SpriteRenderer 를 찾아 두므로 그 뒤에 입힌다.
        base.Start();

        if (description != null)
        {
            SpriteApplier.Apply(gameObject, description.sprite);
            if (description.directionalSprites != null && description.directionalSprites.Length == 8)
                directionsprites = description.directionalSprites;
        }
        FaceDirection(Vector2.down);
        if (IsSleeping) gameObject.AddComponent<EnemySleepIndicator>().Bind(this);
    }

    private Vector2 PlayerPosition => GameManager.instance.Player.transform.position;

    // ── 한 턴의 행동 ────────────────────────────────────────────────────────

    /// <summary>
    /// 이번 턴에 플레이어를 때릴 것인가. 판단하면서 추격/순찰 상태도 갱신한다.
    ///
    /// 한 턴은 두 단계로 나뉜다. 먼저 모든 적이 무엇을 할지 정하고(이 메서드),
    /// 그다음 이동하는 적을 한꺼번에 옮긴 뒤, 때리는 적만 차례로 돈다.
    ///
    /// 정하는 일을 이동보다 먼저 몰아 두는 이유가 있다. 그러지 않으면 앞의 적이
    /// 움직여 플레이어 옆에 붙는 순간 뒤의 적 판단이 달라져, 목록에서의 순서가
    /// 곧 규칙이 되어 버린다. 먼저 다 정해 두면 순서를 타지 않는다.
    /// </summary>
    public bool DecideAttack()
    {
        // 중립인 상인은 아무도 때리지 않는다.
        if (!IsCombatTarget)
        {
            target = null;
            partnerTarget = null;
            return false;
        }

        if (IsSleeping)
        {
            target = null;
            partnerTarget = null;
            if (!GameManager.Chase.TryGetDistance(transform.position, out int steps) ||
                !sleep.TryWakeNearby(steps, Random.value)) return false;
            WakeFromSleep();
        }
        UpdateMode();

        target = PickTarget();
        if (target != null) FaceDirection((Vector2)target.transform.position - (Vector2)transform.position);
        return target != null;
    }

    /// <summary>
    /// 판단에 넘길 사실들을 씬에서 모은다. 무엇을 할지는 여기서 정하지 않는다.
    /// </summary>
    private EnemySight LookAround()
    {
        bool rallying = rally != null;

        var sight = new EnemySight
        {
            Current = currentMode,
            HostileMerchant = Merchant != null && Merchant.Hostile,
            Rallying = rallying,
            RallyArrived = rallying
                && rally.HasArrived(GameManager.FloorTilemap.WorldToCell(transform.position)),
            ChaseMapReady = GameManager.Chase.IsReady,
            StepsToPlayer = -1,
            StraightDistance = Vector2.Distance(PlayerPosition, transform.position),
        };

        // 돌아선 상인이나 덜 온 집결은 아래를 보지 않고 정해진다. 그때는 방을
        // 뒤지거나 걸음 수를 묻는 일을 건너뛴다.
        if (sight.HostileMerchant || (rallying && !sight.RallyArrived)) return sight;

        // 같은 방 안에 있거나 바로 옆에 붙어 있으면 발견한 것으로 본다.
        Rect currentRoom = FindRoomContaining(transform.position);
        sight.PlayerVisible =
            (currentRoom != Rect.zero && currentRoom.Contains(PlayerPosition)) || IsPlayerAdjacent();

        if (sight.ChaseMapReady
            && GameManager.Chase.TryGetDistance(transform.position, out int steps))
            sight.StepsToPlayer = steps;

        return sight;
    }

    /// <summary>이번 턴에 때릴 상대. 플레이어면 partnerTarget 이 null 이다.</summary>
    private GameObject target;
    private Partner partnerTarget;

    /// <summary>
    /// 붙어 있는 것들 중 누구를 때릴지. 아무도 없으면 null.
    /// 누구를 고르는지는 EnemyPlan.PickTarget 이 정한다(둘 다 붙어 있으면 반반).
    /// </summary>
    private GameObject PickTarget()
    {
        partnerTarget = null;

        Partner partner = AdjacentPartner();

        switch (EnemyPlan.PickTarget(IsPlayerAdjacent(), partner != null, Random.value))
        {
            case EnemyTarget.Player:
                return GameManager.instance.Player;

            case EnemyTarget.Partner:
                partnerTarget = partner;
                return partner.gameObject;

            default:
                return null;
        }
    }

    /// <summary>붙어 있는 동료 하나. 없으면 null. 판정은 플레이어와 똑같다.</summary>
    private Partner AdjacentPartner()
    {
        Vector2 origin = transform.position;

        foreach (Partner partner in GameManager.instance.partners)
        {
            if (partner == null || !partner.gameObject.activeInHierarchy) continue;

            // 중립은 적도 건드리지 않는다. 승낙 전에 맞아 쓰러지면
            // 플레이어는 영문도 모른 채 동료 하나를 잃는다.
            if (!partner.Joined) continue;

            Vector2 offset = (Vector2)partner.transform.position - origin;
            if (offset.magnitude > 1.5f) continue;

            var direction = new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));
            if (direction == Vector2.zero) continue;

            if (IsBlockedByTile(origin + direction)) continue;

            bool isDiagonal = Mathf.Abs(direction.x) > 0 && Mathf.Abs(direction.y) > 0;
            if (isDiagonal && !CanMoveDiagonally(origin, direction)) continue;

            return partner;
        }

        return null;
    }

    /// <summary>
    /// 이번 턴의 이동. 기다리지 않는다.
    ///
    /// 좌표만 옮기고 끝난다. 옮긴 것이 화면에 반영되기를 기다리는 일은
    /// 부르는 쪽이 모든 적을 옮긴 뒤 한 번만 하면 된다.
    /// 적마다 기다리면 그 대기가 마릿수만큼 쌓여 플레이어 조작이 느려진다.
    /// </summary>
    public void TakeStep()
    {
        if (!IsCombatTarget) { Merchant.Step(); return; }
        if (IsSleeping) return;
        if (currentMode == EnemyMode.Rally && rally != null)
            TryMoveAlong(rally.StepsToward(transform.position), rememberPath: false);
        else if (currentMode == EnemyMode.Chase)
            StepTowardPlayer();
        else if (currentMode == EnemyMode.Curious)
            StepCurious();
        else
            StepPatrol();
    }

    /// <summary>
    /// 이번 턴의 공격. 찌르는 연출이 있어 한 마리씩 차례로 돈다.
    ///
    /// 겹쳐서 때리면 누구에게 얼마나 맞았는지가 메시지창에 마지막 하나만 남는다.
    /// 때리는 적은 플레이어에 붙어 있는 것들뿐이라 많아야 여덟이다.
    /// </summary>
    public IEnumerator AttackTurn()
    {
        if (IsSleeping) yield break;
        // 빨리감기 중에는 연출을 건너뛰고 피해만 준다.
        if (GameManager.FastForward)
        {
            StrikePlayer();
            yield break;
        }

        yield return AttackPlayer();
    }

    /// <summary>
    /// 이번 턴의 모드를 정하고 그 뒷일을 한다. 규칙은 EnemyPlan.NextMode 에 있다.
    /// </summary>
    private void UpdateMode()
    {
        EnemyModeChange change = EnemyPlan.NextMode(LookAround());

        currentMode = change.Mode;

        if (change.LeaveRally) rally = null;
        if (change.StopCuriosity) curiosity.Stop();
        if (change.ResetPatrol) ResetPatrol(startWandering: false);
    }

    /// <summary>실제 이동 방향에 맞춰 외형을 갱신한다.</summary>
    protected override void MoveTo(Vector2 end)
    {
        FaceDirection(end - (Vector2)transform.position);
        base.MoveTo(end);
    }

    private void FaceDirection(Vector2 direction)
    {
        if (direction == Vector2.zero || spriteRenderer == null) return;
        Vector2 facing = new Vector2(Mathf.Clamp(Mathf.RoundToInt(direction.x), -1, 1),
            Mathf.Clamp(Mathf.RoundToInt(direction.y), -1, 1));
        int index = GetDirectionInfo(facing).spriteIndex;
        if (directionsprites == null || index >= directionsprites.Length || directionsprites[index] == null) return;
        spriteRenderer.sprite = directionsprites[index];
    }

    /// <summary>
    /// 플레이어 쪽으로 한 걸음.
    ///
    /// 걸음 수 지도가 알려 주는 최단 경로를 따른다. 벽을 크게 돌아가야 하는
    /// 지형에서도 제대로 쫓아온다. 지도가 없을 때만 예전처럼 곧장 다가간다.
    /// </summary>
    private void StepTowardPlayer()
    {
        if (GameManager.Chase.IsReady)
        {
            // 길이 다른 생물에게 막혔으면 기다린다. 반대 방향으로 새지 않는다.
            TryMoveAlong(GameManager.Chase.StepsToward(transform.position), rememberPath: false);
            return;
        }
        TryMoveAlong(MoveIntent.Toward(DirectionToward(PlayerPosition)), rememberPath: false);
    }

    private void StepCurious()
    {
        if (curiosity.TryStep(direction =>
            TryMoveAlong(new[] { direction }, rememberPath: false))) return;

        currentMode = EnemyMode.Patrol;
        ResetPatrol(startWandering: false);
        // 이번 턴은 확인으로 끝낸다. 순찰 이동은 다음 턴부터 한다.
    }

    /// <summary>원거리 피해를 줄 때만 발사 당시 위치를 함께 넘긴다. 충돌·함정에는 넘기지 않는다.</summary>
    private void NoticeRangedDamage(int damage, Vector2? attackOrigin)
    {
        if (!attackOrigin.HasValue || GameManager.FloorTilemap == null) return;
        var floor = GameManager.FloorTilemap;
        if (!curiosity.TryStart(currentMode == EnemyMode.Patrol, damage, ThisHp,
            floor.WorldToCell(transform.position), floor.WorldToCell(attackOrigin.Value))) return;
        ResetPatrol(startWandering: false);
        currentMode = EnemyMode.Curious;
    }

    private void StepPatrol()
    {
        // Rigidbody 이동은 다음 물리 갱신에 반영된다. 다음 턴 시작 위치로 도착을 확인한다.
        if (isPatrolTargetSet && HasReachedPatrolTarget())
            isWandering = true;

        if (!isWandering && !isPatrolTargetSet)
        {
            if (PatrolPlan.TryPickTarget(transform.position, roomdata, connectionPoints,
                visited, Random.value, out patrolTarget))
            {
                isPatrolTargetSet = true;
                patrolRoute = RouteTo(patrolTarget);
            }
            else isWandering = true;
        }

        // 이번 턴에 막힌 출구는 기억 초기화로도 다시 뽑지 않는다.
        blockedExits.Clear();
        while (!isWandering)
        {
            Vector2 origin = transform.position;
            if (patrolRoute != null &&
                patrolRoute.TryGetDistance(origin, out int remaining) && remaining > 0 &&
                TryMoveAlong(patrolRoute.StepsToward(origin, allowSideways: false), rememberPath: false))
            {
                if (!visited.Contains(origin)) visited.Add(origin);
                return;
            }

            blockedExits.Add(patrolTarget);
            if (PatrolPlan.TryPickTarget(origin, roomdata, connectionPoints,
                visited, Random.value, out patrolTarget, blockedExits))
            {
                isPatrolTargetSet = true;
                patrolRoute = RouteTo(patrolTarget);
            }
            else
            {
                // 다른 출구도 없거나 모두 막혔다. 방 안에서 비켜 갈 칸을 찾는다.
                ResetPatrol(startWandering: true);
            }
        }

        // 통로에서는 지나온 칸을 피한다. 막다른 곳이면 기억을 비우고 곧바로 되돌아간다.
        if (!TryMoveAlong(Shuffled(CardinalDirections), rememberPath: true))
        {
            visited.Clear();
            TryMoveAlong(Shuffled(CardinalDirections), rememberPath: true);
        }
    }

    /// <summary>
    /// 그 문까지의 지도를 빌려 온다. 같은 문을 고른 다른 적이 이미 만들어 두었으면
    /// 그것을 그대로 받는다(RouteCache 주석).
    /// </summary>
    private static ChaseMap RouteTo(Vector3 target)
        => GameManager.PatrolRoutes.To(new TilemapWalkableGrid(GameManager.FloorTilemap), target);

    private void ResetPatrol(bool startWandering)
    {
        visited.Clear();
        isWandering = startWandering;
        isPatrolTargetSet = false;
    }

    /// <summary>
    /// 후보 방향을 순서대로 시도해서, 처음으로 갈 수 있는 칸으로 옮긴다.
    ///
    /// Chase / Patrol / 배회 / 우회가 전부 이 메서드 하나를 쓴다.
    /// 예전에 네 벌로 복사돼 있던 부분이다.
    /// </summary>
    private bool TryMoveAlong(IEnumerable<Vector2> directions, bool rememberPath)
    {
        Vector2 origin = transform.position;

        foreach (Vector2 direction in directions)
        {
            if (direction == Vector2.zero) continue;
            Vector2 destination = origin + direction;
            if (!CanOccupy(destination, direction, rememberPath)) continue;

            // 갈 수 있는지는 위에서 다 봤으므로 그대로 옮긴다.
            //
            // ── 왜 Move() 를 쓰지 않는가 ──────────────────────────────
            // Move() 는 Linecast 로 한 번 더 막는데, blockingLayer 에 들어 있는
            // 것은 생물(레이어 6)뿐이라 벽·물 판정은 이미 CanOccupy 가 한 것과
            // 겹친다. 남는 것은 "다른 생물이 있는가" 인데, 그 판정이 문제였다.
            //
            // 한 턴의 이동은 모두 같은 프레임에 일어나고 Rigidbody2D 는 다음 물리
            // 갱신에야 좌표를 옮긴다. 그래서 앞선 적이 이미 비우기로 한 칸도 그
            // 프레임에는 콜라이더가 남아 있어 Linecast 에 걸렸다. 뒤따르던 적은
            // 그 칸에 못 들어가고, 도망치는 플레이어를 쫓을 때 한 칸씩 벌어졌다.
            //
            // 이제 "누가 있는가" 는 찜한 칸과 아직 안 움직인 적으로 판단한다
            // (IsEnemyAt · IsPlayerAt). 물리에 묻지 않으므로 프레임 순서를 타지 않는다.
            // 그 칸에 중립 동료가 서 있으면 <b>자리를 맞바꾼다.</b>
            // 플레이어가 하는 것과 같다(Player.TrySwapWithPartner).
            Partner neutral = NeutralPartnerAt(destination);
            if (neutral != null) neutral.SwapTo(origin);

            MoveTo(destination);
            MovedThisTurn = true;

            // 같은 턴의 다른 적이 이 칸을 노리지 않도록 찜해 둔다.
            GameManager.instance.enemyPositions.Add(destination);

            // <b>자리를 바꿨으면 내가 비운 칸도 찜해야 한다.</b> 그 칸에는 방금
            // 동료가 들어섰는데, 동료는 이미 움직인 것으로 표시되어(SwapTo)
            // "아직 안 움직인 것" 을 훑는 검사에서 빠진다. 찜해 두지 않으면
            // 뒤따르는 적이 빈 칸으로 보고 그리로 들어와 또 겹친다.
            // 플레이어 쪽에서 PlayerPosition 을 갱신해야 했던 것과 같은 종류다.
            if (neutral != null) GameManager.instance.enemyPositions.Add(origin);

            if (rememberPath)
            {
                // 출발 칸도 기억해야 바로 뒤로 되돌아가는 왕복을 막는다.
                if (!visited.Contains(origin)) visited.Add(origin);
                if (!visited.Contains(destination)) visited.Add(destination);
                if (FindRoomContaining(destination) != Rect.zero)
                {
                    ResetPatrol(startWandering: false);
                    // 방금 들어온 문보다 다른 문을 먼저 고른다. 외길 방은 다시 나갈 수 있다.
                    visited.Add(origin);
                }
            }

            return true;
        }

        return false;
    }

    private bool CanOccupy(Vector2 destination, Vector2 direction, bool rememberPath)
    {
        if (IsEnemyAt(destination)) return false;
        if (IsPlayerAt(destination)) return false;
        if (IsJoinedPartnerAt(destination)) return false;
        if (IsBlockedByTile(destination)) return false;
        if (IsBlockedByWater(destination)) return false;
        if (!CanMoveDiagonally(transform.position, direction)) return false;
        if (rememberPath && visited.Contains(destination)) return false;
        return true;
    }

    /// <summary>
    /// 그 칸에 선 동료. 없으면 null.
    ///
    /// <b>합류 여부로 대접이 갈린다.</b> 동료는 적의 상대라 밀고 들어가지 않고
    /// 그 앞에서 싸운다(AdjacentPartner). 중립은 싸울 상대가 아니라 길에 서 있는
    /// 사람일 뿐이라, 플레이어처럼 자리를 맞바꾸고 지나간다.
    ///
    /// 이미 움직인 동료도 좌표는 지금 것이므로 그대로 견주면 된다.
    /// </summary>
    private Partner PartnerAt(Vector2 destination, bool joined)
    {
        foreach (Partner partner in GameManager.instance.partners)
        {
            if (partner == null || !partner.gameObject.activeInHierarchy) continue;
            if (partner.Joined != joined) continue;

            if (GridUtil.SameCell(partner.transform.position, destination))
                return partner;
        }
        return null;
    }

    /// <summary>플레이어 편이 된 동료가 서 있는가. 밀고 들어가지 않는다.</summary>
    private bool IsJoinedPartnerAt(Vector2 destination)
        => PartnerAt(destination, joined: true) != null;

    /// <summary>아직 남인 동료. 자리를 맞바꿔 지나간다.</summary>
    private Partner NeutralPartnerAt(Vector2 destination)
        => PartnerAt(destination, joined: false);

    /// <summary>
    /// 가고 싶은 방향을 먼저, 막혔을 때의 우회 방향을 그다음에 내놓는다.
    ///
    /// 예전에는 축 방향일 때의 우회 계산이 잘못돼 있었다.
    /// preferred 가 (1,0) 이면 (-x,y)=(-1,0), (x,-y)=(1,0), (-x,-y)=(-1,0) 이 되어
    /// 사실상 '뒤로' 만 두 번 시도했고, 수직 방향은 한 번도 시도하지 않았다.
    /// 벽 하나만 앞을 막아도 적이 그대로 멈춰 서던 원인이다.
    /// </summary>

    /// <summary>
    /// 네 방향을 섞어 돌려준다. <b>돌려받은 것은 곧바로 다 써야 한다</b> —
    /// 담는 자리가 하나라 다음에 부르면 덮인다.
    /// </summary>
    private Vector2[] Shuffled(Vector2[] source)
    {
        Vector2[] list = shuffledDirections;
        System.Array.Copy(source, list, list.Length);

        for (int i = list.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    /// <summary>
    /// 목표 쪽으로 한 칸 가려면 어느 방향인지.
    /// 축이 맞으면 직선으로, 아니면 대각선으로 접근한다.
    ///
    /// 예전에는 GetDirectionToPlayer 와 GetPatrolDirection 이 목표만 다르고
    /// 내용은 똑같은 채로 따로 있었다.
    /// </summary>
    private Vector2 DirectionToward(Vector2 target)
    {
        Vector2 origin = transform.position;
        float dx = target.x - origin.x;
        float dy = target.y - origin.y;

        // 같은 줄에 있으면 그 축으로만, 아니면 대각선으로 간다.
        if (Mathf.Abs(dy) < float.Epsilon) return new Vector2(Mathf.Sign(dx), 0f);
        if (Mathf.Abs(dx) < float.Epsilon) return new Vector2(0f, Mathf.Sign(dy));

        return new Vector2(Mathf.Sign(dx), Mathf.Sign(dy));
    }

    // ── 순찰 목표 고르기 ────────────────────────────────────────────────────

    /// <remarks>
    /// 칸으로 견준다. 예전에는 거리가 <b>정확히 0</b> 인지를 물었는데
    /// (Mathf.Approximately), 물리가 옮긴 좌표에 오차가 조금이라도 남으면
    /// 문 위에 서 있으면서도 영영 도착하지 못한 것이 된다.
    /// </remarks>
    private bool HasReachedPatrolTarget()
        => GridUtil.SameCell(transform.position, patrolTarget);

    private Rect FindRoomContaining(Vector2 position)
        => PatrolPlan.RoomContaining(roomdata, position);

    // ── 전투 ───────────────────────────────────────────────────────────────

    /// <summary>바로 옆(대각선 포함)에 플레이어가 있고, 실제로 닿을 수 있는가.</summary>
    private bool IsPlayerAdjacent()
    {
        Vector2 origin = transform.position;
        Vector2 playerPosition = PlayerPosition;

        foreach (Vector2 direction in GridUtil.EightDirections)
        {
            Vector2 checkPosition = origin + direction;

            if (!GridUtil.SameCell(playerPosition, checkPosition)) continue;
            if (IsBlockedByTile(checkPosition)) continue;

            bool isDiagonal = Mathf.Abs(direction.x) > 0 && Mathf.Abs(direction.y) > 0;
            if (isDiagonal && !CanMoveDiagonally(origin, direction)) continue;

            return true;
        }

        return false;
    }

    private IEnumerator AttackPlayer()
    {
        currentMode = EnemyMode.Chase;

        if (target == null) yield break;

        // 상대는 바로 옆 칸에 있으므로 그 차이가 곧 찌르는 방향이다.
        Vector2 toTarget = (Vector2)(target.transform.position - transform.position);

        yield return LungeTowards(toTarget);
        StrikeTarget();
        yield return new WaitForFixedUpdate();
    }

    /// <summary>연출 없이 피해만 준다. 빨리감기 중에 쓴다.</summary>
    private void StrikePlayer()
    {
        currentMode = EnemyMode.Chase;
        StrikeTarget();
    }

    /// <summary>
    /// 정해 둔 상대를 때린다.
    ///
    /// 상대를 여기서 다시 고르지 않는다. 고르는 일은 모든 적이 <b>움직이기 전에</b>
    /// 끝나 있어야 하는데(DecideAttack), 때릴 때 다시 고르면 앞선 적이 움직여
    /// 만든 자리에 따라 답이 달라져 목록 순서가 곧 규칙이 된다.
    /// </summary>
    private void StrikeTarget()
    {
        if (target == null) return;

        // 연출 도중에 쓰러졌을 수 있다.
        if (partnerTarget != null)
        {
            if (partnerTarget.gameObject.activeInHierarchy) partnerTarget.Damaged(attackPower);
            return;
        }

        var player = target.GetComponent<Player>();
        if (player != null) player.Damaged(attackPower, transform.position, DealsCritical);
    }

    /// <summary>
    /// 이 칸에 다른 적이 있는가.
    ///
    /// 두 가지를 함께 본다 —
    ///   이번 턴에 다른 적이 <b>찜해 둔</b> 칸(enemyPositions)
    ///   지금 실제로 <b>서 있는</b> 적
    ///
    /// 예전에는 찜한 칸만 봤다. 그런데 때리는 적은 움직이지 않아 아무것도 찜하지
    /// 않으므로, 뒤의 적이 그 칸을 고르고 물리에 막혀 제자리에 서 버렸다.
    ///
    /// 서 있는 자리까지 보는 이유가 하나 더 있다. 한 턴의 이동은 모두 같은
    /// 프레임에 일어나는데(MoveEnemies), Rigidbody2D 는 다음 물리 갱신에야
    /// 좌표를 옮긴다. 그래서 그 프레임 동안에는 <b>이미 움직인 적도 아직 옛
    /// 자리에 서 있고</b>, 그 칸으로 들어가려는 시도는 어차피 물리에 막힌다.
    /// 미리 걸러서 다음 방향을 시도하게 하는 편이 낫다.
    /// </summary>
    private bool IsEnemyAt(Vector2 position)
    {
        foreach (Vector2 reserved in GameManager.instance.enemyPositions)
        {
            if (GridUtil.SameCell(position, reserved))
                return true;
        }

        foreach (Enemy other in GameManager.instance.enemies)
        {
            if (other == null || other == this) continue;
            if (!other.gameObject.activeInHierarchy) continue;

            // 이미 옮긴 적은 찜한 칸으로 표현된다. 옛 자리까지 막으면
            // 뒤따르는 적이 그 칸을 이어받지 못해 한 칸씩 벌어진다.
            if (other.MovedThisTurn) continue;

            if (GridUtil.SameCell(position, other.transform.position))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 그 칸에 플레이어가 있는가.
    ///
    /// 예전에는 Move() 의 Linecast 가 함께 걸러 주었다. 그것을 쓰지 않게 되면서
    /// 여기서 직접 본다 — 빠뜨리면 적이 플레이어를 밟고 지나간다.
    /// </summary>
    private bool IsPlayerAt(Vector2 position)
        => GridUtil.SameCell(position, PlayerPosition);

    public override void SetStats(int hp, int attack, int defenseValue)
    {
        ThisHp = hp;
        maxHp = hp;
        attackPower = attack;
        defense = defenseValue;
    }

    // ── 원거리로 날아온 것에 맞았을 때 ──────────────────────────────────────

    /// <summary>
    /// 회피도 치명타도 없이 그대로 들어오는 피해. 던진 물건이나 원거리 공격용.
    /// 방어력만큼은 줄어들되 최소 1은 들어간다.
    /// </summary>
    public void TakeDirectDamage(int amount, Vector2? attackOrigin = null, bool fromEnemy = false)
    {
        int damage = Mathf.Max(amount - defense, 1);
        if (attackOrigin.HasValue) NotifyRangedHit(fromEnemy);
        ThisHp -= damage;
        WakeOnDamage(damage);
        NoticeRangedDamage(damage, attackOrigin);

        GameManager.instance.ShowMessage(GameMessage.ThrownItemHitEnemy, DisplayName, damage);
        CheckDead();
    }

    /// <summary>
    /// 방어력을 무시하고 그대로 들어가는 피해.
    ///
    /// TakeDirectDamage 는 이름과 달리 방어력을 빼고 있어서(amount - defense),
    /// "방어력 무시" 가 필요한 곳에는 쓸 수 없다. 돈을 던져 맞히는 것이 그 경우다.
    /// </summary>
    public void TakeTrueDamage(int amount, Vector2? attackOrigin = null, bool fromEnemy = false)
    {
        int damage = Mathf.Max(amount, 0);
        if (damage <= 0) return;

        if (attackOrigin.HasValue) NotifyRangedHit(fromEnemy);
        ThisHp -= damage;
        WakeOnDamage(damage);
        NoticeRangedDamage(damage, attackOrigin);

        GameManager.instance.ShowMessage(GameMessage.ThrownItemHitEnemy, DisplayName, damage);
        CheckDead();
    }

    /// <summary>
    /// 동료에게 맞았다. 방어력을 빼고 들어간다.
    ///
    /// <b>쓰러뜨려도 플레이어는 경험치를 받지 않는다.</b> 동료가 대신 잡아 주는데
    /// 경험치까지 오면 데리고 다니는 것이 무조건 이득이 되어 고를 이유가 사라진다.
    /// 시리즈에서도 동료의 대가는 그것이다.
    /// </summary>
    public void TakeDamageFromPartner(int attack, Partner attacker, Vector2? rangedOrigin = null)
    {
        if (attacker == null || (!IsCombatTarget && !rangedOrigin.HasValue)) return;
        if (rangedOrigin.HasValue) NotifyRangedHit();

        int damage = Mathf.Max(1, attack - defense);
        ThisHp -= damage;
        WakeOnDamage(damage);
        NoticeRangedDamage(damage, rangedOrigin);

        GameManager.instance.ShowMessage(
            GameMessage.PartnerHitEnemy, attacker.DisplayName, damage);

        // 싸우는 소리. 플레이어를 놓친 동료가 이걸 듣고 찾아온다.
        GameManager.ReportNoise(transform.position);

        if (ThisHp > 0 || !RecordDefeat()) return;

        // 경험치는 때린 동료에게만 간다. 플레이어는 한 점도 받지 않는다.
        int gained = GameManager.Party.AddExp(attacker.Id, ExpReward);

        GameManager.instance.ShowMessage(
            GameMessage.PartnerDefeatedEnemy, attacker.DisplayName, ExpReward, DisplayName);

        if (gained > 0)
            GameManager.instance.ShowMessage(GameMessage.PartnerLeveledUp, attacker.DisplayName);

        LeaveFloor();
    }

    /// <summary>
    /// 쓰러진 뒤 층에서 물러난다. <b>쓰러지는 길이 어느 쪽이든 여기로 온다.</b>
    ///
    /// 예전에는 플레이어가 잡으면 꺼 두기만 하고(SetActive) 동료가 잡으면 지웠다.
    /// 꺼 둔 쪽은 층이 끝날 때까지 씬에 남아 쌓였고, 두 길의 뒷정리가 달라서
    /// 한쪽만 고치면 어긋날 자리였다.
    ///
    /// 끄는 것과 지우는 것을 함께 한다. Destroy 는 이 프레임이 끝나야 실제로
    /// 지워지므로, 그 사이에 목록을 도는 쪽(턴 흐름 · 조준 · 미니맵)이 "꺼진 것은
    /// 건너뛴다" 는 지금의 검사로 그대로 걸러 낼 수 있게 먼저 꺼 둔다.
    /// </summary>
    private void LeaveFloor()
    {
        GameManager.instance.RemoveEnemyList(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    /// <summary>회복 아이템을 맞았다. 적을 치료해 버린다.</summary>
    public void HealBy(int amount)
    {
        ThisHp = Mathf.Min(ThisHp + amount, maxHp);
        GameManager.instance.ShowMessage(GameMessage.ThrownItemHealedEnemy, DisplayName);
    }

    /// <summary>능력치 상승 아이템을 맞았다. 적이 강해진다.</summary>
    public void RaiseAttackBy(int amount)
    {
        attackPower += amount;
        GameManager.instance.ShowMessage(GameMessage.ThrownItemBuffedEnemy, DisplayName);
    }

    /// <summary>메시지에 쓰는 이름. 프리팹 복제본 표시를 뗀 것.</summary>
    /// <summary>
    /// 이 적이 어느 종류인가. 이름·수치·경험치가 전부 여기서 나온다.
    ///
    /// 예전에는 GameObject 이름이 곧 신원이었고 수치는 DungeonPopulator 에
    /// 박혀 있었다. 그래서 Monster 시트에 hp/atk/def/exp 를 적어 두어도
    /// 아무 데도 쓰이지 않았다.
    /// </summary>
    public MonsterDescription description;

    /// <summary>
    /// 화면에 보이는 이름.
    ///
    /// 정의가 아직 연결되지 않은 프리팹이 있을 수 있어 GameObject 이름으로
    /// 물러선다. 전부 연결하고 나면 이 뒷길은 지워도 된다.
    /// </summary>
    public string DisplayName => Merchant != null ? "상인" : description != null
        ? description.DisplayName
        : gameObject.name.Replace("(Clone)", string.Empty);

    /// <summary>
    /// 이 적이 치명타를 내는가. 정의가 없으면 내지 않는다.
    ///
    /// 기본이 '안 낸다' 인 것이 중요하다. 모든 적이 치명타를 내면 맞는 쪽에서는
    /// 피해 폭이 넓은 것으로만 보이고, "저 녀석은 조심해야 한다" 가 성립하지 않는다.
    /// 얼마나 자주 나는지는 Player 가 정한다(IncomingCriticalPercent).
    /// </summary>
    public bool DealsCritical
    {
        get
        {
            MonsterDefinition definition = description != null ? description.Definition : null;
            return definition != null && definition.DealsCritical;
        }
    }

    /// <summary>
    /// 쓰러뜨렸을 때 주는 경험치.
    ///
    /// 시트에 없으면 옛 방식(공격력 + 방어력)으로 돌아간다. 그때는 수치가
    /// 곧 경험치였는데, 그러면 "약하지만 경험치가 짭짤한 적" 같은 것을
    /// 만들 수 없어서 시트가 따로 정하게 했다.
    /// </summary>
    public int ExpReward
    {
        get
        {
            MonsterDefinition definition = description != null ? description.Definition : null;
            return definition != null ? definition.Exp : attackPower + defense;
        }
    }

    // ── 정체를 숨기고 있는가 ───────────────────────────────────────────────
    //
    // 아직 이렇게 되는 적은 없다. 조준이 "보이는 적만 겨눈다" 는 규칙을 지키려면
    // 먼저 '보이지 않는 상태' 라는 개념이 있어야 해서 자리를 만들어 두었다.
    // 변장한 적과 투명한 적을 실제로 만들 때 이 값을 켜면 조준은 저절로 비켜 간다.

    /// <summary>아이템인 척하고 있는가. 밟거나 때리기 전까지는 적인 줄 모른다.</summary>
    public bool IsDisguised { get; set; }

    /// <summary>모습이 보이지 않는가.</summary>
    public bool IsInvisible { get; set; }

    /// <summary>
    /// 조준으로 겨눌 수 있는가.
    ///
    /// 정체를 숨긴 적을 조준이 집어 주면 그것만으로 정체가 드러난다.
    /// 화살표가 아이템 쪽으로 홱 도는 순간 "저건 적이구나" 를 알게 되기 때문이다.
    /// 숨는 적을 두는 의미가 사라지므로 조준 대상에서 뺀다.
    /// </summary>
    public bool CanBeAimedAt => IsCombatTarget && !IsDisguised && !IsInvisible;

    /// <summary>플레이어에게 맞았을 때.</summary>
    public void Damaged(int playerAttack)
    {
        // 중립인 상인을 때리면 공격이 아니라 말을 건 것이 된다. 상인은 처음에
        // 중립 상태의 적이라, 돌아서게 하는 길은 훔치기와 멀리서 맞히기뿐이다.
        if (!IsCombatTarget)
        {
            GameManager.instance.StartCoroutine(Merchant.Shop.Talk());
            return;
        }

        if (Random.value >= HitChance)
        {
            // 연달아 빗나간 횟수를 센다. 맞히면 아래에서 끊긴다.
            GameManager.ReportMiss();
            GameManager.instance.ShowMessage(GameMessage.AttackMissed);
            return;
        }

        GameManager.ReportHit();

        int randomFactor = Random.Range(-1, 3);
        int damage = Mathf.Max((playerAttack - defense) + randomFactor, 1);

        bool isCritical = Random.value < CriticalChance;
        if (isCritical)
            damage = (int)System.Math.Round(damage * CriticalMultiplier);

        ThisHp -= damage;
        WakeOnDamage(damage);
        GameManager.ReportRecord(RecordStat.BiggestHitDealt, damage);

        GameManager.instance.ShowMessage(
            isCritical ? GameMessage.CriticalHit : GameMessage.EnemyDamaged,
            amount: damage);

        // 예전에는 치명타일 때 여기서 곧바로 return 해 버려서
        // 치명타로 적을 죽여도 사망 처리와 경험치 지급이 되지 않았다.
        CheckDead();
    }

    private void CheckDead()
    {
        if (ThisHp > 0) return;

        gameObject.name = gameObject.name.Replace("(Clone)", string.Empty);

        if (!RecordDefeat()) return;

        GameManager.instance.RewardForKill(DisplayName, ExpReward);

        LeaveFloor();
    }

    /// <summary>
    /// 조회에 쓰는 번호. 정의가 없으면 0 이며, 어떤 표에도 걸리지 않는다.
    /// (ItemInstance.IdOf 와 같은 규칙이다.)
    /// </summary>
    public int MonsterId => description != null ? description.id : 0;

    /// <summary>
    /// 도감에 한 마리 적는다.
    ///
    /// <b>쓰러지는 길이 둘</b>이라 양쪽에서 부른다 — 이쪽(CheckDead)은 플레이어가
    /// 때렸거나 던진 것에 맞은 경우고, 동료가 잡은 경우는 TakeDamageFromPartner 가
    /// 목록에서 빼고 오브젝트를 바로 지워 버려서 이곳을 지나지 않는다.
    /// </summary>
    private bool RecordDefeat()
    {
        ItemDropRate rate = description != null ? description.Rate : ItemDropRate.Medium;
        if (!drops.TryResolveDeath(ThisHp, rate, Random.value, out bool drop)) return false;
        GameManager.RecordEnemyDefeat(MonsterId);
        GameManager.ReportRecord(RecordStat.EnemiesDefeated);
        // 상인이 쓰러지면 가게의 물건은 주인이 없어진다. 상인은 물건을 떨구지 않는다.
        if (Merchant != null) Merchant.Shop.ReleaseStock();
        if (drop && Merchant == null && GameManager.instance.MapMake != null)
            GameManager.instance.MapMake.DropEnemyItem(transform.position);
        return true;
    }
}
