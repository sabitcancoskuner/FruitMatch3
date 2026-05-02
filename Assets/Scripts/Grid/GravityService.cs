using System.Collections.Generic;
using UnityEngine;

public struct FallInstruction
{
    public GridNode targetNode;
    public PieceData pieceData;
    public int startX, startY;
    public int targetX, targetY;
    public bool isNewSpawn;
}

public class GravityService
{
    public List<FallInstruction> CalculateGravityForColumn(BoardState board, int x)
    {
        return CalculateGravityForColumn(board, x, board.Height + board.BufferSize, true);
    }

    public List<FallInstruction> CalculateGravityForColumn(BoardState board, int x, int maxYExclusive, bool allowSpawn)
    {
        List<FallInstruction> instructions = new List<FallInstruction>();

        int clampedMaxY = Mathf.Clamp(maxYExclusive, 0, board.Height + board.BufferSize);
        int spawnHeight = clampedMaxY;

        for (int y = 0; y < clampedMaxY; y++)
        {
            GridNode emptyNode = board.GetNodeAt(x, y);

            if (!emptyNode.isPlayable) continue;

            if (emptyNode.data == null && emptyNode.state == NodeState.Idle)
            {
                bool pieceFound = false;

                for (int i = y + 1; i < clampedMaxY; i++)
                {
                    GridNode nodeAbove = board.GetNodeAt(x, i);

                    if (!nodeAbove.isPlayable) continue;

                    // If we find ANY piece above us (Idle, Falling, or Matching)
                    if (nodeAbove.data != null)
                    {
                        pieceFound = true; // Don't spawn from the sky, we have a piece above us!

                        if (nodeAbove.data.type == PieceType.Obstacle) break;

                        if (nodeAbove.state == NodeState.Idle)
                        {
                            emptyNode.data = nodeAbove.data;
                            nodeAbove.data = null;
                            emptyNode.state = NodeState.Falling;
                            
                            instructions.Add(new FallInstruction
                            {
                               targetNode = emptyNode,
                               pieceData = emptyNode.data,
                               startX = x,
                               startY = i,
                               targetX = emptyNode.xPosition,
                               targetY = emptyNode.yPosition,
                               isNewSpawn = false
                            });
                        }
                        
                        break;
                    }
                }

                if (!pieceFound && allowSpawn)
                {
                    int newID = Random.Range(1, 6);
                    emptyNode.data = new PieceData(newID);
                    emptyNode.state = NodeState.Falling;

                    instructions.Add(new FallInstruction
                            {
                               targetNode = emptyNode,
                               pieceData = emptyNode.data,
                               startX = x,
                               startY = spawnHeight,
                               targetX = emptyNode.xPosition,
                               targetY = emptyNode.yPosition,
                               isNewSpawn = true
                            });
                    spawnHeight++;
                }
            }
        }

        return instructions;
    }
}
