using System.Collections.Generic;
using UnityEngine;

public class MatchScanner 
{
    public Match GetMatchAt(BoardState board, int x, int y)
    {
        GridNode node = board.GetNodeAt(x, y);
        PieceData nodeData = board.GetNodeDataAt(x, y);
        if (node == null) return null;
        if (node.state == NodeState.Falling || node.state == NodeState.Matching) return null;
        if (nodeData == null) return null;

        Vector2Int start = new Vector2Int(x, y);
        int coreID = nodeData.coreID;

        // If its id is 100 or greater it is a powerup or collectible.
        if (coreID >= 100) return null;

        List<Vector2Int> horizontalMatchPositions = ScanHorizontal(board, start, coreID);
        List<Vector2Int> verticalMatchPositions = ScanVertical(board, start, coreID);
        List<Vector2Int> propellerMatchPositions = ScanLocal2x2Match(board, start, coreID);

        Match match = GetMatch(board, horizontalMatchPositions, verticalMatchPositions, propellerMatchPositions, start);

        return match;
    }
    
    public bool HasMatchAt(BoardState board, int x, int y)
    {
        GridNode node = board.GetNodeAt(x, y);
        if (node == null) return false;
        if (node.data == null) return false;
        if (node.state == NodeState.Falling) return false;

        int coreID = node.data.coreID;
        if (coreID >= 100) return false;

        Vector2Int center = new Vector2Int(x, y);

        return ScanHorizontal(board, center, coreID).Count >= 3 || ScanVertical(board, center, coreID).Count >= 3 || ScanLocal2x2Match(board, center, coreID).Count == 4;
    }
    
    public bool HasMatchAt(BoardState board, Vector2Int gridPos)
    {
        return HasMatchAt(board, gridPos.x, gridPos.y);
    }

    private List<Vector2Int> ScanVertical(BoardState board, Vector2Int center, int id)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        positions.Add(center);

        // Walk up
        for (int y = center.y + 1; y < board.Height; y++)
        {
            GridNode next = board.GetNodeAt(center.x, y);
            if (!next.isPlayable) break;

            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(center.x, y));
        }

        // Walk down
        for (int y = center.y - 1; y >= 0; y--)
        {
            GridNode next = board.GetNodeAt(center.x, y);
            if (!next.isPlayable) break;

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

    private List<Vector2Int> ScanHorizontal(BoardState board, Vector2Int center, int id)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        positions.Add(center);

        // Walk right
        for (int x = center.x + 1; x < board.Width; x++)
        {
            GridNode next = board.GetNodeAt(x, center.y);
            if (!next.isPlayable) break;

            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(x, center.y));
        }

        // Walk left
        for (int x = center.x - 1; x >= 0; x--)
        {
            GridNode next = board.GetNodeAt(x, center.y);
            if (!next.isPlayable) break;

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

    private List<Vector2Int> ScanLocal2x2Match(BoardState board, Vector2Int center, int id)
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

            // Grab the nodes of a 2x2 box.
            GridNode bottomLeft = board.GetNodeAt(xPos, yPos);
            GridNode bottomRight = board.GetNodeAt(xPos + 1, yPos);
            GridNode topLeft = board.GetNodeAt(xPos, yPos + 1);
            GridNode topRight = board.GetNodeAt(xPos + 1, yPos + 1);

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

    private Match GetMatch(BoardState board, List<Vector2Int> horizontal, List<Vector2Int> vertical, List<Vector2Int> propeller, Vector2Int startPos)
    {
        List<GridNode> matchedNodes = new List<GridNode>();
        GridNode center = board.GetNodeAt(startPos);
        int coreID = center.data.coreID;

        if (horizontal.Count < 3 && vertical.Count < 3 && propeller.Count == 0) return null;

        matchedNodes.Add(center);

        if (horizontal.Count >= 5 || vertical.Count >= 5)
        {
            // Match-5+ horizontal or vertical
            foreach(Vector2Int gridPos in horizontal)
            {
                GridNode node = board.GetNodeAt(gridPos);

                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            foreach(Vector2Int gridPos in vertical)
            {
                GridNode node = board.GetNodeAt(gridPos);

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
                GridNode node = board.GetNodeAt(gridPos);
                
                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);
            }

            foreach (Vector2Int gridPos in vertical)
            {
                GridNode node = board.GetNodeAt(gridPos);
                
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
                GridNode node = board.GetNodeAt(gridPos);
                
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
                GridNode node = board.GetNodeAt(gridPos);
                
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
                GridNode node = board.GetNodeAt(gridPos);
                
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
                GridNode node = board.GetNodeAt(gridPos);

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
                GridNode node = board.GetNodeAt(gridPos);

                if (!matchedNodes.Contains(node))
                    matchedNodes.Add(node);    
            }
            
            return new Match(matchedNodes, center, MatchShape.Match3, coreID);
        }

        // SHOULD NOT RETURN
        return null;
    }

    private int CountDirection(BoardState board, int x, int y, int dx, int dy, int id)
    {
        int count = 0;
        int cx = x + dx;
        int cy = y + dy;

        while (board.IsInBounds(cx, cy))
        {
            GridNode node = board.GetNodeAt(cx, cy);
            if (node == null || !node.isPlayable || node.data == null || node.data.coreID != id) break;

            count++;
            cx += dx;
            cy += dy;
        }

        return count;
    }

    public bool BoardHasImmediateMatch(BoardState board)
    {
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (HasMatchAt(board, x, y)) return true;
            }
        }

        return false;
    }
}
