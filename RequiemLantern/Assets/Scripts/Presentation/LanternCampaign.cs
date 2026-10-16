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
            campaign.Exit();ExitWeeklyDungeon();ExitMaterialDungeon();campaignArchive=archive;
            var chapters=CampaignCatalog.Chapters.Where(c=>archive?c.Stages.Any(s=>CampaignRules.IsCleared(progress.Data,s)):
                CampaignRules.IsUnlocked(progress.Data,c.Stages[0])||CampaignRules.IsCleared(progress.Data,c.Stages[0])).ToList();
            Clear("stages");Header(archive?"MEMORY ARCHIVE / 지나온 항로":"VOYAGE OF THE LANTERN / 귀항호 항해도",archive?"항해 기록실":"항해 준비",ShowHub);
            var sea=Texture("VoyageSea");
            if(sea)screen.Find("The eternal night").GetComponent<UnityEngine.UI.RawImage>().texture=sea;
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
                Button(screen,"Chapter "+number,$"{number}장 · {CampaignCharacterName(c.FocusCharacterId)}  {clear}/{c.Stages.Count}",78+i*356,140,338,68,()=>{
                    selectedChapter=number;selectedStage=c.Stages.FirstOrDefault(s=>!archive&&CampaignRules.IsUnlocked(progress.Data,s)&&!CampaignRules.IsCleared(progress.Data,s))?.Id
                        ??c.Stages.FirstOrDefault(s=>CampaignRules.IsCleared(progress.Data,s))?.Id??c.Stages[0].Id;ShowCampaign(archive);
                },number==selectedChapter);
            }
            Label(screen,"Chapter title",chapter.Number+"장  "+chapter.Title,92,226,1680,64,43,Gold);
            Label(screen,"Chapter summary",string.IsNullOrEmpty(chapter.FocusStorySummary)?chapter.Summary:chapter.FocusStorySummary,97,298,1700,67,23,Ivory);
            Label(screen,"Chapter focus",CampaignCharacterName(chapter.FocusCharacterId)+"의 본편 이야기",97,370,1680,25,18,Gold);
            Box(screen,"Voyage chart backing",67,414,1223,581,new Color(.017f,.047f,.063f,.40f));
            Label(screen,"Voyage vessel name","귀항호  /  등대 보급선에서 동료들의 이동 거점으로",96,429,1020,32,23,Gold);
            Label(screen,"Voyage chart instruction",archive?"밝혀 둔 기항지의 기억을 다시 펼칩니다.":"기항지를 선택해 상륙합니다 · 밝혀진 항로만 표시합니다.",98,468,1060,27,17,Muted);
            Ring(screen,"Chart compass",1169,430,66,new Color(Gold.r,Gold.g,Gold.b,.38f),1.2f);
            Label(screen,"Chart north","N",1185,444,34,30,19,Gold,TextAnchor.MiddleCenter);
            var route=MakeRect(screen,"Voyage route",0,0,1920,1080);
            Vector2? previous=null;
            for(int i=0;i<visible.Count;i++){
                var stageData=visible[i];int routeIndex=chapter.Stages.IndexOf(stageData);
                Vector2 port=VoyagePortPoint(routeIndex,chapter.Stages.Count);
                if(previous.HasValue){
                    var wake=Thread(route,"Voyage wake "+stageData.Id,previous.Value,port,new Color(.64f,.77f,.74f,.54f),true,2);
                    wake.Glow=true;
                }
                previous=port;
                bool cleared=CampaignRules.IsCleared(progress.Data,stageData);
                bool focus=stageData.Id==selectedStage;var color=cleared?Teal:Gold;
                var button=Button(screen,"Stage "+stageData.Id,"",port.x-42,port.y-42,84,84,()=>{selectedStage=stageData.Id;ShowCampaign(archive);},focus);
                button.GetComponent<OrnateButton>().Frame.enabled=false;
                Ring(button.transform,"Port water",4,4,76,new Color(.022f,.054f,.068f,.98f),38);
                Ring(button.transform,"Port seal",4,4,76,color,focus?4:2);
                if(focus)Ring(button.transform,"Selected port",0,0,84,new Color(color.r,color.g,color.b,.47f),1);
                Label(button.transform,"Stage number",stageData.Number,3,8,78,33,24,color,TextAnchor.MiddleCenter);
                DrawVoyagePortSymbol(button.transform,stageData,42,57,color);
                Label(button.transform,"Stage state",cleared?"완료":archive?"항해 기록":"다음 기항지",-49,-28,182,24,14,color,TextAnchor.MiddleCenter);
                var portTitle=Label(button.transform,"Stage title",stageData.Title,-62,91,208,30,20,Ivory,TextAnchor.MiddleCenter);
                portTitle.resizeTextForBestFit=true;portTitle.resizeTextMinSize=15;portTitle.resizeTextMaxSize=20;
                Label(button.transform,"Stage type",stageData.IsBoss?"닻을 내릴 결전":stageData.KindLabel,-62,125,208,24,14,color,TextAnchor.MiddleCenter);
            }
            var ship=Texture("VoyageShip");
            if(ship)Picture(screen,"Voyage ship","VoyageShip",92,889,263,100);
            else DrawVoyageShip(screen,130,909);
            var berth=visible.FirstOrDefault(stageData=>!CampaignRules.IsCleared(progress.Data,stageData))??visible.Last();
            Vector2 berthPoint=VoyagePortPoint(chapter.Stages.IndexOf(berth),chapter.Stages.Count);
            if(ship)Picture(screen,"Voyage ship position","VoyageShip",berthPoint.x+(berthPoint.x>1100?-128:42),berthPoint.y-42,96,53);
            Label(screen,"Current port","정박 기항지  /  "+berth.Number+"  "+berth.Title,386,921,845,40,22,Gold);
            Label(screen,"Voyage legend","○ 이야기     × 전투     ○× 이야기와 전투     닻: 결전",386,963,845,23,15,Muted);
            bool canEnter=CampaignRules.IsUnlocked(progress.Data,selected),wasCleared=CampaignRules.IsCleared(progress.Data,selected);
            bool hasStory=selected.Before.Count+selected.After.Count>0;
            Box(screen,"Stage details",1320,414,530,581,new Color(.021f,.038f,.057f,.95f));
            Label(screen,"Voyage log eyebrow",archive?"SHIP'S LOG / 지나온 기항지":"LANDING ORDER / 상륙 계획",1347,441,470,30,16,Gold);
            Label(screen,"Selected stage",selected.Number+"  "+selected.Title,1347,489,470,82,31,Gold);
            Label(screen,"Stage summary",selected.Summary,1349,593,468,125,23,Ivory);
            string names=string.Join(" · ",selected.Participants.Select(CampaignCharacterName));
            string partyNames=string.Join(" · ",progress.Data.ActiveParty.Select(CampaignCharacterName));
            Label(screen,"Participants","등장인물  "+names+"\n"+(selected.HasBattle?"출격 편성  "+partyNames+"\n":"")+selected.KindLabel+(selected.HasBattle?" · 적 "+selected.EnemyCount+"명":"")+(selected.IsBoss?" · 보스":""),1349,727,468,88,17,Muted);
            string action=archive?hasStory?"이야기 다시 읽기 →":"전투 기록":selected.Kind==StageKind.Story?wasCleared?"이야기 다시 읽기 →":"이야기 시작 →":wasCleared?"다시 출격 →":"스테이지 시작 →";
            var enter=Button(screen,"Enter stage",action,1350,827,470,74,()=>EnterCampaignStage(selected.Id,archive||selected.Kind==StageKind.Story&&wasCleared),true);
            enter.interactable=archive?wasCleared&&hasStory:canEnter;
            string detail=archive?hasStory?"완료한 이야기를 다시 펼칩니다.":"대화가 없는 전투 스테이지입니다.":wasCleared?selected.HasBattle?"완료한 항해 기록입니다. 다시 출격해도 최초 보상은 중복 지급되지 않습니다.":"완료한 항해 기록입니다. 이야기를 다시 읽을 수 있습니다.":selected.HasBattle?"전투 승리와 후속 대화 후 다음 구간이 열립니다.":"이야기를 읽거나 건너뛰면 다음 구간이 열립니다.";
            Label(screen,"Stage availability",detail,1349,923,468,58,18,Muted,TextAnchor.MiddleCenter);
            Footer(archive?"완료한 기항지의 대화 다시 보기 · 다시 읽기는 보상을 지급하지 않습니다":"귀항호에서 항로·상륙 동선 확인 → 기항지 선택 · 완료한 구간과 다음 기항지만 표시합니다");
        }
        Vector2 VoyagePortPoint(int index,int total)
        {
            // Staggered rows describe a coast; the current ten-stage chapters
            // occupy five columns and two rows of connected ports.
            int columns=Mathf.Max(4,Mathf.CeilToInt(total/2f));
            int row=index/columns,column=index%columns;
            if(row%2==1)column=columns-1-column;
            float spacing=984f/Mathf.Max(1,columns-1);
            return new Vector2(187+column*spacing,570+row*201+(column%2==0?0:24));
        }
        void DrawVoyagePortSymbol(Transform parent,StageData data,float x,float y,Color color)
        {
            if(data.IsBoss){
                Ring(parent,"Anchor eye",x-4,y-14,8,color,2);
                Thread(parent,"Anchor stock",new Vector2(x,y-7),new Vector2(x,y+8),color,false,2.5f);
                Thread(parent,"Anchor crossbar",new Vector2(x-10,y-3),new Vector2(x+10,y-3),color,false,2);
                Thread(parent,"Anchor left",new Vector2(x-13,y),new Vector2(x,y+10),color,false,2.5f);
                Thread(parent,"Anchor right",new Vector2(x+13,y),new Vector2(x,y+10),color,false,2.5f);return;
            }
            if(data.Kind!=StageKind.Battle)Ring(parent,"Story buoy",x-9,y-9,18,color,2);
            if(data.HasBattle){
                Thread(parent,"Battle blade A",new Vector2(x-11,y-11),new Vector2(x+11,y+11),color,false,2);
                Thread(parent,"Battle blade B",new Vector2(x+11,y-11),new Vector2(x-11,y+11),color,false,2);
            }
        }
        void DrawVoyageShip(Transform parent,float x,float y)
        {
            Thread(parent,"Ship hull",new Vector2(x,y),new Vector2(x+156,y),Gold,false,5);
            Thread(parent,"Ship bow",new Vector2(x+156,y),new Vector2(x+190,y-24),Gold,false,4);
            Box(parent,"Ship cabin",x+48,y-39,74,37,new Color(.07f,.13f,.16f,1));
            Thread(parent,"Ship mast",new Vector2(x+109,y-36),new Vector2(x+109,y-90),Gold,false,3);
            Ring(parent,"Ship lantern",x+99,y-104,20,Gold,4);
        }
        string CampaignCharacterName(string id)
        {
            if(string.IsNullOrEmpty(id))return "동료들";
            switch(id.ToLowerInvariant()){
                case "lian":return "리안";case "sera":return "세라";case "minato":return "미나토";case "heidel":return "하이델";case "noa":return "노아";case "kai":return "카이";case "mare":return "마레";
                case "rowan":return "로웬";case "ellen":return "엘렌";case "eve":return "이브";case "orphe":return "오르페";default:return id;
            }
        }
        void EnterCampaignStage(string id,bool replay=false)
        {
            if(!campaign.Enter(id,progress.Data,replay,out string error)){Notify(error);return;}
            ExitWeeklyDungeon();ExitMaterialDungeon();
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
            var snapshot=progress.Capture();var checkpoint=campaign.Capture();
            if(!campaign.FinishStory(progress.Data,out bool changed,out string error)){Notify(error);return;}
            if(changed&&!PersistRecruitment()){progress.Restore(snapshot);campaign.Restore(checkpoint);Notify("저장하지 못했습니다. 이 장면을 다시 마치면 저장을 재시도합니다.");storyIndex=Math.Max(0,story.Count-1);storyDone=FinishCampaignStory;DrawStory();return;}
            ContinueCampaign();
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
            if(materialDungeon.Active!=null){ExitMaterialDungeon();Clear("materialDungeon");ShowMaterialDungeons();return;}
            if(weeklyDungeon.Active!=null){ExitWeeklyDungeon();Clear("weeklyDungeon");ShowWeeklyDungeons();return;}
            if(campaign.Active!=null)ShowCampaign(false);else ShowHub();
        }
        void ShowCharacterChapter(string id)
        {
            var chapter=CampaignCatalog.Chapters.FirstOrDefault(c=>c.FocusCharacterId==id);
            if(chapter==null)return;
            if(!CampaignRules.IsUnlocked(progress.Data,chapter.Stages[0])&&!CampaignRules.IsCleared(progress.Data,chapter.Stages[0])){
                Notify(chapter.Number+"장에서 이어지는 이야기입니다. 앞 장의 여정을 먼저 마쳐 주세요.");return;
            }
            selectedChapter=chapter.Number;selectedStage=chapter.Stages.FirstOrDefault(s=>CampaignRules.IsUnlocked(progress.Data,s)&&!CampaignRules.IsCleared(progress.Data,s))?.Id??chapter.Stages[0].Id;
            ShowCampaign(false);
        }
        void ShowArchive() { ShowCampaign(true); }
    }
}
