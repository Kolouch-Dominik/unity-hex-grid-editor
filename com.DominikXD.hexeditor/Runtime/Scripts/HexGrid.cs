using Editor.Runtime;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(BoxCollider))]
    public class GridMap : MonoBehaviour
    {
        [Header("Hex Settings")]
        public float hexSize = 1.0f;
        public int gridRange = 10;

        // Storing multiple tiles at each cell
        private Dictionary<Vector2Int, List<PlacedHexInfo>> placedHexes
            = new Dictionary<Vector2Int, List<PlacedHexInfo>>();

        private void OnEnable()
        {
            var collider = GetComponent<BoxCollider>();
            collider.size = new Vector3(100000, 0, 100000);
        }

        /// <summary>
        /// Place a tile at (q,r) with a given layer. 
        /// - If the same layer already exists, skip (or you could replace).
        /// - Otherwise, we stack on top of all tiles with layer < newLayer.
        /// </summary>
        public void PlaceHex(Vector2Int qr, GameObject prefab, int newLayer,
                             Quaternion rotation, string prefabGuid)
        {
            if (prefab == null) return;

            if (!placedHexes.TryGetValue(qr, out var stackedList))
            {
                stackedList = new List<PlacedHexInfo>();
                placedHexes[qr] = stackedList;
            }

            // 1) Check if there's already a tile with the same layer
            bool sameLayerFound = false;
            float baseY = 0f;
            foreach (var existingInfo in stackedList)
            {
                if (existingInfo.instance == null) continue;

                // If we already have exactly the same layer, skip
                if (existingInfo.layer == newLayer)
                {
                    sameLayerFound = true;
                }

                // We only stack on top if existing layer < newLayer
                if (existingInfo.layer < newLayer)
                {
                    float topY = existingInfo.instance.transform.position.y;
                    if (topY > baseY) baseY = topY;
                }
            }

            if (sameLayerFound)
            {
                Debug.Log($"[GridMap] Cell {qr} already has a tile in layer {newLayer}, skipping placement.");
                return;
            }

            // 2) Decide how high above that base we want to place
            // If you want an absolute ground at y=0 for layer=0, you can do:
            // if (newLayer == 0) baseY = 0f; // ensure floor always at 0
            // but that's optional. For now, let's just do an offset:
            float layerOffset = 0.5f; // how high each new layer is stacked
            float finalY = baseY + layerOffset;

            // 3) Create the object
            Vector3 pos = AxialToWorld(qr.x, qr.y);
            pos.y = finalY;

            GameObject hexGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, this.transform);
            hexGO.transform.position = pos;
            hexGO.transform.rotation = rotation;
            hexGO.name = $"{prefab.name}_Layer{newLayer}_{qr.x}_{qr.y}";

            Undo.RegisterCreatedObjectUndo(hexGO, "Place Hex");

            // Ensure a collider
            if (hexGO.GetComponent<Collider>() == null)
            {
                MeshCollider col = hexGO.AddComponent<MeshCollider>();
                col.convex = false;
            }

            var info = new PlacedHexInfo()
            {
                prefabGUID = prefabGuid,
                instance = hexGO,
                rotationY = rotation.eulerAngles.y,
                layer = newLayer
            };
            stackedList.Add(info);
        }

        /// <summary>
        /// Remove ALL tiles at (q,r).
        /// If you only want to remove the tile in the same layer, you'd filter here.
        /// </summary>
        public void RemoveHex(Vector2Int qr)
        {
            if (placedHexes.TryGetValue(qr, out var infoList))
            {
                foreach (var info in infoList)
                {
                    if (info.instance != null)
                    {
                        Undo.DestroyObjectImmediate(info.instance);
                    }
                }
                placedHexes.Remove(qr);
            }
        }

        /// <summary>
        /// Clears everything
        /// </summary>
        public void ClearAllHexes()
        {
            foreach (var kvp in placedHexes)
            {
                var infoList = kvp.Value;
                foreach (var info in infoList)
                {
                    if (info.instance != null)
                    {
                        Undo.DestroyObjectImmediate(info.instance);
                    }
                }
            }
            placedHexes.Clear();
        }

        /// <summary>
        /// Raise/lower ALL stacked tiles at (q,r).
        /// </summary>
        public void RaiseLowerAllTiles(Vector2Int qr, float deltaY)
        {
            if (placedHexes.TryGetValue(qr, out var infoList))
            {
                foreach (var info in infoList)
                {
                    if (info.instance)
                    {
                        Undo.RecordObject(info.instance.transform, "Raise/Lower Hex (Stacked)");
                        Vector3 pos = info.instance.transform.position;
                        pos.y += deltaY;
                        info.instance.transform.position = pos;
                    }
                }
            }
        }

        // -------------------------------------------------------------------
        // Save/Load multiple tiles
        // -------------------------------------------------------------------
        public List<PlacedHexData> GetPlacedHexesData()
        {
            var list = new List<PlacedHexData>();
            foreach (var kvp in placedHexes)
            {
                Vector2Int qr = kvp.Key;
                var infoList = kvp.Value;

                foreach (var info in infoList)
                {
                    if (info.instance != null)
                    {
                        Vector3 pos = info.instance.transform.position;
                        float yrot = info.instance.transform.eulerAngles.y;

                        var data = new PlacedHexData()
                        {
                            q = qr.x,
                            r = qr.y,
                            prefabGuid = info.prefabGUID,
                            rotationY = yrot,
                            posX = pos.x,
                            posY = pos.y,
                            posZ = pos.z,
                            layer = info.layer
                        };
                        list.Add(data);
                    }
                }
            }
            return list;
        }

        public void LoadPlacedHexesData(List<PlacedHexData> loadedList)
        {
            ClearAllHexes();

            foreach (var data in loadedList)
            {
                string path = AssetDatabase.GUIDToAssetPath(data.prefabGuid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                Quaternion rot = Quaternion.Euler(0, data.rotationY, 0);

                GameObject hexGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, this.transform);
                hexGO.transform.position = new Vector3(data.posX, data.posY, data.posZ);
                hexGO.transform.rotation = rot;
                hexGO.name = $"Hex_{data.q}_{data.r}_L{data.layer}";

                Undo.RegisterCreatedObjectUndo(hexGO, "Load Hex");

                var coord = new Vector2Int(data.q, data.r);
                if (!placedHexes.TryGetValue(coord, out var infoList))
                {
                    infoList = new List<PlacedHexInfo>();
                    placedHexes[coord] = infoList;
                }

                var info = new PlacedHexInfo()
                {
                    prefabGUID = data.prefabGuid,
                    instance = hexGO,
                    rotationY = data.rotationY,
                    layer = data.layer
                };
                infoList.Add(info);
            }
        }

        // ------------------------------------------------
        // Axial <-> World
        // ------------------------------------------------
        public Vector3 AxialToWorld(int q, int r)
        {
            float x = Mathf.Sqrt(3f) * (q + r / 2f) * hexSize;
            float z = 1.5f * r * hexSize;
            return new Vector3(x, 0f, z);
        }

        public Vector2Int WorldToAxial(Vector3 pos)
        {
            float adjustedX = pos.x / hexSize;
            float adjustedZ = pos.z / hexSize;

            float qf = (Mathf.Sqrt(3f) / 3f * adjustedX) - (adjustedZ / 3f);
            float rf = (2f / 3f * adjustedZ);
            return AxialRound(qf, rf);
        }

        private Vector2Int AxialRound(float q, float r)
        {
            float s = -q - r;
            int rx = Mathf.RoundToInt(q);
            int ry = Mathf.RoundToInt(r);
            int rz = Mathf.RoundToInt(s);

            if (rx + ry + rz != 0)
            {
                float dq = Mathf.Abs(rx - q);
                float dr = Mathf.Abs(ry - r);
                float ds = Mathf.Abs(rz - s);

                if (dq > dr && dq > ds) rx = -ry - rz;
                else if (dr > ds) ry = -rx - rz;
                else rz = -rx - ry;
            }
            return new Vector2Int(rx, ry);
        }

        // ------------------------------------------------
        // Draw "Hex wireframe" in Scene
        // ------------------------------------------------
        private void OnDrawGizmos()
        {
            SceneView sceneView = SceneView.currentDrawingSceneView;
            if (sceneView == null) return;

            Camera cam = sceneView.camera;
            if (cam == null) return;

            Vector3 cameraCenter = GetCameraCenterOnGrid(cam);
            Vector2Int centerQR = WorldToAxial(cameraCenter);

            for (int dq = -gridRange; dq <= gridRange; dq++)
            {
                for (int dr = -gridRange; dr <= gridRange; dr++)
                {
                    int dist = HexDistance(0, 0, dq, dr);
                    if (dist <= gridRange)
                    {
                        int q = centerQR.x + dq;
                        int r = centerQR.y + dr;
                        Vector3 cpos = AxialToWorld(q, r);

                        Gizmos.color = Color.white;
                        DrawHexWire(cpos, hexSize);
                    }
                }
            }
        }

        private int HexDistance(int q1, int r1, int q2, int r2)
        {
            int x1 = q1; int z1 = r1; int y1 = -x1 - z1;
            int x2 = q2; int z2 = r2; int y2 = -x2 - z2;
            return (Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2) + Mathf.Abs(z1 - z2)) / 2;
        }

        private void DrawHexWire(Vector3 center, float radius)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 p1 = center + HexCorner(i, radius);
                Vector3 p2 = center + HexCorner(i + 1, radius);
                Gizmos.DrawLine(p1, p2);
            }
        }

        private Vector3 HexCorner(int cornerIndex, float radius)
        {
            float angle_deg = 60f * cornerIndex - 30f;
            float angle_rad = angle_deg * Mathf.Deg2Rad;
            float x = radius * Mathf.Cos(angle_rad);
            float z = radius * Mathf.Sin(angle_rad);
            return new Vector3(x, 0f, z);
        }

        private Vector3 GetCameraCenterOnGrid(Camera cam)
        {
            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(centerRay, out float dist))
            {
                return centerRay.GetPoint(dist);
            }
            return Vector3.zero;
        }
    }
}
