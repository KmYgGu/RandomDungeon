using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
//Branch는 두 구역(또는 출구) 사이를 연결하는 길 정보를 저장
public class Branch// : MonoBehaviour
{
    public int fromSection;
    public int toSection;

    public Joint fromJoint;
    public Joint toJoint;

    public List<Vector2Int> path;// = new List<Vector2Int>(); // 브레젠햄으로 생성될 경로 타일 좌표들

    public Branch(int from, int to, Joint fromJoint, Joint toJoint)
    {
        this.fromSection = from;
        this.toSection = to;
        this.fromJoint = fromJoint;
        this.toJoint = toJoint;

        path = new List<Vector2Int>();
    }

}
