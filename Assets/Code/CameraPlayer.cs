using UnityEngine;

/// <summary>
/// 카메라를 플레이어에 붙여 둔다.
///
/// 반드시 LateUpdate 여야 한다.
/// Update 에서 따라가면 스크립트 실행 순서에 따라 Player.Update 보다 먼저 돌 수 있고,
/// 그러면 카메라는 '이번 프레임에 움직이기 전' 좌표를 쓰게 되어 한 프레임씩 뒤처진다.
/// 평소 이동은 0.1초에 한 칸이라 티가 안 나지만, 한 칸이 한 프레임인 달리기에서는
/// 캐릭터가 늘 카메라보다 한 칸 앞서 출발하는 것처럼 보였다.
/// LateUpdate 는 그 프레임의 모든 Update 가 끝난 뒤에 돌므로 항상 최신 좌표를 본다.
/// </summary>
public class CameraPlayer : MonoBehaviour
{
    private Transform player;

    private void LateUpdate()
    {
        if (!EnsurePlayer()) return;

        // z 는 카메라 자신의 값을 지킨다. 2D 에서 z 가 바뀌면 아무것도 보이지 않는다.
        Vector3 target = player.position;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
    }

    /// <summary>
    /// 플레이어를 찾아 둔다.
    ///
    /// Start 에서 한 번만 찾으면 안 된다. 플레이어는 던전이 만들어지는 도중에
    /// 생성되므로, 실행 순서에 따라 그 시점에 아직 씬에 없을 수 있다.
    /// 층을 옮길 때마다 새로 생성되기도 한다. 그래서 없으면 그때그때 다시 찾는다.
    /// </summary>
    private bool EnsurePlayer()
    {
        if (player != null) return true;

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return false;

        player = found.transform;
        return true;
    }
}
