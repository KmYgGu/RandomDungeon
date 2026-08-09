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
        TestReadingTime();

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
            new ConditionEntity { id = "poison", name = "독",
                explanation = "독에 의한 데미지를 입고 있어",
                type = "해로움", effect = "턴당피해", value = 1, turn = 10 },

            // 엑셀에서 붙여 넣다 남은 탭·공백. id 가 미묘하게 달라지면 코드가 못 찾는다.
            new ConditionEntity { id = "critup\t ", name = "치명타상승",
                explanation = "평소보다 치명타가 잘 나오겠어",
                type = "이로움", effect = "없음", value = 0, turn = 20 },

            new ConditionEntity { id = "hungry", name = "배고픔",
                explanation = "배고픔을 느끼고 있어",
                type = "해로움", effect = "없음", value = 0, turn = 0 },

            // 효과 이름을 잘못 적은 줄. 버리지 말고 '없음' 으로 두고 알려 준다.
            new ConditionEntity { id = "typo", name = "오타",
                explanation = "무언가 이상하다", type = "해로움",
                effect = "턴당대미지", value = 0, turn = 0 },

            new ConditionEntity { id = "   ", name = "빈 줄", explanation = "" },
        };

        var problems = new List<string>();
        var defs = ConditionSheet.FromEntities(rows, problems);

        // 1) id 가 빈 줄만 버리고 나머지는 살린다.
        Check(defs.Count == 4, $"옮겨 담은 줄 수가 다름 {defs.Count} (기대 4)");

        // 2) 칸 끝의 탭·공백을 털어 낸다.
        Check(defs[1].Id == "critup", $"id 의 탭이 안 지워짐: '{defs[1].Id}'");

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
        Check(ConditionReport.ClampOffset(-5, 3, 2) == 0, "위로 넘겨서 음수가 됨");
        Check(ConditionReport.ClampOffset(99, 3, 2) == 1, "아래로 넘겨서 빈 화면이 됨");
        Check(ConditionReport.ClampOffset(1, 3, 5) == 0, "다 들어가는데 스크롤이 됨");
        Check(ConditionReport.ClampOffset(0, 0, 5) == 0, "빈 목록에서 스크롤이 생김");

        // 어떤 줄 수에서도 마지막 항목까지 볼 수 있어야 한다.
        var many = new List<ConditionDefinition>();
        for (int i = 0; i < 12; i++)
            many.Add(new ConditionDefinition { Id = "c" + i, Description = "상태 " + i });

        for (int capacity = 1; capacity <= 6; capacity++)
        {
            int last = ConditionReport.ClampOffset(int.MaxValue, many.Count, capacity);
            string tail = ConditionReport.BuildText(many, last, capacity);
            Check(tail.Contains($"{many.Count}. "),
                  $"capacity={capacity} 일 때 끝까지 내려도 마지막 줄이 안 보임");
            Check(tail.Split('\n').Length == Math.Min(capacity, many.Count),
                  $"capacity={capacity} 일 때 줄 수가 다름");
        }

        Console.WriteLine("  옮겨 담기 · 지저분한 칸 · 이상한 줄 알림 · 표시 순서 · 스크롤 검사 완료");
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
        InputFocus.Give(InputOwner.BagMenu);

        // 흉내가 실제로 상태를 더럽혔는지부터 확인한다.
        // 이게 참이 아니면 아래 검사는 전부 공짜로 통과해 버린다.
        Check(before.Hp < before.MaxHp, "준비 실패: 체력이 깎이지 않음");
        Check(before.Level > 1, "준비 실패: 레벨이 오르지 않음");
        Check(before.Gold > 0, "준비 실패: 소지금이 늘지 않음");

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
        Check(InputFocus.PlayerHasFocus, "입력 권한이 플레이어에게 돌아오지 않음");

        Console.WriteLine("  죽은 판의 수치 · 가방 · 장비 · 입력 권한 초기화 확인");
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
