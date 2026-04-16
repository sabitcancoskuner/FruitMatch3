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
    
}
