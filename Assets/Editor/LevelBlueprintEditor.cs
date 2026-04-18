using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
    private enum PaintMode { TogglePlayable, PlaceTile, PlacePowerup, EraseTile }
    private PaintMode paintMode = PaintMode.TogglePlayable;

    private int selectedTileID = 1;
    private bool isPainting   = false;
    private bool paintTarget  = false; // target isPlayable value when drag-toggling

    // Asset binding 
    private LevelDataSO loadedLevel;

    // Scroll 
    private Vector2 gridScroll;

    // Visual constants
    private const float CELL = 44f;
    private const float GAP = 2f;

    private Texture2D[] pieceTextures;
    private Texture2D[] powerupTextures;

    private static readonly Color ColUnplayable = Color.gray1;
    private static readonly Color ColEmpty = Color.gray8;
    private static readonly Color[] TileColors =
    {
        Color.ghostWhite,  // 0 – random  (not selectable)
        Color.saddleBrown,  // 1
        Color.skyBlue,  // 2
        Color.softGreen,  // 3
        Color.softYellow,  // 4
        Color.darkOrange,  // 5
        Color.rebeccaPurple,  // 6
        Color.deepPink,  // 7
    };
    private static readonly Color[] PowerupColors =
    {
        Color.aliceBlue,
        Color.aquamarine,
        Color.violet,
        Color.bisque
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
        DrawSettingsPanel();
        GUILayout.Space(6);
        DrawAssetPanel();
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
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (paintMode == PaintMode.PlaceTile)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Tile ID:");
            EditorGUILayout.BeginHorizontal();

            for (int i = 1; i <= 5; i++)
            {
                GUI.backgroundColor = TileColors[i];

                GUIStyle s = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
                Texture2D texture = GetPieceTexture(i);
                GUIContent content = texture != null ? new GUIContent(texture, $"Paint tile ID {i}") 
                                                     : new GUIContent(i.ToString(), $"Paint tile ID {i}");

                if (GUILayout.Button(content, s, GUILayout.Width(45), GUILayout.Height(45)))
                    selectedTileID = i;

                if (i == 5)
                {
                    GUIContent eraseContent = new GUIContent("X", $"Erase tile.");

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

            for (int i = 1; i <= 4; i++)
            {
                GUI.backgroundColor = PowerupColors[i - 1];

                GUIStyle s = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 13 };
                Texture2D texture = GetPowerupTexture(i);
                GUIContent content = texture != null ? new GUIContent(texture, $"Paint powerup ID {i * 100}") 
                                                     : new GUIContent(i.ToString(), $"Paint powerup ID {i * 100}");

                if (GUILayout.Button(content, s, GUILayout.Width(45), GUILayout.Height(45)))
                    selectedTileID = i * 100;

                if (i == 4)
                {
                    GUIContent eraseContent = new GUIContent("X", $"Erase tile.");

                    if (GUILayout.Button(eraseContent, s, GUILayout.Width(45), GUILayout.Height(45)))
                        selectedTileID = 0;
                }
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }
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
            pieceTextures[id] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Sprites/Pieces/characters_{id}.png");
        
        return pieceTextures[id];
    }

    private Texture2D GetPowerupTexture(int id)
    {
        if (powerupTextures == null)
            powerupTextures = new Texture2D[5];

        if (powerupTextures[id] == null)
            powerupTextures[id] = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Sprites/Powerups/powerup_{id}.png");

        return powerupTextures[id];
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
            bg = TileColors[Mathf.Clamp(cell.preSpawnItemID, 0, TileColors.Length - 1)];
        else if (cell.preSpawnItemID > 0 && cell.type == ItemType.Powerup)
            bg = PowerupColors[Mathf.Clamp(cell.preSpawnItemID / 100 - 1, 0, PowerupColors.Length - 1)];
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
                   cell.type = ItemType.Piece;
                }
                break;

            case PaintMode.PlacePowerup:
                if (cell.isPlayable)
                {
                    cell.preSpawnItemID = selectedTileID;
                    cell.type = ItemType.Powerup;
                }
                break;
        }
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
        }

        AssetDatabase.CreateAsset(newAsset, path);
        AssetDatabase.SaveAssets();

        loadedLevel = newAsset;
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newAsset;
        Debug.Log($"[LevelBlueprintEditor] Created → {path}");
    }
}
