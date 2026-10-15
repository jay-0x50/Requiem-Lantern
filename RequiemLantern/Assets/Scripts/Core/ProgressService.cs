using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace RequiemLantern.Core
{
    [Serializable]
    public sealed class ProgressData
    {
        public int Version = 1;
        public string PlayerName = "카이";
        public bool PrologueCompleted;
        public int Coins, LanternFragments, MemoryFragments, KeepLevel = 1;
        public List<string> ClearedStages = new List<string>();
        public List<string> ClaimedBattles = new List<string>();
        public List<string> RewardedStages = new List<string>();
        public List<CharacterProgress> Characters = new List<CharacterProgress>();
        public int CollectionVersion, Stardust, CharacterShards, RecruitPity;
        public bool PickupGuaranteed;
        public uint RecruitRandomState;
        public List<string> OwnedCharacters = new List<string>(), ActiveParty = new List<string>(), RedeemedCodes = new List<string>();
    }

    [Serializable]
    public sealed class CharacterProgress
    {
        public string Id;
        public int Level = 1, SyncLevel;
    }

    [Serializable]
    public sealed class RewardData
    {
        public int Coins, LanternFragments, MemoryFragments;
        public bool FirstClear;
        public string Summary { get { return "코인 +" + Coins + " / 등불 조각 +" + LanternFragments + " / 기억의 파편 +" + MemoryFragments; } }
    }

    public sealed class ProgressService
    {
        public ProgressData Data { get; private set; }
        public const int SynchronizationCost = 10;
        readonly IRecruitRandom recruitRandom;
        public ProgressService(ProgressData data,IRecruitRandom randomSource=null)
        {
            Data = data ?? new ProgressData();
            recruitRandom=randomSource;
            Data.Characters = Data.Characters ?? new List<CharacterProgress>();
            Data.ClearedStages = Data.ClearedStages ?? new List<string>();
            Data.RewardedStages = Data.RewardedStages ?? new List<string>();
            Data.ClaimedBattles = Data.ClaimedBattles ?? new List<string>();
            RecruitmentCatalog.InitializeCollection(Data);
        }
        static ProgressData Clone(ProgressData data)
        {
            var serializer=new XmlSerializer(typeof(ProgressData));
            using(var stream=new MemoryStream()){
                serializer.Serialize(stream,data);stream.Position=0;return (ProgressData)serializer.Deserialize(stream);
            }
        }
        public ProgressData Capture() { return Clone(Data); }
        public void Restore(ProgressData snapshot)
        { if(snapshot==null)throw new ArgumentNullException("snapshot");Data=Clone(snapshot); }
        public bool IsOwned(string id) { return RecruitmentCatalog.Character(id)!=null&&Data.OwnedCharacters.Contains(id); }
        public bool SetParty(IEnumerable<string> ids,out string error)
        {
            error=null;var party=ids==null?null:ids.ToList();
            if(party==null||party.Count!=PrototypeData.PartyCapacity||party.Distinct().Count()!=party.Count||party.Any(id=>!IsOwned(id)))
            {error="보유한 서로 다른 잔영 네 명으로 편성해 주세요.";return false;}
            Data.ActiveParty=party;return true;
        }
        public bool EquipCharacter(int slot,string id,out string error)
        {
            error=null;
            if(slot<0||slot>=PrototypeData.PartyCapacity||Data.ActiveParty.Count!=PrototypeData.PartyCapacity||!IsOwned(id))
            {error="편성할 위치와 보유 잔영을 선택해 주세요.";return false;}
            var party=Data.ActiveParty.ToList();int previous=party.IndexOf(id);
            if(previous>=0)party[previous]=party[slot];party[slot]=id;return SetParty(party,out error);
        }
        public bool Recruit(string bannerId,int count,out RecruitmentResult result,out string error)
        { return new RecruitmentService(Data,recruitRandom).Draw(bannerId,count,out result,out error); }
        public bool RedeemCode(string input,out CouponReward reward,out string error)
        {
            reward=null;error=null;string code=RecruitmentCatalog.NormalizeCode(input);
            if(code!=RecruitmentCatalog.TestCurrencyCode&&code!=RecruitmentCatalog.TestMaterialsCode){error="사용할 수 없는 쿠폰입니다.";return false;}
            if(Data.RedeemedCodes.Contains(code)){error="이미 사용한 쿠폰입니다.";return false;}
            var pending=new CouponReward{Code=code};
            if(code==RecruitmentCatalog.TestCurrencyCode){pending.Stardust=RecruitmentCatalog.TestAmount;pending.Coins=RecruitmentCatalog.TestAmount;}
            else{pending.LanternFragments=pending.MemoryFragments=pending.CharacterShards=RecruitmentCatalog.TestAmount;}
            if(Data.Stardust>int.MaxValue-pending.Stardust||Data.Coins>int.MaxValue-pending.Coins||Data.LanternFragments>int.MaxValue-pending.LanternFragments
                ||Data.MemoryFragments>int.MaxValue-pending.MemoryFragments||Data.CharacterShards>int.MaxValue-pending.CharacterShards)
            {error="재료 보관 한도를 초과하여 쿠폰을 사용하지 않았습니다.";return false;}
            Data.Stardust+=pending.Stardust;Data.Coins+=pending.Coins;Data.LanternFragments+=pending.LanternFragments;Data.MemoryFragments+=pending.MemoryFragments;Data.CharacterShards+=pending.CharacterShards;
            Data.RedeemedCodes.Add(code);reward=pending;return true;
        }
        public CharacterProgress GetCharacter(string id)
        {
            if (!PrototypeData.IsCharacter(id)) return null;
            var value = Data.Characters.FirstOrDefault(x => x.Id == id);
            return value??new CharacterProgress { Id = id };
        }
        public bool Synchronize(string id, out string error)
        {
            error = null;
            if (!PrototypeData.IsCharacter(id)) { error = "동기화할 잔영을 선택해 주세요."; return false; }
            if (!IsOwned(id)) { error = "보유한 잔영만 동기화할 수 있습니다."; return false; }
            var existing = Data.Characters.FirstOrDefault(x => x.Id == id);
            if (existing != null && existing.SyncLevel >= 1) { error = "이미 동기화하여 3스킬을 개방했습니다."; return false; }
            if (Data.LanternFragments < SynchronizationCost) { error = "동기화에 등불 조각 " + SynchronizationCost + "개가 필요합니다."; return false; }
            var value = GetCharacter(id);if(existing==null)Data.Characters.Add(value);Data.LanternFragments -= SynchronizationCost; value.SyncLevel = 1; return true;
        }
        public void ApplyToBattle(BattleState state)
        {
            if (state == null) return;
            foreach (var unit in state.Allies)
            {
                var value = GetCharacter(unit.Id); if (value != null) unit.SyncLevel = value.SyncLevel;
                unit.Sanity = BattleRules.ClampSanity(unit.Sanity);
            }
        }
        public void MarkPrologueComplete() { Data.PrologueCompleted = true; }
        public bool ClaimVictory(BattleEngine battle, out RewardData reward, bool deferStageClear = false)
        {
            reward = null;
            if (battle.State.Outcome != BattleOutcome.Victory || Data.ClaimedBattles.Contains(battle.State.BattleId)) return false;
            bool first = !Data.ClearedStages.Contains(battle.State.StageId) && !Data.RewardedStages.Contains(battle.State.StageId);
            reward = new RewardData { Coins = battle.State.CoinReward, LanternFragments = first ? battle.State.FirstLanternReward : battle.State.RepeatLanternReward, MemoryFragments = first ? battle.State.MemoryReward : 0, FirstClear = first };
            Data.Coins += reward.Coins; Data.LanternFragments += reward.LanternFragments; Data.MemoryFragments += reward.MemoryFragments;
            Data.ClaimedBattles.Add(battle.State.BattleId);
            if (!Data.RewardedStages.Contains(battle.State.StageId)) Data.RewardedStages.Add(battle.State.StageId);
            if (!deferStageClear && !Data.ClearedStages.Contains(battle.State.StageId)) Data.ClearedStages.Add(battle.State.StageId);
            return true;
        }
        public bool RepairKeep(out string error)
        {
            error = null;
            if (Data.KeepLevel >= 3) { error = "프로토타입의 복구 범위는 3단계까지입니다."; return false; }
            int cost = Data.KeepLevel * 100;
            if (Data.Coins < cost) { error = "중앙 등탑 복구에 코인 " + cost + "개가 필요합니다."; return false; }
            Data.Coins -= cost; Data.KeepLevel++; return true;
        }
    }

    /// <summary>Versioned local XML with a previous-good backup, atomic replacement where supported,
    /// and a recoverable backup-first fallback on filesystems that prohibit ReplaceFile.</summary>
    public sealed class SaveStore
    {
        public readonly string Path;
        public string LastWarning { get; private set; }
        public SaveStore(string path) { Path = path; }
        public ProgressData Load()
        {
            LastWarning = null;
            if (!File.Exists(Path) && !File.Exists(Path + ".bak")) return new ProgressData();
            if (!File.Exists(Path))
            {
                try { LastWarning = "중단된 저장의 이전 파일을 복구했습니다."; return Read(Path + ".bak"); }
                catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is XmlException)
                { LastWarning = "이전 저장을 읽지 못했습니다. 기존 파일은 보존했습니다."; return new ProgressData(); }
            }
            try { return Read(Path); }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is XmlException)
            {
                LastWarning = "저장 파일을 읽지 못했습니다. 이전 저장을 확인합니다.";
                if (File.Exists(Path + ".bak"))
                {
                    try { return Read(Path + ".bak"); }
                    catch (Exception backupEx) when (backupEx is IOException || backupEx is InvalidOperationException || backupEx is XmlException) { }
                }
                LastWarning = "저장 파일이 손상되어 새 진행으로 시작합니다. 기존 파일은 보존했습니다.";
                return new ProgressData();
            }
        }
        private static ProgressData Read(string path)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var reader = XmlReader.Create(path, settings))
            {
                var data = (ProgressData)new XmlSerializer(typeof(ProgressData)).Deserialize(reader);
                if (data == null || data.Version != 1 || data.Coins < 0 || data.KeepLevel < 1 || data.KeepLevel > 3) throw new InvalidOperationException("Unsupported or invalid save.");
                data.ClearedStages = data.ClearedStages ?? new List<string>(); data.ClaimedBattles = data.ClaimedBattles ?? new List<string>();
                data.RewardedStages = data.RewardedStages ?? new List<string>();
                data.Characters = data.Characters ?? new List<CharacterProgress>();
                if (data.LanternFragments < 0 || data.MemoryFragments < 0 || data.Characters.Any(x => x == null || !PrototypeData.IsCharacter(x.Id) || x.Level < 1 || x.SyncLevel < 0) || data.Characters.GroupBy(x => x.Id).Any(x => x.Count() > 1)) throw new InvalidOperationException("Invalid character growth.");
                RecruitmentCatalog.InitializeCollection(data);
                if(data.CollectionVersion!=RecruitmentCatalog.CollectionVersion||data.Stardust<0||data.CharacterShards<0||data.RecruitPity<0||data.RecruitPity>=RecruitmentCatalog.HardPity||data.RecruitRandomState==0
                    ||data.OwnedCharacters.Any(id=>RecruitmentCatalog.Character(id)==null)||data.OwnedCharacters.Distinct().Count()!=data.OwnedCharacters.Count
                    ||data.ActiveParty.Count!=PrototypeData.PartyCapacity||data.ActiveParty.Distinct().Count()!=PrototypeData.PartyCapacity||data.ActiveParty.Any(id=>!data.OwnedCharacters.Contains(id))
                    ||data.RedeemedCodes.Distinct().Count()!=data.RedeemedCodes.Count||data.RedeemedCodes.Any(code=>code!=RecruitmentCatalog.TestCurrencyCode&&code!=RecruitmentCatalog.TestMaterialsCode))
                    throw new InvalidOperationException("Invalid collection or recruitment state.");
                return data;
            }
        }
        public void Save(ProgressData data)
        {
            if (data == null) throw new ArgumentNullException("data");
            RecruitmentCatalog.InitializeCollection(data);
            string directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string pending = Path + ".tmp";
            using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new XmlSerializer(typeof(ProgressData)).Serialize(stream, data);
                stream.Flush(true);
            }
            Read(pending);
            if (!File.Exists(Path)) { File.Move(pending, Path); return; }
            try { File.Replace(pending, Path, Path + ".bak"); }
            catch (Exception ex) when (ex is PlatformNotSupportedException || ex is UnauthorizedAccessException)
            {
                // Some sandboxed Windows filesystems reject ReplaceFile metadata access.
                // Keep a validated backup before replacing the primary; Load recovers either
                // a missing or partial primary if the process stops between these operations.
                bool validPrimary = true;
                try { Read(Path); } catch (Exception invalid) when (invalid is InvalidOperationException || invalid is XmlException) { validPrimary = false; }
                if (validPrimary) File.Copy(Path, Path + ".bak", true);
                else if (!File.Exists(Path + ".bak")) File.Copy(pending, Path + ".bak");
                File.Delete(Path);
                File.Move(pending, Path);
            }
        }
    }
}
