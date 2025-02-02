using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Editor.Runtime;

namespace Editor.Runtime
{
    public class EditorStateManager
    {
        private static string DATA_FILE_PATH = "Assets/Editor/HexEditorData.json";

        public static void SaveState(HexEditorState state)
        {
            try
            {
                string json = JsonUtility.ToJson(state, true);
                string dir = Path.GetDirectoryName(DATA_FILE_PATH);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(DATA_FILE_PATH, json);
                Debug.Log("Editor state saved to " + DATA_FILE_PATH);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error saving editor state: " + ex.Message);
            }
        }

        public static HexEditorState LoadState()
        {
            if (!File.Exists(DATA_FILE_PATH)) return null;
            try
            {
                string json = File.ReadAllText(DATA_FILE_PATH);
                HexEditorState state = JsonUtility.FromJson<HexEditorState>(json);
                Debug.Log("Editor state loaded from " + DATA_FILE_PATH);
                return state;
            }
            catch (Exception ex)
            {
                Debug.LogError("Error loading editor state: " + ex.Message);
                return null;
            }
        }
    }
}
