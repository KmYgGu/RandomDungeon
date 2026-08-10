using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 격자 위를 한 칸씩 움직이는 것들(플레이어 / 적)의 공통 부분.
///
/// 통과 가능 여부는 더 이상 여기서 직접 판정하지 않고 TileRules 에 묻는다.
/// 예전에는 같은 내용의 IsBlockedByTile 이 이 클래스와 GameManager 에
/// 따로 있었고, 막는 타일 이름 목록도 Start() 에서 다시 덮어써서
/// Inspector 에 넣은 값이 무시되고 있었다.
/// </summary>
public abstract class MovingObject : MonoBehaviour
{
    public float moveTime = .1f;
    public LayerMask blockingLayer;

    /// <summary>물 위를 지나갈 수 있는 특수 상태인가.</summary>
    public bool isSpecialState;

    /// <summary>여덟 방향 스프라이트. directionMap 의 두 번째 값이 이 배열의 인덱스다.</summary>
    public Sprite[] directionsprites;

    public Transform childTransform;
    public SpriteRenderer spriteRenderer;

    protected Tilemap tilemap;
    protected Rigidbody2D rb2D;
    protected float inverseMoveTime;

    private BoxCollider2D boxCollider;

    /// <summary>
    /// 스프라이트가 평소에 있어야 할 자리(본체 기준). 공격 모션이 여기로 돌아온다.
    /// 0 으로 가정하지 않는 이유는 스프라이트가 처음부터 조금 어긋나 있을 수 있어서다.
    /// </summary>
    protected Vector3 spriteHomePosition;

    /// <summary>
    /// 스프라이트가 평소에 가져야 할 크기. 구멍에 빠지는 연출이 여기로 돌아온다.
    /// 위치와 같은 이유로 1 이라고 가정하지 않는다.
    /// </summary>
    protected Vector3 spriteHomeScale;

    protected virtual void Start()
    {
        childTransform = transform.Find("Sprite");
        spriteRenderer = childTransform.GetComponent<SpriteRenderer>();
        spriteHomePosition = childTransform.localPosition;
        spriteHomeScale = childTransform.localScale;

        tilemap = GameObject.Find("FloorMap").GetComponent<Tilemap>();
        boxCollider = GetComponent<BoxCollider2D>();
        rb2D = GetComponent<Rigidbody2D>();
        inverseMoveTime = 1f / moveTime;
    }

    /// <summary>바라보는 방향 → (화살표 회전각, 스프라이트 인덱스).</summary>
    private static readonly Dictionary<Vector2, (float rotation, int spriteIndex)> directionMap =
        new Dictionary<Vector2, (float, int)>
        {
            { new Vector2(0, 0), (0f, 0) },      // 입력 없음
            { new Vector2(0, -1), (0f, 0) },     // 아래
            { new Vector2(0, 1), (180f, 1) },    // 위
            { new Vector2(-1, 0), (-90f, 2) },   // 왼쪽
            { new Vector2(1, 0), (90f, 3) },     // 오른쪽
            { new Vector2(-1, -1), (-45f, 4) },  // 왼쪽 아래
            { new Vector2(1, -1), (45f, 5) },    // 오른쪽 아래
            { new Vector2(-1, 1), (-135f, 6) },  // 왼쪽 위
            { new Vector2(1, 1), (135f, 7) },    // 오른쪽 위
        };

    protected static (float rotation, int spriteIndex) GetDirectionInfo(Vector2 lookDirection)
    {
        return directionMap.TryGetValue(lookDirection, out var info) ? info : (0f, 0);
    }

    /// <summary>
    /// 한 칸 이동을 시도한다. 실제로 움직였으면 true.
    /// 무언가에 부딪혔다면 objectHit 에 그 대상이 담긴다.
    /// </summary>
    protected bool Move(Vector2 direction, out RaycastHit2D objectHit)
    {
        Vector2 start = transform.position;
        Vector2 end = start + direction;

        // 자기 자신의 콜라이더에 걸리지 않도록 잠깐 꺼 두고 검사한다.
        boxCollider.enabled = false;
        objectHit = Physics2D.Linecast(start, end, blockingLayer);
        boxCollider.enabled = true;

        bool isDiagonal = Mathf.Abs(direction.x) > 0 && Mathf.Abs(direction.y) > 0;
        if (isDiagonal && !TileRules.CanMoveDiagonally(tilemap, start, direction))
            return false;

        if (!TileRules.CanEnter(tilemap, end, isSpecialState))
            return false;

        if (objectHit.transform != null)
            return false;

        MoveTo(end);
        return true;
    }

    /// <summary>
    /// 실제로 좌표를 옮긴다. <b>아무 검사도 하지 않으므로</b> 부르는 쪽이 먼저 확인해야 한다.
    ///
    /// 평소에는 Rigidbody2D.MovePosition 을 쓰지만, 빨리감기 중에는 쓸 수 없다.
    /// MovePosition 은 다음 물리 갱신에야 반영되므로, 같은 프레임에 여러 걸음을
    /// 내딛으면 두 번째 걸음이 아직 옛 좌표에서 출발해 제자리걸음이 된다.
    /// 그래서 빨리감기 중에는 좌표를 즉시 덮어쓴다.
    ///
    /// 적은 Move() 대신 이것을 직접 쓴다 — 이유는 Enemy.TryMoveAlong 참고.
    /// </summary>
    protected void MoveTo(Vector2 end)
    {
        if (GameManager.FastForward)
        {
            rb2D.position = end;
            transform.position = end;
            return;
        }

        rb2D.MovePosition(end);
    }

    /// <summary>해당 위치가 벽인가.</summary>
    protected bool IsBlockedByTile(Vector3 position) => TileRules.IsImpassable(tilemap, position);

    /// <summary>해당 위치가 물인가.</summary>
    protected bool IsBlockedByWater(Vector3 position) => TileRules.IsWater(tilemap, position);

    /// <summary>대각선으로 빠져나갈 수 있는가.</summary>
    protected bool CanMoveDiagonally(Vector2 start, Vector2 direction)
        => TileRules.CanMoveDiagonally(tilemap, start, direction);

    /// <summary>찌를 때 앞으로 나가는 속도 배율.</summary>
    private const float LungeSpeed = 5f;

    /// <summary>제자리로 돌아오는 속도 배율.</summary>
    private const float LungeReturnSpeed = 2f;

    /// <summary>앞으로 나간 채 머무는 시간.</summary>
    private const float LungeHoldSeconds = 0.2f;

    /// <summary>
    /// 공격 모션. 자식 스프라이트만 앞으로 찔렀다가 제자리로 돌아온다.
    ///
    /// 반드시 로컬 좌표로 움직여야 한다.
    /// 예전에는 월드 좌표를 기준으로 삼아서, 애니메이션 도중에 본체가 한 칸이라도
    /// 움직이면 "돌아갈 곳" 이 이미 지나간 자리를 가리켰다. 그러면 스프라이트가
    /// 본체에서 영구히 어긋난 채로 남아, 캐릭터가 엉뚱한 자리에 서 있는 것처럼 보였다.
    /// 로컬 좌표를 쓰면 본체가 어디로 가든 스프라이트는 늘 제자리로 돌아온다.
    /// </summary>
    protected IEnumerator LungeTowards(Vector2 direction)
    {
        // 빨리감기 중에는 연출을 통째로 건너뛴다. 피해 계산은 부르는 쪽에서 따로 한다.
        if (GameManager.FastForward)
        {
            SnapSpriteHome();
            yield break;
        }

        // 이전 모션이 도중에 끊겼을 수 있으므로 확실히 제자리에서 시작한다.
        SnapSpriteHome();

        string originalSortingLayer = spriteRenderer.sortingLayerName;
        spriteRenderer.sortingLayerName = "attack";

        // 로컬 좌표는 부모 스케일이 곱해져 적용되므로, 월드 기준 한 칸을
        // 그대로 넣으면 안 된다. 적 프리팹은 루트 스케일이 4라서
        // 로컬 1 이 월드 4칸이 되어 엉뚱한 곳까지 날아갔다.
        Vector3 localOffset = WorldOffsetToLocal(direction);

        // 거리를 줄인 만큼 속도도 같은 비율로 줄여야 화면에서 같은 속도로 보인다.
        float speedScale = direction.magnitude > Mathf.Epsilon
            ? localOffset.magnitude / direction.magnitude
            : 1f;

        yield return MoveSpriteTo(spriteHomePosition + localOffset, LungeSpeed * speedScale);
        yield return new WaitForSeconds(LungeHoldSeconds);
        yield return MoveSpriteTo(spriteHomePosition, LungeReturnSpeed * speedScale);

        SnapSpriteHome();
        spriteRenderer.sortingLayerName = originalSortingLayer;
    }

    /// <summary>
    /// 월드 기준 오프셋을 자식 스프라이트의 로컬 오프셋으로 바꾼다.
    ///
    /// 자식의 localPosition 에는 부모의 최종 스케일이 곱해지므로,
    /// "한 칸 앞" 을 그리려면 그 스케일로 나눠 두어야 한다.
    /// </summary>
    private Vector3 WorldOffsetToLocal(Vector2 worldOffset)
    {
        Vector3 scale = transform.lossyScale;

        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 0f : worldOffset.x / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 0f : worldOffset.y / scale.y,
            0f);
    }

    /// <summary>스프라이트를 본체 기준 제자리로 되돌린다.</summary>
    protected void SnapSpriteHome()
    {
        if (childTransform != null)
            childTransform.localPosition = spriteHomePosition;
    }

    private IEnumerator MoveSpriteTo(Vector3 localDestination, float speedMultiplier)
    {
        while ((childTransform.localPosition - localDestination).sqrMagnitude > float.Epsilon)
        {
            childTransform.localPosition = Vector3.MoveTowards(
                childTransform.localPosition,
                localDestination,
                inverseMoveTime * Time.deltaTime * speedMultiplier);
            yield return null;
        }
    }

    public virtual void SetStats(int hp, int attack, int defense)
    {
    }
}
