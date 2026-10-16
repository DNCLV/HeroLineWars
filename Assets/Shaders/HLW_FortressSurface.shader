Shader "Hero Line Wars/Fortress Surface"
{
    Properties
    {
        _BaseMap("Kenney Palette", 2D) = "white" {}
        _BaseColor("Limestone Tint", Color) = (.72,.77,.81,1)
        _StoneMix("Stone Lift", Range(0,1)) = .5
        _TeamColor("Faction Accent", Color) = (1,1,1,1)
        _TeamAmount("Faction Amount", Range(0,1)) = 0
        _EmissionStrength("Accent Emission", Range(0,1)) = .13
        _Smoothness("Smoothness", Range(0,1)) = .22
        _StoneDefinition("Stone Face Definition", Range(0,1)) = 0
        [Toggle(_PAVING)] _Paving("Paving Pattern", Float) = 0
        _LaneSize("Lane Width, Length, Cell Size", Vector) = (8,32,1,0)
        _CityLayout("Branch End and Three Plaza Z Positions", Vector) = (32,45,70,95)
        _Variation("Slab Variation", Range(0,1)) = .12
        _SpawnColor("Spawn Accent", Color) = (.12,.55,1,1)
        _GoalColor("Goal Accent", Color) = (.9,.14,.22,1)
        _RuneColor("Arcane Inlay Color", Color) = (.12,.72,.8,1)
        _RuneIntensity("Rune Intensity", Range(0,1.5)) = .7
        _MagicPulse("Magic Pulse", Range(0,.5)) = .16
        [Toggle(_SURROUND)] _Surround("Outer Stone Pattern", Float) = 0
        _OutskirtsRuneStrength("Outer Arcane Motifs", Range(0,.8)) = .35
        _OutskirtsRuneRadius("Outer Rune Radius", Range(1,5)) = 2.4
        _DistrictRuneA("Rune A Center XZ, Radius, Rotation", Vector) = (14,48,4.6,-.14)
        _DistrictRuneB("Rune B Center XZ, Radius, Rotation", Vector) = (34,48,4.6,.21)
        [HDR] _DistrictRuneAColor("Rune A Color", Color) = (.08,.78,1,1)
        [HDR] _DistrictRuneBColor("Rune B Color", Color) = (.16,.58,1,1)
        [HDR] _DistrictRuneAccentColor("Rune Purple Accent", Color) = (.58,.2,.9,1)
        _DistrictRuneBrightness("Rune Brightness", Range(0,2.5)) = 1.35
        _DistrictRunePulse("Rune Pulse", Range(0,.25)) = .1
        [Toggle(_ARCANE_STONE)] _ArcaneStone("Runestone Glyphs", Float) = 0
        _GlyphBounds("Glyph Center X, Base Y, Center Z, Radius", Vector) = (0,0,0,1)
        _GlyphHeight("Glyph Height", Float) = 1
        _StoneExtents("Placed Stone Half Extents", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _TeamColor, _SpawnColor, _GoalColor, _RuneColor;
            half4 _DistrictRuneAColor, _DistrictRuneBColor, _DistrictRuneAccentColor;
            float4 _LaneSize, _CityLayout, _GlyphBounds, _StoneExtents;
            float4 _DistrictRuneA, _DistrictRuneB;
            half _StoneMix, _TeamAmount, _EmissionStrength, _Smoothness, _Paving, _Variation;
            half _StoneDefinition;
            half _RuneIntensity, _MagicPulse, _Surround, _ArcaneStone;
            half _OutskirtsRuneStrength;
            float _OutskirtsRuneRadius;
            half _DistrictRuneBrightness, _DistrictRunePulse;
            float _GlyphHeight;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        // Analytic, derivative-antialiased inlays on the existing opaque mesh.
        // No texture overlays, displacement, extra floor surfaces, or animation.
        half Stroke(float distance, float width)
        {
            float aa = max(fwidth(distance), .001);
            return 1 - smoothstep(width-aa, width+aa, distance);
        }
        float SegmentDistance(float2 p, float2 a, float2 b)
        {
            float2 v = b-a;
            return length(p-a-v*saturate(dot(p-a,v)/max(dot(v,v),.0001)));
        }
        half Rune(float2 p)
        {
            // One open diamond, a central staff, and two short angular branches.
            float diamond = abs(abs(p.x) + abs(p.y-.13) - .25) * .7071;
            half mark = Stroke(diamond,.025) * (1-smoothstep(.2,.27,abs(p.y-.13)));
            mark = max(mark,Stroke(SegmentDistance(p,float2(0,-.48),float2(0,.48)),.025));
            mark = max(mark,Stroke(SegmentDistance(p,float2(-.21,-.12),float2(0,-.3)),.025));
            mark = max(mark,Stroke(SegmentDistance(p,float2(0,-.3),float2(.21,-.12)),.025));
            return mark;
        }
        half Seal(float2 p)
        {
            float radius = length(p);
            half ring = Stroke(abs(radius-1),.02);
            half inner = Stroke(abs(radius-.78),.014);
            // Broken inner circle, diamond, and four radial keys. Broad strokes
            // remain legible when viewed from the actual high RTS camera.
            inner *= smoothstep(.15,.23,min(abs(p.x),abs(p.y)));
            half diamond = Stroke(abs(abs(p.x)+abs(p.y)-.9)*.7071,.019);
            half keys = Stroke(min(abs(p.x),abs(p.y)),.024) *
                smoothstep(.83,.87,radius) * (1-smoothstep(1.08,1.12,radius));
            return saturate(ring+inner+diamond+keys);
        }
        half2 DistrictRuneMarks(float2 delta, float radius, float rotation)
        {
            float sine = sin(rotation), cosine = cos(rotation);
            float2 q = float2(delta.x*cosine-delta.y*sine,
                delta.x*sine+delta.y*cosine) / max(radius,.001);
            float r = length(q);
            half boundary = 1-smoothstep(1.035,1.08,r);
            half seal = Seal(q);
            half innerRing = Stroke(abs(r-.56),.013);
            half innerDiamond = Stroke(abs(abs(q.x)+abs(q.y)-.53)*.7071,.018);
            half axisKeys = Stroke(min(abs(q.x),abs(q.y)),.018)*
                smoothstep(.58,.66,r)*(1-smoothstep(.87,.94,r));
            half glyph = Rune(q*2.35);
            // Three offset angular glyphs keep the circle readable as an
            // ancient inscription rather than a plain target reticle.
            glyph = max(glyph,Rune((q-float2(0,.73))*4.1));
            glyph = max(glyph,Rune((q-float2(-.63,-.36))*4.1));
            glyph = max(glyph,Rune((q-float2(.63,-.36))*4.1));
            half primary = saturate(seal+axisKeys+glyph*.72)*boundary;
            half accent = saturate(innerRing+innerDiamond*.65+glyph*.18)*boundary;
            return half2(primary,accent);
        }
        float2 PavingCoordinates(float3 positionWS)
        {
            float2 p = positionWS.xz / max(_LaneSize.z,.001);
            return float2((p.x + fmod(floor(p.y*.5),2))*.5,p.y*.5);
        }
        half3 PavingNormal(float3 positionWS, half3 meshNormal)
        {
            // A narrow stone bevel in lighting only. The floor geometry stays
            // flat. Use the identical normal in the URP depth-normal pass.
            float2 f = frac(PavingCoordinates(positionWS));
            float2 slope = (1-smoothstep(.008,.035,f)) - smoothstep(.965,.992,f);
            return normalize(meshNormal + half3(-slope.x,0,-slope.y)*.22);
        }
        half StoneHash(float2 p)
        {
            // Stable, sin-free hash: no random regeneration or extra textures.
            float3 q = frac(float3(p.xyx)*.1031);
            q += dot(q,q.yzx+33.33);
            return frac((q.x+q.y)*q.z);
        }
        half StoneCloud(float2 p)
        {
            float2 id = floor(p), f = frac(p);
            f = f*f*(3-2*f);
            return lerp(lerp(StoneHash(id),StoneHash(id+float2(1,0)),f.x),
                lerp(StoneHash(id+float2(0,1)),StoneHash(id+1),f.x),f.y);
        }
        ENDHLSL

        Pass
        {
            Name "FortressForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            // Materials are generated at runtime: retain all four surface modes
            // in players even when no saved Material asset references a keyword.
            #pragma multi_compile_local_fragment _ _PAVING _SURROUND _ARCANE_STONE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 palette = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 albedo = lerp(palette, _BaseColor.rgb, _StoneMix);
                half3 emission = 0;
                // One shared low-frequency pulse animates only existing arcane
                // emission; it adds no geometry, textures, or extra draw calls.
                half magicPulse = 1 - _MagicPulse * (.5 + .5 * sin(_Time.y * 1.7));
                half3 shadingNormal = normalize(input.normalWS);
                #if defined(_PAVING)
                    // Generated road meshes have continuous metric UVs, while
                    // Kenney's source texture is a palette atlas. Sampling that
                    // atlas as a repeating texture creates colored micro-tiles.
                    // Paving detail is intentionally world-space below, so use
                    // the configured stone color as its stable base instead.
                    albedo = _BaseColor.rgb;
                    float2 p = input.positionWS.xz / max(_LaneSize.z, .001);
                    // Repeat the original road inlay across the three physical
                    // roads; the paving mesh itself remains one unique tile grid.
                    p.x = fmod(p.x, max(_LaneSize.w, _LaneSize.x));
                    float2 slabs = PavingCoordinates(input.positionWS);
                    shadingNormal = PavingNormal(input.positionWS,shadingNormal);
                    float2 edge = min(frac(slabs), 1 - frac(slabs));
                    float distance = min(edge.x, edge.y);
                    float aa = max(fwidth(distance), .001);
                    half grout = 1 - smoothstep(.007 - aa, .007 + aa, distance);
                    half bevel = smoothstep(.007, .024, distance);
                    float2 id = floor(slabs);
                    half variation = StoneHash(id);
                    half warmth = StoneHash(id+17.7);
                    half cloud = StoneCloud(p*.65);
                    albedo *= 1 + (variation-.5)*_Variation*1.25 + (cloud-.5)*_Variation*.45;
                    half3 stoneTint = lerp(half3(.93,.98,1.04),half3(1.05,1,.94),warmth);
                    albedo *= lerp(half3(1,1,1),stoneTint,saturate(_Variation*3));
                    albedo *= lerp(.82, 1, bevel);
                    albedo = lerp(albedo, albedo * .5, grout * .75);

                    // Inlaid side channels frame a broad, mostly unadorned field.
                    float sideDistance = min(abs(p.x), abs(_LaneSize.x - p.x));
                    half border = 1 - smoothstep(.73,.77,sideDistance);
                    albedo = lerp(albedo, half3(.19,.27,.32), border * .55);
                    half brass = 1 - smoothstep(.014,.038,abs(sideDistance - .78));
                    albedo = lerp(albedo, half3(.52,.43,.25), brass * .55);

                    float repeatZ = frac((p.y+2)/4)*4-2;
                    half edgeRunes = Rune(float2((sideDistance-.39)*1.9,repeatZ*1.6));
                    half channel = Stroke(abs(sideDistance-.78),.014) * .22;
                    float2 middle = p-float2(_LaneSize.x*.5,_LaneSize.y*.5);
                    half centralSeal = Seal(middle/1.55);
                    half centralRune = Rune(middle/.85);
                    float2 quarter = float2(p.x-_LaneSize.x*.5,
                        p.y-(_LaneSize.y*(p.y<_LaneSize.y*.5 ? .25 : .75)));
                    half waymark = Rune(quarter/.9) * (1-smoothstep(.5,.7,length(quarter)));
                    half arcane = saturate(edgeRunes*.6 + channel + centralSeal*.65 + centralRune*.55 + waymark*.6);

                    half spawnZone = saturate(1-p.y/2);
                    half goalZone = saturate(1-(_LaneSize.y-p.y)/2);
                    albedo = lerp(albedo, lerp(albedo,_SpawnColor.rgb,.3),spawnZone);
                    albedo = lerp(albedo, lerp(albedo,_GoalColor.rgb,.3),goalZone);
                    half spawnSigil = Seal((p-float2(_LaneSize.x*.5,2.1/_LaneSize.z))/1.5);
                    half goalSigil = Seal((p-float2(_LaneSize.x*.5,_LaneSize.y-1.7/_LaneSize.z))/1.7);
                    half3 runeColor = lerp(_RuneColor.rgb,_SpawnColor.rgb,saturate(1-p.y/5));
                    runeColor = lerp(runeColor,_GoalColor.rgb,saturate(1-(_LaneSize.y-p.y)/5));
                    half3 inlay = runeColor*arcane + _SpawnColor.rgb*spawnSigil + _GoalColor.rgb*goalSigil;
                    half coverage = saturate(arcane+spawnSigil+goalSigil) * saturate(_RuneIntensity);
                    albedo = lerp(albedo,half3(.13,.32,.38),coverage*.65);
                    emission = inlay * _RuneIntensity * .42 * magicPulse;
                #elif defined(_SURROUND)
                    // One coherent world-space city surface. No palette atlas
                    // sampling, extra floor planes, or object-space stretching.
                    albedo = _BaseColor.rgb;
                    float2 p = input.positionWS.xz / max(_LaneSize.z,.001);
                    float roadWidth = _LaneSize.x;
                    float stride = max(_LaneSize.w,roadWidth+2);
                    // The classic layout has two roads separated by one city
                    // district: road + gap + road.
                    float mapWidth = stride+roadWidth;

                    // Large staggered city slabs remain readable at gameplay
                    // zoom. Their color comes from stable world-space hashes.
                    float slabRow = floor(p.y/3.6);
                    float2 slabs = float2((p.x+fmod(slabRow,2)*1.35)/2.8,p.y/3.6);
                    float2 slabEdge = min(frac(slabs),1-frac(slabs));
                    float jointDistance = min(slabEdge.x*2.8,slabEdge.y*3.6);
                    half joint = Stroke(jointDistance,.045);
                    float2 slabId = floor(slabs);
                    half variation = StoneHash(slabId);
                    half warmth = StoneHash(slabId+31.7);
                    half cityCloud = StoneCloud(p*.14);
                    albedo *= .88 + variation*.13 + cityCloud*.08;
                    albedo *= lerp(half3(.94,1,1.04),half3(1.03,.98,.91),warmth*.22);
                    albedo = lerp(albedo,albedo*.48,joint*.72);

                    // Identify the single inter-road city district. Road
                    // and connector meshes remain the only playable surfaces.
                    float localX = p.x-floor(p.x/stride)*stride;
                    half inMap = step(0,p.x)*step(p.x,mapWidth)*step(_CityLayout.x,p.y)*step(p.y,_LaneSize.y);
                    half inGap = step(roadWidth+.08,localX)*step(localX,stride-.08);
                    half district = inMap*inGap;

                    // Deterministic broken paving and weathering tie the ground
                    // to the ruins without creating extra geometry.
                    float2 fractureCell = floor(p/5);
                    float2 fractureLocal = frac(p/5)-.5;
                    float fractureSlope = StoneHash(fractureCell)*1.6-.8;
                    float fractureOffset = (StoneHash(fractureCell+9.3)-.5)*.34;
                    float fractureDistance = abs(fractureLocal.y-fractureLocal.x*fractureSlope-fractureOffset);
                    half fracture = Stroke(fractureDistance,.018)*district;
                    half missingSlab = step(.78,StoneHash(slabId+73.1))*district;
                    albedo = lerp(albedo,albedo*.42,saturate(fracture*.9+missingSlab*joint*.42));
                    half moss = smoothstep(.68,.88,StoneCloud(p*.31+float2(11.2,4.7))) *
                        district * saturate(joint*1.5+fracture*.7);
                    albedo = lerp(albedo,half3(.12,.19,.16),moss*.38);

                    // The primary circle is inlaid directly into the existing
                    // ground. The legacy B input remains inert and outside the
                    // footprint for serialized-material compatibility.
                    half2 runeA = DistrictRuneMarks(p-_DistrictRuneA.xy,
                        _DistrictRuneA.z,_DistrictRuneA.w);
                    half2 runeB = DistrictRuneMarks(p-_DistrictRuneB.xy,
                        _DistrictRuneB.z,_DistrictRuneB.w);
                    half primaryA = runeA.x*district;
                    half primaryB = runeB.x*district;
                    half accent = max(runeA.y,runeB.y)*district;
                    albedo = lerp(albedo,_DistrictRuneAColor.rgb*.34,primaryA*.72);
                    albedo = lerp(albedo,_DistrictRuneBColor.rgb*.34,primaryB*.72);
                    albedo = lerp(albedo,_DistrictRuneAccentColor.rgb*.28,accent*.42);
                    half runePulse = 1 + sin(_Time.y*1.15)*_DistrictRunePulse;
                    emission = (_DistrictRuneAColor.rgb*primaryA+
                        _DistrictRuneBColor.rgb*primaryB+
                        _DistrictRuneAccentColor.rgb*accent*.34)*
                        _DistrictRuneBrightness*runePulse;

                    // Outside the old city footprint, courses break down into
                    // dark rocky terrain instead of ending at a hard boundary.
                    float outside = max(max(-p.x,p.x-mapWidth),max(-p.y,p.y-_LaneSize.y));
                    half rockyEdge = smoothstep(2.5,10,outside);
                    half rockNoise = StoneCloud(p*.42)+StoneCloud(p*.17+19.4)*.55;
                    half3 rocky = half3(.075,.105,.13)*(0.76+rockNoise*.24);
                    albedo = lerp(albedo,rocky,rockyEdge*.82);
                #else
                    // Retain original palette mapping; recolor saturated cloth /
                    // magical trims while preserving neutral metal and stone.
                    half blue = smoothstep(.02,.18,palette.b-max(palette.r,palette.g));
                    half red = smoothstep(.04,.22,palette.r-max(palette.g,palette.b));
                    half mask = saturate(blue+red) * _TeamAmount;
                    half brightness = max(.35,max(palette.r,max(palette.g,palette.b)));
                    albedo = lerp(albedo,_TeamColor.rgb * brightness,mask);
                    emission = _TeamColor.rgb * mask * _EmissionStrength;
                    #if defined(_ARCANE_STONE)
                        float3 p = input.positionWS - _GlyphBounds.xyz;
                        // _GlyphBounds stores (centerX, baseY, centerZ, radius).
                        float radius = max(_GlyphBounds.w,.01);
                        float height = max(_GlyphHeight,.01);
                        float2 face = float2(abs(input.normalWS.x)>.5 ? p.z : p.x,
                            (p.y/height-.52)*radius*2)/radius;
                        half glyph = Rune(face);
                        half band = Stroke(abs(p.y/height-.78),.018)*.5;
                        half top = Seal(p.xz/radius*.82)*.6 + Rune(p.xz/radius*1.5)*.5;
                        half mark = saturate(lerp(max(glyph,band),top,step(.55,input.normalWS.y)));
                        albedo = lerp(albedo,half3(.13,.24,.31),mark*saturate(_RuneIntensity)*.6);
                        emission += _TeamColor.rgb * mark * _RuneIntensity * .65 * magicPulse;
                    #endif
                    // Readable architectural form even on a small mobile view:
                    // quieter vertical faces and shaded feet, not painted black
                    // outlines or new detail geometry. Cloth keeps its palette.
                    half faceTone = lerp(.78,1.03,saturate(shadingNormal.y));
                    half footTone = lerp(.84,1,smoothstep(0,.3,input.positionWS.y/max(_LaneSize.z,.001)));
                    albedo *= lerp(1,faceTone*footTone,_StoneDefinition);
                    if (_StoneExtents.x > .001 && _StoneDefinition > 0)
                    {
                        float2 fromCenter = abs(input.positionWS.xz-_GlyphBounds.xz);
                        float2 inset = _StoneExtents.xz-fromCenter;
                        float rimDistance = min(inset.x,inset.y)/max(_LaneSize.z,.001);
                        half rim = 1-smoothstep(.015,.07,rimDistance);
                        half lip = Stroke(abs(rimDistance-.07),.012);
                        half topFace = smoothstep(.6,.9,shadingNormal.y);
                        albedo *= lerp(1,1-rim*.17+lip*.08,topFace*_StoneDefinition);
                    }
                #endif

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = shadingNormal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                // Retain a small safety fill for an unbaked procedural scene,
                // without the old bright ambient floor washing out shadows.
                data.bakedGI = max(SampleSH(data.normalWS), half3(.10,.12,.145));
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.specular = half3(.04,.04,.04);
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.emission = emission;
                surface.alpha = 1;
                return UniversalFragmentPBR(data, surface);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_local_fragment _ _PAVING
            half4 NormalFrag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                #if defined(_PAVING)
                    normal = PavingNormal(input.positionWS,normal);
                #endif
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            float3 _LightDirection;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = Vert(input);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS,normalize(output.normalWS),_LightDirection));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
