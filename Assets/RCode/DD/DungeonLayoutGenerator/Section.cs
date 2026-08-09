using UnityEngine;

// 런타임에서 사용되는 섹션 정보 클래스
public class Section
{
    public int index;
    public RectInt bounds;

    // int → Vector2Int 로 변경
    public Vector2Int minRoomSize;
    public Vector2Int maxRoomSize;

    public bool forceNoRoom;
    public float roomWeight;
}

