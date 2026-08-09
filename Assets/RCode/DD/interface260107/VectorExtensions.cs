using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Vector 타입 변환을 위한 확장 메서드 모음
public static class VectorExtensions
{
    // Vector2Int → Vector3Int 변환(z = 0)
    public static Vector3Int ToVector3Int(this Vector2Int v)
    {
        return new Vector3Int(v.x, v.y, 0);
    }

    // Vector2 → Vector3Int 변환 (소수점 반올림)
    public static Vector3Int ToVector3Int(this Vector2 v)
    {
        return new Vector3Int(
            Mathf.RoundToInt(v.x),
            Mathf.RoundToInt(v.y),
            0
        );
    }

    // Vector3 → Vector3Int 변환
    public static Vector3Int ToVector3Int(this Vector3 v)
    {
        return Vector3Int.RoundToInt(v);
    }
}