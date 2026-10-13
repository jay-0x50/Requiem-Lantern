using System;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        readonly CampaignSession campaign = new CampaignSession();
        int selectedChapter = 1;
        string selectedStage = "c1s01";
        bool campaignArchive;

        StageData NextCampaignStage()
        { return CampaignCatalog.Chapters.SelectMany(c=>c.Stages).FirstOrDefault(s=>!CampaignRules.IsCleared(progress.Data,s)) ?? CampaignCatalog.Chapters.Last().Stages.Last(); }

        void ShowStages() { ShowCampaign(false); }
        void ShowCampaign(bool archive)
        {
            campaign.Exit();campaignArchive=archive;
            var chapters=CampaignCatalog.Chapters.Where(c=>archive?c.Stages.Any(s=>CampaignRules.IsCleared(progress.Data,s)):
                CampaignRules.IsUnlocked(progress.Data,c.Stages[0])||CampaignRules.IsCleared(progress.Data,c.Stages[0])).ToList();
            Clear("stages");Header(archive?"MEMORY ARCHIVE / 지나온 이야기":"MAIN CAMPAIGN / 등불의 여정",archive?"기록실":"작전실",ShowHub);
            if(chapters.Count==0){
                Label(screen,"Empty archive","아직 펼쳐 둔 이야기가 없습니다.",104,282,1680,90,45,Gold);
                Label(screen,"Empty archive note","작전실에서 스테이지를 마치면 지나온 대화를 다시 읽을 수 있습니다.",109,401,1640,75,27,Muted);
                Button(screen,"Open campaign","여정 시작하기 →",113,610,540,77,ShowStages,true);
                Footer("이야기를 마친 뒤 다시 기록실을 찾아 주세요");return;
            }
            var chapter=chapters.FirstOrDefault(c=>c.Number==selectedChapter)??chapters[0];selectedChapter=chapter.Number;
            var visible=chapter.Stages.Where(s=>archive?CampaignRules.IsCleared(progress.Data,s):CampaignRules.IsUnlocked(progress.Data,s)||CampaignRules.IsCleared(progress.Data,s)).ToList();
            var selected=visible.FirstOrDefault(s=>s.Id==selectedStage)??visible.FirstOrDefault(s=>!CampaignRules.IsCleared(progress.Data,s))??visible[0];selectedStage=selected.Id;
            for(int i=0;i<chapters.Count;i++){
                var c=chapters[i];int number=c.Number;
                int clear=c.Stages.Count(s=>CampaignRules.IsCleared(progress.Data,s));
                Button(screen,"Chapter "+number,$"{number}장 · {c.Title}  {clear}/{c.Stages.Count}",78+i*356,140,338,68,()=>{
                    selectedChapter=number;selectedStage=c.Stages.FirstOrDefault(s=>!archive&&CampaignRules.IsUnlocked(progress.Data,s)&&!CampaignRules.IsCleared(progress.Data,s))?.Id
                        ??c.Stages.FirstOrDefault(s=>CampaignRules.IsCleared(progress.Data,s))?.Id??c.Stages[0].Id;ShowCampaign(archive);
                },number==selectedChapter);
            }
            Label(screen,"Chapter title",chapter.Number+"장  "+chapter.Title,92,236,1680,64,45,Gold);
            Label(screen,"Chapter summary",chapter.Summary,97,312,1700,75,24,Muted);
            for(int i=0;i<visible.Count;i++){
                var stageData=visible[i];float x=91+(i%5)*355,y=414+(i/5)*176;
                bool cleared=CampaignRules.IsCleared(progress.Data,stageData);
                var button=Button(screen,"Stage "+stageData.Id,"",x,y,337,153,()=>{selectedStage=stageData.Id;ShowCampaign(archive);},stageData.Id==selectedStage);
                var color=cleared?Teal:Gold;
                Label(button.transform,"Stage number",stageData.Number,22,10,205,38,28,color);
                Label(button.transform,"Stage state",cleared?"완료":"진행 가능",213,14,102,27,16,color,TextAnchor.MiddleRight);
                Label(button.transform,"Stage title",stageData.Title,22,55,290,45,24,Ivory);
                Label(button.transform,"Stage type",stageData.KindLabel+(stageData.IsBoss?" · 보스":""),22,111,290,25,17,color);
            }
            bool canEnter=CampaignRules.IsUnlocked(progress.Data,selected),wasCleared=CampaignRules.IsCleared(progress.Data,selected);
            bool hasStory=selected.Before.Count+selected.After.Count>0;
            Box(screen,"Stage details",91,790,1738,202,new Color(.035f,.055f,.083f,.97f));
            Label(screen,"Selected stage",selected.Number+"  "+selected.Title,121,808,1150,40,30,Gold);
            Label(screen,"Stage summary",selected.Summary,122,857,1160,58,23,Muted);
            string names=string.Join(" · ",selected.Participants.Select(CampaignCharacterName));
            Label(screen,"Participants","등장  "+names+"    /    "+selected.KindLabel,122,928,1160,38,18,Muted);
            string action=archive?hasStory?"이야기 다시 읽기 →":"전투 기록":selected.Kind==StageKind.Story?wasCleared?"이야기 다시 읽기 →":"이야기 시작 →":wasCleared?"다시 출격 →":"스테이지 시작 →";
            var enter=Button(screen,"Enter stage",action,1350,833,438,68,()=>EnterCampaignStage(selected.Id,archive||selected.Kind==StageKind.Story&&wasCleared),true);
            enter.interactable=archive?wasCleared&&hasStory:canEnter;
            string detail=archive?hasStory?"완료한 이야기를 다시 펼칩니다.":"대화가 없는 전투 스테이지입니다.":selected.HasBattle?"전투 승리와 후속 대화 후 다음 구간이 열립니다.":"이야기를 읽거나 건너뛰면 다음 구간이 열립니다.";
            Label(screen,"Stage availability",detail,1336,919,468,58,18,Muted,TextAnchor.MiddleCenter);
            Footer(archive?"완료한 스테이지의 대화 다시 보기 · 다시 읽기는 보상을 지급하지 않습니다":"완료한 구간과 이어지는 스테이지를 표시합니다");
        }
        string CampaignCharacterName(string id)
        {
            switch(id.ToLowerInvariant()){
                case "lian":return "리안";case "sera":return "세라";case "minato":return "미나토";case "heidel":return "하이델";case "noa":return "노아";case "kai":return "카이";
                case "rowan":return "로웬";case "ellen":return "엘렌";case "eve":return "이브";case "orphe":return "오르페";default:return id;
            }
        }
        void EnterCampaignStage(string id,bool replay=false)
        {
            if(!campaign.Enter(id,progress.Data,replay,out string error)){Notify(error);return;}
            selectedStage=campaign.Active.Id;selectedChapter=campaign.Active.Chapter;
            ContinueCampaign();
        }
        void ContinueCampaign()
        {
            if(campaign.Phase==CampaignPhase.BeforeStory||campaign.Phase==CampaignPhase.AfterStory){
                StartStory(campaign.CurrentStory,FinishCampaignStory);return;
            }
            if(campaign.Phase==CampaignPhase.Battle){StartCampaignBattle();return;}
            bool replay=campaign.Replay;
            if(!replay){var next=NextCampaignStage();selectedStage=next.Id;selectedChapter=next.Chapter;}
            ShowCampaign(replay||campaignArchive);
        }
        void FinishCampaignStory()
        {
            if(!campaign.FinishStory(progress.Data,out bool changed,out string error)){Notify(error);return;}
            if(changed)Persist();ContinueCampaign();
        }
        void StartCampaignBattle()
        {
            if(campaign.Active==null||campaign.Phase!=CampaignPhase.Battle)return;
            battle=CampaignBattleFactory.Create(campaign.Active,progress.Data);
            PrepareBattlePresentation();
        }
        void PrepareBattlePresentation()
        {
            var first=battle.State.Allies[0];ChooseSkillForSlot(first.Id,0);
            autoMode=false;lastPresentedEvents.Clear();DrawBattle();
        }
        void LeaveCampaignBattle()
        {
            if(campaign.Active!=null)ShowCampaign(false);else ShowHub();
        }
        void ShowArchive() { ShowCampaign(true); }
    }
}
