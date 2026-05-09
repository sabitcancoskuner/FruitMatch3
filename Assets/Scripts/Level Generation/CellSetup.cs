
public enum ItemType
{
    Piece,
    Collectible,
    Powerup,
    Obstacle
}

[System.Serializable]
public class CellSetup
{
    public bool isPlayable = true;
    
    // To spawn pre defined items, 0 is normal piece other ones are specific IDs
    public int preSpawnItemID = 0;
    public ItemType type;
}