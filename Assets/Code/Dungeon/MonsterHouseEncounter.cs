using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>입장 전 적은 비활성 상태로 보관하고 입장 시 한 마리씩 떨어뜨린다.</summary>
public sealed class MonsterHouseEncounter
{
    public MonsterHousePlan Plan { get; }
    private readonly GameObject holder;
    private readonly List<Enemy> waiting = new List<Enemy>();
    public MonsterHouseEncounter(RectInt interior)
    {
        Plan = new MonsterHousePlan(interior);
        holder = new GameObject("Unopened monster house");
    }

    public void Add(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.gameObject.SetActive(false);
        enemy.transform.SetParent(holder.transform, true);
        waiting.Add(enemy);
    }

    public IEnumerable<Vector3Int> ConcealedCells
    {
        get
        {
            if (Plan.Triggered || GameManager.FloorTilemap == null) yield break;
            foreach (var enemy in waiting)
                if (enemy != null) yield return GameManager.FloorTilemap.WorldToCell(enemy.transform.position);
        }
    }

    public IEnumerator Enter(GameManager game)
    {
        var player = game.Player.GetComponent<Player>();
        if (player != null) player.InterruptForMonsterHouse();
        yield return game.Announce(MessageCatalog.Format(GameMessage.MonsterHouse), DropEnemies(game), waitForConfirmation: false);
    }

    private IEnumerator DropEnemies(GameManager game)
    {
        var floor = GameManager.FloorTilemap;
        var occupied = new HashSet<Vector3Int>();
        occupied.Add(floor.WorldToCell(game.Player.transform.position));
        occupied.Add(floor.WorldToCell(game.PlayerPosition));
        foreach (var partner in game.partners)
            if (partner != null && partner.gameObject.activeInHierarchy)
                occupied.Add(floor.WorldToCell(partner.transform.position));
        foreach (var enemy in game.enemies)
            if (enemy != null && enemy.gameObject.activeInHierarchy)
                occupied.Add(floor.WorldToCell(enemy.transform.position));
        occupied.Add(game.MapMake.GetStairsPosition());
        foreach (var enemy in waiting)
        {
            if (enemy == null) continue;
            Vector3Int preferred = floor.WorldToCell(enemy.transform.position);
            if (!Plan.TryLanding(preferred, occupied,
                cell => floor.HasTile(cell) && TileRules.IsWalkableCell(floor, cell), out var landing))
            {
                Object.Destroy(enemy.gameObject);
                continue;
            }
            occupied.Add(landing);
            enemy.transform.position = GridUtil.CellCenter(landing);
            var sprite = enemy.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null) sprite.enabled = false;
            enemy.gameObject.SetActive(true);
            // Start에서 정상 위치를 기록한 뒤 스프라이트만 위로 올린다.
            yield return null;
            if (enemy == null || sprite == null) continue;
            var visual = sprite.transform;
            Vector3 home = visual.localPosition;
            Vector3 homeScale = visual.localScale;
            float localUnit = 1f / Mathf.Abs(visual.parent.lossyScale.y);
            visual.localPosition = home + Vector3.up * (MonsterHouseDropMotion.Height * localUnit);
            sprite.enabled = true;
            try
            {
                float elapsed = 0f;
                while (elapsed < MonsterHouseDropMotion.FallSeconds)
                {
                    elapsed += Time.deltaTime;
                    visual.localPosition = home + Vector3.up * (MonsterHouseDropMotion.HeightAt(elapsed) * localUnit);
                    yield return null;
                }
                visual.localPosition = home;
                elapsed = 0f;
                while (elapsed < MonsterHouseDropMotion.LandingSeconds)
                {
                    Vector2 scale = MonsterHouseDropMotion.LandingScale(elapsed);
                    visual.localScale = new Vector3(homeScale.x * scale.x, homeScale.y * scale.y, homeScale.z);
                    // 발 위치를 고정한 채 눌렸다가 복원. 작게 튀며 무게감을 준다.
                    float footOffset = sprite.sprite.bounds.min.y * homeScale.y * (1f - scale.y);
                    visual.localPosition = home + Vector3.up * (footOffset + MonsterHouseDropMotion.BounceAt(elapsed) * localUnit);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                if (visual != null)
                {
                    visual.localPosition = home;
                    visual.localScale = homeScale;
                }
            }
            yield return new WaitForSeconds(0.018f);
        }
        // 마지막으로 활성화한 적도 Start를 끝낸 뒤, 층 전체에 한 번 경보를 전달한다.
        var alarm = new MonsterHouseRally(Plan.Interior, new TilemapWalkableGrid(floor));
        foreach (var enemy in game.enemies)
            if (enemy != null && enemy.gameObject.activeInHierarchy) enemy.ReceiveHouseAlarm(alarm);
        waiting.Clear();
    }
}
