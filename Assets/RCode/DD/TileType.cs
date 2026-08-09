/// <summary>
/// 던전 격자 한 칸의 종류. 생성 단계에서는 이 값만 다루고,
/// 실제 타일 애셋으로 바꾸는 일은 DungeonRenderer 가 맡는다.
///
/// 예전에는 이 enum 이 RoomGenerator.cs 파일 맨 아래에 얹혀 있었다.
/// 던전 생성기 전체가 의존하는 타입이므로 자기 파일을 갖는 편이 찾기 쉽다.
/// </summary>
public enum TileType
{
    Wall,
    Floor,
    Roomfloor,
    Water,
    Corridor,
}
