using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Editor.Runtime;

namespace HexEditor
{
    public class HexGridManager : MonoBehaviour
    {
        [Header("Hex Grid Settings")]
        public float hexSize = 1.0f;
        public int gridRange = 10;

        // Ukládáme více hexů (v různých vrstvách) na jedné buňce
        private Dictionary<Vector2Int, List<PlacedHexInfo>> placedHexes = new Dictionary<Vector2Int, List<PlacedHexInfo>>();

        private void OnEnable()
        {
            // Zajistíme obrovský BoxCollider pro snadné raycastování ve scéně.
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.size = new Vector3(100000, 0, 100000);
            }
        }

        /// <summary>
        /// Umístí hex na buňku (q,r) do zadané vrstvy.
        /// Pokud je již hex ve stejné vrstvě, umístění se přeskočí.
        /// Nový hex se umístí tak, aby jeho spodní hrana seděla přesně na horní hraně hexu s nižší vrstvou.
        /// </summary>
        public void PlaceHex(Vector2Int qr, GameObject prefab, int newLayer, Quaternion rotation, string prefabGuid)
        {
            if (prefab == null) return;

            if (!placedHexes.TryGetValue(qr, out var stackedList))
            {
                stackedList = new List<PlacedHexInfo>();
                placedHexes[qr] = stackedList;
            }

            // Pokud už existuje hex ve stejné vrstvě, přeskočíme umístění.
            foreach (var existing in stackedList)
            {
                if (existing.instance != null && existing.layer == newLayer)
                {
                    Debug.Log($"Bunka {qr} již obsahuje hex ve vrstvě {newLayer}.");
                    return;
                }
            }

            // Vypočítáme maximální horní Y souřadnici mezi hexy s nižší vrstvou.
            float baseY = 0f;
            foreach (var existing in stackedList)
            {
                if (existing.instance == null) continue;
                if (existing.layer < newLayer)
                {
                    float topY = GetTileTopY(existing.instance);
                    baseY = Mathf.Max(baseY, topY);
                }
            }

            // Nový hex bude umístěn tak, že jeho spodní hrana (pivot) bude na hodnotě baseY.
            float finalY = baseY;
            Vector3 pos = AxialToWorld(qr.x, qr.y);
            pos.y = finalY;

            GameObject hexGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, this.transform);
            hexGO.transform.position = pos;
            hexGO.transform.rotation = rotation;
            hexGO.name = $"{prefab.name}_Layer{newLayer}_{qr.x}_{qr.y}";
            Undo.RegisterCreatedObjectUndo(hexGO, "Place Hex");

            // Přidáme collider, pokud jej prefab nemá.
            if (hexGO.GetComponent<Collider>() == null)
            {
                MeshCollider col = hexGO.AddComponent<MeshCollider>();
                col.convex = false;
            }

            // Uložíme informace o umístěném hexu.
            PlacedHexInfo info = new PlacedHexInfo()
            {
                prefabGUID = prefabGuid,
                instance = hexGO,
                rotationY = rotation.eulerAngles.y,
                layer = newLayer
            };

            stackedList.Add(info);
        }

        /// <summary>
        /// Zjistí horní hranu hexu (pokud je dostupný renderer).
        /// </summary>
        private float GetTileTopY(GameObject tile)
        {
            Renderer renderer = tile.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds.max.y;
            }
            return tile.transform.position.y;
        }

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

        public void ClearAllHexes()
        {
            foreach (var kvp in placedHexes)
            {
                foreach (var info in kvp.Value)
                {
                    if (info.instance != null)
                    {
                        Undo.DestroyObjectImmediate(info.instance);
                    }
                }
            }
            placedHexes.Clear();
        }

        public void RaiseLowerAllTiles(Vector2Int qr, float deltaY)
        {
            if (placedHexes.TryGetValue(qr, out var infoList))
            {
                foreach (var info in infoList)
                {
                    if (info.instance != null)
                    {
                        Undo.RecordObject(info.instance.transform, "Raise/Lower Hex");
                        info.instance.transform.position += new Vector3(0, deltaY, 0);
                    }
                }
            }
        }

        // Metody pro serializaci a převod mezi axiómickými a světem
        public List<PlacedHexData> GetPlacedHexesData()
        {
            var list = new List<PlacedHexData>();
            foreach (var kvp in placedHexes)
            {
                Vector2Int qr = kvp.Key;
                foreach (var info in kvp.Value)
                {
                    if (info.instance != null)
                    {
                        Vector3 pos = info.instance.transform.position;
                        float yrot = info.instance.transform.eulerAngles.y;
                        list.Add(new PlacedHexData
                        {
                            q = qr.x,
                            r = qr.y,
                            prefabGuid = info.prefabGUID,
                            rotationY = yrot,
                            posX = pos.x,
                            posY = pos.y,
                            posZ = pos.z,
                            layer = info.layer
                        });
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

                Vector2Int coord = new Vector2Int(data.q, data.r);
                if (!placedHexes.TryGetValue(coord, out var infoList))
                {
                    infoList = new List<PlacedHexInfo>();
                    placedHexes[coord] = infoList;
                }
                infoList.Add(new PlacedHexInfo
                {
                    prefabGUID = data.prefabGuid,
                    instance = hexGO,
                    rotationY = data.rotationY,
                    layer = data.layer
                });
            }
        }

        public Vector3 AxialToWorld(int q, int r)
        {
            float x = Mathf.Sqrt(3f) * (q + r / 2f) * hexSize;
            float z = 1.5f * r * hexSize;
            return new Vector3(x, 0, z);
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
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            if (rq + rr + rs != 0)
            {
                float dq = Mathf.Abs(rq - q);
                float dr = Mathf.Abs(rr - r);
                float ds = Mathf.Abs(rs - s);

                if (dq > dr && dq > ds) rq = -rr - rs;
                else if (dr > ds) rr = -rq - rs;
                else rs = -rq - rr;
            }
            return new Vector2Int(rq, rr);
        }

        private void OnDrawGizmos()
        {
            // Vykreslíme mřížku hexagonů na základě středu kamery.
            SceneView sceneView = SceneView.currentDrawingSceneView;
            if (sceneView == null) return;
            Camera cam = sceneView.camera;
            if (cam == null) return;
            Vector3 camCenter = GetCameraCenterOnGrid(cam);
            Vector2Int centerQR = WorldToAxial(camCenter);

            for (int dq = -gridRange; dq <= gridRange; dq++)
            {
                for (int dr = -gridRange; dr <= gridRange; dr++)
                {
                    int dist = HexDistance(0, 0, dq, dr);
                    if (dist <= gridRange)
                    {
                        int q = centerQR.x + dq;
                        int r = centerQR.y + dr;
                        Vector3 pos = AxialToWorld(q, r);
                        Gizmos.color = Color.white;
                        DrawHexWire(pos, hexSize);
                    }
                }
            }
        }

        private int HexDistance(int q1, int r1, int q2, int r2)
        {
            int x1 = q1, z1 = r1, y1 = -q1 - r1;
            int x2 = q2, z2 = r2, y2 = -q2 - r2;
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

        private Vector3 HexCorner(int index, float radius)
        {
            float angleDeg = 60f * index - 30f;
            float angleRad = Mathf.Deg2Rad * angleDeg;
            return new Vector3(radius * Mathf.Cos(angleRad), 0, radius * Mathf.Sin(angleRad));
        }

        private Vector3 GetCameraCenterOnGrid(Camera cam)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
            {
                return ray.GetPoint(dist);
            }
            return Vector3.zero;
        }
    }
}
