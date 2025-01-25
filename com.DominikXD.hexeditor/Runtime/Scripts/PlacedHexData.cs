using System;
namespace Editor.Runtime
{
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
        public int layer; // NEW: which layer this tile belongs to
    }
}