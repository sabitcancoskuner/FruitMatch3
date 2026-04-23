using System.Collections.Generic;
using UnityEngine;

public struct HintData
{
    public int length;
    public List<PieceData> nodesToHint;
    public PieceData nodeToSwap;
}

public class DeadlockAndHintManager 
{
    private MatchScanner scanner;

    public DeadlockAndHintManager(MatchScanner scanner)
    {
        this.scanner = scanner;
    }

    public bool IsBoardSettled(BoardState board)
    {
        // if (_powerupChainDepth > 0) return false;

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                GridNode node = board.GetNodeAt(x, y);
                if (node == null || !node.isPlayable) continue;

                if (node.data == null || node.state != NodeState.Idle) return false;
            }
        }

        return true;
    }

    public bool HasAnyPossibleMove(BoardState board)
    {
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                GridNode node = board.GetNodeAt(x, y);
                if (node == null || !node.isPlayable || node.data == null) continue;

                // Tapping any powerup is already a valid move in this game.
                if (node.data.type == PieceType.Powerup) return true;

                if (x + 1 < board.Width && CanSwapCreateMatch(board, x, y, x + 1, y)) return true;
                if (y + 1 < board.Height && CanSwapCreateMatch(board, x, y, x, y + 1)) return true;
            }
        }

        return false;
    }

    public bool TryGetBestHint(BoardState board, out HintData bestHint)
    {
        bestHint = default;
        List<HintData> allHints = new List<HintData>();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                GridNode node = board.GetNodeAt(x, y);
                PieceData nodeData = board.GetNodeDataAt(x, y);
                if (node == null || !node.isPlayable || nodeData == null || node.state != NodeState.Idle) continue;

                if (nodeData.type == PieceType.Powerup) continue;

                if (x + 1 < board.Width && TryBuildHintForSwap(board, x, y, x + 1, y, out HintData horizontalHint))
                    allHints.Add(horizontalHint);

                if (y + 1 < board.Height && TryBuildHintForSwap(board, x, y, x, y + 1, out HintData verticalHint))
                    allHints.Add(verticalHint);
            }
        }

        if (allHints.Count == 0)
            return false;

        int maxLength = int.MinValue;
        for (int i = 0; i < allHints.Count; i++)
        {
            if (allHints[i].length > maxLength)
                maxLength = allHints[i].length;
        }

        List<HintData> strongestHints = new List<HintData>();
        for (int i = 0; i < allHints.Count; i++)
        {
            if (allHints[i].length == maxLength)
                strongestHints.Add(allHints[i]);
        }

        bestHint = strongestHints[Random.Range(0, strongestHints.Count)];
        return true;
    }

    public bool TryCalculateShuffle(BoardState board, List<GridNode> movableNodes, out List<PieceData> shuffledData)
    {
        Debug.Log("Trying reshuffle.");
        shuffledData = new List<PieceData>();
        List<PieceData> originalData = new();

        foreach (GridNode node in movableNodes)
        {
            shuffledData.Add(node.data);
            originalData.Add(node.data);
        }

        bool validShuffle = false;
        int attempts = 0;
        const int maxAttempts = 150;

        while (!validShuffle && attempts < maxAttempts)
        {
            attempts++;
            Shuffle(shuffledData);

            for (int i = 0; i < movableNodes.Count; i++)
            {
                movableNodes[i].data = shuffledData[i];
            }

            validShuffle = !scanner.BoardHasImmediateMatch(board) && HasAnyPossibleMove(board);
        }

        if (!validShuffle)
        {
            for (int i = 0; i < movableNodes.Count; i++)
            {
                movableNodes[i].data = originalData[i];
            }
        }
        return validShuffle;
    }

    private bool TryBuildHintForSwap(BoardState board, int ax, int ay, int bx, int by, out HintData hintData)
    {
        hintData = default;

        GridNode a = board.GetNodeAt(ax, ay);
        GridNode b = board.GetNodeAt(bx, by);

        if (a == null || b == null || !a.isPlayable || !b.isPlayable) return false;
        if (a.data == null || b.data == null) return false;
        if (a.state != NodeState.Idle || b.state != NodeState.Idle) return false;

        if (a.data.type == PieceType.Obstacle || b.data.type == PieceType.Obstacle) return false;
        if (a.data.type == PieceType.Collectible || b.data.type == PieceType.Collectible) return false;

        // Swap the data
        PieceData originalA = a.data;
        PieceData originalB = b.data;
        a.data = originalB;
        b.data = originalA;

        Match resolvedMatchA = scanner.GetMatchAt(board, ax, ay);
        Match resolvedMatchB = scanner.GetMatchAt(board, bx, by);

        Match selectedMatch = resolvedMatchA != null ? resolvedMatchA : (resolvedMatchB != null ? resolvedMatchB : null);

        // If there is no match, swap the data back
        if (selectedMatch == null)
        {
            a.data = originalA;
            b.data = originalB;
            return false;
        }

        List<PieceData> piecesToHint = new List<PieceData>();
        HashSet<Vector2Int> selectedPositions = new HashSet<Vector2Int>();

        for (int i = 0; i < selectedMatch.matchedNodes.Count; i++)
        {
            GridNode matchedNode = selectedMatch.matchedNodes[i];
            if (matchedNode == null) continue;

            Vector2Int pos = new Vector2Int(matchedNode.xPosition, matchedNode.yPosition);
            selectedPositions.Add(pos);

            PieceData matchedPiece = board.GetNodeDataAt(pos.x, pos.y);
            if (matchedPiece != null && !piecesToHint.Contains(matchedPiece))
                piecesToHint.Add(matchedPiece);
        }

        bool movedAFormsMatch = selectedPositions.Contains(new Vector2Int(bx, by));
        PieceData pieceToSwap = movedAFormsMatch ? originalA : originalB;

        hintData = new HintData
        {
            length = selectedPositions.Count,
            nodesToHint = piecesToHint,
            nodeToSwap = pieceToSwap
        };

        a.data = originalA;
        b.data = originalB;
        return true;
    }

    private bool CanSwapCreateMatch(BoardState board, int ax, int ay, int bx, int by)
    {
        GridNode a = board.GetNodeAt(ax, ay);
        GridNode b = board.GetNodeAt(bx, by);

        if (a == null || b == null || !a.isPlayable || !b.isPlayable) return false;
        if (a.data == null || b.data == null) return false;

        if (a.data.type == PieceType.Obstacle || b.data.type == PieceType.Obstacle) return false;
        if (a.data.type == PieceType.Collectible || b.data.type == PieceType.Collectible) return false;

        // A swap with a powerup is always a move in the current rules.
        if (a.data.type == PieceType.Powerup || b.data.type == PieceType.Powerup) return true;

        PieceData temp = a.data;
        a.data = b.data;
        b.data = temp;

        bool createsMatch = scanner.HasMatchAt(board, ax, ay) || scanner.HasMatchAt(board, bx, by);

        temp = a.data;
        a.data = b.data;
        b.data = temp;

        return createsMatch;
    }


    private void Shuffle(List<PieceData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            PieceData temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
