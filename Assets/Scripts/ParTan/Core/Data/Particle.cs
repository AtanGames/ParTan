using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace ParTan.Core.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Particle
    {
        public float2 Position;
        public float2 Displacement;
        
        public ParticleMaterial Material;

        public float2x2 DeformationDisplacement;
        public float2x2 DeformationGradient;
        
        public float LiquidDensity;
        
        public float LogJp;
        public float Mass;
        public float Volume;

        public Particle(float2 position, ParticleMaterial material, float mass)
        {
            Position = position;
            Displacement = float2.zero;
            DeformationGradient = float2x2.identity;
            DeformationDisplacement = float2x2.zero;
            LiquidDensity = 1f;
            Mass = mass;
            Material = material;
            Volume = 1f;
            LogJp = 1f;
        }
    }
    
    public enum ParticleMaterial
    {
        Liquid = 0,
        Elastic = 1,
        Viscous = 2,
        Sand = 3,
    }
}