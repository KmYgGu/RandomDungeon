using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>스프라이트를 샘플링하지 않고 통행 영역의 윤곽을 화면 고정 UI로 그린다.</summary>
public sealed class MinimapOverlay : MaskableGraphic
{
    private List<MinimapContours.Segment> contours = new List<MinimapContours.Segment>();
    private Vector2 viewportOrigin;
    private Vector2 viewportX;
    private Vector2 viewportY;
    private static readonly Color OutlineColor = new Color32(68, 219, 213, 255);

    private Tilemap worldGrid;
    private MinimapExploration exploration;
    private Vector2Int playerCell;
    private bool hasPlayerVision;
    private readonly List<GroundTarget> groundTargets = new List<GroundTarget>();
    private List<Symbol> symbols = new List<Symbol>();
    private List<Symbol> nextSymbols = new List<Symbol>();

    private struct GroundTarget
    {
        public GameObject Object;
        public MinimapSymbols.Kind Kind;
    }

    private struct Symbol
    {
        public MinimapSymbols.Kind Kind;
        public Vector2Int Cell;
    }

    public static void Refresh(Tilemap source, IReadOnlyList<RectInt> rooms, Vector3Int initialCell)
    {
        var camera = Camera.main;
        // 층 전환 중 사라질 새 Canvas 대신 유지되는 실제 UI를 사용한다.
        var game = GameManager.instance;
        var canvasObject = game != null ? game.UiCanvas : null;
        var canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
        if (source == null || camera == null || canvas == null) return;

        var overlay = canvas.GetComponentInChildren<MinimapOverlay>(true);
        if (overlay == null)
        {
            var holder = new GameObject("MinimapOverlay", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(MinimapOverlay));
            holder.transform.SetParent(canvas.transform, false);
            holder.transform.SetAsFirstSibling();
            overlay = holder.GetComponent<MinimapOverlay>();
            overlay.raycastTarget = false;
            overlay.rectTransform.anchorMin = Vector2.zero;
            overlay.rectTransform.anchorMax = Vector2.one;
            overlay.rectTransform.offsetMin = Vector2.zero;
            overlay.rectTransform.offsetMax = Vector2.zero;
        }
        overlay.Capture(source, camera, rooms, initialCell);
        var renderer = source.GetComponent<TilemapRenderer>();
        if (renderer != null) renderer.enabled = false;
    }

    private void Capture(Tilemap source, Camera camera, IReadOnlyList<RectInt> rooms, Vector3Int initialCell)
    {
        var cells = new List<Vector2Int>();
        foreach (var cell in source.cellBounds.allPositionsWithin)
            if (source.HasTile(cell)) cells.Add(new Vector2Int(cell.x, cell.y));
        exploration = new MinimapExploration(cells, rooms);
        exploration.Reveal(new Vector2Int(initialCell.x, initialCell.y));
        contours = MinimapContours.Build(exploration.KnownCells, exploration.IsTerrainOpen);
        viewportOrigin = camera.WorldToViewportPoint(source.CellToWorld(Vector3Int.zero));
        viewportX = (Vector2)camera.WorldToViewportPoint(source.CellToWorld(Vector3Int.right)) - viewportOrigin;
        viewportY = (Vector2)camera.WorldToViewportPoint(source.CellToWorld(Vector3Int.up)) - viewportOrigin;
        color = OutlineColor * source.color;
        worldGrid = GameManager.FloorTilemap;
        groundTargets.Clear();
        symbols.Clear();
        TrackTag("Exit", MinimapSymbols.Kind.Stairs);
        TrackTag("Trap", MinimapSymbols.Kind.Trap);
        TrackTag("Item", MinimapSymbols.Kind.Item);
        TrackTag("Coin", MinimapSymbols.Kind.Item);
        // 지난 층에서 가져온 물건도 내려놓거나 던지면 표시한다.
        for (int i = 0; i < GameManager.Bag.Count; i++)
            TrackGround(GameManager.Bag[i], MinimapSymbols.Kind.Item);
        SetAllDirty();
    }

    /// <summary>층 생성 후 새로 드롭된 아이템도 같은 탐색/표시 규칙으로 추적한다.</summary>
    public static void TrackSpawnedItem(GameObject item)
    {
        var game = GameManager.instance;
        if (game == null || game.UiCanvas == null) return;
        var overlay = game.UiCanvas.GetComponentInChildren<MinimapOverlay>(true);
        if (overlay != null) overlay.TrackGround(item, MinimapSymbols.Kind.Item);
    }

    private void TrackTag(string tag, MinimapSymbols.Kind kind)
    {
        foreach (var target in GameObject.FindGameObjectsWithTag(tag)) TrackGround(target, kind);
    }

    private void TrackGround(GameObject target, MinimapSymbols.Kind kind)
    {
        if (target == null) return;
        foreach (var tracked in groundTargets)
            if (tracked.Object == target) return;
        groundTargets.Add(new GroundTarget { Object = target, Kind = kind });
    }

    private void LateUpdate()
    {
        var game = GameManager.instance;
        if (game == null || worldGrid == null) return;
        hasPlayerVision = game.Player != null && game.Player.activeInHierarchy;
        if (hasPlayerVision)
        {
            Vector3Int position = worldGrid.WorldToCell(game.Player.transform.position);
            playerCell = new Vector2Int(position.x, position.y);
            if (exploration.Reveal(playerCell))
            {
                contours = MinimapContours.Build(exploration.KnownCells, exploration.IsTerrainOpen);
                SetVerticesDirty();
            }
        }
        nextSymbols.Clear();
        foreach (var tracked in groundTargets)
        {
            var target = tracked.Object;
            if (target == null || !target.activeInHierarchy) continue;
            // 가방 속 물건과 날아가는 물건은 바닥 표시에서 제외한다.
            if (!GameManager.Ground.Contains(target.transform.position)) continue;
            Collect(target, tracked.Kind);
        }
        foreach (var enemy in game.enemies)
            if (enemy != null) Collect(enemy.gameObject, enemy.IsCombatTarget ? MinimapSymbols.Kind.Enemy : MinimapSymbols.Kind.Partner);
        if (game.MapMake != null && game.MapMake.DetectAllEnemies)
            foreach (var cell in game.MapMake.ConcealedMonsterCells)
                nextSymbols.Add(new Symbol { Kind = MinimapSymbols.Kind.Enemy,
                    Cell = new Vector2Int(cell.x, cell.y) });
        foreach (var partner in game.partners)
            if (partner != null) Collect(partner.gameObject, MinimapSymbols.Kind.Partner);
        // 겹친 칸에서도 플레이어를 가장 위에 그린다.
        Collect(game.Player, MinimapSymbols.Kind.Player);
        bool changed = symbols.Count != nextSymbols.Count;
        if (!changed)
            for (int i = 0; i < symbols.Count; i++)
                if (symbols[i].Kind != nextSymbols[i].Kind || symbols[i].Cell != nextSymbols[i].Cell)
                { changed = true; break; }
        if (!changed) return;
        var previous = symbols;
        symbols = nextSymbols;
        nextSymbols = previous;
        SetVerticesDirty();
    }

    private void Collect(GameObject target, MinimapSymbols.Kind kind)
    {
        if (target == null || !target.activeInHierarchy) return;
        Vector3Int cell = worldGrid.WorldToCell(target.transform.position);
        var mapCell = new Vector2Int(cell.x, cell.y);
        if (kind == MinimapSymbols.Kind.Enemy)
        {
            bool detected = GameManager.instance.MapMake != null && GameManager.instance.MapMake.DetectAllEnemies;
            if (!detected && (!hasPlayerVision || !exploration.CanSeeEnemy(playerCell, mapCell))) return;
        }
        else if (kind != MinimapSymbols.Kind.Player && !exploration.IsKnown(mapCell)) return;
        nextSymbols.Add(new Symbol { Kind = kind, Cell = mapCell });
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = rectTransform.rect;
        float pixel = canvas != null && canvas.scaleFactor > 0f ? 1f / canvas.scaleFactor : 1f;
        foreach (var line in contours)
        {
            Vector2 start = UiPoint(line.Start, rect);
            Vector2 end = UiPoint(line.End, rect);
            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end) + Vector2.one * pixel;
            // 같은 색의 사각형을 정수 픽셀에서 연결해 코너 틈과 반투명 이음새를 없앤다.
            int first = helper.currentVertCount;
            Add(helper, new Vector2(min.x, min.y));
            Add(helper, new Vector2(min.x, max.y));
            Add(helper, new Vector2(max.x, max.y));
            Add(helper, new Vector2(max.x, min.y));
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first + 2, first + 3, first);
        }
        DrawSymbols(helper, rect);
    }

    private Vector2 UiPoint(Vector2 cell, Rect rect)
    {
        Vector2 viewport = viewportOrigin + viewportX * cell.x + viewportY * cell.y;
        var point = new Vector2(rect.xMin + viewport.x * rect.width, rect.yMin + viewport.y * rect.height);
        return canvas != null ? RectTransformUtility.PixelAdjustPoint(point, rectTransform, canvas) : point;
    }

    private void DrawSymbols(VertexHelper helper, Rect rect)
    {
        foreach (var symbol in symbols)
        {
            Vector2 cell = symbol.Cell;
            // 기호의 5x5 모양을 한 칸 전체에 대응시킨다. 화면 픽셀 크기로 고정하지 않는다.
            Color tint = MinimapSymbols.ColorOf(symbol.Kind);
            for (int y = 0; y < MinimapSymbols.Size; y++)
            {
                int x = 0;
                while (x < MinimapSymbols.Size)
                {
                    if (!MinimapSymbols.Filled(symbol.Kind, x, y)) { x++; continue; }
                    int start = x++;
                    while (x < MinimapSymbols.Size && MinimapSymbols.Filled(symbol.Kind, x, y)) x++;
                    int first = helper.currentVertCount;
                    Add(helper, UiPoint(cell + new Vector2(start, y) / MinimapSymbols.Size, rect), tint);
                    Add(helper, UiPoint(cell + new Vector2(start, y + 1) / MinimapSymbols.Size, rect), tint);
                    Add(helper, UiPoint(cell + new Vector2(x, y + 1) / MinimapSymbols.Size, rect), tint);
                    Add(helper, UiPoint(cell + new Vector2(x, y) / MinimapSymbols.Size, rect), tint);
                    helper.AddTriangle(first, first + 1, first + 2);
                    helper.AddTriangle(first + 2, first + 3, first);
                }
            }
        }
    }

    private void Add(VertexHelper helper, Vector2 point, Color? tint = null)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = tint ?? color;
        // Graphic 기본 흰색 텍스처만 쓴다. 타일 시트 UV/압축/필터의 영향을 받지 않는다.
        helper.AddVert(vertex);
    }
}
