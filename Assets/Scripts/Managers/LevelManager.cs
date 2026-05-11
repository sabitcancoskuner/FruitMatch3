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
    public event Action OnGameWin;
    public event Action OnGameLose;

    public LevelDataSO CurrentLevelData => currentlevelData;
    public int CurrentMoves => movesAllowed;
    public IReadOnlyList<LevelObjective> CurrentGoals => levelGoals;

    private LevelDataSO currentlevelData;
    private int movesAllowed;
    private List<LevelObjective> levelGoals;

    public static int TargetLevelId = 0;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        LoadLevel(TargetLevelId);
    }

    private void Start()
    {
        if (GridManager.Instance != null)
        {
            GridManager.Instance.OnBoardSettled += CheckForWinSituation;
            GridManager.Instance.OnBoardSettled += CheckForLoseSituation;
        }
    }

    private void OnDisable()
    {
        if (GridManager.Instance != null)
        {
            GridManager.Instance.OnBoardSettled -= CheckForWinSituation;
            GridManager.Instance.OnBoardSettled -= CheckForLoseSituation;
        }
    }

    public void LoadLevel(int levelID)
    {
        if (levels.Length == 0) return;
        if (levelID < 0 || levelID >= levels.Length) levelID = 0;

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

    private void CheckForWinSituation()
    {
        int finishedGoals = 0;

        foreach(LevelObjective goal in levelGoals)
        {
            if (goal.targetCount == 0)
            {
                finishedGoals++;
            }
        }

        if (finishedGoals == levelGoals.Count)
        {
            OnGameWin?.Invoke();
        }
    }

    private void CheckForLoseSituation()
    {
        if (movesAllowed == 0)
        {
            OnGameLose?.Invoke();
        }
    }
}
