using System.Collections.Generic;

using System;

public enum EditorMode
{
    AddRemove,
    HeightMap
}
namespace Editor.Runtime
{

    [Serializable]
    public class HexEditorState
    {
        // For referencing GridMap (asset or scene object)
        public string gridMapGuid;       // If it's an asset
        public string gridMapSceneName;  // If it's a scene object
        public bool isGridMapSceneObject;

        // GridMap settings
        public float hexSize;
        public int gridRange;

        // The tile palette: we store (prefabGUID, layer, name)
        public List<HexTileSetting> tileSettings = new List<HexTileSetting>();

        // Editor window state
        public int selectedTileIndex;
        public float ghostRotationDeg;
        public string currentMode; // "AddRemove" or "HeightMap"
        public float heightStep;
        public int brushSize;

        // All placed hexes in the scene
        public List<PlacedHexData> placedHexes = new List<PlacedHexData>();
    }
}
