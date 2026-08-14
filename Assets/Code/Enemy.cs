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
/// 한 턴은 바깥(GameManager.MoveEnemies)이 세 단계로 나누어 돌린다.
///   DecideAttack() - 무엇을 할지 정한다. 모든 적이 먼저 정한 뒤에야 움직인다.
///   TakeStep()     - 이동. 기다림이 없어 마릿수와 무관하게 같은 프레임에 끝난다.
///   AttackTurn()   - 공격. 연출이 있어 한 마리씩 차례로 돈다.
/// </summary>
public class Enemy : MovingObject
{
    public enum EnemyMode
    {
        /// <summary>플레이어를 쫓는다.</summary>
        Chase,
        /// <summary>통로 쪽 출구를 향해 돌아다닌다.</summary>
        Patrol,
    }

    /// <summary>
    /// 이 걸음 수보다 멀어지면 추격을 포기한다.
    /// 직선 거리가 아니라 실제로 걸어가야 하는 걸음 수다.
    /// </summary>
    private const int ChaseGiveUpDistance = 20;

    /// <summary>같은 칸으로 볼 거리.</summary>
    private const float SameCellThreshold = 0.1f;

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
            if (GameManager.Chase.IsReady
                && GameManager.Chase.TryGetDistance(transform.position, out int steps))
                return steps;

            return Vector2.Distance(transform.position, PlayerPosition);
        }
    }

    protected override void Start()
    {
        var dungeon = FindObjectOfType<RandomDung>();
        connectionPoints = dungeon.edgePoints;
        roomdata = dungeon.RoomData;

        GameManager.instance.AddEnemyToList(this);

        // base.Start() 가 자식에서 SpriteRenderer 를 찾아 두므로 그 뒤에 입힌다.
        base.Start();

        if (description != null) SpriteApplier.Apply(gameObject, description.sprite);
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
        UpdateMode();

        target = PickTarget();
        return target != null;
    }

    /// <summary>이번 턴에 때릴 상대. 플레이어면 partnerTarget 이 null 이다.</summary>
    private GameObject target;
    private Partner partnerTarget;

    /// <summary>
    /// 붙어 있는 것들 중 누구를 때릴지 고른다. 아무도 없으면 null.
    ///
    /// ── 지금은 반반이다 ───────────────────────────────────────────────
    /// 둘 다 붙어 있으면 <b>절반의 확률</b>로 고른다. 원래는 적 성향이 갈라야 할
    /// 자리다 — 여성을 노리는 적, 공격력이 높은 쪽을 노리는 적, 물건을 많이 든
    /// 쪽을 노리는 적처럼. 그런 성향은 시트에 열을 하나 두고 코드가 아는 목록을
    /// 고르게 하면 된다(EquipAbility 와 같은 방식). 지금은 그 목록이 없으므로
    /// 치우치지 않는 반반으로 둔다.
    ///
    /// 반반이 중요한 이유가 있다. 늘 플레이어만 때리면 동료는 맞지 않는 벽이 되어
    /// 데리고 다니는 것이 무조건 이득이 되고, 늘 동료만 때리면 동료가 순식간에
    /// 녹아 데리고 다닐 수가 없다.
    /// </summary>
    private GameObject PickTarget()
    {
        partnerTarget = null;

        bool playerAdjacent = IsPlayerAdjacent();
        Partner partner = AdjacentPartner();

        if (playerAdjacent && partner != null)
        {
            if (Random.value < 0.5f)
            {
                partnerTarget = partner;
                return partner.gameObject;
            }
            return GameManager.instance.Player;
        }

        if (playerAdjacent) return GameManager.instance.Player;

        if (partner != null)
        {
            partnerTarget = partner;
            return partner.gameObject;
        }

        return null;
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
        if (currentMode == EnemyMode.Chase)
            StepTowardPlayer();
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
        // 빨리감기 중에는 연출을 건너뛰고 피해만 준다.
        if (GameManager.FastForward)
        {
            StrikePlayer();
            yield break;
        }

        yield return AttackPlayer();
    }

    /// <summary>플레이어가 보이는지에 따라 Chase / Patrol 을 오간다.</summary>
    private void UpdateMode()
    {
        Vector2 playerPosition = PlayerPosition;

        // 같은 방 안에 있으면 발견한 것으로 본다.
        Rect currentRoom = FindRoomContaining(transform.position);
        if (currentRoom != Rect.zero && currentRoom.Contains(playerPosition))
        {
            currentMode = EnemyMode.Chase;
            return;
        }

        if (currentMode != EnemyMode.Chase) return;

        // 너무 멀어지거나 아예 갈 수 없게 되면 포기하고 다시 순찰한다.
        //
        // 직선 거리가 아니라 '실제로 몇 걸음인지'로 판단한다. 벽 하나를 사이에 두고
        // 직선으로는 두 칸이지만 돌아가려면 서른 걸음인 경우가 흔한데,
        // 직선 거리로 재면 그런 상대를 영영 포기하지 않고 벽만 밀게 된다.
        if (GameManager.Chase.IsReady)
        {
            bool reachable = GameManager.Chase.TryGetDistance(transform.position, out int steps);
            if (!reachable || steps > ChaseGiveUpDistance)
            {
                currentMode = EnemyMode.Patrol;
                isPatrolTargetSet = false;
            }
            return;
        }

        if (Vector2.Distance(playerPosition, transform.position) > ChaseGiveUpDistance)
        {
            currentMode = EnemyMode.Patrol;
            isPatrolTargetSet = false;
        }
    }

    /// <summary>
    /// 플레이어 쪽으로 한 걸음.
    ///
    /// 걸음 수 지도가 알려 주는 최단 경로를 따른다. 벽을 크게 돌아가야 하는
    /// 지형에서도 제대로 쫓아온다. 지도가 없을 때만 예전처럼 곧장 다가간다.
    /// </summary>
    private void StepTowardPlayer()
    {
        if (GameManager.Chase.IsReady &&
            TryMoveAlong(GameManager.Chase.StepsToward(transform.position), rememberPath: false))
            return;

        // 지도를 못 쓰는 상황(플레이어가 물 건너편 등)의 최후 수단.
        TryMoveAlong(MoveIntent.Toward(DirectionToward(PlayerPosition)), rememberPath: false);
    }

    private void StepPatrol()
    {
        if (!isPatrolTargetSet)
        {
            patrolTarget = PickPatrolTarget();
            isPatrolTargetSet = true;
        }

        // 목표에 이미 도착했으면 무작위로 헤맨다.
        if (isWandering)
        {
            if (TryMoveAlong(Shuffled(CardinalDirections), rememberPath: true))
            {
                // 방 안으로 들어왔으면 새 목표를 다시 고른다.
                if (FindRoomContaining(transform.position) != Rect.zero)
                    ResetPatrol(startWandering: false);
            }
            else
            {
                ResetPatrol(startWandering: true);
            }
            return;
        }

        Vector2 preferred = DirectionToward(patrolTarget);
        if (!TryMoveAlong(MoveIntent.Toward(preferred), rememberPath: true))
        {
            // 어느 쪽으로도 못 갔다. 기억을 지우고 헤매기로 전환한다.
            ResetPatrol(startWandering: true);
            return;
        }

        if (HasReachedPatrolTarget())
            isWandering = true;
    }

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
            MoveTo(destination);
            MovedThisTurn = true;

            // 같은 턴의 다른 적이 이 칸을 노리지 않도록 찜해 둔다.
            GameManager.instance.enemyPositions.Add(destination);
            if (rememberPath) visited.Add(destination);

            return true;
        }

        return false;
    }

    private bool CanOccupy(Vector2 destination, Vector2 direction, bool rememberPath)
    {
        if (IsEnemyAt(destination)) return false;
        if (IsPlayerAt(destination)) return false;
        if (IsBlockedByTile(destination)) return false;
        if (IsBlockedByWater(destination)) return false;
        if (!CanMoveDiagonally(transform.position, direction)) return false;
        if (rememberPath && visited.Contains(destination)) return false;
        return true;
    }

    /// <summary>
    /// 가고 싶은 방향을 먼저, 막혔을 때의 우회 방향을 그다음에 내놓는다.
    ///
    /// 예전에는 축 방향일 때의 우회 계산이 잘못돼 있었다.
    /// preferred 가 (1,0) 이면 (-x,y)=(-1,0), (x,-y)=(1,0), (-x,-y)=(-1,0) 이 되어
    /// 사실상 '뒤로' 만 두 번 시도했고, 수직 방향은 한 번도 시도하지 않았다.
    /// 벽 하나만 앞을 막아도 적이 그대로 멈춰 서던 원인이다.
    /// </summary>

    private static IEnumerable<Vector2> Shuffled(Vector2[] source)
    {
        var list = new List<Vector2>(source);
        for (int i = list.Count - 1; i > 0; i--)
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

    /// <summary>
    /// 지금 있는 방에서 나갈 수 있는 통로 지점 중 하나를 무작위로 고른다.
    /// 이미 지나온 곳만 남았으면 기억을 지우고 다시 고른다.
    /// </summary>
    /// <summary>
    /// 다음에 가 볼 문. 고를 수 없으면 제자리를 돌려준다(다음 턴에 다시 시도).
    ///
    /// 고르는 규칙은 <b>동료와 함께 쓰는 PatrolPlan</b> 이 갖고 있다. 예전에는
    /// 여기 있었고 동료 쪽에 같은 것을 따로 만들었더니, 한쪽만 고쳤을 때
    /// 적과 동료가 다르게 돌아다니게 됐다.
    /// </summary>
    private Vector3 PickPatrolTarget()
    {
        return PatrolPlan.TryPickTarget(
            transform.position, roomdata, connectionPoints, visited,
            Random.value, out Vector3 target)
            ? target
            : transform.position;
    }



    private bool HasReachedPatrolTarget()
    {
        return Mathf.Approximately(
            Vector2.Distance(transform.position, patrolTarget), 0f);
    }

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

            if (Vector2.Distance(playerPosition, checkPosition) >= SameCellThreshold) continue;
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
            if (Vector2.Distance(position, reserved) < SameCellThreshold)
                return true;
        }

        foreach (Enemy other in GameManager.instance.enemies)
        {
            if (other == null || other == this) continue;
            if (!other.gameObject.activeInHierarchy) continue;

            // 이미 옮긴 적은 찜한 칸으로 표현된다. 옛 자리까지 막으면
            // 뒤따르는 적이 그 칸을 이어받지 못해 한 칸씩 벌어진다.
            if (other.MovedThisTurn) continue;

            if (Vector2.Distance(position, other.transform.position) < SameCellThreshold)
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
        => Vector2.Distance(position, PlayerPosition) < SameCellThreshold;

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
    public void TakeDirectDamage(int amount)
    {
        int damage = Mathf.Max(amount - defense, 1);
        ThisHp -= damage;

        GameManager.instance.ShowMessage(GameMessage.ThrownItemHitEnemy, DisplayName, damage);
        CheckDead();
    }

    /// <summary>
    /// 방어력을 무시하고 그대로 들어가는 피해.
    ///
    /// TakeDirectDamage 는 이름과 달리 방어력을 빼고 있어서(amount - defense),
    /// "방어력 무시" 가 필요한 곳에는 쓸 수 없다. 돈을 던져 맞히는 것이 그 경우다.
    /// </summary>
    public void TakeTrueDamage(int amount)
    {
        int damage = Mathf.Max(amount, 0);
        if (damage <= 0) return;

        ThisHp -= damage;

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
    public void TakeDamageFromPartner(int attack, Partner attacker)
    {
        if (attacker == null) return;

        int damage = Mathf.Max(1, attack - defense);
        ThisHp -= damage;

        GameManager.instance.ShowMessage(
            GameMessage.PartnerHitEnemy, attacker.DisplayName, damage);

        // 싸우는 소리. 플레이어를 놓친 동료가 이걸 듣고 찾아온다.
        GameManager.ReportNoise(transform.position);

        if (ThisHp > 0) return;

        // 경험치는 때린 동료에게만 간다. 플레이어는 한 점도 받지 않는다.
        int gained = GameManager.Party.AddExp(attacker.Id, ExpReward);

        GameManager.instance.ShowMessage(
            GameMessage.PartnerDefeatedEnemy, attacker.DisplayName, ExpReward, DisplayName);

        if (gained > 0)
            GameManager.instance.ShowMessage(GameMessage.PartnerLeveledUp, attacker.DisplayName);

        GameManager.instance.RemoveEnemyList(this);
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
    public string DisplayName => description != null
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
    public bool CanBeAimedAt => !IsDisguised && !IsInvisible;

    /// <summary>플레이어에게 맞았을 때.</summary>
    public void Damaged(int playerAttack)
    {
        if (Random.value >= HitChance)
        {
            GameManager.instance.ShowMessage(GameMessage.AttackMissed);
            return;
        }

        int randomFactor = Random.Range(-1, 3);
        int damage = Mathf.Max((playerAttack - defense) + randomFactor, 1);

        bool isCritical = Random.value < CriticalChance;
        if (isCritical)
            damage = (int)System.Math.Round(damage * CriticalMultiplier);

        ThisHp -= damage;

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

        GameManager.instance.RemoveEnemyList(this);
        GameManager.instance.RewardForKill(DisplayName, ExpReward);

        gameObject.SetActive(false);
    }
}
