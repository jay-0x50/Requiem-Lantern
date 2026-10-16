using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public sealed class SkillFlavorMarker : MonoBehaviour
    {
        public string ActorId,AnimationKey,Phase;
        public int SkillNumber,QuoteCount,VisualContacts,HealthChanges;
        public bool Awakened,ReducedMotion;
    }
    public sealed class SkillFlavorHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public Action Enter,Leave;
        public void OnPointerEnter(PointerEventData e){Enter?.Invoke();}
        public void OnPointerExit(PointerEventData e){Leave?.Invoke();}
        void OnDisable(){Leave?.Invoke();}
    }
    public partial class LanternApp
    {
        RectTransform skillFlavorTooltip;
        int flavorActionSerial,lastSupportMotionSerial=-1;
        readonly Dictionary<string,int> visibleAwakening=new Dictionary<string,int>(),visibleBarrier=new Dictionary<string,int>(),visibleSupportPower=new Dictionary<string,int>();

        void HideSkillFlavorTooltip(){if(skillFlavorTooltip){skillFlavorTooltip.gameObject.SetActive(false);Destroy(skillFlavorTooltip.gameObject);}skillFlavorTooltip=null;}
        void AddSkillFlavorHover(GameObject node,string actor,string skill)
        {
            var hover=node.AddComponent<SkillFlavorHover>();hover.Enter=()=>ShowSkillFlavorTooltip(actor,skill);hover.Leave=HideSkillFlavorTooltip;
        }
        void ShowSkillFlavorTooltip(string actorId,string skillId)
        {
            if(page!="battle"||overlay||resolving||dragVisuals)return;HideSkillFlavorTooltip();
            var actor=battle.Unit(actorId);var raw=battle.Skill(actorId,skillId);var skill=battle.PreviewSkill(actorId,skillId,raw!=null&&raw.Kind==SkillKind.Heal?LinkKind.Support:LinkKind.Strike);if(actor==null||skill==null)return;
            skillFlavorTooltip=MakeRect(stage,"Skill flavor tooltip",1320,300,540,440);
            var group=skillFlavorTooltip.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            Box(skillFlavorTooltip,"Paper",0,0,540,440,new Color(.018f,.030f,.049f,.99f));
            Box(skillFlavorTooltip,"Edge",0,0,540,2,ElementColor(skill.Element));
            var skillTitle=Label(skillFlavorTooltip,"Name",actor.Name+" · "+skill.Name,20,15,500,42,24,Gold);skillTitle.resizeTextForBestFit=true;skillTitle.resizeTextMinSize=17;skillTitle.resizeTextMaxSize=24;
            string detail="“"+skill.Quote+"”\n"+(skill.Number>0?skill.Number+"스킬":"방어 행동")+" · 행동 1칸\n";
            detail+=skill.CanSupportAlly?"적에게 공격 / 아군에게 지원\n"+skill.SupportDescription:skill.Kind==SkillKind.Heal?"아군에게 치유\n"+skill.SupportDescription:"적 예고에 맞불 / 적 본체·부위에 공격";
            if(skill.Number==3&&actor.AwakeningMaxStacks>0)detail+="\n\n"+actor.AwakeningName+" "+actor.AwakeningStacks+"/"+actor.AwakeningMaxStacks+"\n"+actor.AwakeningDescription+"\n3스택: 각성 S3 실행 시 소모 · 기본 +4 / 불 위력 +1";
            if(skill.Number==3&&actor.SyncLevel<1)detail+="\n3스킬 사용에는 먼저 동기화가 필요합니다.";
            Label(skillFlavorTooltip,"Description",detail,22,67,496,355,20,Ivory);
        }
        void DrawUnitTraits(UnitState unit,float footX)
        {
            visibleAwakening[unit.Id]=unit.AwakeningStacks;visibleBarrier[unit.Id]=unit.Barrier;visibleSupportPower[unit.Id]=unit.SupportPower;
            var root=MakeRect(screen,"Traits "+unit.Id,footX-96,650,192,49);
            if(unit.AwakeningMaxStacks>0){
                Box(root,"Backing",0,0,192,49,new Color(.02f,.034f,.052f,.87f));
                Label(root,"Awakening text","",4,0,184,24,14,Gold,TextAnchor.MiddleCenter);
                for(int i=0;i<3;i++)Box(root,"Stack "+i,9+i*61,25,52,4,Muted);
            }
            Label(root,"Support status","",-4,31,200,18,12,Teal,TextAnchor.MiddleCenter);
            RefreshUnitTraits(unit.Id);
        }
        void RefreshUnitTraits(string id)
        {
            var unit=battle.Unit(id);var root=screen?screen.Find("Traits "+id):null;if(unit==null||!root)return;
            int stacks=visibleAwakening.TryGetValue(id,out var count)?count:unit.AwakeningStacks;
            var title=root.Find("Awakening text");if(title)title.GetComponent<Text>().text="각성 "+stacks+"/"+unit.AwakeningMaxStacks+(stacks>=unit.AwakeningMaxStacks?unit.SyncLevel>0?" · READY":" · 동기화 필요":"");
            for(int i=0;i<3;i++){var segment=root.Find("Stack "+i);if(segment)segment.GetComponent<Image>().color=i<stacks?Gold:new Color(.19f,.23f,.28f,1);}
            var status=root.Find("Support status");int shield=visibleBarrier.TryGetValue(id,out var barrier)?barrier:0,power=visibleSupportPower.TryGetValue(id,out var bonus)?bonus:0;
            if(status)status.GetComponent<Text>().text=(shield>0?"보호막 "+shield:"")+(shield>0&&power>0?" · ":"")+(power>0?"다음 공격 +"+power:"");
        }
        void ApplyFlavorEvent(BattleEvent e)
        {
            if(e.Type=="AwakeningCharge"||e.Type=="AwakeningReady"||e.Type=="AwakeningUsed"){
                visibleAwakening[e.ActorId]=e.AwakeningStacks;RefreshUnitTraits(e.ActorId);
            }
            if(e.Type=="SupportApplied"||e.Type=="ShieldAbsorb"||e.Type=="Damage"){
                visibleBarrier[e.TargetId]=e.Barrier;visibleSupportPower[e.TargetId]=e.SupportPower;RefreshUnitTraits(e.TargetId);
            }
            if(e.Type=="SkillStart"){
                visibleBarrier[e.ActorId]=e.Barrier;visibleSupportPower[e.ActorId]=e.SupportPower;RefreshUnitTraits(e.ActorId);
            }
        }
        IEnumerator PresentSkillIntroduction(BattleEvent e,RectTransform layer,int version)
        {
            flavorActionSerial++;lastSupportMotionSerial=-1;HideSkillFlavorTooltip();
            if(activeFlameRoll){activeFlameRoll.gameObject.SetActive(false);Destroy(activeFlameRoll.gameObject);activeFlameRoll=null;}
            if(string.IsNullOrEmpty(e.Quote))yield break;
            var unit=battle.Unit(e.ActorId);if(unit==null)yield break;
            bool third=e.SkillNumber==3;float duration=(third?(e.Awakened?1.1f:.8f):.4f)*(autoMode?.72f:1);
            if(reducedMotion&&third)duration=autoMode?.33f:.46f;
            var fx=MakeRect(layer,third?"S3 cut-in":"Skill quote",third?45:64,third?240:345,third?852:620,third?214:92);
            var marker=fx.gameObject.AddComponent<SkillFlavorMarker>();marker.ActorId=e.ActorId;marker.AnimationKey=e.AnimationKey;marker.SkillNumber=e.SkillNumber;marker.Awakened=e.Awakened;marker.ReducedMotion=reducedMotion;marker.QuoteCount=1;marker.Phase=third?"cut-in":"quote";
            var group=fx.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            var color=ElementColor(unit.Element);
            Box(fx,"Backing",0,0,third?852:620,third?214:92,new Color(.014f,.024f,.041f,.97f));Box(fx,"Edge",0,0,third?852:620,2,e.Awakened?Gold:color);
            if(third){
                var mask=MakeRect(fx,"Portrait crop",0,2,252,210);mask.gameObject.AddComponent<RectMask2D>();Character(mask,CombatArt(unit.Id),-14,-5,283,505,false);
                Label(fx,"Skill heading",e.Awakened?"A W A K E N E D   /   각성":"S K I L L   0 3",276,13,548,31,18,e.Awakened?Gold:color);
                var title=Label(fx,"Skill name",e.SkillName,270,56,550,59,36,Ivory);title.resizeTextForBestFit=true;title.resizeTextMinSize=20;title.resizeTextMaxSize=36;
                Label(fx,"Quote","“"+e.Quote+"”",276,127,545,66,24,color);
                for(int i=0;i<3;i++){var flame=MakeRect(fx,"Cut-in flame "+i,781-i*24,14,18,24).gameObject.AddComponent<FlameGraphic>();flame.Lit=true;flame.color=e.Awakened?Gold:color;flame.raycastTarget=false;}
            }else{
                Label(fx,"Quote owner",unit.Name+" · "+e.SkillName,18,6,580,29,17,color);
                Label(fx,"Quote","“"+e.Quote+"”",19,36,580,42,25,Ivory);
            }
            Vector2 origin=fx.anchoredPosition;
            try{for(float t=0;t<duration;t+=Mathf.Min(.05f,Time.unscaledDeltaTime)){
                if(version!=screenVersion||!fx)yield break;float p=t/duration;
                group.alpha=Mathf.Min(Mathf.Clamp01(p/.12f),Mathf.Clamp01((1-p)/.16f));
                if(!reducedMotion&&third)fx.anchoredPosition=origin+Vector2.right*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.28f)))*-64;
                yield return null;
            }}finally{if(fx){fx.gameObject.SetActive(false);Destroy(fx.gameObject);}}
        }
        IEnumerator PresentS3Prelude(BattleEvent e,CombatPosePlayer actor,CombatPosePlayer target,RectTransform fx,CombatPhaseMarker combat,Vector2 actorMeet,Vector2 targetMeet,Vector2 contact,int version)
        {
            var marker=fx.gameObject.AddComponent<SkillFlavorMarker>();marker.ActorId=e.ActorId;marker.AnimationKey=e.AnimationKey;marker.SkillNumber=3;marker.Awakened=e.Awakened;marker.ReducedMotion=reducedMotion;marker.Phase="special-windup";
            float duration=(reducedMotion?.10f:e.ActorId=="sera"||e.ActorId=="noa"?.42f:.36f)*(autoMode?.72f:1);
            Color color=ElementColor(battle.Unit(e.ActorId).Element);
            var arcs=new List<WeaponArcGraphic>();for(int i=0;i<2;i++){
                var arc=MakeRect(fx,"Special weapon trail "+i,0,0,1920,1080).gameObject.AddComponent<WeaponArcGraphic>();arc.Center=ToMesh(contact)+new Vector2(i==0?-14:14,i==0?17:-17);arc.Facing=i==0?1:-1;arc.Radius=e.ActorId=="mare"?108:86;arc.Thickness=e.Awakened?13:8;arc.color=Color.clear;arc.raycastTarget=false;arcs.Add(arc);
                if(e.ActorId=="heidel"){arc.Center=ToMesh(actor.OriginalFoot-new Vector2(-65,BodyHeight(battle.Unit(e.ActorId))*.5f));arc.Radius=32;arc.Thickness=4;}
            }
            var burst=Ring(fx,"Special preparation seal",contact.x-35,contact.y-35,70,Color.clear,4);
            var echoes=new List<RawImage>();
            if(e.ActorId=="noa"&&!reducedMotion)for(int i=0;i<2;i++){
                var ghost=MakeRect(fx,"Noa shadow echo "+i,0,0,1,1).gameObject.AddComponent<RawImage>();ghost.texture=actor.Image.texture;ghost.raycastTarget=false;ghost.transform.SetSiblingIndex(2);echoes.Add(ghost);
            }
            int beats=0;
            for(float t=0;t<duration;t+=Mathf.Min(.05f,Time.unscaledDeltaTime)){
                if(version!=screenVersion||!fx)yield break;float p=t/duration,arrive=Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.36f));
                Vector2 a=Vector2.Lerp(actor.OriginalFoot,actorMeet,arrive),b=Vector2.Lerp(target.OriginalFoot,targetMeet,arrive);
                actor.SetPose(CombatPosePlayer.Windup);target.SetPose(CombatPosePlayer.Idle);
                if(!reducedMotion){
                    float beat=Mathf.Sin(p*Mathf.PI*4);
                    switch(e.ActorId){
                        case "lian":a.x-=Mathf.Sin(p*Mathf.PI)*36;actor.Lean(-8*arrive);break;
                        case "sera":case "noa":actor.SetPose(p<.44f?CombatPosePlayer.Swing:CombatPosePlayer.FollowThrough);a.x+=beat*(e.ActorId=="noa"?38:25);a.y-=Mathf.Abs(beat)*12;target.SetPose(CombatPosePlayer.Hit);b.x+=Mathf.Abs(beat)*7;break;
                        case "heidel":actor.SetPose(p<.45f?CombatPosePlayer.Windup:CombatPosePlayer.FollowThrough);a.x-=Mathf.Abs(beat)*19;break;
                        case "kai":actor.SetPose(CombatPosePlayer.Swing);a.y-=Mathf.Sin(p*Mathf.PI)*14;break;
                        case "mare":actor.SetPose(p<.55f?CombatPosePlayer.Swing:CombatPosePlayer.Windup);b.x-=Mathf.Sin(p*Mathf.PI)*33;a.x-=Mathf.Sin(p*Mathf.PI)*22;break;
                    }
                    if((e.ActorId=="sera"||e.ActorId=="noa"||e.ActorId=="heidel")&&p>(beats==0?.35f:.72f)&&beats<2){beats++;marker.VisualContacts++;PlayCombatSound(e.ActorId=="heidel"?"swing":"clash",false,.48f);}
                }
                actor.PlaceFoot(a);target.PlaceFoot(b);combat.Phase="special-windup";combat.ActorFoot=a;combat.DefenderFoot=b;
                for(int i=0;i<echoes.Count;i++){
                    var ghost=echoes[i];ghost.uvRect=actor.Image.uvRect;ghost.rectTransform.pivot=actor.Image.rectTransform.pivot;ghost.rectTransform.sizeDelta=actor.Image.rectTransform.sizeDelta;ghost.rectTransform.localScale=actor.Image.rectTransform.localScale;ghost.rectTransform.anchoredPosition=actor.Image.rectTransform.anchoredPosition+new Vector2((i==0?-1:1)*(23+Mathf.Sin(p*Mathf.PI)*25),i*5);ghost.color=new Color(.56f,.42f,.82f,Mathf.Sin(p*Mathf.PI)*.32f);
                }
                for(int i=0;i<arcs.Count;i++){arcs[i].Progress=Mathf.Repeat(p*2+i*.5f,1);arcs[i].color=new Color(color.r,color.g,color.b,reducedMotion?0:Mathf.Sin(p*Mathf.PI)*.75f);arcs[i].SetVerticesDirty();}
                burst.color=new Color(color.r,color.g,color.b,reducedMotion?0:Mathf.Sin(p*Mathf.PI)*.5f);burst.SetVerticesDirty();yield return null;
            }
            if(version==screenVersion){actor.PlaceFoot(actorMeet);target.PlaceFoot(targetMeet);}
            foreach(var arc in arcs)if(arc)Destroy(arc.gameObject);if(burst)Destroy(burst.gameObject);
            foreach(var echo in echoes)if(echo)Destroy(echo.gameObject);
            marker.Phase="finishing-strike";
        }
        IEnumerator PresentSupportGesture(BattleEvent e,RectTransform layer,int version)
        {
            var owner=battle.Unit(e.ActorId);var recipient=battle.Unit(e.TargetId);if(owner==null||recipient==null)yield break;
            if(e.SkillNumber==3&&lastSupportMotionSerial==flavorActionSerial){ApplyFlavorEvent(e);yield break;}
            if(e.SkillNumber==3)lastSupportMotionSerial=flavorActionSerial;
            var fx=MakeRect(layer,"Support gesture",0,0,1920,1080);var marker=fx.gameObject.AddComponent<SkillFlavorMarker>();marker.ActorId=e.ActorId;marker.AnimationKey=e.AnimationKey;marker.SkillNumber=e.SkillNumber;marker.Awakened=e.Awakened;marker.ReducedMotion=reducedMotion;marker.Phase="support-windup";
            CombatPosePlayer performer=null;var point=BattlePoint(e.TargetId,new Vector2(600,600));Color color=ElementColor(owner.Element);
            if(fighters.TryGetValue(e.ActorId,out var image)&&image)performer=new CombatPosePlayer(fx,image,CombatArt(e.ActorId),true,BodyHeight(owner),"Support performer");
            var seal=Ring(fx,"Ally support seal",point.x-74,point.y-74,148,color,e.Awakened?6:3);
            var motifs=new List<Graphic>();
            if(e.ActorId=="minato")for(int i=0;i<3;i++)motifs.Add(Ring(fx,"Healing tide "+i,point.x-65-i*23,point.y-30-i*9,130+i*46,color,2));
            else {
                Vector2[] vertices=e.ActorId=="lian"?new[]{new Vector2(-53,-66),new Vector2(53,-66),new Vector2(45,15),new Vector2(0,55),new Vector2(-45,15)}:new[]{new Vector2(0,-76),new Vector2(65,0),new Vector2(0,62),new Vector2(-65,0)};
                for(int i=0;i<vertices.Length;i++)motifs.Add(Thread(fx,"Support sigil "+i,point+vertices[i],point+vertices[(i+1)%vertices.Length],color,false,e.Awakened?6:3));
            }
            var number=Label(fx,"Support value",e.Type=="Heal"?"+"+e.Amount:e.Barrier>0?"보호막 "+e.Barrier:e.SupportPower>0?"다음 공격 +"+e.SupportPower:"지원",point.x-165,point.y-155,330,75,32,Color.clear,TextAnchor.MiddleCenter);
            float duration=(e.SkillNumber==3?.65f:.4f)*(autoMode?.72f:1);bool applied=false;
            try{for(float t=0;t<duration;t+=Mathf.Min(.05f,Time.unscaledDeltaTime)){
                if(version!=screenVersion||!fx)yield break;float p=t/duration;
                if(performer!=null){performer.SetPose(p<.52f?CombatPosePlayer.Windup:CombatPosePlayer.FollowThrough);var foot=performer.OriginalFoot;if(!reducedMotion)foot.y-=Mathf.Sin(p*Mathf.PI)*8;performer.PlaceFoot(foot);}
                if(p>=.52f&&!applied){applied=true;marker.Phase="support-contact";marker.VisualContacts=1;if(e.Type=="Heal"&&visibleHealth.ContainsKey(e.TargetId)){RefreshHealth(e.TargetId,Mathf.Min(recipient.MaxHp,visibleHealth[e.TargetId]+e.Amount));marker.HealthChanges++;}ApplyFlavorEvent(e);PlayCombatSound("brace",false,.55f);}
                float alpha=applied?Mathf.Clamp01((1-p)/.48f):.3f;seal.color=new Color(color.r,color.g,color.b,alpha);seal.SetVerticesDirty();foreach(var motif in motifs)if(motif){motif.color=new Color(color.r,color.g,color.b,alpha*.8f);motif.SetVerticesDirty();}number.color=new Color(color.r,color.g,color.b,applied?alpha:0);yield return null;
            }}finally{performer?.Dispose();if(fx){fx.gameObject.SetActive(false);Destroy(fx.gameObject);}}
        }
    }
}
