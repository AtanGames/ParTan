using System;
using ParTan.Core.Data;
using ParTan.Core.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ParTan.Core
{
    public class ParTanCpuSimulation : IDisposable, ISimulation
    {
        private NativeList<Particle> _particles;
        private NativeArray<float> _grid;
        private NativeList<Shape> _shapes;
        
        private SolverJob _solverJob;
        private readonly PartanConfig _config;
        private JobHandle _handle;
        
        public ParTanCpuSimulation(PartanConfig config)
        {
            _particles = new NativeList<Particle>(Allocator.Persistent);
            _grid = new NativeArray<float>(config.simConstants.gridSize.x * config.simConstants.gridSize.y * 4, Allocator.Persistent);
            _shapes = new NativeList<Shape>(Allocator.Persistent);
            
            _config = config;
            
            _solverJob = new SolverJob
            {
                Particles = _particles.AsDeferredJobArray(),
                Grid = _grid,
                Shapes = _shapes.AsDeferredJobArray()
            };
            
            UpdateConfig();
        }

        private void UpdateConfig()
        {
            _solverJob.SimConstants = _config.simConstants;
        }
        
        public void StartSolve()
        {
            _handle.Complete();
        }
        
        public void EndSolve (float deltaTime)
        {
            UpdateConfig();
            _solverJob.DeltaTime = deltaTime;
            
            _handle = _solverJob.Schedule();
        }

        public NativeArray<Particle> GetParticles()
        {
            return _particles.AsArray();
        }

        public int GetParticleCount()
        {
            return _particles.Length;
        }

        public void AddParticle(Particle particle)
        {
            _particles.Add(particle);
        }

        public int AddShape(Shape shape)
        {
            _shapes.Add(shape);
            return _shapes.Length - 1;
        }

        public void UpdateShape(int index, Shape shape)
        {
            _shapes[index] = shape;
        }

        public void Dispose()
        {
            _handle.Complete();
            
            if (_particles.IsCreated)
                _particles.Dispose();

            if (_grid.IsCreated)
                _grid.Dispose();
            
            if (_shapes.IsCreated)
                _shapes.Dispose();
        }
    }
}