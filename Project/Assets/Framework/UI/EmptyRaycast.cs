﻿using UnityEngine.UI;
public class EmptyRaycast : MaskableGraphic
{
    protected EmptyRaycast()
    {
        useLegacyMeshGeneration = false;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
}
