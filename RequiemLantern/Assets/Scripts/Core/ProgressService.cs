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
        public ProgressService(ProgressData data)
        {
            Data = data ?? new ProgressData();
            Data.Characters = Data.Characters ?? new List<CharacterProgress>();
            Data.ClearedStages = Data.ClearedStages ?? new List<string>();
            Data.RewardedStages = Data.RewardedStages ?? new List<string>();
            Data.ClaimedBattles = Data.ClaimedBattles ?? new List<string>();
        }
        public CharacterProgress GetCharacter(string id)
        {
            if (!PrototypeData.IsCharacter(id)) return null;
            var value = Data.Characters.FirstOrDefault(x => x.Id == id);
            if (value == null) { value = new CharacterProgress { Id = id }; Data.Characters.Add(value); }
            return value;
        }
        public bool Synchronize(string id, out string error)
        {
            error = null;
            if (!PrototypeData.IsCharacter(id)) { error = "동기화할 잔영을 선택해 주세요."; return false; }
            var existing = Data.Characters.FirstOrDefault(x => x.Id == id);
            if (existing != null && existing.SyncLevel >= 1) { error = "이미 동기화하여 3스킬을 개방했습니다."; return false; }
            if (Data.LanternFragments < SynchronizationCost) { error = "동기화에 등불 조각 " + SynchronizationCost + "개가 필요합니다."; return false; }
            var value = GetCharacter(id); Data.LanternFragments -= SynchronizationCost; value.SyncLevel = 1; return true;
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
                return data;
            }
        }
        public void Save(ProgressData data)
        {
            if (data == null) throw new ArgumentNullException("data");
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
