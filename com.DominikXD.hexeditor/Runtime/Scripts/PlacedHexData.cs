using System;

namespace Editor.Runtime
{
    /// <summary>
    /// Serializable class for storing data about a placed hex tile.
    /// Used for saving and loading.
    /// </summary>
    [Serializable]
    public class PlacedHexData
    {
        public int q;
        public int r;
        public string prefabGuid;
        public float rotationY;
        public float posX;
        public float posY;
        public float posZ;
        public int layer;
    }
}
