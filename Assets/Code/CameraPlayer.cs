using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraPlayer : MonoBehaviour
{
    private GameObject player;

    
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");

        // 만약 "Player" 태그를 가진 오브젝트를 찾지 못하면 에러 메시지 출력
        if (player == null)
        {
            Debug.LogError("Player 태그를 가진 오브젝트를 찾을 수 없습니다22!");
        }
    }

    void Update()
    {
        // "Player" 태그를 가진 오브젝트의 위치와 이 오브젝트의 위치를 동일하게 맞춤
        if (player != null)
        {
            Vector3 newPosition = new Vector3(player.transform.position.x, player.transform.position.y, transform.position.z);
            transform.position = newPosition;
        }
    }
}
