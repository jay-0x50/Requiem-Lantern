using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RequiemLantern.Core
{
    [Serializable] public sealed class RecurringProgress
    {
        public string DailyKey = "", WeeklyKey = "";
        public bool DailyCheckedIn;
        public int DailyVictories, DailyClashes, DailyDefenses;
        public int WeeklyVictories, WeeklyClashes, WeeklyDefenses, WeeklyDungeonRewardsClaimed;
        public List<string> DailyClaimedQuestIds = new List<string>(), WeeklyClaimedQuestIds = new List<string>();
    }
    [Serializable] public sealed class ActivityReward
    {
        public int Stardust, Coins, LanternFragments, MemoryFragments, CharacterShards;
        public string Summary { get {
            var parts=new List<string>();if(Stardust>0)parts.Add("별가루 +"+Stardust);if(Coins>0)parts.Add("코인 +"+Coins);
            if(LanternFragments>0)parts.Add("등불 조각 +"+LanternFragments);if(MemoryFragments>0)parts.Add("기억의 파편 +"+MemoryFragments);
            if(CharacterShards>0)parts.Add("공용 조각 +"+CharacterShards);return parts.Count==0?"보상 없음":string.Join(" / ",parts.ToArray());
        } }
    }
    [Serializable] public sealed class QuestSnapshot
    {
        public string Id, Title, Description, PeriodKey;
        public int Current, Target;
        public bool Claimed, CanClaim;
        public ActivityReward Reward;
    }
    [Serializable] public sealed class ActivitySnapshot
    {
        public string DailyKey, WeeklyKey, UnlockDescription;
        public DateTime NextDailyResetUtc, NextWeeklyResetUtc;
        public List<QuestSnapshot> DailyQuests = new List<QuestSnapshot>(), WeeklyQuests = new List<QuestSnapshot>();
        public int WeeklyDungeonRewardsClaimed, WeeklyDungeonRewardLimit = 3;
        public bool DungeonUnlocked;
        public List<DungeonDefinition> Dungeons = new List<DungeonDefinition>();
    }
    [Serializable] public sealed class DungeonDefinition
    {
        public string Id, Name, Description, DifficultyLabel;
        public int EnemyCount;
        public bool IsBoss;
        public ActivityReward Reward;
    }

    public static class DungeonCatalog
    {
        public const int WeeklyRewardLimit = 3;
        public static readonly IReadOnlyList<DungeonDefinition> Definitions = new List<DungeonDefinition> {
            new DungeonDefinition { Id="coast", Name="표류물 인양", DifficultyLabel="쉬움", EnemyCount=2,
                Description="검은 해안의 표류물을 회수합니다. 두 그림자의 예고선을 나누어 막아 보세요.",
                Reward=new ActivityReward { Stardust=80, Coins=500, LanternFragments=5, CharacterShards=5 } },
            new DungeonDefinition { Id="reef", Name="침묵의 암초", DifficultyLabel="보통", EnemyCount=3,
                Description="암초에 남은 세 그림자를 걷어 냅니다. 동료를 대신하는 맞불이 중요합니다.",
                Reward=new ActivityReward { Stardust=100, Coins=700, LanternFragments=7, CharacterShards=8 } },
            new DungeonDefinition { Id="watch", Name="검은 항로의 파수꾼", DifficultyLabel="어려움", EnemyCount=1, IsBoss=true,
                Description="귀항로를 막은 파수꾼과 싸웁니다. 연속 예고와 부위 파괴에 대비하세요.",
                Reward=new ActivityReward { Stardust=120, Coins=1000, LanternFragments=10, MemoryFragments=1, CharacterShards=10 } }
        }.AsReadOnly();
        public static DungeonDefinition Get(string id) { return Definitions.FirstOrDefault(value=>value.Id==id); }
        internal static bool IsUnlocked(ProgressData data)
        { return CampaignRules.IsCleared(data,CampaignCatalog.Chapters.SelectMany(chapter=>chapter.Stages).First(stage=>stage.Number=="1-2")); }
        internal static BattleEngine Create(DungeonDefinition dungeon, ProgressData progress,int? seed=null)
        {
            Func<BattleState> factory = ()=>Build(dungeon,progress,seed);
            var battle = new BattleEngine(factory(),factory); battle.BeginCampaign(); return battle;
        }
        static BattleState Build(DungeonDefinition dungeon, ProgressData progress,int? seed)
        {
            var state=PrototypeData.CreateBattle(progress,seed);
            state.StageId="weekly_"+dungeon.Id;state.StageName="잔향 회수 · "+dungeon.Name;state.IsBoss=dungeon.IsBoss;
            state.CoinReward=state.FirstLanternReward=state.RepeatLanternReward=state.MemoryReward=0;
            state.Ember=4;state.Gloom=1;state.Enemies.Clear();
            int hp=dungeon.Id=="coast"?65:dungeon.Id=="reef"?80:300;
            for(int i=0;i<dungeon.EnemyCount;i++){
                string id=i==0?"anger":"enemy_"+(i+1);
                var enemy=new UnitState { Id=id,Name=dungeon.IsBoss?"검은 항로의 파수꾼":"표류한 그림자 "+(i+1),
                    Portrait="Anger",Role="잔향 회수",IsEnemy=true,Element=dungeon.Id=="reef"?Element.Frost:Element.Shadow,
                    Hp=hp,MaxHp=hp,Attack=dungeon.IsBoss?9:7,Defense=1,Speed=11,
                    Rift=dungeon.IsBoss?90:38,MaxRift=dungeon.IsBoss?90:38,IntentProfile=dungeon.IsBoss?"command":"skirmish" };
                enemy.Parts.Add(new PartState { Id=id+"_arm",Name="팔",Hp=dungeon.IsBoss?48:26,MaxHp=dungeon.IsBoss?48:26 });
                enemy.Parts.Add(new PartState { Id=id+"_core",Name="핵",Hp=dungeon.IsBoss?64:40,MaxHp=dungeon.IsBoss?64:40 });
                state.Enemies.Add(enemy);
            }
            BattleRules.ApplyActionProgression(state);return state;
        }
    }

    // A session owns its engine. No request supplies victory, action counts, or rewards.
    public sealed class WeeklyDungeonSession
    {
        public DungeonDefinition Active { get; private set; }
        public BattleEngine Battle { get; private set; }
        public bool Practice { get; private set; }
        public bool Enter(string id,ProgressService progress,bool practice,out string error)
        {
            error=null;
            if(Active!=null){error="진행 중인 잔향 회수를 마치거나 퇴각해 주세요.";return false;}
            var dungeon=DungeonCatalog.Get(id);
            if(dungeon==null){error="잔향 회수 항로를 찾지 못했습니다.";return false;}
            if(!DungeonCatalog.IsUnlocked(progress.Data)){error="본편 1-2의 전투와 뒷이야기를 마치면 잔향 회수가 열립니다.";return false;}
            var activities=progress.PeekActivities();
            if(!practice&&activities.WeeklyDungeonRewardsClaimed>=DungeonCatalog.WeeklyRewardLimit)
            {error="이번 주 공유 보상 3회를 모두 받았습니다. 연습 출격을 선택해 주세요.";return false;}
            var battle=DungeonCatalog.Create(dungeon,progress.Data);
            Active=dungeon;Battle=battle;Practice=practice;return true;
        }
        public void Restart(ProgressService progress)
        {
            if(Active==null)throw new InvalidOperationException("진행 중인 잔향 회수가 없습니다.");
            Battle=DungeonCatalog.Create(Active,progress.Data);
        }
        public bool ClaimVictory(ProgressService progress,out ActivityReward reward,out string error)
        {
            reward=null;error=null;
            if(Active==null||Battle==null||Battle.State.StageId!="weekly_"+Active.Id||Battle.State.Outcome!=BattleOutcome.Victory)
            {error="이 항로의 전투에서 승리해야 합니다.";return false;}
            return progress.ClaimDungeonVictory(Battle,Active,Practice,out reward,out error);
        }
        public void Exit() { Active=null;Battle=null;Practice=false; }
    }

    internal static class RecurringRules
    {
        internal static readonly string[] DailyIds={"daily_checkin","daily_victory","daily_clash","daily_defense"};
        internal static readonly string[] WeeklyIds={"weekly_victory","weekly_clash","weekly_defense","weekly_dungeon"};
        internal static void Initialize(ProgressData data)
        {
            if(data.Activities==null)data.Activities=new RecurringProgress();
            data.Activities.DailyKey=data.Activities.DailyKey??"";data.Activities.WeeklyKey=data.Activities.WeeklyKey??"";
            data.Activities.DailyClaimedQuestIds=data.Activities.DailyClaimedQuestIds??new List<string>();
            data.Activities.WeeklyClaimedQuestIds=data.Activities.WeeklyClaimedQuestIds??new List<string>();
        }
        internal static RecurringProgress Copy(RecurringProgress source)
        {
            return new RecurringProgress { DailyKey=source.DailyKey,WeeklyKey=source.WeeklyKey,DailyCheckedIn=source.DailyCheckedIn,
                DailyVictories=source.DailyVictories,DailyClashes=source.DailyClashes,DailyDefenses=source.DailyDefenses,
                WeeklyVictories=source.WeeklyVictories,WeeklyClashes=source.WeeklyClashes,WeeklyDefenses=source.WeeklyDefenses,
                WeeklyDungeonRewardsClaimed=source.WeeklyDungeonRewardsClaimed,
                DailyClaimedQuestIds=new List<string>(source.DailyClaimedQuestIds),WeeklyClaimedQuestIds=new List<string>(source.WeeklyClaimedQuestIds) };
        }
        internal static bool Refresh(RecurringProgress data,DateTime utc)
        {
            var localDay=utc.AddHours(4).Date;var monday=localDay.AddDays(-((int)localDay.DayOfWeek+6)%7);
            string day=Key(localDay),week=Key(monday);bool changed=false;
            // The saved period keys are high-water marks. A backwards clock never
            // reopens a paid period; reads within a period do not mutate the save.
            if(string.CompareOrdinal(day,data.DailyKey)>0){
                data.DailyKey=day;data.DailyCheckedIn=false;data.DailyVictories=data.DailyClashes=data.DailyDefenses=0;
                data.DailyClaimedQuestIds.Clear();changed=true;
            }
            if(string.CompareOrdinal(week,data.WeeklyKey)>0){
                data.WeeklyKey=week;data.WeeklyVictories=data.WeeklyClashes=data.WeeklyDefenses=data.WeeklyDungeonRewardsClaimed=0;
                data.WeeklyClaimedQuestIds.Clear();changed=true;
            }
            return changed;
        }
        internal static string Key(DateTime value) { return value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture); }
        internal static DateTime Parse(string value)
        { return DateTime.SpecifyKind(DateTime.ParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture),DateTimeKind.Utc); }
        internal static DateTime NextReset(string key,int days) { return Parse(key).AddDays(days).AddHours(-4); }
        internal static bool Valid(RecurringProgress data)
        {
            if(data==null||data.DailyClaimedQuestIds==null||data.WeeklyClaimedQuestIds==null)return false;
            DateTime day,week;
            if(data.DailyKey!=""&&(!DateTime.TryParseExact(data.DailyKey,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day)||day.Year==9999))return false;
            if(data.WeeklyKey!=""&&(!DateTime.TryParseExact(data.WeeklyKey,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out week)||week.DayOfWeek!=DayOfWeek.Monday||week.Year==9999))return false;
            if(data.DailyVictories<0||data.DailyVictories>1||data.DailyClashes<0||data.DailyClashes>3||data.DailyDefenses<0||data.DailyDefenses>2
                ||data.WeeklyVictories<0||data.WeeklyVictories>10||data.WeeklyClashes<0||data.WeeklyClashes>20||data.WeeklyDefenses<0||data.WeeklyDefenses>8
                ||data.WeeklyDungeonRewardsClaimed<0||data.WeeklyDungeonRewardsClaimed>DungeonCatalog.WeeklyRewardLimit)return false;
            if(data.DailyClaimedQuestIds.Distinct().Count()!=data.DailyClaimedQuestIds.Count||data.WeeklyClaimedQuestIds.Distinct().Count()!=data.WeeklyClaimedQuestIds.Count
                ||data.DailyClaimedQuestIds.Any(id=>!DailyIds.Contains(id))||data.WeeklyClaimedQuestIds.Any(id=>!WeeklyIds.Contains(id)))return false;
            if(data.DailyKey==""&&(data.DailyCheckedIn||data.DailyVictories+data.DailyClashes+data.DailyDefenses+data.DailyClaimedQuestIds.Count>0))return false;
            if(data.WeeklyKey==""&&(data.WeeklyVictories+data.WeeklyClashes+data.WeeklyDefenses+data.WeeklyDungeonRewardsClaimed+data.WeeklyClaimedQuestIds.Count>0))return false;
            return QuestList(data,false).Concat(QuestList(data,true)).All(quest=>!quest.Claimed||quest.Current>=quest.Target);
        }
        internal static List<QuestSnapshot> QuestList(RecurringProgress data,bool weekly)
        {
            if(!weekly)return new List<QuestSnapshot> {
                Quest(data,false,"daily_checkin","등대에 인사","거점이나 의뢰 게시판을 방문하기",data.DailyCheckedIn?1:0,1,20,100,1),
                Quest(data,false,"daily_victory","오늘의 항해","본편 또는 보상 출격에서 1회 승리 보상 받기",data.DailyVictories,1,40,150,2),
                Quest(data,false,"daily_clash","빛을 맞대다","승리 보상을 받은 전투에서 맞불을 3회 수행하기",data.DailyClashes,3,40,200,2),
                Quest(data,false,"daily_defense","서로의 방패","승리 보상을 받은 전투에서 방어·회피·반격을 2회 실행하기",data.DailyDefenses,2,60,250,2)
            };
            return new List<QuestSnapshot> {
                Quest(data,true,"weekly_victory","일주일의 항로","본편 또는 보상 출격에서 10회 승리 보상 받기",data.WeeklyVictories,10,200,1200,10),
                Quest(data,true,"weekly_clash","꺼지지 않는 맞불","승리 보상을 받은 전투에서 맞불을 20회 수행하기",data.WeeklyClashes,20,160,800,5,1),
                Quest(data,true,"weekly_defense","돌아올 자리를 지키며","승리 보상을 받은 전투에서 방어·회피·반격을 8회 실행하기",data.WeeklyDefenses,8,120,600,5,0,10),
                Quest(data,true,"weekly_dungeon","잔향 회수대","잔향 회수의 공유 주간 보상을 3회 받기",data.WeeklyDungeonRewardsClaimed,3,160,1400,0,2,20)
            };
        }
        static QuestSnapshot Quest(RecurringProgress data,bool weekly,string id,string title,string description,int current,int target,int stars,int coins,int lantern,int memory=0,int shards=0)
        {
            bool claimed=(weekly?data.WeeklyClaimedQuestIds:data.DailyClaimedQuestIds).Contains(id);
            return new QuestSnapshot { Id=id,Title=title,Description=description,Current=current,Target=target,Claimed=claimed,CanClaim=!claimed&&current>=target,
                PeriodKey=weekly?data.WeeklyKey:data.DailyKey,Reward=new ActivityReward { Stardust=stars,Coins=coins,LanternFragments=lantern,MemoryFragments=memory,CharacterShards=shards } };
        }
    }

    public sealed partial class ProgressService
    {
        DateTime ActivityNow()
        {
            var value=utcNow();return value.Kind==DateTimeKind.Local?value.ToUniversalTime():DateTime.SpecifyKind(value,DateTimeKind.Utc);
        }
        internal RecurringProgress PeekActivities()
        { var pending=RecurringRules.Copy(Data.Activities);RecurringRules.Refresh(pending,ActivityNow());return pending; }
        public ActivitySnapshot GetActivities()
        {
            RecurringRules.Refresh(Data.Activities,ActivityNow());var value=Data.Activities;
            return new ActivitySnapshot { DailyKey=value.DailyKey,WeeklyKey=value.WeeklyKey,
                NextDailyResetUtc=RecurringRules.NextReset(value.DailyKey,1),NextWeeklyResetUtc=RecurringRules.NextReset(value.WeeklyKey,7),
                DailyQuests=RecurringRules.QuestList(value,false),WeeklyQuests=RecurringRules.QuestList(value,true),
                WeeklyDungeonRewardsClaimed=value.WeeklyDungeonRewardsClaimed,WeeklyDungeonRewardLimit=DungeonCatalog.WeeklyRewardLimit,
                Dungeons=DungeonCatalog.Definitions.ToList(),DungeonUnlocked=DungeonCatalog.IsUnlocked(Data),UnlockDescription="본편 1-2의 전투와 뒷이야기 완료 후 개방" };
        }
        public bool CheckIn()
        {
            var pending=PeekActivities();if(pending.DailyCheckedIn)return false;
            pending.DailyCheckedIn=true;Data.Activities=pending;return true;
        }
        public bool ClaimQuest(string questId,string periodKey,out ActivityReward reward,out string error)
        {
            reward=null;error=null;var pending=PeekActivities();
            var quest=RecurringRules.QuestList(pending,false).Concat(RecurringRules.QuestList(pending,true)).FirstOrDefault(value=>value.Id==questId);
            if(quest==null){error="의뢰를 찾지 못했습니다.";return false;}
            if(string.IsNullOrEmpty(periodKey)||quest.PeriodKey!=periodKey){error="의뢰 기간이 바뀌었습니다. 게시판을 새로 확인해 주세요.";return false;}
            if(quest.Claimed){error="이미 받은 의뢰 보상입니다.";return false;}
            if(!quest.CanClaim){error="의뢰 목표를 먼저 완료해 주세요.";return false;}
            if(!CanAdd(quest.Reward)){error="재화 보관 한도를 초과하여 보상을 받지 않았습니다.";return false;}
            (questId.StartsWith("daily_",StringComparison.Ordinal)?pending.DailyClaimedQuestIds:pending.WeeklyClaimedQuestIds).Add(questId);
            Add(quest.Reward);Data.Activities=pending;reward=quest.Reward;return true;
        }
        internal RecurringProgress VictoryActivities(BattleEngine battle,bool dungeon)
        {
            var pending=PeekActivities();pending.DailyVictories=1;pending.WeeklyVictories=Math.Min(10,pending.WeeklyVictories+1);
            pending.DailyClashes=AddCapped(pending.DailyClashes,battle.ResolvedClashes,3);pending.WeeklyClashes=AddCapped(pending.WeeklyClashes,battle.ResolvedClashes,20);
            pending.DailyDefenses=AddCapped(pending.DailyDefenses,battle.ResolvedDefenses,2);pending.WeeklyDefenses=AddCapped(pending.WeeklyDefenses,battle.ResolvedDefenses,8);
            if(dungeon)pending.WeeklyDungeonRewardsClaimed++;return pending;
        }
        static int AddCapped(int current,int addition,int maximum) { return (int)Math.Min(maximum,(long)current+Math.Max(0,addition)); }
        internal bool ClaimDungeonVictory(BattleEngine battle,DungeonDefinition dungeon,bool practice,out ActivityReward reward,out string error)
        {
            reward=null;error=null;
            if(Data.ClaimedBattles.Contains(battle.State.BattleId)){error="이 전투의 결과는 이미 처리했습니다.";return false;}
            var pending=PeekActivities();
            if(!practice&&pending.WeeklyDungeonRewardsClaimed>=DungeonCatalog.WeeklyRewardLimit)
            {error="이번 주 공유 보상 3회를 모두 받았습니다. 퇴각 후 연습 출격을 이용해 주세요.";return false;}
            var payout=practice?new ActivityReward():new ActivityReward { Stardust=dungeon.Reward.Stardust,Coins=dungeon.Reward.Coins,
                LanternFragments=dungeon.Reward.LanternFragments,MemoryFragments=dungeon.Reward.MemoryFragments,CharacterShards=dungeon.Reward.CharacterShards };
            if(!CanAdd(payout)){error="재화 보관 한도를 초과하여 보상을 받지 않았습니다.";return false;}
            if(!practice)pending=VictoryActivities(battle,true);
            Add(payout);Data.ClaimedBattles.Add(battle.State.BattleId);Data.Activities=pending;reward=payout;return true;
        }
        bool CanAdd(ActivityReward value)
        {
            return Data.Stardust<=int.MaxValue-value.Stardust&&Data.Coins<=int.MaxValue-value.Coins&&Data.LanternFragments<=int.MaxValue-value.LanternFragments
                &&Data.MemoryFragments<=int.MaxValue-value.MemoryFragments&&Data.CharacterShards<=int.MaxValue-value.CharacterShards;
        }
        void Add(ActivityReward value)
        { Data.Stardust+=value.Stardust;Data.Coins+=value.Coins;Data.LanternFragments+=value.LanternFragments;Data.MemoryFragments+=value.MemoryFragments;Data.CharacterShards+=value.CharacterShards; }
    }
}
