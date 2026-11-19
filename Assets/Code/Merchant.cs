using UnityEngine;

/// <summary>
/// 상점 주인. <b>처음에는 중립 상태의 적이다</b> — 몸은 Enemy 를 그대로 쓰고,
/// 이 컴포넌트가 붙어 있는 동안에는 싸우는 대신 가게를 본다.
///
///   평소      계산대 곁에 서 있는다
///   빚이 있다  문을 막아선다. 값을 치르거나 물건을 돌려놓아야 비킨다
///   돌아섰다   Enemy 의 추격으로 넘어간다(Enemy.IsCombatTarget 이 참이 된다)
///
/// 중립인 동안 근접 공격은 공격이 아니라 말 걸기다(Enemy.Damaged). 돌아서는
/// 길은 훔치기와 멀리서 맞히기뿐이다(ShopRoom).
/// </summary>
public sealed class Merchant : MonoBehaviour
{
    /// <summary>평소의 몸 색. 다른 적과 한눈에 갈리도록 누런빛을 입힌다.</summary>
    private static readonly Color NeutralTint = new Color(1f, 0.78f, 0.24f);

    /// <summary>돌아선 뒤의 몸 색.</summary>
    private static readonly Color HostileTint = new Color(1f, 0.35f, 0.25f);

    public ShopRoom Shop { get; private set; }
    public Enemy Actor { get; private set; }

    public bool Hostile => Shop != null && Shop.Ledger.Hostile;

    /// <summary>지금 걸어가는 목표 칸과 거기까지의 지도.</summary>
    private Vector3Int target;
    private bool hasTarget;
    private readonly ChaseMap route = new ChaseMap();

    /// <summary>몸 색을 이미 입혔는가, 입혔다면 어느 쪽으로 입혔는가.</summary>
    private bool tinted;
    private bool tintedHostile;

    public void Bind(ShopRoom shop)
    {
        Shop = shop;
        Actor = GetComponent<Enemy>();
        Actor.ConfigureMerchant(this);

        // 적 프리팹을 빌려 쓰는 것이라 그 적의 신원과 그림을 떼어 낸다.
        // 남겨 두면 도감에 그 적을 잡은 것으로 적히고 이름도 그 적의 것이 뜬다.
        Actor.description = null;
        Actor.directionsprites = new Sprite[0];

        // 쓰러뜨릴 상대가 아니다. 돌아서면 이 수치 그대로 쫓아온다.
        Actor.SetStats(9999, 999, 999);

        // 굴림을 1 로 주면 잠들지 않은 채로 선다(EnemySleep).
        Actor.ConfigureSpawnSleep(1f, 1f);

        gameObject.name = "상인";
    }

    private void LateUpdate() => RefreshTint();

    /// <summary>
    /// 돌아섰는지에 맞춰 몸 색을 입힌다. <b>바뀌었을 때만</b> 대입한다.
    ///
    /// 그림(Actor.spriteRenderer)은 Enemy 의 Start 가 찾아 두므로 그 전에는
    /// 입힐 곳이 없다. 그래서 준비될 때까지 프레임마다 들여다보되, 한 번 입힌
    /// 뒤로는 돌아서는 순간에만 다시 손댄다.
    /// </summary>
    private void RefreshTint()
    {
        if (Actor == null || Actor.spriteRenderer == null) return;

        bool hostile = Hostile;
        if (tinted && hostile == tintedHostile) return;

        Actor.spriteRenderer.color = hostile ? HostileTint : NeutralTint;
        tinted = true;
        tintedHostile = hostile;
    }

    /// <summary>
    /// 중립일 때의 한 걸음. 빚이 있으면 문으로, 없으면 계산대 곁으로 간다.
    /// 돌아선 뒤에는 Enemy 가 직접 움직이므로 여기서는 아무것도 하지 않는다.
    /// </summary>
    public void Step()
    {
        if (Shop == null || Hostile || GameManager.FloorTilemap == null) return;

        Vector3Int next = Shop.Ledger.BlocksExit ? Shop.Door : Shop.Waiting;

        // 목표가 바뀌었을 때만 길을 다시 찾는다.
        if (!hasTarget || target != next)
        {
            target = next;
            hasTarget = true;
            route.Rebuild(new TilemapWalkableGrid(GameManager.FloorTilemap), GridUtil.CellCenter(target));
        }

        Vector3Int cell = GameManager.FloorTilemap.WorldToCell(transform.position);
        if (cell != target) Actor.StepAlong(route.StepsToward(transform.position));
    }

    /// <summary>
    /// 문 앞에서 곧바로 비켜선다. 값을 치렀거나 물건을 돌려놓았을 때 부른다.
    ///
    /// 걸어서 비키기를 기다리게 하면, 값을 다 치르고도 상인이 물러날 때까지
    /// 몇 턴을 문 앞에서 서성여야 한다.
    /// </summary>
    public void MoveAside()
    {
        if (Shop == null || Hostile) return;

        if (Shop.TryFreeCell(Shop.Waiting, out Vector3Int cell, forMerchant: true))
            Actor.Relocate(GridUtil.CellCenter(cell));

        // 옮겨졌으니 다음 걸음에서 길을 새로 찾게 한다.
        hasTarget = false;
    }

    /// <summary>그 칸에 서 있는 상인. 없으면 null. 돌아선 상인도 돌려준다.</summary>
    public static Merchant At(Vector2 position)
    {
        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (enemy == null || enemy.Merchant == null) continue;
            if (!enemy.gameObject.activeInHierarchy) continue;

            if (GridUtil.SameCell(enemy.transform.position, position))
                return enemy.Merchant;
        }

        return null;
    }
}
