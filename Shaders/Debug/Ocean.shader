Shader "Unlit/Ocean"
{
    Properties
    {
		_FresnelScale ("Fresnel Scale", Range(0, 1)) = 0.5
		_OceanColorShallow ("Ocean Color Shallow", Color) = (1, 1, 1, 1)
		_OceanColorDeep ("Ocean Color Deep", Color) = (1, 1, 1, 1)
		_BubblesColor ("Bubbles Color", Color) = (1, 1, 1, 1)
		_SkyColor ("Sky Color", Color) = (1, 1, 1, 1)
		_Specular ("Specular", Color) = (1, 1, 1, 1)
		_Gloss ("Gloss", Range(8.0, 256)) = 20
    }
    SubShader
    {
		Tags { "RenderType" = "Opaque" "LightMode" = "ForwardBase" }
		LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
			#include "Lighting.cginc"

			uniform sampler2D _Displace;
			uniform	float4 _Displace_ST;
			uniform sampler2D _NormalBubbles;

			float _FresnelScale;
			float4 _OceanColorShallow;
			float4 _OceanColorDeep;
			float4 _BubblesColor;
			float4 _SkyColor;
			float4 _Specular;
			float _Gloss;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
				float3 worldPos: TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                
                o.uv = TRANSFORM_TEX(v.uv, _Displace);
				float4 displcae = tex2Dlod(_Displace, float4(o.uv, 0, 0));
				v.vertex += float4(displcae.xyz, 0);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
				float4 samp = tex2D(_NormalBubbles, i.uv);
				float3 normal = UnityObjectToWorldNormal(samp.rgb);
				float bubble = samp.w;

				float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
				float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
				float3 reflectDir = reflect(-viewDir, normal); 
				
				
				float3 fresnel = saturate(_FresnelScale + (1 - _FresnelScale) * pow(1 - dot(normal, viewDir), 5));

				float facing = saturate(dot(viewDir, normal));                
				float3 oceanColor = lerp(_OceanColorShallow, _OceanColorDeep, facing);
				float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;

				float3 bubblesDiffuse = _BubblesColor.rbg * _LightColor0.rgb * saturate(dot(lightDir, normal));
			
				float3 oceanDiffuse = oceanColor * _LightColor0.rgb * saturate(dot(lightDir, normal));
				float3 halfDir = normalize(lightDir + viewDir);
				float3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0, dot(normal, halfDir)), _Gloss);

				float3 diffuse = lerp(oceanDiffuse, bubblesDiffuse, bubble);
				float3 col = ambient + lerp(diffuse, _SkyColor.xyz, fresnel) + specular ;

                return float4(col,1);
            }
            ENDCG
        }
    }
}
