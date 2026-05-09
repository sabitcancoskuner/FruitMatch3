using System.Collections.Generic;
using UnityEngine;

public static class PowerupIDs
{
    public const int RocketVertical = 100;
    public const int RocketHorizontal = 200;
    public const int Bomb = 300;
    public const int DiscoBall = 400;
    public const int Propeller = 500;
}

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
            case PowerupIDs.RocketVertical:
                targets.UnionWith(GetRocketTargets(board, node, Vector2.up));
                break;
            
            case PowerupIDs.RocketHorizontal:
                targets.UnionWith(GetRocketTargets(board, node, Vector2.right));
                break;

            case PowerupIDs.Bomb:
                targets.UnionWith(GetBombTargets(board, node));
                break;
            
            case PowerupIDs.DiscoBall:
                targets.UnionWith(GetDiscoTargets(board, node, targetCoreID));
                break;
            
            case PowerupIDs.Propeller:
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

            for (int i = 1; i < 6; i++)
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

    private GridNode GetPropellerDestination(BoardState board)
    {
        List<GridNode> collectibles = new List<GridNode>();
        List<GridNode> obstacles = new List<GridNode>();
        List<GridNode> normalPieces = new List<GridNode>();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                GridNode node = board.GetNodeAt(x, y);
                PieceData data = board.GetNodeDataAt(x, y);

                if (node == null || data == null || node.state != NodeState.Idle) continue;

                if (data.type == PieceType.Collectible) collectibles.Add(node);
                else if (data.type == PieceType.Obstacle) obstacles.Add(node);
                else if (data.type == PieceType.Normal) normalPieces.Add(node);
            }
        }

        // Collectible -> Obstacle -> Normal Piece
        if (collectibles.Count > 0) return collectibles[Random.Range(0, collectibles.Count)];
        if (obstacles.Count > 0) return obstacles[Random.Range(0, obstacles.Count)];
        if (normalPieces.Count > 0) return normalPieces[Random.Range(0, normalPieces.Count)];

        return null;
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

    public (HashSet<GridNode> targets, List<GridNode> primary, List<GridNode> secondary) ProcessCombo(BoardState board, GridNode center, int coreIDA, int coreIDB)
    {
        HashSet<GridNode> targets = new HashSet<GridNode>();
        List<GridNode> primaryVisualTargets = new List<GridNode>();
        List<GridNode> secondaryVisualTargets = new List<GridNode>();

        bool aIsRocket = coreIDA == PowerupIDs.RocketVertical || coreIDA == PowerupIDs.RocketHorizontal;
        bool bIsRocket = coreIDB == PowerupIDs.RocketVertical || coreIDB == PowerupIDs.RocketHorizontal;
        bool aIsPropeller = coreIDA == PowerupIDs.Propeller;
        bool bIsPropeller = coreIDB == PowerupIDs.Propeller;
        bool aIsBomb = coreIDA == PowerupIDs.Bomb;
        bool bIsBomb = coreIDB == PowerupIDs.Bomb;
        bool aIsDisco = coreIDA == PowerupIDs.DiscoBall;
        bool bIsDisco = coreIDB == PowerupIDs.DiscoBall;


        if (aIsRocket && bIsRocket)
        {
            targets.UnionWith(GetRocketTargets(board, center, Vector2.up));
            targets.UnionWith(GetRocketTargets(board, center, Vector2.right));
        }
        else if ((aIsRocket && bIsPropeller) || (aIsPropeller && bIsRocket))
        {
            int rocketID = aIsRocket ? coreIDA : coreIDB;
            Vector2 rocketDir = rocketID == PowerupIDs.RocketVertical ? Vector2.up : Vector2.right;
            GridNode dest = GetPropellerDestination(board);
            if (dest != null)
            {
                secondaryVisualTargets.Add(dest);
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
            {
                for (int x = 0; x < board.Width; x++)
                {
                    GridNode node = board.GetNodeAt(x, y);
                    PieceData data = board.GetNodeDataAt(x, y);
                    if (node != null && node.state == NodeState.Idle && data != null && data.type == PieceType.Normal)
                        normalPieces.Add(node);
                }
            }

            Shuffle(normalPieces);
            int count = Mathf.Min(12, normalPieces.Count);
            for (int i = 0; i < count; i++)
            {
                primaryVisualTargets.Add(normalPieces[i]);
                Vector2 rocketDir = Random.value >= .5f ? Vector2.up : Vector2.right; 
                targets.UnionWith(GetRocketTargets(board, normalPieces[i], rocketDir));
            }
        }
        else if (aIsPropeller && bIsPropeller)
        {
            for (int i = 0; i < 3; i++)
            {
                GridNode dest = GetPropellerDestination(board);
                if (dest != null)
                {
                    primaryVisualTargets.Add(dest);
                    targets.Add(dest);
                }
            }
        }
        else if ((aIsPropeller && bIsBomb) || (aIsBomb && bIsPropeller))
        {
            GridNode dest = GetPropellerDestination(board);
            if (dest != null)
            {
                targets.Add(dest);
                secondaryVisualTargets.Add(dest);
                targets.UnionWith(GetBombTargets(board, dest));
            }
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

                        primaryVisualTargets.Add(node);
                        targets.Add(node);

                        GridNode propDest = GetPropellerDestination(board);
                        if (propDest != null)
                        {
                            secondaryVisualTargets.Add(propDest);
                            targets.Add(propDest);
                        }
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
            List<GridNode> normalPieces = new List<GridNode>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    GridNode node = board.GetNodeAt(x, y);
                    PieceData data = board.GetNodeDataAt(x, y);
                    if (node != null && node.state == NodeState.Idle && data != null && data.type == PieceType.Normal)
                        normalPieces.Add(node);
                }
            }
            
            Shuffle(normalPieces);
            int count = Mathf.Min(12, normalPieces.Count);
            for (int i = 0; i < count; i++)
            {
                primaryVisualTargets.Add(normalPieces[i]);
                targets.UnionWith(GetBombTargets(board, normalPieces[i]));
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

        return (targets, primaryVisualTargets, secondaryVisualTargets);
    }
}
