#include "Data.hlsl"
#include "Matrix.hlsl"
#include "Utils.hlsl"

void ParticleUpdate (int index)
{
    if (index >= particleCount)
        return;
    
    Particle particle = Particles[index];

    if (particle.Material == PARTICLE_LIQUID)
    {
        float2x2 deviatoric = -1.0 * (particle.DeformationDisplacement + transpose(particle.DeformationDisplacement));
        particle.DeformationDisplacement += liquidViscosity * 0.5 * deviatoric;

        float alpha = 0.5 * (1.0 / particle.LiquidDensity - tr(particle.DeformationDisplacement) - 1.0);
        particle.DeformationDisplacement += liquidRelaxation * alpha * Identity;
    }
    else if (particle.Material == PARTICLE_ELASTIC || particle.Material == PARTICLE_VISCOUS)
    {
        float2x2 f = mul(Identity + particle.DeformationDisplacement, particle.DeformationGradient);

        SVDResult svdResult = svd(f);

        float df = det(f);
        float cdf = clamp(abs(df), 0.1, 1000);

        float2x2 q = (1 / (sign(df) * sqrt(cdf))) * f;

        float alpha = elasticityRatio;
        float2x2 tgt = alpha * mul(svdResult.U, svdResult.Vt) + (1 - alpha) * q;

        float2x2 diff = mul(tgt, inverse(particle.DeformationGradient)) - Identity - particle.DeformationDisplacement;
        particle.DeformationDisplacement += elasticRelaxation * diff;
    }
    else if (particle.Material == PARTICLE_SAND)
    {
        float2x2 f = mul(Identity + particle.DeformationDisplacement, particle.DeformationGradient);

        SVDResult svdResult = svd(f);

        if (particle.LogJp == 0)
        {
            svdResult.Sigma = clamp(svdResult.Sigma, float2(1, 1), float2(1000, 1000));
        }

        float df = det(f);
        float cdf = clamp(abs(df), 0.1, 1);
        float2x2 q = (1 / (sign(df) * sqrt(cdf))) * f;

        float alpha = elasticityRatio;
        float2x2 tgt = alpha * mul(mul(svdResult.U, float2x2(svdResult.Sigma.x, 0, 0, svdResult.Sigma.y)), svdResult.Vt) +
            (1 - alpha) * q;

        float2x2 diff = mul(tgt, inverse(particle.DeformationGradient)) - Identity - particle.DeformationDisplacement;
        particle.DeformationDisplacement += elasticRelaxation * diff;

        float2x2 deviatoric = -1.0 * (particle.DeformationDisplacement + transpose(particle.DeformationDisplacement));
        particle.DeformationDisplacement += liquidViscosity * 0.5 * deviatoric;
    }

    Particles[index] = particle;
}

void ParticleToGrid (int index)
{
    if (index >= particleCount)
        return;

    Particle particle = Particles[index];

    float2 p = particle.Position;
    float2 d = particle.Displacement;
    float2x2 dd = particle.DeformationDisplacement;
    float m = particle.Mass;
    
    QuadraticWeightInfo weightInfo = QuadraticWeightInit(p);

    for (uint dx = 0; dx < 3; dx++)
    {
        for (uint dy = 0; dy < 3; dy++)
        {
            float weight = weightInfo.Weights[dx].x * weightInfo.Weights[dy].y;
            
            uint2 neighbourCellIndex = uint2(weightInfo.CellIndex + int2(dx, dy));

            uint gridVertexIdx = GridVertexIndex(neighbourCellIndex, gridSize);

            if (gridVertexIdx >= gridVertexCount)
                continue;

            float2 offset = float2(neighbourCellIndex) - p + 0.5;

            float weightedMass = weight * m;
            float2 momentum = weightedMass * (d + mul(dd, offset));

            InterlockedAdd(Grid[gridVertexIdx], encodeFixedPoint(momentum.x, fixedPointMultiplier));
            InterlockedAdd(Grid[gridVertexIdx + 1], encodeFixedPoint(momentum.y, fixedPointMultiplier));
            InterlockedAdd(Grid[gridVertexIdx + 2], encodeFixedPoint(weightedMass, fixedPointMultiplier));

            if (useGridVolumeForLiquid != 0)
            {
                InterlockedAdd(Grid[gridVertexIdx + 3], encodeFixedPoint(particle.Volume * weight, fixedPointMultiplier));
            }
        }
    }
}

void GridToParticle (int index)
{
    if (index >= particleCount)
        return;
    
    Particle particle = Particles[index];

    float2 p = particle.Position;

    QuadraticWeightInfo weightInfo = QuadraticWeightInit(p);

    float2x2 b = ZeroMatrix;
    float2 d = float2(0, 0);
    float volume = 0.0;

    for (uint dx = 0; dx < 3; dx++)
    {
        for (uint dy = 0; dy < 3; dy++)
        {
            float weight = weightInfo.Weights[dx].x * weightInfo.Weights[dy].y;
            
            uint2 neighbourCellIndex = uint2(weightInfo.CellIndex + int2(dx, dy));
            uint gridVertexIdx = GridVertexIndex(neighbourCellIndex, gridSize);

            if (gridVertexIdx >= gridVertexCount)
                continue;

            float2 weightedDisplacement = weight * float2(
                decodeFixedPoint(Grid[gridVertexIdx], fixedPointMultiplier),
                decodeFixedPoint(Grid[gridVertexIdx + 1], fixedPointMultiplier)
            );

            float2 offset = neighbourCellIndex - p + 0.5;
            b += outerProduct(weightedDisplacement, offset);
            d += weightedDisplacement;

            if (useGridVolumeForLiquid != 0)
            {
                volume += weight * decodeFixedPoint(Grid[gridVertexIdx + 3], fixedPointMultiplier);
            }
        }
    }

    particle.DeformationDisplacement = b * 4.0;
    particle.Displacement = d;
    
    if (useGridVolumeForLiquid != 0)
    {
        volume = 1.0 / volume;

        if (volume < 1)
        {
            particle.LiquidDensity = lerp(particle.LiquidDensity, volume, 0.1f);
        }
    }

    Particles[index] = particle;
}

void ParticleIntegrate (int index)
{
    if (index >= particleCount)
        return;

    Particle particle = Particles[index];

    if (particle.Material == PARTICLE_LIQUID)
    {
        particle.LiquidDensity *= tr(particle.DeformationDisplacement) + 1.0;
        particle.LiquidDensity = max(particle.LiquidDensity, 0.1);
    }
    else
    {
        particle.DeformationGradient = mul(Identity + particle.DeformationDisplacement, particle.DeformationGradient);
    }

    if (particle.Material != PARTICLE_LIQUID)
    {
        SVDResult svdResult = svd(particle.DeformationGradient);

        svdResult.Sigma = clamp(svdResult.Sigma, float2(0.2f, 0.2f), float2(10000, 10000));

        if (particle.Material == PARTICLE_SAND)
        {
            float sinPhi = sin(frictionAngle / 180.0 * 3.14159);
            float alpha = sqrt(2.0/3.0) * 2.0 * sinPhi / (3.0 - sinPhi);
            float beta = 0.5;

            float2 eDiag = log(max(abs(svdResult.Sigma), 1e-6));

            float2x2 eps = diag(eDiag);
            float trace = tr(eps) + particle.LogJp;

            float2x2 eHat = eps - (trace / 2.0) * Identity;
            float frobNrm = length(float2(eHat[0][0], eHat[1][1]));

            if (trace >= 0)
            {
                svdResult.Sigma = float2(1, 1);
                particle.LogJp = beta * trace;
            }
            else
            {
                particle.LogJp = 0;
                float deltaGammaI = frobNrm + (elasticityRatio + 1) * trace * alpha;
                if (deltaGammaI > 0)
                {
                    float2 h = eDiag - deltaGammaI / frobNrm * (eDiag - (trace * 0.5));
                    svdResult.Sigma = exp(h);
                }
            }
        }
        else if (particle.Material == PARTICLE_VISCOUS)
        {
            float yieldSurface = exp(1 - plasticity);

            float j = svdResult.Sigma.x * svdResult.Sigma.y;

            float y = 1.0 / yieldSurface;
            svdResult.Sigma = clamp(svdResult.Sigma, float2(y, y), float2(yieldSurface, yieldSurface));

            float newJ = svdResult.Sigma.x * svdResult.Sigma.y;
            svdResult.Sigma *= sqrt(j / newJ);
        }

        particle.DeformationGradient = mul(mul(svdResult.U, diag(svdResult.Sigma)), svdResult.Vt);
    }

    particle.Position += particle.Displacement;

    particle.Displacement.y -= gridSize.y * gravityStrength * deltaTime * deltaTime;

    for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
    {
        Shape shape = Shapes[shapeIndex];

        CollideResult collideResult = Collide(shape, particle.Position);

        if (collideResult.collides)
        {
            particle.Displacement -= collideResult.penetration * collideResult.normal;
        }
    }
    
    particle.Position = ProjectInsideGuardian(particle.Position, gridSize, GUARDIAN_SIZE);

    Particles[index] = particle;
}
