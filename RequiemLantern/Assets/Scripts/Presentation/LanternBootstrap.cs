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
            EnterCampaignStage(CampaignCatalog.Chapters[0].Stages[0].Id);revealed=999;
            yield return Capture(directory,"02-story");FinishCurrentStory();
            var firstChapter=CampaignCatalog.Chapters[0];
            if(!CampaignRules.IsCleared(progress.Data,firstChapter.Stages[0])||progress.Data.Coins!=0||!CampaignRules.IsUnlocked(progress.Data,firstChapter.Stages[1]))
                throw new Exception("Story-only stage did not complete freely and unlock stage 1-2.");
            yield return Capture(directory,"12-campaign-map");
            if(!screen.Find("Stage "+firstChapter.Stages[0].Id)||!screen.Find("Stage "+firstChapter.Stages[1].Id)||screen.Find("Stage "+firstChapter.Stages[2].Id)||screen.Find("Chapter 2"))
                throw new Exception("Campaign map displayed future stages or hid completed/current stages.");
            EnterCampaignStage(firstChapter.Stages[1].Id);FinishCurrentStory();
            if(page!="battle"||battle.State.Allies.Count!=1||campaign.Phase!=CampaignPhase.Battle)throw new Exception("Mixed stage 1-2 did not enter its solo Lian battle.");
            yield return Capture(directory,"13-campaign-solo-battle");
            while(battle.State.Outcome==BattleOutcome.Ongoing&&battle.State.Round<=12){
                battle.RecommendLinks();battle.ResolveRound(IgniteGrade.Good);
                if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();
            }
            if(battle.State.Outcome!=BattleOutcome.Victory)throw new Exception("Campaign solo tutorial did not reach victory.");
            ShowResult();
            if(CampaignRules.IsCleared(progress.Data,firstChapter.Stages[1])||campaign.Phase!=CampaignPhase.AfterStory)throw new Exception("Mixed stage completed before its after-story.");
            ContinueCampaign();revealed=999;yield return Capture(directory,"14-campaign-after-story");FinishCurrentStory();
            if(!CampaignRules.IsCleared(progress.Data,firstChapter.Stages[1])||!CampaignRules.IsUnlocked(progress.Data,firstChapter.Stages[2]))throw new Exception("Mixed stage after-story did not complete/unlock its successor.");
            EnterCampaignStage(firstChapter.Stages[2].Id);FinishCurrentStory();
            for(int stageIndex=0;stageIndex<firstChapter.Stages.Count;stageIndex++){
                bool displayed=screen.Find("Stage "+firstChapter.Stages[stageIndex].Id);
                if(displayed!=(stageIndex<4))throw new Exception("Campaign map must show completed 1-1..1-3 and frontier 1-4 only.");
            }
            if(screen.Find("Chapter 2"))throw new Exception("Locked future chapter tab was displayed.");
            yield return Capture(directory,"16-stage-frontier");
            ShowArchive();
            if(!screen.Find("Stage "+firstChapter.Stages[2].Id)||screen.Find("Stage "+firstChapter.Stages[3].Id))throw new Exception("Archive displayed an unfinished stage.");
            var meal=firstChapter.Stages[6];CampaignRules.Complete(progress.Data,meal);
            int coinsBeforeReplay=progress.Data.Coins;
            EnterCampaignStage(meal.Id,true);storyIndex=1;DrawStory();revealed=999;yield return Capture(directory,"15-campaign-ensemble");FinishCurrentStory();
            if(progress.Data.Coins!=coinsBeforeReplay||page!="stages")throw new Exception("Campaign replay changed rewards or did not return to the map.");
            ShowHub();yield return Capture(directory,"03-hub");
            ShowRoster(0);yield return Capture(directory,"04-roster");
            // Later-chapter captures use explicit completed-history fixtures and
            // their own save paths. The ongoing player's tour/save is restored below.
            var tourProgress=progress;var tourSave=save;
            foreach(var chapter in CampaignCatalog.Chapters){
                var first=chapter.Stages[0];UseSmokeChapterFixture(chapter,first);
                ShowCharacterChapter(chapter.FocusCharacterId);
                string focus=CampaignCharacterName(chapter.FocusCharacterId);
                if(page!="stages"||screen.Find("Chapter focus").GetComponent<Text>().text!=focus+"의 본편 이야기")
                    throw new Exception("Main chapter focus did not display for "+focus);
                if(chapter.Number<5&&screen.Find("Chapter "+(chapter.Number+1)))throw new Exception("Focus chapter map exposed a future chapter.");
                yield return Capture(directory,"09-chapter"+chapter.Number+"-"+chapter.FocusCharacterId+"-map");
                string human=chapter.Number==1?"로웬":chapter.Number==2?"엘렌":chapter.Number==3?"이브":null;
                if(human!=null){
                    var scene=chapter.Stages.LastOrDefault(stage=>stage.HumanEnemy&&stage.Before.Any(line=>line.Speaker==human))
                        ??chapter.Stages.LastOrDefault(stage=>stage.Before.Any(line=>line.Speaker==human));
                    if(scene==null||!scene.HasBattle)throw new Exception("Main chapter is missing the human friend's pre-battle conversation: "+human);
                    UseSmokeChapterFixture(chapter,scene);EnterCampaignStage(scene.Id);
                    if(campaign.Active!=scene||campaign.Phase!=CampaignPhase.BeforeStory)throw new Exception("Friend dialogue did not enter the main stage flow.");
                    storyIndex=campaign.CurrentStory.FindIndex(line=>line.Speaker==human);DrawStory();revealed=999;
                    string humanArt=campaign.CurrentStory[storyIndex].Portrait;
                    var humanImage=screen.Find(humanArt+" portrait").GetComponent<RawImage>();
                    if(!humanImage.texture||humanImage.texture.name!=humanArt)throw new Exception("Main human dialogue is missing its own 2D portrait: "+humanArt);
                    yield return Capture(directory,"11-"+chapter.FocusCharacterId+"-human");
                    var plaque=screen.Find("Location plaque");string caption=plaque.Find("Chapter").GetComponent<Text>().text;
                    if(!caption.Contains(focus)||!caption.Contains(scene.Number))throw new Exception("Friend dialogue caption lost its main chapter and stage.");
                    if(screen.Find("Dialogue panel").Find("Speaker").GetComponent<Text>().text!=human)throw new Exception("Main-stage human friend speaker did not display.");
                    int coins=progress.Data.Coins;FinishCurrentStory();
                    if(page!="battle"||campaign.Phase!=CampaignPhase.Battle||CampaignRules.IsCleared(progress.Data,scene)||progress.Data.Coins!=coins)
                        throw new Exception("Main friend story skipped its battle gate or paid rewards before victory.");
                }
            }
            var finalChapter=CampaignCatalog.Chapters.Last();UseSmokeChapterFixture(finalChapter,finalChapter.Stages[0]);
            selectedChapter=1;selectedStage=firstChapter.Stages[6].Id;ShowArchive();
            if(screen.Find("Chapter 5")||!screen.Find("Stage "+firstChapter.Stages[6].Id))throw new Exception("Main archive failed to filter completed history.");
            yield return Capture(directory,"08-main-archive");
            campaign.Exit();progress=tourProgress;save=tourSave;ShowRoster(4);
            var lockedStory=screen.Find("Main chapter noa").GetComponent<Button>();
            if(lockedStory.interactable)throw new Exception("Noa roster story bypassed the locked third main chapter.");
            lockedStory.onClick.Invoke();
            if(page!="roster"||CampaignRules.IsUnlocked(progress.Data,CampaignCatalog.Chapters[2].Stages[0]))throw new Exception("Locked roster chapter action bypassed campaign progress.");
            yield return Capture(directory,"19-roster-focus-lock");
            ShowRoster(0);screen.Find("Main chapter lian").GetComponent<Button>().onClick.Invoke();
            if(page!="stages"||selectedChapter!=1)throw new Exception("Roster story button did not open the character's main chapter map.");
            if(progress.Data.Coins!=coinsBeforeReplay)throw new Exception("Main chapter previews unexpectedly changed the ongoing player's rewards.");
            ShowStages();yield return Capture(directory,"05-stages");
            int coinsBeforeLegacy=progress.Data.Coins;
            // Use an actual shuffled duplicate S1 hand so the UI is exercised with
            // two distinct copies, rather than substituting an authored test hand.
            int duplicateSeed=Enumerable.Range(0,100).First(seed=>{
                var candidate=BattleEngine.CreatePrototype(progress.Data,seed);var actor=candidate.Unit("lian");
                return actor.SkillHand.Count==2&&actor.SkillHand.All(card=>candidate.Skill(actor.Id,card.SkillId).Number==1);
            });
            campaign.Exit();battle=BattleEngine.CreatePrototype(progress.Data,duplicateSeed);PrepareBattlePresentation();
            var duplicateCards=battle.Unit("lian").SkillHand.ToArray();
            if(screen.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Skill card ",StringComparison.Ordinal))!=2)
                throw new Exception("Battle sidebar did not display exactly two offered skill cards.");
            screen.Find("Skill card "+duplicateCards[0].Id).GetComponent<Button>().onClick.Invoke();Target(battle.State.Intents[0].Id);
            if(battle.State.Links.Single(l=>l.ActorId=="lian").CardId!=duplicateCards[0].Id)throw new Exception("Selected card identity was not retained in its action link.");
            SelectSlot("lian",1);
            if(selectedCard!=duplicateCards[1].Id||screen.Find("Skill card "+duplicateCards[0].Id).GetComponent<Button>().interactable)
                throw new Exception("Reserved duplicate card remained available for the other action slot.");
            Target(battle.State.Intents[1].Id);
            if(battle.State.Links.Where(l=>l.ActorId=="lian").Select(l=>l.CardId).Distinct().Count()!=2)throw new Exception("Two actions reused the same offered card.");
            SelectSlot("lian",0);if(selectedCard!=duplicateCards[0].Id)throw new Exception("Selecting an existing action did not restore its card.");
            SelectSlot("lian",1);if(selectedCard!=duplicateCards[1].Id)throw new Exception("Selecting the second existing action did not restore its card.");
            yield return Capture(directory,"17-skill-bag");
            battle.RecommendLinks();DrawBattle();yield return Capture(directory,"06-battle");
            smokePhase="first round battlefield effects (60 second deadline)";
            smokeDeadline=Time.realtimeSinceStartup+60;
            var initialHealth=battle.State.Allies.Concat(battle.State.Enemies).ToDictionary(unit=>unit.Id,unit=>unit.Hp);
            bool requiresClash=battle.State.Links.Any(link=>link.Kind==LinkKind.Clash);
            bool capturedImpact=false,capturedWindup=false,capturedRecoil=false,sawClash=false,sawHealthChange=false;
            bool capturedFlameSuspense=false,completedFlameReveal=false;
            FlameRevealMarker testedFlames=null;int flameHealth=0,lastRevealed=0;
            CombatPhaseMarker testedMelee=null;Rect windupUv=default;int windupHealth=0;
            assisted=true;autoMode=false;
            StartCoroutine(Resolve());
            if(!resolving||page!="resolve")throw new Exception("Smoke did not enter the actual Resolve presentation coroutine.");
            while(resolving||page=="resolve"){
                if(Time.realtimeSinceStartup>smokeDeadline)throw new TimeoutException("Battlefield Resolve did not return within 60 seconds.");
                if(!string.IsNullOrEmpty(smokeAsyncError))throw new Exception("Resolve raised an asynchronous exception: "+smokeAsyncError);
                var reveal=overlay?overlay.GetComponentInChildren<FlameRevealMarker>():null;
                if(!capturedFlameSuspense&&reveal&&reveal.Total>0&&reveal.Revealed==0){
                    testedFlames=reveal;flameHealth=visibleHealth.Values.Sum();lastRevealed=0;
                    yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,"18-flame-suspense");capturedFlameSuspense=true;
                }
                if(testedFlames&&!completedFlameReveal){
                    if(testedFlames.Revealed<lastRevealed||testedFlames.Revealed>testedFlames.Total)throw new Exception("Flame reveal did not progress monotonically to its final count.");
                    if(visibleHealth.Values.Sum()!=flameHealth)throw new Exception("Health changed while flames were still revealing.");
                    lastRevealed=testedFlames.Revealed;completedFlameReveal=lastRevealed==testedFlames.Total;
                }
                var damage=overlay?overlay.Find("FX Damage"):null;
                var feedback=damage?damage.Find("Battle feedback"):null;
                var number=feedback?feedback.GetComponent<Text>():null;
                var marker=damage?damage.GetComponent<CombatPhaseMarker>():null;
                var contact=damage?damage.Find("Weapon contact"):null;
                if(!capturedWindup&&marker&&marker.Phase=="windup"){
                    if(!marker.AtlasActive||!marker.Attacker||marker.Attacker.uvRect.width>=.5f)throw new Exception("Melee is missing its attack pose atlas.");
                    if(damage.GetComponentsInChildren<ThreadGraphic>().Length!=0)throw new Exception("Melee still contains a travelling light beam.");
                    testedMelee=marker;windupUv=marker.Attacker.uvRect;windupHealth=visibleHealth.Values.Sum();
                    yield return new WaitForEndOfFrame();
                    WriteSmokeScreenshot(directory,"06a-windup");capturedWindup=true;
                }
                var clashFx=overlay?(overlay.Find("FX Clash")??overlay.Find("FX Tie")):null;
                var clashFeedback=clashFx?clashFx.Find("Battle feedback"):null;
                if(clashFeedback&&clashFeedback.GetComponent<Text>().color.a>.35f&&clashFx.Find("Weapon contact"))sawClash=true;
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
            if(!capturedFlameSuspense||!completedFlameReveal)throw new Exception("Smoke missed the unrevealed flame phase or its sequential completion before damage.");
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
            if(progress.Data.Coins!=coinsBeforeLegacy+120)throw new Exception("Smoke reward was not granted exactly once.");
            if(save.Load().Coins!=coinsBeforeLegacy+120)throw new Exception("Smoke reward was not persisted to the isolated save.");
            string[] screenshots={"01-title","02-story","03-hub","04-roster","05-stages","06-battle","06a-windup","06b-impact","06c-recoil","07-result",
                "08-main-archive","09-chapter1-lian-map","09-chapter2-sera-map","09-chapter3-noa-map","09-chapter4-heidel-map","09-chapter5-minato-map",
                "11-lian-human","11-sera-human","11-noa-human","12-campaign-map","13-campaign-solo-battle","14-campaign-after-story","15-campaign-ensemble",
                "16-stage-frontier","17-skill-bag","18-flame-suspense","19-roster-focus-lock"};
            float writesDeadline=Time.realtimeSinceStartup+10;
            while(screenshots.Any(name=>!FreshSmokeScreenshot(Path.Combine(directory,name+".png")))){
                if(Time.realtimeSinceStartup>writesDeadline)throw new Exception("Smoke did not write all twenty-seven fresh screenshots.");yield return null;
            }
            if(!string.IsNullOrEmpty(smokeAsyncError))throw new Exception("Asynchronous smoke failure: "+smokeAsyncError);
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: 27 fresh screenshots; campaign map, free story-only completion, mixed solo battle and after-story, ensemble replay; title, story, hub, roster, stages, battle, windup, impact, recoil, victory; five character-focused main chapters and three integrated human friend encounters.\nEach focus chapter map showed its main character; Rowan, Ellen and Eve spoke in normal pre-battle stages whose completion remained gated by victory. Roster story buttons opened unlocked main chapter maps and guarded locked chapters; archive showed completed main stages only. Campaign 1-1 unlocked 1-2 without rewards; 1-2 completed only after victory and after-story; replay changed no rewards. Future stages/chapter tabs are hidden; after 1-3 the map shows 1-1 through 1-4. Two offered duplicate skill cards displayed, reserved individually and restored by action selection. Flame suspense began with zero flames revealed, progressed sequentially to all flames and left displayed health unchanged until damage.\nActual Resolve coroutine completed round 1 within 60 seconds; atlas pose changed from windup at weapon contact, no melee light beams, sequential HP changed at contact"+(sawClash?", weapon clash captured":"")+"; round 2 returned with synchronized HP/gauges.\nReward granted once and persisted to isolated smoke-progress.xml.\n");
            Debug.Log("REQUIEM_LANTERN_SMOKE_PASS");Application.Quit(0);
        }
        void UseSmokeChapterFixture(ChapterData chapter,StageData current)
        {
            campaign.Exit();progress=new ProgressService(new ProgressData());
            foreach(var preceding in CampaignCatalog.Chapters.SelectMany(c=>c.Stages).TakeWhile(s=>s.Id!=current.Id))CampaignRules.Complete(progress.Data,preceding);
            save=new SaveStore(Path.Combine(smokeDirectory,"chapter-"+chapter.Number+"-fixture.xml"));
            selectedChapter=chapter.Number;selectedStage=current.Id;
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
