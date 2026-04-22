using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    // Grid Settings
    private int gridWidth;
    private int gridHeight;
    private int bufferSize;

    // Level details and goals.
    private int movesAllowed;
    private List<LevelObjective> levelGoals;

    private GridNode[,] gridData;

    private int _powerupChainDepth = 0;
    private HashSet<int> _pendingGravityColumns = new HashSet<int>();
    private bool _isReshuffling = false;
    private float _nextDeadlockCheckTime = 0f;
    private const float DeadlockCheckInterval = 0.25f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        InputManager.Instance.OnSwipeRequested += HandleSwap;
        InputManager.Instance.OnScreenTapped += ProcessPowerup;
    }

    private void OnDisable()
    {
        InputManager.Instance.OnSwipeRequested -= HandleSwap;
        InputManager.Instance.OnScreenTapped -= ProcessPowerup;
    }

    private void Start() 
    {
        InitializeGrid();
    }

    private void Update()
    {
        if (gridData == null || _isReshuffling) return;
        if (Time.time < _nextDeadlockCheckTime) return;
        _nextDeadlockCheckTime = Time.time + DeadlockCheckInterval;

        if (!IsBoardSettled()) return;

        if (!HasAnyPossibleMove())
        {
            ReshuffleMovablePieces();
        }
    }

    private void InitializeGrid()
    {
        // Instantiating the level data so changes made during the gameplay does not effect the data in the serialized object.
        LevelDataSO levelToLoad = Instantiate(LevelManager.Instance.levels[0]);
        gridHeight = levelToLoad.height;
        gridWidth  = levelToLoad.width;
        bufferSize = levelToLoad.bufferSize;

        movesAllowed = levelToLoad.movesAllowed;
        levelGoals = levelToLoad.levelGoals;

        gridData = new GridNode[gridWidth, gridHeight + bufferSize];

        GridNode node;

        // Initialize grid
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int flattenedNodeID = (y * gridWidth) + x;
                int id;

                CellSetup preSpawnCell = levelToLoad.gridLayout[flattenedNodeID];

                // Pre-defined spawns.
                if (preSpawnCell.preSpawnItemID != 0)
                {
                    id = preSpawnCell.preSpawnItemID;
                }
                else
                {
                    id = GetSafeRandomID(x, y);
                }

                if (preSpawnCell.isPlayable)
                {
                    node = new GridNode(x, y, id);

                    if (preSpawnCell.type == ItemType.Powerup)
                    {   
                        node.data.type = PieceType.Powerup;
                    }
                    else if (preSpawnCell.type == ItemType.Collectible)
                    {
                        node.data.type = PieceType.Collectible;
                    }
                    else if (preSpawnCell.type == ItemType.Obstacle)
                    {
                        node.data.type = PieceType.Obstacle;
                    }
                    
                    node.data.visualPiece = VisualManager.Instance.SpawnPiece(x, y, id);
                }
                else
                {
                    node = new GridNode(x, y, id, false);   
                }

                gridData[x, y] = node;
            }
        }

        // Initialize buffer
        for (int y = gridHeight; y < gridHeight + bufferSize; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int id = GetSafeRandomID(x, y);
                node = new GridNode(x, y, id);
                node.data.visualPiece = VisualManager.Instance.SpawnPiece(x, y, id);
                gridData[x, y] = node;
            }
        }
    }

    private int GetSafeRandomID(int x, int y)
    {
        HashSet<int> forbidden = new HashSet<int>();

        // Two left neighbors share the same ID → that ID would complete a horizontal match
        if (x >= 2 &&
            gridData[x - 1, y]?.data != null && gridData[x - 2, y]?.data != null &&
            gridData[x - 1, y].data.coreID == gridData[x - 2, y].data.coreID)
        {
            forbidden.Add(gridData[x - 1, y].data.coreID);
        }

        // Two lower neighbors share the same ID → that ID would complete a vertical match
        if (y >= 2 &&
            gridData[x, y - 1]?.data != null && gridData[x, y - 2]?.data != null &&
            gridData[x, y - 1].data.coreID == gridData[x, y - 2].data.coreID)
        {
            forbidden.Add(gridData[x, y - 1].data.coreID);
        }

        // 2x2 match
        if (x >= 1 && y >= 1 &&
            gridData[x - 1, y]?.data != null && gridData[x, y - 1]?.data != null && gridData[x - 1, y - 1]?.data != null &&
            gridData[x - 1, y].data.coreID == gridData[x, y - 1].data.coreID && gridData[x - 1, y].data.coreID == gridData[x - 1, y - 1].data.coreID)
        {
            forbidden.Add(gridData[x - 1, y - 1].data.coreID);
        }

        if (forbidden.Count == 0)
            return Random.Range(1, 6);

        List<int> allowed = new List<int>();
        for (int i = 1; i <= 5; i++)
        {
            if (!forbidden.Contains(i))
                allowed.Add(i);
        }

        if (allowed.Count == 0)
            return Random.Range(1, 6);

        return allowed[Random.Range(0, allowed.Count)];
    }

    private void ScanGrid(Vector2Int gridPosition)
    {
        ScanGrid(gridPosition.x, gridPosition.y);
    }

    private void ScanGrid(int x, int y)
    {
        if (!IsInBounds(x, y)) return;
        if (gridData[x, y].data == null) return;
        if (gridData[x, y].state == NodeState.Falling || gridData[x, y].state == NodeState.Matching) return;

        Vector2Int start = new Vector2Int(x, y);
        int coreID = gridData[x, y].data.coreID;

        // If its id is 100 or greater it is a powerup or collectible.
        if (coreID >= 100) return;

        List<Vector2Int> horizontalMatchPositions = ScanHorizontal(start, coreID);
        List<Vector2Int> verticalMatchPositions = ScanVertical(start, coreID);
        List<Vector2Int> propellerMatchPositions = ScanLocal2x2Match(start, coreID);

        Match match = GetMatch(horizontalMatchPositions, verticalMatchPositions, propellerMatchPositions, start);

        if (match != null)
        {
            ProcessMatch(match);
        }
    }

    private Match GetMatch(List<Vector2Int> horizontal, List<Vector2Int> vertical, List<Vector2Int> propeller, Vector2Int startPos)
    {
        List<GridNode> matchedNodes = new List<GridNode>();
        GridNode center = GetNodeAt(startPos);
        int coreID = center.data.coreID;

        if (horizontal.Count < 3 && vertical.Count < 3 && propeller.Count == 0) return null;

        matchedNodes.Add(center);

        if (horizontal.Count >= 5 || vertical.Count >= 5)
        {
            // Match-5+ horizontal or vertical
            foreach(Vector2Int gridPos in horizontal)
            {
                GridNode node = GetNodeAt(gridPos);

                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            foreach(Vector2Int gridPos in vertical)
            {
                GridNode node = GetNodeAt(gridPos);

                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            return new Match(matchedNodes, center, MatchShape.Match5Disco, coreID);
        }
        else if (horizontal.Count >= 3 && vertical.Count >= 3)
        {
            // Match-5+ T or L shaped
            foreach (Vector2Int gridPos in horizontal)
            {
                GridNode node = GetNodeAt(gridPos);
                
                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            foreach (Vector2Int gridPos in vertical)
            {
                GridNode node = GetNodeAt(gridPos);
                
                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            return new Match(matchedNodes, center, MatchShape.Match5Bomb, coreID);
        }
        else if (horizontal.Count == 4)
        {
            // Match-4 Horizontal
            foreach (Vector2Int gridPos in horizontal)
            {
                GridNode node = GetNodeAt(gridPos);
                
                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            return new Match(matchedNodes, center, MatchShape.Match4Horizontal, coreID);
        }
        else if (vertical.Count == 4)
        {
            // Match-4 Vertical
            foreach (Vector2Int gridPos in vertical)
            {
                GridNode node = GetNodeAt(gridPos);
                
                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            return new Match(matchedNodes, center, MatchShape.Match4Vertical, coreID);
        }
        else if (propeller.Count == 4)
        {
            // Match-4 Propeller
            foreach (Vector2Int gridPos in propeller)
            {
                GridNode node = GetNodeAt(gridPos);
                
                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            return new Match(matchedNodes, center, MatchShape.Match4Propeller, coreID);
        }
        else if (horizontal.Count == 3)
        {
            // Normal Match
            foreach(Vector2Int gridPos in horizontal)
            {
                GridNode node = GetNodeAt(gridPos);

                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);   
            }
                
            return new Match(matchedNodes, center, MatchShape.Match3, coreID);
        }
        else if (vertical.Count == 3)
        {
            // Normal Match
            foreach(Vector2Int gridPos in vertical)
            {
                GridNode node = GetNodeAt(gridPos);

                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);    
            }
            
            return new Match(matchedNodes, center, MatchShape.Match3, coreID);
        }

        // SHOULD NOT RETURN
        return null;
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

    private List<Vector2Int> ScanVertical(Vector2Int center, int id)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        positions.Add(center);

        // Walk up
        for (int y = center.y + 1; y < gridHeight; y++)
        {
            if (!gridData[center.x, y].isPlayable) break;

            GridNode next = gridData[center.x, y];
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(center.x, y));
        }

        // Walk down
        for (int y = center.y - 1; y >= 0; y--)
        {
            if (!gridData[center.x, y].isPlayable) break;

            GridNode next = gridData[center.x, y];
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(center.x, y));
        }

        // If there is a at least 3 same piece, return the list.
        if (positions.Count >= 3)
        {  
            return positions;
        }

        // If the list is smaller than 3, return empty list.
        return new List<Vector2Int>();
    }

    private List<Vector2Int> ScanLocal2x2Match(Vector2Int center, int id)
    {
        // center position can be top left, top right, bottom left, bottom right.
        Vector2Int[] potentialOrigins = new Vector2Int[]
        {
            new Vector2Int(center.x, center.y), // bottom left
            new Vector2Int(center.x - 1, center.y), // bottom right
            new Vector2Int(center.x, center.y - 1), // top left,
            new Vector2Int(center.x - 1, center.y -1) // top right
        };

        foreach(Vector2Int origin in potentialOrigins)
        {
            int xPos = origin.x;
            int yPos = origin.y;

            // A 2x2 box needs both origin and top-right corner in main-grid bounds.
            if (!IsInBounds(xPos, yPos) || !IsInBounds(xPos + 1, yPos + 1)) continue;

            // Grab the nodes of a 2x2 box.
            GridNode bottomLeft = GetNodeAt(xPos, yPos);
            GridNode bottomRight = GetNodeAt(xPos + 1, yPos);
            GridNode topLeft = GetNodeAt(xPos, yPos + 1);
            GridNode topRight = GetNodeAt(xPos + 1, yPos + 1);

            if (bottomLeft == null || bottomRight == null || topLeft == null || topRight == null)
                continue;

            if (!bottomLeft.isPlayable || !bottomRight.isPlayable || !topLeft.isPlayable || !topRight.isPlayable)
                continue;

            if (bottomLeft.data == null || bottomLeft.state != NodeState.Idle ||
                bottomRight.data == null || bottomRight.state != NodeState.Idle ||
                topLeft.data == null || topLeft.state != NodeState.Idle ||
                topRight.data == null || topRight.state != NodeState.Idle)
            {
                continue;
            }
            
            if (bottomLeft.data.coreID == id &&
                bottomRight.data.coreID == id &&
                topLeft.data.coreID == id &&
                topRight.data.coreID == id)
            {
                return new List<Vector2Int> 
                {new Vector2Int(xPos, yPos),
                 new Vector2Int(xPos + 1, yPos),
                 new Vector2Int(xPos, yPos + 1),
                 new Vector2Int(xPos + 1, yPos + 1)};
            }
        }

        // return an empty list.
        return new List<Vector2Int>();
    }

    private List<Vector2Int> ScanHorizontal(Vector2Int center, int id)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        positions.Add(center);

        // Walk right
        for (int x = center.x + 1; x < gridWidth; x++)
        {
            if (!gridData[x, center.y].isPlayable) break;

            GridNode next = gridData[x, center.y];
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(x, center.y));
        }

        // Walk left
        for (int x = center.x - 1; x >= 0; x--)
        {
            if (!gridData[x, center.y].isPlayable) break;

            GridNode next = gridData[x, center.y];
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(x, center.y));
        }

        // If there is a at least 3 same piece, return the list.
        if (positions.Count >= 3)
        {   
            return positions;
        }

        // If the list is smaller than 3, return empty list.
        return new List<Vector2Int>();
    }

    private void CheckAdjacentNodes(int x, int y)
    {
        GridNode top = GetNodeAt(x, y + 1);
        GridNode bottom = GetNodeAt(x, y - 1);
        GridNode right = GetNodeAt(x + 1, y);
        GridNode left = GetNodeAt(x - 1, y);

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

    private void ProcessGravityForColumn(int x)
    {
        int spawnHeight = gridHeight + bufferSize;
        int delayIndex = 0;
        float delayTime = 0.04f;

        for (int y = 0; y < gridHeight + bufferSize; y++)
        {
            GridNode emptyNode = GetNodeAt(x, y);

            if (!emptyNode.isPlayable) continue;

            if (emptyNode.data == null && emptyNode.state == NodeState.Idle)
            {
                bool pieceFound = false;

                for (int i = y + 1; i < gridHeight + bufferSize; i++)
                {
                    GridNode nodeAbove = GetNodeAt(x, i);

                    if (!nodeAbove.isPlayable) continue;

                    // If we find ANY piece above us (Idle, Falling, or Matching)
                    if (nodeAbove.data != null)
                    {
                        pieceFound = true; // Don't spawn from the sky, we have a piece above us!

                        if (nodeAbove.data.type == PieceType.Obstacle) break;

                        if (nodeAbove.state == NodeState.Idle)
                        {
                            emptyNode.data = nodeAbove.data;
                            nodeAbove.data = null;
                            emptyNode.state = NodeState.Falling;

                            // Callback
                            VisualManager.Instance.MovePiece(emptyNode.data.visualPiece, emptyNode.xPosition, emptyNode.yPosition, delayIndex * delayTime, () =>
                            {
                                emptyNode.state = NodeState.Idle;
                                ScanGrid(emptyNode.xPosition, emptyNode.yPosition);
                                ProcessGravityForColumn(emptyNode.xPosition);
                            });
                        }
                        
                        break;
                    }
                }

                if (!pieceFound)
                {
                    int newID = Random.Range(1, 6);
                    emptyNode.data = new PieceData(newID);
                    emptyNode.state = NodeState.Falling;

                    emptyNode.data.visualPiece = VisualManager.Instance.SpawnPiece(x, spawnHeight, newID);
                    spawnHeight++;

                    VisualManager.Instance.MovePiece(emptyNode.data.visualPiece, emptyNode.xPosition, emptyNode.yPosition, delayIndex * delayTime, () =>
                    {
                        emptyNode.state = NodeState.Idle;
                        ScanGrid(emptyNode.xPosition, emptyNode.yPosition);
                        ProcessGravityForColumn(emptyNode.xPosition);
                    });
                }

                delayIndex++;
            }
        }
    }

    private bool IsInBounds(Vector2Int gridPos)
    {
        return IsInBounds(gridPos.x, gridPos.y);
    }

    private bool IsInBounds(int x, int y)
    {
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
            return true;

        return false;
    }

    private void HandleSwap(Vector2 swipeStartPosition, Vector2 swipeDirection)
    {
        Vector2Int gridPosA = Utils.CalculateGridLocation(swipeStartPosition);
        Vector2Int gridPosB = Utils.CalculateGridLocation(swipeStartPosition + swipeDirection);

        if (!IsInBounds(gridPosA) || !IsInBounds(gridPosB)) 
        {
            Debug.Log("Out of bounds");
            return;
        }

        GridNode nodeA = GetNodeAt(gridPosA);
        GridNode nodeB = GetNodeAt(gridPosB);

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

            if (!aIsPowerup && !bIsPowerup && !HasMatch(gridPosA) && !HasMatch(gridPosB))
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

            // After the swap, slot A holds what was originally B, slot B holds what was originally A.
            // So slot A should be processed as a powerup if B was a powerup, and vice versa.
            if (bIsPowerup)
                ProcessPowerup(nodeA, nodeB.data?.coreID ?? -1);
            else
                ScanGrid(gridPosA);

            if (aIsPowerup)
                ProcessPowerup(nodeB, nodeA.data?.coreID ?? -1);
            else
                ScanGrid(gridPosB);
        });

    }

    private void ProcessPowerup(Vector2Int gridPosition)
    {
        // Check if it is in the grid dimensions.
        if (gridPosition.x < 0 || gridPosition.x >= gridWidth || gridPosition.y < 0 || gridPosition.y >= gridHeight)
            return;
    
        GridNode tappedNode = GetNodeAt(gridPosition);
        if (tappedNode.data == null) return;

        if (tappedNode.data.type == PieceType.Powerup)
            ProcessPowerup(tappedNode, Random.Range(1, 6));
    }

    private void ProcessPowerup(GridNode node, int targetCoreID = -1)
    {
        if (node.data == null) return;

        _powerupChainDepth++;

        switch (node.data.coreID)
        {
            case 100:
                ProcessRocketPowerup(node, Vector2.up);
                break;
            
            case 200:
                ProcessRocketPowerup(node, Vector2.right);
                break;

            case 300:
                ProcessBombPowerup(node);
                break;
            
            case 400:
                ProcessDiscoPowerup(node, targetCoreID);
                break;
            
            case 500:
                ProcessPropellerPowerup(node);
                break;
            
            default:
                Debug.Log("Something wrong with powerup processing.");
                break;
        }

        _powerupChainDepth--;

        if (_powerupChainDepth == 0)
        {
            foreach (int col in _pendingGravityColumns)
                ProcessGravityForColumn(col);
            _pendingGravityColumns.Clear();
        }
    }

    private void ProcessCollectible(GridNode node)
    {
        foreach(LevelObjective objective in levelGoals)
        {
            if (objective.itemID == node.data.coreID)
            {
                objective.targetCount--;
            }
        }
        VisualManager.Instance.DestroyPiece(node.data.visualPiece);
        node.data = null;
        ProcessGravityForColumn(node.xPosition);
    }

    private void ProcessObstacle(GridNode node)
    {
        VisualManager.Instance.DestroyPiece(node.data.visualPiece);
        node.data = null;
        ProcessGravityForColumn(node.xPosition);
    }

    private void HitNode(GridNode node)
    {
        if (node == null || node.data == null) return;

        if (node.data.type == PieceType.Powerup)
            ProcessPowerup(node);
        else if (node.data.type == PieceType.Obstacle)
            ProcessObstacle(node);
        else if (node.data.type == PieceType.Collectible)
            ProcessCollectible(node);
        else
        {
            VisualManager.Instance.DestroyPiece(node.data.visualPiece);
            node.data = null;
        }
    }

    private void ProcessRocketPowerup(GridNode centerNode, Vector2 direction)
    {
        // Destroy rocket first
        VisualManager.Instance.DestroyPiece(centerNode.data.visualPiece);
        centerNode.data = null;
        
        // Vertical Rocket
        if (direction == Vector2.up)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridNode nodeToDestroy = GetNodeAt(centerNode.xPosition, y);
                HitNode(nodeToDestroy);
            }

            _pendingGravityColumns.Add(centerNode.xPosition);
        }

        // Horizontal Rocket
        else if (direction == Vector2.right)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                GridNode nodeToDestroy = GetNodeAt(x, centerNode.yPosition);
                HitNode(nodeToDestroy);
            }

            for (int x = 0; x < gridWidth; x++)
                _pendingGravityColumns.Add(x);
        }
    }

    private void ProcessBombPowerup(GridNode centerNode)
    {
        HashSet<int> affectedColumns = new HashSet<int>();

        // Destroy the bomb itself first so adjacent bombs can't re-trigger it
        VisualManager.Instance.DestroyPiece(centerNode.data.visualPiece);
        centerNode.data = null;
        affectedColumns.Add(centerNode.xPosition);

        for (int y = centerNode.yPosition - 1; y <= centerNode.yPosition + 1; y++)
        {
            for (int x = centerNode.xPosition - 1; x <= centerNode.xPosition + 1; x++)
            {
                if (!IsInBounds(x, y)) continue;

                GridNode nodeToDestroy = gridData[x, y];
                if (nodeToDestroy.data == null) continue;

                affectedColumns.Add(x);
                HitNode(nodeToDestroy);
            }
        }

        foreach (int x in affectedColumns)
            _pendingGravityColumns.Add(x);
    }

    private void ProcessDiscoPowerup(GridNode centerNode, int targetCoreID)
    {
        if (targetCoreID == -1)  return;

        HashSet<int> affectedColumns = new HashSet<int>();

        // Destroy the disco ball itself first
        VisualManager.Instance.DestroyPiece(centerNode.data.visualPiece);
        centerNode.data = null;
        affectedColumns.Add(centerNode.xPosition);

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                GridNode node = gridData[x, y];
                if (node.data == null || node.data.coreID != targetCoreID) continue;

                affectedColumns.Add(x);
                HitNode(node);
            }
        }

        foreach (int x in affectedColumns)
            _pendingGravityColumns.Add(x);
    }

    private void ProcessPropellerPowerup(GridNode centerNode)
    {
        HashSet<int> affectedColumns = new HashSet<int>();

        // Destroy the propeller itself first
        VisualManager.Instance.DestroyPiece(centerNode.data.visualPiece);
        centerNode.data = null;
        affectedColumns.Add(centerNode.xPosition);

        // High priority, collectible.
        GridNode collectibleNode = GetRandomCollectible();

        // Medium prioriy, obstacle.
        GridNode obstacleNode = GetRandomObstacle();

        // Low priority, normal piece.
        GridNode normalNode = GetRandomPiece();

        if (collectibleNode != null)
        {
            HitNode(collectibleNode);
            affectedColumns.Add(collectibleNode.xPosition);
        }
        else if (obstacleNode != null)
        {
            HitNode(obstacleNode);
            affectedColumns.Add(obstacleNode.xPosition);
        }
        else
        {
            HitNode(normalNode);
            affectedColumns.Add(normalNode.xPosition);
        }

        foreach (int x in affectedColumns)
        {
            ProcessGravityForColumn(x);
        }
    }

    private GridNode GetRandomCollectible()
    {
        List<GridNode> allCollectibles = new List<GridNode>();

        foreach (GridNode node in gridData)
        {
            if (node.data != null &&  node.data.type == PieceType.Collectible)
                allCollectibles.Add(node);
        }

        // If empty, return null.
        if (allCollectibles.Count == 0)
        {
            return null;
        }

        GridNode randomNode = allCollectibles[Random.Range(0, allCollectibles.Count)];

        while (randomNode.state != NodeState.Idle)
        {
            randomNode = allCollectibles[Random.Range(0, allCollectibles.Count)];
        }

        return randomNode;
    }

    private GridNode GetRandomObstacle()
    {
        List<GridNode> allObstacles = new List<GridNode>();

        foreach (GridNode node in gridData)
        {
            if (node.data != null && node.data.type == PieceType.Obstacle)
                allObstacles.Add(node);
        }

        // If empty, return null.
        if (allObstacles.Count == 0)
            return null;

        GridNode randomNode = allObstacles[Random.Range(0, allObstacles.Count)];

        while (randomNode.state != NodeState.Idle)
        {
            randomNode = allObstacles[Random.Range(0, allObstacles.Count)];
        }
        
        return randomNode;
    }

    private GridNode GetRandomPiece()
    {
        List<GridNode> allPieces = new List<GridNode>();

        foreach (GridNode node in gridData)
        {
            if (node.data != null && node.data.type == PieceType.Normal)
                allPieces.Add(node);
        }

        // If empty, return null.
        if (allPieces.Count == 0)
            return null;

        GridNode randomNode = allPieces[Random.Range(0, allPieces.Count)];

        while (randomNode.state != NodeState.Idle)
        {
            randomNode = allPieces[Random.Range(0, allPieces.Count)];
        }
        
        return randomNode;
    }

    private bool HasMatch(Vector2Int pos)
    {
        return HasMatch(pos.x, pos.y);
    }

    private bool HasMatch(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        if (gridData[x, y].data == null) return false;
        if (gridData[x, y].state == NodeState.Falling) return false;

        int coreID = gridData[x, y].data.coreID;
        Vector2Int center = new Vector2Int(x, y);

        return ScanHorizontal(center, coreID).Count >= 3 || ScanVertical(center, coreID).Count >= 3 || ScanLocal2x2Match(center, coreID).Count == 4;
    }

    private bool IsBoardSettled()
    {
        if (_powerupChainDepth > 0) return false;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                GridNode node = gridData[x, y];
                if (node == null || !node.isPlayable) continue;

                if (node.data == null) return false;
                if (node.state != NodeState.Idle) return false;
            }
        }

        return true;
    }

    private bool HasAnyPossibleMove()
    {
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                GridNode node = gridData[x, y];
                if (node == null || !node.isPlayable || node.data == null) continue;

                // Tapping any powerup is already a valid move in this game.
                if (node.data.type == PieceType.Powerup) return true;

                if (x + 1 < gridWidth && CanSwapCreateMatch(x, y, x + 1, y)) return true;
                if (y + 1 < gridHeight && CanSwapCreateMatch(x, y, x, y + 1)) return true;
            }
        }

        return false;
    }

    private bool CanSwapCreateMatch(int ax, int ay, int bx, int by)
    {
        GridNode a = gridData[ax, ay];
        GridNode b = gridData[bx, by];

        if (a == null || b == null || !a.isPlayable || !b.isPlayable) return false;
        if (a.data == null || b.data == null) return false;

        if (a.data.type == PieceType.Obstacle || b.data.type == PieceType.Obstacle) return false;
        if (a.data.type == PieceType.Collectible || b.data.type == PieceType.Collectible) return false;

        // A swap with a powerup is always a move in the current rules.
        if (a.data.type == PieceType.Powerup || b.data.type == PieceType.Powerup) return true;

        PieceData temp = a.data;
        a.data = b.data;
        b.data = temp;

        bool createsMatch = IsMatchAt(ax, ay) || IsMatchAt(bx, by);

        temp = a.data;
        a.data = b.data;
        b.data = temp;

        return createsMatch;
    }

    private bool IsMatchAt(int x, int y)
    {
        GridNode node = gridData[x, y];
        if (node == null || node.data == null) return false;
        if (node.data.coreID >= 100) return false;

        int id = node.data.coreID;
        Vector2Int center = new Vector2Int(x, y);

        int horizontal = 1 + CountDirection(x, y, 1, 0, id) + CountDirection(x, y, -1, 0, id); // 1 is for the start pos
        if (horizontal >= 3) return true;

        int vertical = 1 + CountDirection(x, y, 0, 1, id) + CountDirection(x, y, 0, -1, id); // 1 is for the start pos
        if (vertical >= 3) return true;

        return ScanLocal2x2Match(center, id).Count == 4;

    }

    private int CountDirection(int x, int y, int dx, int dy, int id)
    {
        int count = 0;
        int cx = x + dx;
        int cy = y + dy;

        while (IsInBounds(cx, cy))
        {
            GridNode node = gridData[cx, cy];
            if (node == null || !node.isPlayable || node.data == null || node.data.coreID != id) break;

            count++;
            cx += dx;
            cy += dy;
        }

        return count;
    }

    private void ReshuffleMovablePieces()
    {
        _isReshuffling = true;

        List<GridNode> movableNodes = new List<GridNode>();
        List<PieceData> pool = new List<PieceData>();

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                GridNode node = gridData[x, y];
                if (node == null || !node.isPlayable || node.data == null) continue;

                // Keep obstacles and collectibles locked to their original cells.
                if (node.data.type == PieceType.Obstacle || node.data.type == PieceType.Collectible) continue;

                movableNodes.Add(node);
                pool.Add(node.data);
            }
        }

        if (movableNodes.Count <= 1)
        {
            _isReshuffling = false;
            return;
        }

        bool validShuffle = false;
        int attempts = 0;
        const int maxAttempts = 80;

        while (!validShuffle && attempts < maxAttempts)
        {
            attempts++;
            Shuffle(pool);

            for (int i = 0; i < movableNodes.Count; i++)
            {
                movableNodes[i].data = pool[i];
            }

            validShuffle = !BoardHasImmediateMatch() && HasAnyPossibleMove();
        }

        int finished = 0;
        int total = movableNodes.Count;

        foreach (GridNode node in movableNodes)
        {
            node.state = NodeState.Swapping;
            VisualManager.Instance.MovePiece(node.data.visualPiece, node.xPosition, node.yPosition, 0f, () =>
            {
                node.state = NodeState.Idle;
                finished++;
                if (finished == total)
                {
                    _isReshuffling = false;
                    _nextDeadlockCheckTime = Time.time + DeadlockCheckInterval;
                }
            });
        }
    }

    private bool BoardHasImmediateMatch()
    {
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (IsMatchAt(x, y)) return true;
            }
        }

        return false;
    }

    private void Shuffle(List<PieceData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            PieceData temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private GridNode GetNodeAt(Vector2Int index)
    {
        return GetNodeAt(index.x, index.y);
    }

    private GridNode GetNodeAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight + bufferSize) return null;
        return gridData[x, y];
    }

}
