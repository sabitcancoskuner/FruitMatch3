using System.Collections.Generic;

public enum MatchShape
{
    Match3,
    Match4Horizontal,
    Match4Vertical,
    Match5Bomb,
    Match5Disco
}

public class Match
{
    public List<GridNode> matchedNodes;
    public GridNode center;
    public MatchShape shape;
    public int matchCoreID;

    public Match(List<GridNode> nodes, GridNode target, MatchShape type, int coreID)
    {
        matchedNodes = nodes;
        center = target;
        shape = type;
        matchCoreID = coreID;
    }
}
