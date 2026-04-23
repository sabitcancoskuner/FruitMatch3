using UnityEngine;

public class BoardState
{
    // Grid Details
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int BufferSize { get; private set; }

    private GridNode[,] gridData;

    public BoardState(int width, int height, int bufferSize)
    {
        this.Width = width;
        this.Height = height;
        this.BufferSize = bufferSize;

        gridData = new GridNode[Width, Height + BufferSize];
    }

    public GridNode GetNodeAt(int x, int y)
    {
        if (!IsInBounds(x, y)) return null;

        return gridData[x, y];
    }
    
    public GridNode GetNodeAt(Vector2Int pos)
    {
        return GetNodeAt(pos.x, pos.y);
    }

    public void SetNodeAt(int x, int y, GridNode newNode)
    {
        if (!IsInBounds(x, y)) return;

        gridData[x, y] = newNode;
    }

    public PieceData GetNodeDataAt(int x, int y)
    {
        GridNode node = GetNodeAt(x, y);

        if (node == null) return null;

        return node.data;
    }

    public PieceData GetNodeDataAt(Vector2Int pos)
    {
        return GetNodeDataAt(pos.x, pos.y);
    }

    public void SetNodeDataAt(int x, int y, PieceData newData)
    {
        GridNode node = GetNodeAt(x, y);

        if (node == null) return;

        node.data = newData;
    }

    public bool IsInBounds(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height + BufferSize)
        {
            return false;   
        }

        return true;
    }

    public bool IsInBounds(Vector2Int pos)
    {
        return IsInBounds(pos.x, pos.y);
    }


}
