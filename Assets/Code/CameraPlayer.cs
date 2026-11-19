using UnityEngine;

/// <summary>걷기 표시 좌표와 카메라를 일치시켜 플레이어를 중앙에 유지한다.</summary>
public class CameraPlayer : MonoBehaviour
{
    private Player player;
    private void LateUpdate()
    {
        if (player == null)
        {
            var found = GameObject.FindGameObjectWithTag("Player");
            if (found == null) return;
            player = found.GetComponent<Player>();
        }
        if (player != null) SnapTo(player.UpdateWalkPresentation());
    }
    public void SnapTo(Vector2 target)
    {
        transform.position = new Vector3(target.x, target.y, transform.position.z);
    }
}

/// <summary>한 걸음의 표시 진행률. 달리기와 이벤트는 즉시 완료한다.</summary>
public static class CameraFollowMotion
{
    public static float Progress(float elapsed, float duration, bool immediate)
    {
        if (immediate || duration <= 0f) return 1f;
        return System.Math.Max(0f, System.Math.Min(1f, elapsed / duration));
    }
}
