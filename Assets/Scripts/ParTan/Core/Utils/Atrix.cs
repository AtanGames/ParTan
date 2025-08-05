using ParTan.Core.Data;
using Unity.Mathematics;
using UnityEngine;

namespace ParTan.Core.Utils
{
    public static class Atrix
    {
        public struct SvdResult
        {
            public float2x2 U;
            public float2 Sigma;
            public float2x2 Vt;
        }
        
        public struct QuadraticWeightInfo
        {
            public float2x3 Weights;
            public int2 CellIndex;
        }
        
        public struct CollideResult
        {
            public byte Collides;
            public float Penetration;
            public float2 Normal;
            public float2 PointOnCollider;
        }
        
        public static float2x2 MatColumnMajor(float a, float b, float c, float d)
        {
            return new float2x2(a, c, b, d);
        }
        
        public static float Tr(float2x2 m) => m[0][0] + m[1][1];
        
        public static float Det(float2x2 m) => m[0][0] * m[1][1] - m[0][1] * m[1][0];
        
        public static SvdResult Svd (float2x2 m)
        {
            float E = (m[0][0] + m[1][1]) * 0.5f;
            float F = (m[0][0] - m[1][1]) * 0.5f;
            float G = (m[0][1] + m[1][0]) * 0.5f;
            float H = (m[0][1] - m[1][0]) * 0.5f;

            float Q = math.sqrt(E * E + H * H);
            float R = math.sqrt(F * F + G * G);
            float sx = Q + R;
            float sy = Q - R;

            float a1 = math.atan2(G, F);
            float a2 = math.atan2(H, E);

            float theta = 0.5f * (a2 - a1);
            float phi = 0.5f * (a2 + a1);

            float2x2 U = Rot(phi);
            float2x2 Vt = Rot(theta);
            float2 Sigma = new float2(sx, sy);

            return new SvdResult { U = U, Sigma = Sigma, Vt = Vt };
        }
        
        public static float2x2 Rot(float angle)
        {
            float ct = math.cos(angle);
            float st = math.sin(angle);
            return MatColumnMajor(ct, st, -st, ct);
        }

        public static float2x2 Inverse(float2x2 m)
        {
            var a = m[0][0];
            var b = m[1][0];
            var c = m[0][1];
            var d = m[1][1];
            
            return (1f / Det(m)) * MatColumnMajor(d, -c, -b, a);
        }
        
        public static QuadraticWeightInfo QuadraticWeightInit(float2 position)
        {
            var roundDownPosition = math.floor(position);
            var offset = (position - roundDownPosition) - 0.5f;
            
            return new QuadraticWeightInfo()
            {
                Weights = new float2x3(0.5f * math.pow(0.5f - offset, 2f),
                    0.75f - math.pow(offset, 2f), 
                    0.5f * math.pow(0.5f + offset, 2f)),
                
                CellIndex = (int2)roundDownPosition - 1
            };
        }

        public static int GridVertexIndex(int2 gridVertex, int2 gridSize)
        {
            return 4 * (gridVertex.y * gridSize.x + gridVertex.x);
        }

        public static CollideResult Collide(Shape shape, float2 pos)
        {
            if (shape.ShapeType == ShapeType.Circle)
            {
                var offset = shape.Position - pos;
                var offsetLen = math.length(offset);
                var normal = offset * (offsetLen == 0 ? 0f : 1f / offsetLen);
                return new CollideResult()
                {
                    Collides = (offsetLen <= shape.Radius ? (byte)1 : (byte)0),
                    Penetration = -(offsetLen - shape.Radius),
                    Normal = normal,
                    PointOnCollider = shape.Position + normal * shape.Radius
                };
            }
            
            if (shape.ShapeType == ShapeType.Box)
            {
                var offset = pos - shape.Position;
                var r = Rot(shape.Rotation / 180f * math.PI);
                var rotOffset = math.mul(r, offset);
                var sx = math.sign(rotOffset.x);
                var sy = math.sign(rotOffset.y);
                var penetration = -(math.abs(rotOffset) - shape.HalfSize);
                var normal = math.mul(math.transpose(r), (penetration.y < penetration.x ? new float2(0, sy) : new float2(sx, 0)));
                var minPen = math.min(penetration.x, penetration.y);

                var pointOnBox = shape.Position +
                                 math.mul(math.transpose(r), math.clamp(rotOffset, -shape.HalfSize, shape.HalfSize));

                return new CollideResult()
                {
                    Collides = (minPen > 0 ? (byte)1 : (byte)0),
                    Penetration = minPen,
                    Normal = -normal,
                    PointOnCollider = pointOnBox
                };
            }

            return new CollideResult()
            {
                Collides = 0,
                Penetration = 0f,
                Normal = float2.zero,
                PointOnCollider = float2.zero
            };
        }

        public static float2 ProjectInsideGuardian(float2 p, int2 gridSize, int guardianSize)
        {
            var clampMin = new float2(guardianSize, guardianSize);
            var clampMax = gridSize - clampMin - new float2(1, 1);

            return math.clamp(p, clampMin, clampMax);
        }

        public static float2x2 OuterProduct(float2 x, float2 y)
        {
            return new float2x2(x * y.x, x * y.y);
        }
        
        public static float2x2 Diag (float2 d)
        {
            return new float2x2(d.x, 0f, 0f, d.y);
        }

        public static float DecodeFixedPoint(int fixedPoint, int fixedPointMultiplier)
        {
            return fixedPoint / (float)fixedPointMultiplier;
        }
        
        public static int EncodeFixedPoint(float value, int fixedPointMultiplier)
        {
            return Mathf.RoundToInt(value * fixedPointMultiplier);
        }
    }
}