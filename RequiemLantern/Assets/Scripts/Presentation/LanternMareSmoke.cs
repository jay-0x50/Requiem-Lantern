using System;
using System.Collections;
using System.IO;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        IEnumerator SmokeMareSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="Mare companion";smokeDeadline=Time.realtimeSinceStartup+45;
            save=new SaveStore(Path.Combine(directory,"mare-progress.xml"));progress=new ProgressService(new ProgressData());campaign.Exit();reducedMotion=false;autoMode=false;sound=true;
            if(!progress.Data.ActiveParty.SequenceEqual(new[]{"lian","minato","heidel","mare"})||!progress.IsOwned("mare")||progress.IsOwned("kai"))
                throw new Exception("New starter did not contain the separate four-star companion.");
            ShowRoster(Array.IndexOf(CharacterIds,"Mare"));AssertOriginalPortraits("Mare roster");
            if(!screen.Find("Character growth").GetComponent<Text>().text.Contains("★★★★")||!screen.Find("Character name").GetComponent<Text>().text.Contains("마레"))
                throw new Exception("Mare's original portrait/rarity record was missing.");
            yield return Capture(directory,"01-mare-owned-four-star");
            var atlas=BattleAvatar.LoadAtlas("Mare",true,out var key,out var layout);
            if(!atlas||layout==null||layout.frames.Length!=6||key!="Combat/MareSDActions"||atlas==Texture("Mare"))throw new Exception("Mare lacks a dedicated six-pose SD atlas.");
            var fixture=MakeRect(stage,"Mare pose fixture",0,0,1920,1080);var standing=BattleAvatar.Create(fixture,"Mare",Texture("Mare"),false,new Vector2(500,700),220);
            using(var performer=new CombatPosePlayer(fixture,standing,"Mare",true,220,"Mare pose continuity")){
                for(int pose=0;pose<6;pose++){
                    performer.SetPose(pose);performer.PlaceFoot(new Vector2(500,700));
                    if(performer.Image.texture!=atlas||!performer.HasFrameLayout||!performer.HasSDAtlas||performer.Image.rectTransform.anchoredPosition!=new Vector2(500,-700))
                        throw new Exception("Mare lost the planted foot or atlas in pose "+pose);
                }
            }fixture.gameObject.SetActive(false);Destroy(fixture.gameObject);
            battle=CampaignBattleFactory.Create(CampaignCatalog.Get("c2s04"),progress.Data,0);PrepareBattlePresentation();AssertSmallBattleAvatars();
            var mare=battle.Unit("mare");SelectOfferedCard(mare.Id,mare.SkillHand[0].Id,false);Target(battle.State.Intents[0].Id);
            var linked=battle.State.Links.Single(link=>link.ActorId==mare.Id);string named=NamedClashPrediction(linked);
            if(!named.Contains("마레")||!named.Contains("적 1 예고 1")||!named.Contains("아군 ")||!named.Contains("적 "))throw new Exception("Clash prediction lost its ally/intent/power ownership labels.");
            ShowIntentTooltip(linked.TargetId,new Vector2(1540,420));yield return Capture(directory,"02-mare-party-and-named-clash");HideIntentTooltip();
            battle.ClearLinks();foreach(var other in battle.State.Allies.Where(actor=>actor.Id!="mare")){SelectDefense(other.Id,false);Target(other.Id);}
            SelectOfferedCard(mare.Id,mare.SkillHand[0].Id,false);Target(battle.State.Enemies[0].Id);
            int targetBefore=visibleHealth[battle.State.Enemies[0].Id];var events=battle.ResolveRound(IgniteGrade.Good);
            var hit=events.FirstOrDefault(value=>value.Type=="Damage"&&value.ActorId=="mare");if(hit==null)throw new Exception("Mare did not produce a real core damage event.");
            var layer=MakeRect(screen,"Mare physical contact acceptance",0,0,1920,1080);var status=Label(layer,"Mare status","마레 · 갈고리 타격",50,344,800,50,25,Gold);
            bool complete=false,captured=false;StartCoroutine(SmokeEventPresentation(hit,layer,status,()=>complete=true));
            while(!complete){
                var marker=layer.GetComponentInChildren<CombatPhaseMarker>();
                if(!captured&&marker&&marker.Phase=="contact"){
                    if(marker.ActorId!="mare"||!marker.SDActive||!marker.FrameLayoutActive||marker.ContactSoundCount!=1||marker.Attacker.texture!=atlas
                        ||visibleHealth[hit.TargetId]!=targetBefore-hit.Amount)throw new Exception("Mare's real hit lost its own art or one-contact damage/audio semantics.");
                    yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,"03-mare-real-hook-contact");captured=true;
                }yield return null;
            }
            if(!captured||!fighters["mare"].enabled)throw new Exception("Mare contact was missed or the planning SD did not restore.");
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: three fresh Mare native captures. A new save grants Lian/Minato/Heidel/Mare and keeps Kai as the story protagonist outside the new summon pool. Mare's four-star owned roster uses her own original portrait; all six SD poses retain the atlas metadata and planted-foot anchor, including nonuniform lower-row frames. A real campaign battle fields her saved party and her actual offered card creates a named clash prediction with the allied name, owning enemy/intent number and labelled ally/enemy expected powers. A real core damage event animates Mare's own SD hook attack, with one physical contact sound and visible HP applied once at contact, then restores planning art. Player saves were untouched. Core migration/ownership/pool and twenty-five-stage starter balance checks are recorded separately by RecruitmentTests.\n");
            Debug.Log("REQUIEM_LANTERN_MARE_SMOKE_PASS");Application.Quit(0);
        }
    }
}
