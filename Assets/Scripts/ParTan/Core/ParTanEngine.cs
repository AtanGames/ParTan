using System;
using ParTan.Core.CS;
using ParTan.Core.Data;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ParTan.Core
{
    public class ParTanEngine : MonoBehaviour
    {
        public enum EngineType
        {
            Cpu,
            Gpu
        }
        
        private static ParTanEngine _instance;
        private ISimulation _simulation;

        public PartanConfig config;
        public EngineType engine = EngineType.Cpu;
        public ComputeShader gpuSolver;
        
        public static Action ParTanStart;
        public static Action ParTanUpdate;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Multiple instances of ParTanEngine detected. Destroying the new instance.");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;

            if (engine == EngineType.Cpu)
                _simulation = new ParTanCpuSimulation(config);
            else
                _simulation = new ParTanGpuSimulation(gpuSolver, config);
            
            ParTanUpdate = null;
        }

        private void OnDestroy()
        {
            _simulation?.Dispose();
        }

        private void FixedUpdate()
        {
            _simulation.StartSolve();
            
            ParTanStart?.Invoke();
            ParTanStart = null;
            ParTanUpdate?.Invoke();
            
            _simulation.EndSolve(Time.fixedDeltaTime);
        }
        
        public static NativeArray<Particle> GetParticles()
        {
            return _instance._simulation.GetParticles();
        }

        public static void SpawnParticle(Vector2 position, ParticleMaterial material, float mass = 1f)
        {
            Particle newParticle = new Particle(position, material, mass);
                
            _instance._simulation.AddParticle(newParticle);
        }

        public static int SpawnShape(Shape shape)
        {
            return _instance._simulation.AddShape(shape);
        }

        public static void UpdateShape(int index, Shape shape)
        {
            _instance._simulation.UpdateShape(index, shape);
        }

        public static int GetParticleCount()
        {
            return _instance._simulation.GetParticleCount();
        }
    }
}