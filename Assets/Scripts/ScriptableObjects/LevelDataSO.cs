using UnityEngine;

[CreateAssetMenu(fileName = "New Level Data", menuName = "Level/Level Data")]
public class LevelDataSO : ScriptableObject
{
    [Header("Grid Dimensions")]
    public int width;
    public int height;
    public int bufferSize;

    [Header("Level Rules")]
    public int movesAllowed;

    [Header("Grid Map")]
    public CellSetup[] gridLayout;

}
