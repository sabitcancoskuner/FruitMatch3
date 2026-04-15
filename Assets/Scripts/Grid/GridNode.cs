
public enum NodeState
{
    Idle,
    Falling,
    Matching
}

[System.Serializable]
public class GridNode
{
    public int xPosition;
    public int yPosition;
    public bool isPlayable;
    public NodeState state;
    public PieceData data;

    public GridNode(int x, int y, int coreID, bool playable = true)
    {
        xPosition = x;
        yPosition = y;
        isPlayable = playable;

        state = NodeState.Idle;
        data = new PieceData(coreID);
    }

    public void SetState(NodeState newState)
    {
        state = newState;
    }
}
