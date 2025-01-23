<<<<<<< Updated upstream
﻿using System.Collections.Generic;

using System;

public enum EditorMode
{
    AddRemove,
    HeightMap
}

[Serializable]
public class HexEditorState
{
    // reference to gridmap (guid) 
    public string gridMapGuid;

    public float hexSize;
    public int gridRange;

    public List<string> prefabGuids = new List<string>();

    public int selectedTileIndex;
    public float ghostRotationDeg;
    public string currentMode; // "AddRemove" or "HeightMap"
    public float heightStep;
    public int brushSize;

    public List<PlacedHexData> placedHexes = new List<PlacedHexData>();
}
=======
﻿using System.Collections.Generic;

using System;

public enum EditorMode
{
    AddRemove,
    HeightMap
}

[Serializable]
public class HexEditorState
{
    // reference to gridmap (guid) 
    public string gridMapGuid;

    public float hexSize;
    public int gridRange;

    public List<string> prefabGuids = new List<string>();

    public int selectedTileIndex;
    public float ghostRotationDeg;
    public string currentMode; // "AddRemove" or "HeightMap"
    public float heightStep;
    public int brushSize;

    public List<PlacedHexData> placedHexes = new List<PlacedHexData>();

}
>>>>>>> Stashed changes
