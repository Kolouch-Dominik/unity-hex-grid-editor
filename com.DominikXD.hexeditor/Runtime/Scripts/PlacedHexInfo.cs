using System;

using UnityEngine;
namespace Editor.Runtime
{
    [Serializable]
    public class PlacedHexInfo
    {
        public string prefabGUID;
        public GameObject instance;
        public float rotationY;
        public int layer;
    }
}