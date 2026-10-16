using System;
using System.Collections.Generic;
using System.Linq;

namespace RequiemLantern.Core
{
    [Serializable] public sealed class SyncMaterialStack
    {
        public string CharacterId;
        public int Count;
    }
    [Serializable] public class SyncMaterialDefinition
    {
        public string CharacterId, MaterialName, Description, Portrait;
        public int RequiredCount, DropCount = 3;
    }
    [Serializable] public sealed class SyncMaterialEntry : SyncMaterialDefinition
    {
        public bool Owned, Synchronized, CanSynchronize;
        public int Count;
    }
    [Serializable] public sealed class MaterialSnapshot
    {
        public bool Unlocked;
        public string UnlockDescription;
        public List<SyncMaterialEntry> Entries = new List<SyncMaterialEntry>();
    }
    [Serializable] public sealed class MaterialReward
    {
        public string CharacterId, MaterialName;
        public int Count;
    }
    public static class MaterialCatalog
    {
        public const string TestCouponCode = "LANTERN-SYNC-MATERIALS";
        public static readonly IReadOnlyList<SyncMaterialDefinition> Definitions = new List<SyncMaterialDefinition> {
            Definition("lian","파수꾼의 심지","오래 지켜 온 불의 흔적. 리안의 파편과 공명하는 심지입니다."),
            Definition("sera","잿빛 맹세","잿더미 속에서도 남은 온기. 세라의 파편과 공명하는 결정입니다."),
            Definition("noa","이름 없는 인장","지워진 글자의 자국이 남은 인장. 노아의 파편과 공명합니다."),
            Definition("minato","해류 유리구슬","빛을 담아 잔잔하게 흐르는 구슬. 미나토의 파편과 공명합니다."),
            Definition("heidel","서리 렌즈","차가운 빛을 모으는 관측 렌즈. 하이델의 파편과 공명합니다."),
            Definition("kai","귀항 나침반","돌아올 방향을 가리키는 작은 나침반. 카이의 파편과 공명합니다."),
            Definition("mare","붉은 인양 고리","구조줄에 매인 붉은 고리. 마레의 파편과 공명합니다.")
        }.AsReadOnly();
        static SyncMaterialDefinition Definition(string id,string name,string description)
        {
            var character=RecruitmentCatalog.Character(id);
            return new SyncMaterialDefinition { CharacterId=id,MaterialName=name,Description=description,Portrait=character.Portrait,RequiredCount=character.Rarity==5?10:6 };
        }
        public static SyncMaterialDefinition Get(string characterId) { return Definitions.FirstOrDefault(value=>value.CharacterId==characterId); }
    }

    public sealed partial class ProgressService
    {
        internal static void InitializeSyncMaterials(ProgressData data)
        { if(data.SyncMaterials==null)data.SyncMaterials=new List<SyncMaterialStack>(); }
        internal static bool ValidSyncMaterials(ProgressData data)
        {
            return data.SyncMaterials!=null&&data.SyncMaterials.All(value=>value!=null&&MaterialCatalog.Get(value.CharacterId)!=null&&value.Count>=0)
                &&data.SyncMaterials.Select(value=>value.CharacterId).Distinct().Count()==data.SyncMaterials.Count;
        }
        int MaterialCount(string id)
        { var stack=Data.SyncMaterials.FirstOrDefault(value=>value.CharacterId==id);return stack==null?0:stack.Count; }
        void AddMaterial(string id,int count)
        {
            var stack=Data.SyncMaterials.FirstOrDefault(value=>value.CharacterId==id);
            if(stack==null){stack=new SyncMaterialStack { CharacterId=id };Data.SyncMaterials.Add(stack);}stack.Count+=count;
        }
        public MaterialSnapshot GetSynchronizationMaterials()
        {
            return new MaterialSnapshot { Unlocked=DungeonCatalog.IsUnlocked(Data),UnlockDescription="본편 1-2의 전투와 뒷이야기 완료 후 개방",
                Entries=MaterialCatalog.Definitions.Select(definition=>{
                    bool owned=IsOwned(definition.CharacterId),synchronized=GetCharacter(definition.CharacterId).SyncLevel>=1;int count=MaterialCount(definition.CharacterId);
                    return new SyncMaterialEntry { CharacterId=definition.CharacterId,MaterialName=definition.MaterialName,Description=definition.Description,
                        Portrait=definition.Portrait,RequiredCount=definition.RequiredCount,DropCount=definition.DropCount,Owned=owned,Count=count,Synchronized=synchronized,
                        CanSynchronize=owned&&!synchronized&&count>=definition.RequiredCount&&Data.LanternFragments>=SynchronizationCost };
                }).ToList() };
        }
        internal bool ClaimMaterialVictory(BattleEngine battle,SyncMaterialDefinition material,out MaterialReward reward,out string error)
        {
            reward=null;error=null;
            if(Data.ClaimedBattles.Contains(battle.State.BattleId)){error="이 전투의 회수 보상은 이미 받았습니다.";return false;}
            if(MaterialCount(material.CharacterId)>int.MaxValue-material.DropCount){error="전용 재료 보관 한도를 초과하여 보상을 받지 않았습니다.";return false;}
            var activities=VictoryActivities(battle,false);
            AddMaterial(material.CharacterId,material.DropCount);Data.ClaimedBattles.Add(battle.State.BattleId);Data.Activities=activities;
            reward=new MaterialReward { CharacterId=material.CharacterId,MaterialName=material.MaterialName,Count=material.DropCount };return true;
        }
    }

    // This independent session owns its battle and only grants the selected
    // character's material. Save failures can restore ProgressData and retry it.
    public sealed class MaterialDungeonSession
    {
        public SyncMaterialDefinition Active { get; private set; }
        public BattleEngine Battle { get; private set; }
        public bool Enter(string characterId,ProgressService progress,out string error)
        {
            error=null;
            if(Active!=null){error="진행 중인 기억 잠항을 마치거나 퇴각해 주세요.";return false;}
            var material=MaterialCatalog.Get(characterId);
            if(material==null){error="기억 잠항의 전용 재료를 선택해 주세요.";return false;}
            if(!DungeonCatalog.IsUnlocked(progress.Data)){error="본편 1-2의 전투와 뒷이야기를 마치면 기억 잠항이 열립니다.";return false;}
            var battle=Create(material,progress.Data);Active=material;Battle=battle;return true;
        }
        public void Restart(ProgressService progress)
        {
            if(Active==null)throw new InvalidOperationException("진행 중인 기억 잠항이 없습니다.");
            Battle=Create(Active,progress.Data);
        }
        public bool ClaimVictory(ProgressService progress,out MaterialReward reward,out string error)
        {
            reward=null;error=null;
            if(Active==null||Battle==null||Battle.State.StageId!="material_"+Active.CharacterId||Battle.State.Outcome!=BattleOutcome.Victory)
            {error="이 기억 잠항의 전투에서 승리해야 합니다.";return false;}
            return progress.ClaimMaterialVictory(Battle,Active,out reward,out error);
        }
        public void Exit() { Active=null;Battle=null; }
        internal static BattleEngine Create(SyncMaterialDefinition material,ProgressData progress,int? seed=null)
        {
            Func<BattleState> factory=()=>Build(material,progress,seed);
            var battle=new BattleEngine(factory(),factory);battle.BeginCampaign();return battle;
        }
        static BattleState Build(SyncMaterialDefinition material,ProgressData progress,int? seed)
        {
            var state=PrototypeData.CreateBattle(progress,seed);state.StageId="material_"+material.CharacterId;state.StageName="기억 잠항 · "+material.MaterialName;
            state.IsBoss=false;state.CoinReward=state.FirstLanternReward=state.RepeatLanternReward=state.MemoryReward=0;
            state.Ember=4;state.Gloom=1;state.Enemies.Clear();
            for(int i=0;i<2;i++){
                string id=i==0?"anger":"enemy_2";
                var enemy=new UnitState { Id=id,Name="기억의 그림자 "+(i+1),Portrait="Anger",Role="기억 잠항",IsEnemy=true,
                    Element=Element.Shadow,Hp=60,MaxHp=60,Attack=7,Defense=1,Speed=11,Rift=36,MaxRift=36,IntentProfile="skirmish" };
                enemy.Parts.Add(new PartState { Id=id+"_arm",Name="팔",Hp=26,MaxHp=26 });
                enemy.Parts.Add(new PartState { Id=id+"_core",Name="핵",Hp=40,MaxHp=40 });state.Enemies.Add(enemy);
            }
            BattleRules.ApplyActionProgression(state);return state;
        }
    }
}
