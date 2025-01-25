using System;
using UnityEngine;

namespace Editor.Runtime
{
    [Serializable]
    public class HexPrefabSetting
    {
        public string name;           // optional label
        public GameObject prefab;     // the actual prefab
        public int layer;             // e.g. 0 = floor, 1 = building, etc.

        // Store a GUID for saving/loading if needed:
        public string prefabGUID;
    }
}