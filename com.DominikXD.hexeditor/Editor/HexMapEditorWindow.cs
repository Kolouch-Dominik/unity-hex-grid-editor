using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
public class HexMapEditorWindow : EditorWindow
{
    private static string DATA_FILE_PATH = "Assets/Editor/HexEditorData.json";

    private EditorMode currentMode = EditorMode.AddRemove;

    private GridMap gridMap;
    private List<GameObject> tilePrefabs = new List<GameObject>();
    private int selectedTileIndex = 0;

    private GameObject ghostObject;
    private float ghostRotationDeg = 0f;
    private Vector2 scrollPos;

    private float heightStep = 0.2f;   // value for increasing/decreasing height of hex
    private int brushSize = 0;        // 0 = one hex, 1 = + neighbours, etc.

    // helping
    private bool loadedOnce = false;

    [MenuItem("Window/Hex3D Editor (Advanced)")]
    public static void ShowWindow()
    {
        GetWindow<HexMapEditorWindow>("Hex3D Editor");
    }

    private void OnEnable()
    {
        // callback for drawing
        SceneView.duringSceneGui += OnSceneGUI;

        // first attempt to load 
        if (!loadedOnce)
        {
            loadedOnce = true;
            LoadEditorState();
        }
    }

    private void OnDisable()
    {
        
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyGhost();

        // save state to json
        SaveEditorState();
    }

    private void OnGUI()
    {
        GUILayout.Label("3D Hex Editor", EditorStyles.boldLabel);

        // gridmap
        var newGridMap = (GridMap)EditorGUILayout.ObjectField("GridMap", gridMap, typeof(GridMap), true);
        if (newGridMap != gridMap)
        {
            gridMap = newGridMap;
            // update GUID 
            SaveEditorState();
        }

        // if grid map is not null we can change params
        if (gridMap != null)
        {
            EditorGUI.BeginChangeCheck();
            float newHexSize = EditorGUILayout.FloatField("Hex Size", gridMap.hexSize);
            int newRange = EditorGUILayout.IntField("Grid Range", gridMap.gridRange);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(gridMap, "Change GridMap Settings");
                gridMap.hexSize = newHexSize;
                gridMap.gridRange = newRange;
            }
        }

        EditorGUILayout.Space();

        // two button modes
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentMode == EditorMode.AddRemove, "Add/Remove", "Button"))
        {
            if (currentMode != EditorMode.AddRemove)
            {
                currentMode = EditorMode.AddRemove;
                CreateOrReplaceGhost();
            }
        }
        if (GUILayout.Toggle(currentMode == EditorMode.HeightMap, "Height Map", "Button"))
        {
            if (currentMode != EditorMode.HeightMap)
            {
                currentMode = EditorMode.HeightMap;
                DestroyGhost();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Parameters Height / Brush
        heightStep = EditorGUILayout.FloatField("Height Step", heightStep);
        brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 0, 3);

        // Palette
        int oldSize = tilePrefabs.Count;
        int newSize = EditorGUILayout.IntField("Tile Prefabs Count", oldSize);
        if (newSize != oldSize)
        {
            // change list 
            while (tilePrefabs.Count < newSize) tilePrefabs.Add(null);
            while (tilePrefabs.Count > newSize) tilePrefabs.RemoveAt(tilePrefabs.Count - 1);
        }

        // drawing tilePrefabs
        for (int i = 0; i < tilePrefabs.Count; i++)
        {
            tilePrefabs[i] = (GameObject)EditorGUILayout.ObjectField($"Tile {i}", tilePrefabs[i], typeof(GameObject), false);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
        DrawTilePalette();

        EditorGUILayout.Space();
        // buttons for save/load/clear hexes
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Editor State"))
        {
            SaveEditorState();
        }
        if (GUILayout.Button("Load Editor State"))
        {
            LoadEditorState();
        }
        if (gridMap != null && GUILayout.Button("Clear All Hexes"))
        {
            if (EditorUtility.DisplayDialog("Clear All", "Are you sure you want to remove all placed hexes?", "Yes", "No"))
            {
                gridMap.ClearAllHexes();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (GUI.changed)
        {
            Repaint();
        }
    }

    private void DrawTilePalette()
    {
        if (tilePrefabs == null || tilePrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("No tilePrefabs defined.", MessageType.Info);
            return;
        }

        int columns = 4;
        int buttonSize = 64;

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
        for (int i = 0; i < tilePrefabs.Count; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columns; c++)
            {
                int index = i + c;
                if (index >= tilePrefabs.Count) break;
                DrawTileButton(index, buttonSize);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTileButton(int index, int size)
    {
        GameObject prefab = tilePrefabs[index];
        Texture2D preview = null;
        if (prefab != null)
        {
            preview = AssetPreview.GetAssetPreview(prefab)
                   ?? AssetPreview.GetMiniThumbnail(prefab);
        }

        GUIStyle style = new GUIStyle(GUI.skin.button);
        if (selectedTileIndex == index)
        {
            // highlight
            Color highlight = new Color(1f, 1f, 0.5f, 0.4f);
            style.normal.background = MakeTex(size, size, highlight);
        }

        GUIContent content = preview
            ? new GUIContent(preview)
            : new GUIContent($"Tile {index}");

        if (GUILayout.Button(content, style, GUILayout.Width(size), GUILayout.Height(size)))
        {
            selectedTileIndex = index;
            if (currentMode == EditorMode.AddRemove)
            {
                CreateOrReplaceGhost();
            }
        }
    }

    private Texture2D MakeTex(int w, int h, Color col)
    {
        Color[] pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(w, h);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    // ------------------------------------------------
    // OnSceneGUI
    // ------------------------------------------------
    private void OnSceneGUI(SceneView sceneView)
    {
        if (gridMap == null)
        {
            DestroyGhost();
            return;
        }
        if (tilePrefabs == null || tilePrefabs.Count == 0)
        {
            DestroyGhost();
            return;
        }

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        // mouse wheel rotations 
        if (currentMode == EditorMode.AddRemove && e.type == EventType.ScrollWheel)
        {
            float stepDeg = 60f;
            ghostRotationDeg += (e.delta.y > 0) ? stepDeg : -stepDeg;
            if (ghostRotationDeg < 0) ghostRotationDeg += 360f;
            ghostRotationDeg = ghostRotationDeg % 360f;

            UpdateGhostTransform();
            e.Use();
        }

        // rotation on r
        if (currentMode == EditorMode.AddRemove && e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
        {
            ghostRotationDeg += 60f;
            ghostRotationDeg %= 360f;
            UpdateGhostTransform();
            e.Use();
        }

        // raycasting on floor
        if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
        {
            Vector3 hitPoint = hit.point;
            Vector2Int qr = gridMap.WorldToAxial(hitPoint);

            if (currentMode == EditorMode.AddRemove)
            {
                // show preview hex
                Vector3 snappedPos = gridMap.AxialToWorld(qr.x, qr.y);
                UpdateGhost(snappedPos);

                // mouse click adding/deleting hex
                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                {
                    e.Use();
                    if (e.button == 0)
                    {
                        // left mousebutton add
                        PlaceHexWithBrush(qr);
                    }
                    else
                    {
                        RemoveHexWithBrush(qr);
                    }
                }
            }
            else if (currentMode == EditorMode.HeightMap)
            {
                // disable preview if not needed
                if (ghostObject) ghostObject.SetActive(false);

                // increasing/decreasing height
                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                {
                    e.Use();
                    RaiseLowerHexWithBrush(qr, e.button == 0 ? +heightStep : -heightStep);
                }
            }
        }
        else
        {
            // if ray dont hit, we hide preview (ghost)
            if (ghostObject && currentMode == EditorMode.AddRemove)
                ghostObject.SetActive(false);
        }
    }

    // ------------------------------------------------
    // Brush operations
    // ------------------------------------------------

    /// <summary>
    /// add (placehex) on coord qr and adding neighbours (brushsize)
    /// </summary>
    private void PlaceHexWithBrush(Vector2Int centerQR)
    {
        int idx = Mathf.Clamp(selectedTileIndex, 0, tilePrefabs.Count - 1);
        GameObject prefab = tilePrefabs[idx];
        if (prefab == null) return;

        string prefabGuid = GetAssetGUID(prefab);

        List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
        foreach (var qr in coords)
        {
            if (gridMap == null) break; // for sure
            Quaternion rot = Quaternion.Euler(0, ghostRotationDeg, 0);
            gridMap.PlaceHex(qr, prefab, rot, prefabGuid);
        }
    }

    /// <summary>
    /// delete hex on coord qr and neighbours
    /// </summary>
    private void RemoveHexWithBrush(Vector2Int centerQR)
    {
        List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
        foreach (var qr in coords)
        {
            if (gridMap == null) break;
            gridMap.RemoveHex(qr);
        }
    }

    /// <summary>
    /// Increase / Decrease hex and neighbours around centerQR
    /// </summary>
    private void RaiseLowerHexWithBrush(Vector2Int centerQR, float deltaY)
    {
        List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
        foreach (var qr in coords)
        {
            if (gridMap.TryGetPlacedHex(qr, out GameObject hexGO))
            {
                Undo.RecordObject(hexGO.transform, "Raise/Lower Hex");
                Vector3 pos = hexGO.transform.position;
                pos.y += deltaY;
                hexGO.transform.position = pos;
            }
        }
    }

    /// <summary>
    /// Returns all hex coordinates within "range" of the centers.
    /// range=0 -> just center, range=1 -> center + surrounding 6, etc.
    /// (Simple hex distance.)
    /// </summary>
    private List<Vector2Int> GetHexesInRange(Vector2Int center, int range)
    {
        var results = new List<Vector2Int>();
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                int dz = -dx - dy;
                int dist = (Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz)) / 2;
                if (dist <= range)
                {
                    int q = center.x + dx;
                    int r = center.y + dy;
                    results.Add(new Vector2Int(q, r));
                }
            }
        }
        return results;
    }

    // ------------------------------------------------
    // preview (ghos) functions
    // ------------------------------------------------

    private void CreateOrReplaceGhost()
    {
        DestroyGhost();
        int idx = Mathf.Clamp(selectedTileIndex, 0, tilePrefabs.Count - 1);
        if (tilePrefabs[idx] == null) return;

        ghostObject = Instantiate(tilePrefabs[idx]);
        ghostObject.name = "GhostHex";
        ghostObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

        MakeGhostMaterial(ghostObject);

        // turn off collisions
        foreach (var c in ghostObject.GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    private void DestroyGhost()
    {
        if (ghostObject)
        {
            DestroyImmediate(ghostObject);
            ghostObject = null;
        }
    }

    private void UpdateGhost(Vector3 position)
    {
        if (!ghostObject) CreateOrReplaceGhost();
        if (!ghostObject) return;

        ghostObject.SetActive(true);
        ghostObject.transform.position = position;
        ghostObject.transform.rotation = Quaternion.Euler(0, ghostRotationDeg, 0);
    }

    private void UpdateGhostTransform()
    {
        if (!ghostObject) return;
        ghostObject.transform.rotation = Quaternion.Euler(0, ghostRotationDeg, 0);
    }

    private void MakeGhostMaterial(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            Material ghostMat = new Material(r.sharedMaterial);
            ghostMat.color = new Color(ghostMat.color.r, ghostMat.color.g, ghostMat.color.b, 0.5f);

            ghostMat.SetFloat("_Mode", 3); // Transparent
            ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ghostMat.SetInt("_ZWrite", 0);
            ghostMat.DisableKeyword("_ALPHATEST_ON");
            ghostMat.EnableKeyword("_ALPHABLEND_ON");
            ghostMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            ghostMat.renderQueue = 3000;

            r.sharedMaterial = ghostMat;
        }
    }

    // ------------------------------------------------
    // saving / loading from json
    // ------------------------------------------------

    /// <summary>
    /// Save state to json (DATA_FILE_PATH).
    /// </summary>
    private void SaveEditorState()
    {
        try
        {
            // Creating State
            HexEditorState state = new HexEditorState();

            // Save reference of gridmap
            state.gridMapGuid = GetAssetGUID(gridMap);

            // save settings of gridmap
            if (gridMap != null)
            {
                state.hexSize = gridMap.hexSize;
                state.gridRange = gridMap.gridRange;

                // save placedHexes
                state.placedHexes = gridMap.GetPlacedHexesData();
            }

            // Palette
            state.prefabGuids.Clear();
            foreach (var prefab in tilePrefabs)
            {
                string guid = GetAssetGUID(prefab);
                state.prefabGuids.Add(guid);
            }

            state.selectedTileIndex = selectedTileIndex;
            state.ghostRotationDeg = ghostRotationDeg;
            state.currentMode = currentMode.ToString();
            state.heightStep = heightStep;
            state.brushSize = brushSize;

            // Serialize to Json
            string json = JsonUtility.ToJson(state, true);

            // Save on disk
            string dir = Path.GetDirectoryName(DATA_FILE_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(DATA_FILE_PATH, json);

            
            Debug.Log($"Editor state saved to {DATA_FILE_PATH}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error saving editor state: " + ex);
        }
    }

    /// <summary>
    /// Load state from Json and update Editor and Gridmap
    /// </summary>
    private void LoadEditorState()
    {
        if (!File.Exists(DATA_FILE_PATH))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(DATA_FILE_PATH);
            HexEditorState state = JsonUtility.FromJson<HexEditorState>(json);
            if (state == null) return;

            // Find gripmap (guid)
            GridMap loadedGrid = LoadAssetFromGUID<GridMap>(state.gridMapGuid);
            gridMap = loadedGrid;

            if (gridMap != null)
            {
                Undo.RecordObject(gridMap, "Load GridMap Settings");
                gridMap.hexSize = state.hexSize;
                gridMap.gridRange = state.gridRange;

                // reload placehexes
                gridMap.LoadPlacedHexesData(state.placedHexes);
            }

            // Palette
            tilePrefabs.Clear();
            foreach (var guid in state.prefabGuids)
            {
                GameObject prefab = LoadAssetFromGUID<GameObject>(guid);
                tilePrefabs.Add(prefab);
            }

            selectedTileIndex = state.selectedTileIndex;
            ghostRotationDeg = state.ghostRotationDeg;
            heightStep = state.heightStep;
            brushSize = state.brushSize;

            if (Enum.TryParse(state.currentMode, out EditorMode mode))
                currentMode = mode;
            else
                currentMode = EditorMode.AddRemove;

            if (currentMode == EditorMode.AddRemove)
            {
                CreateOrReplaceGhost();
            }
            else
            {
                DestroyGhost();
            }
            Debug.Log($"Editor state loaded from {DATA_FILE_PATH}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error loading editor state: " + ex);
        }
    }

    private string GetAssetGUID(UnityEngine.Object obj)
    {
        if (obj == null) return "";
        string path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path)) return "";
        return AssetDatabase.AssetPathToGUID(path);
    }

    private T LoadAssetFromGUID<T>(string guid) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset;
    }
}
