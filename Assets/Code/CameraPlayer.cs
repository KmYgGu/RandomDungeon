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

        // ���� "Player" �±׸� ���� ������Ʈ�� ã�� ���ϸ� ���� �޽��� ���
        if (player == null)
        {
            Debug.LogError("Player �±׸� ���� ������Ʈ�� ã�� �� �����ϴ�22!");
        }
    }

    void Update()
    {
        // "Player" �±׸� ���� ������Ʈ�� ��ġ�� �� ������Ʈ�� ��ġ�� �����ϰ� ����
        if (player != null)
        {
            Vector3 newPosition = new Vector3(player.transform.position.x, player.transform.position.y, transform.position.z);
            transform.position = newPosition;
        }
    }
}
