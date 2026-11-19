using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct MonsterHouseStageChance
{
    [Min(1)] public int fromStage;
    [Range(0f, 1f)] public float chance;
}

/// <summary>각 항목의 시작 층부터 다음 항목 직전까지 적용. 순서는 무관하며 같은 층이면 뒤 항목 우선.</summary>
[CreateAssetMenu(fileName = "MonsterHouseSpawnSettings", menuName = "Dungeon/Monster House Spawn Settings")]
public sealed class MonsterHouseSpawnSettings : ScriptableObject
{
    [Range(0f, 1f)] public float defaultChance = 0.3303f;
    public List<MonsterHouseStageChance> stages = new List<MonsterHouseStageChance>();
    public float ChanceForStage(int stage) => ResolveChance(stage, defaultChance, stages);

    public static float ResolveChance(int stage, float fallback, IReadOnlyList<MonsterHouseStageChance> entries)
    {
        float chance = fallback;
        int selectedStage = 0;
        if (entries != null)
            foreach (var entry in entries)
                if (entry.fromStage >= 1 && entry.fromStage <= stage && entry.fromStage >= selectedStage)
                {
                    selectedStage = entry.fromStage;
                    chance = entry.chance;
                }
        return Mathf.Clamp01(chance);
    }
}
