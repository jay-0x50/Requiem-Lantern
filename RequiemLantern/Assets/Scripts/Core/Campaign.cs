using System;
using System.Collections.Generic;
using System.Linq;

namespace RequiemLantern.Core
{
    public enum StageKind { Story, Battle, StoryBattle }
    public enum CampaignPhase { None, BeforeStory, Battle, AfterStory, Complete }
    [Serializable] public sealed class ChapterData
    {
        public int Number; public string Title, Summary, FocusCharacterId, FocusStorySummary;
        public List<StageData> Stages = new List<StageData>();
    }
    [Serializable] public sealed class StageData
    {
        public string Id, Number, Title, Summary, Location, EnemyName, EnemyArt, BattleProfile, MusicId;
        public int Chapter, EnemyCount; public bool IsBoss, HumanEnemy;
        public StageKind Kind;
        public string[] Participants, Party;
        public List<StoryLine> Before = new List<StoryLine>(), After = new List<StoryLine>();
        public bool HasBattle { get { return Kind != StageKind.Story; } }
        public string KindLabel { get { return Kind == StageKind.Story ? "스토리" : Kind == StageKind.Battle ? "전투" : "스토리 + 전투"; } }
    }
    public static class CampaignRules
    {
        public static bool IsCleared(ProgressData progress, StageData stage)
        { return stage != null && (progress.ClearedStages.Contains(stage.Id) || (stage.Number == "1-1" && progress.PrologueCompleted)); }
        public static bool IsUnlocked(ProgressData progress, StageData stage)
        {
            if(stage == null)return false;
            var stages = CampaignCatalog.Chapters.SelectMany(c => c.Stages).ToList();
            int index = stages.FindIndex(s => s.Id == stage.Id);
            return index >= 0 && stages.Take(index).All(s => IsCleared(progress, s));
        }
        public static bool Complete(ProgressData progress, StageData stage)
        {
            if(stage == null)return false;
            bool changed = !progress.ClearedStages.Contains(stage.Id);
            if(changed)progress.ClearedStages.Add(stage.Id);
            if(stage.Number == "1-1")progress.PrologueCompleted = true;
            return changed;
        }
    }
    // Only this session advances before-story → battle → after-story. The client cannot
    // mark a mixed stage complete by skipping its fight or by replaying a story.
    public sealed class CampaignSession
    {
        public StageData Active { get; private set; }
        public CampaignPhase Phase { get; private set; }
        public bool Replay { get; private set; }
        public bool Enter(string id, ProgressData progress, bool replay, out string error)
        {
            error = null; var next = CampaignCatalog.Get(id);
            if(next == null) { error = "스테이지를 찾지 못했습니다."; return false; }
            if(replay ? !CampaignRules.IsCleared(progress,next) : !CampaignRules.IsUnlocked(progress,next))
            { error = "앞 스테이지를 완료한 뒤 진행할 수 있습니다."; return false; }
            Active = next; Replay = replay;
            if(replay)Phase = next.Before.Count + next.After.Count > 0 ? CampaignPhase.BeforeStory : CampaignPhase.Complete;
            else if(progress.RewardedStages.Contains(next.Id) && !CampaignRules.IsCleared(progress,next) && next.After.Count > 0)Phase = CampaignPhase.AfterStory;
            else Phase = next.Before.Count > 0 ? CampaignPhase.BeforeStory : next.HasBattle ? CampaignPhase.Battle : CampaignPhase.Complete;
            return true;
        }
        public List<StoryLine> CurrentStory
        {
            get {
                if(Active == null)return new List<StoryLine>();
                if(Replay)return Active.Before.Concat(Active.After).ToList();
                return Phase == CampaignPhase.BeforeStory ? Active.Before : Phase == CampaignPhase.AfterStory ? Active.After : new List<StoryLine>();
            }
        }
        public bool FinishStory(ProgressData progress, out bool changed, out string error)
        {
            changed = false; error = null;
            if(Active == null || (Phase != CampaignPhase.BeforeStory && Phase != CampaignPhase.AfterStory))
            { error = "진행 중인 스토리가 없습니다."; return false; }
            if(Replay) { Phase = CampaignPhase.Complete; return true; }
            if(Phase == CampaignPhase.BeforeStory && Active.HasBattle) { Phase = CampaignPhase.Battle; return true; }
            changed = CampaignRules.Complete(progress,Active); Phase = CampaignPhase.Complete; return true;
        }
        public bool AcceptVictory(BattleEngine battle, ProgressService progress, out RewardData reward, out string error)
        {
            reward = null; error = null;
            if(Active == null || Replay || Phase != CampaignPhase.Battle || battle.State.StageId != Active.Id || battle.State.Outcome != BattleOutcome.Victory)
            { error = "이 스테이지의 전투에서 승리해야 합니다."; return false; }
            bool after = Active.After.Count > 0;
            if(!progress.ClaimVictory(battle,out reward,after)) { error = "이 전투의 보상은 이미 지급했습니다."; return false; }
            Phase = after ? CampaignPhase.AfterStory : CampaignPhase.Complete;
            return true;
        }
        public void Exit() { Active = null; Phase = CampaignPhase.None; Replay = false; }
    }
    public static class CampaignBattleFactory
    {
        public static BattleEngine Create(StageData stage, ProgressData progress = null, int? seed = null)
        {
            if(stage == null || !stage.HasBattle)throw new ArgumentException("Battle stage required.");
            Func<BattleState> factory = () => Build(stage, progress, seed);
            var battle = new BattleEngine(factory(),factory);
            battle.BeginCampaign(); return battle;
        }
        static BattleState Build(StageData stage, ProgressData progress, int? seed)
        {
            if(stage.Party == null || stage.Party.Length == 0 || stage.Party.Length > PrototypeData.PartyCapacity || stage.Party.Distinct().Count() != stage.Party.Length)
                throw new InvalidOperationException("Campaign party must have one to four distinct allies.");
            if(stage.EnemyCount < 1 || stage.EnemyCount > 3)
                throw new InvalidOperationException("Campaign encounters must have one to three enemies.");
            var state = PrototypeData.CreateBattle(progress, seed);
            state.StageId = stage.Id; state.StageName = stage.Number + "  " + stage.Title;
            state.IsBoss = stage.IsBoss;
            state.Allies = stage.Party.Select(id => PrototypeData.CreateCharacter(id)).ToList();
            BattleRules.ApplyActionProgression(state);
            if(progress != null)new ProgressService(progress).ApplyToBattle(state);
            var enemy=state.Enemies[0];enemy.Name=stage.EnemyName;enemy.Portrait=stage.EnemyArt;enemy.IsHuman=stage.HumanEnemy;
            if(stage.Id=="chapter1_beacon_defense")return state; // Preserve the previously delivered fight and reward identity.
            for(int i=1;i<stage.EnemyCount;i++)state.Enemies.Add(new UnitState {
                Id="enemy_"+(i+1), Name=stage.HumanEnemy?"호위 경비병 "+i:"그림자 군체 "+i,
                Portrait=stage.HumanEnemy?"HumanGuard":"Anger", IsEnemy=true, IsHuman=stage.HumanEnemy,
                Parts=new List<PartState>{new PartState{Id="enemy_"+(i+1)+"_arm"},new PartState{Id="enemy_"+(i+1)+"_core"}}
            });
            foreach(var foe in state.Enemies){
                foe.IntentProfile=foe==enemy?stage.BattleProfile:"skirmish";
                foe.Element=stage.HumanEnemy?Element.Frost:Element.Shadow;
                int hp=stage.IsBoss?260+stage.Chapter*20:stage.EnemyCount>1?48+stage.Chapter*7:105+stage.Chapter*22;
                if(state.Allies.Count==1)hp=65;
                else if(state.Allies.Count==2)hp=Math.Min(hp,55);
                foe.Hp=foe.MaxHp=hp;foe.Attack=stage.EnemyCount>1?7:9;foe.Defense=1;foe.Speed=11;
                foe.Rift=foe.MaxRift=stage.IsBoss?100:stage.EnemyCount>1?32+stage.Chapter*3:55+stage.Chapter*5;
                foe.Parts[0].Hp=foe.Parts[0].MaxHp=stage.IsBoss?48:26;
                foe.Parts[1].Hp=foe.Parts[1].MaxHp=stage.IsBoss?64:40;
                foe.Parts[0].Name=stage.HumanEnemy?"무기":"팔";
                foe.Parts[1].Name=stage.HumanEnemy?"지휘 장비":"핵";
                if(foe.Portrait=="Eve"){foe.Parts[0].Name="손종";foe.Parts[1].Name="기록 봉인";}
                if(foe.Portrait=="Ellen"){foe.Parts[0].Name="무기";foe.Parts[1].Name="인장 장비";}
            }
            state.Gloom=stage.BattleProfile=="illusion"?2:0;
            state.Ember=4;
            state.CoinReward=stage.IsBoss?120:40;state.FirstLanternReward=stage.IsBoss?10:2;
            state.RepeatLanternReward=1;state.MemoryReward=stage.IsBoss?1:0;
            return state;
        }
    }
}
