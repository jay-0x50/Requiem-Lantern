using System;
using System.Collections.Generic;
using UnityEngine;
using RequiemLantern.Core;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        float storyHold;
        bool storyAppFocused=true,storyAppPaused;

        void OnApplicationFocus(bool focused){storyAppFocused=focused;if(focused)storyHold=0;}
        void OnApplicationPause(bool paused){storyAppPaused=paused;if(!paused)storyHold=0;}
        void ProcessStoryInput(float delta,bool nextPressed)
        {
            if(!dialogue||page!="story"||overlay||!storyAppFocused||storyAppPaused)return;
            if(!TickStoryReading(delta,nextPressed)&&nextPressed)NextStory();
        }
        bool TickStoryReading(float delta,bool nextPressed=false)
        {
            bool wasComplete=revealed>=lineText.Length;
            revealed=Mathf.Min(lineText.Length,revealed+delta*36);
            dialogue.text=lineText.Substring(0,Mathf.Min(lineText.Length,(int)revealed));
            if(!wasComplete||!storyAuto){storyHold=0;return false;}
            storyHold+=delta;
            if(nextPressed)return false;
            if(storyHold<Mathf.Clamp(story[storyIndex].AutoDelay,1f,6f))return false;
            NextStory();return true;
        }

        string VisibleStoryTranscript()
        {
            var entries=new List<string>();
            for(int i=0;i<=storyIndex&&i<story.Count;i++){
                var line=story[i];
                string visible=i<storyIndex?line.Text:line.Text.Substring(0,Mathf.Clamp((int)revealed,0,line.Text.Length));
                if(visible.Length==0)continue;
                entries.Add((string.IsNullOrEmpty(line.Speaker)?"":line.Speaker+"  ")+visible);
            }
            return entries.Count==0?"아직 표시된 대화가 없습니다.":string.Join("\n\n",entries);
        }

        string StoryAdvanceLabel()
        {
            if(campaign.Active!=null&&campaign.Replay&&campaign.Active.After.Count>0&&storyIndex==campaign.Active.Before.Count-1)return "이어서 읽기  ◆";
            if(storyIndex<story.Count-1)return "다음  ◆";
            if(campaign.Active==null)return "이야기 마치기  ◆";
            if(campaign.Replay)return "항해 기록으로  ◆";
            if(campaign.Phase==CampaignPhase.BeforeStory&&campaign.Active.HasBattle)return "전투 시작  ◆";
            return "기항지 완료  ◆";
        }
        void CloseStoryLog(){storyHold=0;CloseRecruitmentOverlay();}

        string[] StoryPortraitPositions()
        {
            string left="",right="";
            for(int i=0;i<=storyIndex&&i<story.Count;i++){
                var line=story[i];
                if(string.IsNullOrEmpty(line.Speaker)&&(!string.IsNullOrEmpty(left)||!string.IsNullOrEmpty(right)))continue;
                string active=string.IsNullOrEmpty(line.Portrait)?"":ArtId(line.Portrait);
                string listener=string.IsNullOrEmpty(line.ListenerPortrait)?"":ArtId(line.ListenerPortrait);
                if(active==listener)listener="";
                PlaceStoryPortrait(ref left,ref right,active,listener);
                PlaceStoryPortrait(ref left,ref right,listener,active);
            }
            return new[]{left,right};
        }

        static void PlaceStoryPortrait(ref string left,ref string right,string id,string keep)
        {
            if(string.IsNullOrEmpty(id)||left==id||right==id)return;
            if(string.IsNullOrEmpty(left))left=id;
            else if(string.IsNullOrEmpty(right))right=id;
            else if(left==keep)right=id;
            else left=id;
        }

        void DrawStoryPortraits(StoryLine line)
        {
            var positions=StoryPortraitPositions();
            string key=string.Join("|",positions);
            for(int side=0;side<2;side++){
                string id=positions[side];if(string.IsNullOrEmpty(id))continue;
                var portrait=Character(screen,id,side==0?115:995,62,810,1100,!reducedMotion);
                bool speaking=CampaignCharacterName(id)==line.Speaker;
                if(portrait.texture)portrait.color=speaking?Color.white:new Color(.78f,.80f,.84f,.72f);
                if(lastStoryPortrait.IndexOf(id,StringComparison.Ordinal)<0&&!reducedMotion)StartCoroutine(RevealPortrait(portrait,screenVersion));
            }
            if(string.IsNullOrEmpty(positions[0])&&string.IsNullOrEmpty(positions[1]))Box(screen,"Silence",0,0,1920,800,new Color(.01f,.02f,.04f,.20f));
            lastStoryPortrait=key;
        }
    }
}
