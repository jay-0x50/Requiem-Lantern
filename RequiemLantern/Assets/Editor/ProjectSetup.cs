using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RequiemLantern.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("Requiem Lantern/Open Main Scene")]
        public static void OpenMainScene()
        {
            const string path="Assets/Scenes/Main.unity";
            if(!File.Exists(path))throw new System.IO.FileNotFoundException("Main scene is missing.",path);
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var current=EditorSceneManager.GetActiveScene();
            if(current.path!=path){
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
                EditorSceneManager.OpenScene(path);
            }
        }
        [MenuItem("Requiem Lantern/Prepare Project")]
        public static void Prepare()
        {
            PlayerSettings.companyName="Requiem Lantern Studio";
            PlayerSettings.productName="Requiem Lantern";
            PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground=true;
            EditorSettings.defaultBehaviorMode=EditorBehaviorMode.Mode2D;
            if(File.Exists("Assets/Scenes/Main.unity")){
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};
                AssetDatabase.SaveAssets();return;
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
            camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);
            camera.GetComponent<Camera>().orthographic=true;camera.GetComponent<Camera>().backgroundColor=new Color(.025f,.035f,.05f);
            var light=new GameObject("Global 2D Light",typeof(Light2D)).GetComponent<Light2D>();light.lightType=Light2D.LightType.Global;
            Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/Main.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Requiem Lantern/Build Windows Preview")]
        public static void BuildWindows()
        {
            Prepare();string folder=Path.GetFullPath("../Builds/Windows");Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName=Path.Combine(folder,"RequiemLantern.exe"),
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Windows build failed: "+report.summary.result);
        }
    }
    public sealed class ArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Art/"))return;
            var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}
