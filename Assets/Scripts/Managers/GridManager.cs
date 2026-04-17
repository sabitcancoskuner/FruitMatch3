using System.Collections.Generic;
using Unity.VisualScripting;
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
    }

    private void OnDisable()
    {
        InputManager.Instance.OnSwipeRequested -= HandleSwap;
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
                    id = Random.Range(1, 6);
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
                int id = Random.Range(1, 6);
                node = new GridNode(x, y, id);
                node.data.visualPiece = VisualManager.Instance.SpawnPiece(x, y, id - 1);
                gridData[x, y] = node;
            }
        }
    }

    private void ScanGrid(Vector2Int gridPosition)
    {
        ScanGrid(gridPosition.x, gridPosition.y);
    }

    private void ScanGrid(int x, int y)
    {
        if (!IsInBounds(x, y)) return;
        if (gridData[x, y].data == null) return;
        if (gridData[x, y].state == NodeState.Falling) return;

        List<Vector2Int> horizontalMatchPositions;
        List<Vector2Int> verticalMatchPositions;

        Vector2Int center = new Vector2Int(x, y);
        int coreID = gridData[x, y].data.coreID;

        horizontalMatchPositions = ScanHorizontal(center, coreID);
        verticalMatchPositions = ScanVertical(center, coreID);

        if (horizontalMatchPositions.Count >= 3)
        {
            ProcessMatch(horizontalMatchPositions);      
        }

        if (verticalMatchPositions.Count >= 3)
        {
            ProcessMatch(verticalMatchPositions);
        }
    }

    private void ProcessMatch(List<Vector2Int> positions)
    {
        List<PieceData> extractedData = new List<PieceData>();
        HashSet<int> affectedColumns = new HashSet<int>();

        foreach (Vector2Int pos in positions)
        {
            GridNode node = GetNodeAt(pos);
            if (node.state != NodeState.Matching && node.data != null)
            {
                node.state = NodeState.Matching;
                extractedData.Add(node.data);
                node.data = null;

                affectedColumns.Add(pos.x);
            }
        }

        VisualManager.Instance.DestroyPieces(extractedData);

        foreach (Vector2Int pos in positions)
        {
            GridNode node = GetNodeAt(pos);
            node.state = NodeState.Idle;
        }

        foreach (int x in affectedColumns)
        {
            ProcessGravityForColumn(x);
        }
    }

    private List<Vector2Int> ScanVertical(Vector2Int center, int id)
    {
        int count = 1;
        List<Vector2Int> positions = new List<Vector2Int>();
        positions.Add(center);

        // Walk up
        for (int y = center.y + 1; y < gridHeight; y++)
        {
            if (!gridData[center.x, y].isPlayable) break;

            GridNode next = gridData[center.x, y];
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(center.x, y));
            count++;
        }

        // Walk down
        for (int y = center.y - 1; y >= 0; y--)
        {
            if (!gridData[center.x, y].isPlayable) break;

            GridNode next = gridData[center.x, y];
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(center.x, y));
            count++;
        }

        return positions;
    }

    private List<Vector2Int> ScanHorizontal(Vector2Int center, int id)
    {
        int count = 1;
        List<Vector2Int> positions = new List<Vector2Int>();
        positions.Add(center);

        // Walk right
        for (int x = center.x + 1; x < gridWidth; x++)
        {
            if (!gridData[x, center.y].isPlayable) break;

            GridNode next = gridData[x, center.y];
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(x, center.y));
            count++;
        }

        // Walk left
        for (int x = center.x - 1; x >= 0; x--)
        {
            if (!gridData[x, center.y].isPlayable) break;

            GridNode next = gridData[x, center.y];
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(x, center.y));
            count++;
        }

        return positions;
    }

    private void ProcessGravityForColumn(int x)
    {
        int spawnHeight = gridHeight + bufferSize;

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
                            VisualManager.Instance.MovePiece(node.data.visualPiece, node.xPosition, node.yPosition, () =>
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

                    VisualManager.Instance.MovePiece(node.data.visualPiece, node.xPosition, node.yPosition, () =>
                    {
                        node.state = NodeState.Idle;
                        ScanGrid(node.xPosition, node.yPosition);
                        ProcessGravityForColumn(node.xPosition);
                    });
                }
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
        if (nodeA.state == NodeState.Falling || nodeA.state == NodeState.Matching ||
            nodeB.state == NodeState.Falling || nodeB.state == NodeState.Matching)
        {
            Debug.Log("Can not swap matching or falling nodes.");
            return;
        }

        // Swap the data
        PieceData temp = nodeA.data;
        nodeA.data = nodeB.data;
        nodeB.data = temp;

        VisualManager.Instance.SwapPieces(nodeA.data.visualPiece, nodeB.data.visualPiece, () =>
        {
            ScanGrid(gridPosA);
            ScanGrid(gridPosB);
        });

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
