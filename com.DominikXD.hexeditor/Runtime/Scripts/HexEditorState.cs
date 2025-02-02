using System;
using System.Collections.Generic;

namespace Editor.Runtime
{
    /// <summary>
    /// Serializable class representing the complete state of the Hex Editor.
    /// </summary>
    [Serializable]
    public class HexEditorState
    {
        // GridMap reference information
        public string gridMapGuid;
        public string gridMapSceneName;
        public bool isGridMapSceneObject;

        // GridMap settings
        public float hexSize;
        public int gridRange;

        // Tile palette settings (list of HexTileSetting)
        public List<HexTileSetting> tileSettings = new List<HexTileSetting>();

        // Editor UI state
        public int selectedTileIndex;
        public float ghostRotationDeg;
        public string currentMode; // "AddRemove" or "HeightMap"
        public float heightStep;
        public int brushSize;

        // Data for all placed hex tiles (used for saving/loading)
        public List<PlacedHexData> placedHexes = new List<PlacedHexData>();
    }
}
