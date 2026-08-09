using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 굶어 죽어 가는 동안 화면의 모든 글자에 연두색 테를 두른다.
///
/// 경고 문구는 뜬 뒤 몇 초면 사라지므로, 그것만으로는 "지금 굶고 있다" 는 상태가
/// 화면에 남지 않는다. 만복도가 바닥나 체력이 깎이기 시작하면 글자 전체의 모습이
/// 바뀌어, 무엇을 보고 있든 그 사실이 눈에 들어오게 한다.
///
/// ── TMP 에서 외곽선을 켤 때 반드시 해야 하는 두 가지 ────────────────
/// 1. fontMaterial 로 재질 사본을 만든다.
///    이 프로젝트의 글자는 전부 같은 폰트(DungGeunMo SDF)의 같은 재질을 쓴다.
///    공유 재질을 직접 건드리면 그 폰트를 쓰는 다른 것까지 함께 변하고,
///    무엇보다 아래 2번의 여백이 글자마다 달라야 해서 사본이 필요하다.
///
/// 2. UpdateMeshPadding() 을 부른다.
///    TMP 는 글자 메시를 만들 때 "테가 삐져나갈 만큼" 의 여백을 미리 잡아 둔다.
///    그 여백은 재질을 바꾸는 것만으로는 다시 계산되지 않아서, 부르지 않으면
///    테가 메시 경계에서 잘려 나가거나 아예 보이지 않는다.
///    처음 만들었을 때 HUD 만 테가 안 보였던 것이 이 때문이다.
/// </summary>
public class StarvingTint
{
    /// <summary>두르는 색.</summary>
    private static readonly Color OutlineColor = new Color(0.55f, 0.95f, 0.35f);

    /// <summary>
    /// 테의 굵기(0~1).
    ///
    /// 이 폰트는 획이 얇은 픽셀 글꼴이라 조금만 두꺼워도 글자 속이 메워진다.
    /// 0.2 로 두었더니 흰 글자가 통째로 초록으로 보였다. 테가 둘렸다는 것만
    /// 알아볼 수 있으면 되므로 얇게 잡는다.
    /// </summary>
    private const float OutlineWidth = 0.1f;

    /// <summary>
    /// 마지막에 만들어진 것. 나중에 생기는 글자가 자기를 등록할 수 있게 둔다.
    ///
    /// GameManager 가 판마다 하나만 만들고, 화면에 붙는 글자는 그것 하나만 본다.
    /// </summary>
    private static StarvingTint current;

    private readonly List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();

    /// <summary>지금 테가 둘러져 있는가. 값이 바뀔 때만 손대기 위해 들고 있는다.</summary>
    private bool applied;

    public StarvingTint(GameObject uiRoot)
    {
        // 꺼져 있는 창(가방·상태창)의 글자까지 미리 잡아 둔다.
        // 굶는 동안 그 창을 열면 거기도 테가 둘러져 있어야 한다.
        if (uiRoot != null)
            texts.AddRange(uiRoot.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true));

        current = this;
    }

    /// <summary>
    /// 씬이 만들어진 뒤에 생긴 글자를 목록에 넣는다.
    ///
    /// 넣는 즉시 지금 상태를 입히고, 그 뒤로는 다른 글자와 함께 바뀐다.
    /// 이것이 없으면 나중에 만든 글자만 홀로 테가 없어, 굶는 중인데
    /// 거기만 멀쩡해 보인다.
    /// </summary>
    public static void Register(TextMeshProUGUI text)
    {
        if (text == null || current == null) return;
        if (current.texts.Contains(text)) return;

        current.texts.Add(text);
        Apply(text, current.applied);
    }

    /// <summary>
    /// 굶고 있는지 알려 준다. 매 프레임 불러도 된다 —
    /// 상태가 바뀐 순간에만 실제로 글자를 건드린다.
    /// </summary>
    public void SetStarving(bool starving)
    {
        if (starving == applied) return;
        applied = starving;

        foreach (TextMeshProUGUI text in texts)
        {
            if (text == null) continue;
            Apply(text, starving);
        }
    }

    private static void Apply(TextMeshProUGUI text, bool starving)
    {
        Material material = text.fontMaterial;
        if (material == null) return;

        material.SetColor(ShaderUtilities.ID_OutlineColor,
            starving ? OutlineColor : Color.clear);

        material.SetFloat(ShaderUtilities.ID_OutlineWidth,
            starving ? OutlineWidth : 0f);

        // 테가 들어갈 자리를 다시 계산한다. 이걸 빠뜨리면 그려도 잘려 안 보인다.
        text.UpdateMeshPadding();
    }
}
