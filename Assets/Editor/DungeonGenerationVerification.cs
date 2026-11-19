using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>배치 모드 전용. 씬을 저장하지 않고 실제 타일·프리팹으로 생성 회귀를 검사한다.</summary>
public static class DungeonGenerationVerification
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("배치 모드에서만 실행하세요.");
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var generator = UnityEngine.Object.FindObjectOfType<RandomDung>();
            int fallbacks = 0, retries = 0;
            var styles = new HashSet<int>();
            var hashes = new HashSet<int>();
            foreach (int stage in new[] { 1, 5, 20, 100 })
            for (int seed = 0; seed < 20; seed++)
            {
                generator.GenerateTerrain(stage, seed);
                CheckReachability(generator);
                if (generator.UsedFallback) fallbacks++;
                retries += generator.GenerationAttempts - 1;
                styles.Add(generator.GenerationLoops);
                int hash = Fingerprint(generator);
                hashes.Add(hash);
                var state = UnityEngine.Random.state;
                float expected = UnityEngine.Random.value;
                UnityEngine.Random.state = state;
                generator.GenerateTerrain(stage, seed);
                Require(Fingerprint(generator) == hash, "같은 시드의 지형이 달라짐");
                Require(UnityEngine.Random.value == expected, "생성이 전투용 난수 흐름을 바꿈");
            }
            Require(styles.Contains(1) && styles.Contains(3), "두 구조 유형이 나오지 않음");
            Require(hashes.Count > 60, "서로 다른 시드의 지형 다양성이 부족함");

            // 방 배치가 전부 실패하는 설정으로 재시도 상한·대체 방 경로를 실제로 밟는다.
            int min = generator.minRoomSize, max = generator.maxRoomSize;
            generator.minRoomSize = generator.maxRoomSize = 1;
            generator.GenerateTerrain(1, 123);
            Require(generator.UsedFallback && generator.GenerationAttempts == 4, "대체 방 경로 실패");
            CheckReachability(generator);
            generator.minRoomSize = min;
            generator.maxRoomSize = max;

            for (int seed = 0; seed < 12; seed++)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                generator = UnityEngine.Object.FindObjectOfType<RandomDung>();
                GameManager.instance = UnityEngine.Object.FindObjectOfType<GameManager>();
                GameManager.ResetRun();
                typeof(GameManager).GetProperty("Stage").SetValue(null, seed % 2 == 0 ? 1 : 100);
                generator.useFixedSeed = true;
                generator.fixedSeed = seed;
                generator.GenerateDungeon();
                CheckReachability(generator);
                var cells = new HashSet<Vector3Int> { generator.StartCell };
                foreach (var creature in UnityEngine.Object.FindObjectsOfType<MovingObject>())
                {
                    var cell = generator.floorTilemap.WorldToCell(creature.transform.position);
                    Require(TileRules.IsWalkableCell(generator.floorTilemap, cell), "캐릭터가 통행 불가 칸에 배치됨");
                    if (creature is Player) continue;
                    Require(cells.Add(cell), "캐릭터 중복 배치");
                    Require(cell != generator.StairsCell, "캐릭터가 계단에 배치됨");
                    if (creature is Enemy enemy && enemy.IsCombatTarget) Require(!NearStart(generator, cell), "시작 주변 적 배치");
                }
                foreach (var trap in UnityEngine.Object.FindObjectsOfType<Trap>())
                    Require(!NearStart(generator, generator.floorTilemap.WorldToCell(trap.transform.position)), "시작 주변 함정 배치");
                Require(generator.GetStairsPosition() == generator.StairsCell, "계단 배치가 계획과 다름");
            }
            Debug.Log($"DUNGEON VERIFY PASS: 80 maps x 2, 12 populations, forced fallback; retries={retries}, fallbacks={fallbacks}");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static bool NearStart(RandomDung generator, Vector3Int cell)
        => Mathf.Abs(cell.x - generator.StartCell.x) <= 1 && Mathf.Abs(cell.y - generator.StartCell.y) <= 1;

    private static void CheckReachability(RandomDung generator)
    {
        var distances = DungeonRoutes.Distances(generator.mapWidth, generator.mapHeight,
            cell => TileRules.IsWalkableCell(generator.floorTilemap, cell), generator.StartCell);
        Require(distances.ContainsKey(generator.StairsCell), "계단 도달 불가");
        var layout = (DungeonLayout)typeof(RandomDung).GetField("layout",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(generator);
        foreach (var room in layout.Rooms)
        foreach (var door in room.Doors)
        {
            var direction = door.Outside - door.Position;
            var side = new Vector3Int(-direction.y, direction.x, 0);
            foreach (var cell in new[] { door.Position + side, door.Position - side, door.Outside + side, door.Outside - side })
                Require(!TileRules.IsWalkableCell(generator.floorTilemap, cell), "최종 타일맵에서 입구 폭 확장");
        }
        foreach (var room in generator.RoomCells)
        foreach (var cell in room.allPositionsWithin)
            Require(distances.ContainsKey(new Vector3Int(cell.x, cell.y, 0)), "방 도달 불가");
    }

    private static int Fingerprint(RandomDung generator)
    {
        int hash = 17;
        for (int x = 0; x < generator.mapWidth; x++)
        for (int y = 0; y < generator.mapHeight; y++)
        {
            var tile = generator.floorTilemap.GetTile(new Vector3Int(x, y, 0));
            hash = unchecked(hash * 31 + (tile == null ? 0 : tile.GetInstanceID()));
        }
        return unchecked((hash * 31 + generator.StartCell.GetHashCode()) * 31 + generator.StairsCell.GetHashCode());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
