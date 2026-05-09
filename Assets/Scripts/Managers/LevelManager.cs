using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LevelObjective
{
    public int itemID;
    public int targetCount;
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    public LevelDataSO[] levels;

    public event Action<int> OnMovesChanged;
    public event Action<int, int> OnGoalChanged;
    public event Action<int, IReadOnlyList<LevelObjective>> OnLevelInitialized;

    public LevelDataSO CurrentLevelData => currentlevelData;
    public int CurrentMoves => movesAllowed;
    public IReadOnlyList<LevelObjective> CurrentGoals => levelGoals;

    private LevelDataSO currentlevelData;
    private int movesAllowed;
    private List<LevelObjective> levelGoals;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        LoadLevel(0);
    }

    public void LoadLevel(int levelID)
    {
        // Instantiating the level data so changes made during the gameplay does not effect the data in the serialized object.
        currentlevelData = Instantiate(levels[levelID]);

        movesAllowed = CurrentLevelData.movesAllowed;
        levelGoals = CloneObjectives(CurrentLevelData.levelGoals);

        OnLevelInitialized?.Invoke(CurrentMoves, CurrentGoals);
        OnMovesChanged?.Invoke(CurrentMoves);
    }

    public bool TryConsumeMove()
    {
        if (movesAllowed <= 0)
            return false;

        movesAllowed--;
        OnMovesChanged?.Invoke(CurrentMoves);
        return true;
    }

    public void ProcessLevelObjective(int coreID)
    {
        if (levelGoals != null)
        {
            for (int i = 0; i < levelGoals.Count; i++)
            {
                if (levelGoals[i].itemID == coreID && levelGoals[i].targetCount > 0)
                {
                    levelGoals[i].targetCount--;
                    OnGoalChanged?.Invoke(coreID, levelGoals[i].targetCount);
                    break;
                }
            }
        }
    }

    private List<LevelObjective> CloneObjectives(List<LevelObjective> sourceGoals)
    {
        List<LevelObjective> clonedGoals = new List<LevelObjective>();
        if (sourceGoals == null) return clonedGoals;

        for (int i = 0; i < sourceGoals.Count; i++)
        {
            LevelObjective goal = sourceGoals[i];
            if (goal == null) continue;

            clonedGoals.Add(new LevelObjective
            {
                itemID = goal.itemID,
                targetCount = goal.targetCount
            });
        }

        return clonedGoals;
    }
}
