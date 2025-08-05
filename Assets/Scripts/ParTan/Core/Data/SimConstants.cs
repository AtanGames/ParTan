using System;
using Unity.Mathematics;
using UnityEngine;

namespace ParTan.Core.Data
{
    [Serializable]
    public struct SimConstants
    {
        public int iterations;
        public float liquidViscosity;
        public float liquidRelaxation;
        public float elasticityRatio;
        public float elasticRelaxation;
        public int2 gridSize;
        public byte useGridVolumeForLiquid;
        public float frictionAngle;
        public float plasticity;
        public float gravityStrength;
        public int fixedPointMultiplier;

        public const int GuardianSize = 3;
    }
}