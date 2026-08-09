using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Joint는 방의 출구 또는 방이 없는 섹션의 경계점 역할.
public class Joint// : MonoBehaviour
{
    public Vector2Int position;
    public int sectionIndex;

    public Joint(int sectionIndex, Vector2Int position)
    {
        this.sectionIndex = sectionIndex;
        this.position = position;
    }
}
