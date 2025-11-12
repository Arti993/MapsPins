using System;
using UnityEngine;

[Serializable]
public class PinData
{
    public string name;
    public string description;
    public Texture2D image;
    public Vector2 mapPosition;
    
    public PinData(string name, string description, Texture2D image, Vector2 mapPosition)
    {
        this.name = name;
        this.description = description;
        this.image = image;
        this.mapPosition = mapPosition;
    }
}
