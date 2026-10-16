using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        readonly List<string> flavorCaptures=new List<string>();
        void FlavorFixture(string id,int number,bool awakened=false)
        {
            var data=new ProgressData{PrologueCompleted=true};progress=new ProgressService(data);
            data.OwnedCharacters=MaterialCatalog.Definitions.Select(value=>value.CharacterId).ToList();
            data.ActiveParty=new[]{id}.Concat(RecruitmentCatalog.StarterParty.Where(value=>value!=id)).Take(4).ToList();
            data.Characters=data.OwnedCharacters.Select(value=>new CharacterProgress{Id=value,SyncLevel=1}).ToList();
            campaign.Exit();ExitWeeklyDungeon();ExitMaterialDungeon();battle=CampaignBattleFactory.Create(CampaignCatalog.Get("c1s02"),data,42);
            foreach(var enemy in battle.State.Enemies)enemy.Hp=enemy.MaxHp=600;
            var owner=battle.Unit(id);owner.AwakeningStacks=awakened?3:0;battle.State.Ember=6;
            if(number>0){var skill=owner.Skills.First(value=>value.Number==number);owner.SkillHand[0]=new SkillCard{Id="flavor-fixture-"+id+"-"+number,SkillId=skill.Id};}
            PrepareBattlePresentation();
            if(number==0)SelectDefense(id,false);else SelectOfferedCard(id,owner.SkillHand[0].Id,false);
            DrawBattle();
        }
        List<BattleEvent> ResolveFlavorFixture(string id,int number,string target=null)
        {
            var owner=battle.Unit(id);var skill=number==0?owner.Skills.First(BattleRules.IsDefensive):owner.Skills.First(value=>value.Number==number);
            target=target??(skill.Kind==SkillKind.Heal?battle.State.Allies.First(value=>value.Id!=id).Id:number==0?id:battle.State.Enemies[0].Id);
            if(skill.Kind==SkillKind.Heal){var recipient=battle.Unit(target);recipient.Hp=Math.Max(1,recipient.MaxHp-35);DrawBattle();}
            Target(target);if(!battle.State.Links.Any(value=>value.ActorId==id))throw new Exception("Flavor fixture could not reserve "+id+"/"+number);
            var events=battle.ResolveRound(IgniteGrade.Good).ToList();
            var starts=events.Where(value=>value.Type=="SkillStart"&&value.ActorId==id).ToList();
            if(starts.Count!=1||string.IsNullOrEmpty(starts[0].Quote)||string.IsNullOrEmpty(starts[0].AnimationKey))throw new Exception("An actual executed skill lacked its one quote/key: "+id+"/"+number);
            overlay=MakeRect(stage,"Flavor acceptance",0,0,1920,1080);page="resolve";return events;
        }
        IEnumerator FlavorPlay(BattleEvent e,string directory,string capture=null)
        {
            if(!battleDockRetracted){
                // Match the production Resolve entry instead of rendering
                // isolated action events over a still-interactive planning HUD.
                resolving=true;CancelDragVisuals();HideResolutionPlanning();
                foreach(var control in screen.GetComponentsInChildren<Button>())control.interactable=false;
                var phase=screen.Find("Phase");if(phase)phase.GetComponent<Text>().text=$"ROUND {battle.State.Round:00}   /   ③ 실행";
                yield return RetractBattleDock(screenVersion);
            }
            var status=overlay.GetComponentsInChildren<Text>().FirstOrDefault(value=>value.name=="Current action")??CreateActionStatus(overlay);
            int before=visibleHealth.TryGetValue(e.TargetId,out var health)?health:0;bool complete=false,observed=false,captured=false;
            float maximumAlpha=0,maximumDelta=0;int seenMarkers=0;
            StartCoroutine(SmokeEventPresentation(e,overlay,status,()=>complete=true));
            while(!complete){
                var quote=overlay.GetComponentsInChildren<SkillFlavorMarker>().FirstOrDefault(value=>value.ActorId==e.ActorId&&(value.Phase=="cut-in"||value.Phase=="quote"));
                var gesture=overlay.GetComponentsInChildren<SkillFlavorMarker>().FirstOrDefault(value=>value.ActorId==e.ActorId&&value.Phase=="support-contact");
                var contact=overlay.GetComponentsInChildren<CombatPhaseMarker>().FirstOrDefault(value=>value.ActorId==e.ActorId&&value.Phase=="contact");
                maximumDelta=Mathf.Max(maximumDelta,Time.unscaledDeltaTime);if(quote){seenMarkers++;maximumAlpha=Mathf.Max(maximumAlpha,quote.GetComponent<CanvasGroup>().alpha);}
                bool atCapture=false;
                if(e.Type=="SkillStart"&&quote&&quote.GetComponent<CanvasGroup>().alpha>.75f){
                    observed=true;atCapture=true;
                    if(quote.QuoteCount!=1||quote.SkillNumber!=e.SkillNumber||quote.AnimationKey!=e.AnimationKey||quote.Awakened!=e.Awakened)throw new Exception("Cut-in/quote metadata differs from the actual executed skill.");
                    if(visibleHealth.TryGetValue(e.TargetId,out var quotedHp)&&quotedHp!=before)throw new Exception("A quote/cut-in applied premature damage.");
                }
                if(e.Type=="Damage"){
                    var windup=overlay.GetComponentsInChildren<CombatPhaseMarker>().FirstOrDefault(value=>value.ActorId==e.ActorId&&value.Phase!="contact"&&value.Phase!="follow-through"&&value.Phase!="recoil"&&value.Phase!="recovery");
                    if(windup&&visibleHealth[e.TargetId]!=before)throw new Exception("A visual preparation hit changed HP before the final contact.");
                    if(contact){
                        observed=true;atCapture=true;var special=contact.GetComponent<SkillFlavorMarker>();
                        if(visibleHealth[e.TargetId]!=Math.Max(0,before-e.Amount)||contact.ContactSoundCount!=1||special==null||special.HealthChanges!=1||!contact.SDActive||!contact.FrameLayoutActive)throw new Exception("Special contact did not apply exactly one real HP/audio contact with its own SD atlas.");
                        if(reducedMotion&&(contact.FocusScale!=1||contact.ActorFoot!=fighters[e.ActorId].rectTransform.anchoredPosition*new Vector2(1,-1)))throw new Exception("Reduced motion moved or zoomed the S3 performer.");
                        if(reducedMotion||e.ActorId=="heidel")foreach(string widgetName in new[]{"Enemy rift "+e.TargetId,"Sanity "+e.TargetId}){
                            var widget=screen.Find(widgetName);if(widget&&widget.gameObject.activeInHierarchy)throw new Exception("Stationary contact overlaid the fixed enemy status with its temporary identity.");
                        }
                        if(e.ActorId=="heidel"&&!reducedMotion){
                            var rendered=contact.Attacker.rectTransform.TransformPoint(Vector3.zero);var local=stage.InverseTransformPoint(rendered);
                            if(local.x<-900||local.x>900)throw new Exception("Ranged S3 camera pushed Heidel outside the visible stage.");
                            foreach(var identity in new[]{contact.ActorIdentity,contact.TargetIdentity}){
                                var corners=new Vector3[4];identity.rectTransform.GetWorldCorners(corners);
                                if(corners.Select(value=>stage.InverseTransformPoint(value).x).Any(value=>value<-940||value>940))throw new Exception("A ranged S3 identity label escaped the safe stage width.");
                            }
                        }
                    }
                }
                if((e.Type=="Heal"||e.Type=="SupportApplied")&&gesture){
                    observed=true;atCapture=true;
                    if(e.Type=="Heal"&&(gesture.HealthChanges!=1||visibleHealth[e.TargetId]!=Math.Min(battle.Unit(e.TargetId).MaxHp,before+e.Amount)))throw new Exception("Healing did not change HP once at its support contact.");
                    if(e.Type=="SupportApplied"&&visibleBarrier[e.TargetId]!=e.Barrier)throw new Exception("The support contact displayed a different barrier snapshot.");
                }
                if(e.Type=="FlameRoll"&&activeFlameRoll){var flames=activeFlameRoll.GetComponent<FlameRevealMarker>();if(flames&&flames.Revealed==flames.Total){observed=true;atCapture=true;if(activeFlameActors.Count!=2)throw new Exception("Clash flame panel lost one of its two actual owners.");}}
                if(capture!=null&&!captured&&atCapture){yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,capture);flavorCaptures.Add(capture);captured=true;}
                yield return null;
            }
            if((e.Type=="SkillStart"||e.Type=="Damage"||e.Type=="Heal")&&!observed)throw new Exception("Flavor presentation never exposed its required phase: "+e.ActorId+" "+e.Type+" markers="+seenMarkers+" maximumAlpha="+maximumAlpha+" maximumFrameDelta="+maximumDelta+" key="+e.AnimationKey+" quote="+e.Quote);
            if(capture!=null&&!captured)throw new Exception("A requested flavor capture was missed: "+capture);
        }
        IEnumerator SmokeSkillFlavorSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="skill quotes, S3 motion, awakening and support";smokeDeadline=Time.realtimeSinceStartup+160;flavorCaptures.Clear();
            save=new SaveStore(Path.Combine(directory,"flavor-progress.xml"));assisted=true;reducedMotion=false;autoMode=false;toastText="";toastUntil=0;recurringNextTick=float.MaxValue;
            var ids=new[]{"lian","sera","noa","minato","heidel","kai","mare"};int quotes=0;
            foreach(string id in ids)foreach(int number in new[]{0,1,2,3}){
                FlavorFixture(id,number);var events=ResolveFlavorFixture(id,number);quotes+=events.Count(value=>value.Type=="SkillStart"&&value.ActorId==id);
                yield return null;
            }
            if(quotes!=28)throw new Exception("Not all twenty-eight ally actions produced one real skill quote.");
            int imageIndex=1;
            foreach(string id in ids){
                FlavorFixture(id,3);var events=ResolveFlavorFixture(id,3);
                yield return null;
                foreach(var e in events.Where(value=>value.ActorId==id&&(value.Type=="SkillStart"||value.Type=="Damage"||value.Type=="Heal"||value.Type=="SupportApplied"||value.Type.StartsWith("Awakening")))){
                    string shot=e.Type=="Damage"||e.Type=="Heal"?imageIndex++.ToString("00")+"-"+id+"-s3-contact":e.Type=="SkillStart"&&id=="lian"?"00-lian-s3-cut-in":null;
                    yield return FlavorPlay(e,directory,shot);
                }
            }
            foreach(string id in new[]{"lian","sera","noa"}){
                FlavorFixture(id,3,true);var events=ResolveFlavorFixture(id,3);
                if(!events.Any(value=>value.Type=="AwakeningUsed"&&value.ActorId==id)||battle.Unit(id).AwakeningStacks!=0)throw new Exception("Awakened S3 did not consume its three stacks.");
                foreach(var e in events.Where(value=>value.ActorId==id&&(value.Type=="SkillStart"||value.Type=="Damage"||value.Type=="AwakeningUsed")))
                    yield return FlavorPlay(e,directory,e.Type=="SkillStart"?imageIndex++.ToString("00")+"-"+id+"-awakened-cut-in":null);
            }
            foreach(var mode in new[]{new{Id="lian",Number=3},new{Id="kai",Number=2},new{Id="minato",Number=3}}){
                FlavorFixture(mode.Id,mode.Number,mode.Id=="lian");string friend=battle.State.Allies.First(value=>value.Id!=mode.Id).Id;
                if(!CanConnect(friend))throw new Exception("A supported ally was not a valid native drag target.");
                var events=ResolveFlavorFixture(mode.Id,mode.Number,friend);
                if(!battle.State.Links.Any(value=>value.ActorId==mode.Id&&value.Kind==LinkKind.Support))throw new Exception("Native ally selection did not use the support link kind.");
                bool captured=false;
                foreach(var e in events.Where(value=>value.ActorId==mode.Id&&(value.Type=="SkillStart"||value.Type=="Heal"||value.Type=="SupportApplied"||value.Type=="AwakeningUsed"))){
                    string shot=!captured&&(e.Type=="Heal"||e.Type=="SupportApplied")?imageIndex++.ToString("00")+"-"+mode.Id+"-ally-support":null;if(shot!=null)captured=true;
                    yield return FlavorPlay(e,directory,shot);
                }
            }
            FlavorFixture("lian",3,true);battle.Unit("lian").SyncLevel=0;DrawBattle();ShowSkillFlavorTooltip("lian",battle.Unit("lian").Skills.First(value=>value.Number==3).Id);
            if(!screen.Find("Traits lian/Awakening text").GetComponent<Text>().text.Contains("동기화 필요")||!skillFlavorTooltip.Find("Description").GetComponent<Text>().text.Contains("동기화가 필요"))throw new Exception("A full gauge falsely advertised usable S3 before synchronization.");
            yield return Capture(directory,"14-ready-gauge-sync-lock-tooltip");flavorCaptures.Add("14-ready-gauge-sync-lock-tooltip");HideSkillFlavorTooltip();
            reducedMotion=true;FlavorFixture("sera",3);var reduced=ResolveFlavorFixture("sera",3);foreach(var e in reduced.Where(value=>value.ActorId=="sera"&&(value.Type=="SkillStart"||value.Type=="Damage")))yield return FlavorPlay(e,directory,e.Type=="Damage"?"15-reduced-motion-contact":null);
            reducedMotion=false;FlavorFixture("sera",3);var cancelled=ResolveFlavorFixture("sera",3);var start=cancelled.First(value=>value.Type=="SkillStart"&&value.ActorId=="sera");
            bool stopped=false;var status=CreateActionStatus(overlay);StartCoroutine(SmokeEventPresentation(start,overlay,status,()=>stopped=true));yield return new WaitForSecondsRealtime(.12f);ShowHub();yield return null;yield return null;
            if(!stopped||stage.GetComponentsInChildren<SkillFlavorMarker>().Length!=0||stage.anchoredPosition!=Vector2.zero||combatAudio.ActiveCount!=0)throw new Exception("Leaving during the cut-in left motion, captions or audio behind.");
            FlavorFixture("mare",3);cancelled=ResolveFlavorFixture("mare",3);var damage=cancelled.First(value=>value.Type=="Damage"&&value.ActorId=="mare");stopped=false;status=CreateActionStatus(overlay);StartCoroutine(SmokeEventPresentation(damage,overlay,status,()=>stopped=true));yield return new WaitForSecondsRealtime(.13f);ShowHub();yield return null;yield return null;
            if(!stopped||stage.GetComponentsInChildren<CombatPhaseMarker>().Length!=0||stage.anchoredPosition!=Vector2.zero||combatAudio.ActiveCount!=0)throw new Exception("Leaving during S3 motion left a foreground performer or audio behind.");
            yield return Capture(directory,"16-cancelled-clean-hub");flavorCaptures.Add("16-cancelled-clean-hub");
            FlavorFixture("lian",1);var intent=battle.State.Intents[0];intent.BasePower=1;intent.FlamePower=1;
            var clashEvents=ResolveFlavorFixture("lian",1,intent.Id);
            var normal=clashEvents.First(value=>value.Type=="SkillStart"&&value.ActorId=="lian");yield return FlavorPlay(normal,directory,"17-normal-skill-quote");
            var ignition=clashEvents.First(value=>value.Type=="Ignite"&&value.ActorId=="lian");yield return FlavorPlay(ignition,directory);
            if(activeClashActor!="lian")throw new Exception("SkillStart target normalization broke the existing Ignite intent ownership.");
            var clashFlame=clashEvents.First(value=>value.Type=="FlameRoll"&&value.ActorId=="lian"&&value.EnemyFlameStates.Length>0);yield return FlavorPlay(clashFlame,directory,"18-clash-flame-ownership");
            if(flavorCaptures.Any(shot=>!FreshSmokeScreenshot(Path.Combine(directory,shot+".png"))))throw new Exception("Flavor screenshots were missing or stale.");
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: twenty-eight real ally action SkillStart events each supplied exactly one quote/key. All seven S3 cut-ins and actor-specific SD contacts, plus three awakened variants, used real engine events. Preparation strokes/cut-ins left HP unchanged; final damage contact applied once with one impact sound, healing changed HP once, and shield/support snapshots matched their recipient. Ally targeting used Support links for Lian S3, Kai S2 and Minato S3; no extra damage was invented for visual follow-up strokes. Three awakened fixtures consumed their stacks, and a full unsynchronized gauge clearly required synchronization. Reduced motion retained original feet and unit zoom; leaving during cut-in and special movement removed all temporary captions/performers and stopped audio. Isolated fixtures expose all acquired synchronized skills and seed three stacks for variant coverage; these are animation fixtures rather than natural progression/balance proof. Player saves/settings were untouched. Captures: "+flavorCaptures.Count+".\n");
            Debug.Log("REQUIEM_LANTERN_SKILL_FLAVOR_SMOKE_PASS");Application.Quit(0);
        }
    }
}
