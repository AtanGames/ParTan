using ParTan.Core;
using ParTan.Core.Data;
using UnityEngine;

namespace ParTan.Testing
{
    public class ParticleSpawner : MonoBehaviour
    {
        public int particleCount;
        public Vector2 boundingBoxSize = new Vector2(10f, 10f);

        public ParticleMaterial material;
        public float mass;
        
        private void Start()
        {
            ParTanEngine.ParTanStart += ParTanStart;
        }

        private void ParTanStart()
        {
            for (int i = 0; i < particleCount; i++)
            {
                Vector2 randomPosition = new Vector2(
                    Random.Range(-boundingBoxSize.x / 2, boundingBoxSize.x / 2),
                    Random.Range(-boundingBoxSize.y / 2, boundingBoxSize.y / 2)
                );
                
                ParTanEngine.SpawnParticle(randomPosition + (Vector2)transform.position, material, mass);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireCube(transform.position, boundingBoxSize);
        }
    }
}