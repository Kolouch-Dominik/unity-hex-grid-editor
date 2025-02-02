using System;
using UnityEngine;

namespace Editor.Runtime
{
    /// <summary>
    /// Serializable class representing a tile (hex) setting in the palette.
    /// </summary>
    [Serializable]
    public class HexTileSetting
    {
        public string tileName;
        public GameObject prefab;
        public int layer;
        public string prefabGUID;
    }
}
