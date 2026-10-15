using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        void AssertCanonicalStoryLayout(string directory)
        {
            var settings=new TextGenerationSettings{font=font,fontSize=32,fontStyle=FontStyle.Normal,lineSpacing=1,scaleFactor=1,
                textAnchor=TextAnchor.UpperLeft,color=Color.white,richText=true,horizontalOverflow=HorizontalWrapMode.Wrap,
                verticalOverflow=VerticalWrapMode.Overflow,generationExtents=new Vector2(1550,128),generateOutOfBounds=true};
            int count=0,maxLines=0;float maxHeight=0;var rows=new List<string>{"stage\tphase\trow\tcharacters\tlines\theight"};
            using(var generator=new TextGenerator())foreach(var stageData in CampaignCatalog.Chapters.SelectMany(chapter=>chapter.Stages)){
                foreach(var block in new[]{new{Phase="before",Lines=stageData.Before},new{Phase="after",Lines=stageData.After}}){
                    for(int index=0;index<block.Lines.Count;index++){
                        var line=block.Lines[index];settings.textAnchor=string.IsNullOrEmpty(line.Speaker)?TextAnchor.MiddleCenter:TextAnchor.UpperLeft;
                        if(!generator.Populate(line.Text,settings))throw new Exception("Story layout failed: "+stageData.Number+"/"+block.Phase+"/"+(index+1));
                        int lines=generator.lineCount;float height=generator.GetPreferredHeight(line.Text,settings);count++;maxLines=Mathf.Max(maxLines,lines);maxHeight=Mathf.Max(maxHeight,height);
                        rows.Add(stageData.Number+"\t"+block.Phase+"\t"+(index+1)+"\t"+line.Text.Length+"\t"+lines+"\t"+height.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
                        if(lines>2||height>128.1f)throw new Exception("Story body exceeds two lines or its 128px height: "+stageData.Number+"/"+block.Phase+"/"+(index+1)+" lines="+lines+" height="+height);
                    }
                }
            }
            File.WriteAllLines(Path.Combine(directory,"canonical-layout.tsv"),rows);
            File.WriteAllText(Path.Combine(directory,"layout-summary.txt"),"PASS: actual Unity TextGenerator; bundled "+font.name+"; fontSize=32; width=1550; height=128; canonical rows="+count+"; maxLines="+maxLines+"; maxPreferredHeight="+maxHeight+".\n");
        }
        IEnumerator SmokeStoryReadingSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="story reading acceptance";smokeDeadline=Time.realtimeSinceStartup+75;
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"RUNNING: canonical layout, read-only transcript, timing and transition gates.\n");
            save=new SaveStore(Path.Combine(directory,"story-reading-progress.xml"));progress=new ProgressService(new ProgressData());campaign.Exit();
            assisted=true;autoMode=false;reducedMotion=true;toastText="";toastUntil=0;storyAppFocused=true;storyAppPaused=false;
            AssertCanonicalStoryLayout(directory);
            StartStory(CampaignCatalog.Get("c1s03").Before,()=>{});
            int partialIndex=Enumerable.Range(7,story.Count-8).Last(index=>story[index].Text.Length>16);storyIndex=partialIndex;DrawStory();revealed=7;dialogue.text=lineText.Substring(0,7);storyAuto=true;storyHold=1.25f;
            string fullCurrent=lineText,firstText=story[0].Text;ShowDialogueLog();
            var transcript=overlay.Find("Transcript viewport/Transcript content/Transcript").GetComponent<Text>();
            if(!transcript.text.Contains(firstText)||transcript.text.Contains(fullCurrent)||!transcript.text.EndsWith(fullCurrent.Substring(0,7)))
                throw new Exception("The scroll log omitted early dialogue or revealed the unread end of the current line.");
            int indexBefore=storyIndex;float visibleBefore=revealed,holdBefore=storyHold;ProcessStoryInput(10,true);
            if(storyIndex!=indexBefore||revealed!=visibleBefore||storyHold!=holdBefore)throw new Exception("A log overlay did not pause typing, auto time and manual input.");
            var scroll=overlay.Find("Transcript viewport").GetComponent<ScrollRect>();Canvas.ForceUpdateCanvases();
            if(scroll.content.sizeDelta.y<=scroll.viewport.rect.height||!scroll.GetComponent<Mask>())throw new Exception("A long read log did not use bounded scrollable content.");
            scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();float bottom=scroll.content.anchoredPosition.y;yield return Capture(directory,"01-log-latest-read-prefix");
            scroll.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();
            if(scroll.content.anchoredPosition.y>=bottom-1||scroll.verticalNormalizedPosition<.99f)throw new Exception("The earliest read lines could not be scrolled into view.");
            yield return Capture(directory,"02-log-first-lines");
            overlay.Find("Close log").GetComponent<Button>().onClick.Invoke();
            if(overlay||revealed!=7||storyHold!=0)throw new Exception("Closing the log changed the current text or kept stale auto hold.");
            ProcessStoryInput(.1f,false);if(revealed<=7||storyIndex!=indexBefore)throw new Exception("Typing did not resume from the preserved visible prefix.");
            float frozen=revealed;storyHold=1;OnApplicationFocus(false);ProcessStoryInput(10,true);
            if(revealed!=frozen||storyHold!=1||storyIndex!=indexBefore)throw new Exception("An unfocused application advanced story text or hold.");
            OnApplicationFocus(true);if(storyHold!=0)throw new Exception("Focus restoration retained old automatic hold.");
            storyHold=1;OnApplicationPause(true);ProcessStoryInput(10,false);if(revealed!=frozen||storyHold!=1)throw new Exception("Application pause advanced story.");
            OnApplicationPause(false);if(storyHold!=0)throw new Exception("Pause restoration retained old automatic hold.");
            var delayed=CampaignCatalog.Chapters.SelectMany(chapter=>chapter.Stages).SelectMany(stageData=>stageData.Before.Concat(stageData.After)).FirstOrDefault(line=>Mathf.Abs(line.AutoDelay-4.8f)<.001f);
            if(delayed==null)throw new Exception("The authored 4.8-second emotional hold was missing.");
            var timing=new List<StoryLine>{delayed,new StoryLine("리안","다음 문장입니다.","keep","Lian"),new StoryLine("세라","한 번에 두 문장을 넘기면 안 됩니다.","keep","Sera")};
            StartStory(timing,()=>{});storyAuto=true;revealed=lineText.Length;ProcessStoryInput(4.79f,false);
            if(storyIndex!=0)throw new Exception("The emotional auto delay ended early.");ProcessStoryInput(.02f,false);if(storyIndex!=1)throw new Exception("The authored auto delay did not advance when complete.");
            StartStory(timing,()=>{});storyAuto=true;revealed=lineText.Length;storyHold=4.79f;ProcessStoryInput(.02f,true);
            if(storyIndex!=1)throw new Exception("Auto timing plus Space advanced more than one line.");
            StartStory(timing,()=>{});storyAuto=true;revealed=lineText.Length;ProcessStoryInput(0,true);if(storyIndex!=1)throw new Exception("Manual advance waited for an automatic emotional hold.");storyAuto=false;
            StartStory(CampaignCatalog.Get("c1s04").Before,()=>{});string[] previous=null;string previousSpeaker="";int switches=0,captureIndexAt=-1;
            for(int index=0;index<story.Count;index++){
                storyIndex=index;var positions=StoryPortraitPositions();
                if(previous!=null&&positions.All(id=>!string.IsNullOrEmpty(id))&&positions.OrderBy(id=>id).SequenceEqual(previous.OrderBy(id=>id))&&story[index].Speaker!=previousSpeaker){
                    if(!positions.SequenceEqual(previous))throw new Exception("Alternating speakers exchanged left/right portrait positions.");switches++;captureIndexAt=index;
                }previous=positions;previousSpeaker=story[index].Speaker;
            }
            if(switches==0)throw new Exception("1-4 did not exercise an alternating two-person conversation.");
            storyIndex=captureIndexAt;DrawStory();revealed=lineText.Length;dialogue.text=lineText;yield return Capture(directory,"03-stable-speaker-sides");
            campaign.Exit();progress=new ProgressService(new ProgressData());progress.Data.ClearedStages.Add("c1s01");progress.Data.PrologueCompleted=true;
            var mixed=CampaignCatalog.Get("c1s02");EnterCampaignStage(mixed.Id);storyIndex=story.Count-1;DrawStory();revealed=lineText.Length;dialogue.text=lineText;
            if(!StoryAdvanceLabel().Contains("전투 시작"))throw new Exception("The final before-story prompt did not describe its battle destination.");
            NextStory();if(page!="battle"||CampaignRules.IsCleared(progress.Data,mixed)||progress.Data.RewardedStages.Contains(mixed.Id))throw new Exception("Reading/skipping the pre-battle story bypassed battle completion or rewards.");
            while(battle.State.Outcome==BattleOutcome.Ongoing&&battle.State.Round<25){battle.RecommendLinks();battle.ResolveRound(IgniteGrade.Good);if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();}
            if(battle.State.Outcome!=BattleOutcome.Victory||!campaign.AcceptVictory(battle,progress,out var reward,out string error))throw new Exception("The targeted real battle did not reach its after-story gate.");
            Persist();ContinueCampaign();if(page!="story"||CampaignRules.IsCleared(progress.Data,mixed)||!progress.Data.RewardedStages.Contains(mixed.Id))throw new Exception("The post-victory story lost its separate completion gate.");
            storyIndex=story.Count-1;DrawStory();revealed=lineText.Length;dialogue.text=lineText;
            if(!StoryAdvanceLabel().Contains("기항지 완료"))throw new Exception("The final after-story prompt did not describe completion.");
            yield return Capture(directory,"04-after-story-completion-prompt");NextStory();
            if(!CampaignRules.IsCleared(progress.Data,mixed))throw new Exception("The final after-story did not complete the stage.");
            int coins=progress.Data.Coins,fragments=progress.Data.LanternFragments;EnterCampaignStage(mixed.Id,true);
            storyIndex=mixed.Before.Count-1;DrawStory();revealed=lineText.Length;
            if(!StoryAdvanceLabel().Contains("이어서 읽기"))throw new Exception("A replay's before/after boundary falsely promised an exit.");NextStory();
            if(page!="story"||storyIndex!=mixed.Before.Count)throw new Exception("Replay incorrectly entered battle between its combined dialogue blocks.");
            storyIndex=story.Count-1;DrawStory();revealed=lineText.Length;
            if(!StoryAdvanceLabel().Contains("항해 기록으로"))throw new Exception("The final replay prompt did not describe its archive destination.");NextStory();
            if(progress.Data.Coins!=coins||progress.Data.LanternFragments!=fragments||page!="stages")throw new Exception("Replaying story altered rewards or failed to return to the archive.");
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: four fresh native story-reading captures and actual Unity canonical TextGenerator results. Every final canonical line fit the 32px/1550x128 body within two lines. The log retained the earliest read lines, excluded the unread current suffix, scrolled to first/latest within a masked viewport and paused manual input/typing/automatic time. Closing preserved the prefix and reset hold; typing resumed. Focus and pause callbacks froze progress and reset hold on return. The authored 4.8-second delay waited to its boundary, manual input remained immediate, and auto plus Space advanced once through the shared production handler. 1-4 speaker alternation kept both portraits on stable sides. One real pre-story→battle→victory→after-story flow preserved the battle/reward/completion gates and destination prompts. Replay continued across before/after, ended at the archive and granted no new rewards. Isolated QA saves only.\n");
            Debug.Log("REQUIEM_LANTERN_STORY_READING_SMOKE_PASS");Application.Quit(0);
        }
    }
}
