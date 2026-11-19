using UnityEngine;

/// <summary>미니맵 한 칸을 가득 채울 기호의 색과 5x5 기본 모양. 스프라이트 없이 선명하게 표시한다.</summary>
public static class MinimapSymbols
{
    public enum Kind { Stairs, Trap, Item, Enemy, Partner, Player }
    public const int Size = 5;

    public static Color32 ColorOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Player: return new Color32(255, 255, 255, 255);
            case Kind.Partner: return new Color32(255, 255, 0, 255);
            case Kind.Enemy:
            case Kind.Trap: return new Color32(255, 0, 0, 255);
            default: return new Color32(0, 0, 255, 255);
        }
    }

    public static bool Filled(Kind kind, int x, int y)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size) return false;
        switch (kind)
        {
            case Kind.Player:
                int dx = x - 2, dy = y - 2;
                return dx * dx + dy * dy <= 6;
            case Kind.Trap: return x == y || x + y == Size - 1;
            case Kind.Stairs: return x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
            default: return true;
        }
    }
}
