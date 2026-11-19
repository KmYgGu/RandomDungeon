using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>배치 모드에서 씬을 저장하지 않고 UI의 실제 화면 좌표를 검사한다.</summary>
public static class UiCameraVerification
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("배치 모드 전용입니다.");
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var game = UnityEngine.Object.FindObjectOfType<GameManager>();
            GameManager.instance = game;
            game.ConfigureUiCanvas();
            var canvas = game.UiCanvas.GetComponent<Canvas>();
            Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null,
                "UI가 게임 카메라와 연결되어 있음");
            var generator = UnityEngine.Object.FindObjectOfType<RandomDung>();
            generator.GenerateTerrain(1, 12345);
            MinimapOverlay.Refresh(generator.Minimap, generator.RoomCells, generator.StartCell);
            Canvas.ForceUpdateCanvases();
            var overlay = canvas.GetComponentInChildren<MinimapOverlay>();
            Require(overlay != null && !overlay.raycastTarget, "미니맵 UI 생성 또는 입력 통과 실패");
            var mesh = new Mesh();
            using (var helper = new VertexHelper())
            {
                typeof(MinimapOverlay).GetMethod("OnPopulateMesh",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(overlay, new object[] { helper });
                helper.FillMesh(mesh);
            }
            Require(mesh.vertexCount > 0, "미니맵 UI 메시가 비어 있음");
            Require(!generator.Minimap.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().enabled,
                "원본 월드 미니맵이 중복 표시됨");

            var targets = canvas.GetComponentsInChildren<Graphic>(true);
            Vector2[] before = ScreenPositions(targets);
            Vector3 original = Camera.main.transform.position;
            foreach (Vector3 offset in new[] { new Vector3(.125f, .375f), new Vector3(1f, 1f), new Vector3(20f, -30f) })
            {
                Camera.main.transform.position = original + offset;
                Canvas.ForceUpdateCanvases();
                Vector2[] after = ScreenPositions(targets);
                for (int i = 0; i < before.Length; i++)
                    Require((before[i] - after[i]).sqrMagnitude < .0001f,
                        "카메라 이동으로 UI 화면 좌표가 바뀜: " + i);
            }
            // 다음 층에도 같은 UI 인스턴스가 갱신되는지 확인한다.
            generator.GenerateTerrain(2, 54321);
            MinimapOverlay.Refresh(generator.Minimap, generator.RoomCells, generator.StartCell);
            Require(canvas.GetComponentsInChildren<MinimapOverlay>(true).Length == 1,
                "층 갱신 시 미니맵 UI 중복 생성");
            Debug.Log("UI_CAMERA_VERIFICATION_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static Vector2[] ScreenPositions(Graphic[] graphics)
    {
        var result = new Vector2[graphics.Length * 4];
        var corners = new Vector3[4];
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].rectTransform.GetWorldCorners(corners);
            for (int j = 0; j < 4; j++)
                result[i * 4 + j] = RectTransformUtility.WorldToScreenPoint(null, corners[j]);
        }
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
