using System.Collections.Generic;
using UnityEngine;

public class BoardFactory
{
    public static BoardState CreateBoard(LevelDataSO levelData)
    {
        BoardState board = new BoardState(levelData.width, levelData.height, levelData.bufferSize);
        // 1. Initialize playable grid
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int flattenedNodeID = (y * board.Width) + x;
                CellSetup preSpawnCell = levelData.gridLayout[flattenedNodeID];

                // Determine ID (either pre-spawned or safely randomized)
                int id = preSpawnCell.preSpawnItemID != 0 ? preSpawnCell.preSpawnItemID : GetSafeRandomID(board, x, y);

                GridNode node;

                if (preSpawnCell.isPlayable)
                {
                    node = new GridNode(x, y, id);

                    // Set Piece Type
                    if (preSpawnCell.type == ItemType.Powerup)
                    {
                        int id2 = preSpawnCell.preSpawnItemID;
                        if (id2 != 100 && id2 != 200 && id2 != 300 && id2 != 400 && id2 != 500)
                            UnityEngine.Debug.LogError($"Powerup at ({x},{y}) has invalid preSpawnItemID={id2}. Must be 100/200/300/400/500.");
                        node.data.type = PieceType.Powerup;
                    }
                    else if (preSpawnCell.type == ItemType.Collectible)
                        node.data.type = PieceType.Collectible;
                    else if (preSpawnCell.type == ItemType.Obstacle)
                        node.data.type = PieceType.Obstacle;
                }
                else
                {
                    // Create unplayable node
                    node = new GridNode(x, y, id, false);
                }

                board.SetNodeAt(x, y, node);
            }
        }

        // 2. Initialize Buffer Zone (Gravity spawn area)
        for (int y = board.Height; y < board.Height + board.BufferSize; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int id = GetSafeRandomID(board, x, y);
                GridNode node = new GridNode(x, y, id);
                board.SetNodeAt(x, y, node);
            }
        }

        return board;
    }

    // Made private and static!
    private static int GetSafeRandomID(BoardState board, int x, int y)
    {
        HashSet<int> forbidden = new HashSet<int>();

        // Check horizontal matches
        if (x >= 2 &&
            board.GetNodeDataAt(x - 1, y) != null && board.GetNodeDataAt(x - 2, y) != null &&
            board.GetNodeDataAt(x - 1, y).coreID == board.GetNodeDataAt(x - 2, y).coreID)
        {
            forbidden.Add(board.GetNodeDataAt(x - 1, y).coreID);
        }

        // Check vertical matches
        if (y >= 2 &&
            board.GetNodeDataAt(x, y - 1) != null && board.GetNodeDataAt(x, y - 2) != null &&
            board.GetNodeDataAt(x, y - 1).coreID == board.GetNodeDataAt(x, y - 2).coreID)
        {
            forbidden.Add(board.GetNodeDataAt(x, y - 1).coreID);
        }

        // Check 2x2 matches
        if (x >= 1 && y >= 1 &&
            board.GetNodeDataAt(x - 1, y) != null && board.GetNodeDataAt(x, y - 1) != null && board.GetNodeDataAt(x - 1, y - 1) != null &&
            board.GetNodeDataAt(x - 1, y).coreID == board.GetNodeDataAt(x, y - 1).coreID && board.GetNodeDataAt(x - 1, y).coreID == board.GetNodeDataAt(x - 1, y - 1).coreID)
        {
            forbidden.Add(board.GetNodeDataAt(x - 1, y - 1).coreID);
        }

        // Fallback or Random generation
        if (forbidden.Count == 0) return Random.Range(1, 6);

        List<int> allowed = new List<int>();
        for (int i = 1; i <= 5; i++)
        {
            if (!forbidden.Contains(i)) allowed.Add(i);
        }

        if (allowed.Count == 0) return Random.Range(1, 6);

        return allowed[Random.Range(0, allowed.Count)];
    }
}
