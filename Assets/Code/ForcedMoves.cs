using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어가 <b>제 발로 가지 않는 이동</b>과 그 결말 — 구멍 · 워프 · 날리기.
///
/// ── 왜 GameManager 에서 떼어 냈나 ─────────────────────────────────
/// 함정이 부르는 것은 TrapActions 의 표로 정리했지만, 그 동작 250 줄은 여전히
/// GameManager 안에 있었다. 함정 전용이 아니라 지팡이·능력에서도 부를 것들이라
/// 함정 파일에 넣을 수는 없고, 그렇다고 갓 오브젝트에 둘 이유도 없다.
///
/// 셋의 공통점이 뚜렷하다 — <b>플레이어를 옮기는 동안 턴을 멈추고, 도착한 자리의
/// 땅이 무엇이냐로 결말이 갈린다.</b> 그 흐름이 한 파일에 모여 있어야 읽힌다.
///
/// <b>MonoBehaviour 가 아니다.</b> 씬의 Inspector 연결을 건드리지 않는다.
/// 코루틴을 돌리는 일만 GameManager 에게 맡긴다.
/// </summary>
public class ForcedMoves
{
    private readonly GameManager game;

    public ForcedMoves(GameManager game)
    {
        this.game = game;
    }

    /// <summary>함정 문구를 읽을 틈. 곧바로 다음 일이 일어나면 무엇에 당했는지 못 읽는다.</summary>
    private const float TrapMessagePause = 1f;

    /// <summary>
    /// 구멍. 방어력을 무시하고 아프게 한 뒤 다음 층으로 떨어뜨린다.
    ///
    /// 피해가 먼저다. 이 피해로 죽을 수 있고, 그때는 내려가지 않고 거기서 끝난다.
    /// 순서를 뒤집으면 죽은 채로 다음 층이 시작된다.
    /// </summary>
    public IEnumerator FallThroughHole()
    {
        Player player = game.PlayerComponent;

        // 떨어지는 것도 강제 이동이다. 바닥을 뚫고 내려가는 중에 적이 때리면
        // 이미 그 층에 없는 사람을 때리는 셈이 된다.
        GameManager.ForcedMoveRunning = true;
        InputFocus.Give(InputOwner.Prompt);

        // 먼저 사라지고 나서 아프다. 순서를 뒤집으면 멀쩡히 서 있는 캐릭터가
        // 느닷없이 5 만큼 깎여서, 무엇 때문에 아픈지가 화면에 없다.
        if (player != null) yield return player.FallThroughFloor();

        GameManager.Stats.TakeDamage(TrapEffects.HoleDamage);
        game.ShowMessage(GameMessage.PlayerDamaged, amount: TrapEffects.HoleDamage);

        if (GameManager.Stats.IsDead)
        {
            // 층이 넘어가지 않으므로 여기서 되돌리지 않으면 보이지 않는 채로 죽는다.
            player?.RestoreAfterFall();
            InputFocus.ReturnToPlayer();
            GameManager.ForcedMoveRunning = false;
            game.GameOver();
            yield break;
        }

        // 문구를 읽을 틈을 준다. 달리는 중이면 건너뛴다.
        if (!GameManager.FastForward) yield return new WaitForSeconds(TrapMessagePause);

        player?.RestoreAfterFall();
        InputFocus.ReturnToPlayer();
        GameManager.ForcedMoveRunning = false;

        ShopRoom.LoadNextFloor();
    }

    /// <summary>
    /// 워프. 다른 방의 빈 칸으로 옮긴다.
    ///
    /// 함정 전용이 아니다 — 지팡이·능력에서도 그대로 부르면 된다.
    /// 옮기는 동안 GameManager.ForcedMoveRunning 을 세워, 적이 지난 자리를 기준으로
    /// 움직이거나 때리지 않게 한다.
    ///
    /// 갈 곳을 먼저 정하고 나서 연출을 시작한다. 연출을 띄워 놓고 자리를 못
    /// 찾으면 올라갔다가 제자리로 내려오는 꼴이 되기 때문이다.
    /// </summary>
    public IEnumerator WarpPlayer()
    {
        Player player = game.PlayerComponent;
        if (player == null || game.MapMake == null || GameManager.FloorTilemap == null) yield break;

        // 자리를 고르는 일은 전부 칸 좌표로 한다. 월드 좌표는 칸 중심이 x+0.5 라서
        // 그걸로 계산하면 반 칸씩 어긋난다(TrapEffects 주석). 월드로 되돌리는 것은
        // 맨 마지막 한 번뿐이다.
        bool found = TrapEffects.TryPickWarpDestination(
            game.MapMake.RoomCells,
            EnemyCells(),
            GameManager.FloorTilemap.WorldToCell(player.transform.position),
            // using System 이 있어 Random 만으로는 어느 쪽인지 가려지지 않는다.
            (min, max) => UnityEngine.Random.Range(min, max),
            out Vector3Int destinationCell);

        // 갈 곳이 없으면(방이 하나뿐이고 적으로 가득 찬 경우) 아무 일도 없다.
        if (!found) yield break;

        GameManager.ForcedMoveRunning = true;
        InputFocus.Give(InputOwner.Prompt);

        yield return player.WarpTo(GridUtil.CellCenter(GameManager.FloorTilemap, destinationCell));

        InputFocus.ReturnToPlayer();
        GameManager.ForcedMoveRunning = false;
    }

    /// <summary>
    /// 날리기. direction 방향으로 cells 칸 밀어낸다.
    ///
    /// 함정 전용이 아니다 — 밀어내는 지팡이나 능력도 이것을 부르면 된다.
    /// 그래서 방향과 거리를 밖에서 받는다.
    ///
    /// 날아가는 판정은 던진 아이템과 같은 RangedShot 을 쓴다. 같은 규칙으로
    /// 날아가야 "던진 것은 저기까지 가는데 나는 왜 여기서 멈추지" 가 안 생긴다.
    /// </summary>
    public IEnumerator BlowPlayer(Vector2 direction, int cells)
    {
        Player player = game.PlayerComponent;
        if (player == null || GameManager.FloorTilemap == null) yield break;
        if (direction == Vector2.zero || cells <= 0) yield break;

        var step = new Vector2Int(
            Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));

        Vector3Int origin = GameManager.FloorTilemap.WorldToCell(player.transform.position);

        // 살아 있는 적과 <b>동료</b>를 판정 대상으로 넘긴다. 부딪히면 거기서 멈춘다.
        //
        // 동료를 빼면 날려진 플레이어가 동료를 뚫고 지나가거나 같은 칸에 겹쳐
        // 서서 둘 다 빠져나오지 못한다. 실제로 그렇게 끼었다.
        // 부딪히는 규칙은 적과 똑같다 — 자리를 차지하는 것은 아군도 마찬가지다.
        var living = new List<Enemy>();
        var friends = new List<Partner>();
        var targets = new List<Vector3Int>();

        foreach (Enemy enemy in game.enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            living.Add(enemy);
            friends.Add(null);
            targets.Add(GameManager.FloorTilemap.WorldToCell(enemy.transform.position));
        }

        foreach (Partner partner in game.partners)
        {
            if (partner == null || !partner.gameObject.activeInHierarchy) continue;
            living.Add(null);
            friends.Add(partner);
            targets.Add(GameManager.FloorTilemap.WorldToCell(partner.transform.position));
        }

        RangedShot.Result shot = RangedShot.Trace(
            new TilemapWalkableGrid(GameManager.FloorTilemap), origin, step, cells, targets);

        // 적에게 부딪혔으면 그 앞 칸이 내가 설 자리다.
        // 같은 칸에 겹쳐 서면 둘 다 빠져나오지 못한다.
        Vector3Int landingCell = shot.HitTarget
            ? new Vector3Int(shot.ImpactCell.x - step.x, shot.ImpactCell.y - step.y, 0)
            : shot.ImpactCell;

        Vector3 landing = GridUtil.CellCenter(GameManager.FloorTilemap, landingCell);

        GameManager.ForcedMoveRunning = true;
        InputFocus.Give(InputOwner.Prompt);

        yield return player.BlowTo(landing, direction, shot.StoppedByWall || shot.HitTarget);

        // 부딪힌 것이 있으면 둘 다 아프다. 서로 부딪힌 것이므로 한쪽만 아플 이유가 없다.
        if (shot.HitTarget)
        {
            if (friends[shot.TargetIndex] != null)
                yield return CollideWithPartner(friends[shot.TargetIndex]);
            else
                yield return CollideWith(living[shot.TargetIndex]);
        }

        // 떨어진 자리의 땅이 무엇이냐에 따라 갈린다.
        TrapEffects.BlowOutcome outcome = TrapEffects.ResolveBlow(
            TileRules.KindAt(GameManager.FloorTilemap, landing), shot.StoppedByWall);

        yield return ApplyBlowOutcome(outcome);

        InputFocus.ReturnToPlayer();
        GameManager.ForcedMoveRunning = false;
    }

    /// <summary>
    /// 날려지다 동료와 부딪혔다. <b>적과 똑같이</b> 둘 다 아프다.
    ///
    /// 아군이라고 봐주지 않는다. 봐주면 동료를 방패 삼아 날리기 함정을 무르는
    /// 수가 생기고, 무엇보다 부딪혔는데 아무 일도 없으면 왜 거기서 멈췄는지
    /// 화면에서 읽을 수가 없다.
    /// </summary>
    private IEnumerator CollideWithPartner(Partner target)
    {
        if (target == null) yield break;

        game.ShowTwoLines(
            MessageCatalog.Format(GameMessage.BlownIntoEnemy, target.DisplayName),
            MessageCatalog.Format(GameMessage.PlayerDamaged, amount: TrapEffects.BlowWallDamage));

        // 맞은 쪽이 먼저다(적과 같은 이유).
        target.TakeTrueDamage(TrapEffects.BlowWallDamage);
        GameManager.Stats.TakeDamage(TrapEffects.BlowWallDamage);

        if (GameManager.Stats.IsDead) game.GameOver();

        if (!GameManager.FastForward) yield return new WaitForSeconds(TrapMessagePause);
    }

    /// <summary>날려지다 적과 부딪혔다. 둘 다 방어력 무시 피해를 입는다.</summary>
    private IEnumerator CollideWith(Enemy target)
    {
        if (target == null) yield break;

        game.ShowTwoLines(
            MessageCatalog.Format(GameMessage.BlownIntoEnemy, target.DisplayName),
            MessageCatalog.Format(GameMessage.PlayerDamaged, amount: TrapEffects.BlowWallDamage));

        // 맞은 쪽이 먼저다. 플레이어가 여기서 죽으면 게임 오버로 넘어가는데,
        // 그 뒤에 적을 때리면 이미 끝난 판에서 경험치가 들어온다.
        target.TakeTrueDamage(TrapEffects.BlowWallDamage);
        GameManager.Stats.TakeDamage(TrapEffects.BlowWallDamage);

        if (GameManager.Stats.IsDead) game.GameOver();

        if (!GameManager.FastForward) yield return new WaitForSeconds(TrapMessagePause);
    }

    /// <summary>살아 있는 적들이 선 칸.</summary>
    private List<Vector3Int> EnemyCells()
    {
        var cells = new List<Vector3Int>();
        if (GameManager.FloorTilemap == null) return cells;

        foreach (Enemy enemy in game.enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
                cells.Add(GameManager.FloorTilemap.WorldToCell(enemy.transform.position));
        }
        return cells;
    }

    /// <summary>날려진 끝에 일어나는 일. 판정은 TrapEffects 가 이미 정했다.</summary>
    private IEnumerator ApplyBlowOutcome(TrapEffects.BlowOutcome outcome)
    {
        switch (outcome)
        {
            case TrapEffects.BlowOutcome.HitWall:
                game.ShowTwoLines(
                    MessageCatalog.Format(GameMessage.BlownIntoWall),
                    MessageCatalog.Format(
                        GameMessage.PlayerDamaged, amount: TrapEffects.BlowWallDamage));
                GameManager.Stats.TakeDamage(TrapEffects.BlowWallDamage);
                if (GameManager.Stats.IsDead) game.GameOver();
                break;

            case TrapEffects.BlowOutcome.Water:
                game.ShowMessage(GameMessage.BlownIntoWater);
                if (!GameManager.FastForward) yield return new WaitForSeconds(TrapMessagePause);

                // 물에 들어갈 수 없는 상태로 물 한가운데 놓이면 나올 길이 없다.
                //
                // 안쪽의 WarpPlayer 가 끝나면서 GameManager.ForcedMoveRunning 을 내린다.
                // 그 시점에는 옮기는 일이 이미 다 끝나 있어서, 그때 적 턴이
                // 풀려도 적은 새 자리를 보고 움직인다.
                yield return WarpPlayer();
                break;

            case TrapEffects.BlowOutcome.Lava:
                int burn = TrapEffects.LavaDamage(GameManager.Stats.Hp);
                game.ShowTwoLines(
                    MessageCatalog.Format(GameMessage.BlownIntoLava),
                    MessageCatalog.Format(GameMessage.PlayerDamaged, amount: burn));
                GameManager.Stats.TakeDamage(burn);

                if (GameManager.Stats.IsDead) { game.GameOver(); break; }

                if (!GameManager.FastForward) yield return new WaitForSeconds(TrapMessagePause);
                yield return WarpPlayer();
                break;

            case TrapEffects.BlowOutcome.Pit:
                // 절벽은 구멍 함정과 똑같이 다룬다. 같은 일이 일어난 것이므로
                // 처리도 한 곳에 두어야 한쪽만 고쳐지는 일이 없다.
                yield return FallThroughHole();
                break;
        }
    }
}
