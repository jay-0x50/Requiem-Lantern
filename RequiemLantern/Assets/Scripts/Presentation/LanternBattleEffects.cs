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
        bool resolving;
        string activeClashActor;
        RectTransform activeFlameRoll;
        int flamePresentationRound=-1, tenseFlameCount;
        BattleEngine flamePresentationBattle;
        readonly HashSet<string> activeFlameActors=new HashSet<string>();
        RectTransform dragVisuals;
        RingGraphic dragHalo;
        Text dragHint;
        string snappedTarget;
        readonly Dictionary<string,RawImage> fighters=new Dictionary<string,RawImage>();
        readonly Dictionary<string,List<Image>> healthFills=new Dictionary<string,List<Image>>();
        readonly Dictionary<string,List<Text>> healthLabels=new Dictionary<string,List<Text>>();
        readonly Dictionary<string,int> visibleHealth=new Dictionary<string,int>();
        readonly Dictionary<Image,float> gaugeWidths=new Dictionary<Image,float>();
        readonly List<BattleEvent> lastPresentedEvents=new List<BattleEvent>();
        bool battleDockRetracted;

        // Keep the card nodes in place in the hierarchy: input and accessibility
        // focus keep their owner/CardId even while the planning tray slides away.
        IEnumerator RetractBattleDock(int version)
        {
            var tray=new Dictionary<RectTransform,Vector2>();
            foreach(Transform child in screen){
                var rect=child as RectTransform;
                if(rect&&-rect.anchoredPosition.y>=758)tray[rect]=rect.anchoredPosition;
            }
            float duration=reducedMotion?0:.24f;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime){
                if(version!=screenVersion)yield break;
                float offset=282*Mathf.SmoothStep(0,1,elapsed/duration);
                foreach(var item in tray)if(item.Key)item.Key.anchoredPosition=item.Value+Vector2.down*offset;
                yield return null;
            }
            if(version!=screenVersion)yield break;
            foreach(var item in tray)if(item.Key)item.Key.anchoredPosition=item.Value+Vector2.down*282;
            battleDockRetracted=true;
        }
        void HideResolutionPlanning()
        {
            foreach(Transform child in screen){
                string name=child.name;
                if(name=="Thread layer"||name.StartsWith("Intent ",StringComparison.Ordinal)||name.StartsWith("Part ",StringComparison.Ordinal)
                    ||name.StartsWith("Support target ",StringComparison.Ordinal)||name.StartsWith("Enemy body target ",StringComparison.Ordinal)
                    ||name=="Action progression"||name=="Battle instruction"||name=="Recommend"||name=="Reuse"||name=="Clear links"
                    ||name=="Auto mode"||name=="Release lantern"||name=="Battle log")child.gameObject.SetActive(false);
            }
            foreach(var ally in battle.State.Allies)foreach(string suffix in new[]{" battlefield name"," field HP track"," field HP fill"}){
                var hud=screen.Find(ally.Id+suffix);if(!hud)continue;
                var group=hud.GetComponent<CanvasGroup>()??hud.gameObject.AddComponent<CanvasGroup>();group.alpha=.18f;
            }
        }

        void RegisterHealth(string id,Image fill,Text label=null)
        {
            if(!healthFills.ContainsKey(id)){healthFills[id]=new List<Image>();healthLabels[id]=new List<Text>();}
            healthFills[id].Add(fill);gaugeWidths[fill]=fill.rectTransform.sizeDelta.x/Mathf.Max(.0001f,(float)battle.Unit(id).Hp/battle.Unit(id).MaxHp);
            if(label)healthLabels[id].Add(label);
        }
        void RefreshHealth(string id,int amount)
        {
            var unit=battle.Unit(id);if(unit==null)return;
            visibleHealth[id]=Mathf.Clamp(amount,0,unit.MaxHp);
            if(healthFills.TryGetValue(id,out var fills))foreach(var fill in fills)if(fill)fill.rectTransform.sizeDelta=new Vector2(gaugeWidths[fill]*(float)visibleHealth[id]/unit.MaxHp,7);
            if(healthLabels.TryGetValue(id,out var labels))foreach(var label in labels)if(label)label.text=visibleHealth[id]+" / "+unit.MaxHp;
        }
        void SyncBattlePresentation()
        {
            foreach(var unit in battle.State.Allies.Concat(battle.State.Enemies))RefreshHealth(unit.Id,unit.Hp);
            foreach(var unit in battle.State.Allies.Concat(battle.State.Enemies))RefreshSanity(unit.Id,unit.Sanity);
            foreach(var enemy in battle.State.Enemies){
                var rift=screen.Find("Enemy rift "+enemy.Id);
                if(rift)rift.GetComponent<Text>().text=$"균열 {enemy.Rift} / {enemy.MaxRift}";
                foreach(var part in enemy.Parts)if(part.Destroyed)ShowPartBroken(part.Id);
            }
        }
        void ShowPartBroken(string id)
        {
            var transform=screen.Find("Part "+id);if(!transform)return;
            var button=transform.GetComponent<OrnateButton>();
            var part=battle.State.Enemies.SelectMany(enemy=>enemy.Parts).FirstOrDefault(p=>p.Id==id);
            if(button&&part!=null){button.Caption.text=part.Name+" · 파괴됨";button.interactable=false;}
        }
        void FadeIntent(string id)
        {
            var card=screen.Find("Intent "+id);
            if(card){var group=card.GetComponent<CanvasGroup>()??card.gameObject.AddComponent<CanvasGroup>();group.alpha=.32f;}
            var lineRoot=screen.Find("Thread layer");if(!lineRoot)return;
            foreach(var line in lineRoot.GetComponentsInChildren<ThreadGraphic>())
                if(new[]{"Intent thread ","Area intent ","Original intent ","Enemy intent "}.Any(prefix=>line.name==prefix+id||line.name.StartsWith(prefix+id+" ",StringComparison.Ordinal)))line.gameObject.SetActive(false);
        }
        void CancelDragVisuals()
        {
            if(dragVisuals)Destroy(dragVisuals.gameObject);
            dragVisuals=null;dragLine=null;dragHalo=null;dragHint=null;snappedTarget=null;
        }
        bool CanConnect(string id)
        {
            var skill=battle.Skill(selectedActor,selectedSkill);if(skill==null||string.IsNullOrEmpty(id))return false;
            var unit=battle.Unit(id);
            if(skill.Kind!=SkillKind.Attack)return unit!=null&&!unit.IsEnemy&&unit.Alive;
            var intent=battle.Intent(id);if(intent!=null)return !intent.Cancelled&&!intent.Resolved;
            if(unit!=null)return unit.IsEnemy&&unit.Alive;
            return battle.State.Enemies.Any(enemy=>enemy.Alive&&enemy.Parts.Any(part=>part.Id==id&&!part.Destroyed));
        }
        string DropTarget(PointerEventData e)
        {
            var hit=e.pointerCurrentRaycast.gameObject;
            var direct=hit?hit.GetComponentInParent<LinkTarget>():null;
            if(direct)return CanConnect(direct.Id)?direct.Id:null;
            var point=Pointer(e);
            var closest=anchors.Where(a=>CanConnect(a.Key)).OrderBy(a=>(a.Value-point).sqrMagnitude).FirstOrDefault();
            return closest.Key!=null&&(closest.Value-point).sqrMagnitude<145*145?closest.Key:null;
        }
        void BeginLinkDrag(PointerEventData e,string actor,int slot,string previousSkill,bool fromCard=false)
        {
            var unit=battle.Unit(actor);
            if(page!="battle"||overlay||resolving||unit==null||!unit.Alive||slot<0||slot>=unit.SlotCount)return;
            CancelDragVisuals();
            if(!fromCard||selectedActor!=actor||selectedSlot!=slot)ChooseSkillForSlot(actor,slot);
            var skill=battle.Skill(selectedActor,selectedSkill);
            if(skill==null||skill.Slots>unit.SlotCount)return;
            if(!fromCard)explicitSlotFocus=true;
            RefreshDockFocus();
            dragVisuals=MakeRect(screen,"Live light thread",0,0,1920,1080);
            dragLine=Thread(dragVisuals,"Gold filament",slotAnchors[actor+":"+slot],Pointer(e),Gold,false,4);
            dragLine.Glow=true;dragLine.Pulse=true;dragLine.ReducedMotion=reducedMotion;
            dragHalo=Ring(dragVisuals,"Target resonance",0,0,74,Gold,3);
            dragHint=Label(dragVisuals,"Connection prediction","",0,0,450,65,20,Ivory,TextAnchor.MiddleCenter);
            MoveLinkDrag(e);
        }
        void MoveLinkDrag(PointerEventData e)
        {
            if(page!="battle"||overlay||resolving||!dragLine)return;
            snappedTarget=DropTarget(e);Vector2 point=snappedTarget!=null?anchors[snappedTarget]:Pointer(e);
            Color color=Gold;string hint=battle.Skill(selectedActor,selectedSkill).Name+" · 연결할 대상을 선택하세요";
            if(snappedTarget!=null){
                hint=TargetName(snappedTarget);
                if(battle.Intent(snappedTarget)!=null){var intent=battle.Intent(snappedTarget);var preview=battle.Preview(new LinkCommand{ActorId=selectedActor,SkillId=selectedSkill,CardId=selectedCard,TargetId=snappedTarget,Slot=selectedSlot,Kind=LinkKind.Clash});color=preview.Grade==PreviewGrade.Advantage?Teal:preview.Grade==PreviewGrade.Disadvantage?Hex("E77A5D"):Gold;hint=preview.Summary+"\n"+(intent.Kind==IntentKind.Area?"전체 공격에 맞불":intent.TargetId==selectedActor?"자신을 향한 공격에 맞불":IntentOriginalTarget(intent)+" 대신 "+battle.Unit(selectedActor).Name+" 가로막기");}
                else if(battle.Skill(selectedActor,selectedSkill).Kind!=SkillKind.Attack)color=Teal;
            }
            dragLine.To=ToMesh(point);dragLine.color=color;dragLine.SetVerticesDirty();
            dragHalo.color=new Color(color.r,color.g,color.b,snappedTarget!=null?1:.35f);dragHalo.rectTransform.anchoredPosition=new Vector2(point.x-37,-point.y+37);
            dragHint.text=hint;dragHint.color=color;dragHint.rectTransform.anchoredPosition=new Vector2(Mathf.Clamp(point.x-225,25,1445),-Mathf.Clamp(point.y-103,130,700));
        }
        void EndLinkDrag(PointerEventData e)
        {
            if(!dragVisuals)return;
            string target=page=="battle"&&!overlay&&!resolving?DropTarget(e):null;
            CancelDragVisuals();
            if(page!="battle"||overlay||resolving)return;
            if(target!=null)Target(target);else {DrawBattle();Notify("대상 위에서 놓으면 빛실이 연결됩니다.");}
        }
        IEnumerator ConnectionPulse(Vector2 from,Vector2 to,Color color,int version)
        {
            var root=MakeRect(screen,"Connection confirmed",0,0,1920,1080);
            var beam=Thread(root,"Link flare",from,to,color,false,5);beam.Glow=true;
            var ring=Ring(root,"Link seal",to.x-28,to.y-28,56,color,3);
            for(float t=0;t<.42f;t+=Time.unscaledDeltaTime){
                if(version!=screenVersion||!root)yield break;
                float p=t/.42f;beam.color=new Color(color.r,color.g,color.b,1-p);
                float d=reducedMotion?56:56+p*78;ring.rectTransform.sizeDelta=new Vector2(d,d);ring.rectTransform.anchoredPosition=new Vector2(to.x-d/2,-to.y+d/2);ring.color=new Color(color.r,color.g,color.b,1-p);ring.SetVerticesDirty();yield return null;
            }
            if(root)Destroy(root.gameObject);
        }
        Vector2 BattlePoint(string id,Vector2 fallback)
        {
            if(!string.IsNullOrEmpty(id)&&anchors.TryGetValue(id,out var point))return point;
            return fallback;
        }
        IEnumerator PresentBattleEvent(BattleEvent e,RectTransform layer,Text status,int version)
        {
            if(version!=screenVersion||!layer)yield break;
            if(e.Type=="Ignite")activeClashActor=battle.Intent(e.TargetId)!=null?e.ActorId:null;
            if(e.Type=="Relink"||e.Type=="Illusion")activeClashActor=null;
            status.text=e.Type=="FlameRoll"?"불과 어둠의 위력을 판정합니다…":e.Message;
            if(e.Type=="Sanity")RefreshSanity(e.ActorId,e.ActorSanity);
            if(e.Type=="FlameRoll"){
                yield return PresentFlameRoll(e,layer,version);
                if(status&&version==screenVersion)status.text=e.Message;
            }
            else if(e.Type=="Damage"||e.Type=="Tie"||e.Type=="Clash")yield return PresentMelee(e,layer,status,version,e.Type=="Tie"||e.Type=="Clash");
            else yield return PresentBattleSignal(e,layer,status,version);
            if(e.Type=="Clash"||e.Type=="ClashWin")activeClashActor=null;
        }

        string CombatArt(string id)
        {
            var unit=battle.Unit(id);
            return unit!=null&&!string.IsNullOrEmpty(unit.Portrait)?ArtId(unit.Portrait):ArtId(id);
        }
        void RefreshSanity(string id,int sanity)
        {
            var label=screen.Find("Sanity "+id);
            if(label)label.GetComponent<Text>().text="정신력 "+sanity.ToString("+0;-0;0")+"  ·  "+(50+2*Mathf.Clamp(sanity,-20,20))+"%";
        }
        IEnumerator PresentFlameRoll(BattleEvent e,RectTransform layer,int version)
        {
            if(activeFlameRoll){activeFlameRoll.gameObject.SetActive(false);Destroy(activeFlameRoll.gameObject);}
            activeFlameRoll=MakeRect(layer,"Flame roll",540,220,840,138);
            Box(activeFlameRoll,"Roll glass",0,0,840,138,new Color(.017f,.026f,.047f,.95f));
            var actor=battle.Unit(e.ActorId);var target=battle.Unit(e.TargetId);
            activeFlameActors.Clear();if(actor!=null)activeFlameActors.Add(actor.Id);
            var sides=new List<FlameRollSide>();
            if(actor!=null)sides.Add(DrawRollSide(activeFlameRoll,actor,e.FlameStates,e.ActorPower,e.ActorSanity,e.BasePower,e.FlamePower,22));
            if(target!=null&&e.EnemyFlameStates!=null&&e.EnemyFlameStates.Length>0){activeFlameActors.Add(target.Id);sides.Add(DrawRollSide(activeFlameRoll,target,e.EnemyFlameStates,e.TargetPower,e.TargetSanity,e.EnemyBasePower,e.EnemyFlamePower,444));}
            Label(activeFlameRoll,"Roll index",e.ClashIndex>0?e.ClashIndex+"차 맞불":"위력 점화",328,111,184,24,17,Gold,TextAnchor.MiddleCenter);
            RefreshSanity(e.ActorId,e.ActorSanity);if(target!=null)RefreshSanity(target.Id,e.TargetSanity);
            if(flamePresentationBattle!=battle||flamePresentationRound!=battle.State.Round){flamePresentationBattle=battle;flamePresentationRound=battle.State.Round;tenseFlameCount=0;}
            bool tense=!reducedMotion&&tenseFlameCount<2&&e.EnemyFlameStates!=null&&e.EnemyFlameStates.Length>0
                &&(e.ClashIndex>=2||Mathf.Abs(e.ActorPower-e.TargetPower)<=2||actor!=null&&visibleHealth.TryGetValue(actor.Id,out var hp)&&hp<actor.MaxHp*.35f);
            if(tense)tenseFlameCount++;
            float duration=reducedMotion?.18f:tense?(autoMode?.62f:.86f):(autoMode?.26f:.36f);
            var marker=activeFlameRoll.gameObject.AddComponent<FlameRevealMarker>();
            marker.Total=sides.Sum(s=>s.States.Length);marker.Tense=tense;marker.Duration=duration;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime){
                if(version!=screenVersion||!layer)yield break;
                foreach(var side in sides){
                    float progress=Mathf.Clamp01((t/duration-.20f)/.62f);
                    int reveal=Mathf.Min(side.States.Length,Mathf.FloorToInt(progress*side.States.Length));
                    UpdateRollSide(side,reveal,t,tense);
                }
                int revealed=sides.Sum(s=>s.Revealed);
                if(revealed>marker.Revealed)PlayCombatSound("ignite",false,.5f);
                marker.Revealed=revealed;yield return null;
            }
            foreach(var side in sides)UpdateRollSide(side,side.States.Length,duration,false);
            if(marker.Revealed<marker.Total)PlayCombatSound("ignite",false,.5f);
            marker.Revealed=marker.Total;
            // Keep the resolved roll visible for the following weapon contact.
            yield return null;
        }
        sealed class FlameRollSide
        {
            public bool Dark;public bool[] States;public float FinalPower,BasePower,FlamePower,Multiplier;
            public int Revealed;public FlameGraphic[] Icons;public Text[] Labels;public Text Power;
        }
        FlameRollSide DrawRollSide(Transform parent,UnitState unit,bool[] flames,float power,int sanity,float basePower,float flamePower,float x)
        {
            bool dark=unit.IsEnemy;
            Label(parent,"Roll actor "+unit.Id,unit.Name+" · 정신력 "+sanity.ToString("+0;-0;0"),x,5,370,28,20,dark?Hex("BFA2EF"):Gold);
            int count=flames==null?0:flames.Length;
            var side=new FlameRollSide{Dark=dark,States=flames??new bool[0],FinalPower=power,BasePower=basePower,FlamePower=flamePower,Icons=new FlameGraphic[count],Labels=new Text[count]};
            float raw=basePower+flamePower*side.States.Count(lit=>lit);side.Multiplier=raw>0?power/raw:1;
            for(int i=0;i<count;i++){
                var icon=MakeRect(parent,"Flame "+unit.Id+" "+i,x+104+i*62,37,44,48).gameObject.AddComponent<FlameGraphic>();
                icon.Lit=false;icon.color=Hex("606471");icon.raycastTarget=false;side.Icons[i]=icon;
                side.Labels[i]=Label(parent,"Flame state "+unit.Id+" "+i,"?",x+101+i*62,89,53,20,13,Muted,TextAnchor.MiddleCenter);
            }
            side.Power=Label(parent,"Roll power "+unit.Id,"…",x,34,88,54,43,dark?Violet:Gold,TextAnchor.MiddleCenter);
            Label(parent,"Roll power caption "+unit.Id,"위력",x,90,88,20,14,dark?Violet:Gold,TextAnchor.MiddleCenter);
            return side;
        }
        void UpdateRollSide(FlameRollSide side,int revealed,float elapsed,bool tense)
        {
            side.Revealed=revealed;int litCount=0;
            for(int i=0;i<side.States.Length;i++){
                bool known=i<revealed,lit=known&&side.States[i];if(lit)litCount++;
                var icon=side.Icons[i];icon.Lit=lit;
                float shimmer=known||reducedMotion?1:.5f+.25f*Mathf.Sin(elapsed*(tense?30:18)+i);
                Color tint=known?(lit?(side.Dark?Hex("B99AFF"):Hex("FFC472")):Hex("404755")):Hex("858999");tint.a=shimmer;icon.color=tint;icon.SetVerticesDirty();
                side.Labels[i].text=known?(lit?(side.Dark?"활성":"점화"):"소등"):"?";
                side.Labels[i].color=lit?(side.Dark?Hex("D9C8FA"):Gold):Muted;
            }
            float current=(side.BasePower+side.FlamePower*litCount)*side.Multiplier;
            side.Power.text=revealed==side.States.Length?side.FinalPower.ToString("0.#"):current>0?current.ToString("0.#")+"…":"…";
        }
        static float Smooth(float value){return Mathf.SmoothStep(0,1,Mathf.Clamp01(value));}

        IEnumerator PresentMelee(BattleEvent e,RectTransform layer,Text status,int version,bool clash)
        {
            RawImage actorImage,targetImage;
            fighters.TryGetValue(e.ActorId,out actorImage);fighters.TryGetValue(e.TargetId,out targetImage);
            var actor=battle.Unit(e.ActorId);var target=battle.Unit(e.TargetId);
            if(!actorImage||!targetImage||actor==null||target==null){yield return PresentBattleSignal(e,layer,status,version);yield break;}
            bool damage=e.Type=="Damage";
            if(damage&&activeFlameRoll&&!activeFlameActors.Contains(e.ActorId)){activeFlameRoll.gameObject.SetActive(false);Destroy(activeFlameRoll.gameObject);activeFlameRoll=null;}
            Color attackColor=ElementColor(actor.Element),targetColor=targetImage.color;
            var fx=MakeRect(layer,"FX "+e.Type,0,0,1920,1080);
            Box(fx,"Duel atmosphere",0,117,1920,665,new Color(.014f,.020f,.035f,.25f));
            var marker=fx.gameObject.AddComponent<CombatPhaseMarker>();
            marker.Clash=clash;marker.SkillId=e.SkillId;marker.SkillNumber=e.SkillNumber;
            bool heavy=!clash&&(actor.Id=="lian"||actor.Id=="heidel"||actor.IsEnemy)&&e.SkillNumber>=2;
            bool flourish=!clash&&actor.Id=="sera"&&e.SkillNumber>=2;
            bool sweep=!clash&&(actor.Id=="minato"||actor.Id=="noa");
            var dimmed=new Dictionary<RawImage,Color>();
            var planningWidgets=new List<GameObject>();
            foreach(Transform child in screen){
                string name=child.name;
                bool planning=name=="Thread layer"||name.StartsWith("Intent ",StringComparison.Ordinal)||name.StartsWith("Part ",StringComparison.Ordinal)
                    ||name.StartsWith("Support target ",StringComparison.Ordinal)||name.StartsWith("Enemy body target ",StringComparison.Ordinal)||name=="Clash prediction"
                    ||name=="Recommend"||name=="Reuse"||name=="Clear links"||name=="Auto mode"||name=="Release lantern"||name=="Battle log";
                if(planning&&child.gameObject.activeSelf){planningWidgets.Add(child.gameObject);child.gameObject.SetActive(false);}
            }
            foreach(var image in fighters.Values)if(image&&image!=actorImage&&image!=targetImage){dimmed[image]=image.color;var tint=image.color;image.color=new Color(tint.r*.45f,tint.g*.47f,tint.b*.53f,tint.a);}
            CombatPosePlayer attacker=null,defender=null;
            Vector2 stageOrigin=stage.anchoredPosition;
            bool applied=false,swung=false;
            try {
                attacker=new CombatPosePlayer(fx,actorImage,CombatArt(actor.Id),!actor.IsEnemy,BodyHeight(actor),"Duel attacker");
                defender=new CombatPosePlayer(fx,targetImage,CombatArt(target.Id),!target.IsEnemy,BodyHeight(target),"Duel defender");
                int facing=actor.IsEnemy?-1:1;
                bool lowClash=clash&&(actor.Id=="sera"||target.Id=="sera")&&(attacker.HasAtlas&&defender.HasAtlas);
                int attackContactPose=lowClash&&actor.IsEnemy?CombatPosePlayer.FollowThrough:CombatPosePlayer.Swing;
                int defendContactPose=lowClash&&target.IsEnemy?CombatPosePlayer.FollowThrough:CombatPosePlayer.Swing;
                Vector2 attackContact=attacker.ContactOffset(attackContactPose),defendContact=defender.ContactOffset(defendContactPose);
                float contactX=Mathf.Clamp((attacker.OriginalFoot.x+defender.OriginalFoot.x)*.5f,1020,1540);
                float contactY=BattleAvatar.GroundY-Mathf.Min(BodyHeight(actor),BodyHeight(target))*.5f;
                const float groundY=BattleAvatar.GroundY;
                // The SD weapon tip and the larger enemy weapon share one contact.
                // Foot placement follows each frame's metadata, then returns to the floor.
                Vector2 actorMeet=new Vector2(contactX-attackContact.x,contactY-attackContact.y);
                // During a clash the weapons meet tip to tip. An unopposed strike reaches
                // the target's body instead of stopping at its extended weapon.
                Vector2 targetMeet=new Vector2(clash?contactX-defendContact.x:contactX+facing*35,clash?contactY-defendContact.y:groundY);
                Vector2 contact=new Vector2(contactX,contactY);
                if(reducedMotion){actorMeet=attacker.OriginalFoot;targetMeet=defender.OriginalFoot;contact=defender.OriginalFoot-new Vector2(0,BodyHeight(target)*.48f);}
                marker.ActorId=actor.Id;marker.TargetId=target.Id;marker.Attacker=attacker.Image;marker.Defender=defender.Image;marker.ContactPoint=contact;marker.AtlasActive=attacker.HasAtlas;marker.FrameLayoutActive=attacker.HasFrameLayout;marker.SDActive=attacker.HasSDAtlas;marker.BodyHeight=BodyHeight(actor);
                var shadowA=Box(fx,"Attacker ground",0,0,96,7,new Color(0,0,0,.25f));
                var shadowB=Box(fx,"Defender ground",0,0,106,8,new Color(0,0,0,.27f));
                shadowA.transform.SetAsFirstSibling();shadowB.transform.SetAsFirstSibling();
                var arc=MakeRect(fx,"Weapon swing arc",0,0,1920,1080).gameObject.AddComponent<WeaponArcGraphic>();
                arc.Center=ToMesh(contact)+new Vector2(-facing*22,0);arc.Facing=facing;arc.Radius=sweep?90:72+e.SkillNumber*5;arc.Thickness=heavy?13:flourish?6:9;arc.color=Color.clear;arc.raycastTarget=false;
                WeaponArcGraphic counter=null;
                if(clash){counter=MakeRect(fx,"Counter weapon arc",0,0,1920,1080).gameObject.AddComponent<WeaponArcGraphic>();counter.Center=ToMesh(contact)+new Vector2(facing*22,5);counter.Facing=-facing;counter.Radius=70;counter.Thickness=7;counter.color=Color.clear;counter.raycastTarget=false;}
                WeaponArcGraphic flourishArc=null;
                if(flourish){flourishArc=MakeRect(fx,"Return slash arc",0,0,1920,1080).gameObject.AddComponent<WeaponArcGraphic>();flourishArc.Center=ToMesh(contact)+new Vector2(facing*18,-8);flourishArc.Facing=-facing;flourishArc.Radius=70;flourishArc.Thickness=6;flourishArc.color=Color.clear;flourishArc.raycastTarget=false;}
                var contactRoot=MakeRect(fx,"Weapon contact",0,0,1920,1080);
                var seal=Ring(contactRoot,"Contact shock",contact.x-23,contact.y-23,46,Color.clear,3);
                var flash=Box(contactRoot,"Contact glint",contact.x-4,contact.y-4,8,8,Color.clear);flash.rectTransform.localRotation=Quaternion.Euler(0,0,45);
                var sparks=new List<Image>();var directions=new List<Vector2>();
                if(!reducedMotion)for(int i=0;i<12;i++){
                    float angle=(i+.27f)*Mathf.PI*2/12;
                    directions.Add(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(58+(i%4)*19));
                    var shard=Box(contactRoot,"Metal spark "+i,contact.x,contact.y,9+(i%3)*3,2,Color.clear);
                    shard.rectTransform.localRotation=Quaternion.Euler(0,0,-angle*Mathf.Rad2Deg);sparks.Add(shard);
                }
                var feedback=Label(fx,"Battle feedback",damage?"−"+e.Amount:e.Type=="Tie"?"동 률":e.WinnerStreak+"연승",contact.x-180,contact.y-132,360,88,damage?58:40,Color.clear,TextAnchor.MiddleCenter);
                var actorIdentity=Label(fx,"Duel attacker identity","",0,0,250,29,20,actor.IsEnemy?Violet:Gold,facing>0?TextAnchor.MiddleRight:TextAnchor.MiddleLeft);
                var targetIdentity=Label(fx,"Duel defender identity","",0,0,250,29,20,target.IsEnemy?Violet:Gold,facing>0?TextAnchor.MiddleLeft:TextAnchor.MiddleRight);
                marker.ActorIdentity=actorIdentity;marker.TargetIdentity=targetIdentity;
                status.transform.SetAsLastSibling();
                float speed=autoMode?.72f:1;
                float approach=(reducedMotion?.10f:.25f)*speed,windup=.16f*speed,swing=.11f*speed;
                float contactAt=approach+windup+swing,hold=reducedMotion?.025f:.075f,follow=.12f*speed,recoil=.11f*speed,recover=.18f*speed;
                if(!clash){
                    windup=(heavy?.27f:flourish?.11f:sweep?.21f:.16f)*speed;
                    swing=(heavy?.14f:flourish?.08f:sweep?.14f:.11f)*speed;
                    hold=reducedMotion?.025f:heavy?.10f:.075f;follow=(flourish?.23f:heavy?.18f:.12f)*speed;
                    recover=(heavy?.23f:flourish?.20f:.18f)*speed;contactAt=approach+windup+swing;
                }
                float recoilAt=contactAt+hold+follow,recoverAt=recoilAt+recoil,duration=recoverAt+recover;
                if(clash){approach=(reducedMotion?.06f:.15f)*speed;windup=.10f*speed;swing=.08f*speed;hold=reducedMotion?.025f:.065f;follow=.06f*speed;recoil=.05f*speed;recover=.10f*speed;contactAt=approach+windup+swing;recoilAt=contactAt+hold+follow;recoverAt=recoilAt+recoil;duration=recoverAt+recover;}
                Action impact=()=>{
                    if(applied||version!=screenVersion)return;applied=true;
                    if(damage&&visibleHealth.ContainsKey(e.TargetId))RefreshHealth(e.TargetId,visibleHealth[e.TargetId]-e.Amount);
                    if(PlayCombatSound(clash?"clash":HitSoundFamily(actor,e.SkillNumber,e.Amount),true))marker.ContactSoundCount++;
                };
                bool returnSwung=false;
                for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime){
                    if(version!=screenVersion||!fx)yield break;
                    string phase;int attackPose,defendPose;
                    if(elapsed<approach){phase="approach";attackPose=CombatPosePlayer.Idle;defendPose=CombatPosePlayer.Idle;}
                    else if(elapsed<approach+windup){phase="windup";attackPose=CombatPosePlayer.Windup;defendPose=clash?CombatPosePlayer.Windup:CombatPosePlayer.Idle;}
                    else if(elapsed<contactAt){phase="swing";attackPose=CombatPosePlayer.Swing;defendPose=clash?CombatPosePlayer.Swing:CombatPosePlayer.Idle;}
                    else if(elapsed<contactAt+hold){phase="contact";attackPose=attackContactPose;defendPose=clash?defendContactPose:CombatPosePlayer.Hit;}
                    else if(elapsed<recoilAt){phase="follow-through";attackPose=CombatPosePlayer.FollowThrough;defendPose=damage?CombatPosePlayer.Hit:CombatPosePlayer.FollowThrough;}
                    else if(elapsed<recoverAt){phase="recoil";attackPose=CombatPosePlayer.Recovery;defendPose=damage?CombatPosePlayer.Hit:CombatPosePlayer.Recovery;}
                    else {phase="recovery";attackPose=CombatPosePlayer.Recovery;defendPose=CombatPosePlayer.Recovery;}
                    marker.Phase=phase;marker.Pose=attackPose;
                    if(elapsed>=approach+windup&&!swung){swung=true;PlayCombatSound("swing");}
                    if(elapsed>=contactAt)impact();
                    bool fatal=damage&&applied&&visibleHealth.TryGetValue(e.TargetId,out var remaining)&&remaining<=0;
                    if(fatal&&elapsed>=contactAt+hold)defendPose=CombatPosePlayer.Hit;
                    attacker.SetPose(attackPose);defender.SetPose(defendPose);
                    Vector2 actorFoot=actorMeet,targetFoot=targetMeet;
                    float after=Mathf.Max(0,elapsed-contactAt),motionAfter=Mathf.Max(0,elapsed-contactAt-hold),returning=Smooth((elapsed-recoverAt)/recover);
                    if(!reducedMotion){
                        // Camera focus belongs to this pair's foreground layer. The
                        // sprites, foot pivots, weapon tips and sparks share a single
                        // transform, while HP and flame judgement remain stationary.
                        float focus=Smooth(elapsed/approach)*(1-returning);
                        float zoom=1+(clash?.10f:.16f)*focus*(autoMode?.75f:1);
                        Vector2 offset=new Vector2(1050-contact.x*zoom,555-contact.y*zoom)*focus;
                        fx.localScale=new Vector3(zoom,zoom,1);fx.anchoredPosition=ToMesh(offset);
                        marker.FocusScale=zoom;marker.FocusOffset=offset;
                        float arrive=Smooth(elapsed/approach);
                        actorFoot=Vector2.Lerp(attacker.OriginalFoot,actorMeet,arrive);
                        targetFoot=Vector2.Lerp(defender.OriginalFoot,targetMeet,arrive);
                        actorFoot.y-=Mathf.Sin(arrive*Mathf.PI)*14;
                        if(elapsed>=approach&&elapsed<contactAt){float strike=Smooth((elapsed-approach-windup)/swing);actorFoot.x-=facing*18*(1-strike);if(clash)targetFoot.x+=facing*12*(1-strike);}
                        if(elapsed>=contactAt){float recoilAmount=Mathf.Sin(Mathf.Clamp01(motionAfter/(follow+recoil))*Mathf.PI);actorFoot.x-=facing*12*recoilAmount;targetFoot.x+=facing*(damage?34:18)*recoilAmount;}
                        if(!clash){
                            float charge=Mathf.Sin(Mathf.Clamp01((elapsed-approach)/windup)*Mathf.PI);
                            if(elapsed>=approach&&elapsed<approach+windup){actorFoot.x-=facing*(heavy?32:flourish?18:23)*charge;actorFoot.y+=heavy?8*charge:0;attacker.Lean(facing*(heavy?7:flourish?4:5)*charge);}
                            if(elapsed>=contactAt&&elapsed<recoilAt){float drive=Mathf.Sin(Mathf.Clamp01(motionAfter/follow)*Mathf.PI);actorFoot.x+=facing*(heavy?24:flourish?42:19)*drive;attacker.Lean(-facing*(heavy?8:flourish?5:4)*drive);}
                            if(flourish&&elapsed>=contactAt+hold&&elapsed<recoilAt){attacker.SetPose(motionAfter<follow*.48f?CombatPosePlayer.Swing:CombatPosePlayer.FollowThrough);actorFoot.y-=Mathf.Sin(Mathf.Clamp01(motionAfter/follow)*Mathf.PI)*12;}
                        }
                        if(elapsed>=recoverAt){actorFoot=Vector2.Lerp(actorMeet,attacker.OriginalFoot,returning);targetFoot=Vector2.Lerp(targetMeet,defender.OriginalFoot,returning);}
                        float weight=heavy?6:3.5f;
                        if(applied&&after<hold)stage.anchoredPosition=stageOrigin+new Vector2(facing*weight,2);
                        else if(applied&&motionAfter<.13f){float strength=1-motionAfter/.13f;stage.anchoredPosition=stageOrigin+new Vector2(facing*weight*Mathf.Cos(motionAfter*95),2.5f*Mathf.Sin(motionAfter*130))*strength;}else stage.anchoredPosition=stageOrigin;
                    }
                    attacker.PlaceFoot(actorFoot);defender.PlaceFoot(targetFoot);
                    marker.Phase=phase;marker.Pose=attackPose;marker.ActorFoot=actorFoot;marker.DefenderFoot=targetFoot;
                    actorIdentity.text=actor.Name+" · "+(visibleHealth.TryGetValue(actor.Id,out var actorHp)?actorHp:actor.Hp)+"/"+actor.MaxHp;
                    targetIdentity.text=target.Name+" · "+(visibleHealth.TryGetValue(target.Id,out var targetHp)?targetHp:target.Hp)+"/"+target.MaxHp;
                    actorIdentity.rectTransform.anchoredPosition=ToMesh(actorFoot+new Vector2(facing>0?-262:12,15));
                    targetIdentity.rectTransform.anchoredPosition=ToMesh(targetFoot+new Vector2(facing>0?12:-262,15));
                    shadowA.rectTransform.anchoredPosition=ToMesh(actorFoot+new Vector2(-48,1));shadowB.rectTransform.anchoredPosition=ToMesh(targetFoot+new Vector2(-53,1));
                    if(fatal)defender.Tint(Color.Lerp(targetColor,new Color(.33f,.35f,.41f,.45f),Mathf.Clamp01(after/.20f)));
                    else if(!reducedMotion&&damage&&applied){float tint=Mathf.Clamp01(1-after/.20f)*.34f;defender.Tint(Color.Lerp(targetColor,new Color(1,.62f,.49f,targetColor.a),tint));}
                    float trailProgress=Mathf.Clamp01((elapsed-approach-windup)/swing),trailFade=elapsed<contactAt?1:Mathf.Clamp01(1-after/.17f);
                    arc.Progress=trailProgress;arc.color=new Color(attackColor.r,attackColor.g,attackColor.b,reducedMotion?0:trailFade*.66f);arc.SetVerticesDirty();
                    if(counter){counter.Progress=trailProgress;Color c=ElementColor(target.Element);counter.color=new Color(c.r,c.g,c.b,reducedMotion?0:trailFade*.62f);counter.SetVerticesDirty();}
                    if(flourishArc){
                        float returnProgress=Mathf.Clamp01(motionAfter/(follow*.8f));
                        float returnAlpha=applied&&motionAfter>0&&motionAfter<follow?Mathf.Sin(returnProgress*Mathf.PI)*.7f:0;
                        flourishArc.Progress=returnProgress;flourishArc.color=new Color(attackColor.r,attackColor.g,attackColor.b,reducedMotion?0:returnAlpha);flourishArc.SetVerticesDirty();
                        if(applied&&motionAfter>0&&!returnSwung){returnSwung=true;PlayCombatSound("swing");}
                    }
                    float burst=applied?Mathf.Clamp01(after/.27f):0,burstAlpha=applied&&!reducedMotion?1-burst:0;
                    float diameter=46+burst*92;seal.rectTransform.sizeDelta=new Vector2(diameter,diameter);seal.rectTransform.anchoredPosition=ToMesh(contact-new Vector2(diameter/2,diameter/2));seal.color=new Color(1,.83f,.48f,burstAlpha*.6f);seal.SetVerticesDirty();
                    flash.color=new Color(1,.95f,.78f,applied&&!reducedMotion?Mathf.Clamp01(1-after/.065f):0);
                    for(int i=0;i<sparks.Count;i++){Vector2 p=contact+directions[i]*burst+new Vector2(0,burst*burst*21);sparks[i].rectTransform.anchoredPosition=ToMesh(p);sparks[i].color=new Color(1,.74f+(i%3)*.07f,.40f,burstAlpha);}
                    float numberAlpha=applied?Mathf.Clamp01(1-Mathf.Max(0,after-.15f)/(duration-contactAt-.15f)):0;
                    feedback.color=new Color(attackColor.r,attackColor.g,attackColor.b,numberAlpha);
                    if(!reducedMotion)feedback.rectTransform.anchoredPosition=ToMesh(contact+new Vector2(-180,-132-Mathf.Min(after,.4f)*60));
                    yield return null;
                }
                if(version==screenVersion&&fx)impact();
            }
            finally {
                if(stage)stage.anchoredPosition=stageOrigin;
                if(attacker!=null)attacker.Dispose();if(defender!=null)defender.Dispose();
                foreach(var pair in dimmed)if(pair.Key)pair.Key.color=pair.Value;
                if(version==screenVersion){
                    // Only parent planning objects were hidden; cancelled thread children and
                    // disabled broken-part buttons retain the state set by their own events.
                    foreach(var widget in planningWidgets)if(widget)widget.SetActive(true);
                    if(damage&&applied&&targetImage&&visibleHealth.TryGetValue(e.TargetId,out var remaining)&&remaining<=0)targetImage.color=new Color(.33f,.35f,.41f,.45f);
                }
                if(fx){fx.gameObject.SetActive(false);Destroy(fx.gameObject);}
            }
        }

        // Support and state changes retain light motifs; melee never uses a travelling beam.
        IEnumerator PresentBattleSignal(BattleEvent e,RectTransform layer,Text status,int version)
        {
            if(version!=screenVersion||!layer)yield break;
            var actor=battle.Unit(e.ActorId);var target=battle.Unit(e.TargetId);
            bool damage=e.Type=="Damage",heal=e.Type=="Heal",guard=e.Type=="Guard"||e.Type=="Evade"||e.Type=="EvadeFail"||e.Type=="Counter";
            bool summary=e.Type=="Clash"||e.Type=="ClashWin",shatter=e.Type=="PartBreak"||e.Type=="Rift",resonance=e.Type=="Resonance"||e.Type=="Lantern";
            Color color=heal?Teal:guard?Hex("A7D4E9"):actor!=null?ElementColor(actor.Element):Gold;
            Vector2 source=BattlePoint(e.ActorId,new Vector2(960,480)),destination=BattlePoint(e.TargetId,new Vector2(1155,510));
            Vector2 point=e.Type=="Ignite"?source:summary?Vector2.Lerp(source,destination,.5f):destination;
            var fx=MakeRect(layer,"FX "+e.Type,0,0,1920,1080);
            Text number=null;ThreadGraphic support=null;RingGraphic ring=null;
            RawImage targetImage=null;fighters.TryGetValue(e.TargetId??"",out targetImage);
            Color initialColor=targetImage?targetImage.color:Color.white;
            float duration=autoMode?.30f:.46f;
            bool applied=false;
            Action impact=()=>{if(applied||version!=screenVersion)return;applied=true;if((damage||heal)&&visibleHealth.ContainsKey(e.TargetId))RefreshHealth(e.TargetId,visibleHealth[e.TargetId]+(heal?e.Amount:-e.Amount));if(damage)PlayCombatSound(HitSoundFamily(actor,e.SkillNumber,e.Amount),true);};
            if(e.Type=="Ignite"){
                number=Label(fx,"Ignition",e.Amount>100?"점 화":e.Amount<100?"빗나감":"양 호",point.x-140,point.y-170,280,65,29,e.Amount>100?Gold:Muted,TextAnchor.MiddleCenter);
                ring=Ring(fx,"Ignite halo",point.x-48,point.y-48,96,color,2);duration=autoMode?.12f:.20f;
            }
            else if(heal||guard){
                if(heal){support=Thread(fx,"Healing light",source,source,color,false,3);support.Glow=true;}
                ring=Ring(fx,guard?"Guard seal":"Healing seal",point.x-58,point.y-58,116,color,3);
                string defenseText=e.Type=="Evade"?"회 피":e.Type=="EvadeFail"?"회피 실패":e.Type=="Counter"?"반 격":actor!=null&&actor.Skills.Any(s=>s.Kind==SkillKind.Evade)?"회피 준비":actor!=null&&actor.Skills.Any(s=>s.Kind==SkillKind.Counter)?"반격 준비":"호 위";
                number=Label(fx,"Battle feedback",heal?"+"+e.Amount:defenseText,point.x-160,point.y-132,320,85,39,color,TextAnchor.MiddleCenter);
            }
            else if(e.Type=="Intercept"){
                point=source;color=Teal;duration=autoMode?.24f:.42f;
                ring=Ring(fx,"Interception seal",point.x-48,point.y-48,96,color,3);
                string intercepted=e.OriginalTargetId=="all"?"전체 공격에 맞불\n"+(actor?.Name??""):(battle.Unit(e.OriginalTargetId)?.Name??"동료")+" 대신\n"+(actor?.Name??"")+" · 가로막기";
                number=Label(fx,"Interception feedback",intercepted,point.x-180,point.y-158,360,92,28,color,TextAnchor.MiddleCenter);
            }
            else if(summary){number=Label(fx,"Clash result",e.Type=="ClashWin"?"2연승 · 공격권 확보":"맞불 종료",point.x-270,point.y-160,540,85,34,Gold,TextAnchor.MiddleCenter);duration=autoMode?.20f:.30f;}
            else if(shatter||resonance){
                if(e.Type=="PartBreak")ShowPartBroken(e.TargetId);
                if(e.Type=="Rift"){var rift=screen.Find("Enemy rift "+e.TargetId);if(rift)rift.GetComponent<Text>().text="균열 · 행동 중단";}
                if(e.Type=="Lantern")point=new Vector2(960,480);
                ring=Ring(fx,"Expanding seal",point.x-42,point.y-42,84,color,3);
                number=Label(fx,"Break feedback",e.Type=="PartBreak"?"부위 파괴":e.Type=="Rift"?"균 열":e.Type=="Lantern"?"등불 해방":"공 명",point.x-220,point.y-150,440,95,44,color,TextAnchor.MiddleCenter);duration=autoMode?.37f:.56f;
            }
            else if(e.Type=="DefeatUnit")number=Label(fx,"Fallen",target!=null&&target.IsEnemy?(target.IsHuman?"제 압":"어둠 소멸"):"전투불능",point.x-190,point.y-132,380,70,31,Muted,TextAnchor.MiddleCenter);
            else if(damage)number=Label(fx,"Battle feedback","−"+e.Amount,point.x-170,point.y-134,340,90,52,color,TextAnchor.MiddleCenter);
            else{if(e.Type=="Cancel")FadeIntent(e.TargetId);duration=autoMode?.07f:.14f;}
            if(shatter)PlayCombatSound("break");
            else if(guard)PlayCombatSound(e.Type=="Evade"?"swing":"brace",false,.6f);
            else if(heal||resonance)PlayCombatSound("ignite",false,.45f);
            status.transform.SetAsLastSibling();
            try {
                for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime){
                    if(version!=screenVersion||!fx)yield break;
                    float p=Mathf.Clamp01(elapsed/duration),after=Mathf.Clamp01((p-.38f)/.62f);
                    if(p>=.38f)impact();
                    if(support){support.To=ToMesh(reducedMotion?destination:Vector2.Lerp(source,destination,Mathf.Clamp01(p/.38f)));support.color=new Color(color.r,color.g,color.b,1-after);support.SetVerticesDirty();}
                    if(ring){float diameter=(guard?116:84)+(reducedMotion?0:after*(resonance?200:shatter?150:55));ring.rectTransform.sizeDelta=new Vector2(diameter,diameter);ring.rectTransform.anchoredPosition=ToMesh(point-new Vector2(diameter/2,diameter/2));ring.color=new Color(color.r,color.g,color.b,1-after);ring.SetVerticesDirty();}
                    if(number){var tint=number.color;tint.a=(heal||damage)&&!applied?0:1-after*.7f;number.color=tint;}
                    if(e.Type=="DefeatUnit"&&targetImage)targetImage.color=Color.Lerp(initialColor,new Color(.33f,.35f,.41f,.45f),p);
                    yield return null;
                }
                if(version==screenVersion&&fx)impact();
            }
            finally {if(fx){fx.gameObject.SetActive(false);Destroy(fx.gameObject);}}
        }

        string HitSoundFamily(UnitState actor,int skillNumber,int amount)
        {
            if(actor==null)return "hit_blunt";
            if(actor.Id=="sera"||actor.Id=="noa")return "hit_cut";
            if(actor.Id=="minato")return "hit_blunt";
            return (actor.Id=="lian"||actor.Id=="heidel"||actor.IsEnemy)&&(skillNumber>=2||amount>=18)?"hit_heavy":"hit_blunt";
        }
        bool PlayCombatSound(string family,bool contact=false,float level=1)
        {return combatAudio!=null&&combatAudio.Play(family,sound,contact,level);}
    }
}
