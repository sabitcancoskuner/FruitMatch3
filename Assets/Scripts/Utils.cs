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

    public static int GetPowerupCoreID(MatchShape shape)
    {
        switch (shape)
        {
            case MatchShape.Match4Horizontal:
                return 100;
            
            case MatchShape.Match4Vertical:
                return 200;
            
            case MatchShape.Match5Bomb:
                return 300;
            
            case MatchShape.Match5Disco:
                return 400;

            // SHOULD NOT RUN
            default:    
                return -1;
        }


    }
    
}
