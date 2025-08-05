#ifndef SHARED_TYPES_INCLUDED
#define SHARED_TYPES_INCLUDED

#define GUARDIAN_SIZE 3

cbuffer SimConstants : register(b0)
{
    int iterations;
    float liquidViscosity;
    float liquidRelaxation;
    float elasticityRatio;
    float elasticRelaxation;
    uint2 gridSize;
    uint useGridVolumeForLiquid;
    float frictionAngle;
    float plasticity;
    float gravityStrength;
    float fixedPointMultiplier;
    
    float deltaTime;
    int currentIteration;
    int particleCount;
    int shapeCount;
    uint gridVertexCount;
};

#define PARTICLE_LIQUID 0
#define PARTICLE_ELASTIC 1
#define PARTICLE_VISCOUS 2
#define PARTICLE_SAND 3

#define SHAPE_CIRCLE 0
#define SHAPE_BOX 1

struct Particle
{
    float2 Position;
    float2 Displacement;

    uint Material;

    float2x2 DeformationDisplacement;
    float2x2 DeformationGradient;

    float LiquidDensity;
    float LogJp;
    float Mass;
    float Volume;
};

struct Shape
{
    uint ShapeType;
    float2 Position;
    float Radius;
    float Rotation;
    float2 HalfSize;
};

RWStructuredBuffer<int> Grid;
RWStructuredBuffer<Particle> Particles;
StructuredBuffer<Shape> Shapes;

#endif