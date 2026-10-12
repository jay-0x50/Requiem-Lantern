using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using RequiemLantern.Core;

namespace RequiemLantern.Presentation
{
    public static class LanternBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if(!UnityEngine.Object.FindAnyObjectByType<LanternApp>())new GameObject("Requiem Lantern",typeof(LanternApp));
        }
    }
    public partial class LanternApp
    {
        bool smokeRunning,smokeFailed;
        float smokeDeadline;
        string smokeDirectory,smokePhase,smokeAsyncError;
        DateTime smokeStartedUtc;

        IEnumerator SmokeTour()
        {
            smokeRunning=true;smokeFailed=false;smokeAsyncError=null;smokePhase="screen tour";
            smokeStartedUtc=DateTime.UtcNow;smokeDeadline=Time.realtimeSinceStartup+120;
            Application.LogCallback onLog=(message,trace,type)=>{
                if(!smokeFailed&&(type==LogType.Exception||type==LogType.Assert))smokeAsyncError=message+"\n"+trace;
            };
            Application.logMessageReceived+=onLog;
            var watchdog=StartCoroutine(SmokeWatchdog());
            var steps=new Stack<IEnumerator>();steps.Push(SmokeSteps());
            try {
                // Drive nested enumerators ourselves so errors inside Capture/SmokeSteps
                // produce a failure marker and exit code instead of silently ending a coroutine.
                while(steps.Count>0&&!smokeFailed){
                    if(!string.IsNullOrEmpty(smokeAsyncError)){FailSmoke(new Exception("Asynchronous Unity exception: "+smokeAsyncError));yield break;}
                    bool moved=false;object waiting=null;Exception failure=null;
                    try {moved=steps.Peek().MoveNext();if(moved)waiting=steps.Peek().Current;}
                    catch(Exception error){failure=error;}
                    if(failure!=null){FailSmoke(failure);yield break;}
                    if(!moved){(steps.Pop() as IDisposable)?.Dispose();continue;}
                    if(waiting is IEnumerator nested)steps.Push(nested);else yield return waiting;
                }
            }
            finally {
                smokeRunning=false;Application.logMessageReceived-=onLog;
                if(watchdog!=null)StopCoroutine(watchdog);
                while(steps.Count>0)(steps.Pop() as IDisposable)?.Dispose();
            }
        }
        IEnumerator SmokeWatchdog()
        {
            // This keeps advancing even if a screenshot waits forever for end-of-frame.
            while(smokeRunning&&!smokeFailed){
                if(!string.IsNullOrEmpty(smokeAsyncError)){FailSmoke(new Exception("Asynchronous Unity exception: "+smokeAsyncError));yield break;}
                if(Time.realtimeSinceStartup>smokeDeadline){FailSmoke(new TimeoutException("Smoke timed out during "+smokePhase+"."));yield break;}
                yield return null;
            }
        }
        void FailSmoke(Exception error)
        {
            if(smokeFailed)return;smokeFailed=true;
            try {if(!string.IsNullOrEmpty(smokeDirectory))File.WriteAllText(Path.Combine(smokeDirectory,"smoke-result.txt"),"FAIL: "+smokePhase+"\n"+error+"\n");}
            catch(Exception){ }
            Debug.LogException(error);Application.Quit(1);
        }
        IEnumerator SmokeSteps()
        {
            // A separate save prevents the automated tour from changing a player's progress.
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--capture-dir");
            string directory=index>=0&&index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"RUNNING: Unity screen and battlefield effects smoke tour.\n");
            save=new SaveStore(Path.Combine(directory,"smoke-progress.xml"));progress=new ProgressService(new ProgressData());
            // Deterministic visual capture; the user's PlayerPrefs are never written.
            assisted=true;autoMode=false;reducedMotion=false;
            yield return Capture(directory,"01-title");
            StartStory(StoryCatalog.Prologue,ShowHub);storyIndex=6;DrawStory();revealed=999;
            yield return Capture(directory,"02-story");ShowHub();yield return Capture(directory,"03-hub");
            ShowRoster(0);yield return Capture(directory,"04-roster");
            ShowCharacterStories();yield return Capture(directory,"08-character-stories");
            foreach(var entry in CharacterStoryCatalog.All){
                StartStory(entry.Lines,ShowCharacterStories);yield return Capture(directory,"09-"+entry.Id+"-story");
                var plaque=screen.Find("Location plaque");
                if(!plaque||plaque.Find("Chapter").GetComponent<Text>().text!=entry.Lines[0].Caption)throw new Exception("Character story caption did not display for "+entry.Id);
                string humanName=entry.Id=="lian"?"로웬":entry.Id=="sera"?"엘렌":"이브";
                int humanLine=entry.Lines.FindIndex(line=>line.Speaker==humanName);
                if(humanLine<0)throw new Exception("Character story is missing its human antagonist: "+entry.Id);
                storyIndex=humanLine;DrawStory();revealed=999;
                yield return Capture(directory,"11-"+entry.Id+"-human");
                if(screen.Find("Dialogue panel").Find("Speaker").GetComponent<Text>().text!=humanName)throw new Exception("Human antagonist speaker did not display: "+humanName);
                storyIndex=entry.Lines.Count-1;DrawStory();revealed=999;
                yield return Capture(directory,"10-"+entry.Id+"-ending");NextStory();
                if(page!="character-stories")throw new Exception("Character story did not return to its selection screen: "+entry.Id);
            }
            if(progress.Data.Coins!=0||progress.Data.PrologueCompleted)throw new Exception("Reading character stories unexpectedly changed progress or rewards.");
            ShowStages();yield return Capture(directory,"05-stages");
            StartBattle();battle.RecommendLinks();DrawBattle();yield return Capture(directory,"06-battle");
            smokePhase="first round battlefield effects (60 second deadline)";
            smokeDeadline=Time.realtimeSinceStartup+60;
            var initialHealth=battle.State.Allies.Concat(battle.State.Enemies).ToDictionary(unit=>unit.Id,unit=>unit.Hp);
            bool requiresClash=battle.State.Links.Any(link=>link.Kind==LinkKind.Clash);
            bool capturedImpact=false,capturedWindup=false,capturedRecoil=false,sawClash=false,sawHealthChange=false;
            CombatPhaseMarker testedMelee=null;Rect windupUv=default;int windupHealth=0;
            assisted=true;autoMode=false;
            StartCoroutine(Resolve());
            if(!resolving||page!="resolve")throw new Exception("Smoke did not enter the actual Resolve presentation coroutine.");
            while(resolving||page=="resolve"){
                if(Time.realtimeSinceStartup>smokeDeadline)throw new TimeoutException("Battlefield Resolve did not return within 60 seconds.");
                if(!string.IsNullOrEmpty(smokeAsyncError))throw new Exception("Resolve raised an asynchronous exception: "+smokeAsyncError);
                var damage=overlay?overlay.Find("FX Damage"):null;
                var feedback=damage?damage.Find("Battle feedback"):null;
                var number=feedback?feedback.GetComponent<Text>():null;
                var marker=damage?damage.GetComponent<CombatPhaseMarker>():null;
                var contact=damage?damage.Find("Weapon contact"):null;
                if(!capturedWindup&&marker&&marker.Phase=="windup"&&(!requiresClash||marker.Clash)){
                    if(!marker.AtlasActive||!marker.Attacker||marker.Attacker.uvRect.width>=.5f)throw new Exception("Melee is missing its attack pose atlas.");
                    if(damage.GetComponentsInChildren<ThreadGraphic>().Length!=0)throw new Exception("Melee still contains a travelling light beam.");
                    testedMelee=marker;windupUv=marker.Attacker.uvRect;windupHealth=visibleHealth.Values.Sum();
                    yield return new WaitForEndOfFrame();
                    WriteSmokeScreenshot(directory,"06a-windup");capturedWindup=true;
                }
                if(marker&&marker.Clash&&contact&&number&&number.color.a>.35f)sawClash=true;
                if(initialHealth.Any(pair=>visibleHealth.TryGetValue(pair.Key,out var hp)&&hp<pair.Value))sawHealthChange=true;
                if(!capturedImpact&&marker&&marker==testedMelee&&number&&number.color.a>.35f&&contact){
                    if(marker.Attacker.uvRect==windupUv)throw new Exception("Attack never changed from its windup pose at contact.");
                    if(visibleHealth.Values.Sum()>=windupHealth)throw new Exception("Damage was not applied at visible weapon contact.");
                    yield return new WaitForEndOfFrame();
                    if(damage&&number&&number.color.a>.35f&&page=="resolve"){
                        var blocker=overlay.Find("Resolution input lock");
                        if(!blocker||blocker.GetComponent<Image>().color.a!=0)throw new Exception("Resolution obscured the battlefield with an opaque input blocker.");
                        WriteSmokeScreenshot(directory,"06b-impact");capturedImpact=true;
                    }
                }
                if(capturedImpact&&!capturedRecoil&&marker&&marker==testedMelee&&marker.Phase=="recoil"){
                    yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,"06c-recoil");capturedRecoil=true;
                }
                yield return null;
            }
            if(!capturedWindup||!capturedImpact||!capturedRecoil||!sawHealthChange||(requiresClash&&!sawClash))throw new Exception("Smoke missed atlas windup/contact/recoil, weapon clash or sequential health change.");
            if(page!="battle"||resolving||overlay||battle.State.Round!=2||battle.State.Outcome!=BattleOutcome.Ongoing)
                throw new Exception("First visual round did not return cleanly to round 2 planning.");
            foreach(var unit in battle.State.Allies.Concat(battle.State.Enemies)){
                if(!visibleHealth.TryGetValue(unit.Id,out var shown)||shown!=unit.Hp)throw new Exception("Visible HP differs from core HP for "+unit.Id);
                if(healthFills.TryGetValue(unit.Id,out var fills))foreach(var fill in fills){
                    float expected=gaugeWidths[fill]*(float)unit.Hp/unit.MaxHp;
                    if(Mathf.Abs(fill.rectTransform.sizeDelta.x-expected)>.1f)throw new Exception("Health gauge did not synchronize for "+unit.Id);
                }
            }
            smokePhase="victory, reward persistence and screenshot output";smokeDeadline=Time.realtimeSinceStartup+60;
            while(battle.State.Outcome==BattleOutcome.Ongoing&&battle.State.Round<=12){
                if(Time.realtimeSinceStartup>smokeDeadline)throw new TimeoutException("Remaining smoke battle exceeded its deadline.");
                battle.RecommendLinks();battle.ResolveRound(IgniteGrade.Good);
                if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();
            }
            if(battle.State.Outcome!=BattleOutcome.Victory)throw new Exception("Smoke battle failed to reach victory.");
            ShowResult();yield return Capture(directory,"07-result");
            if(progress.Data.Coins!=120)throw new Exception("Smoke reward was not granted exactly once.");
            if(save.Load().Coins!=120)throw new Exception("Smoke reward was not persisted to the isolated save.");
            string[] screenshots={"01-title","02-story","03-hub","04-roster","05-stages","06-battle","06a-windup","06b-impact","06c-recoil","07-result",
                "08-character-stories","09-lian-story","10-lian-ending","09-sera-story","10-sera-ending","09-noa-story","10-noa-ending",
                "11-lian-human","11-sera-human","11-noa-human"};
            float writesDeadline=Time.realtimeSinceStartup+10;
            while(screenshots.Any(name=>!FreshSmokeScreenshot(Path.Combine(directory,name+".png")))){
                if(Time.realtimeSinceStartup>writesDeadline)throw new Exception("Smoke did not write all twenty fresh screenshots.");yield return null;
            }
            if(!string.IsNullOrEmpty(smokeAsyncError))throw new Exception("Asynchronous smoke failure: "+smokeAsyncError);
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: 20 fresh screenshots; title, story, hub, roster, stages, battle, windup, impact, recoil, victory; character story selection and all three introductions/endings/human antagonists.\nRowan, Ellen and Eve speaker labels displayed; character stories returned to selection after reading and did not change progress/rewards.\nActual Resolve coroutine completed round 1 within 60 seconds; atlas pose changed from windup at weapon contact, no melee light beams, sequential HP changed at contact"+(sawClash?", weapon clash captured":"")+"; round 2 returned with synchronized HP/gauges.\nReward granted once and persisted to isolated smoke-progress.xml.\n");
            Debug.Log("REQUIEM_LANTERN_SMOKE_PASS");Application.Quit(0);
        }
        bool FreshSmokeScreenshot(string path)
        {return File.Exists(path)&&new FileInfo(path).Length>0&&File.GetLastWriteTimeUtc(path)>=smokeStartedUtc.AddSeconds(-1);}
        IEnumerator Capture(string dir,string name)
        {
            yield return new WaitForSecondsRealtime(.7f);yield return new WaitForEndOfFrame();
            WriteSmokeScreenshot(dir,name);yield return new WaitForSecondsRealtime(.1f);
        }
        void WriteSmokeScreenshot(string directory,string name)
        {
            Texture2D texture=null;
            try {
                // Encode/write in managed code so Unicode output directories work,
                // and a native asynchronous screenshot failure cannot leave a false PASS.
                texture=ScreenCapture.CaptureScreenshotAsTexture();
                if(!texture||texture.width<16||texture.height<16)throw new Exception("Screen capture returned no usable texture for "+name+".");
                var png=texture.EncodeToPNG();
                if(png==null||png.Length<64||png[0]!=137||png[1]!=80||png[2]!=78||png[3]!=71)throw new Exception("Screen capture PNG encoding failed for "+name+".");
                string path=Path.Combine(directory,name+".png");File.WriteAllBytes(path,png);
                if(!File.Exists(path)||new FileInfo(path).Length!=png.Length)throw new IOException("Screen capture was not completely written: "+path);
            }
            finally {if(texture)Destroy(texture);}
        }
    }
}
