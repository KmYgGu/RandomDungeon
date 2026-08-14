using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 행동한 뒤의 한 턴 — 동료와 적이 움직이고, 배가 고파지고, 몸이 아문다.
///
/// ── 왜 GameManager 에서 떼어 냈나 ─────────────────────────────────
/// 갓 오브젝트에서 가장 얽힌 부분이었다. 턴 순서(플레이어 → 동료 → 적)와
/// 그 안의 세 단계(정하기 → 이동 → 공격)가 220 줄에 걸쳐 있는데, 그 사이에
/// 가방·함정·상태이상 코드가 끼어 있어 한 흐름으로 읽히지 않았다.
///
/// <b>턴 순서는 이 파일 하나를 읽으면 다 보인다.</b> 새 종류(중립 오브젝트,
/// 소환물 따위)가 턴을 갖게 되면 여기에 한 단계를 더한다.
///
/// <b>MonoBehaviour 가 아니다.</b> 씬의 Inspector 연결을 건드리지 않는다.
/// </summary>
public class TurnLoop
{
    /// <summary>적들이 움직이기 전의 연출용 대기.</summary>
    private const float EnemyTurnDelay = 0.1f;

    /// <summary>몇 턴마다 배가 고파지는가.</summary>
    private const int HungerTickInterval = 5;

    /// <summary>몇 턴마다 체력이 자연 회복되는가.</summary>
    private const int RecoveryTickInterval = 10;

    /// <summary>
    /// 동료가 한 번에 아무는 양.
    ///
    /// 플레이어는 힘에 비례해 회복하지만(PlayerStats.NaturalRecoveryAmount)
    /// 동료에게는 힘이 없다. 고정으로 두되, 동료의 체력이 플레이어보다 훨씬
    /// 적으므로(10~25) 같은 1 이라도 비율로는 더 크다.
    /// </summary>
    private const int PartnerNaturalRecovery = 1;

    private readonly GameManager game;

    public TurnLoop(GameManager game)
    {
        this.game = game;
    }


    public IEnumerator MoveEnemies()
    {
        game.playersTurn = false;

        // 중요한 알림을 읽는 동안에는 적을 움직이지 않는다.
        //
        // 레벨업 같은 것을 읽으라고 띄워 놓고 그 사이에 적이 때리면, 읽을 수도 없고
        // 맞은 것도 못 본다. 확인을 누를 때까지 턴 자체를 멈춘다.
        // 턴 수도 아직 올리지 않는다 — 굶주림 같은 것이 읽는 동안 흘러가면 안 된다.
        while (game.MessageBox.ShowingImportant)
            yield return null;

        // 워프·날리기가 끝나기를 기다린다. 옮겨진 뒤의 자리를 기준으로 움직여야
        // 적이 빈 칸을 쫓거나 때리는 일이 없다.
        while (GameManager.ForcedMoveRunning)
            yield return null;

        GameManager.TurnCount++;

        ApplyHungerTick();
        game.TickConditions();

        // 빨리감기 중에는 연출용 대기를 건너뛴다. 이 대기가 없으면 아래 과정이
        // 전부 같은 프레임 안에서 끝나므로, 달리기 한 번이 한 프레임에 처리된다.
        if (!GameManager.FastForward)
            yield return new WaitForSeconds(EnemyTurnDelay);

        // 적들이 서로 같은 칸으로 몰리지 않도록, 이번 턴의 예약 위치를 새로 받는다.
        game.enemyPositions.Clear();

        foreach (Enemy enemy in game.enemies)
            if (enemy != null) enemy.BeginTurn();

        foreach (Partner partner in game.partners)
            if (partner != null) partner.BeginTurn();

        // 이번 턴의 길찾기 지도를 한 번만 만든다. 적이 몇 마리든 탐색은 한 번이다.
        // 동료도 이 지도로 플레이어를 따라오므로, 적이 없어도 동료가 있으면 만든다.
        if ((game.enemies.Count > 0 || game.partners.Count > 0) && game.Player != null && GameManager.FloorTilemap != null)
            GameManager.Chase.Rebuild(new TilemapWalkableGrid(GameManager.FloorTilemap), game.Player.transform.position);

        // ── 동료가 적보다 먼저 움직인다 ────────────────────────────────────
        //
        // 플레이어가 물러서면 동료가 그 앞을 메워 벽이 되고, 앞으로 나가면 뒤를
        // 따라온다. 적이 먼저 움직이면 그 자리가 이미 채워져 동료가 방패 노릇을
        // 못 한다. 적과 같은 세 단계(정하기 → 이동 → 공격)를 따른다.
        yield return MovePartners();

        // ── 적의 한 턴은 세 단계로 나뉜다 ──────────────────────────────────
        //
        // 예전에는 적 하나하나를 처음부터 끝까지 차례로 돌렸다. 그런데 이동하는 적은
        // 저마다 WaitForFixedUpdate 를 한 번씩 기다리므로, 그 대기가 마릿수만큼
        // 쌓여서 적이 많은 방일수록 플레이어 조작이 눈에 띄게 굼떠졌다.
        //
        // 차례로 돌아야 하는 것은 '공격' 뿐이다. 겹쳐 때리면 누구에게 얼마나 맞았는지
        // 메시지창에 마지막 하나만 남기 때문이다. 이동은 겹쳐도 아무 문제가 없다.

        // 1. 무엇을 할지 먼저 전부 정한다.
        //    이동보다 먼저 정해 두어야, 앞의 적이 움직여 붙는 바람에 뒤의 적 판단이
        //    달라지는 일이 없다. 목록 순서가 규칙이 되어서는 안 된다.
        //
        //    적이 행동하다 죽어 목록에서 빠질 수 있으므로 복사본을 돌린다.
        var movers = new List<Enemy>();
        var attackers = new List<Enemy>();

        foreach (Enemy enemy in new List<Enemy>(game.enemies))
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (enemy.DecideAttack()) attackers.Add(enemy);
            else movers.Add(enemy);
        }

        // 2. 이동은 마릿수와 상관없이 같은 프레임에 끝낸다.
        //    같은 칸으로 겹쳐 들어가는 것은 game.enemyPositions 예약이 막아 준다.
        //
        //    가까운 적부터 움직인다. 줄지어 쫓아올 때 앞선 적이 먼저 비켜 주어야
        //    뒤의 적이 그 칸을 이어받는다. 순서가 반대면 뒤의 적이 아직 차 있는
        //    칸을 보고 물러서서, 도망치는 플레이어와의 간격이 계속 벌어진다.
        movers.Sort((a, b) => a.StepsToPlayer.CompareTo(b.StepsToPlayer));

        foreach (Enemy enemy in movers)
            enemy.TakeStep();

        // 옮긴 좌표(MovePosition)가 화면에 반영되도록 한 번만 기다린다.
        // 적이 백 마리든 이 대기는 한 번뿐이다.
        if (!GameManager.FastForward && movers.Count > 0)
            yield return new WaitForFixedUpdate();

        // 3. 때리는 적만 한 마리씩. 플레이어에 붙어 있는 것들뿐이라 많아야 여덟이다.
        foreach (Enemy enemy in attackers)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (GameManager.FastForward)
            {
                // 빨리감기 중에는 공격에 기다림이 없다(Enemy.AttackTurn).
                // 코루틴으로 넘기면 그것만으로 한 프레임을 쓰므로 그 자리에서 돌린다.
                IEnumerator turn = enemy.AttackTurn();
                while (turn.MoveNext()) { }
            }
            else
            {
                yield return enemy.AttackTurn();
            }

            // 도중에 죽었으면 나머지 적을 움직일 이유가 없다.
            if (GameManager.Stats.IsDead) break;
        }

        // 여기서 InteractionHandled 를 건드리지 않는다.
        //
        // 그 값은 "밟은 칸을 아직 살펴보지 않았다" 는 뜻이고, 새 칸에 도착할 때(TryStep)
        // 열려서 실제로 처리한 순간(OnTriggerStay2D)에 닫힌다. 즉 턴이 아니라
        // '도착' 단위의 표시다.
        //
        // 예전에는 턴이 끝날 때 여기서 닫았는데, 달리는 중에는 이 코루틴이 동기로
        // 끝나기 때문에 물리 프레임이 오기도 전에 닫혀 버렸다. 그래서 달려서 밟은
        // 함정·계단·아이템이 하나도 처리되지 않았다 — 달리기로 함정을 통째로
        // 피할 수 있었다는 뜻이다.
        //
        // 놓자마자 도로 줍는 것을 막는 일은 game.Player.placedUnderFoot 가 따로 맡고 있어서,
        // 여기서 닫지 않아도 그 보호는 그대로다.
        game.playersTurn = true;
    }

    /// <summary>
    /// 동료들의 한 턴. 적과 같은 세 단계로 나눈다.
    ///
    /// 모두가 먼저 정한 뒤에 움직여야 목록에서의 차례가 규칙이 되지 않는다.
    /// 움직이다 쓰러져 목록에서 빠질 수 있으므로 복사본을 돌린다.
    /// </summary>
    private IEnumerator MovePartners()
    {
        if (game.partners.Count == 0) yield break;

        var movers = new List<Partner>();
        var attackers = new List<Partner>();

        foreach (Partner partner in new List<Partner>(game.partners))
        {
            if (partner == null || !partner.gameObject.activeInHierarchy) continue;

            partner.DecideTurn();

            if (partner.WillAttack) attackers.Add(partner);
            else movers.Add(partner);
        }

        foreach (Partner partner in movers)
            partner.TakeStep();

        // 옮긴 좌표가 화면에 반영되도록 한 번만 기다린다(적과 같은 이유).
        if (!GameManager.FastForward && movers.Count > 0)
            yield return new WaitForFixedUpdate();

        foreach (Partner partner in attackers)
        {
            if (partner == null || !partner.gameObject.activeInHierarchy) continue;

            if (GameManager.FastForward)
            {
                IEnumerator turn = partner.AttackTurn();
                while (turn.MoveNext()) { }
            }
            else
            {
                yield return partner.AttackTurn();
            }
        }
    }

    /// <summary>턴이 지날 때마다 배가 고파지고, 굶으면 체력이 깎인다.</summary>
    /// <summary>
    /// 만복도가 줄어드는 간격. 장비가 늘려 준다.
    ///
    /// 간격이 길어질수록 배가 늦게 고파진다. 소모량을 줄이지 않고 간격을 늘리는
    /// 이유는, 소모량은 이미 1 이라 더 줄일 수가 없기 때문이다.
    /// </summary>
    private int HungerInterval
        => HungerTickInterval + (GameManager.Gear != null ? GameManager.Gear.AbilityValue(EquipAbility.SlowHunger) : 0);

    private void ApplyHungerTick()
    {
        if (GameManager.TurnCount % HungerInterval == 0 && !GameManager.Stats.IsStarving)
        {
            int before = GameManager.Stats.Hunger;
            GameManager.Stats.ConsumeHunger();

            HungerWarning.Level level =
                HungerWarning.For(before, GameManager.Stats.Hunger, GameManager.Stats.MaxHunger);

            if (level != HungerWarning.Level.None)
                game.ShowMessage(HungerWarning.MessageOf(level));

            if (GameManager.TurnCount % RecoveryTickInterval == 0)
            {
                // 한 턴에 1 이 안 차는 만큼은 안에 쌓였다가 나중에 한꺼번에 오른다
                // (PlayerStats.RecoverNaturally). 가득 찼는지도 그쪽이 본다.
                GameManager.Stats.RecoverNaturally();

                // 동료도 함께 아문다. 가방이 없어 약을 못 먹고 층을 넘어도 차지
                // 않으므로, 이것이 없으면 오래 데리고 다닐수록 반드시 쓰러진다.
                GameManager.Party.HealAll(PartnerNaturalRecovery);
            }
        }

        if (GameManager.Stats.IsStarving && !GameManager.Stats.IsDead)
        {
            GameManager.Stats.TakeDamage(1);
            game.GameOver();
        }
    }
}
