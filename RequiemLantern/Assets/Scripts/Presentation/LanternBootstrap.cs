using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
            assisted=true;autoMode=false;reducedMotion=false;sound=true;
            if(combatAudio==null||combatAudio.MissingCount!=0)throw new Exception("Combat PCM assets are missing.");
            foreach(var id in BattleAvatar.AllyArtIds){
                var texture=BattleAvatar.LoadAtlas(id,true,out var key,out var layout);
                if(!texture||layout==null||texture==Resources.Load<Texture2D>("Art/"+id))
                    throw new Exception("Battle SD atlas or original portrait separation is missing: "+id);
                var fixture=MakeRect(stage,"SD pose fixture "+id,0,0,1920,1080);
                var avatar=BattleAvatar.Create(fixture,id,Resources.Load<Texture2D>("Art/"+id),false,new Vector2(500,700),BattleAvatar.AllyBodyHeight);
                using(var performer=new CombatPosePlayer(fixture,avatar,id,true,BattleAvatar.AllyBodyHeight,"SD continuity "+id)){
                    if(!performer.HasSDAtlas||!performer.HasFrameLayout||performer.OriginalFoot!=new Vector2(500,700))
                        throw new Exception("SD planning avatar lost its planted foot or atlas when entering animation: "+id);
                    for(int pose=0;pose<6;pose++){
                        performer.SetPose(pose);performer.PlaceFoot(new Vector2(500,700));
                        if(performer.Image.texture!=texture||performer.Image.uvRect.width>=.5f||performer.Image.rectTransform.anchoredPosition!=new Vector2(500,-700))
                            throw new Exception("SD pose continuity failed: "+id+" / "+pose);
                    }
                }
                if(!avatar.enabled)throw new Exception("SD planning avatar was not restored after its action: "+id);
                fixture.gameObject.SetActive(false);Destroy(fixture.gameObject);
            }
            int beforeMuted=combatAudio.PlayedCount;
            if(combatAudio.Play("clash",false,true)||combatAudio.PlayedCount!=beforeMuted||combatAudio.ActiveCount!=0)
                throw new Exception("Muted combat audio created an active voice.");
            if(!combatAudio.Play("clash",true))throw new Exception("Combat PCM did not preload for playback.");
            combatAudio.Update(false);
            if(combatAudio.ActiveCount!=0)throw new Exception("Muting did not stop active combat audio.");
            yield return Capture(directory,"01-title");
            EnterCampaignStage(CampaignCatalog.Chapters[0].Stages[0].Id);revealed=999;
            yield return Capture(directory,"02-story");FinishCurrentStory();
            var firstChapter=CampaignCatalog.Chapters[0];
            if(!CampaignRules.IsCleared(progress.Data,firstChapter.Stages[0])||progress.Data.Coins!=0||!CampaignRules.IsUnlocked(progress.Data,firstChapter.Stages[1]))
                throw new Exception("Story-only stage did not complete freely and unlock stage 1-2.");
            yield return Capture(directory,"12-campaign-map");
            if(!screen.Find("Stage "+firstChapter.Stages[0].Id)||!screen.Find("Stage "+firstChapter.Stages[1].Id)||screen.Find("Stage "+firstChapter.Stages[2].Id)||screen.Find("Chapter 2"))
                throw new Exception("Campaign map displayed future stages or hid completed/current stages.");
            EnterCampaignStage(firstChapter.Stages[1].Id);AssertOriginalPortraits("main story");FinishCurrentStory();
            if(page!="battle"||battle.State.Allies.Count!=1||campaign.Phase!=CampaignPhase.Battle)throw new Exception("Mixed stage 1-2 did not enter its solo Lian battle.");
            AssertSmallBattleAvatars();
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
            AssertVoyageChart(4);
            yield return Capture(directory,"16-stage-frontier");
            ShowArchive();
            if(!screen.Find("Stage "+firstChapter.Stages[2].Id)||screen.Find("Stage "+firstChapter.Stages[3].Id))throw new Exception("Archive displayed an unfinished stage.");
            var meal=firstChapter.Stages[6];CampaignRules.Complete(progress.Data,meal);
            int coinsBeforeReplay=progress.Data.Coins;
            EnterCampaignStage(meal.Id,true);storyIndex=1;DrawStory();revealed=999;yield return Capture(directory,"15-campaign-ensemble");FinishCurrentStory();
            if(progress.Data.Coins!=coinsBeforeReplay||page!="stages")throw new Exception("Campaign replay changed rewards or did not return to the map.");
            ShowHub();AssertOriginalPortraits("main hub");yield return Capture(directory,"03-hub");
            ShowRoster(0);AssertOriginalPortraits("roster");yield return Capture(directory,"04-roster");
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
                AssertVoyageChart(1);
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
            var voyageProgress=progress;var voyageSave=save;toastText="";toastUntil=0;
            var voyageChapter=CampaignCatalog.Chapters[1];UseSmokeChapterFixture(voyageChapter,voyageChapter.Stages.Last());ShowStages();
            AssertVoyageChart(voyageChapter.Stages.Count);
            if(!screen.Find("Stage "+voyageChapter.Stages.Last(stage=>stage.IsBoss).Id).Find("Anchor eye"))throw new Exception("Final boss port lost its anchor symbol.");
            int routeCoins=progress.Data.Coins;
            var revisit=voyageChapter.Stages[0];screen.Find("Stage "+revisit.Id).GetComponent<Button>().onClick.Invoke();
            if(selectedStage!=revisit.Id||progress.Data.Coins!=routeCoins||!screen.Find("Selected stage").GetComponent<Text>().text.Contains(revisit.Title))throw new Exception("Selecting a voyage port entered it or changed its rewards.");
            yield return Capture(directory,"31-voyage-full-chart");
            campaign.Exit();progress=voyageProgress;save=voyageSave;ShowStages();
            int coinsBeforeLegacy=progress.Data.Coins;
            yield return SmokeActionProgression(directory);
            // Isolated UI fixture: choosing an expensive new card should use
            // the empty action and report insufficient ember, not refund an
            // existing action unless the player explicitly selects it.
            campaign.Exit();battle=BattleEngine.CreatePrototype(progress.Data,1448);battle.State.IsBoss=false;battle.State.Round=5;BattleRules.ApplyActionProgression(battle.State);battle.State.Ember=2;
            var budgetActor=battle.Unit("minato");budgetActor.SyncLevel=1;budgetActor.SkillHand.Clear();
            var budgetHeal=new SkillCard{Id="smoke-minato-heal",SkillId="minato_heal"};
            var budgetCare=new SkillCard{Id="smoke-minato-care",SkillId="minato_care"};
            budgetActor.SkillHand.Add(budgetHeal);budgetActor.SkillHand.Add(budgetCare);PrepareBattlePresentation();
            screen.Find("Skill card "+budgetHeal.Id).GetComponent<Button>().onClick.Invoke();Target("lian");
            if(battle.State.Links.Count!=1||battle.State.Links[0].CardId!=budgetHeal.Id||battle.State.Links[0].Slot!=0||battle.AvailableEmber!=1)
                throw new Exception("Budget fixture did not reserve the first healing card in action 1.");
            if(OfferedCardSlot(budgetActor,budgetCare)!=1||screen.Find("Skill card "+budgetCare.Id).GetComponent<Button>().interactable)
                throw new Exception("An unaffordable new card bypassed the empty second action by refunding the first action.");
            screen.Find("Skill card "+budgetCare.Id).GetComponent<Button>().onClick.Invoke();
            if(battle.State.Links.Count!=1||battle.State.Links[0].CardId!=budgetHeal.Id||budgetActor.SkillHand.Count!=2)
                throw new Exception("Selecting an unaffordable new card changed the existing reservation or consumed a hand card.");
            SelectSlot("minato",0);
            if(!screen.Find("Skill card "+budgetCare.Id).GetComponent<Button>().interactable)
                throw new Exception("Explicit action selection did not allow replacing its reservation within the ember budget.");
            screen.Find("Skill card "+budgetCare.Id).GetComponent<Button>().onClick.Invoke();Target("lian");
            if(battle.State.Links.Count!=1||battle.State.Links[0].CardId!=budgetCare.Id||battle.State.Links[0].Slot!=0||battle.PlannedCost!=2||ActionAt(budgetActor,1)!=null||budgetActor.SkillHand.Count!=2)
                throw new Exception("Explicit healing S3 replacement lost its one-action semantics or consumed cards before resolution.");
            toastText="";toastUntil=0;
            // Use an actual shuffled duplicate S1 hand so the UI is exercised with
            // two distinct copies, rather than substituting an authored test hand.
            int duplicateSeed=Enumerable.Range(0,100).First(seed=>{
                var candidate=BattleEngine.CreatePrototype(progress.Data,seed);var actor=candidate.Unit("lian");
                return actor.SkillHand.Count==2&&actor.SkillHand.All(card=>candidate.Skill(actor.Id,card.SkillId).Number==1);
            });
            campaign.Exit();battle=BattleEngine.CreatePrototype(progress.Data,duplicateSeed);battle.State.Round=2;BattleRules.ApplyActionProgression(battle.State);PrepareBattlePresentation();
            if(battle.State.Allies.Count!=4)throw new Exception("Standard battle did not field four allies.");
            AssertSmallBattleAvatars();
            var duplicateCards=battle.Unit("lian").SkillHand.ToArray();
            if(screen.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Skill card ",StringComparison.Ordinal))!=8)
                throw new Exception("Battle dock did not display every ally's two offered skill cards.");
            var dockCards=screen.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Skill card ",StringComparison.Ordinal)).Select(b=>b.GetComponent<RectTransform>()).ToArray();
            if(dockCards.Any(rect=>rect.anchoredPosition.x<20||rect.anchoredPosition.x+rect.sizeDelta.x>1540||rect.sizeDelta.x<160))
                throw new Exception("Four-party skill cards escaped their dock or became too narrow.");
            if(screen.Find("Skill selection").GetComponent<RectTransform>().anchoredPosition.x<1540)
                throw new Exception("Focused skill details still occupied the card area.");
            var otherActor=battle.State.Allies.First(actor=>actor.Id!="lian");var otherCard=otherActor.SkillHand.First();
            screen.Find("Skill card "+otherCard.Id).GetComponent<Button>().onClick.Invoke();
            if(selectedActor!=otherActor.Id||selectedCard!=otherCard.Id||selectedSlot!=0)throw new Exception("An ally's direct skill card did not focus its owner and first action.");
            screen.Find("Skill card "+duplicateCards[0].Id).GetComponent<Button>().onClick.Invoke();Target(battle.State.Intents[0].Id);
            if(battle.State.Links.Single(l=>l.ActorId=="lian").CardId!=duplicateCards[0].Id)throw new Exception("Selected card identity was not retained in its action link.");
            screen.Find("Skill card "+duplicateCards[1].Id).GetComponent<Button>().onClick.Invoke();
            if(selectedActor!="lian"||selectedSlot!=1||selectedCard!=duplicateCards[1].Id)
                throw new Exception("Selecting the next offered card replaced the first action instead of using the empty second action.");
            var directCardNode=screen.Find("Skill card "+duplicateCards[1].Id);var cardDrag=directCardNode.GetComponent<LinkDrag>();
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            var cardRect=directCardNode.GetComponent<RectTransform>();pointer.position=RectTransformUtility.WorldToScreenPoint(null,cardRect.TransformPoint(cardRect.rect.center));
            pointer.pointerCurrentRaycast=new RaycastResult{gameObject=directCardNode.gameObject};cardDrag.OnBeginDrag(pointer);
            if(!dragVisuals||!directCardNode.gameObject.activeInHierarchy||selectedCard!=duplicateCards[1].Id||selectedSlot!=1)
                throw new Exception("Direct skill drag destroyed its source or lost its owner/card/action identity.");
            var intentNode=screen.Find("Intent "+battle.State.Intents[1].Id);var intentRect=intentNode.GetComponent<RectTransform>();
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,intentRect.TransformPoint(intentRect.rect.center));
            pointer.pointerCurrentRaycast=new RaycastResult{gameObject=intentNode.gameObject};cardDrag.OnDrag(pointer);cardDrag.OnEndDrag(pointer);
            if(battle.State.Links.Where(l=>l.ActorId=="lian").Select(l=>l.CardId).Distinct().Count()!=2)throw new Exception("Two actions reused the same offered card.");
            screen.Find("Skill card "+duplicateCards[0].Id).GetComponent<Button>().onClick.Invoke();
            if(selectedSlot!=0||selectedCard!=duplicateCards[0].Id||!screen.Find("Skill card "+duplicateCards[0].Id).GetComponent<Button>().interactable)
                throw new Exception("A reserved skill card did not return to its own action for editing.");
            SelectSlot("lian",1);if(selectedCard!=duplicateCards[1].Id)throw new Exception("Selecting the second existing action did not restore its card.");
            yield return Capture(directory,"17-skill-bag");
            battle.RecommendLinks();DrawBattle();yield return Capture(directory,"06-battle");
            smokePhase="round 2 battlefield effects (60 second deadline)";
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
                foreach(var active in overlay?overlay.GetComponentsInChildren<CombatPhaseMarker>():new CombatPhaseMarker[0]){
                    if(active.Attacker&&active.Attacker.texture&&active.Attacker.texture.name.EndsWith("SDActions",StringComparison.Ordinal)){
                        if(!active.SDActive||!active.FrameLayoutActive||active.BodyHeight>1080*.21f)
                            throw new Exception("SD attack grew beyond battle scale or lost its frame metadata.");
                    }
                    foreach(var performer in new[]{active.Attacker,active.Defender}){
                        if(!performer||!performer.texture)continue;
                        var textureName=performer.texture.name;
                        if(BattleAvatar.AllyArtIds.Contains(textureName)||BattleAvatar.AllyArtIds.Any(id=>textureName==id+"Actions"))
                            throw new Exception("A battle performer switched back to a full-size ally illustration.");
                    }
                }
                if(!capturedWindup&&marker&&marker.Phase=="windup"){
                    AssertDockRetracted();
                    if(marker.FocusScale<=1||marker.FocusScale>1.17f)throw new Exception("Melee pair was not brought into controlled foreground focus.");
                    if(marker.ContactSoundCount!=0)throw new Exception("A contact sound played before weapon contact.");
                    if(!marker.AtlasActive||!marker.Attacker||marker.Attacker.uvRect.width>=.5f)throw new Exception("Melee is missing its attack pose atlas.");
                    if(damage.GetComponentsInChildren<ThreadGraphic>().Length!=0)throw new Exception("Melee still contains a travelling light beam.");
                    testedMelee=marker;windupUv=marker.Attacker.uvRect;windupHealth=visibleHealth.Values.Sum();
                    yield return new WaitForEndOfFrame();
                    WriteSmokeScreenshot(directory,"06a-windup");capturedWindup=true;
                    WriteSmokeScreenshot(directory,"32-focus-dock-retracted");
                }
                var clashFx=overlay?(overlay.Find("FX Clash")??overlay.Find("FX Tie")):null;
                var clashFeedback=clashFx?clashFx.Find("Battle feedback"):null;
                if(clashFeedback&&clashFeedback.GetComponent<Text>().color.a>.35f&&clashFx.Find("Weapon contact"))sawClash=true;
                if(initialHealth.Any(pair=>visibleHealth.TryGetValue(pair.Key,out var hp)&&hp<pair.Value))sawHealthChange=true;
                if(!capturedImpact&&marker&&marker==testedMelee&&number&&number.color.a>.35f&&contact){
                    if(marker.ContactSoundCount!=1)throw new Exception("Weapon contact must play exactly one contact sound.");
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
                    if(marker.ContactSoundCount!=1)throw new Exception("Contact audio replayed during recoil.");
                    yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,"06c-recoil");capturedRecoil=true;
                }
                yield return null;
            }
            if(!capturedWindup||!capturedImpact||!capturedRecoil||!sawHealthChange||(requiresClash&&!sawClash))throw new Exception("Smoke missed atlas windup/contact/recoil, weapon clash or sequential health change.");
            if(!capturedFlameSuspense||!completedFlameReveal)throw new Exception("Smoke missed the unrevealed flame phase or its sequential completion before damage.");
            if(page!="battle"||resolving||overlay||battle.State.Round!=3||battle.State.Outcome!=BattleOutcome.Ongoing)
                throw new Exception("Visual round did not return cleanly to round 3 planning.");
            AssertSmallBattleAvatars();
            if(battleDockRetracted||Mathf.Abs(screen.Find("Action dock").GetComponent<RectTransform>().anchoredPosition.y+758)>.01f)
                throw new Exception("Round-3 planning did not restore the card tray.");
            yield return Capture(directory,"33-focus-next-planning");
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
            yield return SmokeMultipleEnemies(directory);
            yield return SmokeFocusCancellation(directory);
            string[] screenshots={"01-title","02-story","03-hub","04-roster","05-stages","06-battle","06a-windup","06b-impact","06c-recoil","07-result",
                "08-main-archive","09-chapter1-lian-map","09-chapter2-sera-map","09-chapter3-noa-map","09-chapter4-heidel-map","09-chapter5-minato-map",
                "11-lian-human","11-sera-human","11-noa-human","12-campaign-map","13-campaign-solo-battle","14-campaign-after-story","15-campaign-ensemble",
                "16-stage-frontier","17-skill-bag","18-flame-suspense","19-roster-focus-lock","20-three-enemies-intercept","21-third-enemy-target","22-enemy-2-impact","23-enemy-3-impact","24-single-boss",
                "25-normal-round1","26-normal-round2","27-normal-round3","28-normal-round5","29-boss-round3","30-boss-round5","31-voyage-full-chart","32-focus-dock-retracted","33-focus-next-planning","34-focus-reduced-motion"};
            float writesDeadline=Time.realtimeSinceStartup+10;
            while(screenshots.Any(name=>!FreshSmokeScreenshot(Path.Combine(directory,name+".png")))){
                if(Time.realtimeSinceStartup>writesDeadline)throw new Exception("Smoke did not write all forty-two fresh screenshots.");yield return null;
            }
            if(!string.IsNullOrEmpty(smokeAsyncError))throw new Exception("Asynchronous smoke failure: "+smokeAsyncError);
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: 42 fresh screenshots; campaign map, free story-only completion, mixed solo battle and after-story, ensemble replay; title, story, hub, roster, stages, battle, windup, impact, recoil, victory; five character-focused main chapters and three integrated human friend encounters.\nEach focus chapter map showed its main character; Rowan, Ellen and Eve spoke in normal pre-battle stages whose completion remained gated by victory. Roster story buttons opened unlocked main chapter maps and guarded locked chapters; archive showed completed main stages only. Campaign 1-1 unlocked 1-2 without rewards; 1-2 completed only after victory and after-story; replay changed no rewards. Future stages/chapter tabs are hidden; after 1-3 the map shows 1-1 through 1-4. Every ally's two offered cards appeared together; a card focused its owner; the next card automatically used the empty second action, dragged directly to an intent without destroying its source and retained its unique CardId. Reserved cards returned to their own action for editing. Flame suspense began with zero flames revealed, progressed sequentially to all flames and left displayed health unchanged until damage.\nActual Resolve coroutine completed round 2 within 60 seconds; atlas pose changed from windup at weapon contact, no melee light beams, sequential HP changed at contact"+(sawClash?", weapon clash captured":"")+"; round 3 returned with synchronized HP/gauges.\nReward granted once and persisted to isolated smoke-progress.xml.\n");
            File.AppendAllText(Path.Combine(directory,"smoke-result.txt"),"Four-person standard party, eight visible skill cards at least 160 canvas pixels wide; selection details in the right rail. All five battle SD atlases retained their six poses and planted-foot geometry. Allies retained a 220/1080 base body height; pair focus applied a shared 1.10/1.16 foreground transform; original hub, dialogue, roster and dock face portraits remained separate.\n");
            File.AppendAllText(Path.Combine(directory,"smoke-result.txt"),"Multiple-enemy acceptance: 42 fresh screenshots total. Campaign 2-4 fielded three uniquely selectable enemies and four SD allies; every intent displayed its owning enemy number and original named recipient. Sera's real offered card intercepted the second enemy's attack aimed at Lian; only that intent's route changed to Sera, with Lian retained as original target. Direct card drag selected the third enemy's body. Actual core damage events from enemies 2 and 3 animated their own art and applied damage only at contact to the exact target; losing the intercepted clash hit Sera rather than Lian. A main chapter boss still rendered as one larger enemy.\n");
            File.AppendAllText(Path.Combine(directory,"smoke-result.txt"),"Progressive action acceptance: 42 fresh screenshots total. Both battle types started with one usable action per ally while retaining two offered choices. Normal battles progressed [1,1,1,1] → [2,1,1,1] → [2,2,1,1] → [2,2,2,1] → [2,2,2,2]; bosses remained [2,2,1,1] from round 3 through round 5. Locked second chips showed their unlock round or boss limit and rejected clicks, direct calls and drags without reservations or hand consumption. Selecting another candidate replaced the existing first action. Guard actions used the same limit; the dock count matched usable actions and consumed slots. Restart selection clamped to action 1. Synchronized S3 used one action on round 1 for every standard party member, including boss positions 3 and 4; its card identity and the two-choice hand were preserved. The older two-action drag and budget fixtures ran at an explicitly later round.\n");
            File.AppendAllText(Path.Combine(directory,"smoke-result.txt"),"Voyage/focus acceptance: 42 fresh screenshots total. Visible completed/frontier stages were circular ports connected by exactly visibleCount-1 sea wakes; boss ports used their anchor symbols. Selecting a port changed its landing details without entering it or granting rewards. Full-size original portrait separation, archive replay and stage gates remained intact. The planning tray moved down by 282 canvas pixels before flame judgements, stayed below the main combat area through melee and restored at the next planning round. Melee performers, weapon contact and sparks shared the controlled foreground transform while health changed once at contact. Navigation during tray retraction invalidated the old resolution without leaving its card tray or overlay behind. Reduced-motion resolution retracted the tray immediately, kept scale/offset at identity, completed under auto mode, and returned with two freshly dealt CardIds per living ally and working planning controls.\n");
            Debug.Log("REQUIEM_LANTERN_SMOKE_PASS");Application.Quit(0);
        }
        void AssertVoyageChart(int visible)
        {
            if(!screen.Find("Voyage chart backing")||!screen.Find("Voyage ship")||!screen.Find("Voyage route")
                ||screen.Find("The eternal night").GetComponent<RawImage>().texture!=Texture("VoyageSea"))throw new Exception("Voyage chart lost its sea, ship or route layer.");
            if(screen.Find("Voyage route").GetComponentsInChildren<ThreadGraphic>().Length!=Mathf.Max(0,visible-1))throw new Exception("Sea wakes do not connect exactly the visible ports.");
            var ports=screen.GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Stage ",StringComparison.Ordinal)).ToArray();
            if(ports.Length!=visible||ports.Any(port=>!port.transform.Find("Port seal")||port.GetComponent<RectTransform>().sizeDelta!=new Vector2(84,84)))
                throw new Exception("Voyage stages remained rectangular panels or displayed hidden future ports.");
        }
        void AssertDockRetracted()
        {
            var dock=screen.Find("Action dock");
            if(!battleDockRetracted||!dock||Mathf.Abs(dock.GetComponent<RectTransform>().anchoredPosition.y+1040)>.1f)
                throw new Exception("Planning tray did not retract below the combat viewport.");
            foreach(var button in screen.GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Skill card ",StringComparison.Ordinal)))
                if(-button.GetComponent<RectTransform>().anchoredPosition.y<1050)throw new Exception("An offered skill card remained over the focused battle.");
        }
        IEnumerator SmokeFocusCancellation(string directory)
        {
            smokePhase="tray cancellation and reduced-motion focus";smokeDeadline=Time.realtimeSinceStartup+60;
            campaign.Exit();battle=BattleEngine.CreatePrototype(progress.Data,17);PrepareBattlePresentation();battle.RecommendLinks();
            var cancelled=battle;StartCoroutine(Resolve());ShowHub();yield return new WaitForSecondsRealtime(.30f);
            if(page!="hub"||resolving||overlay||cancelled.State.Round!=1)throw new Exception("Navigation during tray retraction leaked or resolved the abandoned round.");
            battle=BattleEngine.CreatePrototype(progress.Data,18);foreach(var unit in battle.State.Allies.Concat(battle.State.Enemies))unit.Hp=unit.MaxHp=5000;
            PrepareBattlePresentation();
            if(battleDockRetracted||screen.Find("Action dock").GetComponent<RectTransform>().anchoredPosition.y!=-758)throw new Exception("A fresh battle inherited the abandoned tray offset.");
            var oldCards=battle.State.Allies.ToDictionary(actor=>actor.Id,actor=>actor.SkillHand.Select(card=>card.Id).ToArray());
            reducedMotion=true;autoMode=true;battle.RecommendLinks();DrawBattle();StartCoroutine(Resolve());bool captured=false;
            while(resolving||page=="resolve"){
                if(Time.realtimeSinceStartup>smokeDeadline)throw new TimeoutException("Reduced-motion focus fixture did not finish.");
                if(battleDockRetracted)AssertDockRetracted();
                foreach(var marker in overlay?overlay.GetComponentsInChildren<CombatPhaseMarker>():new CombatPhaseMarker[0]){
                    if(marker.FocusScale!=1||marker.FocusOffset!=Vector2.zero||marker.transform.localScale!=Vector3.one)
                        throw new Exception("Reduced-motion melee still moved or enlarged the camera.");
                    if(!captured){yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,"34-focus-reduced-motion");captured=true;}
                }
                // Stop the next auto continuation while this round is still presenting.
                if(captured)autoMode=false;
                yield return null;
            }
            if(!captured||page!="battle"||battle.State.Round!=2||battleDockRetracted||overlay)throw new Exception("Reduced-motion resolve did not restore planning.");
            foreach(var actor in battle.State.Allies.Where(actor=>actor.Alive)){
                if(actor.SkillHand.Count!=2||actor.SkillHand.Any(card=>oldCards[actor.Id].Contains(card.Id))
                    ||actor.SkillHand.Any(card=>!screen.Find("Skill card "+card.Id)))throw new Exception("A new planning round retained stale offered card identities.");
            }
            reducedMotion=false;autoMode=false;ShowHub();
        }
        IEnumerator SmokeActionProgression(string directory)
        {
            smokePhase="progressive action slots";smokeDeadline=Time.realtimeSinceStartup+60;
            foreach(bool boss in new[]{false,true}){
                campaign.Exit();battle=BattleEngine.CreatePrototype(progress.Data,17);battle.State.IsBoss=boss;BattleRules.ApplyActionProgression(battle.State);
                foreach(var actor in battle.State.Allies)actor.Hp=actor.MaxHp=5000;
                selectedActor="lian";selectedSlot=1;selectionRound=battle.State.Round;explicitSlotFocus=true;PrepareBattlePresentation();
                if(selectedSlot!=0)throw new Exception("A new round-1 battle retained the previous second-action selection.");
                for(int round=1;round<=5;round++){
                    var expected=battle.State.Allies.Select((actor,index)=>round>=index+2&&(!boss||index<2)?2:1).ToArray();
                    if(battle.State.Round!=round||!battle.State.Allies.Select(actor=>actor.SlotCount).SequenceEqual(expected))
                        throw new Exception("Action progression did not match its round and battle type.");
                    DrawBattle();
                    if(screen.GetComponentsInChildren<Button>().Count(button=>button.name.StartsWith("Skill card ",StringComparison.Ordinal))!=8||battle.State.Allies.Any(actor=>actor.SkillHand.Count!=2))
                        throw new Exception("Usable action limits changed the two offered card choices.");
                    if(!screen.Find("Dock plan count").GetComponent<Text>().text.EndsWith("/"+expected.Sum(),StringComparison.Ordinal))
                        throw new Exception("Dock denominator did not match the actual usable actions.");
                    foreach(var actor in battle.State.Allies){
                        var second=screen.Find(actor.Id+" slot 1").GetComponent<Button>();
                        if(second.interactable!=(actor.SlotCount==2))throw new Exception("Second action chip did not follow its lock state.");
                        if(actor.SlotCount==2)continue;
                        string lockLabel=second.GetComponent<OrnateButton>().Caption.text;
                        int unlockRound=BattleRules.ActionSlotUnlockRound(battle.State,actor);
                        if(!lockLabel.Contains(unlockRound==0?"보스전 제한":"R"+unlockRound+" 개방"))throw new Exception("Locked second action did not explain when it unlocks.");
                        int handCount=actor.SkillHand.Count,linksBefore=battle.State.Links.Count;string actorBefore=selectedActor;int slotBefore=selectedSlot;
                        second.onClick.Invoke();BeginLinkDrag(new PointerEventData(EventSystem.current),actor.Id,1,null);
                        var card=actor.SkillHand.First();var skill=battle.Skill(actor.Id,card.SkillId);
                        bool attacking=skill.Kind==SkillKind.Attack;
                        if(battle.TryLink(actor.Id,1,card.SkillId,attacking?LinkKind.Strike:LinkKind.Support,attacking?battle.State.Enemies[0].Id:actor.Id,out _,card.Id)||dragVisuals
                            ||selectedActor!=actorBefore||selectedSlot!=slotBefore||battle.State.Links.Count!=linksBefore||actor.SkillHand.Count!=handCount)
                            throw new Exception("Locked second action accepted a click, drag or reservation.");
                    }
                    if(round==1){
                        var actor=battle.Unit("lian");var cards=actor.SkillHand.ToArray();
                        SelectOfferedCard(actor.Id,cards[0].Id);Target(battle.State.Enemies[0].Id);
                        SelectOfferedCard(actor.Id,cards[1].Id);Target(battle.State.Enemies[0].Id);
                        if(battle.State.Links.Count!=1||battle.State.Links[0].Slot!=0||battle.State.Links[0].CardId!=cards[1].Id||actor.SkillHand.Count!=2)
                            throw new Exception("The second offered choice did not replace its owner's only action.");
                        battle.ClearLinks();
                        foreach(var owner in battle.State.Allies){
                            owner.SyncLevel=1;var original=owner.SkillHand[0];var third=owner.Skills.First(skill=>skill.Number==3);
                            string cardId="smoke-one-action-s3-"+owner.Id;
                            owner.SkillHand[0]=new SkillCard{Id=cardId,SkillId=third.Id};DrawBattle();
                            if(third.Slots!=1||!screen.Find("Skill card "+cardId).GetComponent<Button>().interactable)
                                throw new Exception("Synchronized S3 was not available as a single action on round 1.");
                            SelectOfferedCard(owner.Id,cardId);Target(third.Kind==SkillKind.Heal?"lian":battle.State.Enemies[0].Id);
                            if(battle.State.Links.Count!=1||battle.State.Links[0].ActorId!=owner.Id||battle.State.Links[0].Slot!=0||battle.State.Links[0].CardId!=cardId||owner.SlotCount!=1)
                                throw new Exception("Round-1 single-action S3 lost its owner, cost, or card identity.");
                            battle.ClearLinks();owner.SkillHand[0]=original;
                        }
                        DrawBattle();
                    }
                    if(!boss&&round==1)yield return Capture(directory,"25-normal-round1");
                    if(!boss&&round==2)yield return Capture(directory,"26-normal-round2");
                    if(!boss&&round==3)yield return Capture(directory,"27-normal-round3");
                    if(!boss&&round==5)yield return Capture(directory,"28-normal-round5");
                    if(boss&&round==3)yield return Capture(directory,"29-boss-round3");
                    if(boss&&round==5)yield return Capture(directory,"30-boss-round5");
                    if(round==5)break;
                    if(!boss&&round==2){
                        var fallen=battle.Unit("lian");selectedActor=fallen.Id;selectedSlot=1;fallen.Hp=0;DrawBattle();
                        if(selectedActor==fallen.Id||selectedSlot!=0||battle.Unit(selectedActor).SlotCount!=1)
                            throw new Exception("A fallen first ally leaked its unlocked second-action selection to the next ally.");
                        fallen.Hp=fallen.MaxHp;DrawBattle();
                    }
                    foreach(var actor in battle.State.Allies){SelectDefense(actor.Id);Target(actor.Id);}
                    battle.ResolveRound(IgniteGrade.Good);battle.BeginNextRound();
                }
                battle.Restart();selectedSlot=1;DrawBattle();
                if(battle.State.Round!=1||selectedSlot!=0||battle.State.Allies.Any(actor=>actor.SlotCount!=1))
                    throw new Exception("Restart did not reset progressive action limits and selection.");
            }
            toastText="";toastUntil=0;
        }
        IEnumerator SmokeMultipleEnemies(string directory)
        {
            smokePhase="multiple enemies and teammate interception";smokeDeadline=Time.realtimeSinceStartup+60;
            campaign.Exit();var stageData=CampaignCatalog.Chapters[1].Stages[3];
            battle=CampaignBattleFactory.Create(stageData,new ProgressData(),41);PrepareBattlePresentation();
            if(battle.State.Enemies.Count!=3||battle.State.Allies.Count!=4)throw new Exception("Campaign 2-4 must field three enemies against the four-person party.");
            AssertSmallBattleAvatars();
            foreach(var enemy in battle.State.Enemies){
                if(!fighters.ContainsKey(enemy.Id)||!screen.Find("Enemy life "+enemy.Id)||!screen.Find("Enemy body target "+enemy.Id))
                    throw new Exception("An enemy is missing its own art, HP or body selection: "+enemy.Id);
                var marker=fighters[enemy.Id].GetComponent<BattleAvatarMarker>();
                if(!marker||marker.BodyHeight!=225||!anchors.ContainsKey(enemy.Id))throw new Exception("Multiple enemy field geometry was not compact: "+enemy.Id);
                foreach(var intent in battle.State.Intents.Where(intent=>intent.ActorId==enemy.Id)){
                    var card=screen.Find("Intent "+intent.Id);
                    if(!card||!card.Find("Intent name").GetComponent<Text>().text.Contains(EnemyNumber(enemy))
                        ||!card.Find("Intent original target").GetComponent<Text>().text.Contains(IntentOriginalTarget(intent)))
                        throw new Exception("Enemy intent did not identify its owner and original recipient: "+intent.Id);
                }
            }
            var second=battle.State.Enemies[1];var third=battle.State.Enemies[2];
            var intercepted=battle.State.Intents.First(intent=>intent.ActorId==second.Id&&intent.Kind==IntentKind.Normal);
            var original=battle.State.Allies[0];var substitute=battle.State.Allies[1];intercepted.TargetId=original.Id;
            var otherTargets=battle.State.Intents.Where(intent=>intent!=intercepted).ToDictionary(intent=>intent.Id,intent=>intent.TargetId);
            DrawBattle();var cardToUse=substitute.SkillHand.First(card=>battle.Skill(substitute.Id,card.SkillId).Kind==SkillKind.Attack);
            screen.Find("Skill card "+cardToUse.Id).GetComponent<Button>().onClick.Invoke();Target(intercepted.Id);
            var linked=battle.State.Links.Single();var intentCard=screen.Find("Intent "+intercepted.Id);
            if(linked.ActorId!=substitute.Id||linked.Kind!=LinkKind.Clash||linked.TargetId!=intercepted.Id||intercepted.TargetId!=original.Id
                ||!intentCard.Find("Intent original target").GetComponent<Text>().text.Contains(original.Name)
                ||!intentCard.Find("Intent target").GetComponent<Text>().text.Contains("가로막기 · "+substitute.Name))
                throw new Exception("A teammate's selected card did not create the visible interception while preserving the original target.");
            var route=screen.Find("Thread layer").Find("Intent thread "+intercepted.Id).GetComponent<ThreadGraphic>();
            if(route.To!=ToMesh(anchors[substitute.Id])||!screen.Find("Thread layer").Find("Original intent "+intercepted.Id)
                ||otherTargets.Any(pair=>battle.Intent(pair.Key).TargetId!=pair.Value))
                throw new Exception("Interception redirected unrelated intents or failed to show its new recipient.");
            yield return Capture(directory,"20-three-enemies-intercept");
            var striker=battle.State.Allies[3];var strikeCard=striker.SkillHand.First(card=>battle.Skill(striker.Id,card.SkillId).Kind==SkillKind.Attack);
            var source=screen.Find("Skill card "+strikeCard.Id);var drag=source.GetComponent<LinkDrag>();
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            pointer.pointerCurrentRaycast=new RaycastResult{gameObject=source.gameObject};drag.OnBeginDrag(pointer);
            var target=screen.Find("Enemy body target "+third.Id);var targetRect=target.GetComponent<RectTransform>();
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,targetRect.TransformPoint(targetRect.rect.center));
            pointer.pointerCurrentRaycast=new RaycastResult{gameObject=target.gameObject};drag.OnDrag(pointer);drag.OnEndDrag(pointer);
            if(!battle.State.Links.Any(link=>link.ActorId==striker.Id&&link.TargetId==third.Id&&link.Kind==LinkKind.Strike&&link.CardId==strikeCard.Id))
                throw new Exception("Direct card drag did not select the third enemy's own body.");
            yield return Capture(directory,"21-third-enemy-target");
            battle.RemoveLink(striker.Id,0);DrawBattle();
            // Force a losing contest only in this isolated acceptance fixture so the
            // original recipient's protection is checked independently of random rolls.
            intercepted.BasePower=90;intercepted.FlamePower=1;
            var events=battle.ResolveRound(IgniteGrade.Good);
            var secondHit=events.FirstOrDefault(e=>e.Type=="Damage"&&e.ActorId==second.Id);
            var thirdHit=events.FirstOrDefault(e=>e.Type=="Damage"&&e.ActorId==third.Id);
            if(secondHit==null||thirdHit==null||secondHit.TargetId!=substitute.Id||events.Any(e=>e.Type=="Damage"&&e.ActorId==second.Id&&e.TargetId==original.Id))
                throw new Exception("Losing the intercepted clash did not hit its substitute, or another enemy failed to act independently.");
            overlay=MakeRect(stage,"Multiple enemy action fixture",0,0,1920,1080);page="resolve";
            var status=CreateActionStatus(overlay);
            var subtitle=status.GetComponent<RectTransform>();
            if(subtitle.anchoredPosition.x+subtitle.sizeDelta.x>700)throw new Exception("Action subtitles overlap the enemy identity and HP columns.");
            yield return SmokeEnemyImpact(secondHit,overlay,status,directory,"22-enemy-2-impact");
            yield return SmokeEnemyImpact(thirdHit,overlay,status,directory,"23-enemy-3-impact");
            if(overlay)Destroy(overlay.gameObject);overlay=null;
            var boss=CampaignCatalog.Chapters[0].Stages.Last(stage=>stage.IsBoss);
            battle=CampaignBattleFactory.Create(boss,new ProgressData(),42);PrepareBattlePresentation();
            if(battle.State.Enemies.Count!=1||fighters[battle.State.Enemies[0].Id].GetComponent<BattleAvatarMarker>().BodyHeight!=310)
                throw new Exception("Single-boss field scale regressed after rendering multiple enemies.");
            yield return Capture(directory,"24-single-boss");
        }
        IEnumerator SmokeEnemyImpact(BattleEvent e,RectTransform layer,Text status,string directory,string screenshot)
        {
            bool complete=false,captured=false;int targetBefore=visibleHealth[e.TargetId];
            var untouched=battle.State.Enemies.Where(enemy=>enemy.Id!=e.ActorId).ToDictionary(enemy=>enemy.Id,enemy=>visibleHealth[enemy.Id]);
            StartCoroutine(SmokeEventPresentation(e,layer,status,()=>complete=true));
            while(!complete){
                var marker=layer.GetComponentInChildren<CombatPhaseMarker>();
                if(!captured&&marker&&marker.Phase=="contact"){
                    if(marker.ActorId!=e.ActorId||marker.TargetId!=e.TargetId||marker.Attacker.texture!=fighters[e.ActorId].texture
                        ||marker.ContactSoundCount!=1||visibleHealth[e.TargetId]!=targetBefore-e.Amount
                        ||untouched.Any(pair=>visibleHealth[pair.Key]!=pair.Value))throw new Exception("An enemy's melee performer or contact damage used the wrong unit.");
                    yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,screenshot);captured=true;
                }
                yield return null;
            }
            if(!captured)throw new Exception("Smoke did not capture the enemy's actual contact: "+e.ActorId);
        }
        IEnumerator SmokeEventPresentation(BattleEvent e,RectTransform layer,Text status,Action completed)
        {yield return PresentBattleEvent(e,layer,status,screenVersion);completed();}
        void AssertOriginalPortraits(string context)
        {
            var images=screen.GetComponentsInChildren<RawImage>().Where(image=>image.name.EndsWith(" portrait",StringComparison.Ordinal)
                &&BattleAvatar.AllyArtIds.Any(id=>image.name==id+" portrait")).ToArray();
            if(images.Length==0)throw new Exception("No original ally portrait was displayed in "+context+".");
            foreach(var image in images){
                string id=image.name.Substring(0,image.name.Length-" portrait".Length);
                if(image.texture!=Resources.Load<Texture2D>("Art/"+id)||image.GetComponent<BattleAvatarMarker>())
                    throw new Exception("Battle SD art replaced an original portrait in "+context+" / "+id);
            }
        }
        void AssertSmallBattleAvatars()
        {
            var markers=screen.GetComponentsInChildren<BattleAvatarMarker>().Where(marker=>marker.IsSD).ToArray();
            if(markers.Length!=battle.State.Allies.Count)throw new Exception("Every fielded ally must use a battle SD avatar.");
            foreach(var unit in battle.State.Allies){
                var image=fighters[unit.Id];var marker=image.GetComponent<BattleAvatarMarker>();
                if(!marker||!marker.IsSD||marker.Layout==null||marker.BodyHeight!=BattleAvatar.AllyBodyHeight
                    ||marker.BodyHeight>1080*.21f||!image.texture.name.EndsWith("SDActions",StringComparison.Ordinal))
                    throw new Exception("Incorrect small battle-only avatar for "+unit.Id);
                if(Mathf.Abs(image.rectTransform.anchoredPosition.y+BattleAvatar.GroundY)>.1f)
                    throw new Exception("SD avatar did not stand on its planted battle foot: "+unit.Id);
                var dock=screen.Find(unit.Id+" dock portrait").GetComponent<RawImage>();
                if(dock.texture!=Resources.Load<Texture2D>("Art/"+ArtId(unit.Portrait)))
                    throw new Exception("Battle dock did not retain the original face portrait: "+unit.Id);
            }
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
