using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace VirtualHeritage.Editor
{
    public static class HeritageProject
    {
        const string ScenePath = "Assets/Heritage/Scenes/Heritage.unity";
        static readonly string[] ShaderAnchors = {
            "Universal Render Pipeline/Lit","Universal Render Pipeline/Unlit","Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Particles/Unlit","Universal Render Pipeline/Particles/Lit",
            "Skybox/Procedural","UI/Default","GUI/Text Shader","Sprites/Default" };

        [InitializeOnLoadMethod]
        static void FirstImport()
        {
            if (!File.Exists(ScenePath)) EditorApplication.delayCall += () => { if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode) Configure(); };
        }

        [MenuItem("Heritage/Configure Project")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Heritage/Resources/Shaders"); Directory.CreateDirectory("Assets/Heritage/Settings"); Directory.CreateDirectory("Assets/Heritage/Scenes");
            AssetDatabase.Refresh();
            string rendererPath = "Assets/Heritage/Settings/HeritageRenderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (!renderer) { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(renderer,rendererPath); }
            string pipelinePath = "Assets/Heritage/Settings/HeritageURP.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (!pipeline) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline,pipelinePath); }
            pipeline.msaaSampleCount = 4; pipeline.renderScale = 1; pipeline.shadowDistance = 70;
            pipeline.mainLightShadowmapResolution = 4096; pipeline.supportsHDR = true;
            pipeline.shadowCascadeCount = 4;
            using (var so = new SerializedObject(pipeline))
            {
                var mode = so.FindProperty("m_AdditionalLightsRenderingMode"); if (mode != null) mode.intValue = 1;
                var limit = so.FindProperty("m_AdditionalLightsPerObjectLimit"); if (limit != null) limit.intValue = 8;
                // Soft, cascaded sun shadows: the default single hard cascade looked jagged on carved detail.
                var soft = so.FindProperty("m_SoftShadowsSupported"); if (soft != null) soft.boolValue = true;
                var softQuality = so.FindProperty("m_SoftShadowQuality"); if (softQuality != null) softQuality.intValue = (int)SoftShadowQuality.High;
                var depth = so.FindProperty("m_RequireDepthTexture"); if (depth != null) depth.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AddAmbientOcclusion(renderer);
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All; QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
            QualitySettings.vSyncCount = 0; QualitySettings.antiAliasing = 4;
            EditorUtility.SetDirty(pipeline);
            // The scene is built entirely at runtime, so nothing references these shaders and the player
            // build would strip them (magenta UI / geometry). A material per shader in Resources keeps each one in the build.
            foreach (string shaderName in ShaderAnchors)
            {
                var shader = Shader.Find(shaderName);
                if (!shader) throw new InvalidOperationException("Required shader is missing: " + shaderName);
                string materialPath = "Assets/Heritage/Resources/Shaders/" + shaderName.Replace('/','_').Replace(' ','_') + ".mat";
                if (!AssetDatabase.LoadAssetAtPath<Material>(materialPath)) AssetDatabase.CreateAsset(new Material(shader),materialPath);
            }
            NormalMapAnchor();
            AlwaysInclude(ShaderAnchors);
            KeepFogVariants();
            PlayerSettings.companyName = "Heritage Studio"; PlayerSettings.productName = "Virtual Heritage Karnataka";
            PlayerSettings.bundleVersion = "0.1.0"; PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            using (var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]))
            {
                var input = playerSettings.FindProperty("activeInputHandler"); if (input != null) { input.intValue = 1; playerSettings.ApplyModifiedPropertiesWithoutUndo(); }
            }
            ConfigureXR();
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                new GameObject("Virtual Heritage Karnataka").AddComponent<HeritageApp>();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes = new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("HERITAGE: Project configured. Open Heritage scene and press Play.");
        }

        // Runtime materials enable _NORMALMAP; a saved material using it keeps that Lit variant in the player.
        static void NormalMapAnchor()
        {
            const string texturePath = "Assets/Heritage/Resources/Shaders/Flat_Normal.asset", materialPath = "Assets/Heritage/Resources/Shaders/Universal_Render_Pipeline_Lit_NormalMap.mat";
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!normal)
            {
                normal = new Texture2D(4,4,TextureFormat.RGBA32,false,true) { name = "Flat normal" };
                var px = new Color[16]; for (int i = 0; i < px.Length; i++) px[i] = new Color(.5f,.5f,1,1);
                normal.SetPixels(px); normal.Apply(); AssetDatabase.CreateAsset(normal,texturePath);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,materialPath); }
            material.SetTexture("_BumpMap",normal); material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        // The scene is built at runtime with fog on; automatic stripping would remove fog from every shader.
        static void KeepFogVariants()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (asset == null || asset.Length == 0) return;
            using (var graphics = new SerializedObject(asset[0]))
            {
                var stripping = graphics.FindProperty("m_FogStripping"); if (stripping != null) stripping.intValue = 1;
                foreach (string keep in new[] { "m_FogKeepLinear","m_FogKeepExp","m_FogKeepExp2" }) { var p = graphics.FindProperty(keep); if (p != null) p.boolValue = true; }
                graphics.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void AddAmbientOcclusion(UniversalRendererData renderer)
        {
            var ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (!ssao)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>(); ssao.name = "Ambient occlusion";
                AssetDatabase.AddObjectToAsset(ssao,renderer); renderer.rendererFeatures.Add(ssao);
            }
            using (var so = new SerializedObject(ssao))
            {
                void Set(string path,float value) { var p = so.FindProperty("m_Settings." + path); if (p != null) p.floatValue = value; }
                Set("Intensity",1.4f); Set("Radius",.3f); Set("DirectLightingStrength",.3f); Set("Falloff",60);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            using (var data = new SerializedObject(renderer))
            {
                var map = data.FindProperty("m_RendererFeatureMap");
                if (map != null && map.arraySize != renderer.rendererFeatures.Count)
                {
                    map.arraySize = renderer.rendererFeatures.Count;
                    for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i],out string _,out long id)) map.GetArrayElementAtIndex(i).longValue = id;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        }

        static bool IsBuiltin(UnityEngine.Object o) { string path = AssetDatabase.GetAssetPath(o); return !(path.StartsWith("Assets/") || path.StartsWith("Packages/")); }

        static void AlwaysInclude(string[] names)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (asset == null || asset.Length == 0) return;
            using (var graphics = new SerializedObject(asset[0]))
            {
                var included = graphics.FindProperty("m_AlwaysIncludedShaders");
                // Built-in shaders (UI, text, sprites, skybox) are not asset files. Listing them here makes the
                // 6000.6 player build fail writing that file (m_LockCount assertion); their Resources materials keep them instead.
                for (int i = included.arraySize - 1; i >= 0; i--)
                {
                    var existing = included.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (!existing || IsBuiltin(existing)) included.DeleteArrayElementAtIndex(i);
                }
                foreach (string name in names)
                {
                    var shader = Shader.Find(name); if (!shader) continue;
                    if (IsBuiltin(shader)) continue;
                    bool exists = false;
                    for (int i = 0; i < included.arraySize; i++) if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                    if (!exists) { int i = included.arraySize; included.InsertArrayElementAtIndex(i); included.GetArrayElementAtIndex(i).objectReferenceValue = shader; }
                }
                graphics.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ConfigureXR()
        {
            const string path = "Assets/Heritage/Settings/XRGeneralSettings.asset";
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (!perTarget) { perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>(); AssetDatabase.CreateAsset(perTarget,path); }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey,perTarget,true);
            var target = BuildTargetGroup.Standalone;
            if (!perTarget.HasSettingsForBuildTarget(target)) perTarget.CreateDefaultSettingsForBuildTarget(target);
            if (!perTarget.HasManagerSettingsForBuildTarget(target)) perTarget.CreateDefaultManagerSettingsForBuildTarget(target);
            var settings = perTarget.SettingsForBuildTarget(target);
            // Initialize after runtime input actions exist; desktop mode never starts XR.
            settings.InitManagerOnStart = false;
            if (!XRPackageMetadataStore.AssignLoader(settings.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",target))
                throw new InvalidOperationException("Could not assign the OpenXR loader.");
            FeatureHelpers.RefreshFeatures(target);
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
            if (!xr) throw new InvalidOperationException("OpenXR settings were not created.");
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            foreach (var feature in xr.GetFeatures())
                if (feature is OculusTouchControllerProfile || feature is ValveIndexControllerProfile || feature is HTCViveControllerProfile || feature is MicrosoftMotionControllerProfile)
                    feature.enabled = true;
            EditorUtility.SetDirty(xr); EditorUtility.SetDirty(settings); EditorUtility.SetDirty(perTarget);
        }

        [MenuItem("Heritage/Build Windows Demo")]
        public static void BuildWindows() { Configure(); BuildPlayerOnly(); }

        public static void BuildPlayerOnly()
        {
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[]{ScenePath}, locationPathName = "Builds/Windows/VirtualHeritageKarnataka.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/build-result.txt",report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nSize: " + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            Debug.Log("HERITAGE: Windows build succeeded.");
        }

        // Browser build for static hosting (e.g. Vercel). Uses its own lighter pipeline asset so the Windows
        // build's quality settings are never modified; the desktop pipeline is restored afterwards.
        [MenuItem("Heritage/Build WebGL (browser)")]
        public static void BuildWebGL()
        {
            Configure();
            var desktop = GraphicsSettings.defaultRenderPipeline; var desktopQuality = QualitySettings.renderPipeline;
            var web = WebPipeline();
            try
            {
                GraphicsSettings.defaultRenderPipeline = web; QualitySettings.renderPipeline = web;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.dataCaching = true;
                PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
                PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL,new[]{GraphicsDeviceType.OpenGLES3});
                Directory.CreateDirectory("Builds/WebGL");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[]{ScenePath}, locationPathName = "Builds/WebGL",
                    target = BuildTarget.WebGL, options = BuildOptions.None });
                Directory.CreateDirectory("Artifacts");
                File.WriteAllText("Artifacts/webgl-build-result.txt",report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nSize: " + report.summary.totalSize);
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("WebGL build failed: " + report.summary.result);
                File.WriteAllText("Builds/WebGL/vercel.json","{\n  \"headers\": [\n    { \"source\": \"/Build/(.*)\", \"headers\": [ { \"key\": \"Cache-Control\", \"value\": \"public, max-age=31536000, immutable\" } ] }\n  ]\n}\n");
                Debug.Log("HERITAGE: WebGL build succeeded.");
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = desktop; QualitySettings.renderPipeline = desktopQuality;
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64);
                AssetDatabase.SaveAssets();
            }
        }

        static UniversalRenderPipelineAsset WebPipeline()
        {
            const string rendererPath = "Assets/Heritage/Settings/HeritageRendererWeb.asset", pipelinePath = "Assets/Heritage/Settings/HeritageURPWeb.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (!renderer) { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(renderer,rendererPath); }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (!pipeline) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline,pipelinePath); }
            pipeline.msaaSampleCount = 2; pipeline.renderScale = 1; pipeline.shadowDistance = 45;
            pipeline.mainLightShadowmapResolution = 2048; pipeline.shadowCascadeCount = 2; pipeline.supportsHDR = true;
            using (var so = new SerializedObject(pipeline))
            {
                var mode = so.FindProperty("m_AdditionalLightsRenderingMode"); if (mode != null) mode.intValue = 1;
                var limit = so.FindProperty("m_AdditionalLightsPerObjectLimit"); if (limit != null) limit.intValue = 4;
                var soft = so.FindProperty("m_SoftShadowsSupported"); if (soft != null) soft.boolValue = true;
                var softQuality = so.FindProperty("m_SoftShadowQuality"); if (softQuality != null) softQuality.intValue = (int)SoftShadowQuality.Low;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets();
            return pipeline;
        }
    }
}
