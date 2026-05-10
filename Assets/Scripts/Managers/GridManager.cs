using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PrimeTween;
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

    private int _powerupChainDepth = 0;
    private HashSet<int> _pendingGravityColumns = new HashSet<int>();
    private bool isReshuffling = false;

    private const float DeadlockCheckInterval = 0.25f; // change to serialize field

    private Coroutine hintRoutine;
    private const float HintingCheckInterval = 6f; // change to serialize field

    private HintData bestMoveToHint;
    private bool wasBoardSettled;

    public event Action OnBoardSettled;

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
        
        StartCoroutine(DeadlockCoroutine());
        ResetHintTimer(false);
        wasBoardSettled = deadlockAndHintManager.IsBoardSettled(board);
    }

    private void InitializeGrid()
    {
        board = BoardFactory.CreateBoard(LevelManager.Instance.CurrentLevelData);

        for (int y = 0; y < board.Height + board.BufferSize; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                GridNode node = board.GetNodeAt(x, y);

                // Only spawn visuals for playable nodes that have data
                if (node != null && node.isPlayable && node.data != null)
                {
                    if (board.IsInPlayableBounds(x, y))
                    {
                        VisualManager.Instance.SpawnBoardMask(x, y);
                        VisualManager.Instance.PaintGridTile(x, y);
                    }
                    
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
        if (!LevelManager.Instance.TryConsumeMove())
        {
            Debug.Log("No moves left.");
            return;
        }

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

        HashSet<GridNode> targetsToDestroy = powerupProcessor.ProcessPowerup(board, centerNode, targetCoreID, powerupCoreID);

        foreach (GridNode target in targetsToDestroy)
        {
            // Only lock nodes that actually have a piece; empty nodes must stay Idle so gravity can refill them.
            if (target != null && target.data != null && target.state == NodeState.Idle)
                target.state = NodeState.Matching;
        }

        GameObject visualPiece = centerNode.data.visualPiece;

        centerNode.state = NodeState.Matching;
        // if it is a disco ball, deletes its data at the end.
        if (powerupCoreID != PowerupIDs.DiscoBall)
        {
            centerNode.data = null;
            centerNode.state = NodeState.Idle;
        }
            
        PlayPowerupAnimation(centerNode, powerupCoreID, targetsToDestroy, visualPiece);
    }

    private void PlayPowerupAnimation(GridNode centerNode, int powerupCoreID, HashSet<GridNode> targetsToDestroy, GameObject visualPiece)
    {
        // Vertical Rocket
        if (powerupCoreID == PowerupIDs.RocketVertical)
        {
            VisualManager.Instance.PlayRocketEffect(visualPiece, Vector2.up, () =>
            {
                ResolvePowerupTargetsForRocket(centerNode, targetsToDestroy);
            });
        }
        // Horizontal Rocket
        else if (powerupCoreID == PowerupIDs.RocketHorizontal)
        {
            VisualManager.Instance.PlayRocketEffect(visualPiece, Vector2.right, () =>
            {
                ResolvePowerupTargetsForRocket(centerNode, targetsToDestroy);
            });
        }
        // Bomb
        else if (powerupCoreID == PowerupIDs.Bomb)
        {
            VisualManager.Instance.PlayBombEffect(visualPiece, () =>
            {
                ResolvePowerupTargets(targetsToDestroy);
            });
        }
        // Disco Ball
        else if (powerupCoreID == PowerupIDs.DiscoBall)
        {
            VisualManager.Instance.PlayDiscoBallEffect(visualPiece, targetsToDestroy, 
            (targetNode) =>
            {
                if (targetNode == centerNode) return;

                if (targetNode.state == NodeState.Matching) 
                    targetNode.state = NodeState.Idle;
                    
                HitNode(targetNode);
                ProcessGravityForColumn(targetNode.xPosition);
            },
            () =>
            {
                centerNode.data = null;
                centerNode.state = NodeState.Idle;
                
                ProcessGravityForColumn(centerNode.xPosition);
            });

        }
        // Propeller
        else if (powerupCoreID == PowerupIDs.Propeller)
        {
            // find the node that is not a propeller
            GridNode targetNode = targetsToDestroy.FirstOrDefault(n => n != centerNode);

            if (targetNode != null && visualPiece != null)
            {
                Vector3 targetPos = new Vector3(targetNode.xPosition, targetNode.yPosition);
                VisualManager.Instance.PlayPropellerEffect(visualPiece, targetPos, () =>
                {
                    ResolvePowerupTargets(targetsToDestroy);
                });
            }
            else
            {
                ResolvePowerupTargets(targetsToDestroy); // change this to target change.
            }
        }
    }

    private void ProcessPowerupCombo(GridNode centerNode, GridNode nodeA, GridNode nodeB, int coreIDA, int coreIDB ,GameObject visualA, GameObject visualB)
    {
        if (centerNode == null || nodeA == null || nodeB == null) return;

        bool aIsRocket = coreIDA == PowerupIDs.RocketVertical || coreIDA == PowerupIDs.RocketHorizontal;
        bool bIsRocket = coreIDB == PowerupIDs.RocketVertical || coreIDB == PowerupIDs.RocketHorizontal;
        bool aIsBomb = coreIDA == PowerupIDs.Bomb;
        bool bIsBomb = coreIDB == PowerupIDs.Bomb;
        bool aIsDisco = coreIDA == PowerupIDs.DiscoBall;
        bool bIsDisco = coreIDB == PowerupIDs.DiscoBall;
        bool aIsPropeller = coreIDA == PowerupIDs.Propeller;
        bool bIsPropeller = coreIDB == PowerupIDs.Propeller;

        Vector3 centerPosition = new Vector3(centerNode.xPosition, centerNode.yPosition);

        var comboData = powerupProcessor.ProcessCombo(board, centerNode, coreIDA, coreIDB);

        ConsumePowerupNode(nodeA);
        ConsumePowerupNode(nodeB);
        
        // Ensure gravity runs on swapped columns even if combo targets miss those columns.
        _pendingGravityColumns.Add(nodeA.xPosition);
        _pendingGravityColumns.Add(nodeB.xPosition);
        
        // Rocket + rocket
        if (aIsRocket && bIsRocket)
        {
            VisualManager.Instance.PlayCrossRocketCombo(visualA, visualB, centerPosition, () =>
            {
                ResolvePowerupTargetsForRocket(centerNode, comboData.targets);
            });
            return;
        }
        // Rocket + propeller
        else if (aIsRocket && bIsPropeller || aIsPropeller && bIsRocket)
        {
            if (comboData.secondary == null || comboData.secondary.Count == 0 || comboData.secondary[0] == null)
            {
                ResolvePowerupTargets(comboData.targets);
                return;
            }

            GridNode destination = comboData.secondary[0];
            Vector3 targetPos = new Vector3(destination.xPosition, destination.yPosition);
            int deliverID = coreIDA == PowerupIDs.Propeller ? coreIDB : coreIDA;

            VisualManager.Instance.PlayPropellerDeliveryCombo(visualA, visualB, centerPosition, targetPos, deliverID, () =>
            {
                ResolvePowerupTargetsForRocket(centerNode, comboData.targets);
            });
        }
        // Rocket + Bomb
        else if (aIsRocket && bIsBomb || aIsBomb && bIsRocket)
        {
            VisualManager.Instance.PlayGiantRocketCombo(visualA, visualB, centerPosition, () =>
            {
                ResolvePowerupTargetsForRocket(centerNode, comboData.targets); 
            });
        }
        // Rocket + Disco Ball
        else if (aIsRocket && bIsDisco || aIsDisco && bIsRocket)
        {
            GameObject actualDisco = aIsDisco ? visualA : visualB;
            GameObject actualPayload = aIsDisco ? visualB : visualA;
            int payloadCoreID = aIsDisco ? coreIDB : coreIDA;

            VisualManager.Instance.PlayUniversalDiscoCombo(actualDisco, actualPayload, payloadCoreID, comboData.primary, comboData.secondary, () =>
            {
                ResolvePowerupTargetsForRocket(centerNode, comboData.targets);
            });
        }
        // Propeller + propeller
        else if (aIsPropeller && bIsPropeller)
        {
            VisualManager.Instance.PlayTriplePropellerCombo(visualA, visualB, centerPosition, comboData.primary, () =>
            {
               ResolvePowerupTargets(comboData.targets); 
            });
        }
        // Propeller + Bomb
        else if (aIsPropeller && bIsBomb || aIsBomb && bIsPropeller)
        {
            Vector3 targetPos = new Vector3(comboData.secondary[0].xPosition, comboData.secondary[0].yPosition);
            int deliverID = coreIDA == PowerupIDs.Propeller ? coreIDB : coreIDA;

            VisualManager.Instance.PlayPropellerDeliveryCombo(visualA, visualB, centerPosition, targetPos, deliverID, () =>
            {
                ResolvePowerupTargets(comboData.targets);
            });
        }
        // Propeller + Disco
        else if (aIsPropeller && bIsDisco || aIsDisco && bIsPropeller)
        {
            GameObject actualDisco = aIsDisco ? visualA : visualB;
            GameObject actualPayload = aIsDisco ? visualB : visualA;
            int payloadCoreID = aIsDisco ? coreIDB : coreIDA;

            VisualManager.Instance.PlayUniversalDiscoCombo(actualDisco, actualPayload, payloadCoreID, comboData.primary, comboData.secondary, () =>
            {
                ResolvePowerupTargets(comboData.targets);
            });
        }
        // Bomb + Bomb
        else if (aIsBomb && bIsBomb)
        {
            VisualManager.Instance.PlayBigBombCombo(visualA, visualB, centerPosition, () =>
            {
                ResolvePowerupTargets(comboData.targets);
            });
        }
        // Bomb + Disco
        else if (aIsBomb && bIsDisco || aIsDisco && bIsBomb)
        {
            GameObject actualDisco = aIsDisco ? visualA : visualB;
            GameObject actualPayload = aIsDisco ? visualB : visualA;
            int payloadCoreID = aIsDisco ? coreIDB : coreIDA;

            VisualManager.Instance.PlayUniversalDiscoCombo(actualDisco, actualPayload, payloadCoreID, comboData.primary, comboData.secondary, () =>
            {
                ResolvePowerupTargets(comboData.targets);
            });
        }
        // Disco + disco
        else if (aIsDisco && bIsDisco)
        {
            VisualManager.Instance.PlayDiscoComboExplosion(visualA, visualB, centerPosition, () =>
            {
                ResolvePowerupTargets(comboData.targets); 
            });
        }
    }

    private void ConsumePowerupNode(GridNode node)
    {
        if (node == null || node.data == null || node.data.type != PieceType.Powerup) return;

        node.state = NodeState.Matching;
        node.data = null;
        node.state = NodeState.Idle;
    }

    private void ResolvePowerupTargets(HashSet<GridNode> targetsToDestroy)
    {
        if (targetsToDestroy == null || targetsToDestroy.Count == 0) return;

        _powerupChainDepth++;

        try
        {
            foreach (GridNode target in targetsToDestroy)
            {
                if (target == null) continue;

                _pendingGravityColumns.Add(target.xPosition);

                if (target.state == NodeState.Matching)
                    target.state = NodeState.Idle;

                HitNode(target);
            }
        }
        finally
        {
            _powerupChainDepth--;
            CheckAndTriggerPendingGravity();
        }
    }

    private void ResolvePowerupTargetsForRocket(GridNode centerNode, HashSet<GridNode> targetsToDestroy)
    {
         _powerupChainDepth++;

        float maxDelay = 0.4f;

        foreach (GridNode target in targetsToDestroy)
        {
            if (target == null) continue;

            // Always mark the column for gravity, even if this specific target
            // was already cleared (for example, the rocket center node).
            _pendingGravityColumns.Add(target.xPosition);

            int distance = Mathf.Abs(target.xPosition - centerNode.xPosition) + Mathf.Abs(target.yPosition - centerNode.yPosition);

            float delay = distance * 0.07f + 0.07f;
            if (delay > maxDelay) delay = maxDelay;

            Tween.Delay(delay, () =>
            {
                if (target == null) return;

                // Overlapping rockets can clear this node earlier; keep it Idle so gravity can process it.
                if (target.data == null)
                {
                    if (target.state == NodeState.Matching)
                        target.state = NodeState.Idle;
                    return;
                }

                if (target.state == NodeState.Matching) target.state = NodeState.Idle;
                
                HitNode(target);
            });
        }

        Tween.Delay(maxDelay + 0.05f, () =>
        {
            _powerupChainDepth--; 
            CheckAndTriggerPendingGravity();
        });
    }

    private void CheckAndTriggerPendingGravity()
    {
        if (_powerupChainDepth == 0)
        {
            foreach (int x in _pendingGravityColumns)
            {
                ProcessGravityForColumn(x);
            }
            _pendingGravityColumns.Clear();
        }
    }

    private void HitNode(GridNode node)
    {
        if (node == null || node.data == null) return;
        
        // Prevent double-hitting pieces that are already in motion or being resolved
        if (node.state == NodeState.Falling || node.state == NodeState.Matching || node.state == NodeState.Swapping) return; 

        if (node.data.type == PieceType.Powerup)
        {
            PieceData powerupData = node.data;
            int powerupCoreID = powerupData.coreID;

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
        if (clearActiveHint)
            VisualManager.Instance.ClearHint();
        
        if (hintRoutine != null)
            StopCoroutine(hintRoutine);
        
        hintRoutine = StartCoroutine(HintCoroutine());
    }

    private void ProcessMatch(Match matchToProcess)
    {
        List<PieceData> extractedData = new List<PieceData>();
        HashSet<int> affectedColumns = new HashSet<int>();
        GridNode centerNode = matchToProcess.center;

        foreach (GridNode node in matchToProcess.matchedNodes)
        {
            if (node.state == NodeState.Idle && node.data != null)
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
            VisualManager.Instance.PlayMatchPopEffect(extractedData);
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

        LevelManager.Instance.ProcessLevelObjective(collectedData.coreID);

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

        LevelManager.Instance.ProcessLevelObjective(obstacleData.coreID);

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

        if (LevelManager.Instance.CurrentMoves <= 0)
        {
            Debug.Log("No moves left.");
            return;
        }

        Vector2Int gridPosA = Utils.CalculateGridLocation(swipeStartPosition);
        Vector2Int gridPosB = Utils.CalculateGridLocation(swipeStartPosition + swipeDirection);

        if (!board.IsInPlayableBounds(gridPosA) || !board.IsInPlayableBounds(gridPosB)) 
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

            if (!LevelManager.Instance.TryConsumeMove())
            {
                Debug.Log("No moves left.");
                return;
            }

            if (aIsPowerup && bIsPowerup)
            {
                // Use swipe destination as combo center.
                ProcessPowerupCombo(nodeB, nodeA, nodeB, powerupCoreIDA, powerupCoreIDB, visualA, visualB);
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

    private IEnumerator DeadlockCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(DeadlockCheckInterval);

            bool isSettledNow = !isReshuffling && _powerupChainDepth == 0 && _pendingGravityColumns.Count == 0 && deadlockAndHintManager.IsBoardSettled(board);

            if (isSettledNow && !wasBoardSettled)
            {
                StartCoroutine(EndGameCoroutine());
            }

            wasBoardSettled = isSettledNow;

            if (!isSettledNow) continue;

            if (!deadlockAndHintManager.HasAnyPossibleMove(board))
            {
                TriggerBoardReshuffle();
            }
        }
    }

    private IEnumerator EndGameCoroutine()
    {
        yield return new WaitForSeconds(.5f);
        OnBoardSettled?.Invoke();
    }

    private void TriggerHint(HintData hintData)
    {
        if (hintData.nodeToSwap == null && (hintData.nodesToHint == null || hintData.nodesToHint.Count == 0)) return;
        VisualManager.Instance.HintMatch(hintData.nodesToHint, hintData.nodeToSwap);
    }

    private IEnumerator HintCoroutine()
    {
        while (true)
        {
            // Pause the loop for 6 seconds
            yield return new WaitForSeconds(HintingCheckInterval);

            // Only hint if the board is completely still and not shuffling
            if (!isReshuffling && deadlockAndHintManager.IsBoardSettled(board))
            {
                if (deadlockAndHintManager.TryGetBestHint(board, out bestMoveToHint))
                {
                    TriggerHint(bestMoveToHint);
                }
            }
        }
    }

}
