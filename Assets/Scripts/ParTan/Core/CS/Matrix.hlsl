#ifndef MATRIX_LOGIC_INCLUDED
#define MATRIX_LOGIC_INCLUDED

struct SVDResult
{
    float2x2 U;
    float2 Sigma;
    float2x2 Vt;
};

static const float2x2 Identity = float2x2(1.0, 0.0, 0.0, 1.0);
static const float2x2 ZeroMatrix = float2x2(0.0, 0.0, 0.0, 0.0);

inline float det(float2x2 m)
{
    return m[0][0] * m[1][1] - m[1][0] * m[0][1];
}

inline float tr(float2x2 m)
{
    return m[0][0] + m[1][1];
}

inline float2x2 rot(float theta)
{
    float ct = cos(theta);
    float st = sin(theta);
    return float2x2(ct, -st,
                    st,  ct);
}

inline float2x2 inverse(float2x2 m)
{
    float a = m[0][0];
    float b = m[0][1];
    float c = m[1][0];
    float d = m[1][1];
    return (1.0 / det(m)) * float2x2(d, -b,
                                     -c, a);
}

inline float2x2 outerProduct(float2 x, float2 y)
{
    return float2x2(x.x * y.x, x.x * y.y,
                    x.y * y.x, x.y * y.y );
}

inline float2x2 diag(float2 d)
{
    return float2x2(d.x, 0.0,
                    0.0, d.y);
}

inline SVDResult svd(float2x2 m)
{
    float E = (m[0][0] + m[1][1]) * 0.5;
    float F = (m[0][0] - m[1][1]) * 0.5;
    float G = (m[1][0] + m[0][1]) * 0.5;
    float H = (m[1][0] - m[0][1]) * 0.5;

    float Q = sqrt(E * E + H * H);
    float R = sqrt(F * F + G * G);
    float sx = Q + R;
    float sy = Q - R;
    
    float a1 = 0.0f;
    
    if (R > 1e-5f)
        a1 = atan2(G, F);
    
    float a2 = 0.0f;

    if (Q > 1e-5f)
        a2 = atan2(H, E);

    float theta = (a2 - a1) * 0.5;
    float phi = (a2 + a1) * 0.5;

    float2x2 U = rot(phi);
    float2 Sigma = float2(sx, sy);
    float2x2 Vt = rot(theta);

    SVDResult res;
    res.U = U;
    res.Sigma = Sigma;
    res.Vt = Vt;
    return res;
}

#endif