#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HandPlayground.Editor
{
    /// <summary>
    /// Batch entry point: -executeMethod HandPlayground.Editor.PortBuild.BuildAndroid
    /// Everything that normally lives in Project Settings is set here so the build is reproducible.
    /// XR Plug-in Management is configured through reflection, so a signature difference fails with a clear message instead of a compile error.
    /// </summary>
    public static class PortBuild
    {
        const string Deps =
            "    implementation 'com.google.mediapipe:tasks-vision:0.10.14'\n" +
            "    implementation 'androidx.camera:camera-core:1.3.4'\n" +
            "    implementation 'androidx.camera:camera-camera2:1.3.4'\n" +
            "    implementation 'androidx.camera:camera-lifecycle:1.3.4'\n";

        public static void BuildAndroid()
        {
            try
            {
                ConfigurePlayer();
                ConfigureGradleTemplates();
                ConfigureXR();
                string outPath = Environment.GetEnvironmentVariable("PORT_APK_PATH");
                if (string.IsNullOrEmpty(outPath)) outPath = "Builds/Android/HandPlaygroundCardboard.apk";
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/Main.unity" },
                    locationPathName = outPath, target = BuildTarget.Android, options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(opts);
                Debug.Log("[PortBuild] Result: " + report.summary.result + ", size " + report.summary.totalSize + " bytes, " + report.summary.totalErrors + " errors");
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 2);
            }
            catch (Exception e) { Debug.LogError("[PortBuild] FAILED: " + e); EditorApplication.Exit(1); }
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "HandTracking Playground (Cardboard)";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.handplayground.cardboard");
            PlayerSettings.bundleVersion = "0.1-cardboard";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;   // add ARMv7 via PORT_ARMV7=1
            if (Environment.GetEnvironmentVariable("PORT_ARMV7") == "1") PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARMv7;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });  // Cardboard plugin 1.20: GLES3
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.MTRendering = true;
            PlayerSettings.gpuSkinning = true;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Debug;
            EditorUserBuildSettings.buildAppBundle = false;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new Exception("Run Unity with -buildTarget Android.");
            PlayerSettings.virtualRealitySupported = false;     // legacy VR must be OFF with XR Plug-in Management
        }

        static void ConfigureGradleTemplates()
        {
            string tpl = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/Tools/GradleTemplates");
            string dst = "Assets/Plugins/Android";
            Directory.CreateDirectory(dst);
            string main = File.ReadAllText(Path.Combine(tpl, "mainTemplate.gradle"));
            if (!main.Contains("**DEPS**")) throw new Exception("Unity's mainTemplate.gradle has no **DEPS** marker; update Deps injection in PortBuild.");
            if (!main.Contains("tasks-vision")) main = main.Replace("**DEPS**", "**DEPS**\n" + Deps);
            File.WriteAllText(Path.Combine(dst, "mainTemplate.gradle"), main);
            string props = File.ReadAllText(Path.Combine(tpl, "gradleTemplate.properties"));
            if (!props.Contains("android.useAndroidX")) props += "\nandroid.useAndroidX=true\nandroid.enableJetifier=true\n";
            File.WriteAllText(Path.Combine(dst, "gradleTemplate.properties"), props);
            AssetDatabase.Refresh();
        }

        static Type Find(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            { var t = a.GetType(fullName, false); if (t != null) return t; }
            return null;
        }

        static void ConfigureXR()
        {
            const string key = "com.unity.xr.management.loader_settings";
            var tPer = Find("UnityEngine.XR.Management.XRGeneralSettingsPerBuildTarget");
            var tGen = Find("UnityEngine.XR.Management.XRGeneralSettings");
            var tMgr = Find("UnityEngine.XR.Management.XRManagerSettings");
            var tStore = Find("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
            if (tPer == null || tGen == null || tMgr == null || tStore == null) throw new Exception("XR Plug-in Management types not found (package com.unity.xr.management missing?).");
            string xrDir = "Assets/XR"; Directory.CreateDirectory(xrDir + "/Settings"); AssetDatabase.Refresh();
            UnityEngine.Object per;
            if (!EditorBuildSettings.TryGetConfigObject(key, out per) || per == null)
            {
                per = ScriptableObject.CreateInstance(tPer);
                AssetDatabase.CreateAsset(per, xrDir + "/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(key, per, true);
            }
            var bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var settingsFor = tPer.GetMethod("SettingsForBuildTarget", bf);
            var create = tPer.GetMethod("CreateDefaultSettingsForBuildTarget", bf);
            if (settingsFor == null) throw new Exception("XRGeneralSettingsPerBuildTarget.SettingsForBuildTarget not found.");
            object gen = settingsFor.Invoke(per, new object[] { BuildTargetGroup.Android });
            if (gen == null && create != null) { create.Invoke(per, new object[] { BuildTargetGroup.Android }); gen = settingsFor.Invoke(per, new object[] { BuildTargetGroup.Android }); }
            if (gen == null) throw new Exception("Could not create XR settings for Android.");
            var managerProp = tGen.GetProperty("Manager", bf);
            object mgr = managerProp.GetValue(gen, null);
            if (mgr == null)
            {
                var m = ScriptableObject.CreateInstance(tMgr);
                AssetDatabase.CreateAsset(m, xrDir + "/Settings/Android Providers.asset");
                managerProp.SetValue(gen, m, null); mgr = m;
            }
            var init = tGen.GetProperty("InitManagerOnStart", bf);
            if (init != null) init.SetValue(gen, true, null);
            var assign = tStore.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.Name == "AssignLoader" && x.GetParameters().Length == 3);
            if (assign == null) throw new Exception("XRPackageMetadataStore.AssignLoader not found.");
            bool ok = (bool)assign.Invoke(null, new object[] { mgr, "Google.XR.Cardboard.XRLoader", BuildTargetGroup.Android });
            if (!ok) throw new Exception("Cardboard loader could not be assigned (Cardboard XR plugin not installed?). Open Project Settings > XR Plug-in Management > Android and tick 'Cardboard XR Plugin'.");
            EditorUtility.SetDirty(per); EditorUtility.SetDirty((UnityEngine.Object)gen); EditorUtility.SetDirty((UnityEngine.Object)mgr);
            AssetDatabase.SaveAssets();
            Debug.Log("[PortBuild] Cardboard XR loader assigned for Android.");
        }
    }

    /// <summary>
    /// Android-only texture budget applied at import time (original asset files are untouched): the avatar's 4K PNGs would
    /// cost ~64 MB of RAM each uncompressed. ASTC 6x6, max 2048.
    /// </summary>
    public sealed class PortTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/3DModels") && !assetPath.StartsWith("Assets/Textures")) return;
            var ti = (TextureImporter)assetImporter;
            var s = ti.GetPlatformTextureSettings("Android");
            s.overridden = true; s.maxTextureSize = 2048;
            s.format = TextureImporterFormat.ASTC_6x6; s.compressionQuality = 50;
            ti.SetPlatformTextureSettings(s);
        }
    }
}
#endif
