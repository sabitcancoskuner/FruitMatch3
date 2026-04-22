using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LevelBlueprintEditor : EditorWindow
{
    // Grid settings 
    private int gridWidth = 7;
    private int gridHeight = 7;
    private int bufferSize = 2;
    private int movesAllowed = 30;

    // Cell data [x, y]
    private CellSetup[,] gridCells;

    // Paint state
    private enum PaintMode { TogglePlayable, PlaceTile, PlacePowerup, PlaceCollectible, PlaceObstacle, EraseTile }
    private PaintMode paintMode = PaintMode.TogglePlayable;

    private int selectedTileID = 1;
    private bool isPainting   = false;
    private bool paintTarget  = false; // target isPlayable value when drag-toggling

    // Asset binding 
    private LevelDataSO loadedLevel;

    // Level goals
    private List<LevelObjective> levelGoals = new List<LevelObjective>();

    // Scroll 
    private Vector2 gridScroll;

    // Visual constants
    private const float CELL = 44f;
    private const float GAP = 2f;

    private Texture2D[] pieceTextures;
    private Texture2D[] powerupTextures;
    private Texture2D[] collectibleTextures;
    private Texture2D[] obstacleTextures;

    private static readonly Color ColUnplayable = Color.gray1;
    private static readonly Color ColEmpty = Color.gray8;
    private static readonly Color[] TileColors =
    {
        Color.orange,  // 1
        Color.skyBlue,  // 2
        Color.softGreen,  // 3
        Color.violet,  // 4
        Color.indianRed  // 5
    };
    private static readonly Color[] PowerupColors =
    {
        Color.royalBlue,
        Color.paleVioletRed,
        Color.darkRed,
        Color.springGreen,
        Color.orangeRed
    };
    private static readonly Color[] CollectibleColors =
    {
        Color.steelBlue,
        Color.mediumPurple,
        Color.yellowNice
    };
    private static readonly Color[] ObstacleColors =
    {
        Color.brown
    };

    [MenuItem("Window/Level Layout Generator")]
    public static void ShowWindow()
    {
        GetWindow<LevelBlueprintEditor>("Level Layout Generator");
    }

    private void OnEnable()
    {
        if (gridCells == null)
            GenerateGrid();
    }

    private void OnGUI()
    {
        DrawAssetPanel();
        GUILayout.Space(6);
        DrawSettingsPanel();
        GUILayout.Space(6);
        DrawGoalsPanel();
        GUILayout.Space(6);
        DrawPaintToolbar();
        GUILayout.Space(6);
        DrawGrid();
    }

    //  Settings
    private void DrawSettingsPanel()
    {
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int w = Mathf.Clamp(EditorGUILayout.IntField("Width", gridWidth),  1, 15);
        int h = Mathf.Clamp(EditorGUILayout.IntField("Height", gridHeight), 1, 15);

        if (EditorGUI.EndChangeCheck()) 
        { 
            gridWidth = w; 
            gridHeight = h; 
        }

        bufferSize = Mathf.Max(0, EditorGUILayout.IntField("Buffer Size", bufferSize));
        movesAllowed = Mathf.Max(1, EditorGUILayout.IntField("Moves Allowed", movesAllowed));

        GUILayout.Space(4);
        if (GUILayout.Button("Generate / Reset Grid", GUILayout.Height(24)))
        {
            bool confirmed = gridCells == null || EditorUtility.DisplayDialog("Reset Grid", "This will clear the current layout. Continue?", "Yes", "Cancel");
            if (confirmed) 
                GenerateGrid();
        }
    }

    private void GenerateGrid()
    {
        gridCells = new CellSetup[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth;  x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                gridCells[x, y] = new CellSetup();
            }
        }
        
        Repaint();
    }

    //  Paint toolbar
    private void DrawPaintToolbar()
    {
        EditorGUILayout.LabelField("Paint Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawModeButton("Toggle Playable", PaintMode.TogglePlayable);
        DrawModeButton("Place Tile", PaintMode.PlaceTile);
        DrawModeButton("Place Powerup", PaintMode.PlacePowerup);
        DrawModeButton("Place Collectible", PaintMode.PlaceCollectible);
        DrawModeButton("Place Obstacle", PaintMode.PlaceObstacle);
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (paintMode == PaintMode.PlaceTile)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Tile ID:");
            EditorGUILayout.BeginHorizontal();

            for (int i = 1; i <= 5; i++)
            {
                GUI.backgroundColor = TileColors[i - 1];

                GUIStyle s = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
                Texture2D texture = GetPieceTexture(i);
                GUIContent content = texture != null ? new GUIContent(texture, $"Paint tile ID {i}") 
                                                     : new GUIContent(i.ToString(), $"Paint tile ID {i}");

                if (GUILayout.Button(content, s, GUILayout.Width(45), GUILayout.Height(45)))
                    selectedTileID = i;

                if (i == 5)
                {
                    GUIContent eraseContent = new GUIContent("X", $"Erase tile.");
                    GUI.backgroundColor = Color.black;

                    if (GUILayout.Button(eraseContent, s, GUILayout.Width(45), GUILayout.Height(45)))
                        selectedTileID = 0;
                }
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        if (paintMode == PaintMode.PlacePowerup)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Powerup ID:");
            EditorGUILayout.BeginHorizontal();

            for (int i = 1; i <= 5; i++)
            {
                GUI.backgroundColor = PowerupColors[i - 1];

                GUIStyle s = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
                Texture2D texture = GetPowerupTexture(i);
                GUIContent content = texture != null ? new GUIContent(texture, $"Paint powerup ID {i * 100}") 
                                                     : new GUIContent(i.ToString(), $"Paint powerup ID {i * 100}");

                if (GUILayout.Button(content, s, GUILayout.Width(45), GUILayout.Height(45)))
                    selectedTileID = i * 100;

                if (i == 5)
                {
                    GUIContent eraseContent = new GUIContent("X", $"Erase tile.");
                    GUI.backgroundColor = Color.black;

                    if (GUILayout.Button(eraseContent, s, GUILayout.Width(45), GUILayout.Height(45)))
                        selectedTileID = 0;
                }
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        if (paintMode == PaintMode.PlaceCollectible)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Powerup ID:");
            EditorGUILayout.BeginHorizontal();

            for (int i = 1; i <= 3; i++)
            {
                GUI.backgroundColor = CollectibleColors[i - 1];

                GUIStyle s = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
                Texture2D texture = GetCollectibleTexture(i);
                GUIContent content = texture != null ? new GUIContent(texture, $"Paint collectible ID {i * 1000}") 
                                                     : new GUIContent(i.ToString(), $"Paint powerup ID {i * 1000}");

                if (GUILayout.Button(content, s, GUILayout.Width(45), GUILayout.Height(45)))
                    selectedTileID = i * 1000;

                if (i == 3)
                {
                    GUIContent eraseContent = new GUIContent("X", $"Erase tile.");
                    GUI.backgroundColor = Color.black;

                    if (GUILayout.Button(eraseContent, s, GUILayout.Width(45), GUILayout.Height(45)))
                        selectedTileID = 0;
                }
            }
            

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        if (paintMode == PaintMode.PlaceObstacle)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Obstacle ID:");
            EditorGUILayout.BeginHorizontal();

            for (int i = 1; i <= 1; i++)
            {
                GUI.backgroundColor = ObstacleColors[i - 1];

                GUIStyle s = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
                Texture2D texture = GetObstacleTexture(i);
                GUIContent content = texture != null ? new GUIContent(texture, $"Paint Obstacle ID {i * 10000}") 
                                                     : new GUIContent(i.ToString(), $"Paint Obstacle ID {i * 10000}");

                if (GUILayout.Button(content, s, GUILayout.Width(45), GUILayout.Height(45)))
                    selectedTileID = i * 10000;

                if (i == 1)
                {
                    GUIContent eraseContent = new GUIContent("X", $"Erase tile.");
                    GUI.backgroundColor = Color.black;

                    if (GUILayout.Button(eraseContent, s, GUILayout.Width(45), GUILayout.Height(45)))
                        selectedTileID = 0;
                }
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawGoalsPanel()
    {
        EditorGUILayout.LabelField("Level Goals", EditorStyles.boldLabel);

        string[] collectibleNames = { "Blue Candy (1000)", "Purple Candy (2000)", "Yellow Candy (3000)" };
        int[] collectibleIDs = { 1000, 2000, 3000 };

        for (int i = 0; i < levelGoals.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            int currentIndex = System.Array.IndexOf(collectibleIDs, levelGoals[i].itemID);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup(currentIndex, collectibleNames, GUILayout.Width(170));
            levelGoals[i].itemID = collectibleIDs[newIndex];

            EditorGUILayout.LabelField("×", GUILayout.Width(14));
            levelGoals[i].targetCount = Mathf.Max(1, EditorGUILayout.IntField(levelGoals[i].targetCount, GUILayout.Width(50)));

            bool removed = GUILayout.Button("−", GUILayout.Width(24));

            EditorGUILayout.EndHorizontal();

            if (removed)
            {
                levelGoals.RemoveAt(i);
                break;
            }
        }

        if (GUILayout.Button("+ Add Goal", GUILayout.Height(22)))
            levelGoals.Add(new LevelObjective { itemID = 1000, targetCount = 1 });
    }

    private void DrawModeButton(string label, PaintMode mode)
    {
        GUI.backgroundColor = paintMode == mode ? Color.cyan : Color.white;
        if (GUILayout.Button(label)) paintMode = mode;
    }

    private Texture2D GetPieceTexture(int id)
    {
        if (pieceTextures == null)
            pieceTextures = new Texture2D[8];

        // Load the piece texture from the sprites folder.
        if (pieceTextures[id] == null)
            pieceTextures[id] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Sprites/Pieces/piece_{id}.png");
        
        return pieceTextures[id];
    }

    private Texture2D GetPowerupTexture(int id)
    {
        if (powerupTextures == null)
            powerupTextures = new Texture2D[6];

        if (powerupTextures[id] == null)
            powerupTextures[id] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Sprites/Powerups/powerup_{id}.png");

        return powerupTextures[id];
    }

    private Texture2D GetCollectibleTexture(int id)
    {
        if (collectibleTextures == null)
            collectibleTextures = new Texture2D[5];

        if (collectibleTextures[id] == null)
            collectibleTextures[id] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Sprites/Collectibles/candy_{id}.png");

        return collectibleTextures[id];
    }

    private Texture2D GetObstacleTexture(int id)
    {
        if (obstacleTextures == null)
            obstacleTextures = new Texture2D[2];
        
        if (obstacleTextures[id] == null)
            obstacleTextures[id] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Sprites/Obstacles/obstacle_{id}.png");
        
        return obstacleTextures[id];
    }

    //  Grid
    private void DrawGrid()
    {
        if (gridCells == null)
        {
            EditorGUILayout.HelpBox("Click 'Generate / Reset Grid' to begin.", MessageType.Info);
            return;
        }

        float totalW = gridWidth  * (CELL + GAP);
        float totalH = gridHeight * (CELL + GAP);
        float fixedPanelsH = 300f;
        float viewH = Mathf.Max(120f, position.height - fixedPanelsH);

        gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.Height(viewH));
        float xOffset = Mathf.Max(0f, (EditorGUIUtility.currentViewWidth - totalW) / 2f);
        Rect area  = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, totalH);

        Event e = Event.current;

        for (int row = gridHeight - 1; row >= 0; row--)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                float drawRow = gridHeight - 1 - row;
                Rect  cellRect = new Rect(
                    area.x + xOffset + col * (CELL + GAP),
                    area.y + 20 + drawRow * (CELL + GAP),
                    CELL, CELL);

                CellSetup cell = gridCells[col, row];
                DrawCell(cellRect, cell);

                if (e.isMouse && cellRect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDown)
                    {
                        isPainting = true;
                        if (paintMode == PaintMode.TogglePlayable)
                            paintTarget = !cell.isPlayable;

                        ApplyPaint(cell);
                        e.Use();
                        Repaint();
                    }
                    else if (e.type == EventType.MouseDrag && isPainting)
                    {
                        ApplyPaint(cell);
                        e.Use();
                        Repaint();
                    }
                }
            }
        }

        if (e.type == EventType.MouseUp)
            isPainting = false;

        EditorGUILayout.EndScrollView();
    }

    private void DrawCell(Rect rect, CellSetup cell)
    {
        Color bg;
        if (!cell.isPlayable)
            bg = ColUnplayable;
        else if (cell.preSpawnItemID > 0 && cell.type == ItemType.Piece)
            bg = TileColors[Mathf.Clamp(cell.preSpawnItemID - 1, 0, TileColors.Length - 1)];
        else if (cell.preSpawnItemID > 0 && cell.type == ItemType.Powerup)
            bg = PowerupColors[Mathf.Clamp(cell.preSpawnItemID / 100 - 1, 0, PowerupColors.Length - 1)];
        else if (cell.preSpawnItemID > 0 && cell.type == ItemType.Collectible)
            bg = CollectibleColors[Mathf.Clamp(cell.preSpawnItemID / 1000 - 1, 0, PowerupColors.Length - 1)];
        else if (cell.preSpawnItemID > 0 && cell.type == ItemType.Obstacle)
            bg = ObstacleColors[Mathf.Clamp(cell.preSpawnItemID / 10000 - 1, 0, PowerupColors.Length - 1)];
        else
            bg = ColEmpty;

        EditorGUI.DrawRect(rect, bg);

        // Border
        Color border = new Color(0f, 0f, 0f, 0.45f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f,  rect.y, 1f, rect.height), border);

        // Label
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 16
        };

        if (!cell.isPlayable)
        {
            labelStyle.normal.textColor = Color.gray4;
            GUI.Label(rect, "X", labelStyle);
        }
        else if (cell.preSpawnItemID > 0)
        {
            Texture2D texture = null;

            switch (cell.type)
            {
                case ItemType.Piece:
                    texture = GetPieceTexture(cell.preSpawnItemID);
                    break;

                case ItemType.Powerup:
                    texture = GetPowerupTexture(cell.preSpawnItemID / 100);
                    break;

                case ItemType.Collectible:
                    texture = GetCollectibleTexture(cell.preSpawnItemID / 1000);
                    break;
                
                case ItemType.Obstacle:
                    texture = GetObstacleTexture(cell.preSpawnItemID / 10000);
                    break;
            }

            if (texture != null)
            {
                float pad = 4f;
                // Using scalemode to protect aspect ratio.
                GUI.DrawTexture(new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2f, rect.height - pad * 2f),
                    texture,
                    ScaleMode.ScaleToFit);
                                        
            }
            else
            {
                labelStyle.normal.textColor = Color.white;
                GUI.Label(rect, cell.preSpawnItemID.ToString(), labelStyle);
            }

        }
    }

    private void ApplyPaint(CellSetup cell)
    {
        switch (paintMode)
        {
            case PaintMode.TogglePlayable:
                cell.isPlayable = paintTarget;
                if (!cell.isPlayable) cell.preSpawnItemID = 0;
                break;

            case PaintMode.PlaceTile:
                if (cell.isPlayable)
                {
                   cell.preSpawnItemID = selectedTileID;
                   cell.type = SetItemType(selectedTileID);
                }
                break;

            case PaintMode.PlacePowerup:
                if (cell.isPlayable)
                {
                    cell.preSpawnItemID = selectedTileID;
                    cell.type = SetItemType(selectedTileID);
                }
                break;

            case PaintMode.PlaceCollectible:
                if (cell.isPlayable)
                {
                    cell.preSpawnItemID = selectedTileID;
                    cell.type = SetItemType(selectedTileID);
                }
                break;

            case PaintMode.PlaceObstacle:
                if (cell.isPlayable)
                {
                    cell.preSpawnItemID = selectedTileID;
                    cell.type = SetItemType(selectedTileID);
                }
                break;
        }
    }

    private ItemType SetItemType(int id)
    {
        if (id >= 10000)
            return ItemType.Obstacle;
        else if (id >= 1000)
            return ItemType.Collectible;
        else if (id >= 100)
            return ItemType.Powerup;
        
        return ItemType.Piece;
    }

    //  Asset panel
    private void DrawAssetPanel()
    {
        EditorGUILayout.LabelField("Level Asset", EditorStyles.boldLabel);
        loadedLevel = (LevelDataSO) EditorGUILayout.ObjectField("Asset", loadedLevel, typeof(LevelDataSO), false);

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = loadedLevel != null;

        GUIContent loadContent = new GUIContent("Load", "Load Imported Asset.");
        if (GUILayout.Button(loadContent, GUILayout.Height(28)))
            LoadFromAsset(loadedLevel);

        GUIContent saveContent = new GUIContent("Save", "Save Current Level Layout to Imported Asset.");
        if (GUILayout.Button(saveContent, GUILayout.Height(28)))
            SaveToAsset(loadedLevel);

        GUI.enabled = true;

        GUIContent createNewContent = new GUIContent("Create New", "Create new Level Data with current Level Layout.");
        if (GUILayout.Button(createNewContent, GUILayout.Height(28)))
            CreateNewAsset();

        EditorGUILayout.EndHorizontal();
    }

    // Serialisation helpers
    private void SaveToAsset(LevelDataSO levelData)
    {
        if (gridCells == null) return;

        levelData.width = gridWidth;
        levelData.height = gridHeight;
        levelData.bufferSize = bufferSize;
        levelData.movesAllowed = movesAllowed;

        levelData.gridLayout = new CellSetup[gridWidth * gridHeight];
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth;  x++)
            {
                CellSetup src = gridCells[x, y];
                levelData.gridLayout[y * gridWidth + x] = new CellSetup
                {
                    isPlayable      = src.isPlayable,
                    preSpawnItemID  = src.preSpawnItemID,
                    type            = src.type
                };
            }
        }

        levelData.levelGoals = new List<LevelObjective>(levelGoals);

        EditorUtility.SetDirty(levelData);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelBlueprintEditor] Saved → {AssetDatabase.GetAssetPath(levelData)}");
    }

    private void LoadFromAsset(LevelDataSO asset)
    {
        gridWidth = Mathf.Max(1, asset.width);
        gridHeight = Mathf.Max(1, asset.height);
        bufferSize = Mathf.Max(0, asset.bufferSize);
        movesAllowed = Mathf.Max(1, asset.movesAllowed);

        gridCells = new CellSetup[gridWidth, gridHeight];

        bool layoutValid = asset.gridLayout != null && asset.gridLayout.Length == gridWidth * gridHeight;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth;  x++)
            {
                if (layoutValid)
                {
                    CellSetup currentCell = asset.gridLayout[y * gridWidth + x];
                    gridCells[x, y] = new CellSetup
                    {
                        isPlayable     = currentCell.isPlayable,
                        preSpawnItemID = currentCell.preSpawnItemID,
                        type           = currentCell.type
                    };
                }
                else
                {
                    gridCells[x, y] = new CellSetup();
                }
            }
        }

        if (!layoutValid)
            Debug.LogWarning("[LevelBlueprintEditor] gridLayout size mismatch – loaded empty grid.");
        else
            Debug.Log($"[LevelBlueprintEditor] Loaded → {AssetDatabase.GetAssetPath(asset)}");

        levelGoals = asset.levelGoals != null
            ? new List<LevelObjective>(asset.levelGoals)
            : new List<LevelObjective>();

        Repaint();
    }

    private void CreateNewAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Level Data", "New Level", "asset", "Choose where to save this level.");

        if (string.IsNullOrEmpty(path)) return;

        LevelDataSO newAsset = CreateInstance<LevelDataSO>();

        if (gridCells != null)
        {
            newAsset.width = gridWidth;
            newAsset.height = gridHeight;
            newAsset.bufferSize = bufferSize;
            newAsset.movesAllowed = movesAllowed;
            newAsset.gridLayout = new CellSetup[gridWidth * gridHeight];

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth;  x++)
                {
                    CellSetup src = gridCells[x, y];
                    newAsset.gridLayout[y * gridWidth + x] = new CellSetup
                    {
                        isPlayable     = src.isPlayable,
                        preSpawnItemID = src.preSpawnItemID,
                        type           = src.type
                    };
                }
            }

            newAsset.levelGoals = new List<LevelObjective>(levelGoals);
        }

        AssetDatabase.CreateAsset(newAsset, path);
        AssetDatabase.SaveAssets();

        loadedLevel = newAsset;
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newAsset;
        Debug.Log($"[LevelBlueprintEditor] Created → {path}");
    }
}
