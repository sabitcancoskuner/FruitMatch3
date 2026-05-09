using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TopPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI movesLeft;

    [Header("Level Objectives")]
    [SerializeField] private Transform objectiveParent;
    [SerializeField] private GameObject objective;

    private readonly Dictionary<int, TextMeshProUGUI> objectiveCountTexts = new Dictionary<int, TextMeshProUGUI>();
    private bool isSubscribedToLevelManagerEvents;

    private void OnDisable()
    {
        UnsubscribeFromLevelManagerEvents();
    }

    private void Start()
    {
        SubscribeToLevelManagerEvents();
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (LevelManager.Instance == null) return;

        SetMovesLeftText(LevelManager.Instance.CurrentMoves);
        RebuildObjectives(LevelManager.Instance.CurrentGoals);
    }

    private void HandleLevelInitialized(int moves, IReadOnlyList<LevelObjective> goals)
    {
        SetMovesLeftText(moves);
        RebuildObjectives(goals);
    }

    private void HandleGoalChanged(int itemID, int remainingCount)
    {
        if (objectiveCountTexts.TryGetValue(itemID, out TextMeshProUGUI countText))
        {
            SetObjectiveCountText(countText, remainingCount);
        }
    }

    private void RebuildObjectives(IReadOnlyList<LevelObjective> goals)
    {
        ClearObjectives();
        if (goals == null) return;

        for (int i = 0; i < goals.Count; i++)
        {
            LevelObjective goal = goals[i];
            if (goal == null) continue;

            CreateObjective(goal.itemID, goal.targetCount);
        }
    }

    private void CreateObjective(int itemID, int count)
    {
        GameObject newObjective = Instantiate(objective, objectiveParent);

        Image objectiveImage = newObjective.GetComponentInChildren<Image>();
        if (objectiveImage != null && VisualManager.Instance != null)
        {
            Sprite objectiveSprite = VisualManager.Instance.GetSpriteForCoreID(itemID);
            if (objectiveSprite != null)
            {
                objectiveImage.sprite = objectiveSprite;
            }
        }

        TextMeshProUGUI objectiveCountText = newObjective.GetComponentInChildren<TextMeshProUGUI>();
        if (objectiveCountText != null)
        {
            SetObjectiveCountText(objectiveCountText, count);
            objectiveCountTexts[itemID] = objectiveCountText;
        }
    }

    private void ClearObjectives()
    {
        objectiveCountTexts.Clear();
        if (objectiveParent == null) return;

        for (int i = objectiveParent.childCount - 1; i >= 0; i--)
        {
            Destroy(objectiveParent.GetChild(i).gameObject);
        }
    }

    private void SubscribeToLevelManagerEvents()
    {
        if (isSubscribedToLevelManagerEvents) return;
        if (LevelManager.Instance == null) return;

        LevelManager.Instance.OnLevelInitialized += HandleLevelInitialized;
        LevelManager.Instance.OnMovesChanged += SetMovesLeftText;
        LevelManager.Instance.OnGoalChanged += HandleGoalChanged;
        isSubscribedToLevelManagerEvents = true;
    }

    private void UnsubscribeFromLevelManagerEvents()
    {
        if (!isSubscribedToLevelManagerEvents) return;
        if (LevelManager.Instance == null) return;

        LevelManager.Instance.OnLevelInitialized -= HandleLevelInitialized;
        LevelManager.Instance.OnMovesChanged -= SetMovesLeftText;
        LevelManager.Instance.OnGoalChanged -= HandleGoalChanged;
        isSubscribedToLevelManagerEvents = false;
    }

    public void SetMovesLeftText(int moves)
    {
        movesLeft.text = moves.ToString();
    }

    private void SetObjectiveCountText(TextMeshProUGUI text, int remainingCount)
    {
        text.text = remainingCount.ToString();
    }
}
