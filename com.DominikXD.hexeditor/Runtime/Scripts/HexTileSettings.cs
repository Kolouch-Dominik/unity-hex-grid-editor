using System;
using UnityEngine;

namespace Editor.Runtime
{
    [Serializable]
    public class HexTileSetting
    {
        public string tileName;
        public GameObject prefab;
        public int layer;

        // Optional: store a GUID for saving/loading the prefab
        public string prefabGUID;
    }
}