using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Editor.Runtime;
using Editor;

namespace HexEditor
{
    public class HexMapEditorWindow : EditorWindow
    {
        private HexGridManager gridManager;
        private GhostController ghostController = new GhostController();
        private List<HexTileSetting> tilePalette = new List<HexTileSetting>();
        private int selectedTileIndex = 0;
        private EditorMode currentMode = EditorMode.AddRemove;
        private float heightStep = 0.2f;
        private int brushSize = 0;

        private Vector2 scrollPos;

        [MenuItem("Window/Hex3D Editor (Advanced)")]
        public static void ShowWindow()
        {
            GetWindow<HexMapEditorWindow>("Hex3D Editor");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadEditorState();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            ghostController.DestroyGhost();
            SaveEditorState();
        }

        private void OnGUI()
        {
            GUILayout.Label("3D Hex Editor", EditorStyles.boldLabel);

            gridManager = (HexGridManager)EditorGUILayout.ObjectField(new GUIContent("Hex Grid Manager", "Reference to the HexGridManager in your scene"), gridManager, typeof(HexGridManager), true);
            if (gridManager != null)
            {
                EditorGUI.BeginChangeCheck();
                float newHexSize = EditorGUILayout.FloatField(new GUIContent("Hex Size", "Size of the hexagon"), gridManager.hexSize);
                int newGridRange = EditorGUILayout.IntField(new GUIContent("Grid Range", "Number of hexes from the center"), gridManager.gridRange);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(gridManager, "Update Grid Settings");
                    gridManager.hexSize = newHexSize;
                    gridManager.gridRange = newGridRange;
                }
            }

            EditorGUILayout.Space();
            DrawModeSelection();
            EditorGUILayout.Space();
            DrawBrushSettings();
            EditorGUILayout.Space();
            DrawTilePaletteSettings();
            EditorGUILayout.Space();
            DrawTilePalette();

            EditorGUILayout.Space();
            DrawSaveLoadButtons();

            if (GUI.changed)
            {
                Repaint();
            }
        }

        private void DrawModeSelection()
        {
            GUILayout.Label("Editor Mode", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(currentMode == EditorMode.AddRemove, "Add/Remove", "Button"))
            {
                if (currentMode != EditorMode.AddRemove)
                {
                    currentMode = EditorMode.AddRemove;
                    ghostController.DestroyGhost();
                    CreateOrReplaceGhost();
                }
            }
            if (GUILayout.Toggle(currentMode == EditorMode.HeightMap, "Height Map", "Button"))
            {
                if (currentMode != EditorMode.HeightMap)
                {
                    currentMode = EditorMode.HeightMap;
                    ghostController.DestroyGhost();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBrushSettings()
        {
            GUILayout.Label("Brush Settings", EditorStyles.boldLabel);
            heightStep = EditorGUILayout.FloatField(new GUIContent("Height Step", "How much to raise or lower hex"), heightStep);
            brushSize = EditorGUILayout.IntSlider(new GUIContent("Brush Size", "Range of effect (0 = single hex)"), brushSize, 0, 3);
        }

        private void DrawTilePaletteSettings()
        {
            GUILayout.Label("Tile Palette Settings", EditorStyles.boldLabel);
            int newCount = EditorGUILayout.IntField("Number of Tiles", tilePalette.Count);
            while (tilePalette.Count < newCount)
                tilePalette.Add(new HexTileSetting());
            while (tilePalette.Count > newCount)
                tilePalette.RemoveAt(tilePalette.Count - 1);

            for (int i = 0; i < tilePalette.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                tilePalette[i].tileName = EditorGUILayout.TextField("Name", tilePalette[i].tileName);
                tilePalette[i].prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", tilePalette[i].prefab, typeof(GameObject), false);
                tilePalette[i].layer = EditorGUILayout.IntField("Layer", tilePalette[i].layer);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawTilePalette()
        {
            GUILayout.Label("Tile Palette", EditorStyles.boldLabel);
            if (tilePalette.Count == 0)
            {
                EditorGUILayout.HelpBox("Define tiles in the palette settings.", MessageType.Info);
                return;
            }

            int columns = 4;
            int buttonSize = 64;
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            for (int i = 0; i < tilePalette.Count; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < columns; j++)
                {
                    int index = i + j;
                    if (index >= tilePalette.Count) break;
                    DrawTileButton(index, buttonSize);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTileButton(int index, int size)
        {
            HexTileSetting tile = tilePalette[index];
            Texture2D preview = null;
            if (tile.prefab != null)
            {
                preview = AssetPreview.GetAssetPreview(tile.prefab) ?? AssetPreview.GetMiniThumbnail(tile.prefab);
            }
            GUIStyle style = new GUIStyle(GUI.skin.button);
            if (selectedTileIndex == index)
            {
                Color highlight = new Color(1f, 1f, 0.5f, 0.4f);
                style.normal.background = MakeTex(size, size, highlight);
            }
            GUIContent content = preview != null ?
                new GUIContent(preview, $"Layer: {tile.layer}") :
                new GUIContent(tile.tileName + $"\nLayer: {tile.layer}");

            if (GUILayout.Button(content, style, GUILayout.Width(size), GUILayout.Height(size)))
            {
                // if there is a change in selection we update preview
                if (selectedTileIndex != index)
                {
                    selectedTileIndex = index;
                    if (currentMode == EditorMode.AddRemove)
                    {
                        CreateOrReplaceGhost();
                    }
                }
            }
        }


        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = col;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DrawSaveLoadButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Editor State"))
            {
                SaveEditorState();
            }
            if (GUILayout.Button("Load Editor State"))
            {
                LoadEditorState();
            }
            if (gridManager != null && GUILayout.Button("Clear All Hexes"))
            {
                if (EditorUtility.DisplayDialog("Clear All", "Are you sure you want to clear all hexes?", "Yes", "No"))
                {
                    gridManager.ClearAllHexes();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (gridManager == null || tilePalette.Count == 0) return;

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                Vector3 hitPoint = hit.point;
                Vector2Int qr = gridManager.WorldToAxial(hitPoint);
                if (currentMode == EditorMode.AddRemove)
                {
                    Vector3 snapPos = gridManager.AxialToWorld(qr.x, qr.y);
                    CreateOrReplaceGhost();
                    ghostController.UpdateGhost(snapPos, ghostController.RotationDegrees);
                    if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                    {
                        e.Use();
                        if (e.button == 0)
                            PlaceHexWithBrush(qr);
                        else
                            RemoveHexWithBrush(qr);
                    }
                }
                else if (currentMode == EditorMode.HeightMap)
                {
                    ghostController.GhostObject?.SetActive(false);
                    if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                    {
                        e.Use();
                        float delta = (e.button == 0) ? heightStep : -heightStep;
                        RaiseLowerHexWithBrush(qr, delta);
                    }
                }
            }
            else
            {
                ghostController.GhostObject?.SetActive(false);
            }

            // Ovládání rotace ghostu kolečkem myši nebo klávesou R
            if (currentMode == EditorMode.AddRemove && e.type == EventType.ScrollWheel)
            {
                float step = 60f;
                ghostController.RotateGhost(e.delta.y > 0 ? step : -step);
                e.Use();
            }
            if (currentMode == EditorMode.AddRemove && e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                ghostController.RotateGhost(60f);
                e.Use();
            }
        }

        private void PlaceHexWithBrush(Vector2Int centerQR)
        {
            if (selectedTileIndex < 0 || selectedTileIndex >= tilePalette.Count) return;
            HexTileSetting setting = tilePalette[selectedTileIndex];
            if (setting.prefab == null) return;
            string prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(setting.prefab));
            Quaternion rot = Quaternion.Euler(0, ghostController.RotationDegrees, 0);
            List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
            foreach (var qr in coords)
            {
                gridManager?.PlaceHex(qr, setting.prefab, setting.layer, rot, prefabGuid);
            }
        }

        private void RemoveHexWithBrush(Vector2Int centerQR)
        {
            List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
            foreach (var qr in coords)
            {
                gridManager?.RemoveHex(qr);
            }
        }

        private void RaiseLowerHexWithBrush(Vector2Int centerQR, float deltaY)
        {
            List<Vector2Int> coords = GetHexesInRange(centerQR, brushSize);
            foreach (var qr in coords)
            {
                gridManager?.RaiseLowerAllTiles(qr, deltaY);
            }
        }

        private List<Vector2Int> GetHexesInRange(Vector2Int center, int range)
        {
            List<Vector2Int> results = new List<Vector2Int>();
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    int dz = -dx - dy;
                    int dist = (Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz)) / 2;
                    if (dist <= range)
                    {
                        results.Add(new Vector2Int(center.x + dx, center.y + dy));
                    }
                }
            }
            return results;
        }

        private void CreateOrReplaceGhost()
        {
            if (selectedTileIndex < 0 || selectedTileIndex >= tilePalette.Count) return;
            HexTileSetting setting = tilePalette[selectedTileIndex];
            if (setting.prefab == null) return;

            // Zničíme stávající ghost a vytvoříme nový
            ghostController.DestroyGhost();
            ghostController.CreateGhost(setting.prefab);
        }

        private void SaveEditorState()
        {
            HexEditorState state = new HexEditorState();
            // Uložíme nastavení gridu
            if (gridManager != null)
            {
                state.hexSize = gridManager.hexSize;
                state.gridRange = gridManager.gridRange;
                state.placedHexes = gridManager.GetPlacedHexesData();
            }
            // Uložíme tile palette
            state.tileSettings = new List<HexTileSetting>();
            foreach (var tile in tilePalette)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile.prefab));
                HexTileSetting copy = new HexTileSetting
                {
                    tileName = tile.tileName,
                    prefab = tile.prefab,
                    layer = tile.layer,
                    prefabGUID = guid
                };
                state.tileSettings.Add(copy);
            }
            state.selectedTileIndex = selectedTileIndex;
            state.ghostRotationDeg = ghostController.RotationDegrees;
            state.currentMode = currentMode.ToString();
            state.heightStep = heightStep;
            state.brushSize = brushSize;

            EditorStateManager.SaveState(state);
        }

        private void LoadEditorState()
        {
            HexEditorState state = EditorStateManager.LoadState();
            if (state == null) return;
            if (gridManager != null)
            {
                Undo.RecordObject(gridManager, "Load Grid Settings");
                gridManager.hexSize = state.hexSize;
                gridManager.gridRange = state.gridRange;
                gridManager.LoadPlacedHexesData(state.placedHexes);
            }
            tilePalette.Clear();
            foreach (var t in state.tileSettings)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(t.prefabGUID));
                HexTileSetting copy = new HexTileSetting
                {
                    tileName = t.tileName,
                    prefab = prefab,
                    layer = t.layer,
                    prefabGUID = t.prefabGUID
                };
                tilePalette.Add(copy);
            }
            selectedTileIndex = state.selectedTileIndex;
            heightStep = state.heightStep;
            brushSize = state.brushSize;
            if (Enum.TryParse(state.currentMode, out EditorMode mode))
                currentMode = mode;
            else
                currentMode = EditorMode.AddRemove;
            if (currentMode == EditorMode.AddRemove)
                CreateOrReplaceGhost();
            else
                ghostController.DestroyGhost();
        }
    }
}
