using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RequiemLantern.Core;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        string lastStoryPortrait="";
        void ShowTitle()
        {
            Clear("title");Box(screen,"Title veil",0,0,1920,1080,new Color(.02f,.035f,.065f,.35f));
            Label(screen,"Overline","T H E   L A S T   L I G H T",160,185,1200,40,19,Gold);
            var logo=Label(screen,"Logo","REQUIEM\nLANTERN",145,225,1400,270,118,Ivory);
            logo.lineSpacing=.78f;
            Box(screen,"Gold rule",162,514,100,3,Gold);
            Label(screen,"Korean title","레퀴엠 랜턴",160,546,1000,64,38);
            Label(screen,"Promise","태양이 꺼진 지 13년.\n우리가 잃어버린 것들은, 아직 빛나고 있다.",163,640,1000,96,26,Muted);
            Button(screen,"Begin story",progress.Data.PrologueCompleted?"등대 요새로 돌아가기   →":"마지막 불씨를 따라서   →",160,814,560,76,()=>{
                if(progress.Data.PrologueCompleted)ShowHub();else StartStory(StoryCatalog.Prologue,()=>{progress.MarkPrologueComplete();Persist();ShowHub();});
            },true);
            Button(screen,"Title settings","설정",748,814,164,76,ShowSettings);
            Label(screen,"Small inscription","기억이 사라져도\n등불은 남는다.",1400,764,350,120,26,Muted,TextAnchor.MiddleRight);
            Footer("초기 플레이 버전  ·  클릭 / 터치로 시작");
        }
        void ShowHub()
        {
            Clear("hub");Header("BEACON KEEP  /  영야의 해안","등대 요새");
            Box(screen,"Portrait plate",66,160,850,800,new Color(.025f,.04f,.06f,.2f));
            Character(screen,"Lian",190,143,625,811);
            Label(screen,"Location number","01",80,170,150,100,72,new Color(.9f,.8f,.66f,.6f));
            Label(screen,"Keep status",$"등대 복구  {progress.Data.KeepLevel}단계",94,800,600,46,25,Gold);
            Label(screen,"Lian quote","“…살아 있으면, 그걸로 됐어.”",94,862,800,64,30);
            Label(screen,"Lian caption","리안  /  등대의 파수꾼",96,927,700,38,19,Muted);
            Label(screen,"Main quest label","MAIN STORY",1010,177,760,40,18,Gold);
            Label(screen,"Main quest title","꺼진 등대",1007,222,800,80,64);
            Label(screen,"Main quest body","이 작은 불을, 함께 지키기로 했다.\n요새 바깥에서 다시 그림자가 움직인다.",1013,318,775,95,25,Muted);
            HubCard("Sortie","01","작전실","예고선을 읽고, 빛실을 잇다",1008,462,ShowStages,true);
            HubCard("Roster","02","잔영 기록","별의 파편을 품은 동료들",1008,594,()=>ShowRoster(0));
            HubCard("FirstMeal","03","첫 식사","잠시, 어둠을 잊는 시간",1008,726,()=>StartStory(StoryCatalog.FirstMeal,ShowHub));
            Button(screen,"Archive","기록실",1010,890,245,65,ShowArchive);
            Button(screen,"Workshop","등대 복구",1275,890,245,65,()=>{
                if(progress.RepairKeep(out string error)){bool saved=Persist();ShowHub();if(saved)Notify("등대의 불빛이 조금 더 따뜻해졌습니다.");}else Notify(error);
            });
            Button(screen,"Settings","설정",1540,890,245,65,ShowSettings);
            if(progress.Data.KeepLevel>1)Box(screen,"Recovered warmth",0,116,1920,908,new Color(1,.53f,.12f,.035f*(progress.Data.KeepLevel-1)));
            Footer("등불지기  카이  ·  작은 온기에서 시작되는 이야기");
        }
        void HubCard(string id,string index,string title,string subtitle,float x,float y,Action action,bool primary=false)
        {
            var b=Button(screen,id,"",x,y,780,111,action,primary);
            Label(b.transform,"Number",index,32,23,60,62,25,Gold);
            Label(b.transform,"Title",title,104,16,520,43,32,Ivory);
            Label(b.transform,"Subtitle",subtitle,105,64,565,27,19,primary?Ivory:Muted);
            Label(b.transform,"Arrow","→",680,25,60,57,33,Gold);
        }
        void StartStory(List<StoryLine> lines,Action done)
        {
            story=lines;storyIndex=0;storyDone=done;storyLog.Clear();storyAuto=false;lastStoryPortrait="";DrawStory();
        }
        void DrawStory()
        {
            if(storyIndex>=story.Count){storyDone?.Invoke();return;}
            var l=story[storyIndex];Clear("story");
            if(l.Background=="keep")screen.Find("The eternal night").GetComponent<RawImage>().texture=Texture("Keep");
            if(l.Background=="black")Box(screen,"Blackout",0,0,1920,1080,new Color(.01f,.02f,.035f,.99f));
            if(!string.IsNullOrEmpty(l.Portrait)){
                string id=l.Portrait;for(int i=0;i<CharacterIds.Length;i++)if(id.Equals(CharacterIds[i],StringComparison.OrdinalIgnoreCase)||id==CharacterNames[i])id=CharacterIds[i];
                var portrait=Character(screen,id,115,62,810,1100,!reducedMotion);
                int characterIndex=Array.IndexOf(CharacterIds,id);
                bool speaking=characterIndex>=0&&l.Speaker==CharacterNames[characterIndex];
                portrait.color=speaking?Color.white:new Color(.67f,.70f,.75f,1);
                if(id!=lastStoryPortrait&&!reducedMotion)StartCoroutine(RevealPortrait(portrait,screenVersion));
                lastStoryPortrait=id;
            }
            else {lastStoryPortrait="";Box(screen,"Silence",0,0,1920,800,new Color(.01f,.02f,.04f,.20f));}
            var sideVeil=MakeRect(screen,"Cinematic side shadows",0,0,1920,1080).gameObject.AddComponent<CinematicVeil>();sideVeil.Horizontal=true;sideVeil.color=new Color(0,.005f,.012f,.67f);sideVeil.raycastTarget=false;
            var plaque=MakeRect(screen,"Location plaque",52,34,535,92).gameObject.AddComponent<OrnateButtonGraphic>();plaque.raycastTarget=false;
            Label(plaque.transform,"Location",!string.IsNullOrEmpty(l.Location)?l.Location:l.Background=="keep"?"등대 요새":l.Background=="black"?"기억의 잔향":"얼어붙은 폐허",32,4,467,66,31,Ivory,TextAnchor.MiddleCenter);
            Label(plaque.transform,"Chapter",!string.IsNullOrEmpty(l.Caption)?l.Caption:"CHAPTER 01  ·  꺼진 등대",35,61,460,23,13,Gold,TextAnchor.MiddleCenter);
            Button(screen,"Dialogue log","기록",1327,38,154,58,ShowDialogueLog);
            Button autoButton=null;
            autoButton=Button(screen,"Auto dialogue",storyAuto?"자동 켜짐":"자동",1495,38,164,58,()=>{storyAuto=!storyAuto;autoButton.GetComponent<OrnateButton>().Caption.text=storyAuto?"자동 켜짐":"자동";});
            Button(screen,"Skip dialogue","건너뛰기",1673,38,190,58,()=>{storyIndex=story.Count;storyDone?.Invoke();});
            var bottomVeil=MakeRect(screen,"Cinematic dialogue fade",0,670,1920,410).gameObject.AddComponent<CinematicVeil>();bottomVeil.color=new Color(.006f,.012f,.024f,.96f);bottomVeil.raycastTarget=false;
            var panel=Box(screen,"Dialogue panel",122,799,1676,248,new Color(.016f,.024f,.039f,.43f));panel.raycastTarget=true;
            var b=panel.gameObject.AddComponent<Button>();b.transition=UnityEngine.UI.Selectable.Transition.None;b.onClick.AddListener(NextStory);
            for(int scan=0;scan<42;scan++)Box(panel.transform,"Glass grain "+scan,0,scan*6,1676,1,new Color(.74f,.66f,.45f,.018f));
            Box(panel.transform,"Left engraved edge",0,0,386,1,new Color(.77f,.63f,.38f,.55f));
            Box(panel.transform,"Right engraved edge",505,0,1171,1,new Color(.77f,.63f,.38f,.30f));
            Label(panel.transform,"Speaker",string.IsNullOrEmpty(l.Speaker)?"기억의 잔향":l.Speaker,44,15,1500,48,27,Gold);
            lineText=l.Text;revealed=0;
            dialogue=Label(panel.transform,"Dialogue","",44,77,1550,128,32,Ivory,string.IsNullOrEmpty(l.Speaker)?TextAnchor.MiddleCenter:TextAnchor.UpperLeft);
            Label(panel.transform,"Continue","◆",1592,195,48,40,19,Gold,TextAnchor.MiddleCenter);
            string entry=$"{l.Speaker}  {l.Text}";if(storyLog.Count==0||storyLog[storyLog.Count-1]!=entry)storyLog.Add(entry);
            Label(screen,"Story progress",$"{storyIndex+1:00} / {story.Count:00}  ·  Space / 클릭",1380,1040,440,25,14,Muted,TextAnchor.MiddleRight);
        }
        System.Collections.IEnumerator RevealPortrait(RawImage portrait,int version)
        {var target=portrait.color;for(float t=0;t<.32f;t+=Time.unscaledDeltaTime){if(version!=screenVersion||!portrait)yield break;portrait.color=new Color(target.r,target.g,target.b,target.a*Mathf.SmoothStep(0,1,t/.32f));yield return null;}if(portrait)portrait.color=target;}
        void NextStory(){if(page!="story"||overlay)return;if(revealed<lineText.Length){revealed=lineText.Length;dialogue.text=lineText;return;}storyIndex++;DrawStory();}
        void ShowDialogueLog()
        {
            if(overlay)return;
            overlay=MakeRect(stage,"Dialogue log",0,0,1920,1080);Box(overlay,"Scrim",0,0,1920,1080,new Color(0,0,0,.8f)).raycastTarget=true;
            Box(overlay,"Paper",220,120,1480,840,PanelColor);Label(overlay,"Heading","지나온 대화",270,150,1100,65,38,Gold);
            string text=string.Join("\n\n",storyLog.GetRange(Mathf.Max(0,storyLog.Count-5),Mathf.Min(5,storyLog.Count)));
            Label(overlay,"Transcript",text,273,258,1360,590,27);
            Button(overlay,"Close log","닫기",1460,154,180,62,()=>{Destroy(overlay.gameObject);});
        }
        void ShowStages()
        {
            Clear("stages");Header("CHAPTER 01  /  꺼진 등대","작전실",ShowHub);
            Label(screen,"Chapter","첫 번째 불빛",104,209,1250,105,70);
            Label(screen,"Chapter description","등대에 도착한 리안, 세라, 미나토.\n몰려드는 ‘분노’로부터 남은 온기를 지킨다.",110,337,1080,102,27,Muted);
            for(int i=0;i<4;i++){
                float x=170+i*354;Box(screen,"Path "+i,x,571,354,2,new Color(.6f,.51f,.36f,.45f));
                Ring(screen,"Node "+i,x,528,88,i==0?Gold:Muted,2);
                Label(screen,"Stage number "+i,(i+1).ToString("00"),x,528,88,88,27,i==0?Gold:Muted,TextAnchor.MiddleCenter);
                Label(screen,"Stage label "+i,new[]{"요새 앞의 그림자","얼어붙은 길","남겨진 편지","시계탑의 잔향"}[i],x-45,642,270,50,23,i==0?Ivory:Muted,TextAnchor.MiddleCenter);
                if(i>0)Label(screen,"Unbuilt "+i,"준비 중",x-20,694,220,30,18,Muted,TextAnchor.MiddleCenter);
            }
            Box(screen,"Mission panel",116,785,1688,188,new Color(.05f,.075f,.11f,.96f));
            Label(screen,"Selected stage","1-1  요새 앞의 그림자",150,807,1000,48,31,Gold);
            Label(screen,"Mission details","3인 출격  ·  연광 전투 튜토리얼  ·  분노 개체 (팔 / 핵)\n첫 클리어 보상: 코인 120 · 등불 조각 10 · 기억의 파편 1",150,864,1060,82,23,Muted);
            Button(screen,"Deploy","출격 준비   →",1366,836,385,87,StartBattle,true);
            Footer("초기 구현 구간 1-1  ·  나머지 지역은 이후 업데이트 예정");
        }
        void ShowRoster(int selected)
        {
            Clear("roster");Header("AFTERGLOW ARCHIVE  /  인물 기록","잔영과 등불지기",ShowHub);
            var descriptions=new[]{"은백색 머리의 파수꾼.\n소등의 날 이후, 13년간 홀로 등대 요새를 지켰다.\n파편의 대가로 어린 시절의 기억 대부분을 잃었다.","붉은 머리의 낙천가.\n사라진 고아원 아이들을 찾아 요새에 합류했다.\n큰 웃음 뒤에 남겨진 마음을 감추고 있다.","청록색 머리의 다정한 의사 지망생.\n치료하지 못한 환자들의 이름을 모두 기억한다.","고글과 개조 석궁을 쓰는 실용주의 기술자.\n등대 요새의 공방을 맡는다.","철종 교단에서 도망친 수수께끼의 잔영.\n그림자와 대화할 수 있다.","얼어붙은 폐허에서 기억을 잃고 깨어났다.\n잔영들의 빛을 증폭하고 조율한다.","철종 교단의 수장.\n빛이 죽은 세계에 ‘안식’을 제안한다."};
            Character(screen,CharacterIds[selected],70,132,710,840);
            Label(screen,"Character index",$"CHARACTER   {selected+1:00} / 07",827,165,990,36,19,Gold);
            Label(screen,"Character name",CharacterNames[selected],820,225,1010,110,76);
            Label(screen,"Role",CharacterRoles[selected],829,361,1010,45,27,Gold);
            Box(screen,"Description rule",832,442,940,1,new Color(.4f,.45f,.5f,.6f));
            Label(screen,"Biography",descriptions[selected],830,484,935,172,28,Muted);
            Label(screen,"Character quote","“"+CharacterQuotes[selected]+"”",830,685,920,109,29);
            for(int i=0;i<7;i++){int n=i;Button(screen,"Select "+CharacterIds[i],CharacterNames[i],833+(i%4)*241,817+(i/4)*82,220,65,()=>ShowRoster(n),i==selected);}
            Footer("원화 기준 캐릭터 기록  ·  후반부 인물의 핵심 반전은 숨겨져 있습니다");
        }
        void ShowArchive()
        {
            Clear("archive");Header("MEMORY ARCHIVE","기록실",ShowHub);
            Label(screen,"Archive intro","빛은 기억을 품는다.",123,205,1640,120,65);
            Label(screen,"Archive sub","지나온 이야기를 다시 펼쳐 봅니다.",130,334,1200,70,27,Muted);
            HubCard("Replay prologue","00","마지막 불씨","폐허에서 깨어난 등불지기와 리안의 만남",130,475,()=>StartStory(StoryCatalog.Prologue,ShowArchive));
            HubCard("Replay meal","01","첫 식사","등대 요새에 남겨진 작은 온기",130,622,()=>StartStory(StoryCatalog.FirstMeal,ShowArchive));
            HubCard("Character stories","02","캐릭터 이야기","리안 · 세라 · 노아가 남긴 이름과 약속",130,769,ShowCharacterStories);
            Label(screen,"Collection","보관 중인 기억",1115,466,620,55,32,Gold);
            Label(screen,"Memory count",progress.Data.MemoryFragments.ToString("00"),1110,553,570,150,110);
            Label(screen,"Memory note","전투에서 되찾은 기억의 파편.\n복구와 이야기 확장을 위한 첫 기록입니다.",1116,731,660,100,25,Muted);
            Footer("설정집의 인물과 사건을 기준으로 구성한 초기 각색");
        }
        void ShowCharacterStories()
        {
            Clear("character-stories");Header("AFTERGLOW / CHARACTER STORIES","캐릭터 이야기",ShowArchive);
            Label(screen,"Spoiler note","인물의 과거와 후반부 내용이 포함됩니다. 권장 감상 순서를 확인하고 읽어 주세요.",126,178,1650,62,23,Muted);
            for(int i=0;i<CharacterStoryCatalog.All.Count;i++){
                var entry=CharacterStoryCatalog.All[i];float x=126+i*566;
                Box(screen,"Story card "+entry.Id,x,284,536,710,new Color(.045f,.073f,.105f,.96f));
                Label(screen,"Story owner "+entry.Id,entry.Name+" · ★5",x+28,300,480,40,25,Gold);
                Character(screen,ArtId(entry.Id),x+55,345,426,369,!reducedMotion);
                Label(screen,"Story title "+entry.Id,entry.Title,x+28,727,480,60,31,Ivory);
                Label(screen,"Story summary "+entry.Id,entry.Summary,x+28,794,480,93,22,Muted);
                Label(screen,"Story order "+entry.Id,entry.RecommendedAfter,x+28,887,480,38,17,Gold);
                Button(screen,"Read character "+entry.Id,"이야기 읽기 →",x+28,931,480,50,()=>StartStory(entry.Lines,ShowCharacterStories),true);
            }
            Footer("세 잔영의 합류, 상실, 그리고 스스로 고른 결의");
        }
        void ShowSettings()
        {
            if(page!="settings")settingsReturn=page;Clear("settings");Header("PREFERENCES","환경 설정",()=>{if(settingsReturn=="title")ShowTitle();else ShowHub();});
            Label(screen,"Settings heading","편안한 속도로, 빛을 잇도록.",133,195,1700,110,57);
            SettingsRow("점화 타이밍 보조","자동으로 ‘양호’ 판정. 끄면 Space / 클릭으로 점화합니다.",390,assisted,()=>{assisted=!assisted;SaveSettings();ShowSettings();});
            SettingsRow("움직임 줄이기","캐릭터 이동, 화면 흔들림과 빛 파편을 간소화합니다.",550,reducedMotion,()=>{reducedMotion=!reducedMotion;SaveSettings();ShowSettings();});
            SettingsRow("효과음","연결, 점화, 장면 전환의 유리종 음색을 재생합니다.",710,sound,()=>{sound=!sound;SaveSettings();ShowSettings();});
            Footer("설정과 진행 상황은 이 기기에 저장됩니다");
        }
        void SettingsRow(string title,string help,int y,bool on,Action change)
        {
            Box(screen,title,132,y,1640,131,new Color(.05f,.075f,.11f,.94f));Label(screen,title+" label",title,165,y+15,1300,51,31);Label(screen,title+" help",help,166,y+76,1330,38,23,Muted);
            Button(screen,title+" toggle",on?"켜짐":"꺼짐",1538,y+29,194,71,change,on);
        }
        void SaveSettings(){PlayerPrefs.SetInt("assisted",assisted?1:0);PlayerPrefs.SetInt("reducedMotion",reducedMotion?1:0);PlayerPrefs.SetInt("sound",sound?1:0);PlayerPrefs.Save();}
        RingGraphic Ring(Transform p,string name,float x,float y,float diameter,Color c,float thickness=3)
        {var g=MakeRect(p,name,x,y,diameter,diameter).gameObject.AddComponent<RingGraphic>();g.color=c;g.Thickness=thickness;g.raycastTarget=false;return g;}
    }
}
