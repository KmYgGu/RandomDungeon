using UnityEngine;

/// <summary>
/// 창 하나를 화면 어디에 얼마만 한 크기로 놓을지 계산한다.
///
/// 계산만 하고 아무것도 그리지 않는다. RectTransform 을 건드리지 않기 때문에
/// Unity 없이도 그대로 돌려 볼 수 있고, 실제로 Tools/Verify 에서 검사한다.
/// 창을 그리는 일은 PanelView 가 맡는다.
///
/// 지켜야 하는 것은 두 가지다.
///   1. 내용이 딱 들어갈 만큼만 커진다(빈 상자가 화면을 덮지 않게).
///   2. 위쪽 HUD(층·레벨·HP·소지금·체력바)를 절대 가리지 않는다.
///
/// 2번이 특히 중요하다. 창이 내용에 따라 커지는 순간부터 "적당히 배치해 두면
/// 되겠지" 가 통하지 않는다. 긴 설명이 들어오면 상자가 위로 자라 HUD 를 덮어
/// 버리기 때문이다. 그래서 자라는 방향을 아래쪽으로 제한한다.
/// </summary>
public static class PanelLayout
{
    /// <summary>
    /// 창이 넘어가면 안 되는 위쪽 한계(캔버스 좌표).
    ///
    /// HUD 중 가장 아래에 있는 것은 체력바이고 그 아래 끝이 y≈157 이다.
    /// 기존 가방 창도 위 끝이 157.3 이었다 — 눈으로 맞춰 둔 값이었는데,
    /// 이제 그 규칙을 숫자로 못 박아 창이 어떻게 자라든 지켜지게 한다.
    /// </summary>
    public const float HudBottom = 155f;

    /// <summary>화면 가장자리에 남길 여백.</summary>
    public const float ScreenMargin = 8f;

    /// <summary>계산 결과. 창의 크기와 중심 위치(캔버스 좌표).</summary>
    public struct Placement
    {
        public Vector2 Size;
        public Vector2 Center;
    }

    /// <summary>
    /// 내용 크기에 맞춰 창을 놓는다.
    ///
    /// canvasSize 는 캔버스의 가로·세로다(중심이 원점이라고 본다).
    /// desiredCenter 는 놓고 싶은 자리고, 그대로 두면 화면이나 HUD 를 침범할 때만
    /// 필요한 만큼 밀어 준다. 그래도 안 들어가면 창 자체를 줄인다.
    /// </summary>
    public static Placement Fit(Vector2 contentSize, Vector2 padding, Vector2 canvasSize, Vector2 desiredCenter)
    {
        // 창이 쓸 수 있는 영역: 좌우는 화면 안쪽, 위는 HUD 아래까지.
        float left = -canvasSize.x * 0.5f + ScreenMargin;
        float right = canvasSize.x * 0.5f - ScreenMargin;
        float bottom = -canvasSize.y * 0.5f + ScreenMargin;

        // HUD 가 화면 밖에 있을 만큼 세로가 짧은 경우까지 생각해 둔다.
        float top = Mathf.Min(HudBottom, canvasSize.y * 0.5f - ScreenMargin);

        float availableWidth = Mathf.Max(right - left, 0f);
        float availableHeight = Mathf.Max(top - bottom, 0f);

        // 1. 내용 + 여백이 기본 크기. 단 쓸 수 있는 영역을 넘지 않는다.
        Vector2 size;
        size.x = Mathf.Min(contentSize.x + padding.x, availableWidth);
        size.y = Mathf.Min(contentSize.y + padding.y, availableHeight);

        // 2. 그 크기가 영역 안에 들어오도록 중심을 민다.
        //    Clamp 의 min 이 max 보다 커지는 일이 없도록 위에서 크기를 이미 제한해 두었다.
        float half = size.x * 0.5f;
        float centerX = Mathf.Clamp(desiredCenter.x, left + half, right - half);

        half = size.y * 0.5f;
        float centerY = Mathf.Clamp(desiredCenter.y, bottom + half, top - half);

        return new Placement { Size = size, Center = new Vector2(centerX, centerY) };
    }

    /// <summary>이 배치가 HUD 를 건드리는가. 검사용.</summary>
    public static bool CoversHud(Placement placement)
    {
        return placement.Center.y + placement.Size.y * 0.5f > HudBottom + 0.001f;
    }
}
