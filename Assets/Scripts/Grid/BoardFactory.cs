using System.Collections.Generic;
using UnityEngine;

public class BoardFactory
{
    private const int MinNormalID = 1;
    private const int MaxNormalID = 5;

    public static BoardState CreateBoard(LevelDataSO levelData)
    {
        BoardState board = new BoardState(levelData.width, levelData.height, levelData.bufferSize);

        // Initialize pre-spawns.

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int flattenedNodeID = (y * board.Width) + x;
                CellSetup preSpawnCell = levelData.gridLayout[flattenedNodeID];

                GridNode node;
                
                if (preSpawnCell.preSpawnItemID != 0 && preSpawnCell.isPlayable)
                {
                    node = new GridNode(x, y, preSpawnCell.preSpawnItemID);

                    // Set Piece Type
                    if (preSpawnCell.type == ItemType.Powerup)
                    {
                        int id2 = preSpawnCell.preSpawnItemID;
                        if (id2 != 100 && id2 != 200 && id2 != 300 && id2 != 400 && id2 != 500)
                            Debug.LogError($"Powerup at ({x},{y}) has invalid preSpawnItemID={id2}. Must be 100/200/300/400/500.");
                        node.data.type = PieceType.Powerup;
                    }
                    else if (preSpawnCell.type == ItemType.Collectible)
                        node.data.type = PieceType.Collectible;
                    else if (preSpawnCell.type == ItemType.Obstacle)
                        node.data.type = PieceType.Obstacle;

                    board.SetNodeAt(x, y, node);
                }
            }
        }

        ValidatePreSpawnLayout(board);

        // Initialize remaining grid
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int flattenedNodeID = (y * board.Width) + x;
                CellSetup preSpawnCell = levelData.gridLayout[flattenedNodeID];

                GridNode node;

                if (preSpawnCell.preSpawnItemID == 0 && preSpawnCell.isPlayable)
                {
                    int id = GetSafeRandomID(board, x, y);
                    node = new GridNode(x, y, id);

                    board.SetNodeAt(x, y, node);
                }
                else if (!preSpawnCell.isPlayable)
                {
                    // Create unplayable node
                    node = new GridNode(x, y, 0, false);
                    board.SetNodeAt(x, y, node);
                }
            }
        }

        // Initialize Buffer Zone (Gravity spawn area)
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

    private static int GetSafeRandomID(BoardState board, int x, int y)
    {
        List<int> allowed = new List<int>();

        for (int candidate = MinNormalID; candidate <= MaxNormalID; candidate++)
        {
            if (IsCandidateSafe(board, x, y, candidate))
                allowed.Add(candidate);
        }

        if (allowed.Count == 0)
            return Random.Range(MinNormalID, MaxNormalID + 1);

        return allowed[Random.Range(0, allowed.Count)];
    }

    private static bool IsCandidateSafe(BoardState board, int x, int y, int candidateID)
    {
        return !CreatesHorizontalMatch(board, x, y, candidateID) &&
               !CreatesVerticalMatch(board, x, y, candidateID) &&
               !CreatesSquareMatch(board, x, y, candidateID);
    }

    private static bool CreatesHorizontalMatch(BoardState board, int x, int y, int candidateID)
    {
        // candidate can be left, center, or right of a 3-streak
        return IsTripletMatch(board, x - 2, y, x - 1, y, x, y, x, y, candidateID) ||
               IsTripletMatch(board, x - 1, y, x, y, x + 1, y, x, y, candidateID) ||
               IsTripletMatch(board, x, y, x + 1, y, x + 2, y, x, y, candidateID);
    }

    private static bool CreatesVerticalMatch(BoardState board, int x, int y, int candidateID)
    {
        // candidate can be bottom, center, or top of a 3-streak
        return IsTripletMatch(board, x, y - 2, x, y - 1, x, y, x, y, candidateID) ||
               IsTripletMatch(board, x, y - 1, x, y, x, y + 1, x, y, candidateID) ||
               IsTripletMatch(board, x, y, x, y + 1, x, y + 2, x, y, candidateID);
    }

    private static bool CreatesSquareMatch(BoardState board, int x, int y, int candidateID)
    {
        // candidate can be any corner of 2x2
        return IsSquareMatch(board, x, y, x, y, candidateID) ||
               IsSquareMatch(board, x - 1, y, x, y, candidateID) ||
               IsSquareMatch(board, x, y - 1, x, y, candidateID) ||
               IsSquareMatch(board, x - 1, y - 1, x, y, candidateID);
    }

    private static bool IsTripletMatch(BoardState board, int ax, int ay, int bx, int by, int cx, int cy, int candidateX, int candidateY, int candidateID)
    {
        int idA = GetNormalCoreID(board, ax, ay, candidateX, candidateY, candidateID);
        int idB = GetNormalCoreID(board, bx, by, candidateX, candidateY, candidateID);
        int idC = GetNormalCoreID(board, cx, cy, candidateX, candidateY, candidateID);

        return idA == candidateID && idB == candidateID && idC == candidateID;
    }

    private static bool IsSquareMatch(BoardState board, int originX, int originY, int candidateX, int candidateY, int candidateID)
    {
        int idBottomLeft = GetNormalCoreID(board, originX, originY, candidateX, candidateY, candidateID);
        int idBottomRight = GetNormalCoreID(board, originX + 1, originY, candidateX, candidateY, candidateID);
        int idTopLeft = GetNormalCoreID(board, originX, originY + 1, candidateX, candidateY, candidateID);
        int idTopRight = GetNormalCoreID(board, originX + 1, originY + 1, candidateX, candidateY, candidateID);

        return idBottomLeft == candidateID &&
               idBottomRight == candidateID &&
               idTopLeft == candidateID &&
               idTopRight == candidateID;
    }

    private static int GetNormalCoreID(BoardState board, int x, int y, int candidateX, int candidateY, int candidateID)
    {
        if (!board.IsInPlayableBounds(x, y))
            return -1;

        if (x == candidateX && y == candidateY)
            return candidateID;

        GridNode node = board.GetNodeAt(x, y);
        if (node == null || !node.isPlayable || node.data == null)
            return -1;

        int id = node.data.coreID;
        if (id < MinNormalID || id > MaxNormalID)
            return -1;

        return id;
    }

    private static void ValidatePreSpawnLayout(BoardState board)
    {
        ValidateHorizontalPreSpawnMatches(board);
        ValidateVerticalPreSpawnMatches(board);
        ValidateSquarePreSpawnMatches(board);
    }

    private static void ValidateHorizontalPreSpawnMatches(BoardState board)
    {
        for (int y = 0; y < board.Height; y++)
        {
            int x = 0;
            while (x < board.Width)
            {
                int id = GetPlacedNormalCoreID(board, x, y);
                if (id == -1)
                {
                    x++;
                    continue;
                }

                int startX = x;
                while (x < board.Width && GetPlacedNormalCoreID(board, x, y) == id)
                {
                    x++;
                }

                int runLength = x - startX;
                if (runLength >= 3)
                {
                    Debug.LogError($"Invalid pre-spawn horizontal match at y={y}, start=({startX},{y}), length={runLength}, coreID={id}.");
                }
            }
        }
    }

    private static void ValidateVerticalPreSpawnMatches(BoardState board)
    {
        for (int x = 0; x < board.Width; x++)
        {
            int y = 0;
            while (y < board.Height)
            {
                int id = GetPlacedNormalCoreID(board, x, y);
                if (id == -1)
                {
                    y++;
                    continue;
                }

                int startY = y;
                while (y < board.Height && GetPlacedNormalCoreID(board, x, y) == id)
                {
                    y++;
                }

                int runLength = y - startY;
                if (runLength >= 3)
                {
                    Debug.LogError($"Invalid pre-spawn vertical match at x={x}, start=({x},{startY}), length={runLength}, coreID={id}.");
                }
            }
        }
    }

    private static void ValidateSquarePreSpawnMatches(BoardState board)
    {
        for (int y = 0; y < board.Height - 1; y++)
        {
            for (int x = 0; x < board.Width - 1; x++)
            {
                int bottomLeft = GetPlacedNormalCoreID(board, x, y);
                if (bottomLeft == -1) continue;

                int bottomRight = GetPlacedNormalCoreID(board, x + 1, y);
                int topLeft = GetPlacedNormalCoreID(board, x, y + 1);
                int topRight = GetPlacedNormalCoreID(board, x + 1, y + 1);

                if (bottomLeft == bottomRight && bottomLeft == topLeft && bottomLeft == topRight)
                {
                    Debug.LogError($"Invalid pre-spawn 2x2 match at origin=({x},{y}), coreID={bottomLeft}.");
                }
            }
        }
    }

    private static int GetPlacedNormalCoreID(BoardState board, int x, int y)
    {
        if (!board.IsInPlayableBounds(x, y))
            return -1;

        GridNode node = board.GetNodeAt(x, y);
        if (node == null || !node.isPlayable || node.data == null)
            return -1;

        int id = node.data.coreID;
        if (id < MinNormalID || id > MaxNormalID)
            return -1;

        return id;
    }
}
