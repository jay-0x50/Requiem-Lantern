using System;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public sealed class IntentHover : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Action<PointerEventData> Enter, Move;
        public Action Exit;
        public void OnPointerEnter(PointerEventData e){Enter?.Invoke(e);}
        public void OnPointerMove(PointerEventData e){Move?.Invoke(e);}
        public void OnPointerExit(PointerEventData e){Exit?.Invoke();}
        public void OnSelect(BaseEventData e){Enter?.Invoke(null);}
        public void OnDeselect(BaseEventData e){Exit?.Invoke();}
    }

    public partial class LanternApp
    {
        RectTransform intentTooltip;
        string hoveredIntent;
        const float IntentTileWidth=134,IntentTileHeight=124,IntentTileGap=12,IntentTileTop=258;

        Rect IntentTileRect(UnitState enemy,int index,int count)
        {
            float width=count*IntentTileWidth+Mathf.Max(0,count-1)*IntentTileGap;
            float left=Mathf.Clamp(EnemyFoot(enemy).x-width/2,battle.State.Enemies.Count==1?960:40,1880-width);
            return new Rect(left+index*(IntentTileWidth+IntentTileGap),IntentTileTop,IntentTileWidth,IntentTileHeight);
        }
        string IntentKindName(IntentState intent)
        {
            switch(intent.Kind){case IntentKind.Area:return "광역";case IntentKind.Piercing:return "관통";case IntentKind.Illusion:return "허상";default:return "타격";}
        }
        string ShortIntentName(string name,int count)
        {return string.IsNullOrEmpty(name)?"공격":name.Length>count?name.Substring(0,count)+"…":name;}
        void DrawCompactEnemyIntents(UnitState enemy,Transform lineLayer)
        {
            var intents=battle.State.Intents.Where(intent=>intent.ActorId==enemy.Id).ToList();
            bool solo=battle.State.Enemies.Count==1;
            for(int i=0;i<intents.Count;i++){
                var intent=intents[i];var clash=IntentClash(intent);var defender=clash==null?null:battle.Unit(clash.ActorId);
                bool intercept=defender!=null&&intent.Kind!=IntentKind.Area&&intent.Kind!=IntentKind.Illusion&&defender.Id!=intent.TargetId;
                Rect rect=IntentTileRect(enemy,i,intents.Count);Vector2 center=rect.center;anchors[intent.Id]=center;
                var card=Button(screen,"Intent "+intent.Id,"",rect.x,rect.y,rect.width,rect.height,()=>Target(intent.Id),clash!=null);
                card.interactable=enemy.Alive&&!intent.Cancelled&&!intent.Resolved;card.gameObject.AddComponent<LinkTarget>().Id=intent.Id;
                var flame=MakeRect(card.transform,"Intent darkness",9,8,24,34).gameObject.AddComponent<FlameGraphic>();
                flame.Lit=true;flame.color=Violet;flame.raycastTarget=false;
                Label(card.transform,"Intent darkness count",BattleRules.Flames(intent).ToString(),36,10,22,28,20,Violet,TextAnchor.MiddleCenter);
                Label(card.transform,"Intent type",IntentKindName(intent),62,11,63,26,16,Ivory,TextAnchor.MiddleCenter);
                Label(card.transform,"Intent name",EnemyNumber(enemy)+ShortIntentName(intent.Name,solo?6:4),8,44,118,23,16,Violet,TextAnchor.MiddleCenter);
                Label(card.transform,"Intent original target","원래 "+IntentOriginalTarget(intent),7,73,120,21,15,Ivory,TextAnchor.MiddleCenter);
                string recipient=clash!=null&&intent.Kind==IntentKind.Illusion?"허상 연결 · "+defender.Name:intercept?"가로막기 · "+defender.Name:clash!=null?"맞불 · "+defender.Name:"미배정";
                Label(card.transform,"Intent target",recipient,7,97,120,21,14,clash!=null?Teal:Muted,TextAnchor.MiddleCenter);
                var hover=card.gameObject.AddComponent<IntentHover>();
                hover.Enter=e=>ShowIntentTooltip(intent.Id,e==null?center:Pointer(e));
                hover.Move=e=>{if(hoveredIntent==intent.Id)PositionIntentTooltip(Pointer(e));};
                hover.Exit=()=>HideIntentTooltip(intent.Id);
                var source=new Vector2(center.x,rect.yMax-4);
                Thread(lineLayer,"Enemy intent "+intent.Id,anchors[enemy.Id],source,new Color(.66f,.49f,.82f,.16f),true,1);
                if(intent.Kind==IntentKind.Area){
                    foreach(var ally in battle.State.Allies.Where(ally=>ally.Alive))IntentRoute(lineLayer,"Area intent "+intent.Id,source,anchors[ally.Id],new Color(.66f,.49f,.82f,.18f),true,1);
                    if(defender!=null)IntentRoute(lineLayer,"Intent thread "+intent.Id,source,anchors[defender.Id],Teal,false,3);
                }else{
                    string recipientId=defender?.Id??intent.TargetId;
                    if(intercept&&anchors.TryGetValue(intent.TargetId,out var original))IntentRoute(lineLayer,"Original intent "+intent.Id,source,original,new Color(.66f,.49f,.82f,.13f),true,1);
                    if(anchors.TryGetValue(recipientId,out var target))IntentRoute(lineLayer,"Intent thread "+intent.Id,source,target,clash==null?new Color(Violet.r,Violet.g,Violet.b,.25f):Teal,clash==null,clash==null?1:3);
                }
            }
        }
        void HideIntentTooltip(string id=null)
        {
            if(id!=null&&hoveredIntent!=id)return;
            if(intentTooltip){intentTooltip.gameObject.SetActive(false);Destroy(intentTooltip.gameObject);}
            intentTooltip=null;hoveredIntent=null;
        }
        void ShowIntentTooltip(string id,Vector2 point)
        {
            HideIntentTooltip();
            if(page!="battle"||overlay||resolving||dragVisuals||battle==null)return;
            var intent=battle.Intent(id);var enemy=intent==null?null:battle.Unit(intent.ActorId);
            if(intent==null||enemy==null||!enemy.Alive||intent.Cancelled||intent.Resolved)return;
            hoveredIntent=id;intentTooltip=MakeRect(screen,"Intent tooltip",0,0,536,430);
            var group=intentTooltip.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            var background=Box(intentTooltip,"Tooltip backing",0,0,536,430,new Color(.018f,.030f,.049f,.98f));
            Box(intentTooltip,"Tooltip accent",0,0,536,2,Violet);
            Label(intentTooltip,"Tooltip title",EnemyNumber(enemy)+enemy.Name+" / "+intent.Name,22,12,492,63,24,Violet);
            var text=Label(intentTooltip,"Tooltip details",IntentTooltipDetails(intent,enemy),22,87,492,600,18,Ivory);
            float bodyHeight=Mathf.Ceil(text.preferredHeight),height=Mathf.Min(914,bodyHeight+109);
            text.rectTransform.sizeDelta=new Vector2(492,bodyHeight);intentTooltip.sizeDelta=new Vector2(536,height);
            background.rectTransform.sizeDelta=intentTooltip.sizeDelta;
            PositionIntentTooltip(point);intentTooltip.SetAsLastSibling();
        }
        void PositionIntentTooltip(Vector2 point)
        {
            if(!intentTooltip)return;
            float width=intentTooltip.sizeDelta.x,height=intentTooltip.sizeDelta.y;
            float x=point.x>=960?point.x-width-20:point.x+20;
            intentTooltip.anchoredPosition=ToMesh(new Vector2(Mathf.Clamp(x,20,1900-width),Mathf.Clamp(point.y+18,126,1060-height)));
        }
        string IntentTooltipDetails(IntentState intent,UnitState enemy)
        {
            var part=enemy.Parts.FirstOrDefault(value=>value.Id==intent.PartId);var clash=IntentClash(intent);
            int baseline=BattleRules.Base(intent),flame=BattleRules.Flame(intent),count=BattleRules.Flames(intent);
            string special=intent.Kind==IntentKind.Area?"생존 아군 전체 공격. 맞불 패배 시 광역 범위를 유지합니다.":intent.Kind==IntentKind.Piercing?
                "일반 수비의 피해 감소·회피·반격을 적용하지 않습니다.":intent.Kind==IntentKind.Illusion?
                "미연결 시 피해 없음. 연결하면 행동을 소모하고 피해 없이 끝납니다.":"한 명 공격. 맞불하지 않으면 수비 행동으로 대응할 수 있습니다.";
            string assigned=clash==null?"없음":battle.Unit(clash.ActorId).Name+" · "+(intent.Kind==IntentKind.Illusion?"허상 연결":intent.Kind==IntentKind.Area?"전체 공격 맞불":clash.ActorId==intent.TargetId?"맞불":"동료 대신 가로막기");
            string preview=intent.Kind==IntentKind.Illusion?"\n허상: 맞불 연결 시 행동만 소모하고 피해 없이 종료":clash==null?"":"\n배정 상성  "+battle.Preview(clash).Summary;
            var selected=battle.Unit(selectedActor);var skill=battle.Skill(selectedActor,selectedSkill);
            if(selected!=null&&selected.Alive&&skill!=null&&skill.Kind==SkillKind.Attack){
                var candidate=new LinkCommand{ActorId=selected.Id,SkillId=skill.Id,CardId=selectedCard,Slot=selectedSlot,TargetId=intent.Id,Kind=LinkKind.Clash};
                preview+="\n선택 후보  "+selected.Name+" · "+skill.Name;
                if(intent.Kind!=IntentKind.Illusion)preview+="\n후보 상성  "+battle.Preview(candidate).Summary;
            }else if(intent.Kind!=IntentKind.Illusion)preview+="\n공격 후보를 선택하면 맞불 상성을 확인할 수 있습니다.";
            return $"기본 위력(상성·어둠도 적용 전)\n{baseline}~{baseline+flame*count}  =  기본 {baseline} + 어둠 {flame} × {count}\n"+
                $"어둠 {count}개 · 정신력 {enemy.Sanity:+0;-0;0} · 점등 {BattleRules.FlameChance(enemy.Sanity):P0}\n"+
                $"기본 속도 {intent.Speed} · {BattleRules.ElementName(intent.Element)} · {IntentKindName(intent)}\n원래 표적  {IntentOriginalTarget(intent)}\n"+
                "사용 부위  "+(part==null?"본체":part.Name+" · "+part.Hp+"/"+part.MaxHp+" · 파괴 시 예고 취소")+"\n특수  "+special+
                "\n현재 배정  "+assigned+preview;
        }
    }
}
