using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MatchScanner 
{
    private static readonly Vector2Int[] Local2x2OriginOffsets = new Vector2Int[]
    {
        new Vector2Int(0, 0), // bottom left
        new Vector2Int(-1, 0), // bottom right
        new Vector2Int(0, -1), // top left,
        new Vector2Int(-1, -1) // top right
    };

    private bool TryGetScannableCenter(BoardState board, int x, int y, out Vector2Int center, out int coreID)
    {
        center = default;
        coreID = -1;

        if (!board.IsInPlayableBounds(x, y)) return false;

        GridNode node = board.GetNodeAt(x, y);
        if (node == null || node.data == null) return false;
        if (node.state == NodeState.Falling || node.state == NodeState.Matching) return false;

        coreID = node.data.coreID;
        if (coreID >= 100) return false;

        center = new Vector2Int(x, y);
        return true;
    }

    public Match GetMatchAt(BoardState board, int x, int y)
    {
        if (!TryGetScannableCenter(board, x, y, out Vector2Int center, out int coreID))
        return null;

        List<Vector2Int> horizontal = ScanHorizontal(board, center, coreID);
        List<Vector2Int> vertical = ScanVertical(board, center, coreID);
        List<Vector2Int> propeller = ScanLocal2x2Match(board, center, coreID);

        Match match = GetMatch(board, horizontal, vertical, propeller, center);
        return match;
    }
    
    public bool HasMatchAt(BoardState board, int x, int y)
    {
        if (!TryGetScannableCenter(board, x, y, out Vector2Int center, out int coreID))
            return false;

        return CountHorizontal(board, center, coreID) >= 3 || CountVertical(board, center, coreID) >= 3 || HasLocal2x2Match(board, center, coreID);
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
            if (next == null || !next.isPlayable ) break;
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(center.x, y));
        }

        // Walk down
        for (int y = center.y - 1; y >= 0; y--)
        {
            GridNode next = board.GetNodeAt(center.x, y);
            if (next == null || !next.isPlayable) break;
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

    private int CountVertical(BoardState board, Vector2Int center, int id)
    {
        int count = 1;

        // Walk up
        for (int y = center.y + 1; y < board.Height; y++)
        {
            GridNode next = board.GetNodeAt(center.x, y);
            if (next == null || !next.isPlayable) break;
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            count++;
        }

        // Walk down
        for (int y = center.y - 1; y >= 0; y--)
        {
            GridNode next = board.GetNodeAt(center.x, y);
            if (next == null || !next.isPlayable) break;
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            count++;
        }

        return count;
    }

    private List<Vector2Int> ScanHorizontal(BoardState board, Vector2Int center, int id)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        positions.Add(center);

        // Walk right
        for (int x = center.x + 1; x < board.Width; x++)
        {
            GridNode next = board.GetNodeAt(x, center.y);
            if (next == null || !next.isPlayable) break;
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            positions.Add(new Vector2Int(x, center.y));
        }

        // Walk left
        for (int x = center.x - 1; x >= 0; x--)
        {
            GridNode next = board.GetNodeAt(x, center.y);
            if (next == null || !next.isPlayable) break;
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

    // 
    private int CountHorizontal(BoardState board, Vector2Int center, int id)
    {
        int count = 1;

        // Walk right
        for (int x = center.x + 1; x < board.Width; x++)
        {
            GridNode next = board.GetNodeAt(x, center.y);
            if (next == null || !next.isPlayable) break;
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            count++;
        }

        // Walk left
        for (int x = center.x - 1; x >= 0; x--)
        {
            GridNode next = board.GetNodeAt(x, center.y);
            if (next == null || !next.isPlayable) break;
            if (next.state != NodeState.Idle || next.data == null || next.data.coreID != id) break;

            count++;
        }

        return count;
    }


    private List<Vector2Int> ScanLocal2x2Match(BoardState board, Vector2Int center, int id)
    {
        foreach(Vector2Int offset in Local2x2OriginOffsets)
        {
            int xPos = center.x + offset.x;
            int yPos = center.y + offset.y;

            // Grab the nodes of a 2x2 box.
            GridNode bottomLeft = board.GetNodeAt(xPos, yPos);
            GridNode bottomRight = board.GetNodeAt(xPos + 1, yPos);
            GridNode topLeft = board.GetNodeAt(xPos, yPos + 1);
            GridNode topRight = board.GetNodeAt(xPos + 1, yPos + 1);

            if (bottomLeft == null || bottomRight == null || topLeft == null || topRight == null)
                continue;
            
            if (!board.IsInPlayableBounds(xPos, yPos) || !board.IsInPlayableBounds(xPos + 1, yPos) ||
                !board.IsInPlayableBounds(xPos, yPos + 1) || !board.IsInPlayableBounds(xPos + 1, yPos + 1))
            {
                continue;
            }

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

    private bool HasLocal2x2Match(BoardState board, Vector2Int center, int id)
    {
        foreach(Vector2Int offset in Local2x2OriginOffsets)
        {
            int xPos = center.x + offset.x;
            int yPos = center.y + offset.y;

            // Grab the nodes of a 2x2 box.
            GridNode bottomLeft = board.GetNodeAt(xPos, yPos);
            GridNode bottomRight = board.GetNodeAt(xPos + 1, yPos);
            GridNode topLeft = board.GetNodeAt(xPos, yPos + 1);
            GridNode topRight = board.GetNodeAt(xPos + 1, yPos + 1);

            if (bottomLeft == null || bottomRight == null || topLeft == null || topRight == null)
                continue;
            
            if (!board.IsInPlayableBounds(xPos, yPos) || !board.IsInPlayableBounds(xPos + 1, yPos) ||
                !board.IsInPlayableBounds(xPos, yPos + 1) || !board.IsInPlayableBounds(xPos + 1, yPos + 1))
            {
                continue;
            }


            if (!bottomLeft.isPlayable || !bottomRight.isPlayable || !topLeft.isPlayable || !topRight.isPlayable)
                continue;

            if (bottomLeft.data == null || bottomRight.data == null || topLeft.data == null || topRight.data == null)
                continue;
            
            if (bottomLeft.state != NodeState.Idle || bottomRight.state != NodeState.Idle || topLeft.state != NodeState.Idle || topRight.state != NodeState.Idle)
                continue;
            
            if (bottomLeft.data.coreID != id || bottomRight.data.coreID != id || topLeft.data.coreID != id || topRight.data.coreID != id)
                continue;

            return true;
        }

        return false;
    }

    private Match GetMatch(BoardState board, List<Vector2Int> horizontal, List<Vector2Int> vertical, List<Vector2Int> propeller, Vector2Int startPos)
    {
        HashSet<GridNode> matchedNodes = new HashSet<GridNode>();
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
                matchedNodes.Add(node);
            }

            foreach(Vector2Int gridPos in vertical)
            {
                GridNode node = board.GetNodeAt(gridPos);
                matchedNodes.Add(node);
            }

            return new Match(matchedNodes.ToList(), center, MatchShape.Match5Disco, coreID);
        }
        else if (horizontal.Count >= 3 && vertical.Count >= 3)
        {
            // Match-5+ T or L shaped
            foreach (Vector2Int gridPos in horizontal)
            {
                GridNode node = board.GetNodeAt(gridPos);
                matchedNodes.Add(node);
            }

            foreach (Vector2Int gridPos in vertical)
            {
                GridNode node = board.GetNodeAt(gridPos);
                matchedNodes.Add(node);
            }

            return new Match(matchedNodes.ToList(), center, MatchShape.Match5Bomb, coreID);
        }
        else if (horizontal.Count == 4)
        {
            // Match-4 Horizontal
            foreach (Vector2Int gridPos in horizontal)
            {
                GridNode node = board.GetNodeAt(gridPos);
                matchedNodes.Add(node);
            }

            return new Match(matchedNodes.ToList(), center, MatchShape.Match4Horizontal, coreID);
        }
        else if (vertical.Count == 4)
        {
            // Match-4 Vertical
            foreach (Vector2Int gridPos in vertical)
            {
                GridNode node = board.GetNodeAt(gridPos);
                matchedNodes.Add(node);
            }

            return new Match(matchedNodes.ToList(), center, MatchShape.Match4Vertical, coreID);
        }
        else if (propeller.Count == 4)
        {
            // Match-4 Propeller
            foreach (Vector2Int gridPos in propeller)
            {
                GridNode node = board.GetNodeAt(gridPos);
                matchedNodes.Add(node);
            }

            return new Match(matchedNodes.ToList(), center, MatchShape.Match4Propeller, coreID);
        }
        else if (horizontal.Count == 3)
        {
            // Normal Match
            foreach(Vector2Int gridPos in horizontal)
            {
                GridNode node = board.GetNodeAt(gridPos);
                matchedNodes.Add(node);   
            }
                
            return new Match(matchedNodes.ToList(), center, MatchShape.Match3, coreID);
        }
        else if (vertical.Count == 3)
        {
            // Normal Match
            foreach(Vector2Int gridPos in vertical)
            {
                GridNode node = board.GetNodeAt(gridPos);
                matchedNodes.Add(node);    
            }
            
            return new Match(matchedNodes.ToList(), center, MatchShape.Match3, coreID);
        }

        // SHOULD NOT RETURN
        return null;
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
