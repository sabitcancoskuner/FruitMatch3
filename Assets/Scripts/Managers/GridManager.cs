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
    }

    private void ScanGrid(int x, int y)
    {
        if (!IsInBounds(x, y)) return;

        List<Vector2Int> horizontalMatchPositions;
        List<Vector2Int> verticalMatchPositions;

        Vector2Int center = new Vector2Int(x, y);
        int coreID = gridData[x, y].data.coreID;

        horizontalMatchPositions = ScanHorizontal(center, coreID);
        verticalMatchPositions = ScanVertical(center, coreID);

        if (horizontalMatchPositions != null)
        {
            foreach(Vector2Int pos in horizontalMatchPositions)
            {
                gridData[pos.x, pos.y].state = NodeState.Matching;
            }
        }

        if (verticalMatchPositions != null)
        {
            foreach(Vector2Int pos in verticalMatchPositions)
            {
                gridData[pos.x, pos.y].state = NodeState.Matching;
            }
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
            if (next.state != NodeState.Idle || next.data.coreID != id) break;

            positions.Add(new Vector2Int(center.x, y));
            count++;
        }

        // Walk down
        for (int y = center.y - 1; y >= 0; y--)
        {
            if (!gridData[center.x, y].isPlayable) break;

            GridNode next = gridData[center.x, y];
            if (next.state != NodeState.Idle || next.data.coreID != id) break;

            positions.Add(new Vector2Int(center.x, y));
            count++;
        }

        if (count >= 3)
        {
            return positions;
        }

        return null;
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
            if (next.state != NodeState.Idle || next.data.coreID != id) break;

            positions.Add(new Vector2Int(x, center.y));
            count++;
        }

        // Walk left
        for (int x = center.x - 1; x >= 0; x--)
        {
            if (!gridData[x, center.y].isPlayable) break;

            GridNode next = gridData[x, center.y];
            if (next.state != NodeState.Idle || next.data.coreID != id) break;

            positions.Add(new Vector2Int(x, center.y));
            count++;
        }

        if (count >= 3)
        {
            return positions;
        }

        return null;
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
        if (!gridData[gridPosA.x, gridPosA.y].isPlayable || !gridData[gridPosB.x, gridPosB.y].isPlayable) 
        {
            Debug.Log("Can not swap unplayable nodes");
            return;
        }

        // Swap the data
        PieceData temp = gridData[gridPosA.x, gridPosA.y].data;
        gridData[gridPosA.x, gridPosA.y].data = gridData[gridPosB.x, gridPosB.y].data;
        gridData[gridPosB.x, gridPosB.y].data = temp;

        Debug.Log("Swapping Pos A: (" + gridPosA.x + ", " + gridPosA.y + ") with Pos B: (" + gridPosB.x + ", " + gridPosB.y + ")");
        VisualManager.Instance.SwapPieces(gridData[gridPosA.x, gridPosA.y].data.visualPiece, 
                                          gridData[gridPosB.x, gridPosB.y].data.visualPiece);

        // Reset states before rescanning
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                gridData[x, y].state = NodeState.Idle;
            }
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                ScanGrid(x, y);
            }
        }
    }

}
