using System;
using UnityEngine;

namespace Editor.Runtime
{
    /// <summary>
    /// Serializable class for runtime information about a placed hex tile.
    /// </summary>
    [Serializable]
    public class PlacedHexInfo
    {
        public string prefabGUID;
        public GameObject instance;
        public float rotationY;
        public int layer;
    }
}
