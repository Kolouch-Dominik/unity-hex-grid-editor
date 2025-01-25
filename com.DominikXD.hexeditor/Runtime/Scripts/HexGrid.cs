
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
    [Tooltip("Velikost poloměru hexu (střed -> roh).")]
    public float hexSize = 1.0f;

    [Tooltip("Poloměr vykreslované mřížky kolem kamery.")]
    public int gridRange = 10;

    private Dictionary<Vector2Int, PlacedHexInfo> placedHexes
        = new Dictionary<Vector2Int, PlacedHexInfo>();

    private void OnEnable()
    {
        var collider = GetComponent<BoxCollider>();
        collider.size = new Vector3(100000, 0, 100000);
    }

    /// <summary>
    /// Place hex to grid if its not there already
    /// </summary>
    public void PlaceHex(Vector2Int qr, GameObject prefab, Quaternion rotation, string prefabGuid)
    {
        if (prefab == null) return;
        if (placedHexes.ContainsKey(qr)) return;

        Vector3 pos = AxialToWorld(qr.x, qr.y);

        // For Undo/Redo support, it is convenient to use PrefabUtility.InstantiatePrefab
        // If it's purely an editor tool, then Instantiate can also do.
        GameObject hexGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, this.transform);
        hexGO.transform.position = pos;
        hexGO.transform.rotation = rotation;
        hexGO.name = $"Hex_{qr.x}_{qr.y}";

        Undo.RegisterCreatedObjectUndo(hexGO, "Place Hex");

        if (hexGO.GetComponent<Collider>() == null)
        {
            MeshCollider col = hexGO.AddComponent<MeshCollider>();
            col.convex = false;
        }

        var info = new PlacedHexInfo()
        {
            prefabGUID = prefabGuid,
            instance = hexGO,
            rotationY = rotation.eulerAngles.y
        };

        placedHexes[qr] = info;
    }

    public void RemoveHex(Vector2Int qr)
    {
        if (placedHexes.TryGetValue(qr, out PlacedHexInfo info))
        {
            // Undo/Redo
            Undo.DestroyObjectImmediate(info.instance);

            placedHexes.Remove(qr);
        }
    }

    public void ClearAllHexes()
    {
        foreach (var kvp in placedHexes)
        {
            if (kvp.Value.instance != null)
            {
                Undo.DestroyObjectImmediate(kvp.Value.instance);
            }
        }
        placedHexes.Clear();
    }

    public bool TryGetPlacedHex(Vector2Int qr, out GameObject hexGO)
    {
        hexGO = null;
        if (placedHexes.TryGetValue(qr, out PlacedHexInfo info))
        {
            hexGO = info.instance;
            return (hexGO != null);
        }
        return false;
    }

    // ------------------------------------------------
    // Coord conversions
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
    // Draw Gizmos (hex wireframe)
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

    /// <summary>
    /// Get data for placed hex for json serialization
    /// </summary>
    public List<PlacedHexData> GetPlacedHexesData()
    {
        var list = new List<PlacedHexData>();
        foreach (var kvp in placedHexes)
        {
            Vector2Int qr = kvp.Key;
            PlacedHexInfo info = kvp.Value;
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
                    posZ = pos.z
                };
                list.Add(data);
            }
        }
        return list;
    }

    /// <summary>
    /// Load PlacedHexData and create gameobjects in grid
    /// </summary>
    public void LoadPlacedHexesData(List<PlacedHexData> loadedList)
    {
        ClearAllHexes();

        foreach (var data in loadedList)
        {
            string path = AssetDatabase.GUIDToAssetPath(data.prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Quaternion rot = Quaternion.Euler(0, data.rotationY, 0);
            // We can use AxialToWorld instead of posX/posZ, but
            // data.posX/posY/posZ can already contain "manual" height offset.
            // To preserve the exact location, we'll use the data directly.
            Vector3 customPos = new Vector3(data.posX, data.posY, data.posZ);

            GameObject hexGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, this.transform);
            hexGO.transform.position = customPos;
            hexGO.transform.rotation = rot;
            hexGO.name = $"Hex_{data.q}_{data.r}";

            Undo.RegisterCreatedObjectUndo(hexGO, "Load Hex");

            var info = new PlacedHexInfo()
            {
                prefabGUID = data.prefabGuid,
                instance = hexGO,
                rotationY = data.rotationY
            };

            placedHexes[new Vector2Int(data.q, data.r)] = info;
        }
    }
}
}