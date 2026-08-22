Shader "Skybox/Dual Panoramic" {
	Properties{
		_Tint1("Tint Color 1", Color) = (.5, .5, .5, .5)
		_Tint2("Tint Color 2", Color) = (.5, .5, .5, .5)
		[Gamma] _Exposure1("Exposure 1", Range(0, 8)) = 1.0
		[Gamma] _Exposure2("Exposure 2", Range(0, 8)) = 1.0
		_Rotation1("Rotation1", Range(0, 360)) = 0
		_Rotation2("Rotation2", Range(0, 360)) = 0
		[NoScaleOffset] _Texture1("Texture 1", 2D) = "grey" {}
		[NoScaleOffset] _Texture2("Texture 2", 2D) = "grey" {}
		[Enum(360 Degrees, 0, 180 Degrees, 1)] _ImageType("Image Type", Float) = 0
		[Toggle] _MirrorOnBack("Mirror on Back", Float) = 0
		[Enum(None, 0, Side by Side, 1, Over Under, 2)] _Layout("3D Layout", Float) = 0
		_Blend("Blend", Range(0.0, 1.0)) = 0.0

		[Header(Sun Procedural)]
		_SunColor("Sun Color", Color) = (1, 0.95, 0.8, 1)
		_SunSize("Sun Angular Size (radians)", Range(0.001, 0.5)) = 0.045
		_SunHaze("Sun Haze - extra soft halo radius (radians)", Range(0.0, 0.5)) = 0.08

		[Header(Moon Textured)]
		[NoScaleOffset] _MoonTex("Moon Texture", 2D) = "black" {}
		_MoonSize("Moon Angular Size (radians)", Range(0.001, 0.5)) = 0.07

		[Header(Shared)]
		_CelestialIntensity("Sun/Moon Intensity", Range(0, 10)) = 1.5
		// Pushed from script every tick — world-space direction from the sky toward the light (i.e. -light.transform.forward).
		_CelestialDir("Celestial Direction", Vector) = (0, 1, 0, 0)
		// Pushed from script — 0 = fully sun (procedural glow), 1 = fully moon (texture). Drives the crossfade.
		_MoonAmount("Moon Amount", Range(0, 1)) = 0
	}

		SubShader{
			Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
			Cull Off ZWrite Off

			Pass {

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 2.0
				#pragma multi_compile_local __ _MAPPING_6_FRAMES_LAYOUT

				#include "UnityCG.cginc"

				sampler2D _Texture1;
				sampler2D _Texture2;

				float4 _Texture1_TexelSize;

				half4 _Texture1_HDR;
				half4 _Texture2_HDR;
				half4 _Tint1;
				half4 _Tint2;
				half _Exposure1;
				half _Exposure2;
				float _Rotation1;
				float _Rotation2;

				float _Blend;

				bool _MirrorOnBack;
				int _ImageType;
				int _Layout;

				half4 _SunColor;
				float _SunSize;
				float _SunHaze;

				sampler2D _MoonTex;
				float _MoonSize;

				float _CelestialIntensity;
				float4 _CelestialDir;
				float _MoonAmount;

				inline float2 ToRadialCoords(float3 coords)
				{
					float3 normalizedCoords = normalize(coords);
					float latitude = acos(normalizedCoords.y);
					float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
					float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
					return float2(0.5,1.0) - sphereCoords;
				}

				float3 RotateAroundYInDegrees(float3 vertex, float degrees)
				{
					float alpha = degrees * UNITY_PI / 180.0;
					float sina, cosa;
					sincos(alpha, sina, cosa);
					float2x2 m = float2x2(cosa, -sina, sina, cosa);
					return float3(mul(m, vertex.xz), vertex.y).xzy;
				}

				struct appdata_t {
					float4 vertex : POSITION;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f {
					float4 vertex : SV_POSITION;
					float3 texcoord : TEXCOORD0;
					float2 image180ScaleAndCutoff : TEXCOORD1;
					float4 layout3DScaleAndOffset : TEXCOORD2;
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert(appdata_t v)
				{
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
					float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation1);
					o.vertex = UnityObjectToClipPos(rotated);

					o.texcoord = v.vertex.xyz;

					// Calculate constant horizontal scale and cutoff for 180 (vs 360) image type
					if (_ImageType == 0)  // 360 degree
						o.image180ScaleAndCutoff = float2(1.0, 1.0);
					else  // 180 degree
						o.image180ScaleAndCutoff = float2(2.0, _MirrorOnBack ? 1.0 : 0.5);
					// Calculate constant scale and offset for 3D layouts
					if (_Layout == 0) // No 3D layout
						o.layout3DScaleAndOffset = float4(0,0,1,1);
					else if (_Layout == 1) // Side-by-Side 3D layout
						o.layout3DScaleAndOffset = float4(unity_StereoEyeIndex,0,0.5,1);
					else // Over-Under 3D layout
						o.layout3DScaleAndOffset = float4(0, 1 - unity_StereoEyeIndex,1,0.5);
					return o;
				}

				// Procedural sun: bright core + soft haze halo, purely angle-based —
				// no texture sample, no tangent basis needed.
				half4 SampleSun(float angle)
				{
					float core = 1.0 - smoothstep(_SunSize * 0.85, _SunSize, angle);
					float halo = 1.0 - smoothstep(_SunSize, _SunSize + _SunHaze, angle);
					float alpha = saturate(core + halo * 0.4);
					half3 color = _SunColor.rgb * (core + halo * 0.5);
					return half4(color, alpha);
				}

				// Textured moon disc: projects the view direction into a tangent basis
				// around _CelestialDir to get UVs for _MoonTex.
				half4 SampleMoon(float3 viewDir, float3 forward, float cosAngle, float angle)
				{
					float3 upHint = (abs(forward.y) < 0.99) ? float3(0, 1, 0) : float3(1, 0, 0);
					float3 right = normalize(cross(upHint, forward));
					float3 up = cross(forward, right);

					float3 perp = viewDir - forward * cosAngle;
					float sinSize = max(sin(_MoonSize), 1e-4);
					float2 discUV = float2(dot(perp, right), dot(perp, up)) / sinSize;
					discUV = discUV * 0.5 + 0.5;

					// Outside the disc's own UV square — never sample here. With Wrap
					// Mode set to Repeat this would otherwise fetch a different tile
					// of the texture and show up as opaque patches bleeding past the
					// intended circular edge.
					if (any(discUV < 0.0) || any(discUV > 1.0)) return half4(0, 0, 0, 0);

					half4 tex = tex2D(_MoonTex, discUV);
					float edge = 1.0 - smoothstep(_MoonSize * 0.9, _MoonSize, angle);
					tex.a *= edge;
					return tex;
				}

				// Crossfades the procedural sun and textured moon by _MoonAmount, both
				// centered on the same _CelestialDir (the same light doubles as sun by
				// day and moon by night), and fades near/below the horizon.
				half4 SampleCelestialDisc(float3 viewDir)
				{
					float3 forward = normalize(_CelestialDir.xyz);
					float cosAngle = dot(viewDir, forward);
					float angle = acos(saturate(cosAngle));

					float maxSize = max(_SunSize + _SunHaze, _MoonSize);
					if (angle > maxSize) return half4(0, 0, 0, 0);

					half4 sun = SampleSun(angle);
					half4 moon = SampleMoon(viewDir, forward, cosAngle, angle);

					half4 disc = lerp(sun, moon, _MoonAmount);

					float horizonFade = smoothstep(-0.02, 0.06, forward.y);
					disc.a *= horizonFade;
					return disc;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					float2 tc = ToRadialCoords(i.texcoord);
					if (tc.x > i.image180ScaleAndCutoff[1])
						return half4(0,0,0,1);
					tc.x = fmod(tc.x * i.image180ScaleAndCutoff[0], 1);
					tc = (tc + i.layout3DScaleAndOffset.xy) * i.layout3DScaleAndOffset.zw;

					half4 tex1 = tex2D(_Texture1, tc);
					tc.x = frac(tc.x + (_Rotation2 - _Rotation1) / 360.0);
					half4 tex2 = tex2D(_Texture2, tc);

					half3 c1 = DecodeHDR(tex1, _Texture1_HDR);
					half3 c2 = DecodeHDR(tex2, _Texture2_HDR);

					c1 = lerp(c1, c2, _Blend) * lerp(_Tint1.rgb, _Tint2.rgb, _Blend) * unity_ColorSpaceDouble.rgb * lerp(_Exposure1, _Exposure2, _Blend);

					float3 viewDir = normalize(i.texcoord);
					half4 disc = SampleCelestialDisc(viewDir);
					c1 = lerp(c1, disc.rgb * _CelestialIntensity, saturate(disc.a));

					return half4(c1, 1);
				}
				ENDCG
			}
		}


			//CustomEditor "SkyboxPanoramicShaderGUI"
					Fallback Off

}
