using ParTan.Core.Data;
using ParTan.Core.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
// ReSharper disable ForCanBeConvertedToForeach

namespace ParTan.Core.Jobs
{
    [BurstCompile]
    public struct SolverJob : IJob
    {
        public SimConstants SimConstants;
        
        public NativeArray<Particle> Particles;
        public NativeArray<float> Grid;
        public NativeArray<Shape> Shapes;

        public float DeltaTime;

        public void Execute()
        {
            for (int i = 0; i < SimConstants.iterations; i++)
            {
                ParticleUpdatePbmpm();
                GridZero();
                ParticlesToGrid();
                GridUpdate();
                GridToParticle();
            }

            MpmParticleIntegrate();
        }

        private void ParticleUpdatePbmpm()
        {
            for (int i = 0; i < Particles.Length; i++)
            {
                var particle = Particles[i];

                if (particle.Material == ParticleMaterial.Liquid)
                {
                    var deviatoric = -1f * (particle.DeformationDisplacement + math.transpose(particle.DeformationDisplacement));
                    particle.DeformationDisplacement += SimConstants.liquidViscosity * 0.5f * deviatoric;

                    var alpha = 0.5f * (1f / particle.LiquidDensity - Atrix.Tr(particle.DeformationDisplacement) - 1f);
                    particle.DeformationDisplacement += SimConstants.liquidRelaxation * alpha * float2x2.identity;
                }
                else if (particle.Material == ParticleMaterial.Elastic || particle.Material == ParticleMaterial.Viscous)
                {
                    var f = math.mul(float2x2.identity + particle.DeformationDisplacement, particle.DeformationGradient);
                    
                    var svdResult = Atrix.Svd(f);
                    
                    float df = Atrix.Det(f);
                    float cdf = math.clamp(math.abs(df), 0.1f, 1000f);
                    
                    var q = (1f / (math.sign(df) * math.sqrt(cdf))) * f;
                    
                    var alpha = SimConstants.elasticityRatio;
                    float2x2 tgt = alpha * math.mul(svdResult.U, svdResult.Vt) + (1f - alpha) * q;
                    
                    var diff = (math.mul(tgt, Atrix.Inverse(particle.DeformationGradient)) - float2x2.identity) - particle.DeformationDisplacement;
                    particle.DeformationDisplacement += SimConstants.elasticRelaxation * diff;
                }
                else if (particle.Material == ParticleMaterial.Sand)
                {
                    var f = math.mul(float2x2.identity + particle.DeformationDisplacement, particle.DeformationGradient);
                    
                    var svdResult = Atrix.Svd(f);

                    if (particle.LogJp == 0)
                    {
                        svdResult.Sigma = math.clamp(svdResult.Sigma, new float2(1f, 1f), new float2(1000f, 1000f));
                    }
                    
                    var df = Atrix.Det(f);
                    float cdf = math.clamp(math.abs(df), 0.1f, 1f);
                    var q = (1f / (math.sign(df) * math.sqrt(cdf))) * f;
                    
                    var alpha = SimConstants.elasticityRatio;
                    var tgt = alpha * (math.mul(math.mul(svdResult.U, Atrix.MatColumnMajor(svdResult.Sigma.x, 0, 0, svdResult.Sigma.y)),
                                       svdResult.Vt)) + (1f - alpha) * q;
                    
                    var diff = (math.mul(tgt, Atrix.Inverse(particle.DeformationGradient)) - float2x2.identity) -
                               particle.DeformationDisplacement;
                    particle.DeformationDisplacement += SimConstants.elasticRelaxation * diff;

                    var deviatoric = -1f * (particle.DeformationDisplacement +
                                            math.transpose(particle.DeformationDisplacement));
                    particle.DeformationDisplacement += SimConstants.liquidViscosity * 0.5f * deviatoric;
                }
                
                Particles[i] = particle;
            }
        }

        private void GridZero()
        {
            for (int i = 0; i < Grid.Length; i++)
            {
                Grid[i] = 0f;
            }
        }
        
        private void ParticlesToGrid()
        {
            for (int i = 0; i < Particles.Length; i++)
            {
                var particle = Particles[i];

                var p = particle.Position;
                var d = particle.Displacement;
                var dd = particle.DeformationDisplacement;
                var m = particle.Mass;

                var weightInfo = Atrix.QuadraticWeightInit(p);

                for (int dx = 0; dx < 3; dx++)
                {
                    for (int dy = 0; dy < 3; dy++)
                    {
                        var weight = weightInfo.Weights[dx].x * weightInfo.Weights[dy].y;
                        
                        var neighbourCellIndex = weightInfo.CellIndex + new int2(dx, dy);

                        var gridVertexIdx = Atrix.GridVertexIndex(neighbourCellIndex, SimConstants.gridSize);

                        if (gridVertexIdx < 0 || gridVertexIdx >= Grid.Length)
                        {
                            continue;
                        }
                        
                        var offset = (float2)neighbourCellIndex - p + 0.5f;
                        
                        var weightedMass = weight * m;
                        var momentum = weightedMass * (d + math.mul(dd, offset));

                        Grid[gridVertexIdx] += momentum.x;
                        Grid[gridVertexIdx + 1] += momentum.y;
                        Grid[gridVertexIdx + 2] += weightedMass;

                        if (SimConstants.useGridVolumeForLiquid != 0)
                        {
                            Grid[gridVertexIdx + 3] += particle.Volume * weight;
                        }
                    }
                }
            }
        }
        
        private void GridUpdate()
        {
            for (int x = 0; x < SimConstants.gridSize.x; x++)
            {
                for (int y = 0; y < SimConstants.gridSize.y; y++)
                {
                    var gridPosition = new int2(x, y);

                    var gridVertexAddress = Atrix.GridVertexIndex(gridPosition, SimConstants.gridSize);

                    var dx = Grid[gridVertexAddress];
                    var dy = Grid[gridVertexAddress + 1];
                    var w = Grid[gridVertexAddress + 2];

                    if (w < 1e-5f)
                    {
                        dx = 0f;
                        dy = 0f;
                    }

                    dx /= w;
                    dy /= w;
                    
                    var gridDisplacement = new float2(dx, dy);
                    var displacedGridPosition = gridPosition + gridDisplacement;

                    for (int shapeIndex = 0; shapeIndex < Shapes.Length; shapeIndex++)
                    {
                        var shape = Shapes[shapeIndex];
                        
                        var collideResult = Atrix.Collide(shape, displacedGridPosition);

                        if (collideResult.Collides != 0)
                        {
                            var gap = math.min(0,
                                math.dot(collideResult.Normal, collideResult.PointOnCollider - gridPosition));
                            var penetration = math.dot(collideResult.Normal, gridDisplacement) - gap;

                            var radialImpulse = math.max(penetration, 0f);
                            gridDisplacement -= radialImpulse * collideResult.Normal;
                        }
                    }

                    var projectedGridPosition = Atrix.ProjectInsideGuardian(displacedGridPosition,
                        SimConstants.gridSize, SimConstants.GuardianSize + 1);
                    var projectedDifference = projectedGridPosition - displacedGridPosition;

                    if (projectedDifference.x != 0f)
                    {
                        gridDisplacement.x = 0f;
                        gridDisplacement.y = 0f;
                    }
                    
                    if (projectedDifference.y != 0f)
                    {
                        gridDisplacement.x = 0f;
                        gridDisplacement.y = 0f;
                    }
                    
                    Grid[gridVertexAddress] = gridDisplacement.x;
                    Grid[gridVertexAddress + 1] = gridDisplacement.y;
                }
            }
        }
        
        private void GridToParticle()
        {
            for (int i = 0; i < Particles.Length; i++)
            {
                var particle = Particles[i];
                
                var p = particle.Position;

                var weightInfo = Atrix.QuadraticWeightInit(p);

                var b = float2x2.zero;
                var d = float2.zero;
                var volume = 0f;

                for (int dx = 0; dx < 3; dx++)
                {
                    for (int dy = 0; dy < 3; dy++)
                    {
                        var weight = weightInfo.Weights[dx].x * weightInfo.Weights[dy].y;
                        
                        var neighbourCellIndex = (int2)weightInfo.CellIndex + new int2(dx, dy);
                        
                        var gridVertexIdx = Atrix.GridVertexIndex(neighbourCellIndex, SimConstants.gridSize);
                        
                        if (gridVertexIdx < 0 || gridVertexIdx >= Grid.Length)
                        {
                            continue;
                        }
                        
                        var weightedDisplacement = weight * new float2(Grid[gridVertexIdx], 
                            Grid[gridVertexIdx + 1]);
                        
                        var offset = neighbourCellIndex - p + 0.5f;
                        b += Atrix.OuterProduct(weightedDisplacement, offset);
                        d += weightedDisplacement;
                        
                        if (SimConstants.useGridVolumeForLiquid != 0)
                        {
                            volume += weight * Grid[gridVertexIdx + 3];
                        }
                    }
                }

                particle.DeformationDisplacement = b * 4f;
                particle.Displacement = d;
                
                if (SimConstants.useGridVolumeForLiquid != 0)
                {
                    volume = 1f / volume;

                    if (volume < 1f)
                    {
                        particle.LiquidDensity = math.lerp(particle.LiquidDensity, volume, 0.1f);
                    }
                }
                
                Particles[i] = particle;
            }
        }
        
        private void MpmParticleIntegrate()
        {
            for (int i = 0; i < Particles.Length; i++)
            {
                var particle = Particles[i];

                if (particle.Material == ParticleMaterial.Liquid)
                {
                    particle.LiquidDensity *= (Atrix.Tr(particle.DeformationDisplacement) + 1f);
                    
                    particle.LiquidDensity = math.max(particle.LiquidDensity, 0.1f);
                }
                else
                {
                    particle.DeformationGradient = math.mul(float2x2.identity + particle.DeformationDisplacement, particle.DeformationGradient);
                }

                if (particle.Material != ParticleMaterial.Liquid)
                {
                    var svdResult = Atrix.Svd(particle.DeformationGradient);

                    svdResult.Sigma = math.clamp(svdResult.Sigma, new float2(0.2f, 0.2f), new float2(10000f, 10000f));

                    if (particle.Material == ParticleMaterial.Sand)
                    {
                        var sinPhi = math.sin(SimConstants.frictionAngle / 180f * math.PI);
                        var alpha = math.sqrt(2f / 3f) * 2f * sinPhi / (3f - sinPhi);
                        var beta = 0.5f;

                        var eDiag = math.log(math.max(math.abs(svdResult.Sigma), new float2(1e-6f, 1e-6f)));

                        var eps = Atrix.Diag(eDiag);
                        var trace = Atrix.Tr(eps) + particle.LogJp;
                        
                        var eHat = eps - (trace / 2f) * float2x2.identity;
                        var frobNrm = math.length(new float2(eHat[0][0], eHat[1][1]));

                        if (trace >= 0f)
                        {
                            svdResult.Sigma = new float2(1f, 1f);
                            particle.LogJp = beta * trace;
                        }
                        else
                        {
                            particle.LogJp = 0;
                            var deltaGammaI = frobNrm + (SimConstants.elasticityRatio + 1f) * trace * alpha;
                            if (deltaGammaI > 0f)
                            {
                                var h = eDiag - deltaGammaI / frobNrm * (eDiag - (trace * 0.5f));
                                svdResult.Sigma = math.exp(h);
                            }
                        }
                    }
                    else if (particle.Material == ParticleMaterial.Viscous)
                    {
                        var yieldSurface = math.exp(1 - SimConstants.plasticity);
                        
                        var j = svdResult.Sigma.x * svdResult.Sigma.y;

                        svdResult.Sigma = math.clamp(svdResult.Sigma, new float2(1f / yieldSurface),
                            new float2(yieldSurface));
                        
                        var newJ = svdResult.Sigma.x * svdResult.Sigma.y;
                        svdResult.Sigma *= math.sqrt(j / newJ);
                    }
                    
                    particle.DeformationGradient = math.mul(svdResult.U, math.mul(Atrix.Diag(svdResult.Sigma), svdResult.Vt));
                }

                particle.Position += particle.Displacement;

                particle.Displacement.y -=
                    SimConstants.gridSize.y * SimConstants.gravityStrength * DeltaTime * DeltaTime;

                for (int shapeIndex = 0; shapeIndex < Shapes.Length; shapeIndex++)
                {
                    var shape = Shapes[shapeIndex];

                    var collideResult = Atrix.Collide(shape, particle.Position);

                    if (collideResult.Collides == 0) 
                        continue;
                    
                    particle.Displacement -= collideResult.Penetration * collideResult.Normal;
                }

                particle.Position = Atrix.ProjectInsideGuardian(particle.Position, SimConstants.gridSize, SimConstants.GuardianSize);
                
                Particles[i] = particle;
            }
        }
    }
}