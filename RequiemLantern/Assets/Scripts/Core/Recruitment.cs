using System;
using System.Collections.Generic;
using System.Linq;

namespace RequiemLantern.Core
{
    [Serializable] public sealed class RecruitCharacter
    {
        public string Id, Name, Portrait, Role;
        public int Rarity;
        public bool Recruitable = true;
    }
    [Serializable] public sealed class RecruitBanner
    {
        public string Id, Name, FeaturedCharacterId;
    }
    [Serializable] public sealed class RecruitmentPull
    {
        public string CharacterId, Name;
        public int Rarity, DuplicateShards, PityAfter;
        public bool IsPickup, IsNew, GuaranteedAfter;
    }
    [Serializable] public sealed class RecruitmentResult
    {
        public string BannerId;
        public int Cost;
        public List<RecruitmentPull> Pulls = new List<RecruitmentPull>();
    }
    [Serializable] public sealed class CouponReward
    {
        public string Code;
        public int Stardust, Coins, LanternFragments, MemoryFragments, CharacterShards;
        public List<SyncMaterialStack> SyncMaterials;
    }
    public static class RecruitmentCatalog
    {
        public const int CollectionVersion = 2, SingleCost = 160, TenCost = 1600, HardPity = 80;
        public const int DuplicateFourStarShards = 20, DuplicateFiveStarShards = 100, TestAmount = 9999999;
        public const double FiveStarRate = .008;
        public const string TestCurrencyCode = "LANTERN-TEST-9999999", TestMaterialsCode = "LANTERN-MATERIALS";
        public static readonly IReadOnlyList<RecruitCharacter> Characters = new List<RecruitCharacter> {
            new RecruitCharacter{Id="lian",Name="리안",Rarity=5,Portrait="Lian",Role="광휘 · 수호자"},
            new RecruitCharacter{Id="sera",Name="세라",Rarity=5,Portrait="Sera",Role="화염 · 돌격수"},
            new RecruitCharacter{Id="noa",Name="노아",Rarity=5,Portrait="Noa",Role="그림자 · 조율사"},
            new RecruitCharacter{Id="minato",Name="미나토",Rarity=4,Portrait="Minato",Role="물결 · 치유사"},
            new RecruitCharacter{Id="heidel",Name="하이델",Rarity=4,Portrait="Heidel",Role="서리 · 사수"},
            new RecruitCharacter{Id="kai",Name="카이",Rarity=4,Portrait="Kai",Role="광휘 · 등불지기",Recruitable=false},
            new RecruitCharacter{Id="mare",Name="마레",Rarity=4,Portrait="Mare",Role="화염 · 인양꾼"}
        }.AsReadOnly();
        public static readonly IReadOnlyList<RecruitBanner> Banners = new List<RecruitBanner> {
            new RecruitBanner{Id="lian",Name="마지막 파수꾼",FeaturedCharacterId="lian"},
            new RecruitBanner{Id="sera",Name="꺼지지 않는 약속",FeaturedCharacterId="sera"},
            new RecruitBanner{Id="noa",Name="되찾은 이름",FeaturedCharacterId="noa"}
        }.AsReadOnly();
        public static readonly IReadOnlyList<string> StarterParty = Array.AsReadOnly(new[]{"lian","minato","heidel","mare"});
        public static RecruitCharacter Character(string id) { return Characters.FirstOrDefault(character=>character.Id==id); }
        public static RecruitBanner Banner(string id) { return Banners.FirstOrDefault(banner=>banner.Id==id); }
        internal static void InitializeCollection(ProgressData data)
        {
            if(data.CollectionVersion==0){
                // Legacy growth entries were created merely by viewing/applying a
                // character. Only the explicit starter grant establishes ownership.
                data.OwnedCharacters=StarterParty.ToList();data.ActiveParty=StarterParty.ToList();
                data.RecruitPity=0;data.PickupGuaranteed=false;data.RedeemedCodes=new List<string>();
                data.RecruitRandomState=unchecked((uint)Guid.NewGuid().GetHashCode());
                if(data.RecruitRandomState==0)data.RecruitRandomState=0x6d2b79f5u;
                data.CollectionVersion=CollectionVersion;
            }
            data.OwnedCharacters=data.OwnedCharacters??new List<string>();
            data.ActiveParty=data.ActiveParty??new List<string>();data.RedeemedCodes=data.RedeemedCodes??new List<string>();
            if(data.CollectionVersion==1){
                // The provisional starter used the protagonist. Grant the new
                // companion once; preserve every existing ownership and party
                // slot, including Kai, as well as all progress and random state.
                if(!data.OwnedCharacters.Contains("mare"))data.OwnedCharacters.Add("mare");
                data.CollectionVersion=CollectionVersion;
            }
        }
        public static string NormalizeCode(string code)
        { return string.IsNullOrEmpty(code)?"":new string(code.Where(character=>!char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant(); }
    }

    // Recruitment advances only its persisted stream. A local state copy makes
    // failed/overflowing batches atomic, and tests can supply exact boundaries.
    public interface IRecruitRandom { double NextDouble(ref uint state); }
    public sealed class StoredRecruitRandom : IRecruitRandom
    {
        public double NextDouble(ref uint state)
        {
            if(state==0)throw new InvalidOperationException("Recruitment random state is not initialized.");
            state^=state<<13;state^=state>>17;state^=state<<5;return state/4294967296d;
        }
    }
    public sealed class RecruitmentService
    {
        public ProgressData Data { get; private set; }
        readonly IRecruitRandom random;
        public RecruitmentService(ProgressData data,IRecruitRandom randomSource=null)
        { Data=data??throw new ArgumentNullException("data");RecruitmentCatalog.InitializeCollection(Data);random=randomSource??new StoredRecruitRandom(); }
        double Roll(ref uint state)
        {
            double value=random.NextDouble(ref state);
            if(double.IsNaN(value)||double.IsInfinity(value)||value<0||value>=1)throw new InvalidOperationException("Invalid recruitment random sample.");
            return value;
        }
        public bool Draw(string bannerId,int count,out RecruitmentResult result,out string error)
        {
            result=null;error=null;var banner=RecruitmentCatalog.Banner(bannerId);
            if(banner==null){error="모집할 픽업 배너를 선택해 주세요.";return false;}
            if(count!=1&&count!=10){error="모집은 1회 또는 10회로 진행합니다.";return false;}
            int cost=count*RecruitmentCatalog.SingleCost;
            if(Data.Stardust<cost){error="모집에 별가루 "+cost+"개가 필요합니다.";return false;}
            if(Data.RecruitPity<0||Data.RecruitPity>=RecruitmentCatalog.HardPity||Data.RecruitRandomState==0||Data.CharacterShards<0)
            {error="모집 기록을 확인할 수 없습니다.";return false;}
            var owned=Data.OwnedCharacters.ToList();int pity=Data.RecruitPity,shards=Data.CharacterShards;bool guaranteed=Data.PickupGuaranteed;
            uint randomState=Data.RecruitRandomState;var pending=new RecruitmentResult{BannerId=banner.Id,Cost=cost};
            try{
                for(int index=0;index<count;index++){
                    // Three samples per pull keep the stored stream consistent
                    // across rarity, pity and guarantee branches.
                    double rarity=Roll(ref randomState),pickup=Roll(ref randomState),member=Roll(ref randomState);
                    bool fiveStar=pity+1>=RecruitmentCatalog.HardPity||rarity<RecruitmentCatalog.FiveStarRate;
                    RecruitCharacter character;
                    if(fiveStar){
                        bool featured=guaranteed||pickup<.5;
                        var pool=RecruitmentCatalog.Characters.Where(value=>value.Recruitable&&value.Rarity==5&&value.Id!=banner.FeaturedCharacterId).ToList();
                        character=featured?RecruitmentCatalog.Character(banner.FeaturedCharacterId):pool[(int)(member*pool.Count)];
                        guaranteed=!featured;pity=0;
                    }else{
                        var pool=RecruitmentCatalog.Characters.Where(value=>value.Recruitable&&value.Rarity==4).ToList();character=pool[(int)(member*pool.Count)];pity++;
                    }
                    bool isNew=!owned.Contains(character.Id);int duplicate=isNew?0:fiveStar?RecruitmentCatalog.DuplicateFiveStarShards:RecruitmentCatalog.DuplicateFourStarShards;
                    if(shards>int.MaxValue-duplicate){error="캐릭터 조각 보관 한도를 초과합니다.";return false;}
                    shards+=duplicate;if(isNew)owned.Add(character.Id);
                    pending.Pulls.Add(new RecruitmentPull{CharacterId=character.Id,Name=character.Name,Rarity=character.Rarity,IsPickup=fiveStar&&character.Id==banner.FeaturedCharacterId,
                        IsNew=isNew,DuplicateShards=duplicate,PityAfter=pity,GuaranteedAfter=guaranteed});
                }
            }catch(InvalidOperationException){error="모집 결과를 준비하지 못했습니다. 재화는 사용하지 않았습니다.";return false;}
            Data.Stardust-=cost;Data.CharacterShards=shards;Data.OwnedCharacters=owned;Data.RecruitPity=pity;Data.PickupGuaranteed=guaranteed;Data.RecruitRandomState=randomState;
            result=pending;return true;
        }
    }
}
