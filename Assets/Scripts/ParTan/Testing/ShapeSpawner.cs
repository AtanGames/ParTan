using System;
using ParTan.Core;
using ParTan.Core.Data;
using Unity.Mathematics;
using UnityEngine;

namespace ParTan.Testing
{
    [ExecuteInEditMode]
    public class ShapeSpawner : MonoBehaviour
    {
        public Shape shape;
        public Color circleColor = new Color(0.2f, 0.7f, 1f, 1f);
        public Color boxColor = new Color(1f, 0.8f, 0.2f, 1f);

        private int _index;
        
        private void Start()
        {
            ParTanEngine.ParTanStart += ParTanStart;
        }

        private void Update()
        {
            shape.Position = new float2(transform.position.x, transform.position.y);
        }

        private void ParTanStart()
        {
            _index = ParTanEngine.SpawnShape(shape);
            
            ParTanEngine.ParTanUpdate += ParTanUpdate;
        }

        private void ParTanUpdate()
        {
            ParTanEngine.UpdateShape(_index, shape);
        }
        
        private void OnDrawGizmos()
        {
            DrawShape(shape);
        }

        private void DrawShape(Shape s)
        {
            Vector3 pos = new Vector3(s.Position.x, s.Position.y, 0f);
            float angleDeg = -s.Rotation;
            Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg);

            switch (s.ShapeType)
            {
                case ShapeType.Circle:
                    Gizmos.color = circleColor;
                    Matrix4x4 prev = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one * s.Radius * 2f);
                    Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
                    Gizmos.matrix = prev;
                    break;

                case ShapeType.Box:
                    Gizmos.color = boxColor;
                    Vector3 fullSize = new Vector3(s.HalfSize.x * 2f, s.HalfSize.y * 2f, 1f);
                    prev = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(pos, rot, fullSize);
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                    Gizmos.matrix = prev;
                    break;
            }
        }
    }
}