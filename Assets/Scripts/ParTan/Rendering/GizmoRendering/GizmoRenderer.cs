using System;
using System.Collections.Generic;
using ParTan.Core;
using ParTan.Core.Data;
using Unity.Collections;
using UnityEngine;

namespace ParTan.Rendering.GizmoRendering
{
    public class GizmoRenderer : MonoBehaviour
    {
        public float particleSize = 0.1f;

        public Color liquidColor;
        public Color elasticColor;
        public Color viscousColor;
        public Color sandColor;

        public PartanConfig config;
        
        private NativeArray<Particle> _particleData;
        
        private List<SphereDraw> _sphereDraws;
        private int _particleCount;
        
        private struct SphereDraw
        {
            public Color Color;
            public Vector2 Position;
        }
        
        private void Start()
        {
            _sphereDraws = new List<SphereDraw>();
            
            ParTanEngine.ParTanUpdate += ParTanUpdate;
        }

        private void ParTanUpdate()
        {
            _particleData = ParTanEngine.GetParticles();
            _particleCount = ParTanEngine.GetParticleCount();
            _sphereDraws.Clear();
            
            for (int i = 0; i < _particleCount; i++)
            {
                _sphereDraws.Add(new SphereDraw()
                {
                    Color = _particleData[i].Material switch
                    {
                        ParticleMaterial.Liquid => liquidColor,
                        ParticleMaterial.Elastic => elasticColor,
                        ParticleMaterial.Viscous => viscousColor,
                        ParticleMaterial.Sand => sandColor,
                        _ => Color.magenta
                    },
                    Position = _particleData[i].Position
                });
            }
        }

        private void OnDrawGizmos()
        {
            var fullMin = Vector2.zero;
            var fullSize = new Vector2(config.simConstants.gridSize.x, config.simConstants.gridSize.y);
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(fullMin + fullSize * 0.5f, fullSize);

            var inset = SimConstants.GuardianSize;
            var innerMin = fullMin + new Vector2(inset, inset);
            var innerSize = fullSize - new Vector2(inset * 2f, inset * 2f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(innerMin + innerSize * 0.5f, innerSize);
            
            if (!Application.isPlaying)
                return;
            
            for (var i = 0; i < _sphereDraws.Count; i++)
            {
                var sphereDraw = _sphereDraws[i];
                Gizmos.color = sphereDraw.Color;

                Gizmos.DrawSphere(sphereDraw.Position, particleSize);
            }
        }
    }
}