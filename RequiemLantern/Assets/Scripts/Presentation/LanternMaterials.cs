using System;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        readonly MaterialDungeonSession materialDungeon = new MaterialDungeonSession();
        string selectedMaterialCharacter="lian";
        bool materialBusy;

        void ExitMaterialDungeon(){materialDungeon.Exit();materialBusy=false;}
        void ShowMaterialDungeons(string characterId=null)
        {
            if(page=="battle"||page=="resolve"){Notify("전투를 마친 뒤 기억 잠항을 열어 주세요.");return;}
            if(characterId!=null&&MaterialCatalog.Get(characterId)!=null)selectedMaterialCharacter=characterId;
            campaign.Exit();ExitWeeklyDungeon();ExitMaterialDungeon();
            var snapshot=progress.GetSynchronizationMaterials();
            var selected=snapshot.Entries.FirstOrDefault(value=>value.CharacterId==selectedMaterialCharacter)??snapshot.Entries[0];
            selectedMaterialCharacter=selected.CharacterId;
            Clear("materialDungeon");Header("MEMORY DIVE / 귀항호 재료 채집","기억 잠항",ShowHub);
            var sea=Texture("VoyageSea");if(sea)screen.Find("The eternal night").GetComponent<RawImage>().texture=sea;
            Box(screen,"Material selection backing",76,154,510,766,new Color(.020f,.036f,.053f,.97f));
            Label(screen,"Material selection heading","공명하는 기억을 선택",105,175,450,48,29,Gold);
            Label(screen,"Material selection help","미보유 잔영의 재료도 미리 모을 수 있습니다.",107,229,450,54,19,Muted);
            for(int i=0;i<snapshot.Entries.Count;i++){
                var entry=snapshot.Entries[i];float y=294+i*84;
                var option=Button(screen,"Select material "+entry.CharacterId,"",101,y,460,72,()=>ShowMaterialDungeons(entry.CharacterId),entry.CharacterId==selected.CharacterId);
                Label(option.transform,"Character",CampaignCharacterName(entry.CharacterId)+(entry.Owned?" · 보유":" · 미보유"),18,7,420,29,23,entry.Owned?Ivory:Muted);
                Label(option.transform,"Material count",entry.MaterialName+"  "+entry.Count.ToString("N0")+" / "+entry.RequiredCount,19,40,420,25,17,Gold);
            }
            Character(screen,selected.Portrait,590,160,555,766);
            Box(screen,"Material detail backing",1172,154,661,766,new Color(.018f,.034f,.052f,.97f));
            Label(screen,"Material owner",CampaignCharacterName(selected.CharacterId)+"의 전용 재료",1205,178,590,42,23,Gold);
            Label(screen,"Material name",selected.MaterialName,1200,237,600,72,39,Ivory);
            Label(screen,"Material inventory","보유  "+selected.Count.ToString("N0")+"개   /   동기화 필요  "+selected.RequiredCount+"개",1207,331,584,48,25,Gold);
            Label(screen,"Material description",selected.Description,1208,411,582,119,25,Muted);
            Box(screen,"Material detail rule",1207,556,583,1,new Color(Gold.r,Gold.g,Gold.b,.42f));
            Label(screen,"Material sortie rules","일반전 · 적 2명 · 출격 편성 4명\n승리 수령 시 "+selected.MaterialName+" +"+selected.DropCount+"개\n입장 재화 소모 없음 · 횟수 제한 없음",1207,582,583,126,23,Ivory);
            Label(screen,"Material sync help",selected.Synchronized?"이미 동기화한 잔영입니다. 모은 재료는 보관됩니다.":"동기화에는 전용 재료와 등불 조각 10개가 필요합니다.",1207,727,583,62,20,Muted);
            var enter=Button(screen,"Enter material dungeon",selected.MaterialName+" 채집 출항 →",1207,817,584,73,()=>StartMaterialDungeon(selected.CharacterId),true);enter.interactable=snapshot.Unlocked;
            Label(screen,"Material availability",snapshot.Unlocked?"승리 후 재료를 수령해 주세요. 본편 진행·주간 보상 횟수는 바뀌지 않으며 보급 전투 의뢰에는 집계됩니다.":snapshot.UnlockDescription,91,938,1715,43,21,Gold,TextAnchor.MiddleCenter);
            Footer("등불 조각: 보급 의뢰·잔향 회수  /  캐릭터별 전용 재료: 기억 잠항");
        }
        void StartMaterialDungeon(string id)
        {
            if(materialBusy||page!="materialDungeon")return;
            if(!materialDungeon.Enter(id,progress,out string error)){Notify(error);return;}
            campaign.Exit();ExitWeeklyDungeon();battle=materialDungeon.Battle;PrepareBattlePresentation();
        }
        void RetryMaterialDungeon()
        { materialDungeon.Restart(progress);battle=materialDungeon.Battle;PrepareBattlePresentation(); }
        void ShowMaterialDungeonResult()
        {
            bool victory=battle.State.Outcome==BattleOutcome.Victory;
            Clear("result");Character(screen,battle.State.Allies[0].Portrait,90,143,700,826);
            Label(screen,"Result eyebrow","MEMORY DIVE / 귀항호 재료 채집",862,223,970,39,21,Gold);
            Label(screen,"Result headline",victory?"손에 남은 기억":"다음 잠항을 향해",854,292,1010,120,68);
            Label(screen,"Result quote",victory?"동료의 파편과 공명하는 흔적을 회수했습니다.\n아래 버튼으로 재료를 수령하고 귀항합니다.":"회수하지 못한 재료를 다시 찾아 나설 수 있습니다.\n실패와 퇴각으로 재화나 출격 횟수는 줄지 않습니다.",864,449,930,130,29,Muted);
            if(victory)Label(screen,"Material pending reward",materialDungeon.Active.MaterialName+" +"+materialDungeon.Active.DropCount,870,625,904,88,36,Gold);
            Button(screen,"Material result collect",victory?"재료 수령하고 귀항 →":"다시 도전하기 →",865,857,560,83,()=>{if(victory)ClaimMaterialDungeonReward();else RetryMaterialDungeon();},true);
            Button(screen,"Material result repeat",victory?"수령 후 다시 잠항":"잠항 선택으로",1450,857,360,83,()=>{if(victory)ClaimMaterialDungeonReward(true);else {ExitMaterialDungeon();ShowMaterialDungeons();}});
            Footer("수령 저장이 끝난 뒤에만 잠항을 종료합니다 · 선택한 잔영의 전용 재료만 획득합니다");
        }
        void ClaimMaterialDungeonReward(bool repeat=false)
        {
            if(materialBusy||page!="result"||materialDungeon.Active==null)return;
            materialBusy=true;var snapshot=progress.Capture();string id=materialDungeon.Active.CharacterId;
            if(!materialDungeon.ClaimVictory(progress,out var reward,out string error)){materialBusy=false;Notify(error);return;}
            if(!PersistRecruitment()){
                progress.Restore(snapshot);materialBusy=false;Notify("저장하지 못해 재료 수령을 취소했습니다. 이 화면에서 다시 수령해 주세요.");return;
            }
            ExitMaterialDungeon();ShowMaterialDungeons(id);Notify("기억 회수 완료 · "+reward.MaterialName+" +"+reward.Count);
            if(repeat)StartMaterialDungeon(id);
        }
        void SynchronizeFromRoster(string id,int selected)
        {
            var snapshot=progress.Capture();
            if(!progress.Synchronize(id,out string error)){Notify(error);return;}
            if(!PersistRecruitment()){progress.Restore(snapshot);Notify("저장하지 못해 동기화를 취소했습니다. 재료는 보존했습니다.");return;}
            ShowRoster(selected);Notify("동기화 완료 · 3스킬이 열렸습니다.");
        }
    }
}
