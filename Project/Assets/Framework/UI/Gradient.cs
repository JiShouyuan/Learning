﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/Gradient")]
public class Gradient : BaseMeshEffect
{
    public Color32 topColor = Color.white;
    public Color32 bottomColor = Color.black;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive())
            return;
        var count = vh.currentVertCount;
        if (count == 0) return;

        var vertexts = new List<UIVertex>();
        for (int i = 0; i < count; i++)
        {
            var vertex = new UIVertex();
            try
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertexts.Add(vertex);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Gradient.ModifyMesh: Set vertexs wrong! Error is " + ex.Message);
                return;
            }
        }

        var topY = vertexts[0].position.y;
        var bottomY = vertexts[0].position.y;
        for (int i = 1; i < count; i++)
        {
            var y = vertexts[i].position.y;
            if (y > topY) topY = y;
            else if (y < bottomY) bottomY = y;
        }
        var height = topY - bottomY;
        for (int i = 0; i < count; i++)
        {
            var vertex = vertexts[i];
            var color = Color32.Lerp(bottomColor, topColor, (vertex.position.y - bottomY) / height);
            vertex.color = color;
            vh.SetUIVertex(vertex, i);
        }
    }
}