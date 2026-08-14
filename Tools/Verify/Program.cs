using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 실제 C# 던전 생성 코드(Check.dll)를 Unity 없이 직접 호출해 검증한다.
// Tilemap 이 필요한 부분은 건드리지 않고, 순수 계산 부분만 확인한다.
static class Program
{
    static int failures;

    static void Check(bool condition, string label)
    {
        if (!condition) { Console.WriteLine("  FAIL: " + label); failures++; }
    }

    static void Main()
    {
        TestDoorPlacement();
        TestLPathContiguity();
        TestRoomGraph();
        TestAutoRunner();
        TestDirectionLatch();
        TestDiagonalGrace();
        TestButtonPress();
        TestInputFocus();
        TestChaseMap();
        TestRangedShot();
        TestResetRun();
        TestPanelLayout();
        TestAimTargeting();
        TestLongPress();
        TestThrowLanding();
        TestGoldThrow();
        TestHungerWarning();
        TestConditionSheet();
        TestTrapSheet();
        TestReadingTime();
        TestMessageLog();
        TestMonsterSheet();
        TestItemSheet();
        TestWarpDestination();
        TestBlowOutcome();
        TestBedrock();
        TestTerrainFromAsset();
        TestConditionTracker();
        TestItemMark();
        TestItemOrder();
        TestWeightedPick();
        TestItemEnhancement();
        TestSheetIndex();
        TestExtensionPoints();
        TestPartnerSheet();
        TestPartnerLines();
        TestSpentPress();
        TestPartnerRoster();
        TestPartnerPlan();

        Console.WriteLine(failures == 0
            ? "\nALL PASS"
            : $"\n{failures} FAILURES");
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    // ── 문이 늘 벽 위에 있고, 방 안쪽과 바깥에 각각 맞닿는가 ────────────────
    static void TestDoorPlacement()
    {
        Console.WriteLine("[문 배치]");
        var carver = typeof(CorridorCarver);
        var open = carver.GetMethod("OpenDoorToward", BindingFlags.NonPublic | BindingFlags.Static);

        int cases = 0;
        var rnd = new System.Random(1);
        for (int t = 0; t < 5000; t++)
        {
            var a = new RoomInfo(rnd.Next(1, 30), rnd.Next(1, 30), rnd.Next(5, 11), rnd.Next(5, 11));
            var b = new RoomInfo(rnd.Next(1, 40), rnd.Next(1, 40), rnd.Next(5, 11), rnd.Next(5, 11));

            var door = (Door)open.Invoke(null, new object[] { a, b });
            cases++;

            bool onVerticalWall = (door.Position.x == a.Bounds.xMin || door.Position.x == a.Bounds.xMax)
                && door.Position.y >= a.InteriorMinY && door.Position.y <= a.InteriorMaxY;
            bool onHorizontalWall = (door.Position.y == a.Bounds.yMin || door.Position.y == a.Bounds.yMax)
                && door.Position.x >= a.InteriorMinX && door.Position.x <= a.InteriorMaxX;
            Check(onVerticalWall || onHorizontalWall, $"문이 벽 위에 있지 않음 {door.Position}");

            // 문에 붙은 네 칸 중 하나는 반드시 방 안쪽이어야 한다(그래야 드나들 수 있다)
            bool touchesInterior = false;
            foreach (var d in new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down })
                if (a.ContainsInterior(door.Position.x + d.x, door.Position.y + d.y)) touchesInterior = true;
            Check(touchesInterior, "문이 방 안쪽과 맞닿지 않음");

            // 바깥 칸은 방 밖이어야 한다
            Check(!a.ContainsInterior(door.Outside.x, door.Outside.y), "문 바깥 칸이 방 안쪽임");
            Check((door.Outside - door.Position).sqrMagnitude == 1, "문 바깥 칸이 인접하지 않음");
        }

        // 같은 방향으로 두 번 요청하면 같은 문을 재사용해야 한다
        var r1 = new RoomInfo(5, 5, 7, 7);
        var r2 = new RoomInfo(25, 5, 7, 7);
        open.Invoke(null, new object[] { r1, r2 });
        open.Invoke(null, new object[] { r1, r2 });
        Check(r1.Doors.Count == 1, $"문이 중복 생성됨 ({r1.Doors.Count}개)");

        Console.WriteLine($"  {cases} 케이스 검사 완료");
    }

    // ── 통로 경로가 한 칸씩 끊기지 않고 이어지는가 ─────────────────────────
    static void TestLPathContiguity()
    {
        Console.WriteLine("[통로 경로]");
        var build = typeof(CorridorCarver).GetMethod("BuildLPath", BindingFlags.NonPublic | BindingFlags.Static);

        var rnd = new System.Random(2);
        for (int t = 0; t < 5000; t++)
        {
            var from = new Vector3Int(rnd.Next(0, 50), rnd.Next(0, 50), 0);
            var to = new Vector3Int(rnd.Next(0, 50), rnd.Next(0, 50), 0);
            foreach (bool horizontalFirst in new[] { true, false })
            {
                var path = (List<Vector3Int>)build.Invoke(null, new object[] { from, to, horizontalFirst });
                Check(path[0] == from, "경로가 시작점에서 출발하지 않음");
                Check(path[path.Count - 1] == to, "경로가 도착점에서 끝나지 않음");

                for (int i = 1; i < path.Count; i++)
                {
                    int step = Mathf.Abs(path[i].x - path[i - 1].x) + Mathf.Abs(path[i].y - path[i - 1].y);
                    Check(step <= 1, $"경로가 끊김 또는 대각선 이동: {path[i - 1]} -> {path[i]}");
                }
            }
        }
        Console.WriteLine("  10000 경로 검사 완료");
    }

    // ── 달리기 상태 기계 ───────────────────────────────────────────────────
    static void TestAutoRunner()
    {
        Console.WriteLine("[달리기 상태]");
        var runner = new AutoRunner();

        Check(!runner.IsRunning, "처음부터 달리는 중으로 되어 있음");

        runner.Begin(new Vector2(10.5f, 4.5f), Vector2.right, insideRoom: true, hp: 20f);
        Check(runner.IsRunning, "Begin 후에도 달리는 중이 아님");
        Check(runner.StartedInsideRoom, "시작 시 방 안 여부가 기록되지 않음");
        Check(runner.StepsTaken == 0, "시작 직후 걸음 수가 0이 아님");

        // 한 걸음마다 칸이 방향만큼 정확히 옮겨져야 한다
        runner.RecordStep(20f);
        Check(runner.Cell == new Vector2(11.5f, 4.5f), $"한 걸음 뒤 위치가 틀림 {runner.Cell}");
        Check(runner.StepsTaken == 1, "걸음 수가 세어지지 않음");

        // 달리기는 무조건 직진이다 — 방향이 도중에 바뀌지 않아야 한다
        runner.RecordStep(20f);
        Check(runner.Direction == Vector2.right, "달리는 도중 방향이 바뀜");
        Check(runner.Cell == new Vector2(12.5f, 4.5f), $"두 걸음 뒤 위치가 틀림 {runner.Cell}");

        // 상한에 반드시 걸려야 한다(무한 달리기 방지)
        while (runner.CanTakeAnotherStep) runner.RecordStep(20f);
        Check(runner.StepsTaken == AutoRunner.MaxSteps,
              $"상한이 지켜지지 않음 {runner.StepsTaken}");

        runner.Stop(RunStopReason.Junction);
        Check(!runner.IsRunning, "Stop 후에도 달리는 중");
        Check(runner.LastStopReason == RunStopReason.Junction, "정지 사유가 기록되지 않음");

        // 이미 멈춘 뒤의 Stop 은 첫 번째 사유를 덮어쓰지 않아야 한다
        runner.Stop(RunStopReason.HitWall);
        Check(runner.LastStopReason == RunStopReason.Junction, "정지 사유가 덮어써짐");

        Console.WriteLine("  상태 전이 검사 완료");
    }

    // ── 방향키 잠금: "달리기가 멈춘 자리에서 한 칸 더 가는" 버그 방지 ───────
    static void TestDirectionLatch()
    {
        Console.WriteLine("[방향키 잠금]");
        // 유예를 끄고 잠금 규칙만 본다
        var input = new DirectionInput { DiagonalGraceSeconds = 0f };

        Check(input.Resolve(1, 0, false, 0f, out Vector2 d) && d == Vector2.right, "평소 입력이 읽히지 않음");

        // 달리기가 멈추면서 잠금이 걸린다. 키를 계속 누르고 있어도 무시돼야 한다.
        input.RequireRelease();
        for (int frame = 0; frame < 10; frame++)
            Check(!input.Resolve(1, 0, false, frame * 0.016f, out _), $"잠금 중인데 입력이 통과함 (프레임 {frame})");

        // 손을 떼면 잠금이 풀린다
        Check(!input.Resolve(0, 0, false, 1f, out _), "뗀 상태인데 입력이 있다고 함");
        Check(input.Resolve(1, 0, false, 1.1f, out d) && d == Vector2.right, "뗀 뒤에도 잠금이 안 풀림");

        // 다른 방향으로 바꿔 눌러도 잠금은 유지돼야 한다
        input.RequireRelease();
        Check(!input.Resolve(0, 1, false, 1.2f, out _), "잠금 중 다른 방향이 통과함");
        Check(!input.Resolve(-1, -1, false, 1.3f, out _), "잠금 중 대각선이 통과함");

        // Shift(대각선 전용)는 축 입력을 걸러야 한다
        var fresh = new DirectionInput();
        Check(!fresh.Resolve(1, 0, true, 0f, out _), "대각선 전용인데 축 입력이 통과함");
        Check(fresh.Resolve(1, 1, true, 0f, out d) && d == new Vector2(1, 1), "대각선 입력이 막힘");

        Console.WriteLine("  잠금 규칙 검사 완료");
    }

    // ── 대각선 유예: 두 키를 '거의' 동시에 눌렀을 때 ───────────────────────
    static void TestDiagonalGrace()
    {
        Console.WriteLine("[대각선 유예]");
        const float grace = 0.06f;
        const float frame = 1f / 60f;

        // 1) 실제 상황: 오른쪽을 누르고 30ms 뒤에 위를 누른다 (사람의 평균적인 간격)
        var input = new DirectionInput { DiagonalGraceSeconds = grace };
        float t = 0f;
        int straightSteps = 0;
        Vector2 committed = Vector2.zero;

        for (int i = 0; i < 20; i++, t += frame)
        {
            int h = 1;                       // 오른쪽은 계속 눌려 있음
            int v = t >= 0.030f ? 1 : 0;     // 30ms 뒤부터 위도 눌림

            if (!input.Resolve(h, v, false, t, out Vector2 dir)) continue;

            committed = dir;
            if (dir.y == 0) straightSteps++;
            break;
        }

        Check(straightSteps == 0, "대각선을 의도했는데 먼저 직선으로 한 칸 갔다");
        Check(committed == new Vector2(1, 1), $"대각선으로 확정되지 않음: {committed}");

        // 2) 정말로 한 방향만 누른 경우: 유예 시간이 지나면 그 방향으로 확정돼야 한다
        var single = new DirectionInput { DiagonalGraceSeconds = grace };
        t = 0f;
        Vector2 result = Vector2.zero;
        bool moved = false;
        for (int i = 0; i < 20; i++, t += frame)
        {
            if (single.Resolve(1, 0, false, t, out result)) { moved = true; break; }
        }
        Check(moved, "한 방향만 눌렀는데 영영 움직이지 않음");
        Check(result == Vector2.right, $"방향이 틀림: {result}");
        Check(t <= grace + frame * 2, $"확정이 너무 늦음 ({t * 1000f:F0}ms)");

        // 3) 두 키가 같은 프레임에 들어오면 기다리지 않고 즉시 확정
        var instant = new DirectionInput { DiagonalGraceSeconds = grace };
        Check(instant.Resolve(1, 1, false, 0f, out Vector2 both) && both == new Vector2(1, 1),
              "같은 프레임 대각선인데 즉시 확정되지 않음");

        // 4) 이미 누르고 있는 중(연속 이동)에는 지연이 없어야 한다
        var held = new DirectionInput { DiagonalGraceSeconds = grace };
        held.Resolve(1, 0, false, 0f, out _);              // 첫 입력 → 대기 시작
        held.Resolve(1, 0, false, grace + 0.01f, out _);   // 확정
        Check(held.Resolve(1, 0, false, grace + 0.02f, out Vector2 next) && next == Vector2.right,
              "연속 이동에 불필요한 지연이 생김");

        // 5) 유예를 끄면 예전처럼 즉시 확정된다
        var off = new DirectionInput { DiagonalGraceSeconds = 0f };
        Check(off.Resolve(1, 0, false, 0f, out Vector2 now) && now == Vector2.right,
              "유예를 껐는데도 지연됨");

        Console.WriteLine($"  30ms 시차 두 키 → 대각선 확정 · 단일 입력 {grace * 1000f:F0}ms 내 확정");
    }

    // ── 버튼 판정: 조준 / 달리기 / 대기가 서로 새지 않는가 ─────────────────
    static void TestButtonPress()
    {
        Console.WriteLine("[버튼 판정]");

        // 1) 아무 일에도 안 쓰고 뗐다 → 조준(Tapped)
        var b = new ButtonPress();
        b.Update(down: true, held: true, up: false);
        Check(b.Pressed && !b.Tapped, "누르자마자 Tapped 가 나옴");

        for (int i = 0; i < 100; i++) b.Update(false, true, false);   // 오래 누르고 있어도
        Check(!b.Tapped, "누르고 있는 중에 Tapped 가 나옴");

        b.Update(false, false, true);
        Check(b.Tapped, "쓰이지 않고 뗐는데 Tapped 가 안 나옴");

        // 시간을 재지 않으므로, 오래 눌렀다 떼도 여전히 Tapped 여야 한다
        // (조준이 '얼마나 빨리 떼는가' 에 좌우되지 않는다)

        // 2) 달리기에 쓰인 경우 → 뗄 때 조준이 따라 나오면 안 된다
        var run = new ButtonPress();
        run.Update(true, true, false);
        run.Consume();                                   // 방향을 밀어서 달리기 시작
        Check(!run.Pressed, "소비했는데 Pressed 가 남아 있음");

        for (int i = 0; i < 30; i++) run.Update(false, true, false);
        run.Update(false, false, true);
        Check(!run.Tapped, "달린 뒤 뗄 때 조준이 켜짐");

        // 3) 다시 누르면 정상 동작해야 한다(소비 상태가 남지 않음)
        run.Update(true, true, false);
        run.Update(false, false, true);
        Check(run.Tapped, "한 번 소비한 뒤 다음 눌림이 먹지 않음");

        // 4) A + B 동시 판정: 누르는 순서가 어느 쪽이든 잡혀야 한다
        var a = new ButtonPress();
        var s2 = new ButtonPress();

        // B 를 누르고 있는 상태에서 A 를 누름
        s2.Update(true, true, false);
        a.Update(true, true, false);
        Check((a.Pressed && s2.Held) || (s2.Pressed && a.Held), "B 먼저 → A 동시 입력이 안 잡힘");

        // 반대 순서
        var a2 = new ButtonPress();
        var s3 = new ButtonPress();
        a2.Update(true, true, false);
        a2.Update(false, true, false);
        s3.Update(true, true, false);
        Check((a2.Pressed && s3.Held) || (s3.Pressed && a2.Held), "A 먼저 → B 동시 입력이 안 잡힘");

        // 5) A 단독은 동시 입력으로 잡히면 안 된다(공격이 대기로 바뀌면 곤란)
        var soloA = new ButtonPress();
        var idleB = new ButtonPress();
        soloA.Update(true, true, false);
        idleB.Update(false, false, false);
        Check(!((soloA.Pressed && idleB.Held) || (idleB.Pressed && soloA.Held)),
              "A 만 눌렀는데 동시 입력으로 잡힘");

        Console.WriteLine("  조준 · 달리기 소비 · A+B 동시(양방향) 검사 완료");
    }

    // ── 입력 소유권: "가방 중에 조준 토글이 먹는" 버그 방지 ─────────────────
    static void TestInputFocus()
    {
        Console.WriteLine("[입력 소유권]");

        InputFocus.Reset();
        Check(InputFocus.PlayerHasFocus, "초기 상태에서 플레이어가 권한을 못 가짐");

        InputFocus.Give(InputOwner.BagMenu);
        Check(!InputFocus.PlayerHasFocus, "가방이 열렸는데 플레이어가 여전히 권한을 가짐");
        Check(InputFocus.Owner == InputOwner.BagMenu, "소유자가 기록되지 않음");

        InputFocus.Give(InputOwner.Prompt);
        Check(!InputFocus.PlayerHasFocus, "선택창 중에 플레이어가 권한을 가짐");

        InputFocus.ReturnToPlayer();
        Check(InputFocus.PlayerHasFocus, "창을 닫았는데 권한이 안 돌아옴");

        InputFocus.Give(InputOwner.BagMenu);
        InputFocus.Reset();
        Check(InputFocus.PlayerHasFocus, "층을 넘길 때 권한이 초기화되지 않음");

        Console.WriteLine("  소유권 전이 검사 완료");
    }

    // ── 추격 길찾기 ────────────────────────────────────────────────────────

    /// <summary>ASCII 그림으로 만든 가짜 격자. '#'=벽, 그 외=바닥.</summary>
    // ── 조준 모드가 겨눌 적을 제대로 고르는가 ──────────────────────────────
    //
    // 2순위 조건: 벽에 가리지 않고 여덟 방향 중 하나로 곧게 놓인 적.
    // 같은 방 / 마주 보이는 통로가 자연히 여기 걸리고, 벽 뒤는 걸리지 않아야 한다.
    static void TestAimTargeting()
    {
        Console.WriteLine("[조준 대상 고르기]");

        Vector2Int NoFacing = Vector2Int.zero;
        Vector2Int dir;

        // 1) 같은 방 안에서 곧게 놓인 적을 겨눈다.
        var room = new AsciiGrid(new[]
        {
            "#########",
            "#.......#",
            "#..E....#",     // 적이 P 의 바로 위쪽 줄에 있다
            "#.......#",
            "#..P....#",
            "#########",
        });
        var p = room.Find('P');
        var e = room.Find('E');
        Check(AimTargeting.TryFindInLine(room, p, new List<Vector3Int> { e }, NoFacing, 20, out dir),
              "같은 방의 곧게 놓인 적을 못 찾음");
        Check(dir == new Vector2Int(0, 1), $"방향이 위쪽이 아님 {dir}");

        // 2) 곧게 놓이지 않은 적(나이트 이동처럼 어긋난 자리)은 겨누지 않는다.
        var skew = new AsciiGrid(new[]
        {
            "#########",
            "#....E..#",     // P 에서 오른쪽 2, 위 3 — 여덟 방향 어디에도 안 놓임
            "#.......#",
            "#.......#",
            "#..P....#",
            "#########",
        });
        Check(!AimTargeting.TryFindInLine(skew, skew.Find('P'), new List<Vector3Int> { skew.Find('E') },
                                          NoFacing, 20, out dir),
              "여덟 방향에 놓이지 않은 적을 겨눔");

        // 3) 벽 뒤의 적은 곧게 놓여 있어도 겨누지 않는다.
        var walled = new AsciiGrid(new[]
        {
            "#########",
            "#..E....#",
            "#..#....#",     // 사이에 벽
            "#.......#",
            "#..P....#",
            "#########",
        });
        Check(!AimTargeting.TryFindInLine(walled, walled.Find('P'), new List<Vector3Int> { walled.Find('E') },
                                          NoFacing, 20, out dir),
              "벽 뒤의 적을 겨눔");

        // 4) 통로에서 멀리 마주 보는 적도 겨눈다(같은 방이 아니어도 된다).
        var corridor = new AsciiGrid(new[]
        {
            "###########",
            "#P.......E#",
            "###########",
        });
        Check(AimTargeting.TryFindInLine(corridor, corridor.Find('P'),
                                         new List<Vector3Int> { corridor.Find('E') }, NoFacing, 20, out dir),
              "일직선 통로 저편의 적을 못 찾음");
        Check(dir == new Vector2Int(1, 0), $"방향이 오른쪽이 아님 {dir}");

        // 5) 여럿이면 가까운 쪽을 겨눈다.
        var many = new AsciiGrid(new[]
        {
            "###########",
            "#B...P..A.#",     // A 는 3칸, B 는 4칸
            "###########",
        });
        var pm = many.Find('P');
        Check(AimTargeting.TryFindInLine(many, pm,
                                         new List<Vector3Int> { many.Find('B'), many.Find('A') },
                                         NoFacing, 20, out dir),
              "여럿 중 아무도 못 찾음");
        Check(dir == new Vector2Int(1, 0), $"가까운 쪽(오른쪽)을 안 겨눔 {dir}");

        // 5-b) 거리 규칙이 방향 취향보다 앞선다는 것을 실제로 가르는 경우.
        //      먼 적이 '지금 바라보는 쪽' 에 있어서 점수로는 그쪽이 이긴다.
        //      거리를 먼저 보지 않으면 먼 적을 겨누게 된다.
        var farInFront = new AsciiGrid(new[]
        {
            "############",
            "#B..P.....A#",     // B 는 왼쪽 3칸, A 는 오른쪽 6칸
            "############",
        });
        var pf = farInFront.Find('P');
        Check(AimIn(farInFront, pf,
                    new List<Vector3Int> { farInFront.Find('A'), farInFront.Find('B') },
                    new Vector2Int(1, 0), out dir),          // 오른쪽(먼 적 쪽)을 보는 중
              "가까운 적이 뒤에 있을 때 아무도 못 찾음");
        Check(dir == new Vector2Int(-1, 0),
              $"바라보는 쪽의 먼 적을 겨눔. 가까운 적(뒤쪽)이 우선이어야 한다 {dir}");

        // 6) 거리가 같으면 지금 바라보는 쪽을 겨눈다.
        var tie = new AsciiGrid(new[]
        {
            "#######",
            "#A.P.B#",       // 좌우로 똑같이 2칸
            "#######",
        });
        var pt = tie.Find('P');
        var both = new List<Vector3Int> { tie.Find('A'), tie.Find('B') };
        Check(AimIn(tie, pt, both, new Vector2Int(-1, 0), out dir),
              "왼쪽을 보는 중인데 아무도 못 찾음");
        Check(dir == new Vector2Int(-1, 0), $"바라보는 쪽(왼쪽)을 안 겨눔 {dir}");
        Check(AimIn(tie, pt, both, new Vector2Int(1, 0), out dir),
              "오른쪽을 보는 중인데 아무도 못 찾음");
        Check(dir == new Vector2Int(1, 0), $"바라보는 쪽(오른쪽)을 안 겨눔 {dir}");

        // 7) 조준과 실제 투사체가 어긋나면 안 된다.
        //    벽이 대각선으로 맞물린 틈은 몸은 못 지나가지만 투사체는 지나간다.
        //    겨눌 수 있다고 했으면 실제로 맞아야 한다.
        var gap = new AsciiGrid(new[]
        {
            "#####",
            "#.#.#",
            "##P##",
            "#E#.#",
            "#####",
        });
        var pg = gap.Find('P');
        var eg = gap.Find('E');
        Check(AimTargeting.TryFindInLine(gap, pg, new List<Vector3Int> { eg }, NoFacing, 5, out dir),
              "대각선 틈 너머의 적을 못 겨눔");
        var shot = RangedShot.Trace(gap, pg, dir, 5, new List<Vector3Int> { eg });
        Check(shot.HitTarget && shot.ImpactCell == eg,
              "겨눌 수 있다고 했는데 실제로는 맞지 않음");

        // 8) 적이 없으면 아무것도 고르지 않는다(기본값인 '바라보는 쪽' 으로 남는다).
        Check(!AimTargeting.TryFindInLine(room, p, new List<Vector3Int>(), NoFacing, 20, out dir),
              "적이 없는데 방향을 골랐음");

        // 9) 조준 옮기기: 겨눌 수 있는 방향을 빠짐없이, 늘 같은 순서로 돈다.
        var around = new AsciiGrid(new[]
        {
            "#######",
            "#..E..#",       // 위
            "#.....#",
            "#E.P.E#",       // 왼쪽 · 오른쪽
            "#.....#",
            "#..E..#",       // 아래
            "#######",
        });
        var pa = around.Find('P');
        var four = new List<Vector3Int>
        {
            new Vector3Int(3, 5, 0),   // 위
            new Vector3Int(1, 3, 0),   // 왼쪽
            new Vector3Int(5, 3, 0),   // 오른쪽
            new Vector3Int(3, 1, 0),   // 아래
        };

        var aimable = AimTargeting.CollectAimable(around, pa, four, 10);
        Check(aimable.Count == 4, $"겨눌 수 있는 방향 수가 다름 {aimable.Count} (기대 4)");

        // 네 번 옮기면 처음으로 돌아와야 한다. 도중에 같은 방향이 두 번 나오면 안 된다.
        var start = new Vector2Int(0, 1);
        var seen = new List<Vector2Int> { start };
        var at = start;
        for (int i = 0; i < 4; i++)
        {
            Check(AimTargeting.TryCycle(aimable, at, out at), $"{i + 1}번째 옮기기에서 멈춤");
            if (i < 3)
                Check(!seen.Contains(at), $"이미 겨눴던 방향으로 되돌아감 {at}");
            seen.Add(at);
        }
        Check(at == start, $"네 번 옮겼는데 처음으로 안 돌아옴 {at}");

        // 10) 겨눌 적이 하나뿐이면 옮기지 않는다.
        var single = AimTargeting.CollectAimable(corridor, corridor.Find('P'),
                                                 new List<Vector3Int> { corridor.Find('E') }, 20);
        Check(single.Count == 1, $"통로에서 겨눌 방향이 1개가 아님 {single.Count}");
        Check(!AimTargeting.TryCycle(single, new Vector2Int(1, 0), out _),
              "겨눌 적이 하나뿐인데 조준이 옮겨짐");

        // 10-b) 겨눌 적이 아예 없어도 옮기지 않는다.
        //       9·10·10-b 가 false 인 경우들이고, 이때 부르는 쪽(Player)이 조준을 끝낸다.
        Check(!AimTargeting.TryCycle(new List<Vector2Int>(), new Vector2Int(0, 1), out _),
              "겨눌 적이 없는데 조준이 옮겨짐");

        // 11) 허공을 겨누고 있었다면 첫 번째 적으로 간다.
        Check(AimTargeting.TryCycle(aimable, new Vector2Int(1, 1), out Vector2Int fromEmpty),
              "허공을 겨누다가 옮기지 못함");
        Check(fromEmpty == aimable[0], $"첫 번째 대상으로 가지 않음 {fromEmpty}");

        Console.WriteLine("  같은 방 · 통로 · 벽 뒤 · 가까운 적 우선 · 대각선 틈 일치 · 조준 옮기기 검사 완료");
    }

    // ── 길게 누름이 시간으로 갈리는가 ──────────────────────────────────────
    //
    // 이 프로젝트에서 시간으로 판정하는 곳은 여기 하나뿐이라 특히 조심해야 한다.
    // 짧게 누른 것이 길게로 새면 조준이 옮겨지는 대신 꺼져 버린다.
    static void TestLongPress()
    {
        Console.WriteLine("[길게 누름]");

        const float frame = 1f / 60f;
        float threshold = GameInput.LongPressSeconds;

        // 짧게 눌렀다 뗀다 — 기준에 닿지 않아야 하고, Tapped 가 나와야 한다.
        var b = new ButtonPress();
        b.Update(down: true, held: true, up: false, deltaTime: frame);
        int shortFrames = (int)(threshold / frame) - 2;
        for (int i = 0; i < shortFrames; i++)
        {
            b.Update(false, true, false, frame);
            Check(b.HeldSeconds < threshold, $"짧게 누르는 중인데 길게로 넘어감 ({b.HeldSeconds:F3}s)");
        }
        b.Update(false, false, true, frame);
        Check(b.Tapped, "짧게 눌렀는데 Tapped 가 나오지 않음");
        Check(b.HeldSeconds == 0f, "손을 뗐는데 누른 시간이 남아 있음");

        // 계속 누르고 있으면 기준을 넘는다.
        var h = new ButtonPress();
        h.Update(true, true, false, frame);
        for (int i = 0; i < 120 && h.HeldSeconds < threshold; i++)
            h.Update(false, true, false, frame);
        Check(h.HeldSeconds >= threshold, "오래 눌러도 길게로 넘어가지 않음");

        // 길게로 넘어간 뒤 소비하면, 손을 뗄 때 Tapped 가 나오지 않아야 한다.
        // (조준을 끈 그 손짓이 곧바로 조준을 옮기는 일을 막는다)
        h.Consume();
        h.Update(false, false, true, frame);
        Check(!h.Tapped, "길게 눌러 소비했는데 뗄 때 Tapped 가 또 나옴");

        // 다시 누르면 시간이 0 부터 다시 센다.
        h.Update(true, true, false, frame);
        Check(h.HeldSeconds == 0f, $"새로 눌렀는데 시간이 이어짐 {h.HeldSeconds}");

        Console.WriteLine($"  기준 {threshold:F2}s · 짧게/길게 · 소비 후 재입력 검사 완료");
    }

    /// <summary>6번 검사용. 거리는 같고 바라보는 쪽만 다른 경우를 짧게 쓰기 위한 것.</summary>
    static bool AimIn(AsciiGrid g, Vector3Int from, List<Vector3Int> targets, Vector2Int facing, out Vector2Int dir)
        => AimTargeting.TryFindInLine(g, from, targets, facing, 20, out dir);

    // ── 던진 것이 떨어진 땅에 따라 어떻게 되는가 ────────────────────────────
    //
    // 없어지는 땅(용암·구멍)에서 바닥 목록에 남겨 두면, 눈에 보이지 않는 물건이
    // 그 칸을 영영 차지한다. 반대로 물에서 없애 버리면 "빠졌다" 는 문구와 달리
    // 되찾을 수 없게 된다. 문구와 실제가 어긋나기 쉬운 자리라 규칙으로 못 박는다.
    static void TestThrowLanding()
    {
        Console.WriteLine("[던진 것의 결말]");

        // 1) 이름 목록이 땅의 종류로 제대로 갈리는가.
        Check(TileRules.KindOfTile("S1Wall") == TileRules.TerrainKind.Wall, "벽 타일이 벽으로 안 갈림");
        Check(TileRules.KindOfTile("S1Water") == TileRules.TerrainKind.Water, "물 타일이 물로 안 갈림");
        Check(TileRules.KindOfTile("S1Lava") == TileRules.TerrainKind.Lava, "용암 타일이 용암으로 안 갈림");
        Check(TileRules.KindOfTile("S1Pit") == TileRules.TerrainKind.Pit, "구멍 타일이 구멍으로 안 갈림");
        Check(TileRules.KindOfTile("EnTile1") == TileRules.TerrainKind.Floor, "평범한 타일이 바닥이 아님");
        Check(TileRules.KindOfTile(null) == TileRules.TerrainKind.Floor, "타일 없는 칸이 바닥이 아님");

        // 2) 용암 - 없어지고, 탔다고 알린다.
        var lava = ItemThrower.ResolveLanding(TileRules.TerrainKind.Lava, false);
        Check(lava.Destroyed, "용암에 떨어졌는데 없어지지 않음");
        Check(lava.Message == GameMessage.ItemBurnedAway, "용암 문구가 다름");

        // 3) 구멍 - 없어지고, 멀리 떨어졌다고 알린다.
        var pit = ItemThrower.ResolveLanding(TileRules.TerrainKind.Pit, false);
        Check(pit.Destroyed, "구멍에 떨어졌는데 없어지지 않음");
        Check(pit.Message == GameMessage.ItemFellFarAway, "구멍 문구가 다름");

        // 4) 물 - 없어지지 않고, 그 자리에 그대로 남아야 되찾을 수 있다.
        var water = ItemThrower.ResolveLanding(TileRules.TerrainKind.Water, false);
        Check(!water.Destroyed, "물에 빠진 것이 없어짐 (되찾을 수 없게 된다)");
        Check(water.RestingTerrain == TileRules.TerrainKind.Water,
              "물에 빠진 것이 물 아닌 땅에 놓이려 함 (뭍으로 올라와 버린다)");
        Check(water.Message == GameMessage.ItemSankIntoWater, "물 문구가 다름");

        // 5) 평범한 바닥 - 예전 그대로. 벽에 맞았는지에 따라 문구만 갈린다.
        var floor = ItemThrower.ResolveLanding(TileRules.TerrainKind.Floor, false);
        Check(!floor.Destroyed, "평범한 바닥에서 없어짐");
        Check(floor.RestingTerrain == TileRules.TerrainKind.Floor, "평범한 바닥의 처리가 바뀜");
        Check(floor.Message == GameMessage.ItemThrown, "땅에 떨어진 문구가 다름");

        var walled = ItemThrower.ResolveLanding(TileRules.TerrainKind.Floor, true);
        Check(walled.Message == GameMessage.ItemHitWall, "벽에 부딪힌 문구가 다름");

        // 6) 없어지는 땅은 벽에 부딪혔든 아니든 결말이 같아야 한다.
        //    (벽 앞 칸이 용암인 경우가 실제로 생긴다)
        foreach (bool hitWall in new[] { false, true })
        {
            Check(ItemThrower.ResolveLanding(TileRules.TerrainKind.Lava, hitWall).Destroyed,
                  $"벽부딪힘={hitWall} 일 때 용암에서 안 없어짐");
            Check(ItemThrower.ResolveLanding(TileRules.TerrainKind.Pit, hitWall).Destroyed,
                  $"벽부딪힘={hitWall} 일 때 구멍에서 안 없어짐");
            Check(!ItemThrower.ResolveLanding(TileRules.TerrainKind.Water, hitWall).Destroyed,
                  $"벽부딪힘={hitWall} 일 때 물에서 없어짐");
        }

        // 7) 남는 것은 반드시 '놓일 수 있는 땅' 을 지정해야 한다.
        //    물에 빠진 것이 바닥을 찾아 나서면 뭍으로 올라오고,
        //    바닥에 떨어진 것이 물을 찾아 나서면 없는 자리를 뒤지게 된다.
        foreach (TileRules.TerrainKind kind in Enum.GetValues(typeof(TileRules.TerrainKind)))
        {
            // 벽 칸에는 애초에 떨어질 수가 없다. RangedShot 이 벽을 만나면 그 '앞' 칸에서
            // 멈추기 때문이다(출발 칸도 벽일 수 없다). 그래서 여기서 따질 대상이 아니다.
            if (kind == TileRules.TerrainKind.Wall) continue;

            var r = ItemThrower.ResolveLanding(kind, false);
            if (r.Destroyed) continue;

            Check(r.RestingTerrain == kind,
                  $"{kind}: 떨어진 땅과 놓일 땅이 다름 ({r.RestingTerrain})");
        }

        // 7-b) 같은 물웅덩이에 두 번 던졌을 때 겹치지 않는가.
        //      물이면 자리 찾기를 건너뛰던 시절의 버그가 되살아나지 않도록 못 박는다.
        var pond = new HashSet<Vector3>();
        Func<Vector3, bool> pondBlocked = c => pond.Contains(c);
        var splash = new Vector3(3f, 3f, 0f);

        Check(ItemThrower.TryFindFreeSpot(splash, pondBlocked, out Vector3 first) && first == splash,
              "빈 물자리인데 옆으로 밀림");
        pond.Add(first);

        Check(ItemThrower.TryFindFreeSpot(splash, pondBlocked, out Vector3 second),
              "두 번째 물건이 놓일 자리를 못 찾음");
        Check(second != first, "같은 물자리에 두 번째 물건이 겹쳐 놓임");

        // 8) 놓을 자리 찾기.
        //    떨어진 칸이 비어 있으면 그대로, 차 있으면 옆 칸, 둘레가 다 막혔으면 실패.
        var occupied = new HashSet<Vector3>();
        Func<Vector3, bool> blocked = c => occupied.Contains(c);
        var here = new Vector3(5f, 5f, 0f);

        Check(ItemThrower.TryFindFreeSpot(here, blocked, out Vector3 spot) && spot == here,
              "빈 칸인데 옆으로 밀어냄");

        // 떨어진 칸만 차 있으면 이웃 여덟 칸 중 하나로 간다.
        occupied.Add(here);
        Check(ItemThrower.TryFindFreeSpot(here, blocked, out spot), "옆 칸이 비었는데 못 찾음");
        Check(spot != here, "차 있는 칸에 그대로 겹쳐 놓음");
        Check(Mathf.Abs(spot.x - here.x) <= 1f && Mathf.Abs(spot.y - here.y) <= 1f,
              $"한 칸 넘게 떨어진 자리로 감 {spot}");

        // 둘레 여덟 칸까지 전부 막히면 실패해야 한다(= 파묻혀 사라진다).
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                occupied.Add(new Vector3(here.x + dx, here.y + dy, 0f));

        Check(!ItemThrower.TryFindFreeSpot(here, blocked, out _),
              "둘레가 전부 막혔는데도 놓을 자리를 찾았다고 함");

        // 여덟 칸 중 하나만 열려 있어도 찾아내야 한다(어느 방향이든).
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                var open = new Vector3(here.x + dx, here.y + dy, 0f);
                occupied.Remove(open);

                Check(ItemThrower.TryFindFreeSpot(here, blocked, out spot),
                      $"({dx},{dy}) 한 칸만 열렸는데 못 찾음");
                Check(spot == open, $"({dx},{dy}) 열린 칸이 아닌 곳을 고름 {spot}");

                occupied.Add(open);
            }
        }

        Console.WriteLine("  물 · 용암 · 구멍 · 평범한 바닥 결말 · 놓을 자리 검사 완료");
    }

    // ── 던진 돈의 피해 ──────────────────────────────────────────────────────
    //
    // 액수를 10 으로 나눈 몫이고, 방어력을 무시한다.
    // 내림이라는 점이 중요하다 — 105 골드로 11 이 나오면 안 된다.
    static void TestGoldThrow()
    {
        Console.WriteLine("[던진 돈]");

        Check(GoldPile.DamageOf(100) == 10, $"100골드 피해가 10이 아님 {GoldPile.DamageOf(100)}");
        Check(GoldPile.DamageOf(413) == 41, $"413골드 피해가 41이 아님 {GoldPile.DamageOf(413)}");
        Check(GoldPile.DamageOf(499) == 49, $"499골드 피해가 49가 아님 {GoldPile.DamageOf(499)}");

        // 내림인지 확인한다. 9 이하가 버려져야 한다.
        for (int extra = 0; extra <= 9; extra++)
        {
            Check(GoldPile.DamageOf(200 + extra) == 20,
                  $"{200 + extra}골드에서 내림이 되지 않음 {GoldPile.DamageOf(200 + extra)}");
        }

        // 실제로 나올 수 있는 액수 범위에서 피해가 음수이거나 터무니없지 않은가.
        for (int amount = GoldPile.MinAmount; amount < GoldPile.MaxAmount; amount++)
        {
            int damage = GoldPile.DamageOf(amount);
            Check(damage >= 0, $"{amount}골드 피해가 음수 {damage}");
            Check(damage * GoldPile.DamageDivisor <= amount, $"{amount}골드 피해가 올림됨 {damage}");
        }

        // 화면에 보여 줄 이름에 액수가 들어가는가.
        Check(GoldPile.DisplayName(413) == "골드(413)", $"이름이 다름 {GoldPile.DisplayName(413)}");

        Console.WriteLine($"  {GoldPile.MinAmount}~{GoldPile.MaxAmount}골드 피해 · 표시 이름 검사 완료");
    }

    // ── 만복도 경고가 제때, 한 번씩만 뜨는가 ───────────────────────────────
    //
    // 배고픔은 다섯 턴에 1씩 줄어드는 느린 변화라 눈으로 확인하기 어렵다.
    // 경계마다 정확히 한 번씩 뜨는지, 더 위험한 단계가 덜 위험한 것에 가려지지
    // 않는지를 여기서 본다.
    static void TestHungerWarning()
    {
        Console.WriteLine("[만복도 경고]");

        const int max = 100;
        HungerWarning.Level L(int prev, int cur) => HungerWarning.For(prev, cur, max);

        // 1) 경계를 넘어서는 그 한 번에만 뜬다.
        Check(L(21, 20) == HungerWarning.Level.Rumble, "20%에서 꼬르륵이 안 뜸");
        Check(L(20, 19) == HungerWarning.Level.None, "20% 아래에서 또 뜸");
        Check(L(11, 10) == HungerWarning.Level.Dizzy, "10%에서 아찔함이 안 뜸");
        Check(L(10, 9) == HungerWarning.Level.None, "10% 아래에서 또 뜸");
        Check(L(3, 2) == HungerWarning.Level.Limit, "2 남았을 때 한계가 안 뜸");
        Check(L(2, 1) == HungerWarning.Level.MustEat, "1 남았을 때 문구가 안 뜸");
        Check(L(1, 0) == HungerWarning.Level.Starved, "0에서 아사 문구가 안 뜸");

        // 2) 한 칸씩 100 → 0 까지 내려가며, 각 문구가 정확히 한 번씩만 나오는가.
        var seen = new Dictionary<HungerWarning.Level, int>();
        for (int h = max; h > 0; h--)
        {
            var level = L(h, h - 1);
            if (level == HungerWarning.Level.None) continue;
            seen[level] = seen.TryGetValue(level, out int n) ? n + 1 : 1;
        }
        foreach (HungerWarning.Level level in Enum.GetValues(typeof(HungerWarning.Level)))
        {
            if (level == HungerWarning.Level.None) continue;
            Check(seen.TryGetValue(level, out int count) && count == 1,
                  $"{level} 이 {(seen.ContainsKey(level) ? seen[level] : 0)}번 나옴 (1번이어야 함)");
        }

        // 3) 여러 단계를 한 번에 건너뛰면 가장 위험한 것만 알린다.
        Check(L(20, 0) == HungerWarning.Level.Starved, "20에서 0으로 떨어졌는데 아사가 아님");
        Check(L(50, 5) == HungerWarning.Level.Dizzy, "50에서 5로 떨어졌는데 아찔함이 아님");

        // 4) 먹어서 늘어난 경우에는 아무 말도 하지 않는다.
        Check(L(5, 80) == HungerWarning.Level.None, "먹었는데 경고가 뜸");
        Check(L(10, 10) == HungerWarning.Level.None, "변화가 없는데 경고가 뜸");

        // 5) 최대치가 늘어나면 경고 지점도 함께 따라온다.
        Check(HungerWarning.For(41, 40, 200) == HungerWarning.Level.Rumble,
              "최대 200일 때 20%(=40)에서 꼬르륵이 안 뜸");
        Check(HungerWarning.For(21, 20, 200) == HungerWarning.Level.Dizzy,
              "최대 200일 때 10%(=20)에서 아찔함이 안 뜸");

        // 6) 문구가 단계마다 다 달라야 한다. 하나라도 겹치면 어느 단계인지 알 수 없다.
        var messages = new HashSet<GameMessage>();
        foreach (HungerWarning.Level level in Enum.GetValues(typeof(HungerWarning.Level)))
        {
            if (level == HungerWarning.Level.None) continue;
            Check(messages.Add(HungerWarning.MessageOf(level)), $"{level} 의 문구가 다른 단계와 겹침");
        }

        Console.WriteLine("  20% · 10% · 2 · 1 · 0 단계 검사 완료");
    }

    // ── 엑셀 표에서 읽어 온 줄을 옮겨 담기, 그리고 상태 화면 ───────────────
    //
    // 엑셀은 사람이 손으로 채우는 곳이라 칸 끝에 탭·공백이 남고, 오타도 난다.
    // 그런 줄 하나가 나머지를 통째로 죽이면 표를 고치기가 더 어려워진다.
    static void TestConditionSheet()
    {
        Console.WriteLine("[상태이상 표]");

        var rows = new List<ConditionEntity>
        {
            new ConditionEntity { id = 20001, id2 = "poison", name = "독",
                explanation = "독에 의한 데미지를 입고 있어",
                type = "해로움", effect = "턴당피해", value = 1, turn = 10 },

            // 엑셀에서 붙여 넣다 남은 탭·공백. id 가 미묘하게 달라지면 코드가 못 찾는다.
            new ConditionEntity { id = 10001, id2 = "critup\t ", name = "치명타상승",
                explanation = "평소보다 치명타가 잘 나오겠어",
                type = "이로움", effect = "없음", value = 0, turn = 20 },

            new ConditionEntity { id = 20002, id2 = "hungry", name = "배고픔",
                explanation = "배고픔을 느끼고 있어",
                type = "해로움", effect = "없음", value = 0, turn = 0 },

            // 효과 이름을 잘못 적은 줄. 버리지 말고 '없음' 으로 두고 알려 준다.
            new ConditionEntity { id = 20003, id2 = "typo", name = "오타",
                explanation = "무언가 이상하다", type = "해로움",
                effect = "턴당대미지", value = 0, turn = 0 },

            new ConditionEntity { id = 20004, id2 = "   ", name = "빈 줄", explanation = "" },
        };

        var problems = new List<string>();
        var defs = ConditionSheet.FromEntities(rows, problems);

        // 1) id 가 빈 줄만 버리고 나머지는 살린다.
        Check(defs.Count == 4, $"옮겨 담은 줄 수가 다름 {defs.Count} (기대 4)");

        // 2) 칸 끝의 탭·공백을 털어 낸다.
        Check(defs[1].Id == "critup", $"id 의 탭이 안 지워짐: '{defs[1].Id}'");

        // 2-1) 코드가 쓰는 이름은 표 번호가 아니라 id2 다.
        //
        //      시트에서 id 가 숫자로 바뀌었을 때 이것을 놓치면 Id 가 "20002" 가 되어
        //      ConditionCatalog.HungryId("hungry") 로 찾을 수 없게 된다. 화면에서
        //      배고픔·굶주림이 조용히 사라지고, 에러는 하나도 나지 않는다.
        Check(defs[2].Id == "hungry", $"코드용 이름이 id2 가 아님: '{defs[2].Id}'");
        Check(defs[2].Number == 20002, $"표 번호가 안 실림 {defs[2].Number}");
        Check(ConditionCatalog.HungryId == "hungry" && ConditionCatalog.StarvingId == "starving",
              "코드가 가리키는 이름이 시트의 id2 값과 어긋남");

        // 3) 값이 제대로 옮겨졌는가.
        Check(defs[0].Effect == ConditionEffect.DamagePerTurn, "효과를 못 알아봄");
        Check(defs[0].Amount == 1 && defs[0].DurationTurns == 10, "숫자 열이 어긋남");
        Check(defs[1].Kind == ConditionKind.Beneficial, "이로움을 못 알아봄");
        Check(defs[2].Effect == ConditionEffect.None, "'없음' 을 못 알아봄");

        // 4) 모르는 효과는 '없음' 으로 두되, 무엇이 이상한지 알려 준다.
        Check(defs[3].Effect == ConditionEffect.None, "모르는 효과가 '없음' 이 아님");
        Check(problems.Count == 1, $"이상한 줄을 {problems.Count}건 알림 (기대 1)");
        Check(problems[0].Contains("typo"), "어느 줄이 이상한지 안 알려 줌");

        // 5) 빈 표에도 넘어지지 않는다.
        Check(ConditionSheet.FromEntities(null).Count == 0, "null 에서 줄이 나옴");
        Check(ConditionSheet.FromEntities(new List<ConditionEntity>()).Count == 0, "빈 표에서 줄이 나옴");

        // 6) 표시 순서: 해로운 것이 먼저, 같은 분류 안에서는 시트 차례.
        var sorted = ConditionCatalog.Sort(defs);
        Check(sorted[0].Id == "poison" && sorted[1].Id == "hungry"
              && sorted[2].Id == "typo" && sorted[3].Id == "critup",
              "표시 순서가 해로움 -> 이로움 이 아님: " +
              string.Join(",", sorted.ConvertAll(d => d.Id)));

        // ── 화면에 늘어놓기 ────────────────────────────────────────────────
        var none = new List<ConditionDefinition>();

        Check(ConditionReport.BuildText(none, 0, 5) == ConditionReport.HealthyText,
              "이상이 없는데 기본 문구가 아님");
        Check(ConditionReport.BuildText(null, 0, 5) == ConditionReport.HealthyText,
              "목록이 null 인데 기본 문구가 아님");

        string all = ConditionReport.BuildText(sorted, 0, 5);
        Check(!all.Contains(ConditionReport.HealthyText), "이상이 있는데 기본 문구가 남아 있음");
        Check(all.StartsWith("1. 독에 의한 데미지를 입고 있어"), "목록 형식이 다름:\n" + all);

        // 번호는 화면이 아니라 목록 기준이다.
        Check(ConditionReport.BuildText(sorted, 1, 2).StartsWith("2. "),
              "한 칸 내렸는데 번호가 1부터 다시 시작함");

        // 끝까지 내려도 화면은 꽉 채운다.
        string bottom = ConditionReport.BuildText(sorted, 99, 2);
        Check(bottom.StartsWith("3. "), $"끝까지 내렸을 때 시작 번호가 틀림: {bottom}");
        Check(bottom.Contains("4. "), "끝까지 내렸는데 마지막 항목이 안 보임");

        // 스크롤은 목록 밖으로 나가지 않는다.
        Check(PageScroll.ClampOffset(-5, 3, 2) == 0, "위로 넘겨서 음수가 됨");
        Check(PageScroll.ClampOffset(99, 3, 2) == 1, "아래로 넘겨서 빈 화면이 됨");
        Check(PageScroll.ClampOffset(1, 3, 5) == 0, "다 들어가는데 스크롤이 됨");
        Check(PageScroll.ClampOffset(0, 0, 5) == 0, "빈 목록에서 스크롤이 생김");

        // 어떤 줄 수에서도 마지막 항목까지 볼 수 있어야 한다.
        var many = new List<ConditionDefinition>();
        for (int i = 0; i < 12; i++)
            many.Add(new ConditionDefinition { Id = "c" + i, Description = "상태 " + i });

        for (int capacity = 1; capacity <= 6; capacity++)
        {
            int last = PageScroll.ClampOffset(int.MaxValue, many.Count, capacity);
            string tail = ConditionReport.BuildText(many, last, capacity);
            Check(tail.Contains($"{many.Count}. "),
                  $"capacity={capacity} 일 때 끝까지 내려도 마지막 줄이 안 보임");
            Check(tail.Split('\n').Length == Math.Min(capacity, many.Count),
                  $"capacity={capacity} 일 때 줄 수가 다름");
        }

        Console.WriteLine("  옮겨 담기 · 지저분한 칸 · 이상한 줄 알림 · 표시 순서 · 스크롤 검사 완료");
    }

    // ── 함정 표 ────────────────────────────────────────────────────────────
    //
    // 함정은 확률로 작동하고 효과도 제각각이라, 에디터에서 몇 번 밟아 보는 것으로는
    // 표가 제대로 읽혔는지 알 수 없다. 특히 띄울 문구가 비어 있는 줄은 "가끔 아무
    // 말도 없네" 로만 보여서 버그로 알아채기까지 오래 걸린다. 그래서 여기서 본다.
    static void TestTrapSheet()
    {
        Console.WriteLine("[함정 표]");

        var rows = new List<TrapEntity>
        {
            new TrapEntity { id = 10001, id2 = "dart", name = "독침",
                explanation = "어디선가 독침이 날라왔다!", charani = "피격", note = "힘이 감소" },

            new TrapEntity { id = 10006, id2 = "spin", name = "회전",
                explanation = "빙글빙글 돌았다!", charani = "빙글빙글", note = "혼란 상태" },

            new TrapEntity { id = 10005, id2 = "hole", name = "구멍",
                explanation = "구멍으로 떨어졌다!", charani = "없음", note = "강제로 다음 층으로" },

            // 문구를 아직 안 적은 줄. 작동해도 아무 말이 없으므로 알려 줘야 한다.
            new TrapEntity { id = 10002, id2 = "warp", name = "워프",
                explanation = "", charani = "빙글", note = "무작위 방 위치로 이동" },

            // 몸짓 이름을 잘못 적은 줄. 버리지 말고 '없음' 으로 두고 알려 준다.
            new TrapEntity { id = 10099, id2 = "typo\t ", name = "오타",
                explanation = "무언가 이상하다", charani = "빙그르", note = "" },

            new TrapEntity { id = 10098, id2 = "   ", name = "빈 줄" },
        };

        var problems = new List<string>();
        var defs = TrapSheet.FromEntities(rows, problems);

        // 1) id2 가 빈 줄만 버린다.
        Check(defs.Count == 5, $"옮겨 담은 줄 수가 다름 {defs.Count} (기대 5)");

        // 2) 코드가 쓰는 이름은 id2, 번호는 따로 실린다.
        Check(defs[0].Id == "dart", $"코드용 이름이 id2 가 아님: '{defs[0].Id}'");
        Check(defs[0].Number == 10001, $"표 번호가 안 실림 {defs[0].Number}");
        Check(defs[4].Id == "typo", $"id2 의 탭이 안 지워짐: '{defs[4].Id}'");

        // 3) 몸짓을 알아본다. 설명해 준 셋과 '없음' 이 전부 이름을 가져야 한다.
        Check(defs[0].Animation == TrapAnimation.Hit, "'피격' 을 못 알아봄");
        Check(defs[1].Animation == TrapAnimation.SpinFast, "'빙글빙글' 을 못 알아봄");
        Check(defs[2].Animation == TrapAnimation.None, "'없음' 을 못 알아봄");
        Check(defs[3].Animation == TrapAnimation.Spin, "'빙글' 을 못 알아봄");
        Check(TrapSheet.ParseAnimation("수면", "gas") == TrapAnimation.Sleep, "'수면' 을 못 알아봄");

        // 한 바퀴와 두 바퀴는 다른 몸짓이다. 같은 값으로 뭉뚱그리면
        // 워프와 혼란이 화면에서 구별되지 않는다.
        Check(TrapAnimation.Spin != TrapAnimation.SpinFast, "빙글과 빙글빙글이 같은 값");

        // 4) 문구와 설정은 서로 다른 열이다. 뒤바뀌면 설정 메모가 화면에 뜬다.
        Check(defs[0].Message == "어디선가 독침이 날라왔다!", $"띄울 문구가 다름: '{defs[0].Message}'");
        Check(defs[0].Note == "힘이 감소", $"설정 메모가 다름: '{defs[0].Note}'");

        // 5) 이상한 줄을 알려 준다 — 빈 문구 하나, 모르는 몸짓 하나.
        Check(problems.Count == 2, $"이상한 줄을 {problems.Count}건 알림 (기대 2)");
        Check(problems.Exists(p => p.Contains("warp")), "문구가 빈 줄을 안 알려 줌");
        Check(problems.Exists(p => p.Contains("typo")), "몸짓이 이상한 줄을 안 알려 줌");
        Check(defs[4].Animation == TrapAnimation.None, "모르는 몸짓이 '없음' 이 아님");

        // 6) 빈 표에도 넘어지지 않는다.
        Check(TrapSheet.FromEntities(null).Count == 0, "null 에서 줄이 나옴");
        Check(TrapSheet.FromEntities(new List<TrapEntity>()).Count == 0, "빈 표에서 줄이 나옴");

        Console.WriteLine("  옮겨 담기 · 몸짓 다섯 가지 · 빈 문구 알림 · 문구/설정 구분 검사 완료");
    }

    // ── 중요한 알림을 붙잡아 두는 시간 ─────────────────────────────────────
    //
    // 너무 짧으면 공격하느라 연달아 누르던 A 에 그대로 지워지고,
    // 너무 길면 이야기 한 줄마다 손이 묶인다.
    static void TestReadingTime()
    {
        Console.WriteLine("[알림 읽는 시간]");

        float shortLine = MessageBoxView.ReadingSeconds("레벨업!");
        float longLine = MessageBoxView.ReadingSeconds(new string('가', 200));

        // 1) 아무리 짧아도 곧바로 넘길 수는 없다.
        Check(MessageBoxView.ReadingSeconds("") > 0f, "빈 글이 즉시 넘어감");
        Check(MessageBoxView.ReadingSeconds(null) > 0f, "null 에서 즉시 넘어감");
        Check(shortLine > 0f, "짧은 글이 즉시 넘어감");

        // 2) 길수록 오래 붙잡는다.
        Check(longLine > shortLine, "긴 글이 짧은 글보다 오래 안 붙잡음");

        // 3) 아무리 길어도 한없이 붙잡지는 않는다.
        Check(longLine <= 3f, $"200자에서 {longLine}초나 붙잡음");

        // 4) 길이가 늘면 시간이 줄어드는 일은 없어야 한다(단조).
        float previous = 0f;
        for (int length = 0; length <= 120; length++)
        {
            float seconds = MessageBoxView.ReadingSeconds(new string('가', length));
            Check(seconds >= previous - 0.0001f, $"{length}자에서 시간이 거꾸로 줄었다");
            previous = seconds;
        }

        Console.WriteLine($"  최소 {MessageBoxView.ReadingSeconds(""):F2}s ~ 최대 {longLine:F2}s 검사 완료");
    }

    // ── 지나간 메시지의 이력 ────────────────────────────────────────────────
    //
    // 메시지 창은 3초면 닫히고 달릴 때는 연출 대기까지 건너뛰므로, 눈으로
    // 확인하기 어려운 화면이다. 두 줄짜리 메시지가 섞이면 판에 들어가는 줄 수
    // 계산이 어긋나 마지막 줄이 잘리는데, 그 역시 실제로 스크롤을 끝까지
    // 내려 봐야 드러난다. 그래서 여기서 확인한다.
    static void TestMessageLog()
    {
        Console.WriteLine("[메시지 이력]");

        var log = new MessageLog();

        // 1) 아무것도 없을 때.
        Check(log.Count == 0, $"새 이력에 줄이 있음 {log.Count}");
        Check(log.BuildText(0, 5) == MessageLog.EmptyText, "빈 이력인데 기본 문구가 아님");

        // 2) 두 줄짜리 메시지는 두 줄로 쌓인다. 순서는 쓰인 그대로.
        log.Add("고블린를 쓰러뜨렸다.\n5경험치를 얻었다.");
        Check(log.Count == 2, $"두 줄짜리가 {log.Count}줄로 쌓임 (기대 2)");

        string both = log.BuildText(0, 5);
        Check(both.StartsWith("고블린를 쓰러뜨렸다."), "두 줄의 앞뒤가 뒤집힘:\n" + both);
        Check(both.EndsWith("5경험치를 얻었다."), "두 줄의 앞뒤가 뒤집힘:\n" + both);

        // 3) 뜨지 않은 것은 쌓이지 않는다.
        log.Add(null);
        log.Add("");
        log.Add("   \n\n  ");
        Check(log.Count == 2, $"빈 메시지가 쌓임 {log.Count}");

        // 4) 오래된 것부터 버린다. 최근 것은 반드시 남는다.
        var full = new MessageLog();
        for (int i = 0; i < MessageLog.MaxLines + 50; i++) full.Add("줄 " + i);

        Check(full.Count == MessageLog.MaxLines,
              $"한도를 넘겨 쌓임 {full.Count} (한도 {MessageLog.MaxLines})");

        string tail = full.BuildText(int.MaxValue, 3);
        Check(tail.EndsWith("줄 " + (MessageLog.MaxLines + 49)),
              "맨 끝이 가장 최근 줄이 아님:\n" + tail);
        Check(!full.BuildText(0, MessageLog.MaxLines).Contains("줄 0\n"),
              "한도를 넘겼는데 가장 오래된 줄이 남아 있음");

        // 5) 열자마자 보이는 자리(맨 끝)에 최근 것이 화면 가득 들어와야 한다.
        //    한 줄만 보이고 나머지가 빈 채로 남으면 스크롤 계산이 어긋난 것이다.
        for (int capacity = 1; capacity <= 8; capacity++)
        {
            int last = PageScroll.ClampOffset(int.MaxValue, full.Count, capacity);
            string page = full.BuildText(last, capacity);

            Check(page.Split('\n').Length == Math.Min(capacity, full.Count),
                  $"capacity={capacity} 일 때 맨 끝 화면의 줄 수가 다름");
            Check(page.EndsWith("줄 " + (MessageLog.MaxLines + 49)),
                  $"capacity={capacity} 일 때 맨 끝에서 최근 줄이 안 보임");
        }

        // 6) 위로 올리면 과거로 간다.
        Check(full.BuildText(0, 1).Trim() == "줄 50", "맨 위가 가장 오래된 줄이 아님");

        // 7) 판을 새로 시작하면 비워진다.
        full.Clear();
        Check(full.Count == 0, $"비운 뒤에도 줄이 남음 {full.Count}");
        Check(full.BuildText(0, 5) == MessageLog.EmptyText, "비운 뒤 기본 문구가 아님");

        Console.WriteLine($"  줄 쪼개기 · 한도 {MessageLog.MaxLines}줄 · 맨 끝에서 열기 · 비우기 검사 완료");
    }

    // ── 적 표 옮겨 담기 ─────────────────────────────────────────────────────
    //
    // 적 수치가 코드에 박혀 있던 것을 시트로 옮겼다. 시트 쪽이 잘못 읽히면
    // 적이 체력 0 으로 태어나거나 경험치가 0 이 되는데, 에디터에서 한 마리
    // 잡아 보고 "그런가 보다" 하고 넘어가기 쉬운 종류라 여기서 확인한다.
    static void TestMonsterSheet()
    {
        Console.WriteLine("[적 표]");

        var rows = new List<MonsterEntity>
        {
            new MonsterEntity { id = 10001, name = "걸어다니는 시체",
                hp = 5, atk = 2, def = 2, exp = 3,
                rate = "중", category = "몬스터", explanation = "발견하면 다가 오겠어" },

            // 엑셀에서 붙여 넣다 남은 탭·공백.
            new MonsterEntity { id = 10002, name = "박쥐\t ", hp = 3, atk = 4, def = 1, exp = 5,
                rate = "상 ", category = "몬스터" },

            // 빈도를 잘못 적은 줄. 버리지 말고 '중' 으로 두고 알려 준다.
            new MonsterEntity { id = 10003, name = "슬라임", hp = 8, atk = 1, def = 3, exp = 2,
                rate = "가끔" },

            // 이름이 빠진 줄. 쓰러뜨렸을 때 문장만 남으므로 알려 준다.
            new MonsterEntity { id = 10004, name = "", hp = 1, atk = 1, def = 1, exp = 1,
                rate = "하" },

            // 번호가 없는 줄은 프리팹이 찾을 길이 없다.
            new MonsterEntity { id = 0, name = "이름만 있는 줄" },
        };

        var problems = new List<string>();
        var defs = MonsterSheet.FromEntities(rows, problems);

        // 1) 번호 없는 줄만 버린다.
        Check(defs.Count == 4, $"옮겨 담은 줄 수가 다름 {defs.Count} (기대 4)");

        // 2) 수치가 제자리로 갔는가.
        //    검사는 세 값이 서로 다른 줄로 한다 — 실제 시트의 첫 줄은
        //    atk 도 def 도 2 라서, 두 열이 뒤바뀌어도 티가 나지 않는다.
        Check(defs[1].Hp == 3, $"체력이 어긋남 {defs[1].Hp} (기대 3)");
        Check(defs[1].Attack == 4, $"공격력이 어긋남 {defs[1].Attack} (기대 4)");
        Check(defs[1].Defense == 1, $"방어력이 어긋남 {defs[1].Defense} (기대 1)");
        Check(defs[1].Exp == 5, $"경험치가 어긋남 {defs[1].Exp} (기대 5)");

        Check(defs[0].Name == "걸어다니는 시체", $"이름이 어긋남 '{defs[0].Name}'");
        Check(defs[0].Description == "발견하면 다가 오겠어", $"설명이 어긋남 '{defs[0].Description}'");
        Check(defs[0].Category == "몬스터", $"갈래가 어긋남 '{defs[0].Category}'");

        // 3) 칸 끝의 탭·공백을 털어 낸다.
        Check(defs[1].Name == "박쥐", $"이름의 탭이 안 지워짐: '{defs[1].Name}'");
        Check(defs[1].Rate == SpawnRate.High, "공백이 붙은 '상 ' 을 못 알아봄");

        // 4) 빈도 세 가지.
        Check(defs[0].Rate == SpawnRate.Medium, "'중' 을 못 알아봄");
        Check(defs[3].Rate == SpawnRate.Low, "'하' 를 못 알아봄");

        // 5) 모르는 빈도는 '중' 으로 두되 무엇이 이상한지 알려 준다.
        Check(defs[2].Rate == SpawnRate.Medium, "모르는 빈도가 '중' 이 아님");
        Check(problems.Count == 2, $"이상한 줄을 {problems.Count}건 알림 (기대 2)");

        bool toldRate = false, toldName = false;
        foreach (string problem in problems)
        {
            if (problem.Contains("10003")) toldRate = true;
            if (problem.Contains("10004")) toldName = true;
        }
        Check(toldRate, "빈도가 이상한 줄의 번호를 안 알려 줌");
        Check(toldName, "이름이 빈 줄의 번호를 안 알려 줌");

        // 6) 치명타는 켜고 끄는 값이다. 안 적으면 꺼진 것이고,
        //    기본이 꺼짐이어야 "저 녀석은 조심해야 한다" 가 성립한다.
        Check(!defs[0].DealsCritical, "안 적었는데 치명타가 붙음");

        var crits = new List<MonsterEntity>
        {
            new MonsterEntity { id = 20001, name = "회심의 적", critical = 1, rate = "중" },
            new MonsterEntity { id = 20002, name = "평범한 적", critical = 0, rate = "중" },
        };
        var critProblems = new List<string>();
        var critDefs = MonsterSheet.FromEntities(crits, critProblems);

        Check(critDefs[0].DealsCritical, "1 인데 치명타가 안 켜짐");
        Check(!critDefs[1].DealsCritical, "0 인데 치명타가 켜짐");
        Check(critProblems.Count == 0, $"멀쩡한 1/0 에 알림이 뜸 ({critProblems.Count}건)");

        // 이 열은 한때 확률(%)이었다. 예전 감각으로 20 이라고 적으면 20% 가 아니라
        // 모르는 값인데, 그대로 두면 조용히 '낸다' 로만 굴러간다. 반드시 알려야 한다.
        var oddProblems = new List<string>();
        var oddDefs = MonsterSheet.FromEntities(
            new List<MonsterEntity>
            {
                new MonsterEntity { id = 20004, name = "옛 표기", critical = 20, rate = "중" },
            },
            oddProblems);

        Check(oddDefs[0].DealsCritical, "1·0 이 아닌 값을 '안 낸다' 로 봄");
        Check(oddProblems.Count == 1, $"1·0 이 아닌 값을 안 알림 ({oddProblems.Count}건)");
        Check(MonsterSheet.ParseCritical(0, 0) == false && MonsterSheet.ParseCritical(1, 0),
              "1/0 판정이 뒤집힘");

        // 7) 빈 표에도 넘어지지 않는다.
        Check(MonsterSheet.FromEntities(null).Count == 0, "null 에서 줄이 나옴");
        Check(MonsterSheet.FromEntities(new List<MonsterEntity>()).Count == 0, "빈 표에서 줄이 나옴");

        Console.WriteLine("  옮겨 담기 · 수치 자리 · 빈도 세 가지 · 치명타 1/0 · 이상한 줄 알림 검사 완료");
    }

    // ── 아이템 표 옮겨 담기 ─────────────────────────────────────────────────
    //
    // 아이템은 조회를 이름에서 번호로 옮긴 참이다. 시트 쪽이 잘못 읽히면
    // 장비가 아닌 것이 장비가 되거나 가격이 0 이 되는데, 에디터에서는
    // 한 개 집어 보고 넘어가기 쉬운 종류라 여기서 확인한다.
    static void TestItemSheet()
    {
        Console.WriteLine("[아이템 표]");

        var rows = new List<ItemEntity>
        {
            new ItemEntity { id = 20001, name = "사과", explanation = "만복도를 30 회복합니다.",
                sellprice = 50, itemtype = "food", equipslot = "none", basepower = 0, droprate = "B" },

            // 엑셀에서 붙여 넣다 남은 탭·공백, 그리고 대소문자 섞임.
            new ItemEntity { id = 10001, name = "기력회복약\t ", explanation = "힘을 회복합니다.",
                sellprice = 200, itemtype = "Expendables", equipslot = " ", basepower = 0, droprate = "B" },

            new ItemEntity { id = 50001, name = "청동검", explanation = "낡은 검이다.",
                sellprice = 300, itemtype = "equipment", equipslot = "weapon", basepower = 3, droprate = "B" },

            // 장비가 아닌데 성능이 적힌 줄. 0 으로 두고 알려 준다.
            new ItemEntity { id = 60001, name = "이상한 돌", sellprice = 10,
                itemtype = "special", equipslot = "none", basepower = 7, droprate = "B" },

            // 갈래를 잘못 적은 줄.
            new ItemEntity { id = 70001, name = "오타", itemtype = "포션", equipslot = "none", droprate = "B" },

            // 번호가 없는 줄은 프리팹이 찾을 길이 없다.
            new ItemEntity { id = 0, name = "이름만 있는 줄" },
        };

        var problems = new List<string>();
        var defs = ItemSheet.FromEntities(rows, problems);

        // 1) 번호 없는 줄만 버린다.
        Check(defs.Count == 5, $"옮겨 담은 줄 수가 다름 {defs.Count} (기대 5)");

        // 2) 값이 제자리로 갔는가.
        Check(defs[0].Name == "사과" && defs[0].SellPrice == 50,
              $"이름·가격이 어긋남 '{defs[0].Name}' {defs[0].SellPrice}");
        Check(defs[0].Kind == ItemKind.Food, "'food' 를 못 알아봄");
        Check(defs[0].Description == "만복도를 30 회복합니다.", $"설명이 어긋남 '{defs[0].Description}'");

        // 3) 칸 끝의 탭·공백을 털어 내고, 대소문자를 가리지 않는다.
        Check(defs[1].Name == "기력회복약", $"이름의 탭이 안 지워짐: '{defs[1].Name}'");
        Check(defs[1].Kind == ItemKind.Expendables, "'Expendables' 를 못 알아봄");
        Check(defs[1].Slot == EquipmentSlot.None, "공백뿐인 칸이 장비 자리로 읽힘");

        // 4) 장비.
        Check(defs[2].Slot == EquipmentSlot.Weapon, "'weapon' 을 못 알아봄");

        // 표에는 복수형(accessories)으로 적혀 있다. 그것 하나 때문에
        // 장비가 아닌 것으로 읽히면 반지가 영영 안 끼워진다.
        Check(ItemSheet.ParseSlot("accessories", 0) == EquipmentSlot.Accessory,
              "복수형 'accessories' 를 못 알아봄");
        Check(ItemSheet.ParseSlot("accessory", 0) == EquipmentSlot.Accessory,
              "단수형 'accessory' 를 못 알아봄");
        Check(defs[2].BasePower == 3, $"기본 성능이 어긋남 {defs[2].BasePower}");
        Check(defs[2].IsEquipment, "무기인데 장비가 아니라고 함");

        // 5) 장비가 아니면 성능은 0 이고, 그 사실을 알려 준다.
        Check(!defs[3].IsEquipment, "장비가 아닌데 장비라고 함");
        Check(defs[3].BasePower == 0, $"장비가 아닌데 성능이 남음 {defs[3].BasePower}");

        // 6) 모르는 갈래는 기본값으로 두되 알려 준다.
        Check(defs[4].Kind == ItemKind.Expendables, "모르는 갈래가 기본값이 아님");

        Check(problems.Count == 2, $"이상한 줄을 {problems.Count}건 알림 (기대 2)");

        bool toldPower = false, toldKind = false;
        foreach (string problem in problems)
        {
            if (problem.Contains("60001")) toldPower = true;
            if (problem.Contains("70001")) toldKind = true;
        }
        Check(toldPower, "장비가 아닌데 성능이 적힌 줄을 안 알려 줌");
        Check(toldKind, "갈래가 이상한 줄을 안 알려 줌");

        // 7) 빈 표에도 넘어지지 않는다.
        Check(ItemSheet.FromEntities(null).Count == 0, "null 에서 줄이 나옴");

        // ── 장비 특수 능력 ────────────────────────────────────────────────
        var gear = new List<ItemEntity>
        {
            new ItemEntity { id = 50001, name = "나무몽둥이", itemtype = "equipment",
                equipslot = "weapon", basepower = 2,
                ability = "경험치증가", abilityvalue = 1, baseonly = 1, droprate = "B" },

            new ItemEntity { id = 60001, name = "헌옷", itemtype = "equipment",
                equipslot = "armor", basepower = 1,
                ability = "치명타무효", abilityvalue = 0, droprate = "B" },

            new ItemEntity { id = 70001, name = "구리반지", itemtype = "equipment",
                equipslot = "accessories", ability = "만복도절약", abilityvalue = 5, droprate = "B" },

            // 숫자를 쓰는 능력인데 값이 없다. 아무 일도 안 일어나므로 알려야 한다.
            new ItemEntity { id = 50002, name = "빈몽둥이", itemtype = "equipment",
                equipslot = "weapon", ability = "경험치증가", abilityvalue = 0 },

            // 장비가 아닌데 능력이 적힌 줄.
            new ItemEntity { id = 20002, name = "이상한사과", itemtype = "food",
                equipslot = "none", ability = "치명타무효", droprate = "B" },

            // 모르는 능력.
            new ItemEntity { id = 50003, name = "오타몽둥이", itemtype = "equipment",
                equipslot = "weapon", ability = "무적", droprate = "B" },
        };

        var gearProblems = new List<string>();
        var gearDefs = ItemSheet.FromEntities(gear, gearProblems);

        Check(gearDefs[0].Ability == EquipAbility.BonusExp, "'경험치증가' 를 못 알아봄");
        Check(gearDefs[0].AbilityValue == 1, $"능력 값이 어긋남 {gearDefs[0].AbilityValue}");
        Check(gearDefs[0].BaseOnly, "baseonly 1 인데 꺼져 있음");

        Check(gearDefs[1].Ability == EquipAbility.NegateCritical, "'치명타무효' 를 못 알아봄");
        Check(!gearDefs[1].BaseOnly, "baseonly 0 인데 켜져 있음");

        Check(gearDefs[2].Ability == EquipAbility.SlowHunger, "'만복도절약' 을 못 알아봄");
        Check(gearDefs[2].Slot == EquipmentSlot.Accessory, "복수형 자리를 못 알아봄");

        // 치명타무효는 숫자를 쓰지 않으므로 값이 0 이어도 조용해야 한다.
        Check(!ItemSheet.NeedsValue(EquipAbility.NegateCritical), "치명타무효가 값을 요구함");
        Check(ItemSheet.NeedsValue(EquipAbility.BonusExp), "경험치증가가 값을 안 씀");

        // 장비가 아니면 능력을 달지 않는다. 달아 두면 안 차고도 효과를 보는 줄 안다.
        Check(gearDefs[4].Ability == EquipAbility.None, "장비가 아닌데 능력이 붙음");

        bool toldNoValue = false, toldNotGear = false, toldUnknown = false;
        foreach (string problem in gearProblems)
        {
            if (problem.Contains("50002")) toldNoValue = true;
            if (problem.Contains("20002")) toldNotGear = true;
            if (problem.Contains("50003")) toldUnknown = true;
        }
        Check(toldNoValue, "값 없는 능력을 안 알려 줌");
        Check(toldNotGear, "장비가 아닌데 능력이 적힌 줄을 안 알려 줌");
        Check(toldUnknown, "모르는 능력을 안 알려 줌");

        // ── 드랍 등급 ─────────────────────────────────────────────────────
        //
        // 시트에는 A+ · B- 같은 등급으로 적는다. 등급 사이가 뒤집히면 표를
        // 채우는 사람이 등급으로 견줄 수가 없다.
        var ladder = new[] { "A+", "A", "A-", "B+", "B", "B-", "C+", "C", "C-", "D+", "D", "D-" };
        float previous = float.MaxValue;

        foreach (string grade in ladder)
        {
            float weight = ItemSheet.ParseDropRate(grade, 0);
            Check(weight > 0f, $"{grade} 의 가중치가 0 이다");
            Check(weight <= previous, $"{grade} 가 앞 등급보다 흔하다 ({weight} > {previous})");
            previous = weight;
        }

        // 아래로 내려가도 1 밑으로는 안 간다. 0 이 되면 "안 나옴" 과 뜻이 겹친다.
        Check(ItemSheet.ParseDropRate("Z-", 0) >= 1f, "낮은 등급이 0 이 되어 버림");

        // 대소문자를 가리지 않는다.
        Check(ItemSheet.ParseDropRate("a+", 0) == ItemSheet.ParseDropRate("A+", 0),
              "소문자 등급을 다르게 봄");

        // 비었거나 이상한 값은 0(안 나옴)이고 알려 준다.
        var gradeProblems = new List<string>();
        // 동전은 시트의 한 줄이 아니라 코드가 등급을 들고 있다(GoldPile.DropGrade).
        // 비었거나 오타면 가중치가 0 이 되어 골드가 한 층에도 나오지 않는데,
        // 다른 아이템이 멀쩡히 나오므로 에러도 경고도 나지 않는다. 실제로 그랬다.
        Check(ItemSheet.ParseDropRate(GoldPile.DropGrade, 0) > 0f,
              $"동전이 바닥에 나오지 않는 등급임: '{GoldPile.DropGrade}'");

        Check(ItemSheet.ParseDropRate("", 11, gradeProblems) == 0f, "빈 등급이 0 이 아님");
        Check(ItemSheet.ParseDropRate("특급", 22, gradeProblems) == 0f, "모르는 등급이 0 이 아님");
        Check(ItemSheet.ParseDropRate("A*", 33, gradeProblems) == 0f, "이상한 부호가 0 이 아님");
        Check(gradeProblems.Count == 3, $"이상한 등급을 {gradeProblems.Count}건 알림 (기대 3)");

        Console.WriteLine("  옮겨 담기 · 대소문자 · 장비 자리 · 성능 정리 · 특수 능력 · 드랍 등급 · 이상한 줄 알림 검사 완료");
    }

    // ── 워프가 데려갈 자리 ──────────────────────────────────────────────────
    //
    // 겹쳐 서면 둘 다 빠져나오지 못하고, 제자리로 워프하면 함정을 밟은 뜻이
    // 없어진다. 둘 다 "가끔" 일어나는 종류라 눈으로는 잡기 어렵다.
    //
    // 여기 숫자는 전부 칸 좌표다. 예전에는 월드 좌표(칸 중심이 x+0.5)로 검사해서
    // 반 칸 어긋남과 한 칸 넘침을 두 번 다 놓쳤다 — 검사에 쓴 방이 정수 좌표라
    // 어긋나도 값이 같았고, 범위를 넉넉히 잡아 두어 한 칸 넘쳐도 통과했다.
    static void TestWarpDestination()
    {
        Console.WriteLine("[워프 목적지]");

        // 방 셋. 안쪽 칸들이라 벽은 이미 빠져 있다.
        var rooms = new List<RectInt>
        {
            new RectInt(1, 1, 3, 3),      // 0: 칸 (1,1)~(3,3)  아홉 칸
            new RectInt(10, 1, 2, 2),     // 1: 칸 (10,1)~(11,2) 네 칸
            new RectInt(20, 20, 1, 1),    // 2: 칸 (20,20)      한 칸
        };

        var rnd = new System.Random(11);
        Func<int, int, int> next = (min, max) => rnd.Next(min, max);

        var inRoom0 = new Vector3Int(2, 2, 0);

        // 1) 지금 있는 방은 고르지 않는다. 그리고 벽 안으로 나가지 않는다.
        var seen = new HashSet<Vector3Int>();
        for (int i = 0; i < 400; i++)
        {
            Check(TrapEffects.TryPickWarpDestination(rooms, null, inRoom0, next, out Vector3Int to),
                  "갈 곳이 있는데 못 찾음");
            Check(!rooms[0].Contains(new Vector2Int(to.x, to.y)), $"같은 방 안으로 워프함 {to}");
            Check(rooms[1].Contains(new Vector2Int(to.x, to.y))
                  || rooms[2].Contains(new Vector2Int(to.x, to.y)),
                  $"어느 방에도 없는 칸으로 워프함 {to}");
            seen.Add(to);
        }

        // 방 1(네 칸) + 방 2(한 칸) = 다섯. 여섯이면 한 칸 넘친 것이고
        // 넷이면 모자란 것이다. 개수를 세는 것이 가장 확실하다.
        Check(seen.Count == 5, $"갈 수 있는 칸 수가 다름 {seen.Count} (기대 5)");

        // 2) 누가 서 있는 칸은 고르지 않는다.
        //    방 1 을 한 칸만 남기고 채우고, 방 2 도 막는다.
        var crowded = new List<Vector3Int>
        {
            new Vector3Int(10, 1, 0), new Vector3Int(10, 2, 0), new Vector3Int(11, 2, 0),
            new Vector3Int(20, 20, 0),
        };
        for (int i = 0; i < 200; i++)
        {
            Check(TrapEffects.TryPickWarpDestination(rooms, crowded, inRoom0, next, out Vector3Int to),
                  "빈 칸이 하나 있는데 못 찾음");
            Check(to == new Vector3Int(11, 1, 0), $"적이 선 칸으로 워프함 {to}");
        }

        // 3) 갈 방이 가득 차면 다른 방으로 넘어간다.
        var room1Full = new List<Vector3Int>
        {
            new Vector3Int(10, 1, 0), new Vector3Int(10, 2, 0),
            new Vector3Int(11, 1, 0), new Vector3Int(11, 2, 0),
        };
        for (int i = 0; i < 200; i++)
        {
            Check(TrapEffects.TryPickWarpDestination(rooms, room1Full, inRoom0, next, out Vector3Int to),
                  "다른 방이 남았는데 포기함");
            Check(to == new Vector3Int(20, 20, 0), $"가득 찬 방으로 워프함 {to}");
        }

        // 4) 방이 하나뿐이면 그 방 안에서 옮긴다(제자리 규칙보다 우선).
        var single = new List<RectInt> { rooms[0] };
        Check(TrapEffects.TryPickWarpDestination(single, null, inRoom0, next, out Vector3Int same),
              "방이 하나뿐인데 갈 곳이 없다고 함");
        Check(rooms[0].Contains(new Vector2Int(same.x, same.y)),
              $"하나뿐인 방 밖으로 나감 {same}");

        // 5) 갈 곳이 정말 없으면 false. 빈 목록에도 넘어지지 않는다.
        Check(!TrapEffects.TryPickWarpDestination(
                  new List<RectInt>(), null, inRoom0, next, out _), "방이 없는데 자리를 찾음");
        Check(!TrapEffects.TryPickWarpDestination(null, null, inRoom0, next, out _),
              "null 에서 자리를 찾음");

        var allTaken = new List<Vector3Int>();
        for (int x = 1; x <= 3; x++)
            for (int y = 1; y <= 3; y++) allTaken.Add(new Vector3Int(x, y, 0));
        Check(!TrapEffects.TryPickWarpDestination(single, allTaken, inRoom0, next, out _),
              "빈 칸이 없는데 자리를 찾음");

        Console.WriteLine("  다른 방 우선 · 적 칸 제외 · 가득 찬 방 건너뛰기 · 칸 수 · 갈 곳 없음 검사 완료");
    }

    // ── 날려진 끝에 무슨 일이 일어나는가 ────────────────────────────────────
    //
    // 물·용암·절벽에 떨어지는 것은 실제로 밟아 보기 전에는 확인하기 어렵고,
    // 용암 피해가 죽음으로 이어지면 판이 그대로 끝난다.
    static void TestBlowOutcome()
    {
        Console.WriteLine("[날리기 결말]");

        // 1) 평범한 바닥. 벽에 부딪혔는지가 갈림길이다.
        Check(TrapEffects.ResolveBlow(TileRules.TerrainKind.Floor, false)
              == TrapEffects.BlowOutcome.Landed, "그냥 떨어졌는데 다른 일이 남");
        Check(TrapEffects.ResolveBlow(TileRules.TerrainKind.Floor, true)
              == TrapEffects.BlowOutcome.HitWall, "벽에 부딪혔는데 그냥 착지로 봄");

        // 2) 지형이 벽보다 우선이다. 벽에 부딪히며 물에 빠질 수 있는데,
        //    둘을 다 적용하면 피해가 겹쳐 갑자기 죽는다.
        foreach (bool wall in new[] { false, true })
        {
            Check(TrapEffects.ResolveBlow(TileRules.TerrainKind.Water, wall)
                  == TrapEffects.BlowOutcome.Water, $"물이 물로 안 나옴 (벽={wall})");
            Check(TrapEffects.ResolveBlow(TileRules.TerrainKind.Lava, wall)
                  == TrapEffects.BlowOutcome.Lava, $"용암이 용암으로 안 나옴 (벽={wall})");
            Check(TrapEffects.ResolveBlow(TileRules.TerrainKind.Pit, wall)
                  == TrapEffects.BlowOutcome.Pit, $"절벽이 절벽으로 안 나옴 (벽={wall})");
        }

        // 3) 용암 피해는 지금 체력의 절반이고, 이것만으로는 죽지 않는다.
        for (int hp = 1; hp <= 60; hp++)
        {
            int damage = TrapEffects.LavaDamage(hp);
            Check(damage < hp, $"체력 {hp} 에서 용암이 죽임 (피해 {damage})");
            Check(damage == hp / 2, $"체력 {hp} 에서 절반이 아님 (피해 {damage})");
        }

        Check(TrapEffects.LavaDamage(1f) == 0, "체력 1 에서 용암이 죽임");
        Check(TrapEffects.LavaDamage(0f) == 0, "체력 0 에서 음수 피해");
        Check(TrapEffects.LavaDamage(20f) == 10, "체력 20 의 절반이 10 이 아님");

        // 4) 날리는 거리와 벽 피해가 뜻대로 있는가.
        Check(TrapEffects.BlowDistance == 8, $"날리는 거리가 다름 {TrapEffects.BlowDistance}");
        Check(TrapEffects.BlowWallDamage == 5, $"벽 피해가 다름 {TrapEffects.BlowWallDamage}");

        Console.WriteLine("  지형 우선 · 벽 부딪힘 · 용암 절반(죽지 않음) 검사 완료");
    }

    // ── 맵 끝자락(허물 수 없는 벽) ──────────────────────────────────────────
    //
    // 겉모습이 보통 벽과 같아서 눈으로는 구별이 안 된다. 통행 판정에서 벽과
    // 똑같이 다뤄지지 않으면 길찾기가 맵 밖으로 새고, 반대로 허물 수 있게
    // 되면 플레이어가 맵 밖으로 나가 돌아올 길이 없어진다.
    static void TestBedrock()
    {
        Console.WriteLine("[맵 끝자락]");

        string bedrock = TileRules.BedrockTileNames[0];
        string wall = TileRules.ImpassableTileNames[0];
        string water = TileRules.WaterTileNames[0];

        // 1) 통행 판정에서는 보통 벽과 똑같다.
        Check(TileRules.KindOfTile(bedrock) == TileRules.TerrainKind.Wall,
              "끝자락이 벽으로 안 나옴");
        Check(TileRules.KindOfTile(bedrock) == TileRules.KindOfTile(wall),
              "끝자락과 보통 벽의 판정이 다름");

        // 2) 허물 수 있는가 — 여기만 다르다.
        Check(!TileRules.IsDestructible(bedrock), "끝자락을 허물 수 있다고 함");
        Check(TileRules.IsDestructible(wall), "보통 벽을 못 허문다고 함");
        Check(TileRules.IsDestructible(water), "물을 못 없앤다고 함");
        Check(TileRules.IsDestructible((string)null), "빈 칸을 못 없앤다고 함");

        // 3) 이름 목록이 서로 겹치면 판정 순서에 따라 답이 달라진다.
        foreach (string name in TileRules.BedrockTileNames)
        {
            Check(!TileRules.ImpassableTileNames.Contains(name),
                  $"'{name}' 이 보통 벽 목록에도 있음");
            Check(!TileRules.WaterTileNames.Contains(name),
                  $"'{name}' 이 물 목록에도 있음");
            Check(!TileRules.LavaTileNames.Contains(name),
                  $"'{name}' 이 용암 목록에도 있음");
            Check(!TileRules.PitTileNames.Contains(name),
                  $"'{name}' 이 구멍 목록에도 있음");
        }

        Console.WriteLine("  벽과 같은 통행 판정 · 허물 수 없음 · 이름 겹침 없음 검사 완료");
    }

    // ── 지형은 타일 애셋이 정한다 ───────────────────────────────────────────
    //
    // 이름으로 가르던 시절에는 2스테이지 벽("S2Wall")이 어느 목록에도 없어서
    // 걸어서 통과되는 벽이 되었다. 에러가 나지 않는 종류라 여기서 지킨다.
    static void TestTerrainFromAsset()
    {
        Console.WriteLine("[타일이 아는 지형]");

        string wallName = TileRules.ImpassableTileNames[0];
        string bedrockName = TileRules.BedrockTileNames[0];

        // 1) 애셋이 답하면 그것이 이긴다. 이름이 무엇이든 상관없다.
        var stage2Wall = new FakeTerrainTile(TileRules.TerrainKind.Wall, destructible: true);
        Check(TileRules.Resolve(stage2Wall, "S2Wall") == TileRules.TerrainKind.Wall,
              "이름이 목록에 없는 벽을 벽으로 못 알아봄");
        Check(TileRules.Resolve(stage2Wall, "아무이름") == TileRules.TerrainKind.Wall,
              "애셋이 답했는데 이름을 따라감");

        // 2) 이름이 벽 목록에 있어도 애셋이 물이라면 물이다.
        //    겉모습과 규칙이 어긋날 수 있다는 것이 이 방식의 핵심이다.
        var wateryLookingWall = new FakeTerrainTile(TileRules.TerrainKind.Water, destructible: true);
        Check(TileRules.Resolve(wateryLookingWall, wallName) == TileRules.TerrainKind.Water,
              "애셋이 물이라는데 이름을 따라 벽이라 함");

        // 3) 애셋이 답하지 않으면 이름으로 간다(아직 옮기지 않은 타일용 뒷길).
        Check(TileRules.Resolve(null, wallName) == TileRules.TerrainKind.Wall,
              "뒷길이 벽을 못 알아봄");
        Check(TileRules.Resolve(null, "S2Wall") == TileRules.TerrainKind.Floor,
              "목록에 없는 이름이 바닥이 아님 — 뒷길의 한계를 확인");
        Check(TileRules.Resolve(null, null) == TileRules.TerrainKind.Floor,
              "타일이 없는 칸이 바닥이 아님");

        // 4) 맵 끝자락 — 물처럼 보이지만 벽이고, 허물 수 없다.
        var bedrockWater = new FakeTerrainTile(TileRules.TerrainKind.Wall, destructible: false);
        Check(TileRules.Resolve(bedrockWater, "S1BedrockWater") == TileRules.TerrainKind.Wall,
              "물 모양 끝자락이 벽이 아님");
        Check(!bedrockWater.Destructible, "끝자락을 허물 수 있다고 함");

        // 5) 이름 뒷길의 허물기 판정은 그대로 남아 있어야 한다.
        Check(!TileRules.IsDestructible(bedrockName), "이름으로도 끝자락은 못 허문다");
        Check(TileRules.IsDestructible(wallName), "보통 벽을 못 허문다고 함");

        Console.WriteLine("  애셋 우선 · 이름 뒷길 · 겉모습과 규칙 분리 검사 완료");
    }

    /// <summary>ITerrainTile 을 흉내 내는 검사용 타일. Unity 애셋이 필요 없다.</summary>
    class FakeTerrainTile : ITerrainTile
    {
        public FakeTerrainTile(TileRules.TerrainKind terrain, bool destructible)
        {
            Terrain = terrain;
            Destructible = destructible;
        }

        public TileRules.TerrainKind Terrain { get; }
        public bool Destructible { get; }
    }

    // ── 걸려 있는 상태이상과 남은 턴 ────────────────────────────────────────
    //
    // 잠에 걸린 채로 몇 턴이 지나야 깨는지는 실제로 걸려 봐야 알 수 있고,
    // 그동안 조작이 막히므로 눈으로 확인하기가 가장 번거로운 종류다.
    static void TestConditionTracker()
    {
        Console.WriteLine("[상태이상 걸기]");

        var tracker = new ConditionTracker();

        // 1) 처음에는 아무것도 없다.
        Check(tracker.IsEmpty, "새 판인데 걸려 있는 것이 있음");
        Check(!tracker.Has("sleep"), "안 걸었는데 걸려 있다고 함");

        // 2) 걸면 그 턴만큼 간다. 5턴이면 다섯 번째 턴이 지날 때 풀린다.
        tracker.Apply("sleep", 5);
        Check(tracker.Has("sleep"), "걸었는데 안 걸려 있다고 함");

        for (int turn = 1; turn <= 4; turn++)
        {
            var expired = tracker.Tick();
            Check(expired.Count == 0, $"{turn}턴째에 벌써 풀림");
            Check(tracker.Has("sleep"), $"{turn}턴째에 이미 사라짐");
        }

        var done = tracker.Tick();
        Check(done.Count == 1 && done[0] == "sleep", "다섯째 턴에 안 풀림");
        Check(!tracker.Has("sleep"), "풀렸는데 남아 있음");
        Check(tracker.IsEmpty, "다 풀렸는데 비어 있지 않음");

        // 3) 이미 걸린 것에 또 걸면 긴 쪽이 남는다.
        //    덮어쓰면 5턴짜리 잠이 1턴짜리에 밀려 곧바로 깨고,
        //    더하면 같은 함정을 여러 번 밟아 영영 못 움직이게 된다.
        tracker.Apply("sleep", 5);
        tracker.Apply("sleep", 2);
        Check(tracker.RemainingTurns("sleep") == 5, $"짧은 것에 밀림 {tracker.RemainingTurns("sleep")}");

        tracker.Apply("sleep", 9);
        Check(tracker.RemainingTurns("sleep") == 9, $"긴 것이 안 먹힘 {tracker.RemainingTurns("sleep")}");

        // 4) 여럿이 함께 걸리고 각자 제 턴에 풀린다.
        tracker.Clear();
        tracker.Apply("sleep", 1);
        tracker.Apply("disarray", 3);

        var first = tracker.Tick();
        Check(first.Count == 1 && first[0] == "sleep", "짧은 것만 풀려야 하는데 아님");
        Check(tracker.Has("disarray"), "긴 것이 함께 풀림");

        tracker.Tick();
        var second = tracker.Tick();
        Check(second.Count == 1 && second[0] == "disarray", "긴 것이 제 턴에 안 풀림");

        // 5) 영영 걸린 것은 턴이 지나도 풀리지 않고, 짧은 것에 밀리지도 않는다.
        tracker.Clear();
        tracker.Apply("curse", ConditionTracker.Forever);
        for (int i = 0; i < 50; i++) tracker.Tick();
        Check(tracker.Has("curse"), "영영 걸린 것이 저절로 풀림");

        tracker.Apply("curse", 3);
        Check(tracker.Has("curse"), "영영 걸린 것이 짧은 것에 밀림");
        for (int i = 0; i < 10; i++) tracker.Tick();
        Check(tracker.Has("curse"), "영영 걸린 것이 결국 풀림");

        // 6) 손으로 풀 수 있다(해독제 같은 것이 쓸 자리).
        tracker.Remove("curse");
        Check(!tracker.Has("curse"), "손으로 풀었는데 남아 있음");

        // 7) 이상한 입력에 넘어지지 않는다.
        tracker.Apply(null, 3);
        tracker.Apply("", 3);
        tracker.Apply("음수", -1);
        Check(tracker.IsEmpty, "빈 id 나 음수 턴이 걸림");

        Console.WriteLine("  남은 턴 · 긴 쪽 남기기 · 여럿 동시 · 영영 · 손으로 풀기 검사 완료");
    }

    // ── 아이템 표식 ─────────────────────────────────────────────────────────
    //
    // 색 태그가 어긋나면 아이템 이름까지 물들거나, 태그 글자가 그대로 보인다.
    // 눈으로는 "색이 좀 이상한가" 정도로만 보여 놓치기 쉽다.
    static void TestItemMark()
    {
        Console.WriteLine("[아이템 표식]");

        // 1) 표식이 없으면 아무것도 붙지 않는다. 색을 골라 두었어도 마찬가지다.
        Check(ItemMark.Prefix(MarkShape.None, MarkColor.Red) == "",
              "표식이 없는데 글자가 붙음");

        // 2) 모양 셋이 각자 다른 글자를 낸다.
        var glyphs = new HashSet<string>();
        foreach (MarkShape shape in new[] { MarkShape.Circle, MarkShape.Triangle, MarkShape.Square })
        {
            string glyph = ItemMark.GlyphOf(shape);
            Check(glyph.Length > 0, $"{shape} 의 글자가 비어 있음");
            Check(glyphs.Add(glyph), $"{shape} 의 글자가 다른 모양과 겹침");
        }

        // 3) 색 태그가 표식에서 닫힌다. 안 닫으면 뒤따르는 아이템 이름까지 물든다.
        string prefix = ItemMark.Prefix(MarkShape.Circle, MarkColor.Blue);
        Check(prefix.StartsWith("<color=") && prefix.EndsWith("</color>"),
              $"색 태그 모양이 다름: {prefix}");
        Check(prefix.Contains("●"), $"모양 글자가 빠짐: {prefix}");

        // 태그 안에 든 글자는 모양 하나뿐이어야 한다.
        int open = prefix.IndexOf('>');
        int close = prefix.IndexOf("</color>");
        Check(prefix.Substring(open + 1, close - open - 1) == "●",
              "색 태그가 모양 말고 다른 것까지 감쌈");

        // 4) 여덟 색이 모두 다른 값을 낸다. 겹치면 눈으로 구별할 수 없다.
        var hexes = new HashSet<string>();
        foreach (MarkColor color in ItemMark.Colors)
        {
            string hex = ItemMark.HexOf(color);
            Check(hex.StartsWith("#") && hex.Length == 7, $"{color} 의 색 값이 이상함: {hex}");
            Check(hexes.Add(hex), $"{color} 의 색이 다른 색과 겹침");
            Check(ItemMark.NameOf(color).Length > 0, $"{color} 의 이름이 비어 있음");
        }
        Check(ItemMark.Colors.Length == 8, $"색이 여덟이 아님 {ItemMark.Colors.Length}");

        // 5) 고를 수 있는 모양에 '지운다'(None)가 들어 있어야 표식을 뗄 수 있다.
        bool canErase = false;
        foreach (MarkShape shape in ItemMark.Shapes)
            if (shape == MarkShape.None) canErase = true;
        Check(canErase, "표식을 지울 방법이 없음");

        // ── 게임이 붙이는 표시(끈적끈적 · 장착 중) ────────────────────────
        //
        // 사람이 고르는 표식과 달리 색을 고를 수 없다. 여덟 색 어디와도 겹치지
        // 않아야 "내가 적어 둔 것" 과 "물건에 실제로 일어난 일" 이 갈린다.

        string equipped = ItemMark.EquippedPrefix();
        string sticky = ItemMark.StickyPrefix();

        foreach (string fact in new[] { equipped, sticky })
        {
            Check(fact.StartsWith("<color=") && fact.EndsWith("</color>"),
                  $"색 태그 모양이 다름: {fact}");

            int factOpen = fact.IndexOf('>');
            int factClose = fact.IndexOf("</color>");
            bool wellFormed = factOpen > 0 && factClose > factOpen;
            Check(wellFormed, $"태그가 안 닫힘: {fact}");

            // 모양이 이미 어긋났으면 잘라 보지 않는다. 여기서 예외가 나면 검사가
            // 통째로 죽어 뒤에 오는 항목들이 아예 돌지 않는다.
            if (!wellFormed) continue;

            // 태그가 표시 글자에서 닫혀야 뒤따르는 아이템 이름이 물들지 않는다.
            string inside = fact.Substring(factOpen + 1, factClose - factOpen - 1);
            Check(inside.Length == 1, $"태그가 표시 글자 말고 다른 것까지 감쌈: {inside}");
        }

        Check(!hexes.Contains(ItemMark.EquippedHex), $"장착 표시 색이 마킹 색과 겹침: {ItemMark.EquippedHex}");
        Check(!hexes.Contains(ItemMark.StickyHex), $"끈적끈적 색이 마킹 색과 겹침: {ItemMark.StickyHex}");

        Check(ItemMark.EquippedGlyph == "E", $"장착 표시가 E 가 아님: {ItemMark.EquippedGlyph}");
        Check(ItemMark.EquippedGlyph != ItemMark.StickyGlyph, "장착 표시와 끈적끈적이 같은 글자");

        // ── 장비 자리별 글자 색 ───────────────────────────────────────────
        //
        // 무기 노랑 · 방어구 주황 · 장신구 진한 분홍. 셋이 겹치면 색으로
        // 가려낸다는 목적 자체가 없어진다.

        var slotHexes = new HashSet<string>();
        foreach (EquipmentSlot slot in
                 new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Accessory })
        {
            string hex = ItemMark.SlotHexOf(slot);
            Check(hex.StartsWith("#") && hex.Length == 7, $"{slot} 의 색 값이 이상함: {hex}");
            Check(slotHexes.Add(hex), $"{slot} 의 색이 다른 자리와 겹침: {hex}");
        }

        // 장비가 아니면 색을 입히지 않는다. 소모품까지 물들면 가려낼 수가 없다.
        Check(ItemMark.SlotHexOf(EquipmentSlot.None) == "", "장비가 아닌데 색이 있음");
        Check(ItemMark.SlotColored("사과", EquipmentSlot.None) == "사과", "소모품에 색이 붙음");

        // 태그가 이름 전체를 감싸고 거기서 닫힌다.
        string colored = ItemMark.SlotColored("나무몽둥이+2", EquipmentSlot.Weapon);
        Check(colored.StartsWith($"<color={ItemMark.WeaponHex}>") && colored.EndsWith("</color>"),
              $"무기 색 태그 모양이 다름: {colored}");

        int nameOpen = colored.IndexOf('>');
        int nameClose = colored.IndexOf("</color>");
        Check(nameOpen > 0 && nameClose > nameOpen, $"이름 색 태그가 안 닫힘: {colored}");
        if (nameOpen > 0 && nameClose > nameOpen)
            Check(colored.Substring(nameOpen + 1, nameClose - nameOpen - 1) == "나무몽둥이+2",
                  $"태그가 이름 전체를 감싸지 않음: {colored}");

        // 빈 이름에 태그만 남기지 않는다.
        Check(ItemMark.SlotColored("", EquipmentSlot.Armor) == "", "빈 이름에 태그가 붙음");

        // 셋이 함께 붙는 모습. 장착 표시가 맨 앞에 온다.
        string together = equipped + sticky + ItemMark.Prefix(MarkShape.Circle, MarkColor.Red);
        Check(together.IndexOf(ItemMark.EquippedGlyph) < together.IndexOf(ItemMark.StickyGlyph),
              "장착 표시가 끈적끈적보다 뒤에 옴");
        Check(together.IndexOf(ItemMark.StickyGlyph) < together.IndexOf("●"),
              "게임이 붙인 표시가 내가 적은 표식보다 뒤에 옴");

        // ── 표식 수첩(종류 단위) ──────────────────────────────────────────
        var book = new MarkBook();
        const int apple = 20001;
        const int seed = 30001;

        // 6) 적어 두기 전에는 아무것도 붙지 않는다.
        Check(book.IsEmpty, "새 수첩에 적힌 것이 있음");
        Check(book.PrefixOf(apple) == "", "안 적었는데 표식이 나옴");

        // 7) 번호로 적고 번호로 꺼낸다. 같은 번호면 어느 개체든 같은 표식이다 —
        //    던지거나 바닥에 둬도 표식이 유지되는 것이 이 성질에서 나온다.
        book.Set(apple, MarkShape.Triangle, MarkColor.SkyBlue);
        Check(book.PrefixOf(apple) == ItemMark.Prefix(MarkShape.Triangle, MarkColor.SkyBlue),
              "적어 둔 표식이 그대로 나오지 않음");
        Check(book.PrefixOf(seed) == "", "다른 종류에까지 표식이 번짐");

        // 8) 다시 적으면 덮어쓴다.
        book.Set(apple, MarkShape.Square, MarkColor.Purple);
        Check(book.TryGet(apple, out MarkShape s2, out MarkColor c2), "덮어쓴 뒤 사라짐");
        Check(s2 == MarkShape.Square && c2 == MarkColor.Purple, "덮어쓰기가 안 먹음");

        // 9) 모양을 None 으로 적으면 지우는 것이다.
        book.Set(apple, MarkShape.None, MarkColor.Red);
        Check(book.PrefixOf(apple) == "", "지웠는데 표식이 남음");
        Check(book.IsEmpty, "지웠는데 수첩이 비지 않음");

        // 10) 번호 0(정의가 연결되지 않은 아이템)은 받지 않는다.
        //     받아 두면 서로 다른 물건들이 0 번 하나를 나눠 갖는다.
        book.Set(0, MarkShape.Circle, MarkColor.Red);
        Check(book.IsEmpty, "번호 0 에 표식이 붙음");

        // 11) 새 판에서 지워진다.
        book.Set(apple, MarkShape.Circle, MarkColor.Green);
        book.Clear();
        Check(book.IsEmpty, "비웠는데 남아 있음");

        Console.WriteLine("  모양 셋 · 색 여덟 · 태그 닫힘 · 지우기 · 종류 단위 수첩 검사 완료");
    }

    // ── 가방 정리 차례 ──────────────────────────────────────────────────────
    //
    // 차례가 틀려도 "원래 이런가 보다" 하고 넘어가기 쉽다. 특히 문화권 비교를
    // 쓰면 돌리는 기기에 따라 답이 달라져서, 한 곳에서 확인해도 소용이 없다.
    static void TestItemOrder()
    {
        Console.WriteLine("[가방 정리 차례]");

        // 1) 한글은 가나다 순으로 선다.
        //    완성형 한글이 초성 → 중성 → 종성 차례로 배열돼 있어서 코드 값
        //    순서가 곧 사전 순서다. 그 성질에 기대고 있으므로 여기서 못 박는다.
        var names = new List<string> { "회복씨앗", "사과", "기력회복약", "힘의구슬" };
        ItemOrder.SortNames(names);

        Check(names[0] == "기력회복약", $"첫째가 다름: {names[0]}");
        Check(names[1] == "사과", $"둘째가 다름: {names[1]}");
        Check(names[2] == "회복씨앗", $"셋째가 다름: {names[2]}");
        Check(names[3] == "힘의구슬", $"넷째가 다름: {names[3]}");

        // 2) 같은 글자로 시작하면 다음 글자로 가른다.
        var similar = new List<string> { "회복약", "회복씨앗", "회복" };
        ItemOrder.SortNames(similar);
        Check(similar[0] == "회복", $"짧은 것이 먼저 오지 않음: {similar[0]}");
        Check(similar[1] == "회복씨앗" && similar[2] == "회복약",
              $"같은 앞글자 차례가 다름: {similar[1]}, {similar[2]}");

        // 3) 이름이 없는 것은 뒤로 보낸다.
        var withEmpty = new List<string> { null, "사과", "", "기력회복약" };
        ItemOrder.SortNames(withEmpty);
        Check(withEmpty[0] == "기력회복약" && withEmpty[1] == "사과",
              "이름 있는 것이 앞으로 오지 않음");
        Check(string.IsNullOrEmpty(withEmpty[2]) && string.IsNullOrEmpty(withEmpty[3]),
              "이름 없는 것이 뒤로 가지 않음");

        // 4) 견주기 자체의 성질. 뒤집으면 부호도 뒤집혀야 정렬이 안정된다.
        Check(ItemOrder.CompareByName("사과", "사과") == 0, "같은 이름인데 다르다고 함");
        Check(ItemOrder.CompareByName("사과", "회복") < 0, "사과가 회복보다 뒤에 옴");
        Check(ItemOrder.CompareByName("회복", "사과") > 0, "뒤집었는데 부호가 안 뒤집힘");
        Check(ItemOrder.CompareByName(null, null) == 0, "둘 다 비었는데 다르다고 함");

        // 5) 빈 목록·null 에 넘어지지 않는다.
        ItemOrder.SortNames(null);
        ItemOrder.SortNames(new List<string>());

        // ── 차고 있는 것이 맨 앞 ──────────────────────────────────────────
        //
        // 무기 → 방어구 → 장신구 차례로 서고, 나머지는 그 뒤에서 이름순이다.
        // 차고 있지 않은 장비는 나머지에 섞인다.

        // 6) 자리마다 차례가 다르고, 차고 있지 않은 것은 전부 맨 뒤 무리다.
        Check(ItemOrder.RankOf(EquipmentSlot.Weapon) < ItemOrder.RankOf(EquipmentSlot.Armor),
              "무기가 방어구보다 앞이 아님");
        Check(ItemOrder.RankOf(EquipmentSlot.Armor) < ItemOrder.RankOf(EquipmentSlot.Accessory),
              "방어구가 장신구보다 앞이 아님");
        Check(ItemOrder.RankOf(EquipmentSlot.Accessory) < ItemOrder.RankOf(EquipmentSlot.None),
              "차고 있는 것이 안 차고 있는 것보다 뒤에 옴");

        // 7) 실제로 정리해 본다. 가방에 차고 있는 셋과 아닌 넷이 섞여 있다.
        //    "나무몽둥이" 는 하나는 차고 있고 하나는 여벌이다 — 여벌은 이름순 무리로 간다.
        var bag = new List<(EquipmentSlot equipped, string name)>
        {
            (EquipmentSlot.None, "사과"),
            (EquipmentSlot.None, "나무몽둥이"),      // 여벌 무기
            (EquipmentSlot.Accessory, "구리반지"),
            (EquipmentSlot.None, "기력회복약"),
            (EquipmentSlot.Weapon, "나무몽둥이"),
            (EquipmentSlot.None, "회복씨앗"),
            (EquipmentSlot.Armor, "헌옷"),
        };
        bag.Sort((a, b) => ItemOrder.Compare(a.equipped, a.name, b.equipped, b.name));

        Check(bag[0].equipped == EquipmentSlot.Weapon, $"첫째가 차고 있는 무기가 아님: {bag[0].name}");
        Check(bag[1].equipped == EquipmentSlot.Armor, $"둘째가 차고 있는 방어구가 아님: {bag[1].name}");
        Check(bag[2].equipped == EquipmentSlot.Accessory, $"셋째가 차고 있는 장신구가 아님: {bag[2].name}");

        // 나머지 넷은 이름순이다. 여벌 나무몽둥이가 여기 섞여 있어야 한다.
        Check(bag[3].name == "기력회복약", $"넷째가 다름: {bag[3].name}");
        Check(bag[4].name == "나무몽둥이" && bag[4].equipped == EquipmentSlot.None,
              $"여벌 장비가 이름순 무리에 없음: {bag[4].name}");
        Check(bag[5].name == "사과", $"여섯째가 다름: {bag[5].name}");
        Check(bag[6].name == "회복씨앗", $"일곱째가 다름: {bag[6].name}");

        // 8) 견주기 자체의 성질. 뒤집으면 부호도 뒤집혀야 한다.
        Check(ItemOrder.Compare(EquipmentSlot.Weapon, "힣", EquipmentSlot.None, "가") < 0,
              "차고 있는 것이 이름에 밀림");
        Check(ItemOrder.Compare(EquipmentSlot.None, "가", EquipmentSlot.Weapon, "힣") > 0,
              "뒤집었는데 부호가 안 뒤집힘");
        Check(ItemOrder.Compare(EquipmentSlot.Weapon, "사과", EquipmentSlot.Weapon, "사과") == 0,
              "같은 것인데 다르다고 함");

        Console.WriteLine("  가나다 순 · 같은 앞글자 · 이름 없는 것 뒤로 · 차고 있는 것 먼저 검사 완료");
    }

    // ── 가중치로 고르기 ─────────────────────────────────────────────────────
    //
    // 실제로 났던 버그를 못 박아 둔다. 프리팹은 여덟 개인데 가중치가 다섯 개
    // 그대로였고, 반복문이 짧은 쪽에 맞춰 도는 바람에 <b>뒤의 세 개가 영영
    // 나오지 않았다.</b> 에러가 나지 않아서 "장비가 왜 안 떨어지지" 로만 보였다.
    static void TestWeightedPick()
    {
        Console.WriteLine("[가중치로 고르기]");

        var weights = new List<float> { 3f, 3f, 3f, 2f, 1f, 5f, 5f, 5f };

        // 1) 모든 자리가 실제로 뽑힌다. 뒤쪽이 빠지지 않는지가 핵심이다.
        Check(Math.Abs(WeightedPick.Total(weights) - 27f) < 0.001f,
              $"합이 다름 {WeightedPick.Total(weights)}");

        var seen = new HashSet<int>();
        for (float roll = 0f; roll < 27f; roll += 0.25f)
            seen.Add(WeightedPick.Choose(weights, roll));

        Check(seen.Count == weights.Count,
              $"뽑히지 않는 자리가 있음 — {seen.Count}/{weights.Count} 만 나옴");

        for (int i = 0; i < weights.Count; i++)
            Check(seen.Contains(i), $"{i} 번째가 한 번도 안 나옴");

        // 2) 경계값. 각 구간의 첫 값이 그 자리를 가리켜야 한다.
        Check(WeightedPick.Choose(weights, 0f) == 0, "맨 앞이 0 번이 아님");
        Check(WeightedPick.Choose(weights, 2.999f) == 0, "첫 구간 끝이 넘어감");
        Check(WeightedPick.Choose(weights, 3f) == 1, "둘째 구간이 안 시작됨");
        Check(WeightedPick.Choose(weights, 26.999f) == 7, "마지막 자리가 안 나옴");

        // 3) 가중치 0 은 건너뛴다. "이건 바닥에 안 나온다" 를 적을 수 있어야 한다.
        var withZero = new List<float> { 0f, 5f, 0f };
        Check(WeightedPick.Total(withZero) == 5f, "0 이 합에 섞임");
        for (float roll = 0f; roll < 5f; roll += 0.5f)
            Check(WeightedPick.Choose(withZero, roll) == 1, $"0 짜리가 뽑힘 (roll {roll})");

        // 4) 전부 0 이거나 비어 있으면 고를 것이 없다.
        Check(WeightedPick.Choose(new List<float> { 0f, 0f }, 0f) == WeightedPick.NoChoice,
              "전부 0 인데 뭔가 뽑힘");
        Check(WeightedPick.Choose(new List<float>(), 0f) == WeightedPick.NoChoice,
              "빈 목록에서 뭔가 뽑힘");
        Check(WeightedPick.Choose(null, 0f) == WeightedPick.NoChoice, "null 에서 뭔가 뽑힘");
        Check(WeightedPick.Total(null) == 0f, "null 의 합이 0 이 아님");

        // 5) 소수점 오차로 끝을 살짝 넘겨도 마지막 것을 준다.
        Check(WeightedPick.Choose(weights, 27f) == 7, "끝을 넘겼을 때 아무것도 안 나옴");

        Console.WriteLine("  모든 자리가 뽑히는가 · 경계값 · 가중치 0 · 빈 목록 검사 완료");
    }

    // ── 강화 수치 — 무엇에 붙고, 얼마나 붙고, 어떻게 보이는가 ────────────────
    static void TestItemEnhancement()
    {
        Console.WriteLine("[강화 수치]");

        var club = new ItemDefinition   // 나무몽둥이
        {
            Id = 50001, Name = "나무몽둥이", Kind = ItemKind.Equipment,
            Slot = EquipmentSlot.Weapon, BasePower = 2,
        };
        var rags = new ItemDefinition   // 헌옷
        {
            Id = 60001, Name = "헌옷", Kind = ItemKind.Equipment,
            Slot = EquipmentSlot.Armor, BasePower = 1,
        };
        var apple = new ItemDefinition  // 사과
        {
            Id = 20001, Name = "사과", Kind = ItemKind.Food, Slot = EquipmentSlot.None,
        };

        // 1) 장비만 강화된다. 사과에 +2 가 붙으면 더해질 성능이 없는데 이름에만 뜬다.
        for (float roll = 0f; roll <= 1f; roll += 0.01f)
        {
            int level = ItemEnhancement.OnSpawn(club, roll);
            Check(level >= 0 && level <= ItemEnhancement.MaxOnSpawn,
                  $"강화가 0~{ItemEnhancement.MaxOnSpawn} 밖 — {level} (roll {roll})");

            Check(ItemEnhancement.OnSpawn(apple, roll) == 0, $"장비가 아닌데 강화됨 (roll {roll})");
        }

        // 표에 없는 번호. 정의를 못 찾았는데 강화가 붙으면 어긋난 채로 굴러간다.
        Check(ItemEnhancement.OnSpawn(null, 0.99f) == 0, "정의가 없는데 강화됨");

        // 2) 양 끝. Random.value 는 1 을 낼 수 있어 위끝이 넘어가면 안 된다.
        Check(ItemEnhancement.OnSpawn(club, 0f) == 0, "맨 앞이 +0 이 아님");
        Check(ItemEnhancement.OnSpawn(club, 1f) == ItemEnhancement.MaxOnSpawn, "위끝이 최대가 아님");

        // 3) 낮은 수치가 더 흔하다. 이것이 뒤집히면 +2 가 흔해져 강화의 뜻이 없어진다.
        var counts = new int[ItemEnhancement.MaxOnSpawn + 1];
        const int Rolls = 10000;
        for (int i = 0; i < Rolls; i++)
            counts[ItemEnhancement.OnSpawn(club, (i + 0.5f) / Rolls)]++;

        for (int level = 1; level <= ItemEnhancement.MaxOnSpawn; level++)
            Check(counts[level] < counts[level - 1],
                  $"+{level} 가 +{level - 1} 보다 드물지 않음 — {counts[level]} vs {counts[level - 1]}");

        // 그러면서도 모든 수치가 실제로 나와야 한다. 하나라도 0 이면 영영 안 나오는 것이다.
        for (int level = 0; level <= ItemEnhancement.MaxOnSpawn; level++)
            Check(counts[level] > 0, $"+{level} 가 한 번도 안 나옴");

        // 절반씩 줄어드는 규칙이 실제로 그 비율로 나오는가(오차 1%p 안).
        Check(Math.Abs(counts[0] / (float)Rolls - 4f / 7f) < 0.01f, $"+0 의 비율이 다름 — {counts[0]}/{Rolls}");
        Check(Math.Abs(counts[2] / (float)Rolls - 1f / 7f) < 0.01f, $"+2 의 비율이 다름 — {counts[2]}/{Rolls}");

        // 4) +0 은 이름에 붙이지 않는다. 초반에는 대부분이 +0 이라 전부 지저분해진다.
        Check(ItemEnhancement.Suffix(0) == "", "+0 이 이름에 붙음");
        Check(ItemEnhancement.Suffix(1) == "+1", "+1 이 안 붙음");
        Check(ItemEnhancement.Suffix(2) == "+2", "+2 가 안 붙음");

        // 5) 이름에 붙는 모습. 가방·발밑·알림이 전부 이 꼴을 쓴다.
        Check(club.Name + ItemEnhancement.Suffix(2) == "나무몽둥이+2", "이름에 강화가 안 붙음");
        Check(rags.Name + ItemEnhancement.Suffix(0) == "헌옷", "강화 0 인데 이름이 달라짐");

        // 6) 주웠을 때의 문구. 이름과 강화만 있고 성능 숫자는 붙지 않는다.
        string picked = MessageCatalog.Format(
            GameMessage.ItemPickedUpFromGround, club.Name + ItemEnhancement.Suffix(2));

        Check(picked.Contains("나무몽둥이+2"), $"알림에 강화가 없음 — {picked}");
        Check(picked.Contains("주웠다"), $"주웠다는 말이 없음 — {picked}");
        Check(!picked.Contains("공격력") && !picked.Contains("\n"),
              $"알림에 성능 줄이 붙음 — {picked}");

        // 7) 가방이 가득 찼을 때. 무엇을 못 주웠는지 말해 주지 않으면, 여러 개가
        //    겹쳐 놓인 칸에서 어느 것이 남았는지 알 수 없다.
        string full = MessageCatalog.Format(
            GameMessage.BagFull, club.Name + ItemEnhancement.Suffix(1));

        Check(full.Contains("나무몽둥이+1"), $"못 주운 것의 이름이 없음 — {full}");
        Check(full.Contains("가득") && full.Contains("줍지 못했다"), $"가방이 찼다는 말이 없음 — {full}");

        Console.WriteLine(
            "  장비만 강화 · 양 끝 · 낮은 쪽이 흔한가 · 절반 비율 · +0 감추기 · 이름 · 문구 검사 완료");
    }

    // ── 시트 색인 ───────────────────────────────────────────────────────────
    //
    // 카탈로그가 목록을 훑던 것을 색인으로 바꿨다. <b>답이 달라지면 안 된다</b> —
    // 표는 그대로인데 게임이 달라지면, 표를 고친 사람은 무엇이 바뀐 건지 알 수 없다.
    static void TestSheetIndex()
    {
        Console.WriteLine("[시트 색인]");

        var rows = new List<ItemDefinition>
        {
            new ItemDefinition { Id = 20001, Name = "사과" },
            new ItemDefinition { Id = 50001, Name = "나무몽둥이" },
            new ItemDefinition { Id = 20001, Name = "나중에 적은 사과" },   // 번호가 겹친 줄
        };

        var byId = SheetIndex.Build(rows, d => d.Id);

        // 1) 있는 번호를 찾고, 없는 번호는 못 찾는다.
        Check(byId.TryGetValue(50001, out ItemDefinition club) && club.Name == "나무몽둥이",
              "있는 번호를 못 찾음");
        Check(!byId.ContainsKey(99999), "없는 번호가 찾아짐");

        // 2) 번호가 겹치면 앞의 것을 남긴다. 훑어서 처음 만난 것을 돌려주던
        //    예전과 같은 답이어야 한다.
        Check(byId[20001].Name == "사과", $"겹친 번호에서 뒤의 것이 이김: {byId[20001].Name}");
        Check(byId.Count == 2, $"줄 수가 다름: {byId.Count}");

        // 3) 이름(문자열)으로도 색인한다. 함정이 이 길을 쓴다.
        var traps = new List<ItemDefinition>
        {
            new ItemDefinition { Id = 1, Name = "gas" },
            new ItemDefinition { Id = 2, Name = "dart" },
        };
        var byName = SheetIndex.Build(traps, d => d.Name);
        Check(byName.TryGetValue("dart", out ItemDefinition dart) && dart.Id == 2,
              "이름으로 못 찾음");

        // 4) 열쇠가 없는 줄은 건너뛴다. Dictionary 는 null 열쇠를 받지 않으므로
        //    걸러 내지 않으면 색인을 만드는 그 자리에서 터진다.
        var withNulls = new List<ItemDefinition>
        {
            new ItemDefinition { Id = 1, Name = null },
            null,
            new ItemDefinition { Id = 2, Name = "사과" },
        };
        //    걸러 내지 못하면 여기서 예외가 난다. 잡아서 실패로 알린다 —
        //    그냥 두면 검사가 통째로 죽어 아래 것들이 아예 돌지 않는다.
        Dictionary<string, ItemDefinition> safe;
        try
        {
            safe = SheetIndex.Build(withNulls, d => d.Name);
        }
        catch (Exception e)
        {
            Check(false, $"이름 없는 줄에서 터짐: {e.GetType().Name}");
            safe = new Dictionary<string, ItemDefinition>();
        }

        Check(safe.Count == 1 && safe.ContainsKey("사과"), $"이름 없는 줄이 섞임: {safe.Count}");

        // 5) 빈 목록·null 에 넘어지지 않는다.
        Check(SheetIndex.Build(new List<ItemDefinition>(), d => d.Id).Count == 0,
              "빈 목록에서 뭔가 나옴");
        Check(SheetIndex.Build<int, ItemDefinition>(null, d => d.Id).Count == 0,
              "null 목록에서 뭔가 나옴");
        Check(SheetIndex.Build<int, ItemDefinition>(rows, null).Count == 0,
              "열쇠 뽑는 법이 없는데 색인이 만들어짐");

        Console.WriteLine("  찾기 · 겹친 번호는 앞의 것 · 이름 색인 · 빈 열쇠 · null 검사 완료");
    }

    // ── 동료 표 · 특성 표 ───────────────────────────────────────────────────
    //
    // 시트 둘이 번호로 이어져 있다(partner.skill1~5 → Trait.id). 그 이음매가
    // 어긋나면 그 특성만 조용히 없는 것이 되고, 동료는 멀쩡히 따라온다.
    static void TestPartnerSheet()
    {
        Console.WriteLine("[동료 표]");

        var traitRows = new List<TraitEntity>
        {
            new TraitEntity { id = 101, name = "혼신", explanation = "치명타시 방어력을 무시한다." },
            new TraitEntity { id = 105, name = "건강", explanation = "상태 이상 회복이 빠르다." },
            new TraitEntity { id = 119, name = "투지", explanation = "체력이 낮을수록 치명타율이 증가한다." },
            new TraitEntity { id = 0, name = "번호 없음" },        // 버려질 줄
            new TraitEntity { id = 118 },                          // 이름이 빈 줄
        };
        var traitProblems = new List<string>();
        var traits = TraitSheet.FromEntities(traitRows, traitProblems);

        Check(traits.Count == 4, $"특성 줄 수가 다름 {traits.Count}");
        Check(traits[0].Id == 101 && traits[0].Name == "혼신", "특성이 제자리에 안 들어감");
        Check(traitProblems.Count == 1, $"이름 빈 특성을 {traitProblems.Count}건 알림 (기대 1)");

        var partnerRows = new List<PartnerEntity>
        {
            new PartnerEntity
            {
                id = 10002, name = "여전사", hp = 25, atk = 10, def = 2, bag = 5,
                skill1 = 101, skill2 = 105, skill3 = 119,      // skill4~6 은 빈칸
                gender = "여성", category = "인간",
                explanation = "강한 육체를 만들기 위해",
                note = "사람이 읽는 메모",
            },
        };
        var partnerProblems = new List<string>();
        var partners = PartnerSheet.FromEntities(partnerRows, partnerProblems);

        // 1) 수치가 제자리로 들어간다. 열이 여섯이라 한 칸만 밀려도 알아채기 어렵다.
        //    atk 와 def 를 다른 값으로 둔 것은 바꿔치기가 티 나게 하기 위해서다.
        PartnerDefinition warrior = partners[0];
        Check(warrior.Hp == 25, $"체력이 다름 {warrior.Hp}");
        Check(warrior.Attack == 10, $"공격력이 다름 {warrior.Attack}");
        Check(warrior.Defense == 2, $"방어력이 다름 {warrior.Defense}");
        Check(warrior.BagSize == 5, $"가방 칸 수가 다름 {warrior.BagSize}");
        Check(warrior.Note == "사람이 읽는 메모", "메모가 안 들어감");

        // 종족과 성별은 열이 둘이다. 한때 한 칸에 "인간, 여성" 으로 붙어 있었고,
        // 열을 가른 뒤 엔티티를 안 고쳐서 gender 가 통째로 버려진 적이 있다.
        Check(warrior.Gender == "여성", $"성별이 다름 '{warrior.Gender}'");
        Check(warrior.Category == "인간", $"종족이 다름 '{warrior.Category}'");

        // 2) 빈 특성 칸은 담기지 않는다. 담아 두면 0 번을 찾으러 다니게 된다.
        Check(warrior.TraitIds.Count == 3, $"특성 수가 다름 {warrior.TraitIds.Count}");
        Check(warrior.TraitIds[0] == 101 && warrior.TraitIds[2] == 119, "특성 차례가 다름");
        Check(!warrior.TraitIds.Contains(0), "빈 칸이 0 번 특성으로 담김");
        Check(partnerProblems.Count == 0, $"멀쩡한 줄에 알림이 뜸 ({partnerProblems.Count}건)");

        // 여섯째 칸까지 읽는다. 열을 늘렸는데 코드가 안 따라가면 그 칸만 조용히
        // 사라진다 — skill6 이 실제로 그렇게 한 번 버려졌다.
        var sixth = PartnerSheet.FromEntities(
            new List<PartnerEntity>
            {
                new PartnerEntity { id = 10099, name = "여섯", hp = 10, skill6 = 118 },
            });
        Check(sixth[0].TraitIds.Count == 1 && sixth[0].TraitIds[0] == 118,
              $"skill6 이 버려짐 ({sixth[0].TraitIds.Count}개)");

        // 3) 같은 특성을 두 번 적으면 한 번만 담고 알려 준다.
        var dupProblems = new List<string>();
        var dup = PartnerSheet.FromEntities(
            new List<PartnerEntity>
            {
                new PartnerEntity { id = 10003, name = "겹침", hp = 10, skill1 = 105, skill2 = 105 },
            },
            dupProblems);

        Check(dup[0].TraitIds.Count == 1, $"겹친 특성이 둘로 담김 {dup[0].TraitIds.Count}");
        Check(dupProblems.Count == 1, $"겹친 특성을 안 알림 ({dupProblems.Count}건)");

        // 4) 체력이 0 이면 따라오자마자 쓰러진다. 빈칸을 눈치채기 어려운 자리다.
        var weakProblems = new List<string>();
        PartnerSheet.FromEntities(
            new List<PartnerEntity> { new PartnerEntity { id = 10004, name = "체력없음", hp = 0 } },
            weakProblems);
        Check(weakProblems.Count == 1, $"체력 0 을 안 알림 ({weakProblems.Count}건)");

        // 5) 표에 없는 특성 번호를 가리키면 알려 준다. 이것이 이 검사의 핵심이다 —
        //    번호를 잘못 적어도 게임은 멀쩡히 돌고 에러가 나지 않는다.
        var crossProblems = new List<string>();
        var stranger = PartnerSheet.FromEntities(
            new List<PartnerEntity>
            {
                new PartnerEntity { id = 10005, name = "없는특성", hp = 10, skill1 = 105, skill2 = 999 },
            });

        PartnerSheet.ReportUnknownTraits(stranger, traits, crossProblems);
        Check(crossProblems.Count == 1, $"없는 특성 번호를 {crossProblems.Count}건 알림 (기대 1)");
        Check(crossProblems.Count > 0 && crossProblems[0].Contains("999"),
              "어느 번호가 없는지 안 알려 줌");

        // 멀쩡히 이어져 있으면 아무 말도 하지 않는다.
        var quiet = new List<string>();
        PartnerSheet.ReportUnknownTraits(partners, traits, quiet);
        Check(quiet.Count == 0, $"멀쩡한 이음매에 알림이 뜸 ({quiet.Count}건)");

        // 6) 빈 표에도 넘어지지 않는다.
        Check(PartnerSheet.FromEntities(null).Count == 0, "null 에서 동료가 나옴");
        Check(TraitSheet.FromEntities(null).Count == 0, "null 에서 특성이 나옴");

        Console.WriteLine("  옮겨 담기 · 수치 자리 · 빈 특성 칸 · 겹친 특성 · 없는 특성 번호 검사 완료");
    }

    // ── 동료 대사 ───────────────────────────────────────────────────────────
    //
    // 열 이름의 숫자(hp80)는 "80% 일 때" 가 아니라 "80% 이상일 때" 다.
    // 그렇게 읽지 않으면 79% 처럼 어느 칸에도 안 걸리는 구간이 생긴다.
    static void TestPartnerLines()
    {
        Console.WriteLine("[동료 대사]");

        var rows = new List<PartnerLineEntity>
        {
            new PartnerLineEntity
            {
                id = 10001, name = "모험가", meet = "같이 동행할까?",
                hp100 = "백", hp80 = "팔십", hp60 = "육십",
                hp30 = "삼십", hp10 = "십", hp1 = "하나",
                action = "출구를 추측해 볼까?", actioncase1 = "가", actioncase2 = "나", actioncase3 = "다",
            },
            // 만났을 때만 있고 나머지는 비어 있다. 열둘 중 일곱이 이 모양이다.
            new PartnerLineEntity { id = 10006, name = "베테랑", meet = "나와 함께 간다면" },
        };
        var problems = new List<string>();
        var sets = PartnerLineSheet.FromEntities(rows, problems);

        // 1) 대사가 비는 것은 잘못이 아니다. 채워 가는 표라서 알리지 않는다.
        Check(sets.Count == 2, $"줄 수가 다름 {sets.Count}");
        Check(problems.Count == 0, $"빈 대사를 잘못으로 알림 ({problems.Count}건)");

        PartnerLineSet full = sets[0];
        Check(full.Meet == "같이 동행할까?", "만났을 때 대사가 안 들어감");
        Check(full.Action == "출구를 추측해 볼까?", "고유 행동 대사가 안 들어감");
        Check(full.ActionCases[2] == "다", "행동 결과 셋째가 안 들어감");

        // 2) 구간마다 제 대사가 나온다. 경계값이 핵심이다.
        Check(PartnerLineSheet.LineForPercent(full, 100) == "백", "100% 가 다른 칸을 봄");
        Check(PartnerLineSheet.LineForPercent(full, 99) == "팔십", "99% 가 hp80 칸이 아님");
        Check(PartnerLineSheet.LineForPercent(full, 80) == "팔십", "80% 경계가 어긋남");
        Check(PartnerLineSheet.LineForPercent(full, 79) == "육십", "79% 가 어느 칸에도 안 걸림");
        Check(PartnerLineSheet.LineForPercent(full, 60) == "육십", "60% 경계가 어긋남");
        Check(PartnerLineSheet.LineForPercent(full, 59) == "삼십", "59% 가 어긋남");
        Check(PartnerLineSheet.LineForPercent(full, 30) == "삼십", "30% 경계가 어긋남");
        Check(PartnerLineSheet.LineForPercent(full, 29) == "십", "29% 가 어긋남");
        Check(PartnerLineSheet.LineForPercent(full, 10) == "십", "10% 경계가 어긋남");
        Check(PartnerLineSheet.LineForPercent(full, 9) == "하나", "9% 가 어긋남");
        Check(PartnerLineSheet.LineForPercent(full, 1) == "하나", "1% 경계가 어긋남");

        // 3) 쓰러졌으면 대사가 없다. 그건 대사가 아니라 쓰러질 때의 연출이다.
        Check(PartnerLineSheet.LineForPercent(full, 0) == "", "0% 인데 말을 함");

        // 4) 빈 칸이면 위 칸으로 올라가 대신 읽지 않는다.
        //    그렇게 하면 빈사인데 "여유롭습니다" 를 말하게 된다.
        PartnerLineSet meetOnly = sets[1];
        Check(PartnerLineSheet.LineForPercent(meetOnly, 100) == "", "빈 칸인데 말을 함");
        Check(PartnerLineSheet.LineForPercent(meetOnly, 5) == "", "빈 칸에서 위 칸을 대신 읽음");
        Check(PartnerLineSheet.LineForPercent(null, 50) == "", "대사가 없는데 말을 함");

        // 5) 백분율 계산. 내림이라야 한 대 맞고도 100% 대사를 하지 않는다.
        Check(PartnerLineSheet.PercentOf(25, 25) == 100, "가득인데 100 이 아님");
        Check(PartnerLineSheet.PercentOf(24, 25) == 96, $"내림이 아님 {PartnerLineSheet.PercentOf(24, 25)}");
        Check(PartnerLineSheet.PercentOf(1, 25) == 4, "낮은 쪽 계산이 다름");
        Check(PartnerLineSheet.PercentOf(0, 25) == 0, "0 이 0 이 아님");
        Check(PartnerLineSheet.PercentOf(10, 0) == 0, "최대가 0 인데 나눔");

        Check(PartnerLineSheet.FromEntities(null).Count == 0, "null 에서 대사가 나옴");

        Console.WriteLine("  옮겨 담기 · 구간 경계 · 빈 칸은 말하지 않음 · 백분율 검사 완료");
    }

    // ── 늘어날 것들의 자리 ──────────────────────────────────────────────────
    //
    // 함정 · 특성 · 상태이상은 앞으로 계속 늘어난다. 늘릴 때 <b>고칠 곳이
    // 한 군데인가</b>를 못 박아 둔다. 표에 없는 것이 조용히 도는 일도 함께 본다.
    static void TestExtensionPoints()
    {
        Console.WriteLine("[늘어날 것들의 자리]");

        // 1) 함정. 시트의 id2 로 찾고, 아직 안 만든 것은 아무 일도 하지 않는다.
        foreach (string id in new[]
                 { TrapIds.Hole, TrapIds.Warp, TrapIds.Blow, TrapIds.Dart,
                   TrapIds.Gas, TrapIds.Spin, TrapIds.Restraint, TrapIds.Sticky })
        {
            Check(TrapActions.IsImplemented(id), $"{id} 함정에 동작이 없음");
        }

        // 아직 안 만든 함정(soil)과 없는 이름은 조용히 아무 일도 하지 않는다.
        Check(!TrapActions.IsImplemented(TrapIds.Soil), "soil 이 이미 만들어져 있음");
        Check(!TrapActions.IsImplemented("없는함정"), "모르는 함정에 동작이 붙음");
        Check(!TrapActions.IsImplemented(null), "null 에 동작이 붙음");
        Check(TrapActions.Find(TrapIds.Soil) == null, "안 만든 함정이 무언가를 함");

        // 워프만 몸짓을 밖에서 돌리지 않는다. 안에서 이미 돌기 때문에
        // 밖에서 또 돌리면 두 바퀴가 아니라 네 바퀴를 돈다.
        Check(!TrapActions.PlaysAnimationFirst(TrapIds.Warp), "워프가 밖에서도 돎");
        Check(TrapActions.PlaysAnimationFirst(TrapIds.Gas), "가스가 몸짓을 안 함");

        // 2) 특성. 표에 있는 것만 실제로 무언가 한다.
        Check(TraitEffects.IsImplemented(TraitEffects.SureHit), "필중에 동작이 없음");
        Check(TraitEffects.IsImplemented(TraitEffects.DoubleStrike), "연격에 동작이 없음");
        Check(!TraitEffects.IsImplemented(TraitEffects.Hoarder), "수집광이 이미 만들어져 있음");
        Check(!TraitEffects.IsImplemented(99999), "없는 번호에 동작이 붙음");

        var sureHit = new List<int> { TraitEffects.SureHit };
        var none = new List<int> { TraitEffects.Hoarder, TraitEffects.Pure };

        Check(TraitEffects.AlwaysHits(sureHit), "필중인데 안 맞음");
        Check(!TraitEffects.AlwaysHits(none), "필중이 아닌데 반드시 맞음");
        Check(!TraitEffects.StrikesTwice(sureHit), "필중이 두 번 때림");

        // 여럿을 가진 동료. 하나라도 있으면 켜진다.
        var both = new List<int>
        {
            TraitEffects.Healthy, TraitEffects.DoubleStrike, TraitEffects.SureHit,
        };
        Check(TraitEffects.AlwaysHits(both) && TraitEffects.StrikesTwice(both),
              "여러 특성 중 하나만 먹음");

        // 특성이 없는 동료(빈 목록·null)에 넘어지지 않는다.
        Check(!TraitEffects.AlwaysHits(new List<int>()), "특성이 없는데 켜짐");
        Check(!TraitEffects.AlwaysHits(null), "null 인데 켜짐");

        // 3) 상태이상. 부르는 쪽은 효과가 아니라 <b>뜻</b>으로 묻는다.
        var asleep = new List<ConditionEffect> { ConditionEffect.SkipTurn };
        var confused = new List<ConditionEffect> { ConditionEffect.ScrambleDirection };
        var poisoned = new List<ConditionEffect> { ConditionEffect.DamagePerTurn };

        Check(!ConditionRules.CanAct(asleep), "잠들었는데 행동함");
        Check(ConditionRules.CanAct(confused), "혼란인데 행동을 못 함");
        Check(ConditionRules.CanAct(poisoned), "독인데 행동을 못 함");
        Check(ConditionRules.CanAct(null) && ConditionRules.CanAct(new List<ConditionEffect>()),
              "아무것도 안 걸렸는데 행동을 못 함");

        Check(ConditionRules.ScramblesDirection(confused), "혼란인데 방향이 안 흔들림");
        Check(!ConditionRules.ScramblesDirection(asleep), "잠인데 방향이 흔들림");

        // 여럿 동시에 걸린 경우. 각각이 따로 먹는다.
        var both2 = new List<ConditionEffect>
        {
            ConditionEffect.SkipTurn, ConditionEffect.ScrambleDirection,
        };
        Check(!ConditionRules.CanAct(both2) && ConditionRules.ScramblesDirection(both2),
              "둘 다 걸렸는데 하나만 먹음");

        Console.WriteLine("  함정 표 · 안 만든 것 · 특성 표 · 여럿 겹침 · 상태이상 뜻으로 묻기 검사 완료");
    }

    // ── 다 쓴 눌림(자리 바꾸기) ─────────────────────────────────────────────
    //
    // 자리를 바꾼 뒤 B 를 누른 채로 두면 다음 프레임에 곧바로 달리기가 걸린다.
    // 자리만 바꾸려던 손이 통로 끝까지 내달리게 되는 자리다.
    static void TestSpentPress()
    {
        Console.WriteLine("[다 쓴 눌림]");

        var button = new ButtonPress();

        // 누르고 있는 동안에는 계속 쓸 수 있다(달리기가 이 성질에 기댄다).
        button.Update(down: true, held: true, up: false);
        Check(button.HeldAvailable, "누른 순간에 못 씀");
        button.Update(down: false, held: true, up: false);
        Check(button.HeldAvailable, "누르고 있는데 못 씀");

        // 다 쓰면 손을 떼기 전까지 잠긴다.
        button.Spend();
        Check(button.Held, "다 썼다고 눌림 자체가 풀림");
        Check(!button.HeldAvailable, "다 썼는데 아직 쓸 수 있음");

        button.Update(down: false, held: true, up: false);
        Check(!button.HeldAvailable, "떼지도 않았는데 다시 쓸 수 있게 됨");

        // 뗐다 다시 누르면 풀린다.
        button.Update(down: false, held: false, up: true);
        button.Update(down: true, held: true, up: false);
        Check(button.HeldAvailable, "다시 눌렀는데 안 풀림");

        // 다 쓴 눌림은 조준으로도 새지 않는다(Consume 을 함께 하기 때문).
        var second = new ButtonPress();
        second.Update(down: true, held: true, up: false);
        second.Spend();
        second.Update(down: false, held: false, up: true);
        Check(!second.Tapped, "다 쓴 눌림이 조준으로 샘");

        // Consume 만 한 것은 잠기지 않는다 — 달리기는 누른 채로 계속 먹어야 한다.
        var third = new ButtonPress();
        third.Update(down: true, held: true, up: false);
        third.Consume();
        Check(third.HeldAvailable, "Consume 만 했는데 잠김");

        Console.WriteLine("  누른 동안 쓰기 · 다 쓰면 잠김 · 떼면 풀림 · 조준으로 안 샘 검사 완료");
    }

    // ── 동료 명단 ───────────────────────────────────────────────────────────
    //
    // 층을 넘을 때 씬을 다시 부르므로 동료 오브젝트도 사라진다. 여기 남는 값으로
    // 다시 만들기 때문에, 이 목록이 곧 "누가 따라오고 있는가" 다.
    static void TestPartnerRoster()
    {
        Console.WriteLine("[동료 명단]");

        var roster = new PartnerRoster();
        Check(roster.IsEmpty, "새 명단이 비어 있지 않음");

        // 1) 들이면 체력이 가득 찬 채로 시작한다.
        Check(roster.TryAdd(10001, 20), "동료를 못 들임");
        Check(roster.Count == 1, $"인원이 다름 {roster.Count}");
        Check(roster.Find(10001).Hp == 20 && roster.Find(10001).MaxHp == 20, "체력이 안 참");

        // 2) 같은 종류를 둘 받지 않는다.
        Check(!roster.TryAdd(10001, 20), "같은 동료를 둘 들임");
        Check(roster.Count == 1, "같은 동료로 인원이 늘어남");

        // 번호나 체력이 없는 것은 들이지 않는다(시트가 비었을 때).
        Check(!roster.TryAdd(0, 20), "번호 0 을 들임");
        Check(!roster.TryAdd(10002, 0), "체력 0 인 동료를 들임");

        // 3) 피해. 0 이하가 되면 그 자리에서 목록에서 빠진다.
        roster.TryAdd(10002, 10);
        Check(!roster.Damage(10002, 4), "안 쓰러졌는데 쓰러졌다고 함");
        Check(roster.Find(10002).Hp == 6, $"체력이 안 깎임 {roster.Find(10002).Hp}");
        Check(roster.Damage(10002, 6), "0 이 됐는데 안 쓰러짐");
        Check(!roster.Contains(10002), "쓰러졌는데 명단에 남음");
        Check(!roster.Damage(99999, 1), "없는 동료가 맞음");

        // 4) 계단. <b>계단과 같은 방</b>에 있는 동료만 따라 내려온다.
        //    걸음 수로 자르면 경계가 화면에 안 보여서, 두고 온 이유를 알 수 없다.
        //    방은 눈에 보이므로 "지금 내려가면 두고 간다" 가 한눈에 보인다.
        var party = new PartnerRoster();
        party.TryAdd(1, 10);
        party.TryAdd(2, 10);
        party.TryAdd(3, 10);

        // 안쪽 칸 (5,5)~(9,9). RectInt 의 xMax 는 마지막 칸의 '다음' 이다.
        var stairsRoom = new RectInt(5, 5, 5, 5);

        var cells = new Dictionary<int, Vector3Int>
        {
            { 1, new Vector3Int(5, 5, 0) },   // 방 구석 — 경계 안
            { 2, new Vector3Int(9, 9, 0) },   // 반대쪽 구석 — 경계 안
            { 3, new Vector3Int(10, 9, 0) },  // 한 칸 밖 = 벽/통로
        };

        List<int> left = party.DropLeftBehind(stairsRoom, cells);
        Check(left.Count == 1 && left[0] == 3, $"두고 온 동료가 다름 ({left.Count}명)");
        Check(party.Contains(1) && party.Contains(2), "같은 방 동료가 빠짐");
        Check(!party.Contains(3), "다른 자리의 동료가 따라옴");

        // 자리를 모르는 동료(통로에 있거나 목록에 없음)는 두고 간다.
        var stranded = new PartnerRoster();
        stranded.TryAdd(7, 10);
        stranded.TryAdd(8, 10);
        List<int> lost = stranded.DropLeftBehind(
            stairsRoom, new Dictionary<int, Vector3Int> { { 8, new Vector3Int(0, 0, 0) } });
        Check(lost.Count == 2, $"방 밖 동료를 안 두고 감 ({lost.Count}명)");
        Check(stranded.IsEmpty, "아무도 못 따라오는데 명단이 남음");

        // 방을 못 찾았으면 아무도 두고 가지 않는다. 동료를 잃는 것은 되돌릴 수
        // 없어서, 판단할 수 없을 때는 잃지 않는 쪽으로 둔다.
        var safe = new PartnerRoster();
        safe.TryAdd(9, 10);
        List<int> none = safe.DropLeftBehind(new RectInt(0, 0, 0, 0), cells);
        Check(none.Count == 0 && safe.Contains(9), "방을 못 찾았는데 동료를 잃음");

        // 4-1) 경험치는 동료 자신의 것이다. 플레이어는 한 점도 받지 않는다
        //      (그 판단은 Enemy.TakeDamageFromPartner 가 한다).
        var grower = new PartnerRoster();
        grower.TryAdd(50, 20);
        PartnerMember member = grower.Find(50);

        Check(member.Level == 1, "처음 레벨이 1 이 아님");
        Check(member.ExpRemaining == member.ExpToNextLevel, "안 모았는데 남은 양이 줄어 있음");

        Check(grower.AddExp(50, 3) == 0, "조금 모았는데 레벨이 오름");
        Check(member.ExpRemaining == member.ExpToNextLevel - 3,
              $"남은 양이 안 줄음 {member.ExpRemaining}");

        int before = member.MaxHp;
        int need = member.ExpRemaining;
        Check(grower.AddExp(50, need) == 1, "채웠는데 레벨이 안 오름");
        Check(member.Level == 2, $"레벨이 다름 {member.Level}");
        Check(member.MaxHp == before + PartnerRoster.LevelUpHp, "레벨이 올랐는데 체력이 안 늘음");

        // 레벨이 오르면 그만큼 회복된다. 안 그러면 한 번 깎인 체력이
        // 판이 끝날 때까지 그대로 남는다.
        Check(member.Hp == 20 + PartnerRoster.LevelUpHp, $"회복이 안 됨 {member.Hp}");

        // 한 번에 여러 레벨도 오른다.
        Check(grower.AddExp(50, 10000) >= 2, "많이 줬는데 한 레벨만 오름");

        Check(grower.AddExp(50, 0) == 0, "0 을 줬는데 뭔가 일어남");
        Check(grower.AddExp(99999, 100) == 0, "없는 동료가 자람");

        // 4-2) 자연 회복. 동료에게는 이것 말고 회복 수단이 없다(가방이 없다).
        var hurt = new PartnerRoster();
        hurt.TryAdd(60, 10);
        hurt.Damage(60, 6);
        Check(hurt.Find(60).Hp == 4, "준비 실패: 안 깎임");

        hurt.HealAll(1);
        Check(hurt.Find(60).Hp == 5, $"자연 회복이 안 됨 {hurt.Find(60).Hp}");

        hurt.HealAll(100);
        Check(hurt.Find(60).Hp == hurt.Find(60).MaxHp, "최대를 넘어 회복됨");
        hurt.HealAll(0);
        Check(hurt.Find(60).Hp == hurt.Find(60).MaxHp, "0 을 줬는데 값이 변함");

        // 4-3) 특기 쿨타임. 쓰고 나면 한동안 다시 청할 수 없다.
        Check(hurt.IsActionReady(60, 0), "처음부터 특기를 못 씀");

        hurt.StartActionCooldown(60, 100);
        Check(!hurt.IsActionReady(60, 100), "쓴 그 턴에 또 쓸 수 있음");
        Check(!hurt.IsActionReady(60, 100 + PartnerPlan.ActionCooldownTurns - 1),
              "쿨타임이 덜 지났는데 쓸 수 있음");
        Check(hurt.IsActionReady(60, 100 + PartnerPlan.ActionCooldownTurns),
              "쿨타임이 지났는데 못 씀");

        Check(!hurt.IsActionReady(99999, 0), "없는 동료가 특기를 씀");

        // 5) 새 판. 비우지 않으면 죽은 판의 동료가 따라온다 —
        //    에디터에서는 도메인 리로드가 가려 주고 빌드에서만 드러난다.
        party.Clear();
        Check(party.IsEmpty, "새 판인데 동료가 남음");

        Console.WriteLine("  들이기 · 중복 · 피해로 빠짐 · 계단과 같은 방 · 새 판 비우기 검사 완료");
    }

    // ── 동료 판단 ───────────────────────────────────────────────────────────
    //
    // 적 AI 는 판정과 이동이 섞여 있어 에디터를 켜야만 확인할 수 있었고, 실제로
    // "논리는 갈 수 있다는데 물리가 막는" 버그를 두 번 냈다. 동료는 판정을 떼어
    // 두었으므로 여기서 전부 확인한다.
    static void TestPartnerPlan()
    {
        Console.WriteLine("[동료 판단]");

        Func<Vector3Int, bool> openField = _ => true;

        // 걸음 수 지도를 대신하는 함수. 벽이 없으면 대각선 거리가 곧 걸음 수다.
        Func<Vector3Int, Vector3Int, int> stepsTo =
            (from, to) => PartnerPlan.ChebyshevDistance(from, to);

        var player = new Vector3Int(10, 10, 0);
        Func<Vector3Int, int> toPlayer = cell => stepsTo(cell, player);

        // 1) 붙어 있는 적을 때린다.
        var self = new Vector3Int(11, 10, 0);
        var enemy = new Vector3Int(12, 10, 0);
        PartnerTurn turn = PartnerPlan.Decide(
            self, player, new List<Vector3Int> { enemy }, openField, toPlayer);

        Check(turn.Action == PartnerAction.Attack, $"붙은 적을 안 때림 ({turn.Action})");
        Check(turn.Target == enemy, "엉뚱한 칸을 때림");

        // 2) 붙은 적이 둘이면 플레이어와도 붙은 쪽을 먼저 친다(협공).
        //    각자 다른 적을 치면 어느 쪽도 빨리 안 쓰러진다.
        var lone = new Vector3Int(12, 11, 0);          // 동료에만 붙음
        var shared = new Vector3Int(11, 9, 0);         // 동료·플레이어 둘 다에 붙음
        Check(PartnerPlan.IsAdjacent(player, shared), "준비 실패: 협공 대상이 플레이어에 안 붙음");
        Check(!PartnerPlan.IsAdjacent(player, lone), "준비 실패: 단독 대상이 플레이어에 붙음");

        turn = PartnerPlan.Decide(
            self, player, new List<Vector3Int> { lone, shared }, openField, toPlayer);
        Check(turn.Target == shared, $"협공 대상을 안 고름 {turn.Target}");

        // 2-1) 무기가 안 닿는 적은 <b>목록에 들어오지도 않는다</b>(Partner.EnemyCells).
        //
        //      실제로 났던 버그다. 벽 두 개가 맞물린 대각선 틈으로 동료가 손을
        //      뻗어 적을 때렸다. 무기가 어디까지 닿는지는 무기의 규칙이라
        //      (TileRules.CanReachWithMelee) 판단 쪽에 심지 않았다 — 심었으면
        //      원거리 무기를 든 동료가 생길 때 여기 예외를 파야 한다.
        //
        //      그래서 여기서 확인하는 것은 "빠진 적은 붙지 않은 것으로 다뤄지는가" 다.
        var behindCorner = new Vector3Int(12, 11, 0);   // 동료(11,10)의 대각선
        Check(PartnerPlan.IsAdjacent(self, behindCorner), "준비 실패: 대각선이 아님");

        // 목록에서 빠지면 때리지 않고, 대신 돌아서 다가간다.
        turn = PartnerPlan.Decide(
            self, player, new List<Vector3Int>(), openField, toPlayer);
        Check(turn.Action != PartnerAction.Attack, "빠진 적을 때림");

        // 목록에 있으면 똑같이 때린다.
        Check(PartnerPlan.PickAdjacentEnemy(
                  self, player, new List<Vector3Int> { behindCorner }) == behindCorner,
              "목록에 있는데 못 때림");

        // 3) 가까운 적에게는 달려든다. 플레이어 곁을 지키느라 눈앞의 적을
        //    지나치면 동료를 데리고 다닐 이유가 없다.
        var far = new Vector3Int(14, 10, 0);           // 세 칸 밖 = 붙지 않았다
        turn = PartnerPlan.Decide(
            self, player, new List<Vector3Int> { far }, openField, toPlayer);

        Check(turn.Action == PartnerAction.Move, $"가까운 적에게 안 다가감 ({turn.Action})");
        Check(PartnerPlan.ChebyshevDistance(turn.Target, far)
              < PartnerPlan.ChebyshevDistance(self, far), "적에게서 멀어짐");

        // 너무 먼 적은 못 본 것으로 한다. 안 그러면 방 저쪽 적을 보고 흩어진다.
        var tooFar = new Vector3Int(11 + PartnerPlan.EnemySeekDistance + 1, 10, 0);
        turn = PartnerPlan.Decide(
            self, player, new List<Vector3Int> { tooFar }, openField, toPlayer);
        Check(turn.Action != PartnerAction.Attack, "먼 적을 때림");
        Check(turn.Action == PartnerAction.Stay, $"먼 적을 보고 움직임 ({turn.Action})");

        // 4) 적이 없고 플레이어와 멀어졌으면 따라간다.
        var behind = new Vector3Int(10 + PartnerPlan.FollowDistance + 2, 10, 0);
        turn = PartnerPlan.Decide(
            behind, player, new List<Vector3Int>(), openField, toPlayer);

        Check(turn.Action == PartnerAction.Move, $"멀어졌는데 안 따라옴 ({turn.Action})");
        Check(toPlayer(turn.Target) < toPlayer(behind), "따라간다면서 멀어짐");

        // 4-1) 한 칸만 벌어져도 따라붙는다.
        //
        //      실제로 났던 버그다. FollowDistance 가 2 였을 때 동료는 두 칸
        //      떨어진 자리에서 멈췄고, 플레이어가 걸을 때마다 따라붙었다 멈췄다
        //      하며 <b>간격이 늘 두 칸으로 벌어진 채</b>였다.
        var oneGap = new Vector3Int(12, 10, 0);   // 플레이어(10,10)에서 두 칸
        Check(toPlayer(oneGap) == 2, "준비 실패: 두 칸이 아님");

        turn = PartnerPlan.Decide(
            oneGap, player, new List<Vector3Int>(), openField, toPlayer);
        Check(turn.Action == PartnerAction.Move, $"두 칸 벌어졌는데 안 따라옴 ({turn.Action})");
        Check(toPlayer(turn.Target) == 1, $"따라왔는데 아직 두 칸 {turn.Target}");

        // 5) 곁에 있으면 가만히 있는다. 계속 붙으려 들면 통로에서 서로 밀린다.
        var beside = new Vector3Int(11, 11, 0);
        Check(toPlayer(beside) <= PartnerPlan.FollowDistance, "준비 실패: 곁이 아님");
        turn = PartnerPlan.Decide(
            beside, player, new List<Vector3Int>(), openField, toPlayer);
        Check(turn.Action == PartnerAction.Stay, $"곁인데 움직임 ({turn.Action})");

        // 6) 갈 곳이 다 막혀 있으면 제자리. 여기서 없는 칸을 고르면 그대로
        //    "논리는 갈 수 있다는데 물리가 막는" 그 버그가 된다.
        turn = PartnerPlan.Decide(
            behind, player, new List<Vector3Int>(), _ => false, toPlayer);
        Check(turn.Action == PartnerAction.Stay, $"막혔는데 움직임 ({turn.Action})");

        // 7) 고른 칸은 반드시 들어갈 수 있는 이웃 칸이어야 한다.
        //    한 칸 건너뛰면 순간이동이 된다.
        var occupied = new HashSet<Vector3Int> { new Vector3Int(11, 10, 0) };
        Func<Vector3Int, bool> blocked = cell => !occupied.Contains(cell);

        turn = PartnerPlan.Decide(
            behind, player, new List<Vector3Int>(), blocked, toPlayer);
        Check(turn.Action != PartnerAction.Move || !occupied.Contains(turn.Target),
              "다른 생물이 선 칸을 고름");
        Check(turn.Action != PartnerAction.Move
              || PartnerPlan.ChebyshevDistance(behind, turn.Target) == 1,
              $"한 걸음이 아님 {turn.Target}");

        // 8) 거리 세는 법. 대각선이 한 걸음이라 이 게임의 이동과 맞아야 한다.
        Check(PartnerPlan.ChebyshevDistance(new Vector3Int(0, 0, 0), new Vector3Int(1, 1, 0)) == 1,
              "대각선이 한 걸음이 아님");
        Check(PartnerPlan.IsAdjacent(new Vector3Int(0, 0, 0), new Vector3Int(1, 1, 0)),
              "대각선이 붙은 것이 아님");
        Check(!PartnerPlan.IsAdjacent(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 0)),
              "같은 칸을 붙었다고 함");

        // ── 아직 남일 때 ──────────────────────────────────────────────────
        //
        // 플레이어를 쫓지도 적을 치지도 않고 서성인다. 그리고 두 턴에 한 걸음이라
        // (무거움) 플레이어가 쫓아가서 말을 걸 수 있다.

        // 9) 무거움. 남은 두 턴에 한 번, 동료가 되면 매 턴 움직인다.
        Check(PartnerPlan.ActsThisTurn(0, joined: false), "첫 턴부터 못 움직임");
        Check(!PartnerPlan.ActsThisTurn(1, joined: false), "남인데 매 턴 움직임");
        Check(PartnerPlan.ActsThisTurn(2, joined: false), "두 턴이 지났는데 안 움직임");

        for (int t = 0; t < 6; t++)
            Check(PartnerPlan.ActsThisTurn(t, joined: true), $"동료가 된 뒤 {t}턴에 멈춤");

        // 10) 서성이기. 갈 수 있는 칸 하나를 고르되 한 걸음이어야 한다.
        var wanderer = new Vector3Int(5, 5, 0);
        for (float roll = 0f; roll <= 1f; roll += 0.1f)
        {
            PartnerTurn step = PartnerPlan.DecideNeutral(wanderer, openField, roll);

            Check(step.Action == PartnerAction.Move, $"서성이지 않음 (roll {roll})");
            Check(PartnerPlan.ChebyshevDistance(wanderer, step.Target) == 1,
                  $"한 걸음이 아님 {step.Target}");
        }

        // 굴린 값이 여덟 칸에 고루 퍼진다. 한 곳만 나오면 서성이는 것이 아니다.
        var seenCells = new HashSet<Vector3Int>();
        for (float roll = 0f; roll < 1f; roll += 0.05f)
            seenCells.Add(PartnerPlan.DecideNeutral(wanderer, openField, roll).Target);
        Check(seenCells.Count == 8, $"고르는 칸이 여덟이 아님 ({seenCells.Count})");

        // 11) 왔던 칸으로 곧바로 되돌아가지 않는다. 두 칸을 오가면 고장 난 것처럼 보인다.
        var back = new Vector3Int(4, 5, 0);
        for (float roll = 0f; roll < 1f; roll += 0.05f)
        {
            PartnerTurn step = PartnerPlan.DecideNeutral(wanderer, openField, roll, back);
            Check(step.Target != back, $"왔던 칸으로 되돌아감 (roll {roll})");
        }

        // 그것밖에 없으면 어쩔 수 없이 되돌아간다. 갇혀서 아무것도 못 하면 안 된다.
        Func<Vector3Int, bool> onlyBack = cell => cell == back;
        PartnerTurn forced = PartnerPlan.DecideNeutral(wanderer, onlyBack, 0.5f, back);
        Check(forced.Action == PartnerAction.Move && forced.Target == back,
              "갈 곳이 그것뿐인데 멈춤");

        // 11-1) 말 고르기. 셋 중 하나만 나오고, 특기는 드물다.
        var talkCount = new Dictionary<PartnerTalk, int>
        {
            { PartnerTalk.Hp, 0 }, { PartnerTalk.Action, 0 }, { PartnerTalk.Exp, 0 },
        };

        const int TalkRolls = 9000;
        for (int i = 0; i < TalkRolls; i++)
            talkCount[PartnerPlan.PickTalk((i + 0.5f) / TalkRolls, actionAvailable: true)]++;

        foreach (var pair in talkCount)
            Check(pair.Value > 0, $"{pair.Key} 가 한 번도 안 나옴");

        // 특기는 다른 둘보다 드물어야 한다. 자주 나오면 특별할 것이 없어진다.
        Check(talkCount[PartnerTalk.Action] < talkCount[PartnerTalk.Hp],
              $"특기가 체력 대사보다 잦음 {talkCount[PartnerTalk.Action]}");
        Check(talkCount[PartnerTalk.Action] < talkCount[PartnerTalk.Exp],
              $"특기가 경험치 대사보다 잦음 {talkCount[PartnerTalk.Action]}");

        // 특기를 못 쓸 때는 아예 안 고른다. 고르면 없는 대사가 뜬다.
        for (float roll = 0f; roll <= 1f; roll += 0.02f)
            Check(PartnerPlan.PickTalk(roll, actionAvailable: false) != PartnerTalk.Action,
                  $"못 쓰는데 특기를 고름 (roll {roll})");

        // 11-2) 놓치면 탐색으로 돌아간다.
        //
        //       예전에는 걸음 수 지도만 보고 어디에 있든 정확히 찾아왔다.
        //       워프로 맵 반대편에 떨어져도 곧장 오는 것이 개연성이 없었다.

        // 보이면 무조건 따라간다 — 처음 만나는 순간이 여기다.
        Check(PartnerPlan.KeepsFollowing(wasFollowing: false, playerVisible: true, stepsToPlayer: 99),
              "보이는데 안 따라감");

        // 안 보여도 따라가던 중이면 이어 간다. 통로에서는 둘 다 방 밖이라
        // '보인다' 가 false 인데, 그것만으로 끊으면 통로에 들어서자마자 멈춘다.
        Check(PartnerPlan.KeepsFollowing(true, playerVisible: false, stepsToPlayer: 3),
              "통로에서 따라가기가 끊김");
        Check(PartnerPlan.KeepsFollowing(
                  true, false, PartnerPlan.FollowGiveUpDistance),
              "경계 안인데 놓침");

        // 너무 멀어지거나(워프) 갈 수 없게 되면 놓친 것으로 본다.
        Check(!PartnerPlan.KeepsFollowing(
                  true, false, PartnerPlan.FollowGiveUpDistance + 1),
              "너무 멀어졌는데 계속 따라감");
        Check(!PartnerPlan.KeepsFollowing(true, false, -1), "갈 수 없는데 계속 따라감");

        // 놓친 뒤에는 다시 만날 때까지 스스로 찾아가지 않는다.
        Check(!PartnerPlan.KeepsFollowing(false, false, 3), "안 보이는데 저절로 따라붙음");

        // 11-3) 탐색. 목표를 정해 놓고 곧게 걷는다 — 서성이는 것과 다르다.
        var goal = new Vector3Int(20, 5, 0);
        PartnerTurn search = PartnerPlan.DecideSearch(wanderer, goal, openField);

        Check(search.Action == PartnerAction.Move, "탐색인데 안 움직임");
        Check(PartnerPlan.ChebyshevDistance(search.Target, goal)
              < PartnerPlan.ChebyshevDistance(wanderer, goal), "목표에서 멀어짐");
        Check(PartnerPlan.ChebyshevDistance(wanderer, search.Target) == 1, "한 걸음이 아님");

        // 닿았으면 멈춘다 — 부르는 쪽이 그걸 보고 새 목표를 고른다.
        Check(PartnerPlan.DecideSearch(goal, goal, openField).Action == PartnerAction.Stay,
              "이미 닿았는데 움직임");
        Check(PartnerPlan.DecideSearch(wanderer, goal, _ => false).Action == PartnerAction.Stay,
              "막혔는데 움직임");

        // 11-4) 소리. 싸움·던지기가 난 자리를 한동안 기억한다.
        var noise = new NoiseBoard();
        Check(!noise.TryGetLatest(0, out _), "아무 일도 없었는데 소리가 들림");

        var spot = new Vector3Int(7, 9, 0);
        noise.Report(spot, 100);

        Check(noise.TryGetLatest(100, out Vector3Int heard) && heard == spot,
              "난 소리를 못 들음");
        Check(noise.TryGetLatest(100 + NoiseBoard.FadeTurns, out _), "아직인데 사라짐");
        Check(!noise.TryGetLatest(100 + NoiseBoard.FadeTurns + 1, out _),
              "오래된 소리가 안 사라짐");

        // 나중 것이 앞의 것을 덮는다. 목록으로 쌓으면 "어느 것부터" 를 또 정해야 한다.
        var later = new Vector3Int(1, 1, 0);
        noise.Report(later, 105);
        Check(noise.TryGetLatest(105, out Vector3Int last) && last == later,
              "나중 소리가 앞의 것을 안 덮음");

        // 새 층. 지난 층의 소리를 따라가면 갈 수 없는 자리를 향해 걷는다.
        noise.Clear();
        Check(!noise.TryGetLatest(105, out _), "새 층인데 지난 소리가 남음");

        // 11-5) 방향 사다리. 적과 동료가 함께 쓰는 것이고,
        //       앞으로 도망·거리 벌리기·아이템 다가가기도 여기서 나온다.
        var from = new Vector3Int(5, 5, 0);
        var to = new Vector3Int(8, 5, 0);

        var ladder = new List<Vector2>(MoveIntent.Toward(from, to));
        Check(ladder.Count > 1, $"물러설 방향이 없음 ({ladder.Count})");
        Check(ladder[0] == Vector2.right, $"첫 방향이 목표 쪽이 아님 {ladder[0]}");

        // 한 칸만 보고 고르면 벽 앞에서 갈 곳이 없어진다. 물러설 길이 있어야 한다.
        Check(ladder.Contains(Vector2.up) && ladder.Contains(Vector2.down),
              "축 방향인데 옆으로 비켜 볼 길이 없음");
        Check(ladder[ladder.Count - 1] == Vector2.left, "막다른 길에서 뒤로 물러설 길이 없음");

        // 대각선은 가로·세로로 나눠 시도한다.
        var diagonal = new List<Vector2>(
            MoveIntent.Toward(from, new Vector3Int(8, 8, 0)));
        Check(diagonal[0] == new Vector2(1, 1), "대각선이 첫 방향이 아님");
        Check(diagonal.Contains(new Vector2(1, 0)) && diagonal.Contains(new Vector2(0, 1)),
              "대각선이 막혔을 때 나눠 시도하지 않음");

        // 같은 칸이면 갈 곳이 없다.
        Check(new List<Vector2>(MoveIntent.Toward(from, from)).Count == 0, "제자리인데 방향이 나옴");

        // 도망은 반대쪽부터. 등 뒤가 막혔을 때 옆으로 샐 길도 함께 나온다.
        var flee = new List<Vector2>(MoveIntent.AwayFrom(from, to));
        Check(flee[0] == Vector2.left, $"도망인데 반대쪽이 아님 {flee[0]}");
        Check(flee.Count > 1, "도망칠 다른 길이 없음");

        // 거리 벌리기 — 가까우면 물러나고 멀면 다가가고 딱 맞으면 가만히.
        var near = new Vector3Int(6, 5, 0);
        var wayOff = new Vector3Int(15, 5, 0);
        Check(new List<Vector2>(MoveIntent.KeepDistance(from, near, 3))[0] == Vector2.left,
              "너무 가까운데 안 물러남");
        Check(new List<Vector2>(MoveIntent.KeepDistance(from, wayOff, 3))[0] == Vector2.right,
              "너무 먼데 안 다가감");
        Check(new List<Vector2>(MoveIntent.KeepDistance(from, to, 3)).Count == 0,
              "거리가 딱 맞는데 움직이려 함");

        // 11-6) 순찰 목표 — <b>지금 방의 문</b>만 고른다.
        //       층 전체에서 고르면 벽 너머가 목표가 되어 영영 닿지 못한다.
        var rooms = new List<Rect> { new Rect(0, 0, 10, 10) };
        var exits = new List<Vector3>
        {
            new Vector3(5, 10, 0),      // 이 방의 문(벽 위)
            new Vector3(50, 50, 0),     // 저 멀리 다른 방의 문
        };
        var seen = new List<Vector2>();

        Check(PatrolPlan.TryPickTarget(
                  new Vector2(5, 5), rooms, exits, seen, 0.5f, out Vector3 door),
              "방 안인데 목표를 못 고름");
        Check(door.x == 5 && door.y == 10, $"다른 방의 문을 고름 {door}");

        // 통로 한가운데면 고를 수 없다. 방에 들어설 때까지 그냥 나아간다.
        Check(!PatrolPlan.TryPickTarget(
                  new Vector2(30, 30), rooms, exits, seen, 0.5f, out _),
              "통로인데 목표를 고름");

        // 다녀온 문은 뺀다 — 그러지 않으면 두 문 사이를 오간다.
        // 이 방의 문이 하나뿐이라 후보가 비고, 그때는 기억을 지우고 다시 돈다.
        var oneDoor = new List<Vector2> { new Vector2(5, 10) };
        Check(PatrolPlan.TryPickTarget(
                  new Vector2(5, 5), rooms, exits, oneDoor, 0.5f, out _),
              "다 돌았는데 다시 돌지 못함");
        Check(oneDoor.Count == 0, "후보가 없는데 기억을 안 지움");

        Check(!PatrolPlan.TryPickTarget(
                  new Vector2(5, 5), rooms, new List<Vector3>(), seen, 0.5f, out _),
              "문이 없는데 목표를 고름");

        // 12) 사방이 막혔으면 제자리.
        Check(PartnerPlan.DecideNeutral(wanderer, _ => false, 0.5f).Action == PartnerAction.Stay,
              "막혔는데 움직임");

        Console.WriteLine(
            "  붙은 적 · 협공 · 달려들기 · 따라오기 · 곁에서 멈춤 · 막힘 · 한 걸음 · 무거움 · 서성이기 검사 완료");
    }

    class AsciiGrid : IWalkableGrid
    {
        private readonly string[] rows;   // rows[0] 이 맨 위
        private readonly int width, height;

        public AsciiGrid(string[] picture)
        {
            rows = picture;
            height = picture.Length;
            width = picture[0].Length;
        }

        public BoundsInt Bounds => new BoundsInt(0, 0, 0, width, height, 1);

        private char At(Vector3Int c)
        {
            if (c.x < 0 || c.x >= width || c.y < 0 || c.y >= height) return '#';
            return rows[height - 1 - c.y][c.x];      // y 는 아래에서 위로
        }

        public bool IsWalkable(Vector3Int cell) => At(cell) != '#';
        public bool IsWall(Vector3Int cell) => At(cell) == '#';
        public Vector3Int WorldToCell(Vector2 w) => new Vector3Int((int)w.x, (int)w.y, 0);

        public Vector3Int Find(char marker)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (rows[height - 1 - y][x] == marker) return new Vector3Int(x, y, 0);
            return new Vector3Int(-1, -1, 0);
        }
    }

    /// <summary>적이 지도를 따라 실제로 플레이어에게 도달하는지 걸어 본다.</summary>
    static int WalkToPlayer(AsciiGrid grid, Vector3Int enemy, Vector3Int player, int maxTurns = 300)
    {
        var map = new ChaseMap();
        var at = new Vector2(enemy.x, enemy.y);
        var goal = new Vector2(player.x, player.y);

        for (int turn = 0; turn < maxTurns; turn++)
        {
            if (Vector2.Distance(at, goal) < 1.5f) return turn;   // 옆에 붙었다

            map.Rebuild(grid, goal);

            bool moved = false;
            foreach (Vector2 step in map.StepsToward(at))
            {
                at += step;
                moved = true;
                break;                                           // 가장 가까워지는 한 걸음
            }
            if (!moved) return -1;                               // 갈 곳이 없다
        }
        return -1;
    }

    static void TestChaseMap()
    {
        Console.WriteLine("[추격 길찾기]");

        // 1) 벽이 정면을 막고 있고, 통로는 위쪽으로 크게 돌아가야 한다.
        //    예전 탐욕적 로직이 바로 여기서 벽만 밀고 멈춰 있었다.
        var detour = new AsciiGrid(new[]
        {
            "###########",
            "#.........#",
            "#.#######.#",
            "#.#     #.#",
            "#E#######P#",
            "###########",
        });
        var e = detour.Find('E');
        var p = detour.Find('P');
        int turns = WalkToPlayer(detour, e, p);
        Check(turns > 0, "우회가 필요한 지형에서 적이 플레이어에게 닿지 못함");
        Check(turns >= 8, $"우회로가 비정상적으로 짧음 ({turns}턴) — 벽을 통과했을 수 있음");

        // 2) 벽으로 완전히 갈라진 경우: 닿을 수 없다고 정확히 보고해야 한다.
        var sealedOff = new AsciiGrid(new[]
        {
            "#########",
            "#E##...P#",
            "#########",
        });
        var map = new ChaseMap();
        var pp = sealedOff.Find('P');
        map.Rebuild(sealedOff, new Vector2(pp.x, pp.y));
        var ee = sealedOff.Find('E');
        Check(!map.TryGetDistance(new Vector2(ee.x, ee.y), out _),
              "막힌 벽 너머인데 닿을 수 있다고 보고함");

        // 3) 대각선으로 벽 틈을 스쳐 지나가지 못해야 한다(실제 이동 규칙과 동일).
        var pinch = new AsciiGrid(new[]
        {
            "#####",
            "#.#P#",
            "##.##",
            "#E#.#",
            "#####",
        });
        map.Rebuild(pinch, new Vector2(pinch.Find('P').x, pinch.Find('P').y));
        var pe = pinch.Find('E');
        foreach (Vector2 step in map.StepsToward(new Vector2(pe.x, pe.y)))
        {
            bool diagonal = step.x != 0 && step.y != 0;
            if (!diagonal) continue;
            bool sideA = pinch.IsWall(new Vector3Int(pe.x + (int)step.x, pe.y, 0));
            bool sideB = pinch.IsWall(new Vector3Int(pe.x, pe.y + (int)step.y, 0));
            Check(!(sideA && sideB), "벽 사이 틈을 대각선으로 빠져나가려 함");
        }

        // 4) 뻥 뚫린 방: 거리 지도가 실제 최단 걸음 수(체비쇼프)와 맞아야 한다.
        var open = new AsciiGrid(new[]
        {
            "###########",
            "#.........#",
            "#.........#",
            "#.........#",
            "#.........#",
            "###########",
        });
        map.Rebuild(open, new Vector2(1, 1));
        map.TryGetDistance(new Vector2(9, 4), out int d);
        Check(d == 8, $"열린 공간의 걸음 수가 틀림: {d} (기대 8)");

        // 5) 방을 가로질러 늘 도달해야 한다
        int reached = 0, tries = 0;
        var rnd = new System.Random(9);
        for (int t = 0; t < 200; t++)
        {
            var a = new Vector3Int(rnd.Next(1, 10), rnd.Next(1, 5), 0);
            var b = new Vector3Int(rnd.Next(1, 10), rnd.Next(1, 5), 0);
            if (a == b) continue;
            tries++;
            if (WalkToPlayer(open, a, b) >= 0) reached++;
        }
        Check(reached == tries, $"열린 방에서 도달 실패 {tries - reached}건");

        Console.WriteLine($"  우회 {turns}턴 만에 도달 · 도달률 {reached}/{tries}");
    }

    // ── 원거리 판정 ────────────────────────────────────────────────────────
    static void TestRangedShot()
    {
        Console.WriteLine("[원거리 판정]");

        // 1) 핵심 요구사항: 상하좌우가 전부 벽이고 대각선만 트인 자리.
        //    근접은 어느 쪽으로도 못 때리지만, 투사체는 대각선 틈으로 날아가야 한다.
        var boxed = new AsciiGrid(new[]
        {
            "#####",
            "#.#.#",     // 대각선(왼쪽 위 / 오른쪽 위)은 트여 있음
            "##E##",     // E 의 상하좌우는 전부 벽
            "#T#.#",     // 왼쪽 아래 대각선 너머에 표적 T
            "#####",
        });
        var shooter = boxed.Find('E');
        var mark = boxed.Find('T');

        // 상하좌우가 정말 전부 벽인지 먼저 확인 (전제 검증)
        Check(boxed.IsWall(new Vector3Int(shooter.x + 1, shooter.y, 0)), "전제 오류: 오른쪽이 벽이 아님");
        Check(boxed.IsWall(new Vector3Int(shooter.x - 1, shooter.y, 0)), "전제 오류: 왼쪽이 벽이 아님");
        Check(boxed.IsWall(new Vector3Int(shooter.x, shooter.y + 1, 0)), "전제 오류: 위가 벽이 아님");
        Check(boxed.IsWall(new Vector3Int(shooter.x, shooter.y - 1, 0)), "전제 오류: 아래가 벽이 아님");

        // 이동 규칙이라면 이 대각선은 막혀야 한다(양옆이 벽이므로)
        bool movementWouldBeBlocked =
            boxed.IsWall(new Vector3Int(shooter.x - 1, shooter.y, 0)) &&
            boxed.IsWall(new Vector3Int(shooter.x, shooter.y - 1, 0));
        Check(movementWouldBeBlocked, "전제 오류: 근접이 막히는 상황이 아님");

        // 그런데 투사체는 통과해서 표적에 맞아야 한다
        var result = RangedShot.Trace(
            boxed, shooter, new Vector2Int(-1, -1), 5,
            new List<Vector3Int> { mark });
        Check(result.HitTarget, "벽 사이 대각선 틈으로 투사체가 날아가지 못함");
        Check(result.ImpactCell == mark, $"맞은 칸이 다름 {result.ImpactCell} vs {mark}");

        // 2) 벽에 막히면 벽 '바로 앞' 칸에 떨어져야 한다
        var corridor = new AsciiGrid(new[]
        {
            "#######",
            "#E...##",
            "#######",
        });
        var from = corridor.Find('E');
        var wallShot = RangedShot.Trace(corridor, from, new Vector2Int(1, 0), 10, new List<Vector3Int>());
        Check(wallShot.StoppedByWall, "벽에 막혔는데 그렇게 보고하지 않음");
        Check(!wallShot.HitTarget, "맞을 대상이 없는데 맞았다고 함");
        Check(wallShot.ImpactCell == new Vector3Int(4, 1, 0),
              $"벽 앞 칸이 아님 {wallShot.ImpactCell} (기대 (4,1))");

        // 3) 가장 가까운 대상에 먼저 맞아야 한다(뒤의 적을 관통하지 않음)
        var line = new AsciiGrid(new[]
        {
            "########",
            "#E.....#",
            "########",
        });
        var shooter3 = line.Find('E');
        var near = new Vector3Int(3, 1, 0);
        var far = new Vector3Int(5, 1, 0);
        var pierce = RangedShot.Trace(line, shooter3, new Vector2Int(1, 0), 10,
                                      new List<Vector3Int> { far, near });   // 일부러 먼 것을 먼저 넣음
        Check(pierce.HitTarget, "일직선상의 대상을 못 맞힘");
        Check(pierce.ImpactCell == near, $"가까운 대상이 아니라 {pierce.ImpactCell} 에 맞음");

        // 4) 사거리를 넘어가면 못 맞힌다
        var shortRange = RangedShot.Trace(line, shooter3, new Vector2Int(1, 0), 1,
                                          new List<Vector3Int> { near });
        Check(!shortRange.HitTarget, "사거리 밖인데 맞았다고 함");
        Check(shortRange.TravelledCells == 1, $"날아간 칸 수가 틀림 {shortRange.TravelledCells}");

        Console.WriteLine("  대각선 틈 통과 · 벽 앞 착지 · 최근접 명중 검사 완료");
    }

    // ── 모든 방이 이어지는가 + 루프가 요청한 만큼 얹히는가 ─────────────────
    static void TestRoomGraph()
    {
        Console.WriteLine("[방 연결 그래프]");
        var rnd = new System.Random(3);

        for (int t = 0; t < 2000; t++)
        {
            int n = rnd.Next(2, 19);
            var layout = new DungeonLayout();
            for (int i = 0; i < n; i++)
                layout.Rooms.Add(new RoomInfo(rnd.Next(1, 40), rnd.Next(1, 40), 6, 6));

            int extra = rnd.Next(0, 6);
            var edges = RoomGraph.Plan(layout, extra);

            // 신장 트리(n-1) + 요청한 루프 수 만큼 나와야 한다
            int maxPossibleLoops = n * (n - 1) / 2 - (n - 1);
            Check(edges.Count == (n - 1) + Math.Min(extra, maxPossibleLoops),
                  $"간선 수가 다름 n={n} extra={extra} -> {edges.Count}");

            // 실제로 모든 방이 하나로 이어지는지 직접 확인
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++) adjacency[i] = new List<int>();
            foreach (var e in edges) { adjacency[e.A].Add(e.B); adjacency[e.B].Add(e.A); }

            var seen = new HashSet<int> { 0 };
            var stack = new Stack<int>();
            stack.Push(0);
            while (stack.Count > 0)
                foreach (int next in adjacency[stack.Pop()])
                    if (seen.Add(next)) stack.Push(next);

            Check(seen.Count == n, $"모든 방이 이어지지 않음 n={n}, 도달 {seen.Count}");
        }
        Console.WriteLine("  2000 그래프 검사 완료");
    }

    // ── 새 판을 시작할 때 지난 판의 상태가 넘어오지 않는가 ──────────────────
    //
    // 판 단위 상태는 층을 넘어가야 해서 static 으로 들고 있는데, static 은 씬을
    // 다시 불러도 초기화되지 않는다. 에디터는 Play 마다 도메인을 다시 로드해
    // 저절로 새 값이 되므로 이 문제가 아예 드러나지 않고, 빌드에서만 죽은
    // 캐릭터의 수치를 물려받는다. 그래서 에디터가 아닌 이곳에서 확인한다.
    static void TestResetRun()
    {
        Console.WriteLine("[새 판 초기화]");

        // 한 판을 진행하다 죽은 상태를 흉내 낸다.
        var before = GameManager.Stats;
        before.TakeDamage(7);
        before.AddGold(1234);
        before.AddExp(100);          // 레벨업까지 일어나 최대 체력도 올라간다
        before.ConsumeHunger(30);
        GameManager.LookDirection = Vector2.right;
        GameManager.FastForward = true;
        GameManager.InteractionHandled = true;
        GameManager.Log.Add("지난 판에서 지나간 메시지");
        GameManager.Marks.Set(20001, MarkShape.Circle, MarkColor.Red);
        InputFocus.Give(InputOwner.BagMenu);

        // 흉내가 실제로 상태를 더럽혔는지부터 확인한다.
        // 이게 참이 아니면 아래 검사는 전부 공짜로 통과해 버린다.
        Check(before.Hp < before.MaxHp, "준비 실패: 체력이 깎이지 않음");
        Check(before.Level > 1, "준비 실패: 레벨이 오르지 않음");
        Check(before.Gold > 0, "준비 실패: 소지금이 늘지 않음");
        Check(GameManager.Log.Count > 0, "준비 실패: 메시지 이력이 쌓이지 않음");
        Check(!GameManager.Marks.IsEmpty, "준비 실패: 표식이 적히지 않음");

        GameManager.ResetRun();

        Check(!ReferenceEquals(GameManager.Stats, before), "PlayerStats 를 새로 만들지 않고 재사용함");
        Check(GameManager.Stats.Hp == PlayerStats.InitialMaxHp,
              $"체력이 초기값이 아님 {GameManager.Stats.Hp}");
        Check(GameManager.Stats.MaxHp == PlayerStats.InitialMaxHp,
              $"최대 체력이 지난 판 값을 물려받음 {GameManager.Stats.MaxHp}");
        Check(GameManager.Stats.Level == 1, $"레벨이 남아 있음 {GameManager.Stats.Level}");
        Check(GameManager.Stats.Gold == 0, $"소지금이 남아 있음 {GameManager.Stats.Gold}");
        Check(GameManager.Stats.Hunger == PlayerStats.InitialHunger,
              $"포만도가 남아 있음 {GameManager.Stats.Hunger}");
        Check(GameManager.Bag.IsEmpty, "가방이 비워지지 않음");
        Check(GameManager.Gear == null, "장비가 비워지지 않음");
        Check(GameManager.Stage == 0, $"층이 0으로 돌아가지 않음 {GameManager.Stage}");
        Check(GameManager.TurnCount == 0, $"턴 수가 남아 있음 {GameManager.TurnCount}");
        Check(GameManager.LookDirection == Vector2.down, "바라보는 방향이 초기화되지 않음");
        Check(!GameManager.FastForward, "빨리감기가 켜진 채로 남음");
        Check(!GameManager.InteractionHandled, "밟은 것 처리 표시가 남음");
        Check(GameManager.Log.Count == 0, $"지난 판의 메시지 이력이 남음 {GameManager.Log.Count}줄");
        Check(GameManager.Marks.IsEmpty, "지난 판의 표식이 남음");
        Check(InputFocus.PlayerHasFocus, "입력 권한이 플레이어에게 돌아오지 않음");

        Console.WriteLine("  죽은 판의 수치 · 가방 · 장비 · 이력 · 표식 · 입력 권한 초기화 확인");
    }

    // ── 창이 내용에 맞춰 커지되 HUD 를 가리지 않는가 ────────────────────────
    //
    // 창 크기가 내용에 따라 달라지기 시작하면 "적당한 자리에 놓아 두기" 가 통하지
    // 않는다. 긴 설명 하나가 들어오는 순간 상자가 위로 자라 HUD 를 덮기 때문이다.
    // 눈으로는 짧은 글로 확인하고 넘어가기 쉬우므로 여기서 무작위로 밀어붙인다.
    static void TestPanelLayout()
    {
        Console.WriteLine("[창 크기 · HUD 침범]");

        // 16:9 (캔버스 기준 해상도 800 폭, match=width) 와 4:3 을 함께 본다.
        var canvases = new[] { new Vector2(800f, 450f), new Vector2(800f, 600f) };
        var padding = new Vector2(44f, 28f);

        // 1. 작은 내용은 내용 + 여백 그대로여야 한다(공연히 커지지 않는다).
        var small = PanelLayout.Fit(new Vector2(100f, 60f), padding, canvases[0], new Vector2(0f, 0f));
        Check(Math.Abs(small.Size.x - 144f) < 0.01f, $"작은 창 가로가 내용+여백이 아님 {small.Size.x}");
        Check(Math.Abs(small.Size.y - 88f) < 0.01f, $"작은 창 세로가 내용+여백이 아님 {small.Size.y}");
        Check(small.Center == Vector2.zero, $"들어갈 자리가 있는데 창이 밀렸음 {small.Center}");

        // 2. HUD 한가운데에 놓아 달라고 해도 HUD 아래로 밀려나야 한다.
        var pushed = PanelLayout.Fit(new Vector2(200f, 100f), padding, canvases[0], new Vector2(0f, 210f));
        Check(!PanelLayout.CoversHud(pushed), $"HUD 자리를 요청했는데 그대로 놓임 (윗변 {pushed.Center.y + pushed.Size.y / 2})");

        // 3. 화면보다 큰 내용을 넣어도 넘치지 않는다.
        foreach (var canvas in canvases)
        {
            var huge = PanelLayout.Fit(new Vector2(5000f, 5000f), padding, canvas, Vector2.zero);
            Check(!PanelLayout.CoversHud(huge), $"큰 내용이 HUD 를 덮음 (캔버스 {canvas.y})");
            Check(huge.Size.x <= canvas.x, $"창이 화면보다 넓음 {huge.Size.x} > {canvas.x}");
            Check(huge.Center.y - huge.Size.y / 2 >= -canvas.y / 2 - 0.01f, "창 아랫변이 화면 밖으로 나감");
        }

        // 4. 세로를 HudBottom 으로 주면 '윗변 정렬' 이 되어야 한다.
        //    상태창 옆 목록이 항목 수와 상관없이 상태창과 나란히 서는 근거다.
        foreach (var canvas in canvases)
        {
            float expectedTop = Math.Min(PanelLayout.HudBottom, canvas.y / 2f - PanelLayout.ScreenMargin);
            for (int lines = 1; lines <= 12; lines++)
            {
                var p = PanelLayout.Fit(new Vector2(120f, lines * 34f), padding, canvas,
                                        new Vector2(250f, PanelLayout.HudBottom));
                float top = p.Center.y + p.Size.y / 2f;
                Check(Math.Abs(top - expectedTop) < 0.01f,
                      $"{lines}줄일 때 윗변이 어긋남: {top} (기대 {expectedTop})");
            }
        }

        // 5. 무작위로 밀어붙인다. 내용 크기와 놓고 싶은 자리를 마구 바꿔 본다.
        var rnd = new System.Random(7);
        int cases = 0;
        foreach (var canvas in canvases)
        {
            for (int t = 0; t < 20000; t++)
            {
                var content = new Vector2((float)rnd.NextDouble() * 1600f, (float)rnd.NextDouble() * 1200f);
                var want = new Vector2((float)(rnd.NextDouble() * 1200 - 600), (float)(rnd.NextDouble() * 900 - 450));

                var p = PanelLayout.Fit(content, padding, canvas, want);
                cases++;

                Check(!PanelLayout.CoversHud(p),
                      $"HUD 침범: 내용{content} 요청{want} -> 중심{p.Center} 크기{p.Size}");

                // 화면 밖으로 나가지 않는가
                Check(p.Center.x - p.Size.x / 2 >= -canvas.x / 2 - 0.01f, $"왼쪽으로 넘침 {p.Center} {p.Size}");
                Check(p.Center.x + p.Size.x / 2 <= canvas.x / 2 + 0.01f, $"오른쪽으로 넘침 {p.Center} {p.Size}");
                Check(p.Center.y - p.Size.y / 2 >= -canvas.y / 2 - 0.01f, $"아래로 넘침 {p.Center} {p.Size}");

                // 크기가 음수이거나 NaN 이면 RectTransform 이 조용히 망가진다
                Check(p.Size.x >= 0f && p.Size.y >= 0f, $"크기가 음수 {p.Size}");
                Check(!float.IsNaN(p.Center.x) && !float.IsNaN(p.Center.y), "중심이 NaN");
            }
        }

        Console.WriteLine($"  {cases} 배치 검사 완료");
    }
}
