using System.IO;
using System.Linq;
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
            Prepare();ValidateStoryArt();ValidateCombatAudio();ValidateBattleAvatars();ValidateEnemyGroups();string folder=Path.GetFullPath("../Builds/Windows");Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName=Path.Combine(folder,"RequiemLantern.exe"),
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Windows build failed: "+report.summary.result);
        }
        static void ValidateStoryArt()
        {
            var lines=RequiemLantern.Core.CampaignCatalog.Chapters.SelectMany(c=>c.Stages).SelectMany(s=>s.Before.Concat(s.After));
            var portraits=lines.SelectMany(line=>new[]{line.Portrait,line.ListenerPortrait})
                .Concat(RequiemLantern.Core.CampaignCatalog.Chapters.SelectMany(c=>c.Stages).Where(s=>s.HasBattle).Select(s=>s.EnemyArt))
                .Where(id=>!string.IsNullOrEmpty(id)).Distinct();
            foreach(string id in portraits){
                var texture=Resources.Load<Texture2D>("Art/"+id);
                if(!texture){
                    string path="Assets/Resources/Art/"+id+".png";
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                    texture=Resources.Load<Texture2D>("Art/"+id);
                    var main=AssetDatabase.LoadMainAssetAtPath(path);
                    Debug.Log("STORY_ART_IMPORT: "+id+"; file="+File.Exists(path)+"; asset="+(main?main.GetType().Name:"missing")+"; resource="+(texture?texture.name:"missing"));
                }
                if(!texture)throw new System.Exception("Story art cannot be loaded: Art/"+id);
            }
        }
        static void ValidateCombatAudio()
        {
            foreach(string family in RequiemLantern.Presentation.CombatAudioBank.Families)for(int i=1;i<=3;i++){
                string path="Audio/Combat/"+family+"_"+i.ToString("00");
                var clip=Resources.Load<AudioClip>(path);
                if(!clip||clip.samples<1||clip.channels!=2)throw new System.Exception("Combat audio cannot be loaded: "+path);
            }
        }
        static void ValidateBattleAvatars()
        {
            foreach(string id in RequiemLantern.Presentation.BattleAvatar.AllyArtIds){
                var texture=RequiemLantern.Presentation.BattleAvatar.LoadAtlas(id,true,out var key,out var layout);
                if(!texture||layout==null)throw new System.Exception("Battle SD art or six-pose metadata cannot be loaded: "+key);
                var original=Resources.Load<Texture2D>("Art/"+id);
                if(!original||original==texture)throw new System.Exception("Original menu/dialogue portrait must remain separate: "+id);
            }
        }
        static void ValidateEnemyGroups()
        {
            foreach(var stage in RequiemLantern.Core.CampaignCatalog.Chapters.SelectMany(chapter=>chapter.Stages).Where(stage=>stage.HasBattle)){
                var battle=RequiemLantern.Core.CampaignBattleFactory.Create(stage,new RequiemLantern.Core.ProgressData(),0);
                if(battle.State.Enemies.Count!=stage.EnemyCount||battle.State.Enemies.Count<1||battle.State.Enemies.Count>3)
                    throw new System.Exception("Invalid enemy group for stage: "+stage.Id);
                if(battle.State.IsBoss!=stage.IsBoss||battle.State.Allies.Any(actor=>actor.SlotCount!=1))
                    throw new System.Exception("Battle action progression lost its stage boss flag or round-1 action limit: "+stage.Id);
                foreach(var enemy in battle.State.Enemies)if(!Resources.Load<Texture2D>("Art/"+enemy.Portrait))
                    throw new System.Exception("Enemy group art is missing: "+stage.Id+" / "+enemy.Portrait);
                if(battle.State.Intents.Any(intent=>!battle.State.Enemies.Any(enemy=>enemy.Id==intent.ActorId)))
                    throw new System.Exception("Enemy intent lost its owning enemy: "+stage.Id);
            }
        }
    }
    public sealed class ArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            bool battleSD=assetPath.StartsWith("Assets/Resources/Combat/")&&assetPath.EndsWith("SDActions.png");
            if(!assetPath.StartsWith("Assets/Resources/Art/")&&!battleSD)return;
            var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;
            importer.textureShape=TextureImporterShape.Texture2D;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=battleSD?4096:2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}
