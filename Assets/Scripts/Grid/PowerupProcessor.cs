using System.Collections.Generic;
using UnityEngine;

public class PowerupProcessor
{
    public HashSet<GridNode> ProcessPowerup(BoardState board, GridNode node, int targetCoreID = -1, int forcedPowerupCoreID = -1)
    {
        HashSet<GridNode> targets = new HashSet<GridNode>();
        if (node == null) return targets;

        int powerupCoreID = forcedPowerupCoreID;
        if (powerupCoreID == -1)
        {
            if (node.data == null) return targets;
            powerupCoreID = node.data.coreID;
        }   

        switch (powerupCoreID)
        {
            case 100:
                targets.UnionWith(GetRocketTargets(board, node, Vector2.up));
                break;
            
            case 200:
                targets.UnionWith(GetRocketTargets(board, node, Vector2.right));
                break;

            case 300:
                targets.UnionWith(GetBombTargets(board, node));
                break;
            
            case 400:
                targets.UnionWith(GetDiscoTargets(board, node, targetCoreID));
                break;
            
            case 500:
                targets.UnionWith(ReturnPropellerTargets(board, node));
                break;
            
            default:
                Debug.Log("Something wrong with powerup processing.");
                break;
        }

        return targets;
    }

    private HashSet<GridNode> GetDiscoTargets(BoardState board, GridNode centerNode, int targetCoreID)
    {
        HashSet<GridNode> targets = new HashSet<GridNode>();
        targets.Add(centerNode);
        if (targetCoreID == -1)
        {
            int mostFound = 0;
            int id = 0;

            for (int i = 0; i < 6; i++)
            {
                int total = 0;
                for (int y = 0; y < board.Height; y++)
                {
                    for (int x = 0; x < board.Width; x++)
                    {   
                        GridNode node = board.GetNodeAt(x, y);
                        if (node == null || node.state != NodeState.Idle) continue;
                        
                        PieceData nodeData = board.GetNodeDataAt(x, y);
                        if (nodeData == null || nodeData.coreID != i) continue;
                        
                        total++;
                    }
                }
                if (total > mostFound)
                {
                    mostFound = total;
                    id = i;
                }
            }

            targetCoreID = id;
        }

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {   
                GridNode node = board.GetNodeAt(x, y);
                if (node == null || node.state != NodeState.Idle) continue;

                PieceData nodeData = board.GetNodeDataAt(x, y);
                if (nodeData == null || nodeData.coreID != targetCoreID) continue;
                
                targets.Add(node);
            }
        }

        return targets;
    }

    private HashSet<GridNode> GetBombTargets(BoardState board, GridNode centerNode)
    {
        HashSet<GridNode> targets = new HashSet<GridNode>();

        for (int y = centerNode.yPosition - 2; y <= centerNode.yPosition + 2; y++)
        {
            for (int x = centerNode.xPosition - 2; x <= centerNode.xPosition + 2; x++)
            {
                GridNode node = board.GetNodeAt(x, y);
                PieceData nodeData = board.GetNodeDataAt(x, y);
                if (node == null || nodeData == null) continue;

                targets.Add(node);
            }
        }
        
        return targets;
    }

    private HashSet<GridNode> GetRocketTargets(BoardState board, GridNode centerNode, Vector2 direction)
    {
        HashSet<GridNode> targets = new HashSet<GridNode>();
        
        // Vertical Rocket
        if (direction == Vector2.up)
        {
            for (int y = 0; y < board.Height; y++)
            {
                GridNode node = board.GetNodeAt(centerNode.xPosition, y);
                if (node == null) continue;

                targets.Add(node);
            }
        }

        // Horizontal Rocket
        else if (direction == Vector2.right)
        {
            for (int x = 0; x < board.Width; x++)
            {
                GridNode node = board.GetNodeAt(x, centerNode.yPosition);
                if (node == null) continue;

                targets.Add(node);
            }
        }

        return targets;
    }

    private HashSet<GridNode> ReturnPropellerTargets(BoardState board, GridNode centerNode)
    {
        HashSet<GridNode> targets = new HashSet<GridNode>();
        targets.Add(centerNode);
        targets.Add(GetPropellerDestination(board));

        return targets;
    }

    private GridNode GetRandomCollectible(BoardState board)
    {
        List<GridNode> allCollectibles = new List<GridNode>();

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                GridNode node = board.GetNodeAt(x, y);
                PieceData nodeData = board.GetNodeDataAt(x, y);
                if (node.state == NodeState.Idle && nodeData != null && nodeData.type == PieceType.Collectible)
                    allCollectibles.Add(node);
            }
        }

        // If empty, return null.
        if (allCollectibles.Count == 0)
        {
            return null;
        }

        GridNode randomNode = allCollectibles[Random.Range(0, allCollectibles.Count)];

        return randomNode;
    }

    private GridNode GetRandomObstacle(BoardState board)
    {
        List<GridNode> allObstacles = new List<GridNode>();

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                GridNode node = board.GetNodeAt(x, y);
                PieceData nodeData = board.GetNodeDataAt(x, y);
                if (node.state == NodeState.Idle && nodeData != null && nodeData.type == PieceType.Obstacle)
                    allObstacles.Add(node);
            }
        }

        // If empty, return null.
        if (allObstacles.Count == 0)
            return null;

        GridNode randomNode = allObstacles[Random.Range(0, allObstacles.Count)];
        
        return randomNode;
    }

    private GridNode GetRandomPiece(BoardState board)
    {
        List<GridNode> allPieces = new List<GridNode>();

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                GridNode node = board.GetNodeAt(x, y);
                PieceData nodeData = board.GetNodeDataAt(x, y);
                if (node.state == NodeState.Idle && nodeData != null && nodeData.type == PieceType.Normal)
                    allPieces.Add(node);
            }
        }

        // If empty, return null.
        if (allPieces.Count == 0)
            return null;

        GridNode randomNode = allPieces[Random.Range(0, allPieces.Count)];

        return randomNode;
    }

    private GridNode GetPropellerDestination(BoardState board)
    {
        GridNode c = GetRandomCollectible(board);
        if (c != null) return c;
        GridNode o = GetRandomObstacle(board);
        if (o != null) return o;
        return GetRandomPiece(board);
    }

    private int GetRandomNormalCoreID(BoardState board, int exclude = -1)
    {
        List<int> colors = new List<int>();
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                PieceData d = board.GetNodeDataAt(x, y);
                if (d == null || d.type != PieceType.Normal || d.coreID == exclude) continue;
                if (!colors.Contains(d.coreID)) colors.Add(d.coreID);
            }
        return colors.Count == 0 ? -1 : colors[Random.Range(0, colors.Count)];
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    public HashSet<GridNode> ProcessCombo(BoardState board, GridNode center, int coreIDA, int coreIDB)
    {
        HashSet<GridNode> targets = new HashSet<GridNode>();

        bool aIsRocket = coreIDA == 100 || coreIDA == 200;
        bool bIsRocket = coreIDB == 100 || coreIDB == 200;
        bool aIsPropeller = coreIDA == 500;
        bool bIsPropeller = coreIDB == 500;
        bool aIsBomb = coreIDA == 300;
        bool bIsBomb = coreIDB == 300;
        bool aIsDisco = coreIDA == 400;
        bool bIsDisco = coreIDB == 400;

        if (aIsRocket && bIsRocket)
        {
            targets.UnionWith(GetRocketTargets(board, center, Vector2.up));
            targets.UnionWith(GetRocketTargets(board, center, Vector2.right));
        }
        else if ((aIsRocket && bIsPropeller) || (aIsPropeller && bIsRocket))
        {
            int rocketID = aIsRocket ? coreIDA : coreIDB;
            Vector2 rocketDir = rocketID == 100 ? Vector2.up : Vector2.right;
            GridNode dest = GetPropellerDestination(board);
            if (dest != null)
            {
                targets.Add(dest);
                targets.UnionWith(GetRocketTargets(board, dest, rocketDir));
            }
        }
        else if ((aIsRocket && bIsBomb) || (aIsBomb && bIsRocket))
        {
            for (int offset = -1; offset <= 1; offset++)
            {
                GridNode rowNode = board.GetNodeAt(center.xPosition, center.yPosition + offset);
                if (rowNode != null)
                    targets.UnionWith(GetRocketTargets(board, rowNode, Vector2.right));

                GridNode colNode = board.GetNodeAt(center.xPosition + offset, center.yPosition);
                if (colNode != null)
                    targets.UnionWith(GetRocketTargets(board, colNode, Vector2.up));
            }
        }
        else if ((aIsRocket && bIsDisco) || (aIsDisco && bIsRocket))
        {
            List<GridNode> normalPieces = new List<GridNode>();
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    GridNode node = board.GetNodeAt(x, y);
                    PieceData data = board.GetNodeDataAt(x, y);
                    if (node != null && data != null && data.type == PieceType.Normal)
                        normalPieces.Add(node);
                }

            Shuffle(normalPieces);
            int count = Mathf.Min(12, normalPieces.Count);
            for (int i = 0; i < count; i++)
            {
                Vector2 rocketDir = Random.Range(0, 100) >= 50 ? Vector2.up : Vector2.right; 
                targets.UnionWith(GetRocketTargets(board, normalPieces[i], rocketDir));
            }
        }
        else if (aIsPropeller && bIsPropeller)
        {
            for (int i = 0; i < 3; i++)
            {
                GridNode dest = GetPropellerDestination(board);
                if (dest != null) targets.Add(dest);
            }
        }
        else if ((aIsPropeller && bIsBomb) || (aIsBomb && bIsPropeller))
        {
            GridNode dest = GetPropellerDestination(board);
            if (dest != null)
                targets.UnionWith(GetBombTargets(board, dest));
        }
        else if ((aIsPropeller && bIsDisco) || (aIsDisco && bIsPropeller))
        {
            int targetColor = GetRandomNormalCoreID(board);
            if (targetColor != -1)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    for (int x = 0; x < board.Width; x++)
                    {
                        GridNode node = board.GetNodeAt(x, y);
                        PieceData data = board.GetNodeDataAt(x, y);
                        if (node == null || data == null || data.coreID != targetColor) continue;
                        targets.Add(node);
                        GridNode propDest = GetPropellerDestination(board);
                        if (propDest != null) targets.Add(propDest);
                    }
                }
            }
        }
        else if (aIsBomb && bIsBomb)
        {
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    GridNode node = board.GetNodeAt(center.xPosition + dx, center.yPosition + dy);
                    if (node != null && node.data != null) targets.Add(node);
                }
        }
        else if ((aIsBomb && bIsDisco) || (aIsDisco && bIsBomb))
        {
            int targetColor = GetRandomNormalCoreID(board);
            if (targetColor != -1)
            {
                for (int y = 0; y < board.Height; y++)
                    for (int x = 0; x < board.Width; x++)
                    {
                        GridNode node = board.GetNodeAt(x, y);
                        PieceData data = board.GetNodeDataAt(x, y);
                        if (node == null || data == null || data.coreID != targetColor) continue;
                        targets.UnionWith(GetBombTargets(board, node));
                    }
            }
        }
        else if (aIsDisco && bIsDisco)
        {
            int colorA = GetRandomNormalCoreID(board);
            int colorB = GetRandomNormalCoreID(board, colorA);

            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    GridNode node = board.GetNodeAt(x, y);
                    PieceData data = board.GetNodeDataAt(x, y);
                    if (node == null || data == null) continue;
                    
                    targets.Add(node);
                }
        }

        return targets;
    }
}
