using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [SerializeField] private int gridWidth;
    [SerializeField] private int gridHeight;
    [SerializeField] private int bufferSize;
    [SerializeField] private GameObject idleObject;
    [SerializeField] private GameObject matchingObject;

    // TEMP LIST
    private List<GameObject> activeObjects;

    private GridNode[,] gridData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        gridData = new GridNode[gridWidth, gridHeight + bufferSize];
        activeObjects = new List<GameObject>();
    }

    private void OnEnable()
    {
        InputManager.Instance.OnSwapRequested += HandleSwap;
    }

    private void OnDisable()
    {
        InputManager.Instance.OnSwapRequested -= HandleSwap;
    }

    private void Start() 
    {
        InitializeGrid();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                ScanGrid(x, y);
            }
        }
        PaintGrid();
    }

    private void InitializeGrid()
    {
        GridNode node;
        // y
        for (int i = 0; i < gridHeight + bufferSize; i++)
        {
            // x
            for (int j = 0; j < gridWidth; j++)
            {
                int id = Random.Range(0, 5);

                // Hardcoded unplayable grid nodes.
                if (j == 5 && i == 3)
                {
                    node = new GridNode(j, i, 999, false); 
                }
                else if (j == 2 && i == 7)
                {
                    node = new GridNode(j, i, 999, false); 
                }
                else
                {
                    node = new GridNode(j, i, id);   
                }

                gridData[j, i] = node;
            }
        }
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

    private void HandleSwap(Vector2 posA, Vector2 posB)
    {
        Vector2Int gridPosA = Utils.CalculateGridLocation(posA);
        Vector2Int gridPosB = Utils.CalculateGridLocation(posB);

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
        PaintGrid();
    }

    // TEMP METHOD FOR VISUAlS
    private void PaintGrid()
    {
        if (activeObjects.Count != 0)
        {
            foreach (GameObject obj in activeObjects)
            {
                Destroy(obj);
            }
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (!gridData[x, y].isPlayable) continue;

                if (gridData[x, y].state == NodeState.Idle)
                {
                    GameObject obj =Instantiate(idleObject, new Vector2(x, y), Quaternion.identity);
                    activeObjects.Add(obj);
                }
                else if (gridData[x, y].state == NodeState.Matching)
                {
                    GameObject obj = Instantiate(matchingObject, new Vector2(x, y), Quaternion.identity);
                    activeObjects.Add(obj);
                }
            }
        }
    }
}
