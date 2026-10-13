using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RequiemLantern.Core;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        string selectedActor="lian",selectedSkill="lian_glimmer",selectedCard; int selectedSlot,selectionRound;
        Dictionary<string,Vector2> anchors=new Dictionary<string,Vector2>();
        Dictionary<string,Vector2> slotAnchors=new Dictionary<string,Vector2>();
        ThreadGraphic dragLine; bool ignitionTapped; float ignitionTimestamp;
        void StartBattle(){campaign.Exit();battle=BattleEngine.CreatePrototype(progress.Data);PrepareBattlePresentation();}
        string ArtId(string id){return CharacterIds.FirstOrDefault(x=>x.Equals(id,StringComparison.OrdinalIgnoreCase))??id;}
        Color ElementColor(Element e){switch(e){case Element.Flame:return Hex("E77A5D");case Element.Tide:return Teal;case Element.Frost:return Hex("A7D4E9");case Element.Shadow:return Violet;default:return Gold;}}
        Image Gauge(Transform p,string name,float x,float y,float w,int value,int max,Color color)
        {Box(p,name+" track",x,y,w,7,new Color(.04f,.055f,.075f,.95f));return Box(p,name+" fill",x,y,w*Mathf.Clamp01((float)value/Mathf.Max(1,max)),7,color);}
        void DrawBattle()
        {
            ChooseSkillForSlot(selectedActor,selectedSlot,true);
            Clear("battle");var s=battle.State;anchors.Clear();slotAnchors.Clear();fighters.Clear();healthFills.Clear();healthLabels.Clear();gaugeWidths.Clear();visibleHealth.Clear();
            foreach(var unit in s.Allies.Concat(s.Enemies))visibleHealth[unit.Id]=unit.Hp;
            Box(screen,"Gloom",0,0,1920,1080,new Color(.025f,.01f,.065f,.06f*s.Gloom));
            Box(screen,"Battle header",0,0,1920,117,new Color(.035f,.05f,.075f,.96f));
            Button(screen,"Retreat","〈",32,32,65,57,()=>ConfirmRetreat());
            Label(screen,"Battle stage",s.StageName,121,23,690,44,30);
            Label(screen,"Phase",$"ROUND {s.Round:00}   /   ② 연결",124,74,720,27,19,Gold);
            Label(screen,"Resources",$"불씨  {s.Ember} / 6    예약  {battle.PlannedCost}       등불  {s.Lantern} / 100       어둠도  {s.Gloom}",865,24,1010,48,23,Gold,TextAnchor.MiddleRight);
            Label(screen,"Battle instruction","슬롯을 선택하고 예고선·부위·아군을 클릭하세요. 드래그로도 연결할 수 있습니다.",805,78,1066,28,18,Muted,TextAnchor.MiddleRight);
            var lineLayer=MakeRect(screen,"Thread layer",0,0,1920,1080);
            for(int i=0;i<s.Allies.Count;i++){
                var u=s.Allies[i];float x=65+i*322;
                var portrait=Character(screen,string.IsNullOrEmpty(u.Portrait)?ArtId(u.Id):u.Portrait,x,284,315,484);
                fighters[u.Id]=portrait;
                if(!u.Alive)portrait.color=new Color(.36f,.36f,.4f,.6f);
                anchors[u.Id]=new Vector2(x+157,537);
                Label(screen,u.Id+" battlefield name",u.Name,x+65,687,210,39,22,Ivory,TextAnchor.MiddleCenter);
                RegisterHealth(u.Id,Gauge(screen,u.Id+" field HP",x+74,736,180,u.Hp,u.MaxHp,ElementColor(u.Element)));
                var target=Button(screen,"Support target "+u.Id,"",x+25,338,265,355,()=>Target(u.Id));target.image.color=Color.clear;target.GetComponent<OrnateButton>().Frame.enabled=false;target.gameObject.AddComponent<LinkTarget>().Id=u.Id;
            }
            var enemy=s.Enemies[0];fighters[enemy.Id]=Character(screen,string.IsNullOrEmpty(enemy.Portrait)?"Anger":enemy.Portrait,1306,192,479,558);
            anchors[enemy.Id]=new Vector2(1530,482);
            Label(screen,"Enemy name",(enemy.IsHuman?"인간 / ":"UMBRA / ")+enemy.Name,1280,130,561,46,25,Violet,TextAnchor.MiddleCenter);
            RegisterHealth(enemy.Id,Gauge(screen,"Enemy HP",1307,182,477,enemy.Hp,enemy.MaxHp,Violet),Label(screen,"Enemy life",$"{enemy.Hp} / {enemy.MaxHp}",1290,195,510,28,18,Muted,TextAnchor.MiddleCenter));
            Label(screen,"Enemy rift",$"균열 {enemy.Rift} / {enemy.MaxRift}",1510,224,290,24,16,Muted,TextAnchor.MiddleRight);
            Label(screen,"Sanity "+enemy.Id,$"정신력 {enemy.Sanity:+0;-0;0} · 어둠 활성 {BattleRules.FlameChance(enemy.Sanity):P0}",1270,744,545,31,18,Violet,TextAnchor.MiddleCenter);
            var enemyTarget=Button(screen,"Enemy body target","본체 공격",1493,658,242,57,()=>Target(enemy.Id));
            enemyTarget.gameObject.AddComponent<LinkTarget>().Id=enemy.Id;
            for(int i=0;i<enemy.Parts.Count;i++){
                var part=enemy.Parts[i];float x=1274+i*268;
                anchors[part.Id]=new Vector2(x+119,599);
                var b=Button(screen,"Part "+part.Id,part.Name+"  "+(part.Destroyed?"파괴됨":$"{part.Hp}/{part.MaxHp}"),x,570,245,56,()=>Target(part.Id));b.interactable=!part.Destroyed;b.gameObject.AddComponent<LinkTarget>().Id=part.Id;
            }
            for(int i=0;i<s.Intents.Count;i++){
                var intent=s.Intents[i];float x=950,y=254+i*127;anchors[intent.Id]=new Vector2(x+150,y+48);
                var b=Button(screen,"Intent "+intent.Id,"",x,y,302,97,()=>Target(intent.Id));
                b.gameObject.AddComponent<LinkTarget>().Id=intent.Id;
                Label(b.transform,"Intent name",intent.Name+(intent.Kind==IntentKind.Area?" · 광역":""),17,9,270,36,24,Violet);
                Label(b.transform,"Power",$"어둠 {BattleRules.Flames(intent)}개 · 위력 {BattleRules.Base(intent)}~{BattleRules.Base(intent)+BattleRules.Flame(intent)*BattleRules.Flames(intent)} · 속도 {intent.Speed}",17,52,274,28,16,Muted);
                if(anchors.TryGetValue(intent.TargetId,out var target))Thread(lineLayer,"Intent thread "+intent.Id,new Vector2(1256,y+48),target,Violet,true,2);
                else foreach(var unit in s.Allies)Thread(lineLayer,"Area intent "+intent.Id,new Vector2(1256,y+48),anchors[unit.Id],new Color(.66f,.49f,.82f,.55f),true,2);
            }
            Box(screen,"Action dock",0,782,1920,242,new Color(.035f,.05f,.075f,.98f));
            for(int i=0;i<s.Allies.Count;i++){
                var u=s.Allies[i];float x=35+i*399;
                Box(screen,u.Id+" card",x,801,381,201,PanelColor);
                Label(screen,u.Id+" card name",u.Name+"   "+BattleRules.ElementName(u.Element),x+17,800,235,42,24,ElementColor(u.Element));
                Label(screen,"Sanity "+u.Id,$"정신력 {u.Sanity:+0;-0;0} · 점등 {BattleRules.FlameChance(u.Sanity):P0}",x+18,841,345,23,16,Gold);
                var health=Label(screen,u.Id+" card HP",$"{u.Hp}/{u.MaxHp}",x+247,815,112,35,18,Muted,TextAnchor.MiddleRight);
                RegisterHealth(u.Id,Gauge(screen,u.Id+" card gauge",x+17,863,345,u.Hp,u.MaxHp,ElementColor(u.Element)),health);
                for(int j=0;j<u.SlotCount;j++){
                    int slot=j;var link=s.Links.FirstOrDefault(l=>l.ActorId==u.Id&&(l.Slot==slot||battle.Skill(l.ActorId,l.SkillId).Slots==2));
                    string caption=link==null?"행동 "+(j+1)+"  +":battle.Skill(u.Id,link.SkillId).Name;
                    var btn=Button(screen,u.Id+" slot "+j,caption,x+16+j*180,887,168,62,()=>SelectSlot(u.Id,slot),selectedActor==u.Id&&selectedSlot==j);
                    btn.interactable=u.Alive;slotAnchors[u.Id+":"+j]=new Vector2(x+100+j*180,887);
                    var drag=btn.gameObject.AddComponent<LinkDrag>();
                    drag.Begin=e=>BeginLinkDrag(e,u.Id,slot,link?.SkillId);
                    drag.Drag=MoveLinkDrag;drag.End=EndLinkDrag;
                    Label(screen,u.Id+" target "+j,link==null?"미연결":TargetName(link.TargetId),x+16+j*180,958,172,27,16,Muted,TextAnchor.MiddleCenter);
                }
            }
            var selected=battle.Unit(selectedActor);
            Label(screen,"Skill selection",$"{selected.Name} · 행동 {selectedSlot+1} · 동기화 {selected.SyncLevel}",1262,787,610,37,21,Gold);
            string composition="1스킬 ×3 · 2스킬 ×2"+(selected.SyncLevel>0?" · 3스킬 ×1":" · 3스킬 동기화 필요");
            Label(screen,"Skill bag",composition+" · 남은 덱 "+selected.DrawPile.Count,1267,824,585,24,14,Muted);
            for(int i=0;i<selected.SkillHand.Count;i++){
                var card=selected.SkillHand[i];var sk=battle.Skill(selected.Id,card.SkillId);int offered=i+1;
                bool available=CanUseSkillCard(selected,card,selectedSlot);
                var b=Button(screen,"Skill card "+card.Id,$"{offered}. {sk.Number}스킬 · {sk.Name}",1265+i*289,852,275,69,()=>{
                    selectedSkill=sk.Id;selectedCard=card.Id;DrawBattle();
                },card.Id==selectedCard);
                Label(b.transform,"Flame count",$"불 {BattleRules.Flames(sk)}개 · {BattleRules.Base(sk)}+{BattleRules.Flame(sk)}×점등 · 비용 {sk.Cost}",10,41,255,23,12,Muted,TextAnchor.MiddleCenter);
                b.GetComponent<OrnateButton>().Caption.rectTransform.sizeDelta=new Vector2(225,38);
                b.GetComponent<OrnateButton>().Caption.fontSize=17;b.interactable=available;
            }
            var defense=selected.Skills.FirstOrDefault(BattleRules.IsDefensive);
            if(defense!=null){
                string kind=defense.Kind==SkillKind.Evade?"회피":defense.Kind==SkillKind.Counter?"반격":"방어";
                var b=Button(screen,"Defense skill "+defense.Id,kind+" · "+defense.Name+"    불 "+BattleRules.Flames(defense)+"개",1265,930,564,35,()=>{
                    selectedSkill=defense.Id;selectedCard=null;DrawBattle();
                },selectedSkill==defense.Id&&string.IsNullOrEmpty(selectedCard));
                b.interactable=selected.Alive;b.GetComponent<OrnateButton>().Caption.fontSize=17;
            }
            foreach(var l in s.Links){
                if(!anchors.TryGetValue(l.TargetId,out var end))continue;var color=Gold;
                if(l.Kind==LinkKind.Clash){var preview=battle.Preview(l);color=preview.Grade==PreviewGrade.Advantage?Teal:preview.Grade==PreviewGrade.Disadvantage?Hex("E77A5D"):Gold;
                    Label(screen,"Clash prediction",preview.Summary,660,154+28*s.Links.IndexOf(l),590,28,18,color);
                }
                var linked=Thread(lineLayer,"Light thread",slotAnchors[l.ActorId+":"+l.Slot],end,color,false,3);linked.Glow=true;linked.Pulse=true;linked.ReducedMotion=reducedMotion;
            }
            Button(screen,"Recommend","AI 제안",50,145,174,56,()=>{battle.RecommendLinks();DrawBattle();Notify("광역 맞불 → 부위 파괴 → 부상 회복 순서로 제안했습니다.");});
            Button(screen,"Reuse","이전 연결",239,145,174,56,()=>{battle.ReusePreviousLinks();DrawBattle();});
            Button(screen,"Clear links","초기화",428,145,135,56,()=>{battle.ClearLinks();DrawBattle();});
            Button(screen,"Auto mode",autoMode?"오토 켜짐":"수동 연결",50,216,174,52,()=>{autoMode=!autoMode;if(autoMode)battle.RecommendLinks();DrawBattle();},autoMode);
            Button(screen,"Release lantern",s.LanternQueued?"해방 예약됨":"등불 해방",240,216,218,52,()=>{if(!battle.TryLantern(selectedActor,out var error))Notify(error);else DrawBattle();}).interactable=s.Lantern>=100&&!s.LanternQueued;
            Button(screen,"Battle log","전투 기록",479,216,140,52,ShowBattleLog);
            Button(screen,"Remove selected","연결 해제",1265,971,275,42,()=>{battle.RemoveLink(selectedActor,selectedSlot);DrawBattle();});
            var resolve=Button(screen,"Resolve round","빛실 실행   →",1554,971,275,42,()=>{if(autoMode)battle.RecommendLinks();StartCoroutine(Resolve());},true);resolve.interactable=s.Links.Count>0;
            Footer("제시된 스킬과 방어 행동을 골라 연결 · 맞불 2연승 → 공격 · 초록: 유리 / 주황: 접전 / 붉음: 불리");
        }
        string TargetName(string id){var intent=battle.Intent(id);if(intent!=null)return "맞불 · "+intent.Name;var unit=battle.Unit(id);if(unit!=null)return unit.Name;return battle.State.Enemies.SelectMany(e=>e.Parts).FirstOrDefault(p=>p.Id==id)?.Name??id;}
        bool CanUseSkillCard(UnitState actor,SkillCard card,int slot)
        {
            if(actor==null||card==null||!actor.Alive)return false;
            var skill=battle.Skill(actor.Id,card.SkillId);if(skill==null||!BattleRules.IsSkillUnlocked(actor,skill))return false;
            if(skill.Slots==2&&slot!=0)return false;
            int replaced=battle.State.Links.Where(l=>l.ActorId==actor.Id&&(l.Slot==slot||skill.Slots==2||battle.Skill(l.ActorId,l.SkillId).Slots==2)).Sum(l=>battle.Skill(l.ActorId,l.SkillId).Cost);
            return skill.Cost<=battle.AvailableEmber+replaced&&battle.IsCardAvailable(actor,card.Id,slot);
        }
        void ChooseSkillForSlot(string actor,int slot,bool preserve=false)
        {
            var unit=battle.Unit(actor);
            if(unit==null||!unit.Alive)unit=battle.State.Allies.FirstOrDefault(u=>u.Alive)??battle.State.Allies.First();
            slot=Mathf.Clamp(slot,0,unit.SlotCount-1);bool same=selectedActor==unit.Id&&selectedSlot==slot&&selectionRound==battle.State.Round;
            selectedActor=unit.Id;selectedSlot=slot;selectionRound=battle.State.Round;
            if(preserve&&same){
                var skill=battle.Skill(unit.Id,selectedSkill);
                if(skill!=null&&BattleRules.IsDefensive(skill)&&string.IsNullOrEmpty(selectedCard))return;
                var card=unit.SkillHand.FirstOrDefault(c=>c.Id==selectedCard&&c.SkillId==selectedSkill);
                if(CanUseSkillCard(unit,card,slot))return;
            }
            var linked=battle.State.Links.FirstOrDefault(l=>l.ActorId==unit.Id&&(l.Slot==slot||battle.Skill(l.ActorId,l.SkillId).Slots==2));
            if(linked!=null){
                var skill=battle.Skill(unit.Id,linked.SkillId);
                var card=unit.SkillHand.FirstOrDefault(c=>c.Id==linked.CardId);
                if(BattleRules.IsDefensive(skill)||CanUseSkillCard(unit,card,slot)){selectedSkill=linked.SkillId;selectedCard=linked.CardId;return;}
            }
            var available=unit.SkillHand.FirstOrDefault(c=>CanUseSkillCard(unit,c,slot));
            if(available!=null){selectedSkill=available.SkillId;selectedCard=available.Id;return;}
            selectedSkill=unit.Skills.FirstOrDefault(BattleRules.IsDefensive)?.Id;selectedCard=null;
        }
        void SelectSlot(string actor,int slot)
        { ChooseSkillForSlot(actor,slot);DrawBattle(); }
        void Target(string id)
        {
            if(page!="battle"||overlay||resolving)return;
            var skill=battle.Skill(selectedActor,selectedSkill);LinkKind kind=skill.Kind==SkillKind.Heal?LinkKind.Support:(skill.Kind==SkillKind.Guard||skill.Kind==SkillKind.Evade||skill.Kind==SkillKind.Counter)?LinkKind.Guard:battle.Intent(id)!=null?LinkKind.Clash:LinkKind.Strike;
            if(battle.TryLink(selectedActor,selectedSlot,selectedSkill,kind,id,out string error,selectedCard)){Tone("link",880,.2f,.13f);DrawBattle();if(anchors.TryGetValue(id,out var end))StartCoroutine(ConnectionPulse(slotAnchors[selectedActor+":"+selectedSlot],end,kind==LinkKind.Support?Teal:Gold,screenVersion));}else Notify(error);
        }
        Vector2 Pointer(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(screen,e.position,null,out var p);return new Vector2(p.x,-p.y);}
        Vector2 ToMesh(Vector2 p){return new Vector2(p.x,-p.y);}
        ThreadGraphic Thread(Transform p,string name,Vector2 from,Vector2 to,Color c,bool dashed,float width)
        {var g=MakeRect(p,name,0,0,1920,1080).gameObject.AddComponent<ThreadGraphic>();g.From=ToMesh(from);g.To=ToMesh(to);g.color=c;g.Dashed=dashed;g.Width=width;g.raycastTarget=false;return g;}
        void ConfirmRetreat()
        {
            if(overlay)return;
            overlay=MakeRect(stage,"Retreat prompt",0,0,1920,1080);Box(overlay,"Scrim",0,0,1920,1080,new Color(0,0,0,.84f)).raycastTarget=true;
            Label(overlay,"Retreat question","이 전투를 중단하고 작전실로 돌아갈까요?",370,350,1180,120,38,Ivory,TextAnchor.MiddleCenter);
            Button(overlay,"Stay","전투 계속",530,540,390,76,()=>Destroy(overlay.gameObject),true);Button(overlay,"Leave","작전실로 돌아가기",1000,540,390,76,LeaveCampaignBattle);
        }
        void ShowBattleLog()
        {
            if(overlay)return;
            overlay=MakeRect(stage,"Battle log",0,0,1920,1080);Box(overlay,"Scrim",0,0,1920,1080,new Color(0,0,0,.92f)).raycastTarget=true;
            Label(overlay,"Log heading","라운드 기록",100,65,1400,70,44,Gold);
            var lines=lastPresentedEvents.TakeLast(17).Select(e=>e.Message).ToList();
            if(lines.Count==0)lines=battle.State.Links.Select(l=>battle.Unit(l.ActorId).Name+" → "+TargetName(l.TargetId)+" : "+l.Reason).ToList();
            Label(overlay,"Events",string.Join("\n",lines),107,184,1690,735,26);
            Button(overlay,"Close battle log","닫기",1510,917,294,68,()=>Destroy(overlay.gameObject),true);
        }
        IEnumerator Resolve()
        {
            if(page!="battle"||overlay||resolving)yield break;
            int version=screenVersion;
            resolving=true;CancelDragVisuals();page="resolve";
            var dock=screen.GetComponentsInChildren<Button>();foreach(var b in dock)b.interactable=false;
            foreach(var drag in screen.GetComponentsInChildren<LinkDrag>())drag.enabled=false;
            foreach(var sway in screen.GetComponentsInChildren<IdleSway>())sway.enabled=false;
            var phase=screen.Find("Phase");if(phase)phase.GetComponent<Text>().text=$"ROUND {battle.State.Round:00}   /   ③ 실행";
            var grades=new Dictionary<string,IgniteGrade>();
            var planned=battle.State.Links.OrderByDescending(l=>battle.Skill(l.ActorId,l.SkillId).Speed).ToList();
            foreach(var link in planned){
                if(battle.Skill(link.ActorId,link.SkillId).Kind!=SkillKind.Attack||assisted||autoMode){grades[link.ActorId+":"+link.Slot]=IgniteGrade.Good;continue;}
                overlay=MakeRect(stage,"Ignite prompt",0,0,1920,1080);Box(overlay,"Ignite scrim",0,0,1920,1080,new Color(.02f,.03f,.055f,.25f)).raycastTarget=true;
                Label(overlay,"Ignite skill",battle.Unit(link.ActorId).Name+"  /  "+battle.Skill(link.ActorId,link.SkillId).Name,320,193,1280,74,42,Gold,TextAnchor.MiddleCenter);
                Label(overlay,"Ignite instruction",reducedMotion?"‘지금 점화’가 표시될 때 클릭 또는 Space":"두 원이 겹칠 때 클릭 또는 Space",400,760,1120,62,29,Ivory,TextAnchor.MiddleCenter);
                Ring(overlay,"Target timing ring",850,430,220,Gold,5);
                var moving=Ring(overlay,"Shrinking timing ring",760,340,400,Ivory,3);
                var timing=Label(overlay,"Timing countdown","",735,577,450,55,23,Gold,TextAnchor.MiddleCenter);
                var hit=Box(overlay,"Timing input",650,320,620,430,Color.clear);hit.raycastTarget=true;
                var press=hit.gameObject.AddComponent<ImmediatePress>();ignitionTapped=false;
                press.Pressed=e=>{if(version==screenVersion&&!ignitionTapped){ignitionTapped=true;ignitionTimestamp=Time.unscaledTime;}};
                float began=Time.unscaledTime;
                while(Time.unscaledTime-began<1.8f&&!ignitionTapped){
                    if(version!=screenVersion)yield break;
                    float diameter=reducedMotion?220:400-180*(Time.unscaledTime-began)/1.1f;
                    moving.rectTransform.sizeDelta=new Vector2(diameter,diameter);moving.rectTransform.anchoredPosition=new Vector2(960-diameter/2,-540+diameter/2);moving.SetVerticesDirty();
                    float until=1.1f-(Time.unscaledTime-began);timing.text=Mathf.Abs(until)<.13f?"지금 점화":until>0?"점화까지 "+until.ToString("0.0")+"초":"점화 창이 지났습니다";
                    if(Input.GetKeyDown(KeyCode.Space)){ignitionTapped=true;ignitionTimestamp=Time.unscaledTime;}yield return null;
                }
                var grade=ignitionTapped?BattleRules.GradeTiming((ignitionTimestamp-began-1.1f)*1000,battle.State.Gloom,false,battle.State.LanternQueued):IgniteGrade.Miss;
                grades[link.ActorId+":"+link.Slot]=grade;Label(overlay,"Timing result",grade==IgniteGrade.Perfect?"점 화":grade==IgniteGrade.Good?"양 호":"빗나감",620,496,680,80,58,Gold,TextAnchor.MiddleCenter);
                Tone("ignite"+grade,grade==IgniteGrade.Perfect?1046:523,.38f,.25f);yield return new WaitForSecondsRealtime(.48f);if(version!=screenVersion)yield break;if(overlay)Destroy(overlay.gameObject);overlay=null;
            }
            if(version!=screenVersion)yield break;
            var events=battle.ResolveRound(link=>grades.TryGetValue(link.ActorId+":"+link.Slot,out var grade)?grade:IgniteGrade.Good).ToList();
            lastPresentedEvents.Clear();lastPresentedEvents.AddRange(events);
            activeClashActor=null;
            if(version!=screenVersion)yield break;
            overlay=MakeRect(stage,"Battlefield effects",0,0,1920,1080);
            Box(overlay,"Resolution input lock",0,0,1920,1080,Color.clear).raycastTarget=true;
            Box(overlay,"Action subtitle plate",495,130,1330,56,new Color(.018f,.030f,.050f,.76f));
            var eventText=Label(overlay,"Current action","",521,135,1280,46,23,Ivory,TextAnchor.MiddleCenter);
            foreach(var e in events){if(version!=screenVersion)yield break;yield return PresentBattleEvent(e,overlay,eventText,version);}
            if(version!=screenVersion)yield break;
            SyncBattlePresentation();
            resolving=false;
            if(battle.State.Outcome!=BattleOutcome.Ongoing){ShowResult();yield break;}
            battle.BeginNextRound();DrawBattle();
            if(autoMode){
                battle.RecommendLinks();DrawBattle();int nextVersion=screenVersion;
                yield return new WaitForSecondsRealtime(1.2f);
                // Opening a log pauses this single pending continuation.
                // Closing it resumes the same round; navigation/another resolve invalidates it.
                while(page=="battle"&&autoMode&&screenVersion==nextVersion&&overlay&&!resolving)yield return null;
                if(page=="battle"&&autoMode&&screenVersion==nextVersion&&!overlay&&!resolving)StartCoroutine(Resolve());
            }
        }
        void ShowResult()
        {
            bool victory=battle.State.Outcome==BattleOutcome.Victory;RewardData reward=null;
            bool inCampaign=campaign.Active!=null;
            if(victory){
                if(inCampaign&&campaign.Phase==CampaignPhase.Battle){
                    if(campaign.AcceptVictory(battle,progress,out reward,out string error))Persist();else Notify(error);
                }else if(!inCampaign&&progress.ClaimVictory(battle,out reward))Persist();
            }
            Clear("result");Character(screen,battle.State.Allies[0].Portrait,90,143,700,826);
            Label(screen,"Result eyebrow",victory?"THE LIGHT REMAINS":"A MOMENT IN THE DARK",862,233,970,39,21,Gold);
            Label(screen,"Result headline",victory?"지켜낸 불씨":"아직, 끝이 아니야",854,310,1010,130,70);
            Label(screen,"Result quote",victory?"어둠이 물러난 자리.\n동료들에게 남겨진 이야기를 이어갑니다.":"빛실을 다시 잇고, 다른 길을 찾아보자.\n정신력과 불의 수치를 확인하고 다시 맞붙을 수 있다.",864,482,930,132,29,Muted);
            if(reward!=null)Label(screen,"Rewards",reward.Summary.Replace(" / ","\n"),870,655,904,143,28,Gold);
            bool hasAfter=inCampaign&&campaign.Phase==CampaignPhase.AfterStory;
            string caption=victory?hasAfter?"후속 이야기 읽기 →":"작전실로 →":"다시 도전하기 →";
            Button(screen,"Result continue",caption,865,857,560,83,()=>{
                if(inCampaign){if(victory)ContinueCampaign();else StartCampaignBattle();}
                else if(victory)ShowStages();else StartBattle();
            },true);
            Button(screen,"Result hub","등대로",1450,857,312,83,ShowHub);
            Footer(hasAfter?"후속 대화를 마치면 이 스테이지가 완료됩니다":"진행 상황 자동 저장 · 기록실에서 완료한 이야기를 다시 볼 수 있습니다");
        }
    }
}
