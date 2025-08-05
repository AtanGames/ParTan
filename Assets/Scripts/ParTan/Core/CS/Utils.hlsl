#ifndef UTILS_INCLUDED
#define UTILS_INCLUDED

struct QuadraticWeightInfo
{
    float2 Weights[3];
    int2 CellIndex;
};

struct CollideResult
{
    bool collides;
    float penetration;
    float2 normal;
    float2 pointOnCollider;
};

float2 pow2 (float2 x)
{
    return x * x;
}

QuadraticWeightInfo QuadraticWeightInit (float2 position)
{
    float2 roundDownPos = floor(position);
    float2 offset = (position - roundDownPos) - 0.5f;

    QuadraticWeightInfo res;
    
    res.Weights[0] = 0.5 * pow2(0.5 - offset);
    res.Weights[1] = 0.75 - pow2(offset);
    res.Weights[2] = 0.5 * pow2(0.5 + offset);
    
    res.CellIndex = int2(roundDownPos - 1);

    return res;
}

uint GridVertexIndex (uint2 gridVertex, uint2 gridSize)
{
    return (gridVertex.y * gridSize.x + gridVertex.x) * 4;
}

uint2 GridVertexFromIndex(uint index, uint2 gridSize)
{
    uint y = index / gridSize.x;
    uint x = index % gridSize.x;
    return uint2(x, y);
}

float decodeFixedPoint(int fixedPoint, float fixedPointMultiplier)
{
    return fixedPoint / fixedPointMultiplier;
}

int encodeFixedPoint(float floatingPoint, float fixedPointMultiplier)
{
    return floatingPoint * fixedPointMultiplier;
}

float2 ProjectInsideGuardian (float2 p, int2 gridSize, int guardianSize)
{
    float2 clampMin = float2(guardianSize, guardianSize);
    float2 clampMax = gridSize - clampMin - float2(1, 1);

    return clamp(p, clampMin, clampMax);
}

CollideResult Collide (Shape shape, float2 pos)
{
    CollideResult result;
    
    if (shape.ShapeType == SHAPE_CIRCLE)
    {
        float2 offset = shape.Position - pos;
        float offsetLen = length(offset);
        float2 normal = offset * (offsetLen == 0 ? 0 : 1.0 / offsetLen);

        result.collides = offsetLen <= shape.Radius;
        result.penetration = -(offsetLen - shape.Radius);
        result.normal = normal;
        result.pointOnCollider = shape.Position + normal * shape.Radius;

        return result;
    }

    if (shape.ShapeType == SHAPE_BOX)
    {
        float2 offset = pos - shape.Position;
        float2x2 r = rot(shape.Rotation / 180 * 3.14159);
        float2 rotOffset = mul(r, offset);
        float sx = sign(rotOffset.x);
        float sy = sign(rotOffset.y);
        float2 penetration = -(abs(rotOffset) - shape.HalfSize);
        float2 normal = mul(transpose(r), penetration.y < penetration.x ? float2(0, sy) : float2(sx, 0));
        float minPen = min(penetration.x, penetration.y);

        float2 pointOnBox = shape.Position + mul(transpose(r), clamp(rotOffset, -shape.HalfSize, shape.HalfSize));

        result.collides = minPen > 0;
        result.penetration = minPen;
        result.normal = -normal;
        result.pointOnCollider = pointOnBox;

        return result;
    }

    result.collides = false;
    result.penetration = 0;
    result.normal = float2(0, 0);
    result.pointOnCollider = float2(0, 0);
    
    return result;
}

#endif
