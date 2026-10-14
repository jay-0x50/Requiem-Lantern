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
        bool explicitSlotFocus;
        BattleEngine slotFocusBattle;
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
            Label(screen,"Battle instruction","아래 스킬을 골라 대상을 클릭하거나, 카드에서 직접 드래그해 연결하세요.",805,78,1066,28,18,Muted,TextAnchor.MiddleRight);
            var lineLayer=MakeRect(screen,"Thread layer",0,0,1920,1080);
            for(int i=0;i<s.Allies.Count;i++){
                var u=s.Allies[i];float footX=165+(4-s.Allies.Count)*117.5f+i*235;
                string artId=string.IsNullOrEmpty(u.Portrait)?ArtId(u.Id):ArtId(u.Portrait);
                Box(screen,u.Id+" ground",footX-48,698,96,8,new Color(0,0,0,.3f));
                var portrait=BattleAvatar.Create(screen,artId,Texture(artId),false,new Vector2(footX,BattleAvatar.GroundY),BattleAvatar.AllyBodyHeight);
                fighters[u.Id]=portrait;
                if(!u.Alive)portrait.color=new Color(.36f,.36f,.4f,.6f);
                anchors[u.Id]=new Vector2(footX,595);
                Label(screen,u.Id+" battlefield name",u.Name,footX-85,707,170,27,19,Ivory,TextAnchor.MiddleCenter);
                RegisterHealth(u.Id,Gauge(screen,u.Id+" field HP",footX-66,738,132,u.Hp,u.MaxHp,ElementColor(u.Element)));
                var target=Button(screen,"Support target "+u.Id,"",footX-98,475,196,229,()=>Target(u.Id));target.image.color=Color.clear;target.GetComponent<OrnateButton>().Frame.enabled=false;target.gameObject.AddComponent<LinkTarget>().Id=u.Id;
            }
            var enemy=s.Enemies[0];string enemyArt=string.IsNullOrEmpty(enemy.Portrait)?"Anger":ArtId(enemy.Portrait);
            Box(screen,enemy.Id+" ground",1517,700,156,10,new Color(0,0,0,.34f));
            fighters[enemy.Id]=BattleAvatar.Create(screen,enemyArt,Texture(enemyArt),true,new Vector2(1595,702),BattleAvatar.EnemyBodyHeight);
            anchors[enemy.Id]=new Vector2(1595,565);
            Label(screen,"Enemy name",(enemy.IsHuman?"인간 / ":"UMBRA / ")+enemy.Name,1280,130,561,46,25,Violet,TextAnchor.MiddleCenter);
            RegisterHealth(enemy.Id,Gauge(screen,"Enemy HP",1307,182,477,enemy.Hp,enemy.MaxHp,Violet),Label(screen,"Enemy life",$"{enemy.Hp} / {enemy.MaxHp}",1290,195,510,28,18,Muted,TextAnchor.MiddleCenter));
            Label(screen,"Enemy rift",$"균열 {enemy.Rift} / {enemy.MaxRift}",1510,224,290,24,16,Muted,TextAnchor.MiddleRight);
            Label(screen,"Sanity "+enemy.Id,$"정신력 {enemy.Sanity:+0;-0;0} · 어둠 활성 {BattleRules.FlameChance(enemy.Sanity):P0}",1270,728,545,26,18,Violet,TextAnchor.MiddleCenter);
            var enemyTarget=Button(screen,"Enemy body target","본체 공격",1484,313,222,46,()=>Target(enemy.Id));
            enemyTarget.gameObject.AddComponent<LinkTarget>().Id=enemy.Id;
            for(int i=0;i<enemy.Parts.Count;i++){
                var part=enemy.Parts[i];float x=1350+i*248;
                anchors[part.Id]=new Vector2(x+116,290);
                var b=Button(screen,"Part "+part.Id,part.Name+"  "+(part.Destroyed?"파괴됨":$"{part.Hp}/{part.MaxHp}"),x,265,232,47,()=>Target(part.Id));b.GetComponent<OrnateButton>().Caption.fontSize=19;b.interactable=!part.Destroyed;b.gameObject.AddComponent<LinkTarget>().Id=part.Id;
            }
            for(int i=0;i<s.Intents.Count;i++){
                var intent=s.Intents[i];float x=1050,y=282+i*119;anchors[intent.Id]=new Vector2(x+150,y+48);
                var b=Button(screen,"Intent "+intent.Id,"",x,y,302,97,()=>Target(intent.Id));
                b.gameObject.AddComponent<LinkTarget>().Id=intent.Id;
                Label(b.transform,"Intent name",intent.Name+(intent.Kind==IntentKind.Area?" · 광역":""),17,9,270,36,24,Violet);
                Label(b.transform,"Power",$"어둠 {BattleRules.Flames(intent)}개 · 위력 {BattleRules.Base(intent)}~{BattleRules.Base(intent)+BattleRules.Flame(intent)*BattleRules.Flames(intent)} · 속도 {intent.Speed}",17,52,274,28,16,Muted);
                if(anchors.TryGetValue(intent.TargetId,out var target))Thread(lineLayer,"Intent thread "+intent.Id,new Vector2(x+306,y+48),target,Violet,true,2);
                else foreach(var unit in s.Allies)Thread(lineLayer,"Area intent "+intent.Id,new Vector2(x+306,y+48),anchors[unit.Id],new Color(.66f,.49f,.82f,.55f),true,2);
            }
            Box(screen,"Action dock",0,758,1920,266,new Color(.025f,.041f,.065f,.98f));
            Box(screen,"Dock gold rail",0,758,1920,2,new Color(Gold.r,Gold.g,Gold.b,.55f));
            for(int i=0;i<s.Allies.Count;i++){
                var u=s.Allies[i];float x=42+(4-s.Allies.Count)*184+i*368;
                Box(screen,u.Id+" card",x-5,765,355,255,selectedActor==u.Id?new Color(.11f,.115f,.14f,.85f):PanelColor);
                for(int c=0;c<u.SkillHand.Count;c++){
                    var card=u.SkillHand[c];var sk=battle.Skill(u.Id,card.SkillId);int cardSlot=OfferedCardSlot(u,card);
                    var reserved=s.Links.FirstOrDefault(link=>link.ActorId==u.Id&&link.CardId==card.Id);
                    bool available=CanUseSkillCard(u,card,cardSlot);
                    var b=Button(screen,"Skill card "+card.Id,"",x+c*174,770,166,129,()=>SelectOfferedCard(u.Id,card.Id),selectedActor==u.Id&&card.Id==selectedCard);
                    b.interactable=available;
                    Label(b.transform,"Skill tier",sk.Number+"스킬  ·  "+BattleRules.ElementName(sk.Element),8,10,150,22,14,ElementColor(sk.Element),TextAnchor.MiddleCenter);
                    var skillCaption=Label(b.transform,"Skill name",sk.Name,8,34,150,30,20,Ivory,TextAnchor.MiddleCenter);
                    skillCaption.resizeTextForBestFit=true;skillCaption.resizeTextMinSize=16;skillCaption.resizeTextMaxSize=20;
                    Label(b.transform,"Skill power",$"불 {BattleRules.Flames(sk)}개 · {BattleRules.Base(sk)}~{BattleRules.Base(sk)+BattleRules.Flame(sk)*BattleRules.Flames(sk)}",8,68,150,24,14,Gold,TextAnchor.MiddleCenter);
                    string state=reserved!=null?(sk.Slots==2?"행동 1·2 예약 · 편집":"행동 "+(reserved.Slot+1)+" 예약 · 편집"):!available?"사용 불가":"비용 "+sk.Cost+" · "+(sk.Slots==2?"행동 두 칸":"행동 "+(cardSlot+1));
                    Label(b.transform,"Card reservation",state,7,99,152,22,13,reserved!=null?Teal:Muted,TextAnchor.MiddleCenter);
                    var cardDrag=b.gameObject.AddComponent<LinkDrag>();
                    cardDrag.Begin=e=>{if(SelectOfferedCard(u.Id,card.Id,false))BeginLinkDrag(e,u.Id,selectedSlot,sk.Id,true);};
                    cardDrag.Drag=MoveLinkDrag;cardDrag.End=EndLinkDrag;
                }
                var portrait=Picture(screen,u.Id+" dock portrait",string.IsNullOrEmpty(u.Portrait)?ArtId(u.Id):u.Portrait,x,906,69,69,false);
                portrait.uvRect=new Rect(.22f,.56f,.56f,.43f);
                if(!u.Alive)portrait.color=new Color(.35f,.35f,.4f,.6f);
                Label(screen,u.Id+" card name",u.Name,x+81,904,150,25,20,ElementColor(u.Element));
                var health=Label(screen,u.Id+" card HP",$"{u.Hp} / {u.MaxHp}",x+81,928,150,21,14,Muted);
                RegisterHealth(u.Id,Gauge(screen,u.Id+" card gauge",x+81,951,150,u.Hp,u.MaxHp,ElementColor(u.Element)),health);
                Label(screen,"Sanity "+u.Id,$"정신력 {u.Sanity:+0;-0;0} · 점등 {BattleRules.FlameChance(u.Sanity):P0}",x+81,968,218,18,13,Gold);
                var defense=u.Skills.FirstOrDefault(BattleRules.IsDefensive);
                if(defense!=null){
                    string kind=defense.Kind==SkillKind.Evade?"회피":defense.Kind==SkillKind.Counter?"반격":"방어";
                    var defensive=Button(screen,"Defense skill "+defense.Id,kind,x+245,906,96,38,()=>SelectDefense(u.Id),selectedActor==u.Id&&selectedSkill==defense.Id&&string.IsNullOrEmpty(selectedCard));
                    defensive.interactable=u.Alive;defensive.GetComponent<OrnateButton>().Caption.fontSize=17;
                    Label(screen,u.Id+" sync",u.SyncLevel>0?"3스킬 개방":"동기화 필요",x+241,944,104,22,12,Muted,TextAnchor.MiddleCenter);
                    var defenseDrag=defensive.gameObject.AddComponent<LinkDrag>();
                    defenseDrag.Begin=e=>{if(SelectDefense(u.Id,false))BeginLinkDrag(e,u.Id,selectedSlot,defense.Id,true);};defenseDrag.Drag=MoveLinkDrag;defenseDrag.End=EndLinkDrag;
                }
                for(int j=0;j<u.SlotCount;j++){
                    int slot=j;var link=s.Links.FirstOrDefault(l=>l.ActorId==u.Id&&(l.Slot==slot||battle.Skill(l.ActorId,l.SkillId).Slots==2));
                    string targetCaption=link==null?"미연결":TargetName(link.TargetId);if(targetCaption.Length>10)targetCaption=targetCaption.Substring(0,9)+"…";
                    string caption=(j+1)+" · "+targetCaption;
                    var btn=Button(screen,u.Id+" slot "+j,caption,x+j*174,987,166,31,()=>SelectSlot(u.Id,slot),selectedActor==u.Id&&selectedSlot==j);
                    btn.GetComponent<OrnateButton>().Caption.fontSize=12;
                    btn.interactable=u.Alive;slotAnchors[u.Id+":"+j]=new Vector2(x+83+j*174,987);
                    var drag=btn.gameObject.AddComponent<LinkDrag>();
                    drag.Begin=e=>BeginLinkDrag(e,u.Id,slot,link?.SkillId);
                    drag.Drag=MoveLinkDrag;drag.End=EndLinkDrag;
                }
            }
            var selected=battle.Unit(selectedActor);
            var focusedSkill=battle.Skill(selectedActor,selectedSkill);
            Label(screen,"Skill selection",$"{selected.Name} · 행동 {selectedSlot+1}",1570,769,314,26,19,Gold);
            Label(screen,"Selected skill name",focusedSkill?.Name??"스킬을 선택하세요",1570,801,314,30,22,Ivory);
            Label(screen,"Selected skill detail",focusedSkill==null?"":$"{BattleRules.Base(focusedSkill)} + {BattleRules.Flame(focusedSkill)} × 점등 · 불 {BattleRules.Flames(focusedSkill)}개\n비용 {focusedSkill.Cost} · 속도 {focusedSkill.Speed} · 행동 {focusedSkill.Slots}칸",1570,834,314,47,16,Muted);
            Label(screen,"Skill bag","1스킬 ×3 · 2스킬 ×2"+(selected.SyncLevel>0?" · 3스킬 ×1":"")+" · 덱 "+selected.DrawPile.Count,1570,884,314,22,13,Muted);
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
            Label(screen,"Dock plan count",$"연결 {s.Links.Count}/{s.Allies.Where(unit=>unit.Alive).Sum(unit=>unit.SlotCount)}",1570,906,85,29,17,Gold,TextAnchor.MiddleCenter);
            Label(screen,"Dock ember reservation",$"불씨 {battle.PlannedCost}/{s.Ember}",1658,906,91,29,17,Muted,TextAnchor.MiddleCenter);
            Button(screen,"Remove selected","연결 해제",1752,906,132,31,()=>{battle.RemoveLink(selectedActor,selectedSlot);DrawBattle();}).GetComponent<OrnateButton>().Caption.fontSize=16;
            var resolve=Button(screen,"Resolve round","빛실 실행   →",1570,944,314,65,()=>{if(autoMode)battle.RecommendLinks();StartCoroutine(Resolve());},true);resolve.interactable=s.Links.Count>0;
            resolve.GetComponent<OrnateButton>().Caption.fontSize=28;
            Footer("스킬 → 대상 클릭 / 드래그 · 행동칩: 연결 편집 · 초록: 유리 / 주황: 접전 / 붉음: 불리");
        }
        string TargetName(string id){var intent=battle.Intent(id);if(intent!=null)return "맞불 · "+intent.Name;var unit=battle.Unit(id);if(unit!=null)return unit.Name;return battle.State.Enemies.SelectMany(e=>e.Parts).FirstOrDefault(p=>p.Id==id)?.Name??id;}
        LinkCommand ActionAt(UnitState actor,int slot)
        {return battle.State.Links.FirstOrDefault(link=>link.ActorId==actor.Id&&(link.Slot==slot||battle.Skill(actor.Id,link.SkillId).Slots==2));}
        int OfferedCardSlot(UnitState actor,SkillCard card)
        {
            var reserved=battle.State.Links.FirstOrDefault(link=>link.ActorId==actor.Id&&link.CardId==card.Id);
            if(reserved!=null)return reserved.Slot;
            var skill=battle.Skill(actor.Id,card.SkillId);
            if(skill.Slots==2)return 0;
            bool same=selectedActor==actor.Id&&selectionRound==battle.State.Round;
            if(same&&explicitSlotFocus)return Mathf.Clamp(selectedSlot,0,actor.SlotCount-1);
            // Pick the intended action before checking cost. An unaffordable
            // second action must not silently refund and replace the first one.
            if(same&&ActionAt(actor,selectedSlot)==null)return selectedSlot;
            for(int slot=0;slot<actor.SlotCount;slot++)if(ActionAt(actor,slot)==null)return slot;
            return same?Mathf.Clamp(selectedSlot,0,actor.SlotCount-1):0;
        }
        bool SelectOfferedCard(string actorId,string cardId,bool redraw=true)
        {
            if(page!="battle"||overlay||resolving)return false;
            var actor=battle.Unit(actorId);var card=actor?.SkillHand.FirstOrDefault(value=>value.Id==cardId);
            if(actor==null||card==null)return false;
            int slot=OfferedCardSlot(actor,card);
            if(!CanUseSkillCard(actor,card,slot)){Notify("불씨와 사용할 수 있는 행동을 확인해 주세요.");return false;}
            if(selectedActor!=actorId||selectionRound!=battle.State.Round)explicitSlotFocus=false;
            selectedActor=actorId;selectedSlot=slot;selectedSkill=card.SkillId;selectedCard=card.Id;selectionRound=battle.State.Round;
            if(redraw)DrawBattle();else RefreshDockFocus();
            return true;
        }
        bool SelectDefense(string actorId,bool redraw=true)
        {
            if(page!="battle"||overlay||resolving)return false;
            var actor=battle.Unit(actorId);var defense=actor?.Skills.FirstOrDefault(BattleRules.IsDefensive);
            if(actor==null||!actor.Alive||defense==null)return false;
            bool same=selectedActor==actorId&&selectionRound==battle.State.Round;
            int slot=same?Mathf.Clamp(selectedSlot,0,actor.SlotCount-1):0;
            if(!(same&&explicitSlotFocus)&&ActionAt(actor,slot)!=null){
                for(int candidate=0;candidate<actor.SlotCount;candidate++)if(ActionAt(actor,candidate)==null){slot=candidate;break;}
            }
            if(!same)explicitSlotFocus=false;
            selectedActor=actorId;selectedSlot=slot;selectedSkill=defense.Id;selectedCard=null;selectionRound=battle.State.Round;
            if(redraw)DrawBattle();else RefreshDockFocus();
            return true;
        }
        void RefreshDockFocus()
        {
            foreach(var actor in battle.State.Allies){
                foreach(var card in actor.SkillHand){
                    var node=screen.Find("Skill card "+card.Id);if(!node)continue;
                    var frame=node.GetComponent<OrnateButton>().Frame;frame.Primary=selectedActor==actor.Id&&selectedCard==card.Id;frame.SetVerticesDirty();
                }
                for(int slot=0;slot<actor.SlotCount;slot++){
                    var node=screen.Find(actor.Id+" slot "+slot);if(!node)continue;
                    var frame=node.GetComponent<OrnateButton>().Frame;frame.Primary=selectedActor==actor.Id&&selectedSlot==slot;frame.SetVerticesDirty();
                }
                var defense=actor.Skills.FirstOrDefault(BattleRules.IsDefensive);var defenseNode=defense==null?null:screen.Find("Defense skill "+defense.Id);
                if(defenseNode){var frame=defenseNode.GetComponent<OrnateButton>().Frame;frame.Primary=selectedActor==actor.Id&&selectedSkill==defense.Id&&string.IsNullOrEmpty(selectedCard);frame.SetVerticesDirty();}
            }
            var actorName=screen.Find("Skill selection");if(actorName)actorName.GetComponent<Text>().text=battle.Unit(selectedActor).Name+" · 행동 "+(selectedSlot+1);
            var skill=battle.Skill(selectedActor,selectedSkill);var skillName=screen.Find("Selected skill name");if(skillName)skillName.GetComponent<Text>().text=skill?.Name??"";
            var detail=screen.Find("Selected skill detail");if(detail&&skill!=null)detail.GetComponent<Text>().text=$"{BattleRules.Base(skill)} + {BattleRules.Flame(skill)} × 점등 · 불 {BattleRules.Flames(skill)}개\n비용 {skill.Cost} · 속도 {skill.Speed} · 행동 {skill.Slots}칸";
            var selected=battle.Unit(selectedActor);var bag=screen.Find("Skill bag");if(bag)bag.GetComponent<Text>().text="1스킬 ×3 · 2스킬 ×2"+(selected.SyncLevel>0?" · 3스킬 ×1":"")+" · 덱 "+selected.DrawPile.Count;
        }
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
            if(slotFocusBattle!=battle||selectedActor!=unit.Id||selectionRound!=battle.State.Round)explicitSlotFocus=false;
            slotFocusBattle=battle;
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
        { ChooseSkillForSlot(actor,slot);explicitSlotFocus=true;DrawBattle(); }
        void Target(string id)
        {
            if(page!="battle"||overlay||resolving)return;
            var skill=battle.Skill(selectedActor,selectedSkill);if(skill==null)return;
            LinkKind kind=skill.Kind==SkillKind.Heal?LinkKind.Support:(skill.Kind==SkillKind.Guard||skill.Kind==SkillKind.Evade||skill.Kind==SkillKind.Counter)?LinkKind.Guard:battle.Intent(id)!=null?LinkKind.Clash:LinkKind.Strike;
            if(battle.TryLink(selectedActor,selectedSlot,selectedSkill,kind,id,out string error,selectedCard)){explicitSlotFocus=false;DrawBattle();PlayCombatSound("brace",false,.45f);if(anchors.TryGetValue(id,out var end))StartCoroutine(ConnectionPulse(slotAnchors[selectedActor+":"+selectedSlot],end,kind==LinkKind.Support?Teal:Gold,screenVersion));}else {DrawBattle();Notify(error);}
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
                PlayCombatSound("ignite",false,.6f);yield return new WaitForSecondsRealtime(.48f);if(version!=screenVersion)yield break;if(overlay)Destroy(overlay.gameObject);overlay=null;
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
