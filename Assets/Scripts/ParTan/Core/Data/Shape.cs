using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace ParTan.Core.Data
{
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct Shape
    {
        public ShapeType ShapeType;
        public float2 Position;
        public float Radius;
        public float Rotation;
        public float2 HalfSize;
    }
    
    public enum ShapeType
    {
        Circle = 0,
        Box = 1
    }
}