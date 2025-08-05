using System.Runtime.InteropServices;
using ParTan.Core.Data;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ParTan.Core.CS
{
    public class ParTanGpuSimulation : ISimulation
    {
        public static int PARTICLE_LIMIT = 1000;
        public static int SHAPE_LIMIT = 10;
        
        private static readonly int Iterations = Shader.PropertyToID("iterations");
        private static readonly int LiquidViscosity = Shader.PropertyToID("liquidViscosity");
        private static readonly int LiquidRelaxation = Shader.PropertyToID("liquidRelaxation");
        private static readonly int ElasticityRatio = Shader.PropertyToID("elasticityRatio");
        private static readonly int ElasticRelaxation = Shader.PropertyToID("elasticRelaxation");
        private static readonly int GridSize = Shader.PropertyToID("gridSize");
        private static readonly int UseGridVolumeForLiquid = Shader.PropertyToID("useGridVolumeForLiquid");
        private static readonly int FrictionAngle = Shader.PropertyToID("frictionAngle");
        private static readonly int Plasticity = Shader.PropertyToID("plasticity");
        private static readonly int GravityStrength = Shader.PropertyToID("gravityStrength");
        private static readonly int FixedPointMultiplier = Shader.PropertyToID("fixedPointMultiplier");
        
        private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
        private static readonly int CurrentIteration = Shader.PropertyToID("currentIteration");
        private static readonly int ParticleCount = Shader.PropertyToID("particleCount");
        private static readonly int GridVertexCount = Shader.PropertyToID("gridVertexCount");
        private static readonly int ShapeCount = Shader.PropertyToID("shapeCount");
        
        private static readonly int ParticlesId = Shader.PropertyToID("Particles");
        private static readonly int GridId = Shader.PropertyToID("Grid");
        private static readonly int ShapesId = Shader.PropertyToID("Shapes");

        private int ParticleUpdate => _shader.FindKernel("ParticleUpdateK");
        private int GridZero => _shader.FindKernel("GridZeroK");
        private int ParticleToGrid => _shader.FindKernel("ParticleToGridK");
        private int GridUpdate => _shader.FindKernel("GridUpdateK");
        private int GridToParticle => _shader.FindKernel("GridToParticleK");
        private int ParticleIntegrate => _shader.FindKernel("ParticleIntegrateK");
        
        private ComputeShader _shader;
        private PartanConfig _config;
        
        private ComputeBuffer _particleBuffer;
        private ComputeBuffer _gridBuffer;
        private ComputeBuffer _shapeBuffer;

        private NativeArray<Particle> _particlesNative;
        private NativeArray<Shape> _shapesNative;
        private Particle[] _particlesTemp;
        
        private int _particleCount = 0;
        private int _gridVertexCount = 0;
        private int _shapeCount = 0;
        
        public ParTanGpuSimulation(ComputeShader shader, PartanConfig config)
        {
            _shader = shader;
            _config = config;
            
            _particlesNative = new NativeArray<Particle>(PARTICLE_LIMIT, Allocator.Persistent);
            _shapesNative = new NativeArray<Shape>(SHAPE_LIMIT, Allocator.Persistent);
            _particlesTemp = new Particle[PARTICLE_LIMIT];
            
            _gridVertexCount = config.simConstants.gridSize.x * config.simConstants.gridSize.y * 4;
            
            int particleStride = Marshal.SizeOf(typeof(Particle));
            int shapeStride = Marshal.SizeOf(typeof(Shape));
            
            _particleBuffer = new ComputeBuffer(PARTICLE_LIMIT, particleStride, ComputeBufferType.Structured);
            _gridBuffer = new ComputeBuffer(_gridVertexCount, sizeof(int), ComputeBufferType.Structured);
            _shapeBuffer = new ComputeBuffer(SHAPE_LIMIT, shapeStride, ComputeBufferType.Structured);

            for (int i = 0; i < _shapesNative.Length; i++)
            {
                var shape = _shapesNative[i];
                shape.Position = new float2(-100, -100);
                _shapesNative[i] = shape;
            }
            
            _shapeBuffer.SetData(_shapesNative);
            
            _shader.SetBuffer(ParticleUpdate, ParticlesId, _particleBuffer);
            
            _shader.SetBuffer(GridZero, GridId, _gridBuffer);
            
            _shader.SetBuffer(ParticleToGrid, ParticlesId, _particleBuffer);
            _shader.SetBuffer(ParticleToGrid, GridId, _gridBuffer);
            
            _shader.SetBuffer(GridUpdate, GridId, _gridBuffer);
            _shader.SetBuffer(GridUpdate, ShapesId, _shapeBuffer);
            
            _shader.SetBuffer(GridToParticle, ParticlesId, _particleBuffer);
            _shader.SetBuffer(GridToParticle, GridId, _gridBuffer);
            
            _shader.SetBuffer(ParticleIntegrate, ParticlesId, _particleBuffer);
            _shader.SetBuffer(ParticleIntegrate, ShapesId, _shapeBuffer);
            
            UpdateParams();
        }

        private void UpdateParams()
        {
            var sim = _config.simConstants;
            
            _shader.SetInt(Iterations, sim.iterations);
            _shader.SetFloat(LiquidViscosity, sim.liquidViscosity);
            _shader.SetFloat(LiquidRelaxation, sim.liquidRelaxation);
            _shader.SetFloat(ElasticityRatio, sim.elasticityRatio);
            _shader.SetFloat(ElasticRelaxation, sim.elasticRelaxation);
            _shader.SetInts(GridSize, sim.gridSize.x, sim.gridSize.y);
            _shader.SetInt(UseGridVolumeForLiquid, sim.useGridVolumeForLiquid);
            _shader.SetFloat(FrictionAngle, sim.frictionAngle);
            _shader.SetFloat(Plasticity, sim.plasticity);
            _shader.SetFloat(GravityStrength, sim.gravityStrength);
            _shader.SetFloat(FixedPointMultiplier, Mathf.Pow(10, sim.fixedPointMultiplier));
            
            _shader.SetInt(GridVertexCount, sim.gridSize.x * sim.gridSize.y * 4);
        }

        public void StartSolve()
        {
            _particleBuffer.GetData(_particlesTemp);
            _particlesNative.CopyFrom(_particlesTemp);
        }

        public void EndSolve(float deltaTime)
        {
            UpdateParams();
            
            _shader.SetFloat(DeltaTime, deltaTime);
            _shader.SetInt(ParticleCount, _particleCount);
            _shader.SetInt(ShapeCount, _shapeCount);

            int gridThreadGroup = Mathf.CeilToInt((_gridVertexCount / 64f) / 4f);
            int particleThreadGroup = Mathf.CeilToInt(_particleCount / 64f);
            
            for (int i = 0; i < _config.simConstants.iterations; i++)
            {
                _shader.SetFloat(CurrentIteration, i);
                
                _shader.Dispatch(ParticleUpdate, particleThreadGroup, 1, 1);
                _shader.Dispatch(GridZero, gridThreadGroup, 1, 1);
                _shader.Dispatch(ParticleToGrid, particleThreadGroup, 1, 1);
                _shader.Dispatch(GridUpdate, gridThreadGroup, 1, 1);
                _shader.Dispatch(GridToParticle, particleThreadGroup, 1, 1);
            }
            
            _shader.Dispatch(ParticleIntegrate, particleThreadGroup, 1, 1);
        }

        public NativeArray<Particle> GetParticles()
        {
            return _particlesNative;
        }

        public int GetParticleCount()
        {
            return _particleCount;
        }

        public void AddParticle(Particle particle)
        {
            if (_particleCount >= PARTICLE_LIMIT)
            {
                Debug.LogWarning("Particle limit reached, cannot add more particles.");
                return;
            }
            
            _particlesNative[_particleCount] = particle;
            _particleCount++;
            
            _particleBuffer.SetData(_particlesNative);
        }

        public int AddShape(Shape shape)
        {
            _shapesNative[_shapeCount] = shape;
            _shapeCount++;
            
            _shapeBuffer.SetData(_shapesNative);
            
            return _shapeCount - 1;
        }

        public void UpdateShape(int index, Shape shape)
        {
            _shapesNative[index] = shape;
            _shapeBuffer.SetData(_shapesNative);
        }

        public void Dispose()
        {
            if (_particlesNative.IsCreated)
                _particlesNative.Dispose();
            
            if (_shapesNative.IsCreated)
                _shapesNative.Dispose();
            
            _particleBuffer?.Release();
            
            _gridBuffer?.Release();
            
            _shapeBuffer?.Release();
        }
    }
}