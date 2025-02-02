using UnityEngine;
using UnityEditor;

namespace Editor.Runtime
{
    public class GhostController
    {
        public GameObject GhostObject { get; private set; }
        private float ghostRotationDeg = 0f;

        public float RotationDegrees => ghostRotationDeg;

        public void CreateGhost(GameObject prefab)
        {
            DestroyGhost();
            if (prefab == null) return;
            GhostObject = Object.Instantiate(prefab);
            GhostObject.name = "GhostHexPreview";
            GhostObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            DisableColliders(GhostObject);
            ApplyGhostMaterial(GhostObject);
        }

        public void UpdateGhost(Vector3 position, float rotation)
        {
            if (GhostObject == null) return;
            GhostObject.SetActive(true);
            GhostObject.transform.position = position;
            GhostObject.transform.rotation = Quaternion.Euler(0, rotation, 0);
        }

        public void RotateGhost(float deltaDegrees)
        {
            ghostRotationDeg = (ghostRotationDeg + deltaDegrees) % 360f;
            if (GhostObject != null)
            {
                GhostObject.transform.rotation = Quaternion.Euler(0, ghostRotationDeg, 0);
            }
        }

        public void DestroyGhost()
        {
            if (GhostObject != null)
            {
                Object.DestroyImmediate(GhostObject);
                GhostObject = null;
            }
        }

        private void DisableColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }

        private void ApplyGhostMaterial(GameObject go)
        {
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                Material ghostMat = new Material(renderer.sharedMaterial);
                Color col = ghostMat.color;
                col.a = 0.5f;
                ghostMat.color = col;
                ghostMat.SetFloat("_Mode", 3);
                ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostMat.SetInt("_ZWrite", 0);
                ghostMat.DisableKeyword("_ALPHATEST_ON");
                ghostMat.EnableKeyword("_ALPHABLEND_ON");
                ghostMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                ghostMat.renderQueue = 3000;
                renderer.sharedMaterial = ghostMat;
            }
        }
    }
}
