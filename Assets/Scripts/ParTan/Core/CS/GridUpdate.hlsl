#include "Data.hlsl"
#include "Utils.hlsl"

void GridZero (uint index)
{
    if (index >= gridSize.x * gridSize.y)
        return;
    
    uint baseIndex = index * 4;

    Grid[baseIndex] = 0.0f;
    Grid[baseIndex + 1] = 0.0f;
    Grid[baseIndex + 2] = 0.0f;
    Grid[baseIndex + 3] = 0.0f;
}

void GridUpdate (uint index)
{
    if (index >= gridSize.x * gridSize.y)
        return;

    uint2 gridPosition = GridVertexFromIndex(index, gridSize);
    uint gridVertexAddress = index * 4;

    float dx = decodeFixedPoint(Grid[gridVertexAddress], fixedPointMultiplier);
    float dy = decodeFixedPoint(Grid[gridVertexAddress + 1], fixedPointMultiplier);
    
    float w = decodeFixedPoint(Grid[gridVertexAddress + 2], fixedPointMultiplier);

    if (w < 1e-5f)
    {
        dx = 0;
        dy = 0;
    }

    dx /= w;
    dy /= w;

    float2 gridDisplacement = float2(dx, dy);
    float2 displacedGridPosition = gridPosition + gridDisplacement;

    for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
    {
        Shape shape = Shapes[shapeIndex];

        CollideResult collideResult = Collide(shape, displacedGridPosition);

        if (collideResult.collides)
        {
            float gap = min(0, dot(collideResult.normal, collideResult.pointOnCollider - gridPosition));
            float penetration = dot(collideResult.normal, gridDisplacement) - gap;

            float radialImpulse = max(penetration, 0);
            gridDisplacement -= radialImpulse * collideResult.normal;
        }
    }

    float2 projectedGridPosition = ProjectInsideGuardian(displacedGridPosition, gridSize, GUARDIAN_SIZE + 1);
    float2 projectedDifference = projectedGridPosition - displacedGridPosition;

    if (projectedDifference.x != 0 || projectedDifference.y != 0)
    {
        gridDisplacement.x = 0;
        gridDisplacement.y = 0;
    }

    Grid[gridVertexAddress] = encodeFixedPoint(gridDisplacement.x, fixedPointMultiplier);
    Grid[gridVertexAddress + 1] = encodeFixedPoint(gridDisplacement.y, fixedPointMultiplier);
}