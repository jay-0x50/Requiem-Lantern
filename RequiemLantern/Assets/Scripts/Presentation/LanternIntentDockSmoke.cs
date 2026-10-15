using System;
using System.Collections;
using System.IO;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        PointerEventData IntentSmokePointer(Vector2 point,GameObject target)
        {
            return new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
                position=RectTransformUtility.WorldToScreenPoint(null,screen.TransformPoint(ToMesh(point))),
                pointerCurrentRaycast=new RaycastResult{gameObject=target}};
        }
        void AssertCompactIntents(int expected)
        {
            var cards=screen.GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Intent ",StringComparison.Ordinal)).ToArray();
            if(cards.Length!=expected)throw new Exception("Visible intent tiles do not match actual boss actions: "+expected);
            foreach(var enemy in battle.State.Enemies){
                var intents=battle.State.Intents.Where(intent=>intent.ActorId==enemy.Id).ToArray();
                for(int index=0;index<intents.Length;index++){
                    var intent=intents[index];var card=screen.Find("Intent "+intent.Id);var rect=card.GetComponent<RectTransform>();
                    Rect expectedRect=IntentTileRect(enemy,index,intents.Length);
                    if(rect.sizeDelta!=new Vector2(134,124)||rect.anchoredPosition!=ToMesh(expectedRect.position)||anchors[intent.Id]!=expectedRect.center)
                        throw new Exception("Tile, target anchor and actual owner geometry disagree: "+intent.Id);
                    if(!card.Find("Intent original target").GetComponent<Text>().text.Contains(IntentOriginalTarget(intent))
                        ||card.GetComponent<LinkTarget>().Id!=intent.Id||card.Find("Intent darkness count").GetComponent<Text>().text!=BattleRules.Flames(intent).ToString())
                        throw new Exception("An intent tile hid its original recipient, darkness count or link identity.");
                    if(expectedRect.xMin<20||expectedRect.xMax>1900||expectedRect.yMax>=390)throw new Exception("Compact intent escaped the viewport or collided with the boss head.");
                    if(index>0&&expectedRect.xMin<IntentTileRect(enemy,index-1,intents.Length).xMax+11)throw new Exception("Horizontal intent tiles overlap.");
                }
            }
        }
        void AssertIntentTooltipBounds()
        {
            if(!intentTooltip||intentTooltip.GetComponentsInChildren<Graphic>().Any(graphic=>graphic.raycastTarget)
                ||intentTooltip.GetComponent<CanvasGroup>().blocksRaycasts)throw new Exception("Intent details blocked pointer input.");
            var point=intentTooltip.anchoredPosition;var size=intentTooltip.sizeDelta;
            if(point.x<19||point.x+size.x>1901||-point.y<125||-point.y+size.y>1061)throw new Exception("Intent tooltip escaped the canvas.");
        }
        string IntentSmokeSelection()
        {return selectedActor+"|"+selectedSkill+"|"+selectedCard+"|"+selectedSlot+"|"+JsonUtility.ToJson(battle.State);}
        IEnumerator SmokeIntentDockSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="compact intent dock";smokeDeadline=Time.realtimeSinceStartup+120;
            if(args.Contains("--drag-only")){yield return SmokeIllusionDragOnly(directory);yield break;}
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"RUNNING: real boss actions and compact intent hover/drag smoke.\n");
            save=new SaveStore(Path.Combine(directory,"smoke-progress.xml"));progress=new ProgressService(new ProgressData());
            assisted=true;autoMode=false;reducedMotion=false;toastText="";toastUntil=0;campaign.Exit();
            battle=BattleEngine.CreatePrototype(progress.Data,17);
            foreach(var unit in battle.State.Allies.Concat(battle.State.Enemies))unit.Hp=unit.MaxHp=5000;
            PrepareBattlePresentation();
            for(int round=1;round<=3;round++){
                AssertCompactIntents(round+3);yield return Capture(directory,"0"+round+"-boss-"+(round+3)+"-actions");
                if(round==3)break;
                foreach(var actor in battle.State.Allies){SelectDefense(actor.Id);Target(actor.Id);}
                battle.ResolveRound(IgniteGrade.Good);battle.BeginNextRound();DrawBattle();
            }
            var last=battle.State.Intents.Last();var lastNode=screen.Find("Intent "+last.Id);var lastRect=lastNode.GetComponent<RectTransform>();
            var center=new Vector2(lastRect.anchoredPosition.x+67,-lastRect.anchoredPosition.y+62);
            var hover=lastNode.GetComponent<IntentHover>();string beforeHover=IntentSmokeSelection();hover.OnPointerEnter(IntentSmokePointer(center,lastNode.gameObject));
            AssertIntentTooltipBounds();
            string details=intentTooltip.Find("Tooltip details").GetComponent<Text>().text;
            if(!details.Contains("기본 위력(상성·어둠도 적용 전)")||!details.Contains("정신력 ")||!details.Contains("점등 ")||!details.Contains("기본 속도 ")
                ||!details.Contains("원래 표적  "+IntentOriginalTarget(last))||!details.Contains("부위  ")||!details.Contains("특수  ")||!details.Contains("후보 상성  "))
                throw new Exception("Hover omitted required mechanics or its own intent recipient.");
            foreach(var corner in new[]{new Vector2(20,130),new Vector2(1900,130),new Vector2(20,1050),new Vector2(1900,1050)}){
                hover.OnPointerMove(IntentSmokePointer(corner,lastNode.gameObject));AssertIntentTooltipBounds();
            }
            hover.OnPointerMove(IntentSmokePointer(center,lastNode.gameObject));
            if(beforeHover!=IntentSmokeSelection())throw new Exception("Hover changed a selected card, reservation, resource, unit or random hand state.");
            yield return Capture(directory,"04-bounded-intent-details");
            var realKind=last.Kind;string realTarget=last.TargetId;last.Kind=IntentKind.Illusion;last.TargetId="all";
            ShowIntentTooltip(last.Id,center);string illusion=intentTooltip.Find("Tooltip details").GetComponent<Text>().text;
            if(!illusion.Contains("허상: 맞불 연결 시 행동만 소모하고 피해 없이 종료")||!illusion.Contains("원래 표적  아군 전체")
                ||illusion.Contains("2연승")||illusion.Contains("배정 상성")||illusion.Contains("후보 상성"))throw new Exception("Illusion tooltip promised a real clash/attack or hid its original party target.");
            var illusionActor=battle.Unit("sera");var illusionCard=illusionActor.SkillHand[0];
            if(!battle.TryLink(illusionActor.Id,0,illusionCard.SkillId,LinkKind.Clash,last.Id,out _,illusionCard.Id))throw new Exception("Illusion assignment fixture was invalid.");
            ShowIntentTooltip(last.Id,center);string assignedIllusion=intentTooltip.Find("Tooltip details").GetComponent<Text>().text;
            if(assignedIllusion.Contains("2연승")||assignedIllusion.Contains("배정 상성")||assignedIllusion.Contains("후보 상성")
                ||!assignedIllusion.Contains("현재 배정  세라 · 허상 연결"))throw new Exception("Assigned illusion retained a misleading standard clash preview.");
            battle.ClearLinks();last.Kind=realKind;last.TargetId=realTarget;HideIntentTooltip();
            var sera=battle.Unit("sera");var card=sera.SkillHand.First();SelectOfferedCard(sera.Id,card.Id,false);
            if(intentTooltip)throw new Exception("Selecting another card retained the previous intent tooltip.");
            var intercepted=battle.State.Intents.First(intent=>intent.Kind==IntentKind.Normal);intercepted.TargetId="lian";DrawBattle();
            var otherTargets=battle.State.Intents.Where(intent=>intent!=intercepted).ToDictionary(intent=>intent.Id,intent=>intent.TargetId);
            var source=screen.Find("Skill card "+card.Id);var drag=source.GetComponent<LinkDrag>();
            var sourceRect=source.GetComponent<RectTransform>();var pointer=IntentSmokePointer(new Vector2(sourceRect.anchoredPosition.x+83,-sourceRect.anchoredPosition.y+64),source.gameObject);
            drag.OnBeginDrag(pointer);if(!dragVisuals||!source.gameObject.activeInHierarchy||intentTooltip)throw new Exception("Card drag lost its source or left hover details over the action.");
            var tile=screen.Find("Intent "+intercepted.Id);pointer=IntentSmokePointer(anchors[intercepted.Id],tile.gameObject);drag.OnDrag(pointer);drag.OnEndDrag(pointer);
            var link=battle.State.Links.Single();
            if(link.ActorId!="sera"||link.CardId!=card.Id||link.TargetId!=intercepted.Id||link.Kind!=LinkKind.Clash||intercepted.TargetId!="lian"
                ||!screen.Find("Intent "+intercepted.Id).Find("Intent target").GetComponent<Text>().text.Contains("가로막기 · 세라")
                ||otherTargets.Any(pair=>battle.Intent(pair.Key).TargetId!=pair.Value))throw new Exception("Dragging to a compact tile changed the wrong recipient or intent owner.");
            var route=screen.Find("Thread layer").GetComponentsInChildren<ThreadGraphic>().Single(line=>line.name=="Light thread");
            if(route.To!=ToMesh(anchors[intercepted.Id]))throw new Exception("Committed link did not end at the compact tile center.");
            var hoverNode=screen.Find("Intent "+intercepted.Id);hoverNode.GetComponent<IntentHover>().OnPointerEnter(IntentSmokePointer(anchors[intercepted.Id],hoverNode.gameObject));
            string assigned=intentTooltip.Find("Tooltip details").GetComponent<Text>().text;
            if(!assigned.Contains("현재 배정  세라")||!assigned.Contains("배정 상성  "+battle.Preview(link).Summary))throw new Exception("Tooltip preview belonged to another assigned actor or intent.");
            HideIntentTooltip();yield return Capture(directory,"05-dragged-teammate-interception");
            var lian=battle.Unit("lian");SelectOfferedCard(lian.Id,lian.SkillHand[0].Id);var clicked=battle.State.Intents.First(intent=>intent!=intercepted&&intent.Kind!=IntentKind.Illusion);
            screen.Find("Intent "+clicked.Id).GetComponent<Button>().onClick.Invoke();
            if(!battle.State.Links.Any(value=>value.ActorId==lian.Id&&value.TargetId==clicked.Id))throw new Exception("Clicking a compact tile did not retain normal target-link semantics.");
            yield return Capture(directory,"06-clicked-second-intent");
            var dead=battle.State.Enemies[0];int oldHp=dead.Hp;dead.Hp=0;DrawBattle();
            var disabled=screen.Find("Intent "+intercepted.Id);disabled.GetComponent<IntentHover>().OnPointerEnter(IntentSmokePointer(anchors[intercepted.Id],disabled.gameObject));
            if(disabled.GetComponent<Button>().interactable||intentTooltip)throw new Exception("A defeated enemy accepted an intent hover or input.");
            dead.Hp=oldHp;DrawBattle();
            foreach(var intent in battle.State.Intents){intent.BasePower=1;intent.FlamePower=1;}
            foreach(var actor in new[]{sera,lian})foreach(var skill in actor.Skills.Where(skill=>skill.Kind==SkillKind.Attack)){skill.BasePower=30;skill.FlamePower=4;}
            DrawBattle();var pending=screen.Find("Intent "+intercepted.Id);pending.GetComponent<IntentHover>().OnPointerEnter(IntentSmokePointer(anchors[intercepted.Id],pending.gameObject));
            StartCoroutine(Resolve());bool sawExecution=false;
            while(resolving||page=="resolve"){
                if(Time.realtimeSinceStartup>smokeDeadline)throw new TimeoutException("Compact intent resolve did not restore planning.");
                if(intentTooltip)throw new Exception("Execution retained an intent tooltip.");
                if(battleDockRetracted&&!sawExecution&&overlay&&overlay.GetComponentsInChildren<CombatPhaseMarker>().Length>0){
                    yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,"07-execution-focus");sawExecution=true;
                }
                yield return null;
            }
            if(!sawExecution)throw new Exception("Smoke missed the real compact-intent execution presentation.");
            AssertUxPlanningRestored();AssertCompactIntents(6);yield return Capture(directory,"08-next-round-intents-restored");
            battle.ClearLinks();dead.Hp=1;var finisher=battle.Unit("lian");SelectOfferedCard(finisher.Id,finisher.SkillHand[0].Id);Target(dead.Id);
            StartCoroutine(Resolve());while(resolving||page=="resolve"){
                if(Time.realtimeSinceStartup>smokeDeadline)throw new TimeoutException("Compact intent victory did not return a result.");yield return null;
            }
            if(page!="result"||battle.State.Outcome!=BattleOutcome.Victory||intentTooltip)throw new Exception("Compact intent battle did not retain victory/result semantics.");
            yield return Capture(directory,"09-victory-result");
            campaign.Exit();battle=CampaignBattleFactory.Create(CampaignCatalog.Chapters[1].Stages[3],new ProgressData(),31);PrepareBattlePresentation();
            AssertCompactIntents(battle.State.Intents.Count);yield return Capture(directory,"10-three-enemy-compact-actions");
            string[] shots={"01-boss-4-actions","02-boss-5-actions","03-boss-6-actions","04-bounded-intent-details","05-dragged-teammate-interception","06-clicked-second-intent","07-execution-focus","08-next-round-intents-restored","09-victory-result","10-three-enemy-compact-actions"};
            if(shots.Any(shot=>!FreshSmokeScreenshot(Path.Combine(directory,shot+".png"))))throw new Exception("Compact intent smoke did not write ten fresh screenshots.");
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: 10 fresh native screenshots. Actual four-person boss rounds produced 4, 5 and 6 selectable horizontal intent tiles; tile rectangles, owner IDs, darkness counts, original targets, LinkTarget IDs and thread anchors matched the live engine. Each tile stayed within the viewport above the boss's head; multi-enemy actions used the same compact UI. Hover revealed complete power/base/darkness, sanity/probability, speed/kind, original recipient, part/special effect and current/candidate matchup details without changing card selection, reservations, resources, unit or hand state. Tooltip graphics never blocked raycasts and remained bounded at all four screen edges; changing selection and starting execution removed them. Direct offered-card drag intercepted Lian's attack with Sera on the exact chosen intent, preserving original and unrelated recipients; direct tile click assigned another action. Assigned matchup details matched the same engine command. Defeated-enemy actions were disabled and rejected hover. Actual Resolve retracted the dock and restored compact intents and fixed HUD next round, then a second actual resolution reached victory and the normal result view.\n");
            Debug.Log("REQUIEM_LANTERN_INTENT_DOCK_SMOKE_PASS");Application.Quit(0);
        }
        IEnumerator SmokeIllusionDragOnly(string directory)
        {
            smokePhase="illusion drag hint";smokeDeadline=Time.realtimeSinceStartup+30;
            save=new SaveStore(Path.Combine(directory,"illusion-drag-progress.xml"));progress=new ProgressService(new ProgressData());
            assisted=true;autoMode=false;reducedMotion=false;toastText="";toastUntil=0;campaign.Exit();
            battle=BattleEngine.CreatePrototype(progress.Data,17);var illusion=battle.State.Intents[0];illusion.Kind=IntentKind.Illusion;illusion.TargetId="all";
            PrepareBattlePresentation();var actor=battle.Unit("lian");var card=actor.SkillHand[0];SelectOfferedCard(actor.Id,card.Id,false);
            string before=IntentSmokeSelection();var source=screen.Find("Skill card "+card.Id);var sourceRect=source.GetComponent<RectTransform>();
            var drag=source.GetComponent<LinkDrag>();drag.OnBeginDrag(IntentSmokePointer(new Vector2(sourceRect.anchoredPosition.x+83,-sourceRect.anchoredPosition.y+64),source.gameObject));
            var tile=screen.Find("Intent "+illusion.Id);drag.OnDrag(IntentSmokePointer(anchors[illusion.Id],tile.gameObject));
            if(!dragVisuals||!dragHint||dragHint.text!="허상 · 연결 시 행동만 소모하고 피해 없음"||dragHint.color!=Gold||dragLine.color!=Gold
                ||snappedTarget!=illusion.Id||before!=IntentSmokeSelection()||intentTooltip)
                throw new Exception("Illusion drag displayed a standard clash prediction, wrong recipient/color or changed the battle before dropping.");
            yield return new WaitForEndOfFrame();WriteSmokeScreenshot(directory,"11-illusion-drag-hint");CancelDragVisuals();
            if(dragVisuals||before!=IntentSmokeSelection())throw new Exception("Cancelling the illusion drag reserved or consumed an action.");
            File.WriteAllText(Path.Combine(directory,"illusion-drag-result.txt"),"PASS: actual offered-card drag to a live illusion intent showed the neutral gold hint '허상 · 연결 시 행동만 소모하고 피해 없음' without a standard two-win attack prediction or substitute-defender claim. Hover/drag changed no selection, reservation, resource, unit or hand state; cancelling consumed no action. The existing ten-case intent dock report was preserved.\n");
            Debug.Log("REQUIEM_LANTERN_ILLUSION_DRAG_SMOKE_PASS");Application.Quit(0);
        }
    }
}
