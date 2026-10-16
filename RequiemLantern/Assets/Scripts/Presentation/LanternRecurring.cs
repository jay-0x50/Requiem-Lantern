using System;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        readonly WeeklyDungeonSession weeklyDungeon = new WeeklyDungeonSession();
        ActivitySnapshot recurringSnapshot;
        bool weeklyQuestTab, recurringBusy;
        float recurringNextTick;

        string ActivityResetLabel(DateTime utc)
        { return utc.ToUniversalTime().AddHours(9).ToString("MM월 dd일 HH:mm")+" (KST)"; }

        // Reset/check-in changes use the same transaction as a manual reward.
        // A failed local write leaves the previous saved period and rewards intact.
        bool RefreshRecurringActivities(bool showFailure=true)
        {
            var snapshot=progress.Capture();string before=JsonUtility.ToJson(snapshot);
            progress.CheckIn();var next=progress.GetActivities();
            if(before!=JsonUtility.ToJson(progress.Data)&&!PersistRecruitment()){
                progress.Restore(snapshot);recurringSnapshot=null;
                if(showFailure)Notify("보급 기록을 저장하지 못했습니다. 잠시 후 다시 열어 주세요.");
                return false;
            }
            recurringSnapshot=next;return true;
        }
        void TickRecurringActivities()
        {
            if((page!="recurring"&&page!="weeklyDungeon")||recurringBusy||Time.unscaledTime<recurringNextTick)return;
            recurringNextTick=Time.unscaledTime+1;
            if(recurringSnapshot==null)return;
            var now=DateTime.UtcNow;
            if(now<recurringSnapshot.NextDailyResetUtc&&now<recurringSnapshot.NextWeeklyResetUtc)return;
            if(!RefreshRecurringActivities()){recurringNextTick=Time.unscaledTime+10;return;}
            if(page=="recurring")ShowRecurringBoard(weeklyQuestTab);else ShowWeeklyDungeons();
        }
        void ExitWeeklyDungeon(){weeklyDungeon.Exit();}

        void ShowRecurringBoard(bool weekly=false)
        {
            if(page=="battle"||page=="resolve"){Notify("전투를 마친 뒤 보급 게시판을 열어 주세요.");return;}
            campaign.Exit();ExitWeeklyDungeon();ExitMaterialDungeon();weeklyQuestTab=weekly;recurringBusy=false;
            if(!RefreshRecurringActivities())return;
            Clear("recurring");Header("SUPPLY BOARD / 귀항호 보급 의뢰","보급 게시판",ShowHub);
            Button(screen,"Daily quests","일일 의뢰",84,146,390,64,()=>ShowRecurringBoard(false),!weekly);
            Button(screen,"Weekly quests","주간 의뢰",492,146,390,64,()=>ShowRecurringBoard(true),weekly);
            var quests=weekly?recurringSnapshot.WeeklyQuests:recurringSnapshot.DailyQuests;
            Label(screen,"Quest reset",(weekly?"매주 월요일 오전 5시":"매일 오전 5시")+" 초기화  ·  다음 "+ActivityResetLabel(weekly?recurringSnapshot.NextWeeklyResetUtc:recurringSnapshot.NextDailyResetUtc),91,233,1745,36,21,Gold);
            for(int i=0;i<quests.Count;i++){
                var quest=quests[i];float y=294+i*164;
                Box(screen,"Quest row "+quest.Id,82,y,1225,145,new Color(.028f,.046f,.068f,.97f));
                Box(screen,"Quest accent "+quest.Id,82,y,3,145,quest.Claimed?Muted:quest.CanClaim?Gold:Teal);
                Label(screen,"Quest title "+quest.Id,quest.Title,106,y+12,820,43,29,quest.Claimed?Muted:Ivory);
                Label(screen,"Quest description "+quest.Id,quest.Description,108,y+60,785,32,21,Muted);
                Label(screen,"Quest reward "+quest.Id,quest.Reward.Summary,108,y+103,970,29,19,Gold);
                Label(screen,"Quest progress "+quest.Id,Mathf.Min(quest.Current,quest.Target)+" / "+quest.Target,1044,y+13,225,32,23,quest.CanClaim?Gold:Muted,TextAnchor.MiddleRight);
                var collect=Button(screen,"Claim quest "+quest.Id,quest.Claimed?"수령 완료":quest.CanClaim?"보상 수령":"진행 중",1086,y+66,190,59,()=>ClaimRecurringQuest(quest.Id,quest.PeriodKey),quest.CanClaim);
                collect.interactable=quest.CanClaim&&!quest.Claimed;
            }
            Box(screen,"Quest notes",1340,294,493,637,new Color(.022f,.037f,.056f,.96f));
            Label(screen,"Supply note heading","작은 항해가,\n다음 불씨를 남깁니다.",1370,316,428,107,31,Ivory);
            Label(screen,"Supply currency","보유 별가루  "+progress.Data.Stardust.ToString("N0"),1373,451,421,43,25,Gold);
            Label(screen,"Supply note","완료한 의뢰는 직접 수령해 주세요.\n\n승리 보상을 받은 전투만 집계합니다. 패배·퇴각·연습은 제외됩니다.\n\n미수령 보상은 초기화 후 사라집니다.",1373,516,421,249,23,Muted);
            Button(screen,"Supply dungeon","잔향 회수로 →",1371,790,429,60,ShowWeeklyDungeons,true);
            Button(screen,"Supply materials","전용 재료 · 기억 잠항 →",1371,868,429,60,()=>ShowMaterialDungeons());
            Footer("일일·주간 의뢰는 각각 수령할 수 있습니다 · 진행과 수령 기록은 이 기기에 저장됩니다");
        }
        void ClaimRecurringQuest(string id,string periodKey)
        {
            if(recurringBusy||page!="recurring")return;recurringBusy=true;
            var snapshot=progress.Capture();
            if(!progress.ClaimQuest(id,periodKey,out var reward,out string error)){
                recurringBusy=false;ShowRecurringBoard(weeklyQuestTab);Notify(error);return;
            }
            if(!PersistRecruitment()){
                progress.Restore(snapshot);recurringBusy=false;Notify("저장하지 못해 보상 수령을 취소했습니다. 다시 수령할 수 있습니다.");return;
            }
            recurringBusy=false;ShowRecurringBoard(weeklyQuestTab);Notify("보급 수령 · "+reward.Summary);
        }
        void ShowWeeklyDungeons()
        {
            if(page=="battle"||page=="resolve"){Notify("전투를 마친 뒤 잔향 회수를 열어 주세요.");return;}
            campaign.Exit();ExitWeeklyDungeon();ExitMaterialDungeon();recurringBusy=false;
            if(!RefreshRecurringActivities())return;
            Clear("weeklyDungeon");Header("ECHO SALVAGE / 귀항호 주간 원정","잔향 회수",ShowHub);
            var sea=Texture("VoyageSea");if(sea)screen.Find("The eternal night").GetComponent<RawImage>().texture=sea;
            int remaining=Math.Max(0,recurringSnapshot.WeeklyDungeonRewardLimit-recurringSnapshot.WeeklyDungeonRewardsClaimed);
            Box(screen,"Salvage status backing",80,146,1754,121,new Color(.024f,.043f,.061f,.96f));
            Label(screen,"Dungeon shared rewards","이번 주 보상  "+remaining+" / "+recurringSnapshot.WeeklyDungeonRewardLimit+"회 남음  ·  모든 난도 공유",109,162,1700,43,29,Gold);
            Label(screen,"Dungeon weekly reset","월요일 오전 5시 초기화  ·  다음 "+ActivityResetLabel(recurringSnapshot.NextWeeklyResetUtc)+"  ·  실패·퇴각은 횟수를 쓰지 않습니다.",112,216,1690,31,21,Muted);
            for(int i=0;i<recurringSnapshot.Dungeons.Count;i++){
                var dungeon=recurringSnapshot.Dungeons[i];float x=80+i*594;
                Box(screen,"Dungeon card "+dungeon.Id,x,295,566,605,new Color(.021f,.037f,.055f,.97f));
                Box(screen,"Dungeon gold edge "+dungeon.Id,x,295,566,2,new Color(Gold.r,Gold.g,Gold.b,.6f));
                Label(screen,"Dungeon difficulty "+dungeon.Id,dungeon.DifficultyLabel,x+30,316,505,34,20,Gold);
                Label(screen,"Dungeon title "+dungeon.Id,dungeon.Name,x+25,367,516,59,36,Ivory);
                Label(screen,"Dungeon description "+dungeon.Id,dungeon.Description,x+30,448,505,95,24,Muted);
                Label(screen,"Dungeon rules "+dungeon.Id,"적 "+dungeon.EnemyCount+"명  ·  "+(dungeon.IsBoss?"보스전":"일반전")+"\n보유한 출격 편성 4명",x+30,566,505,75,22,Ivory);
                Label(screen,"Dungeon reward "+dungeon.Id,"승리 수령 보상\n"+dungeon.Reward.Summary,x+30,662,505,151,21,Gold);
                var sortie=Button(screen,"Enter dungeon "+dungeon.Id,remaining>0?"보상 원정 출항 →":"보상 없는 연습 →",x+30,818,505,60,()=>StartWeeklyDungeon(dungeon.Id,remaining<=0),true);
                sortie.interactable=recurringSnapshot.DungeonUnlocked;
            }
            Label(screen,"Dungeon availability",recurringSnapshot.DungeonUnlocked?"승리 후 보상을 수령할 때만 횟수가 줄어듭니다. 연습은 보상과 의뢰 진행도가 없습니다.":recurringSnapshot.UnlockDescription,94,932,1710,41,23,Gold,TextAnchor.MiddleCenter);
            Button(screen,"Dungeon supply board","보급 게시판",1480,965,340,47,()=>ShowRecurringBoard(true));
            Button(screen,"Dungeon materials","기억 잠항",1118,965,340,47,()=>ShowMaterialDungeons());
            Footer("메인 이야기와 별도의 주간 원정입니다 · 원정에서 본편 스테이지를 완료하거나 최초 보상을 받지 않습니다");
        }
        void StartWeeklyDungeon(string id,bool practice)
        {
            if(recurringBusy||page!="weeklyDungeon")return;
            if(!RefreshRecurringActivities())return;
            if(!weeklyDungeon.Enter(id,progress,practice,out string error)){Notify(error);return;}
            campaign.Exit();ExitMaterialDungeon();battle=weeklyDungeon.Battle;PrepareBattlePresentation();
            if(practice)Notify("연습 원정 · 보상과 의뢰 진행도가 없습니다.");
        }
        void RetryWeeklyDungeon()
        { weeklyDungeon.Restart(progress);battle=weeklyDungeon.Battle;PrepareBattlePresentation(); }

        void ShowWeeklyDungeonResult()
        {
            bool victory=battle.State.Outcome==BattleOutcome.Victory;
            Clear("result");Character(screen,battle.State.Allies[0].Portrait,90,143,700,826);
            Label(screen,"Result eyebrow",weeklyDungeon.Practice?"ECHO SALVAGE / PRACTICE":"ECHO SALVAGE / 귀항호 원정",862,223,970,39,21,Gold);
            Label(screen,"Result headline",victory?"되찾은 잔향":"귀항을 준비하며",854,292,1010,120,68);
            Label(screen,"Result quote",victory?weeklyDungeon.Practice?"연습 원정을 마쳤습니다.\n보상과 의뢰 진행도는 증가하지 않습니다.":"어둠 속에 남은 보급품을 회수했습니다.\n아래 버튼으로 보상을 수령하고 귀항합니다.":"실패는 주간 보상 횟수를 차감하지 않습니다.\n편성과 맞불을 확인해 다시 도전할 수 있습니다.",864,449,930,130,29,Muted);
            if(victory&&!weeklyDungeon.Practice)Label(screen,"Dungeon pending rewards",weeklyDungeon.Active.Reward.Summary.Replace(" / ","\n"),870,604,904,200,27,Gold);
            Button(screen,"Dungeon result continue",victory?weeklyDungeon.Practice?"연습 종료하고 귀항 →":"보상 수령하고 귀항 →":"다시 도전하기 →",865,857,560,83,()=>{if(victory)ClaimWeeklyDungeonReward();else RetryWeeklyDungeon();},true);
            Button(screen,"Dungeon result return",victory?"보급 게시판으로":"원정 선택으로",1450,857,360,83,()=>{if(victory)ClaimWeeklyDungeonReward(true);else {ExitWeeklyDungeon();ShowWeeklyDungeons();}});
            Footer(weeklyDungeon.Practice?"보상 없는 연습 · 본편 진행과 보급 의뢰에는 영향을 주지 않습니다":"수령 저장이 끝난 뒤에만 원정을 종료합니다 · 실패·퇴각은 주간 보상을 차감하지 않습니다");
        }
        void ClaimWeeklyDungeonReward(bool board=false)
        {
            if(recurringBusy||page!="result"||weeklyDungeon.Active==null)return;
            recurringBusy=true;var snapshot=progress.Capture();
            if(!weeklyDungeon.ClaimVictory(progress,out var reward,out string error)){recurringBusy=false;Notify(error);return;}
            if(!PersistRecruitment()){
                progress.Restore(snapshot);recurringBusy=false;Notify("저장하지 못해 수령을 취소했습니다. 이 화면에서 다시 수령해 주세요.");return;
            }
            bool practice=weeklyDungeon.Practice;recurringBusy=false;ExitWeeklyDungeon();if(board)ShowRecurringBoard(true);else ShowWeeklyDungeons();Notify(practice?"연습 원정 완료 · 보상과 의뢰 진행도는 증가하지 않았습니다.":"잔향 회수 완료 · "+reward.Summary);
        }
    }
}
