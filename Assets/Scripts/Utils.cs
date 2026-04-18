using UnityEngine;

public class Utils
{
    public static Vector2Int CalculateGridLocation(Vector2 position)
    {
        return new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
    }

    public static Vector3 ScreenToWorld(Vector3 position)
    {
        position.z = Camera.main.nearClipPlane;
        return Camera.main.ScreenToWorldPoint(position);
    }

    public static PieceType GetPieceType(MatchShape shape)
    {
        switch (shape)
        {
            case MatchShape.Match4Horizontal:
                return PieceType.VerticalRocket;
            
            case MatchShape.Match4Vertical:
                return PieceType.HorizontalRocket;
            
            case MatchShape.Match5Bomb:
                return PieceType.Bomb;
            
            case MatchShape.Match5Disco:
                return PieceType.DiscoBall;

            // SHOULD NOT RUN
            default:    
                return PieceType.Normal;
        }


    }
    
}
