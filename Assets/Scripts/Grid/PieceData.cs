using UnityEngine;

public enum PieceType
{
    Normal,
    Bomb,
    Disco,
    VerticalRocket,
    HorizontalRocket
}

[System.Serializable]
public class PieceData
{
    public int coreID;
    public GameObject visualPiece;
    public PieceType type;

    public PieceData(int id, PieceType type = PieceType.Normal)
    {
        coreID = id;
        this.type = type;
    }
}
