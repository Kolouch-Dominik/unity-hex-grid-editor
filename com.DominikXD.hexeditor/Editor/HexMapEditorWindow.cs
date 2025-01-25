using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Editor.Runtime;

namespace Editor
{
    public class HexMapEditorWindow : EditorWindow
    {
        private static string DATA_FILE_PATH = "Assets/Editor/HexEditorData.json";

        private EditorMode currentMode = EditorMode.AddRemove;

        // Reference to our scene's GridMap
        private GridMap gridMap;
        private bool loadedOnce = false;

        // The new "tile palette" with layer info
        private List<HexTileSetting> tileSettings = new List<HexTileSetting>();
        private int selectedTileIndex = 0;

        // Ghost object for previewing tile
        private GameObject ghostObject;
        private float ghostRotationDeg = 0f;

        // Brush parameters
        private float heightStep = 0.2f; // how much to raise/lower
        private int brushSize = 0;     // 0 = single hex, 1=neighbors, etc.

        // Scrolling in the palette
        private Vector2 scrollPos;

        [MenuItem("Window/Hex3D Editor (Advanced)")]
        public static void ShowWindow()
        {
            GetWindow<HexMapEditorWindow>("Hex3D Editor");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;

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

            // Save on disable
            SaveEditorState();
        }

        private void OnGUI()
        {
            GUILayout.Label("3D Hex Editor", EditorStyles.boldLabel);

            // Pick the GridMap
            var newGridMap = (GridMap)EditorGUILayout.ObjectField("GridMap", gridMap, typeof(GridMap), true);
            if (newGridMap != gridMap)
            {
                gridMap = newGridMap;
                SaveEditorState();
            }

            // GridMap params
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

            // Two modes
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

            // Brush controls
            heightStep = EditorGUILayout.FloatField("Height Step", heightStep);
            brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 0, 3);

            EditorGUILayout.Space();

            // The tileSettings array
            int oldCount = tileSettings.Count;
            int newCount = EditorGUILayout.IntField("Number of Tiles in Palette", oldCount);
            if (newCount != oldCount)
            {
                while (tileSettings.Count < newCount) tileSettings.Add(new HexTileSetting());
                while (tileSettings.Count > newCount) tileSettings.RemoveAt(tileSettings.Count - 1);
            }

            // Draw each tile setting
            for (int i = 0; i < tileSettings.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Tile {i}", EditorStyles.boldLabel);
                tileSettings[i].tileName = EditorGUILayout.TextField("Name", tileSettings[i].tileName);
                tileSettings[i].prefab = (GameObject)EditorGUILayout.ObjectField("Prefab",
                    tileSettings[i].prefab, typeof(GameObject), false);
                tileSettings[i].layer = EditorGUILayout.IntField("Layer",
                    tileSettings[i].layer);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tile Palette (Click to select)", EditorStyles.boldLabel);
            DrawTilePalette();

            EditorGUILayout.Space();

            // Save/Load/clear
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
                if (EditorUtility.DisplayDialog("Clear All",
                    "Are you sure you want to remove all placed hexes?", "Yes", "No"))
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
            if (tileSettings == null || tileSettings.Count == 0)
            {
                EditorGUILayout.HelpBox("No tile settings defined.", MessageType.Info);
                return;
            }

            int columns = 4;
            int buttonSize = 64;

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            for (int i = 0; i < tileSettings.Count; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    int index = i + c;
                    if (index >= tileSettings.Count) break;
                    DrawTileButton(index, buttonSize);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTileButton(int index, int size)
        {
            var setting = tileSettings[index];
            GameObject prefab = setting.prefab;
            Texture2D preview = null;
            if (prefab != null)
            {
                preview = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
            }

            // Highlight if selected
            GUIStyle style = new GUIStyle(GUI.skin.button);
            if (selectedTileIndex == index)
            {
                Color highlight = new Color(1f, 1f, 0.5f, 0.4f);
                style.normal.background = MakeTex(size, size, highlight);
            }

            // Button label
            GUIContent content = preview
                ? new GUIContent(preview, $"Layer={setting.layer}")
                : new GUIContent($"{setting.tileName}\n(Layer={setting.layer})");

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

        //------------------------------------------------------------------------------
        // Scene GUI
        //------------------------------------------------------------------------------
        private void OnSceneGUI(SceneView sceneView)
        {
            if (gridMap == null)
            {
                DestroyGhost();
                return;
            }

            if (tileSettings == null || tileSettings.Count == 0)
            {
                DestroyGhost();
                return;
            }

            // Editor events
            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            // Rotate ghost via scrollWheel or R
            if (currentMode == EditorMode.AddRemove && e.type == EventType.ScrollWheel)
            {
                float stepDeg = 60f;
                ghostRotationDeg += (e.delta.y > 0) ? stepDeg : -stepDeg;
                if (ghostRotationDeg < 0) ghostRotationDeg += 360f;
                ghostRotationDeg = ghostRotationDeg % 360f;

                UpdateGhostTransform();
                e.Use();
            }
            if (currentMode == EditorMode.AddRemove && e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                ghostRotationDeg += 60f;
                ghostRotationDeg %= 360f;
                UpdateGhostTransform();
                e.Use();
            }

            // Raycast
            if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                Vector3 hitPoint = hit.point;
                Vector2Int qr = gridMap.WorldToAxial(hitPoint);

                if (currentMode == EditorMode.AddRemove)
                {
                    // Show ghost
                    Vector3 snappedPos = gridMap.AxialToWorld(qr.x, qr.y);
                    UpdateGhost(snappedPos);

                    // Left = place, Right = remove
                    if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                    {
                        e.Use();
                        if (e.button == 0)
                        {
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
                    if (ghostObject) ghostObject.SetActive(false);

                    if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                    {
                        e.Use();
                        float delta = (e.button == 0) ? +heightStep : -heightStep;
                        RaiseLowerHexWithBrush(qr, delta);
                    }
                }
            }
            else
            {
                if (ghostObject && currentMode == EditorMode.AddRemove)
                {
                    ghostObject.SetActive(false);
                }
            }
        }

        //------------------------------------------------------------------------------
        // Brush Operations
        //------------------------------------------------------------------------------
        private void PlaceHexWithBrush(Vector2Int centerQR)
        {
            if (selectedTileIndex < 0 || selectedTileIndex >= tileSettings.Count) return;
            HexTileSetting setting = tileSettings[selectedTileIndex];
            if (setting.prefab == null) return;

            // The prefab GUID
            string prefabGuid = GetAssetGUID(setting.prefab);

            // The tile's assigned layer
            int newLayer = setting.layer;

            // The tile's rotation
            Quaternion rot = Quaternion.Euler(0, ghostRotationDeg, 0);

            // Get brush coords
            List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
            foreach (var qr in coords)
            {
                if (gridMap == null) break;
                gridMap.PlaceHex(qr, setting.prefab, newLayer, rot, prefabGuid);
            }
        }

        private void RemoveHexWithBrush(Vector2Int centerQR)
        {
            List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
            foreach (var qr in coords)
            {
                if (gridMap == null) break;
                gridMap.RemoveHex(qr);
            }
        }

        private void RaiseLowerHexWithBrush(Vector2Int centerQR, float deltaY)
        {
            List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
            foreach (var qr in coords)
            {
                gridMap.RaiseLowerAllTiles(qr, deltaY);
            }
        }

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

        //------------------------------------------------------------------------------
        // Ghost
        //------------------------------------------------------------------------------
        private void CreateOrReplaceGhost()
        {
            DestroyGhost();

            if (selectedTileIndex < 0 || selectedTileIndex >= tileSettings.Count) return;
            var setting = tileSettings[selectedTileIndex];
            if (setting.prefab == null) return;

            ghostObject = Instantiate(setting.prefab);
            ghostObject.name = "GhostHexPreview";
            ghostObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

            MakeGhostMaterial(ghostObject);

            // Disable collisions
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

        //------------------------------------------------------------------------------
        // Saving/Loading
        //------------------------------------------------------------------------------
        private void SaveEditorState()
        {
            try
            {
                HexEditorState state = new HexEditorState();

                // GridMap reference
                bool isSceneObject;
                string sceneObjectName;
                string guid = GetAssetOrSceneRef(gridMap, out isSceneObject, out sceneObjectName);
                state.gridMapGuid = guid;
                state.gridMapSceneName = sceneObjectName;
                state.isGridMapSceneObject = isSceneObject;

                // If we have a gridmap, store its settings + placed hexes
                if (gridMap != null)
                {
                    state.hexSize = gridMap.hexSize;
                    state.gridRange = gridMap.gridRange;
                    state.placedHexes = gridMap.GetPlacedHexesData();
                }

                // Save tile palette
                state.tileSettings.Clear();
                foreach (var tset in tileSettings)
                {
                    // Fill the prefabGUID
                    bool dummySceneObj;
                    string dummyName;
                    string pGuid = GetAssetOrSceneRef(tset.prefab, out dummySceneObj, out dummyName);

                    // Make a copy
                    HexTileSetting copy = new HexTileSetting()
                    {
                        tileName = tset.tileName,
                        prefab = tset.prefab,
                        layer = tset.layer,
                        prefabGUID = pGuid
                    };
                    state.tileSettings.Add(copy);
                }

                // Editor fields
                state.selectedTileIndex = selectedTileIndex;
                state.ghostRotationDeg = ghostRotationDeg;
                state.currentMode = currentMode.ToString();
                state.heightStep = heightStep;
                state.brushSize = brushSize;

                // Serialize
                string json = JsonUtility.ToJson(state, true);
                string dir = Path.GetDirectoryName(DATA_FILE_PATH);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(DATA_FILE_PATH, json);

                Debug.Log($"Editor state saved to {DATA_FILE_PATH}");
            }
            catch (Exception ex)
            {
                Debug.LogError("Error saving editor state: " + ex);
            }
        }

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

                // Re-find the gridMap
                GridMap loadedGrid = null;
                if (!state.isGridMapSceneObject)
                {
                    // It's an asset
                    loadedGrid = LoadAssetFromGUID<GridMap>(state.gridMapGuid);
                }
                else
                {
                    // It's a scene object
                    if (!string.IsNullOrEmpty(state.gridMapSceneName))
                    {
                        GameObject go = GameObject.Find(state.gridMapSceneName);
                        if (go != null)
                        {
                            loadedGrid = go.GetComponent<GridMap>();
                        }
                    }
                }

                gridMap = loadedGrid;
                if (gridMap != null)
                {
                    Undo.RecordObject(gridMap, "Load GridMap Settings");
                    gridMap.hexSize = state.hexSize;
                    gridMap.gridRange = state.gridRange;
                    gridMap.LoadPlacedHexesData(state.placedHexes);
                }

                // Load tile palette
                tileSettings.Clear();
                foreach (var tset in state.tileSettings)
                {
                    // Rebuild the tile in memory
                    GameObject prefab = LoadAssetFromGUID<GameObject>(tset.prefabGUID);
                    HexTileSetting copy = new HexTileSetting()
                    {
                        tileName = tset.tileName,
                        prefab = prefab,
                        layer = tset.layer,
                        prefabGUID = tset.prefabGUID
                    };
                    tileSettings.Add(copy);
                }

                // Editor fields
                selectedTileIndex = state.selectedTileIndex;
                ghostRotationDeg = state.ghostRotationDeg;
                heightStep = state.heightStep;
                brushSize = state.brushSize;

                if (Enum.TryParse(state.currentMode, out EditorMode mode))
                    currentMode = mode;
                else
                    currentMode = EditorMode.AddRemove;

                // Create or destroy ghost
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

        //------------------------------------------------------------------------------
        // Helper
        //------------------------------------------------------------------------------
        private string GetAssetOrSceneRef(UnityEngine.Object obj,
                                          out bool isSceneObject,
                                          out string sceneObjectName)
        {
            isSceneObject = false;
            sceneObjectName = "";

            if (obj == null) return "";

            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                // Scene object
                isSceneObject = true;
                sceneObjectName = obj.name;
                return "";
            }
            else
            {
                // It's an asset
                return AssetDatabase.AssetPathToGUID(path);
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
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
