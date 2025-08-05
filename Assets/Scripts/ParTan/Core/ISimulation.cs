using System;
using ParTan.Core.Data;
using Unity.Collections;
using UnityEngine;

namespace ParTan.Core
{
    public interface ISimulation : IDisposable
    {
        public void StartSolve();
        public void EndSolve(float deltaTime);

        public NativeArray<Particle> GetParticles();
        public int GetParticleCount();
        public void AddParticle(Particle particle);
        
        public int AddShape(Shape shape);
        public void UpdateShape(int index, Shape shape);
    }
}