#ifndef COMMON_HLSL
#define COMMON_HLSL

#define G 9.81f
#define PI 3.14159265358f
#define PI2	6.28318530717f

inline float2 ComplexMul(float2 lhs, float2 rhs)
{
	return float2(
		lhs.x * rhs.x - lhs.y * rhs.y,
		lhs.x * rhs.y + lhs.y * rhs.x
		);
}


inline float2 WaveVector(float n,float m,float Len,float N)
{
	n -= 0.5f;
	m -= 0.5f;
	n = ((n < N * 0.5f) ? n : n - Len);
	m = ((m < N * 0.5f) ? m : m - Len);
	return 2.0f * PI * float2(n, m) / Len;
}

inline float2 Conj(float2 a)
{
	return float2(a.x, -a.y);
}

inline float Random(float2 uv, float salt, float random)
{
	uv += float2(salt, random);
	return frac(sin(dot(uv, float2(12.9898f, 78.233f))) * 43758.5453f);
}


inline float Phillips(float n, float m, float amp, float2 wind, float N, float len)
{
	// set N=M
	float2 k = WaveVector(n, m, len, N);

	float klen = length(k);
	float klen2 = klen * klen;
	float klen4 = klen2 * klen2;
	if(klen < 0.0001f)
		return 0;

	float kDotW = dot(normalize(k), normalize(wind));
	float kDotW2 = kDotW * kDotW;
	float wlen = length(wind);
	float l = wlen * wlen / G;
	float l2 = l * l;
	float damping = 0.01f;
	float L2 = l2 * damping * damping;
	return amp * exp(-1 / (klen2 * l2)) / klen4 * kDotW2 * exp(-klen2 * L2);
}


inline float2 hTilde0(float2 uv, float r1, float r2, float phi)
{
	float2 r;
	float rand1 = Random(uv, 10.612f, r1);
	float rand2 = Random(uv, 11.899f, r2);
	rand1 = clamp(rand1, 0.01f, 1.0f);
	rand2 = clamp(rand2, 0.01f, 1.0f);
	float x = sqrt(-2.0f * log(rand1));
	float y = 2.0f * PI * rand2;
	r.x = x * cos(y);
	r.y = x * sin(y);
	return r * sqrt(phi * 0.5f); 
}


#endif