using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//방은 실제 게임 맵에 배치될 구조물이며, 위치, 크기, 출구 등을 포함.
/*public class Room : MonoBehaviour
{
    public int sectionIndex; // 어떤 섹션에 속하는 방인지
    public RectInt area;     // 방의 위치 및 크기 (x, y, width, height)
    public List<Joint> joints = new List<Joint>(); // 출구 정보 목록

    public Room(int sectionIndex, RectInt area)
    {
        this.sectionIndex = sectionIndex;
        this.area = area;
    }

    // 방의 중심 좌표
    public Vector2Int Center => new Vector2Int(area.x + area.width / 2, area.y + area.height / 2);
}*/

//순수 데이터 이므로 MonoBehaviour 제거
public class Room
{
    public int sectionIndex;            // 어떤 섹션에 속하는 방인지
    public RectInt area;                // 방의 위치 및 크기
    public List<Joint> joints;           // 출구(조인트) 목록

    public Room(int sectionIndex, RectInt area)
    {
        this.sectionIndex = sectionIndex;
        this.area = area;
        this.joints = new List<Joint>();
    }

    // 방의 중심 좌표
    public Vector2Int Center
    {
        get
        {
            return new Vector2Int(
                area.x + area.width / 2,
                area.y + area.height / 2
            );
        }
    }
}
