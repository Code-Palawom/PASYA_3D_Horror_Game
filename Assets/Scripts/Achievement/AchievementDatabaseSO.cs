using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AchievementDatabase", menuName = "Achievement/Database")]
public class AchievementDatabaseSO : ScriptableObject {
    public List<AchievementDefinitionSO> achievements = new List<AchievementDefinitionSO>();
}