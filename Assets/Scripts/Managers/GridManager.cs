using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    // PURE C# CLASSES
    private BoardState board;
    private MatchScanner matchScanner;
    private PowerupProcessor powerupProcessor;
    private DeadlockAndHintManager deadlockAndHintManager;
    private GravityService gravityService;

    // Level details and goals.
    private int movesAllowed;
    private List<LevelObjective> levelGoals;

    private int _powerupChainDepth = 0;
    private HashSet<int> _pendingGravityColumns = new HashSet<int>();
    private bool isReshuffling = false;

    private float nextDeadlockCheckTime = 0f;
    private const float DeadlockCheckInterval = 0.25f; // change to serialize field

    private float nextHintingCheckTime = 0f;
    private const float HintingCheckInterval = 6f; // change to serialize field

    private HintData bestMoveToHint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        matchScanner = new MatchScanner();
        powerupProcessor = new PowerupProcessor();
        deadlockAndHintManager = new DeadlockAndHintManager(matchScanner);
        gravityService = new GravityService();
    }

    private void OnEnable()
    {
        InputManager.Instance.OnSwipeRequested += HandleSwap;
        InputManager.Instance.OnScreenTapped += HandleScreenTapped;
    }

    private void OnDisable()
    {
        InputManager.Instance.OnSwipeRequested -= HandleSwap;
        InputManager.Instance.OnScreenTapped -= HandleScreenTapped;
    }

    private void Start() 
    {
        InitializeGrid();
        nextDeadlockCheckTime = Time.time + DeadlockCheckInterval;
        ResetHintTimer(false);
    }

    private void Update()
    {
        if (isReshuffling) return;
        if (Time.time >= nextDeadlockCheckTime)
        {
            nextDeadlockCheckTime = Time.time + DeadlockCheckInterval;

            if (!deadlockAndHintManager.IsBoardSettled(board)) return;

            if (!deadlockAndHintManager.HasAnyPossibleMove(board))
            {
                TriggerBoardReshuffle();
            }
        }

        if (Time.time >= nextHintingCheckTime)
        {
            nextHintingCheckTime = Time.time + HintingCheckInterval;

            if (!deadlockAndHintManager.IsBoardSettled(board)) return;

            if (deadlockAndHintManager.TryGetBestHint(board, out bestMoveToHint))
            {
                TriggerHint(bestMoveToHint);
            }
        }
    }

    private void InitializeGrid()
    {
        // Instantiating the level data so changes made during the gameplay does not effect the data in the serialized object.
        LevelDataSO levelToLoad = Instantiate(LevelManager.Instance.levels[0]);
        board = BoardFactory.CreateBoard(levelToLoad);

        movesAllowed = levelToLoad.movesAllowed;
        levelGoals = levelToLoad.levelGoals;

        for (int y = 0; y < board.Height + board.BufferSize; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                GridNode node = board.GetNodeAt(x, y);

                // Only spawn visuals for playable nodes that have data
                if (node != null && node.isPlayable && node.data != null)
                {
                    node.data.visualPiece = VisualManager.Instance.SpawnPiece(x, y, node.data.coreID);
                }
            }
        }
    }

    private void HandleScreenTapped(Vector2Int gridPosition)
    {
        ResetHintTimer();

        GridNode tappedNode = board.GetNodeAt(gridPosition);
        if (tappedNode == null || tappedNode.data == null) return;
        if (tappedNode.data.type != PieceType.Powerup) return;

        ProcessPowerup(tappedNode);
    }

    private void ProcessPowerup(GridNode centerNode, int targetCoreID = -1, int forcedPowerupCoreID = -1)
    {
        if (centerNode == null) return;

        int powerupCoreID = forcedPowerupCoreID;
        if (powerupCoreID == -1)
        {
            if (centerNode.data == null || centerNode.data.type != PieceType.Powerup) return;
            powerupCoreID = centerNode.data.coreID;
        }

        // Ensure center node is destroyed so it doesn't stay as a Powerup node and doesn't infinitely recurse
        ConsumePowerupNode(centerNode);

        // 1. Ask the Brain WHAT to destroy
        HashSet<GridNode> targetsToDestroy = powerupProcessor.ProcessPowerup(board, centerNode, targetCoreID, powerupCoreID);

        // 2. Tell the Body to ACTUALLY destroy them
        ResolvePowerupTargets(targetsToDestroy);
    }

    private void ProcessPowerupCombo(GridNode centerNode, GridNode nodeA, GridNode nodeB, int coreIDA, int coreIDB)
    {
        if (centerNode == null) return;

        // Both swapped powerups are always consumed at combo start.
        ConsumePowerupNode(nodeA);
        ConsumePowerupNode(nodeB);

        HashSet<GridNode> targetsToDestroy = powerupProcessor.ProcessCombo(board, centerNode, coreIDA, coreIDB);

        // Ensure gravity runs on both swapped columns even if combo produced no direct target there.
        if (nodeA != null) _pendingGravityColumns.Add(nodeA.xPosition);
        if (nodeB != null) _pendingGravityColumns.Add(nodeB.xPosition);

        ResolvePowerupTargets(targetsToDestroy);
    }

    private void ConsumePowerupNode(GridNode node)
    {
        if (node == null || node.data == null || node.data.type != PieceType.Powerup) return;

        node.state = NodeState.Matching;
        VisualManager.Instance.DestroyPiece(node.data.visualPiece);
        node.data = null;
        node.state = NodeState.Idle;
    }

    private void ResolvePowerupTargets(HashSet<GridNode> targetsToDestroy)
    {
        _powerupChainDepth++;

        try
        {
            if (targetsToDestroy == null) return;

            foreach (GridNode target in targetsToDestroy)
            {
                if (target == null) continue;

                _pendingGravityColumns.Add(target.xPosition);
                HitNode(target);
            }
        }
        finally
        {
            _powerupChainDepth--;

            if (_powerupChainDepth == 0)
            {
                foreach (int x in _pendingGravityColumns)
                {
                    // ProcessGravityForColumn(x);
                }

                _pendingGravityColumns.Clear();
            }
        }
    }

    private void HitNode(GridNode node)
    {
        if (node == null || node.data == null) return;
        
        // Prevent double-hitting pieces that are already in motion or being resolved
        if (node.state == NodeState.Falling || node.state == NodeState.Matching) return; 

        if (node.data.type == PieceType.Powerup)
        {
            PieceData powerupData = node.data;
            int powerupCoreID = powerupData.coreID;

            node.state = NodeState.Matching;
            VisualManager.Instance.DestroyPiece(powerupData.visualPiece);
            node.data = null;
            node.state = NodeState.Idle;

            // CHAIN REACTION! A powerup hit another powerup.
            ProcessPowerup(node, -1, powerupCoreID);
        }
        else if (node.data.type == PieceType.Collectible)
        {
            ProcessCollectible(node, false); 
        }
        else if (node.data.type == PieceType.Obstacle)
        {
            ProcessObstacle(node, false); 
        }
        else
        {
            // It's a normal piece. Destroy the visuals and clear the data!
            node.state = NodeState.Matching;
            VisualManager.Instance.DestroyPiece(node.data.visualPiece);
            node.data = null;
            node.state = NodeState.Idle;
        }
    }

    private void ResetHintTimer(bool clearActiveHint = true)
    {
        nextHintingCheckTime = Time.time + HintingCheckInterval;

        if (clearActiveHint)
            VisualManager.Instance.ClearHint();
    }

    private void ProcessMatch(Match matchToProcess)
    {
        List<PieceData> extractedData = new List<PieceData>();
        HashSet<int> affectedColumns = new HashSet<int>();
        GridNode centerNode = matchToProcess.center;

        foreach (GridNode node in matchToProcess.matchedNodes)
        {
            if (node.state != NodeState.Matching && node.data != null)
            {
                node.state = NodeState.Matching;
                extractedData.Add(node.data);
                node.data = null;
                CheckAdjacentNodes(node.xPosition, node.yPosition);

                affectedColumns.Add(node.xPosition);
            }
        }

        // If it is a powerup, update grid data.
        if (matchToProcess.shape != MatchShape.Match3)
        {
            int powerupID = Utils.GetPowerupCoreID(matchToProcess.shape);
            if (powerupID == -1)
            {
                Debug.LogError("Something wrong with powerup ID");
                return;
            }

            // Synthesize the new powerup immediately so gravity affects it correctly
            PieceData newData = new PieceData(powerupID, PieceType.Powerup);
            centerNode.data = newData;
            centerNode.data.visualPiece = VisualManager.Instance.SpawnPiece(centerNode.xPosition, centerNode.yPosition, centerNode.data.coreID);

            VisualManager.Instance.CombinePieces(extractedData, new Vector3(centerNode.xPosition, centerNode.yPosition), () =>
            {
                VisualManager.Instance.DestroyPieces(extractedData);
            });
        }
        else
        {
            VisualManager.Instance.DestroyPieces(extractedData);
        }

        foreach (GridNode node in matchToProcess.matchedNodes)
        {
            node.state = NodeState.Idle;
        }

        foreach (int x in affectedColumns)
        {
            ProcessGravityForColumn(x);
        }
    }

    private void CheckAdjacentNodes(int x, int y)
    {
        GridNode top = board.GetNodeAt(x, y + 1);
        GridNode bottom = board.GetNodeAt(x, y - 1);
        GridNode right = board.GetNodeAt(x + 1, y);
        GridNode left = board.GetNodeAt(x - 1, y);

        CheckNode(top);
        CheckNode(bottom);
        CheckNode(right);
        CheckNode(left);
    }

    private void CheckNode(GridNode node)
    {
        if (node == null || node.data == null) return;
        
        if (node.data.type == PieceType.Collectible)
        {
            ProcessCollectible(node);
        }
        else if (node.data.type == PieceType.Obstacle)
        {
            ProcessObstacle(node);
        }
    }

    private void ProcessCollectible(GridNode node, bool triggerGravity = true)
    {
        if (node == null || node.data == null || node.state != NodeState.Idle) return;

        PieceData collectedData = node.data;
        node.state = NodeState.Matching;
        node.data = null;

        if (levelGoals != null)
        {
            for (int i = 0; i < levelGoals.Count; i++)
            {
                if (levelGoals[i].itemID == collectedData.coreID && levelGoals[i].targetCount > 0)
                {
                    levelGoals[i].targetCount--;
                    break;
                }
            }
        }

        VisualManager.Instance.DestroyPiece(collectedData.visualPiece);

        node.state = NodeState.Idle;
        if (triggerGravity)
            ProcessGravityForColumn(node.xPosition);
    }

    private void ProcessObstacle(GridNode node, bool triggerGravity = true)
    {
        if (node == null || node.data == null || node.state != NodeState.Idle) return;

        PieceData obstacleData = node.data;
        node.state = NodeState.Matching;
        node.data = null;

        if (levelGoals != null)
        {
            for (int i = 0; i < levelGoals.Count; i++)
            {
                if (levelGoals[i].itemID == obstacleData.coreID && levelGoals[i].targetCount > 0)
                {
                    levelGoals[i].targetCount--;
                    break;
                }
            }
        }

        VisualManager.Instance.DestroyPiece(obstacleData.visualPiece);

        node.state = NodeState.Idle;
        if (triggerGravity)
            ProcessGravityForColumn(node.xPosition);
    }

    private void ProcessGravityForColumn(int x)
    {
        List<FallInstruction> falls = gravityService.CalculateGravityForColumn(board, x);

        int delayIndex = 0;
        float delayTime = 0.04f;

        foreach (FallInstruction fall in falls)
        {
            GridNode targetNode = fall.targetNode;

            if (fall.isNewSpawn)
                fall.pieceData.visualPiece = VisualManager.Instance.SpawnPiece(fall.startX, fall.startY, fall.pieceData.coreID);

            VisualManager.Instance.MovePiece(fall.pieceData.visualPiece, fall.targetX, fall.targetY, delayIndex * delayTime, () =>
            {
                targetNode.state = NodeState.Idle;

                Match matchToProcess = matchScanner.GetMatchAt(board, targetNode.xPosition, targetNode.yPosition);
                if (matchToProcess != null)
                {
                    ProcessMatch(matchToProcess);
                }

                ProcessGravityForColumn(targetNode.xPosition);
            });

            delayIndex++;
        }
    }

    private void HandleSwap(Vector2 swipeStartPosition, Vector2 swipeDirection)
    {
        ResetHintTimer();

        Vector2Int gridPosA = Utils.CalculateGridLocation(swipeStartPosition);
        Vector2Int gridPosB = Utils.CalculateGridLocation(swipeStartPosition + swipeDirection);

        if (!board.IsInBounds(gridPosA) || !board.IsInBounds(gridPosB)) 
        {
            Debug.Log("Out of bounds");
            return;
        }

        GridNode nodeA = board.GetNodeAt(gridPosA);
        GridNode nodeB = board.GetNodeAt(gridPosB);

        if (!nodeA.isPlayable || !nodeB.isPlayable) 
        {
            Debug.Log("Can not swap unplayable nodes");
            return;
        }
        if (nodeA.data == null || nodeB.data == null)
        {
            Debug.Log("Can not swap empty nodes.");
            return;
        }
        if (nodeA.state == NodeState.Falling || nodeA.state == NodeState.Matching || nodeA.state == NodeState.Swapping ||
            nodeB.state == NodeState.Falling || nodeB.state == NodeState.Matching || nodeB.state == NodeState.Swapping)
        {
            Debug.Log("Can not swap matching, falling or swapping nodes.");
            return;
        }

        bool aIsObstacle = nodeA.data.type == PieceType.Obstacle;
        bool bIsObstacle = nodeB.data.type == PieceType.Obstacle;

        // Two obstacles can not switch.
        if (aIsObstacle && bIsObstacle) return;

        // If one of them is an obstacle, shake other piece/
        if (aIsObstacle)
        {
            VisualManager.Instance.ShakeAtPosition(nodeB.data.visualPiece, swipeDirection);
            return;
        }
        else if (bIsObstacle)
        {
            VisualManager.Instance.ShakeAtPosition(nodeA.data.visualPiece, swipeDirection);
            return;
        }

        // Lock both nodes to prevent concurrent swaps
        nodeA.state = NodeState.Swapping;
        nodeB.state = NodeState.Swapping;

        // Capture visual pieces and logical positions BEFORE data swap
        GameObject visualA = nodeA.data.visualPiece;
        GameObject visualB = nodeB.data.visualPiece;
        Vector3 logicalPosA = new Vector3(gridPosA.x, gridPosA.y, 0);
        Vector3 logicalPosB = new Vector3(gridPosB.x, gridPosB.y, 0);

        // Check piece type, if they are powerups or not
        bool aIsPowerup = nodeA.data.type == PieceType.Powerup;
        bool bIsPowerup = nodeB.data.type == PieceType.Powerup;
        int powerupCoreIDA = aIsPowerup ? nodeA.data.coreID : -1;
        int powerupCoreIDB = bIsPowerup ? nodeB.data.coreID : -1;

        // Swap the data
        PieceData temp = nodeA.data;
        nodeA.data = nodeB.data;
        nodeB.data = temp;

        // visualA (from posA) moves to posB; visualB (from posB) moves to posA
        VisualManager.Instance.SwapPieces(visualA, visualB, logicalPosB, logicalPosA, () =>
        {
            // The swap animation is finished; unlock before evaluating matches.
            // ScanLocal2x2Match requires Idle nodes, so checking while Swapping would miss valid 2x2 matches.
            nodeA.state = NodeState.Idle;
            nodeB.state = NodeState.Idle;

            if (!aIsPowerup && !bIsPowerup && !matchScanner.HasMatchAt(board, gridPosA) && !matchScanner.HasMatchAt(board, gridPosB))
            {
                // No match formed — swap data back and animate the reversal
                nodeA.state = NodeState.Swapping;
                nodeB.state = NodeState.Swapping;

                PieceData swapBack = nodeA.data;
                nodeA.data = nodeB.data;
                nodeB.data = swapBack;

                VisualManager.Instance.SwapPieces(visualA, visualB, logicalPosA, logicalPosB, () =>
                {
                    nodeA.state = NodeState.Idle;
                    nodeB.state = NodeState.Idle;
                });
                return;
            }

            if (aIsPowerup && bIsPowerup)
            {
                // Use swipe destination as combo center.
                ProcessPowerupCombo(nodeB, nodeA, nodeB, powerupCoreIDA, powerupCoreIDB);
                return;
            }

            // After the swap, slot A holds what was originally B, slot B holds what was originally A.
            // So slot A should be processed as a powerup if B was a powerup, and vice versa.
            if (bIsPowerup)
                ProcessPowerup(nodeA, nodeB.data?.coreID ?? -1);
            else
            {
                Match matchToProcess = matchScanner.GetMatchAt(board, gridPosA.x, gridPosA.y);
                if (matchToProcess != null)
                {
                    ProcessMatch(matchToProcess);
                }
            }

            if (aIsPowerup)
                ProcessPowerup(nodeB, nodeA.data?.coreID ?? -1);
            else
            {
                Match matchToProcess = matchScanner.GetMatchAt(board, gridPosB.x, gridPosB.y);
                if (matchToProcess != null)
                {
                    ProcessMatch(matchToProcess);
                }
            }
        });

    }

    private void TriggerBoardReshuffle()
    {
        isReshuffling = true;
        List<GridNode> movableNodes = new List<GridNode>();

        // 1. Gather nodes that can be shuffled
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                GridNode node = board.GetNodeAt(x, y);
                if (node == null || !node.isPlayable || node.data == null) continue;
                if (node.data.type == PieceType.Obstacle || node.data.type == PieceType.Collectible) continue;
                
                movableNodes.Add(node);
            }
        }

        if (movableNodes.Count <= 1)
        {
            isReshuffling = false;
            return;
        }

        if (deadlockAndHintManager.TryCalculateShuffle(board, movableNodes, out List<PieceData> shuffledData))
        {
            int finished = 0;
            int total = movableNodes.Count;

            // 3. Tell the VisualManager to move them!
            for (int i = 0; i < movableNodes.Count; i++)
            {
                GridNode node = movableNodes[i];
                node.data = shuffledData[i]; // Apply the data permanently
                node.state = NodeState.Swapping;

                VisualManager.Instance.MovePiece(node.data.visualPiece, node.xPosition, node.yPosition, 0f, () =>
                {
                    node.state = NodeState.Idle;
                    finished++;
                    if (finished == total)
                    {
                        isReshuffling = false;
                        nextDeadlockCheckTime = Time.time + DeadlockCheckInterval;
                        ResetHintTimer();
                    }
                });
            }
        }
        else
        {
            isReshuffling = false; // Failsafe
        }
    }

    private void TriggerHint(HintData hintData)
    {
        if (hintData.nodeToSwap == null && (hintData.nodesToHint == null || hintData.nodesToHint.Count == 0)) return;
        VisualManager.Instance.HintMatch(hintData.nodesToHint, hintData.nodeToSwap);
    }

}
