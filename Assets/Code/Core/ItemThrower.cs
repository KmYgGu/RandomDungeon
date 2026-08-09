using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 던진 아이템이 날아가서 무언가에 맞거나 땅에 떨어지기까지를 처리한다.
///
/// 판정 자체는 RangedShot 이 한다(화살 같은 다른 원거리 수단과 공유하는 계산).
/// 이 클래스는 그 결과를 눈에 보이게 만들고, 맞은 적에게 효과를 적용하는 일만 한다.
/// </summary>
public class ItemThrower
{
    /// <summary>한 칸을 날아가는 데 걸리는 시간.</summary>
    private const float SecondsPerCell = 0.015f;

    /// <summary>벽에 부딪혀 튀는 데 걸리는 시간.</summary>
    private const float BounceSeconds = 0.1f;

    /// <summary>튈 때 벽 쪽으로 얼마나 밀려 들어가 보이는가(칸 단위).</summary>
    private const float BounceDepth = 0.3f;

    /// <summary>자리를 비켜 옆 칸으로 튀어 갈 때 걸리는 시간.</summary>
    private const float HopSeconds = 0.1f;

    /// <summary>튀어 갈 때 떠오르는 높이(칸 단위).</summary>
    private const float HopHeight = 0.35f;

    /// <summary>떨어진 것이 어떻게 되는가.</summary>
    public readonly struct Landing
    {
        /// <summary>없어지는가(용암 · 구멍).</summary>
        public readonly bool Destroyed;

        /// <summary>
        /// 어떤 땅 위에 놓여야 하는가.
        ///
        /// 자리가 차 있어 옆으로 밀어낼 때, 아무 데로나 보내면 안 되고
        /// 같은 종류의 땅을 찾아야 한다. 물에 빠진 것이 옆 칸으로 밀리면서
        /// 뭍으로 올라오면 "물에 빠졌다" 는 문구와 어긋나기 때문이다.
        /// </summary>
        public readonly TileRules.TerrainKind RestingTerrain;

        public readonly GameMessage Message;

        public Landing(bool destroyed, TileRules.TerrainKind restingTerrain, GameMessage message)
        {
            Destroyed = destroyed;
            RestingTerrain = restingTerrain;
            Message = message;
        }
    }

    /// <summary>
    /// 떨어진 자리의 땅에 따라 결말을 정한다.
    ///
    /// Tilemap 도 GameObject 도 보지 않으므로 Unity 없이 그대로 확인할 수 있다.
    /// 실제로 지우고 옮기는 일은 Throw 가 이 답을 보고 한다.
    /// </summary>
    public static Landing ResolveLanding(TileRules.TerrainKind terrain, bool stoppedByWall)
    {
        switch (terrain)
        {
            case TileRules.TerrainKind.Lava:
                return new Landing(true, TileRules.TerrainKind.Lava, GameMessage.ItemBurnedAway);

            case TileRules.TerrainKind.Pit:
                return new Landing(true, TileRules.TerrainKind.Pit, GameMessage.ItemFellFarAway);

            case TileRules.TerrainKind.Water:
                // 물에 빠진 것은 사라지지 않는다. 가라앉아 있을 뿐이라
                // 물에 들어갈 수 있는 상태라면 헤엄쳐 가서 다시 주울 수 있다.
                //
                // 자리가 차 있으면 옆으로 밀리는데, 그때도 물 안에서만 옮겨야 한다.
                // 예전에는 물이면 자리 찾기를 아예 건너뛰었고, 그래서 같은 물웅덩이에
                // 두 번째로 던진 것이 첫 번째 위에 그대로 겹쳐 놓였다.
                return new Landing(false, TileRules.TerrainKind.Water, GameMessage.ItemSankIntoWater);

            default:
                return new Landing(false, TileRules.TerrainKind.Floor,
                    stoppedByWall ? GameMessage.ItemHitWall : GameMessage.ItemThrown);
        }
    }

    /// <summary>착지 자리가 이미 차 있을 때 대신 살펴볼 여덟 방향.</summary>
    private static readonly Vector2[] AdjacentDirections =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1),
    };

    private readonly Tilemap tilemap;
    private readonly GroundObjectRegistry ground;
    private readonly List<Enemy> enemies;

    public ItemThrower(Tilemap tilemap, GroundObjectRegistry ground, List<Enemy> enemies)
    {
        this.tilemap = tilemap;
        this.ground = ground;
        this.enemies = enemies;
    }

    /// <summary>
    /// item 을 direction 방향으로 던진다.
    /// 적에게 맞으면 아이템은 사라지고, 못 맞히면 땅에 떨어진다.
    /// 끝나면 onFinished 에 띄울 메시지를 넘긴다(적에게 맞은 경우엔 적 쪽에서 이미 알렸으므로 null).
    /// </summary>
    public IEnumerator Throw(
        GameObject item,
        string itemName,
        Vector2 direction,
        PlayerStats stats,
        Action<GameMessage?> onFinished)
    {
        var grid = new TilemapWalkableGrid(tilemap);
        Vector3Int origin = tilemap.WorldToCell(item.transform.position);

        // 살아 있는 적들의 칸을 판정 대상으로 넘긴다.
        var targets = new List<Vector3Int>();
        var living = new List<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            living.Add(enemy);
            targets.Add(tilemap.WorldToCell(enemy.transform.position));
        }

        RangedShot.Result shot = RangedShot.Trace(
            grid,
            origin,
            new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y)),
            stats.ThrowDistance,
            targets);

        yield return FlyTo(item, origin, shot.ImpactCell);

        // 벽에 막혀 멈춘 것이면 튕기는 모습을 보여 준다.
        // (맞힌 경우와 벽에 막힌 경우는 함께 일어나지 않는다 - RangedShot 참고)
        if (shot.StoppedByWall)
        {
            yield return BounceOffWall(
                item, GridUtil.CellCenter(shot.ImpactCell), direction.normalized);
        }

        if (shot.HitTarget)
        {
            ApplyHit(living[shot.TargetIndex], item, itemName, stats);

            // 맞은 아이템은 소모된다.
            UnityEngine.Object.Destroy(item);
            onFinished?.Invoke(null);
            yield break;
        }

        // 아무것도 못 맞혔다. 떨어진 자리의 땅이 무엇이냐에 따라 갈린다.
        Vector3 impact = GridUtil.CellCenter(shot.ImpactCell);

        Landing outcome = ResolveLanding(TileRules.KindAt(tilemap, impact), shot.StoppedByWall);

        if (outcome.Destroyed)
        {
            // 없어지는 것은 바닥 목록에 넣지 않는다.
            UnityEngine.Object.Destroy(item);
            onFinished?.Invoke(outcome.Message);
            yield break;
        }

        // 같은 종류의 땅에서만 자리를 찾는다. 물이면 물 안에서, 바닥이면 바닥에서.
        bool Blocked(Vector3 candidate) => IsSpotBlocked(candidate, outcome.RestingTerrain);

        if (!TryFindFreeSpot(impact, Blocked, out Vector3 landing))
        {
            // 둘레가 온통 물건이라 놓을 자리가 없다. 예전에는 경고만 남기고 겹쳐서
            // 놓았는데, 그러면 한 칸에 여러 개가 포개져 어느 것을 밟는지 알 수 없었다.
            UnityEngine.Object.Destroy(item);
            onFinished?.Invoke(GameMessage.ItemBuriedAmongItems);
            yield break;
        }

        // 제자리가 차 있어 옆으로 밀려났다면, 튀어 옮겨 가는 모습을 보여 준다.
        // 그러지 않으면 엉뚱한 칸에 순간이동한 것처럼 보인다.
        if (landing != impact)
            yield return HopTo(item, impact, landing);

        item.transform.position = landing;
        ground.Add(landing);

        onFinished?.Invoke(outcome.Message);
    }

    /// <summary>
    /// 맞은 적에게 아이템의 효과를 적용한다.
    ///
    /// 특별한 효과가 있는 아이템은 그 효과를 그대로 적에게 준다.
    /// (회복 아이템을 던지면 적이 회복되고, 능력치 아이템을 던지면 적이 강해진다.)
    /// 전투·생존과 무관한 아이템은 그냥 맞은 만큼 아프다 — 힘에 비례한 피해.
    /// </summary>
    private static void ApplyHit(Enemy target, GameObject item, string itemName, PlayerStats stats)
    {
        // 돈은 아이템이 아니라 재화다. 액수를 10 으로 나눈 만큼이,
        // 방어력을 무르고 그대로 들어간다.
        if (GoldPile.IsGold(item))
        {
            target.TakeTrueDamage(GoldPile.DamageOf(GoldPile.AmountOf(item)));
            return;
        }

        ThrownEffect effect = ItemEffects.GetThrownEffect(itemName, out int amount);

        switch (effect)
        {
            case ThrownEffect.HealHp:
                target.HealBy(amount);
                break;

            case ThrownEffect.RaiseAttack:
                target.RaiseAttackBy(amount);
                break;

            default:
                target.TakeDirectDamage(stats.ThrowDamage);
                break;
        }
    }

    /// <summary>
    /// 놓일 자리가 차 있어 옆 칸으로 비켜 갈 때, 통 튀어 옮겨 가는 모습.
    ///
    /// 포물선을 그리며 넘어간다. 바닥을 미끄러지듯 옮기면 "밀려났다" 로 보이는데,
    /// 실제로는 먼저 떨어진 물건에 부딪혀 튄 것이므로 떠오르는 편이 맞다.
    /// </summary>
    private IEnumerator HopTo(GameObject item, Vector3 from, Vector3 to)
    {
        if (GameManager.FastForward)
        {
            item.transform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < HopSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / HopSeconds);

            // 가로로는 곧게 가면서, 세로로만 한 번 떠올랐다 내려온다.
            Vector3 flat = Vector3.Lerp(from, to, t);
            float lift = Mathf.Sin(t * Mathf.PI) * HopHeight;

            item.transform.position = flat + Vector3.up * lift;
            yield return null;
        }

        item.transform.position = to;
    }

    /// <summary>아이템이 날아가는 모습을 보여 준다.</summary>
    private IEnumerator FlyTo(GameObject item, Vector3Int from, Vector3Int to)
    {
        Vector3 start = GridUtil.CellCenter(from);
        Vector3 end = GridUtil.CellCenter(to);

        int cells = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
        if (cells == 0) yield break;

        // 달리기 같은 빨리감기 중에는 연출을 건너뛴다.
        if (GameManager.FastForward)
        {
            item.transform.position = end;
            yield break;
        }

        float duration = cells * SecondsPerCell;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            item.transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        item.transform.position = end;
    }

    /// <summary>
    /// 착지 지점이 비어 있으면 그대로, 이미 차 있으면 인접한 빈 칸을 찾는다.
    /// 여덟 방향이 전부 막혀 있으면 false.
    ///
    /// isBlocked 만 받고 Tilemap 도 GameObject 도 보지 않으므로
    /// Unity 없이 그대로 확인할 수 있다.
    /// </summary>
    public static bool TryFindFreeSpot(
        Vector3 landing, Func<Vector3, bool> isBlocked, out Vector3 spot)
    {
        spot = landing;

        if (isBlocked == null) return true;
        if (!isBlocked(landing)) return true;

        foreach (Vector2 direction in AdjacentDirections)
        {
            Vector3 candidate = landing + (Vector3)direction;
            if (isBlocked(candidate)) continue;

            spot = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 이 자리에 아이템을 둘 수 없는가.
    ///
    /// 이미 무언가 놓여 있거나, 평범한 바닥이 아니면 둘 수 없다.
    /// 자리가 없다고 해서 물·용암·구멍으로 밀어내면 안 된다. 그쪽은 각자 다른
    /// 결말이 있는 땅이고, 이미 "땅에 떨어졌다" 는 문구를 띄우기로 정한 뒤라
    /// 실제와 어긋난 말을 하게 된다.
    /// </summary>
    private bool IsSpotBlocked(Vector3 position, TileRules.TerrainKind restingTerrain)
    {
        return ground.Contains(position)
            || TileRules.KindAt(tilemap, position) != restingTerrain;
    }

    /// <summary>
    /// 벽에 부딪혀 통 튀는 모습.
    ///
    /// 날아가기는 벽 바로 앞 칸에서 멈추므로 그냥 두면 벽에 닿지도 않고 뚝 서 버린다.
    /// 벽 쪽으로 한 번 부딪혔다가 되돌아오는 흔들림을 잦아들게 주어,
    /// "맞고 튕겼다" 가 눈에 보이게 한다. 칸이 바뀌는 것은 아니라서
    /// 판정에는 아무 영향이 없다.
    /// </summary>
    private IEnumerator BounceOffWall(GameObject item, Vector3 restPosition, Vector2 direction)
    {
        // 달리기 같은 빨리감기 중에는 연출을 건너뛴다.
        if (GameManager.FastForward)
        {
            item.transform.position = restPosition;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < BounceSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / BounceSeconds);

            // 벽 쪽으로 갔다가 되돌아오기를 한 번. (1 - t) 가 붙어 있어 잦아든다.
            float swing = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);

            item.transform.position = restPosition + (Vector3)(direction * (BounceDepth * swing));
            yield return null;
        }

        item.transform.position = restPosition;
    }
}
