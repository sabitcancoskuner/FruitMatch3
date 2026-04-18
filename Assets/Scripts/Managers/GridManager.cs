using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    // Grid Settings
    private int gridWidth;
    private int gridHeight;
    private int bufferSize;

    private GridNode[,] gridData;

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

    private void InitializeGrid()
    {
        LevelDataSO levelToLoad = LevelManager.Instance.levels[0];
        gridHeight = levelToLoad.height;
        gridWidth  = levelToLoad.width;
        bufferSize = levelToLoad.bufferSize;

        gridData = new GridNode[gridWidth, gridHeight + bufferSize];

        GridNode node;

        // Initialize grid
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int flattenedNodeID = (y * gridWidth) + x;
                int id;

                // Pre-defined spawns.
                if (levelToLoad.gridLayout[flattenedNodeID].preSpawnItemID != 0)
                {
                    id = levelToLoad.gridLayout[flattenedNodeID].preSpawnItemID;
                }
                else
                {
                    id = GetSafeRandomID(x, y);
                }

                if (levelToLoad.gridLayout[flattenedNodeID].isPlayable)
                {
                    node = new GridNode(x, y, id);
                    node.data.visualPiece = VisualManager.Instance.SpawnPiece(x, y, id - 1);
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
                node.data.visualPiece = VisualManager.Instance.SpawnPiece(x, y, id - 1);
                gridData[x, y] = node;
            }
        }
    }

    private int GetSafeRandomID(int x, int y)
    {
        List<int> forbidden = new List<int>();

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

        List<Vector2Int> horizontalMatchPositions = ScanHorizontal(start, coreID);
        List<Vector2Int> verticalMatchPositions = ScanVertical(start, coreID);

        Match match = GetMatchType(horizontalMatchPositions, verticalMatchPositions, start);

        if (match != null)
        {
            ProcessMatch(match);
        }
    }

    private Match GetMatchType(List<Vector2Int> horizontal, List<Vector2Int> vertical, Vector2Int startPos)
    {
        List<GridNode> matchedNodes = new List<GridNode>();
        GridNode center = GetNodeAt(startPos);
        int coreID = center.data.coreID;

        if (horizontal.Count < 3 && vertical.Count < 3) return null;

        foreach (Vector2Int gridPos in horizontal)
        {
            GridNode node = GetNodeAt(gridPos);
            
            if (!matchedNodes.Contains(node))
                matchedNodes.Add(GetNodeAt(gridPos));
        }

        foreach (Vector2Int gridPos in vertical)
        {
            GridNode node = GetNodeAt(gridPos);
            
            if (!matchedNodes.Contains(node))
                matchedNodes.Add(GetNodeAt(gridPos));
        }

        if (horizontal.Count >= 5 || vertical.Count >= 5)
        {
            // Match-5 horizontal or vertical
            return new Match(matchedNodes, center, MatchShape.Match5Disco, coreID);
        }
        else if (horizontal.Count >= 3 && vertical.Count >= 3)
        {
            // Match-5 T or L shaped
            return new Match(matchedNodes, center, MatchShape.Match5Bomb, coreID);
        }
        else if (horizontal.Count == 4)
        {
            // Match-4 Horizontal
            return new Match(matchedNodes, center, MatchShape.Match4Horizontal, coreID);
        }
        else if (vertical.Count == 4)
        {
            // Match-4 Vertical 
            return new Match(matchedNodes, center, MatchShape.Match4Vertical, coreID);
        }
        
        // Normal Match
        return new Match(matchedNodes, center, MatchShape.Match3, coreID);
        
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

                affectedColumns.Add(node.xPosition);
            }
        }

        VisualManager.Instance.DestroyPieces(extractedData);

        // If it is a powerup, update grid data.
        if (matchToProcess.shape != MatchShape.Match3)
        {
            PieceData newData = new PieceData(1000, Utils.GetPieceType(matchToProcess.shape));
            centerNode.data = newData;
            centerNode.data.visualPiece = VisualManager.Instance.SpawnPowerup(centerNode.data.type, centerNode.xPosition, centerNode.yPosition);
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

        // If there is a at least 2 same piece, add the center and return the list.
        if (positions.Count >= 2)
        {
            positions.Add(center);
            
            return positions;
        }

        // If the list is smaller than 3, return empty list.
        return new List<Vector2Int>();
    }

    private List<Vector2Int> ScanHorizontal(Vector2Int center, int id)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

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

        // If there is a at least 2 same piece, add the center and return the list.
        if (positions.Count >= 2)
        {
            positions.Add(center);
            
            return positions;
        }

        // If the list is smaller than 3, return empty list.
        return new List<Vector2Int>();
    }

    private void ProcessGravityForColumn(int x)
    {
        int spawnHeight = gridHeight + bufferSize;
        int delayIndex = 0;
        float delayTime = 0.04f;

        for (int y = 0; y < gridHeight + bufferSize; y++)
        {
            GridNode node = GetNodeAt(x, y);

            if (!node.isPlayable) continue;

            if (node.data == null && node.state == NodeState.Idle)
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

                        // Only pull it down if it's Idle. If it's already falling, we just wait for it to land!
                        if (nodeAbove.state == NodeState.Idle)
                        {
                            // Move piece down
                            node.data = nodeAbove.data;
                            nodeAbove.data = null;
                            node.state = NodeState.Falling;

                            // Callback
                            VisualManager.Instance.MovePiece(node.data.visualPiece, node.xPosition, node.yPosition, delayIndex * delayTime, () =>
                            {
                                node.state = NodeState.Idle;
                                ScanGrid(node.xPosition, node.yPosition);
                                ProcessGravityForColumn(node.xPosition);
                            });
                        }
                        
                        break;
                    }
                }

                if (!pieceFound)
                {
                    int newID = Random.Range(1, 6);
                    node.data = new PieceData(newID);
                    node.state = NodeState.Falling;

                    node.data.visualPiece = VisualManager.Instance.SpawnPiece(x, spawnHeight, newID - 1);
                    spawnHeight++;

                    VisualManager.Instance.MovePiece(node.data.visualPiece, node.xPosition, node.yPosition, delayIndex * delayTime, () =>
                    {
                        node.state = NodeState.Idle;
                        ScanGrid(node.xPosition, node.yPosition);
                        ProcessGravityForColumn(node.xPosition);
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

        // Lock both nodes to prevent concurrent swaps
        nodeA.state = NodeState.Swapping;
        nodeB.state = NodeState.Swapping;

        // Capture visual pieces and logical positions BEFORE data swap
        GameObject visualA = nodeA.data.visualPiece;
        GameObject visualB = nodeB.data.visualPiece;
        Vector3 logicalPosA = new Vector3(gridPosA.x, gridPosA.y, 0);
        Vector3 logicalPosB = new Vector3(gridPosB.x, gridPosB.y, 0);

        // Check piece type, if they are powerups or not
        bool aIsPowerup = nodeA.data.type != PieceType.Normal;
        bool bIsPowerup = nodeB.data.type != PieceType.Normal;

        // Swap the data
        PieceData temp = nodeA.data;
        nodeA.data = nodeB.data;
        nodeB.data = temp;

        // visualA (from posA) moves to posB; visualB (from posB) moves to posA
        VisualManager.Instance.SwapPieces(visualA, visualB, logicalPosB, logicalPosA, () =>
        {
            if (!aIsPowerup && !bIsPowerup && !HasMatch(gridPosA) && !HasMatch(gridPosB))
            {
                // No match formed — swap data back and animate the reversal
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

            nodeA.state = NodeState.Idle;
            nodeB.state = NodeState.Idle;

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
    
        GridNode tapppedNode = GetNodeAt(gridPosition);

        if (tapppedNode.data.type != PieceType.Normal)
            ProcessPowerup(tapppedNode, tapppedNode.data?.coreID ?? -1);
    }

    private void ProcessPowerup(GridNode node, int targetCoreID = -1)
    {
        switch (node.data.type)
        {
            case PieceType.VerticalRocket:
                ProcessRocketPowerup(node, Vector2.up);
                break;
            
            case PieceType.HorizontalRocket:
                ProcessRocketPowerup(node, Vector2.right);
                break;

            case PieceType.Bomb:
                ProcessBombPowerup(node);
                break;
            
            case PieceType.Disco:
                ProcessDiscoPowerup(node, targetCoreID);
                break;
            
            default:
                Debug.Log("Something wrong with powerup processing.");
                break;
        }
    }

    private void ProcessRocketPowerup(GridNode centerNode, Vector2 direction)
    {
        // Vertical Rocket
        if (direction == Vector2.up)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GridNode nodeToDestroy = GetNodeAt(centerNode.xPosition, y);
                if (nodeToDestroy.data == null) continue;

                VisualManager.Instance.DestroyPiece(nodeToDestroy.data.visualPiece);
                nodeToDestroy.data = null;
            }

            ProcessGravityForColumn(centerNode.xPosition);
        }

        // Horizontal Rocket
        else if (direction == Vector2.right)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                GridNode nodeToDestroy = GetNodeAt(x, centerNode.yPosition);
                if (nodeToDestroy.data == null) continue;

                VisualManager.Instance.DestroyPiece(nodeToDestroy.data.visualPiece);
                nodeToDestroy.data = null;
            }

            for (int x = 0; x < gridWidth; x++)
            {
                ProcessGravityForColumn(x);
            }
        }
    }

    private void ProcessBombPowerup(GridNode centerNode)
    {
        HashSet<int> affectedColumns = new HashSet<int>();

        for (int y = centerNode.yPosition - 1; y <= centerNode.yPosition + 1; y++)
        {
            for (int x = centerNode.xPosition - 1; x <= centerNode.xPosition + 1; x++)
            {
                if (!IsInBounds(x, y)) continue;

                GridNode nodeToDestroy = gridData[x, y];
                if (nodeToDestroy.data == null) continue;

                VisualManager.Instance.DestroyPiece(nodeToDestroy.data.visualPiece);
                nodeToDestroy.data = null;

                affectedColumns.Add(x);
            }
        }

        foreach (int x in affectedColumns)
        {
            ProcessGravityForColumn(x);
        }
    }

    private void ProcessDiscoPowerup(GridNode centerNode, int targetCoreID)
    {
        if (targetCoreID == -1) return;

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

                VisualManager.Instance.DestroyPiece(node.data.visualPiece);
                node.data = null;

                affectedColumns.Add(x);
            }
        }

        foreach (int x in affectedColumns)
        {
            ProcessGravityForColumn(x);
        }
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

        return ScanHorizontal(center, coreID).Count >= 3 || ScanVertical(center, coreID).Count >= 3;
    }

    private GridNode GetNodeAt(Vector2Int index)
    {
        return GetNodeAt(index.x, index.y);
    }

    private GridNode GetNodeAt(int x, int y)
    {
        return gridData[x, y];
    }

}
