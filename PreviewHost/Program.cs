using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using RequiemLantern.Core;

namespace RequiemLantern.Preview
{
    internal sealed class ActionRequest
    {
        public string Action { get; set; }
        public string ActorId { get; set; }
        public int Slot { get; set; }
        public string SkillId { get; set; }
        public LinkKind Kind { get; set; }
        public string TargetId { get; set; }
        public string StageId { get; set; }
        public string ExpectedPhase { get; set; }
        public Dictionary<string, IgniteGrade> Grades { get; set; }
    }

    internal static class Program
    {
        private static string Origin = "http://127.0.0.1:8127";
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
        private static BattleEngine battle = BattleEngine.CreatePrototype();
        private static ProgressService progress;
        private static SaveStore saves;
        private static string root;
        private static readonly CampaignSession campaign = new CampaignSession();

        private static async Task Main(string[] args)
        {
            root = args.Length > 0 ? System.IO.Path.GetFullPath(args[0]) : LocateWorkspace();
            if (!Directory.Exists(System.IO.Path.Combine(root, "RequiemLantern", "Assets"))) throw new InvalidOperationException("Workspace root is missing RequiemLantern/Assets.");
            if (args.Length > 2) {
                int port; if (!int.TryParse(args[2], out port) || port < 1024 || port > 65535) throw new ArgumentException("Invalid preview port.");
                Origin = "http://127.0.0.1:" + port;
            }
            saves = new SaveStore(args.Length > 1 ? System.IO.Path.GetFullPath(args[1]) : System.IO.Path.Combine(root, "PreviewHost", ".local", "progress.xml"));
            progress = new ProgressService(saves.Load());
            battle = BattleEngine.CreatePrototype(progress.Data);
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls(Origin);
            var app = builder.Build();
            var gate = new SemaphoreSlim(1, 1);
            app.Run(async context =>
            {
                await gate.WaitAsync();
                try { await Handle(context); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
                    if (!context.Response.HasStarted) await RespondJson(context, new { error = "처리 중 문제가 발생했습니다. " + ex.Message }, 500);
                }
                finally { gate.Release(); }
            });
            Console.WriteLine("Requiem Lantern preview: " + Origin + "/");
            Console.WriteLine("Rules: shared Unity Core C# / local save: PreviewHost/.local/progress.xml");
            await app.RunAsync();
        }

        private static string LocateWorkspace()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var current = new DirectoryInfo(start);
                while (current != null)
                { if (Directory.Exists(System.IO.Path.Combine(current.FullName, "RequiemLantern", "Assets"))) return current.FullName; current = current.Parent; }
            }
            throw new DirectoryNotFoundException("Run from the Requiem Lantern workspace or pass its absolute path.");
        }

        private static async Task Handle(HttpContext context)
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; font-src 'self'; connect-src 'self'; frame-ancestors 'none'";
            var request = context.Request;
            if (!IPAddress.IsLoopback(context.Connection.RemoteIpAddress)) { await RespondJson(context, new { error = "Loopback only." }, 403); return; }
            string path = request.Path.Value;
            if (request.Method == "GET" && path == "/api/state") { await RespondJson(context, Snapshot(null, null)); return; }
            if (request.Method == "POST" && path == "/api/action")
            {
                string origin = request.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, Origin, StringComparison.Ordinal))
                { await RespondJson(context, new { error = "Origin rejected." }, 403); return; }
                if (request.ContentType == null || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                { await RespondJson(context, new { error = "JSON body required." }, 415); return; }
                if (request.ContentLength > 65536) { await RespondJson(context, new { error = "Request too large." }, 413); return; }
                string body;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 4096, true))
                {
                    var buffer = new char[65537]; int length = 0, count;
                    while (length < buffer.Length && (count = await reader.ReadAsync(buffer, length, buffer.Length - length)) > 0) length += count;
                    if (length > 65536) { await RespondJson(context, new { error = "Request too large." }, 413); return; }
                    body = new string(buffer, 0, length);
                }
                ActionRequest action;
                try { action = JsonSerializer.Deserialize<ActionRequest>(body, Json); }
                catch (JsonException) { await RespondJson(context, Snapshot("요청 형식을 확인해 주세요.", null), 400); return; }
                string error = null; RewardData reward = null; bool persist = false;
                string actionName = action == null ? "" : action.Action;
                bool battleAction = new[] { "recommend", "link", "remove", "clear", "reuse", "resolve", "next", "lantern", "restart" }.Contains(actionName);
                if (battleAction && campaign.Active != null && (campaign.Replay || campaign.Phase != CampaignPhase.Battle)) {
                    await RespondJson(context, Snapshot("전투 스테이지의 전투 단계에서 실행해 주세요.", null), 400); return;
                }
                switch (action == null ? "" : action.Action)
                {
                    case "stage-enter":
                    case "stage-replay":
                        if (campaign.Enter(action.StageId, progress.Data, actionName == "stage-replay", out error) && campaign.Phase == CampaignPhase.Battle)
                            battle = CampaignBattleFactory.Create(campaign.Active, progress.Data);
                        break;
                    case "stage-story":
                        if (campaign.Active == null || campaign.Active.Id != action.StageId ||
                            (!string.IsNullOrEmpty(action.ExpectedPhase) && action.ExpectedPhase != campaign.Phase.ToString())) error = "진행 중인 스토리와 요청이 일치하지 않습니다.";
                        else if (campaign.FinishStory(progress.Data, out persist, out error) && campaign.Phase == CampaignPhase.Battle)
                            battle = CampaignBattleFactory.Create(campaign.Active, progress.Data);
                        break;
                    case "stage-exit": campaign.Exit(); break;
                    case "recommend": battle.RecommendLinks(); break;
                    case "link": battle.TryLink(action.ActorId, action.Slot, action.SkillId, action.Kind, action.TargetId, out error); break;
                    case "remove": battle.RemoveLink(action.ActorId, action.Slot); break;
                    case "clear": battle.ClearLinks(); break;
                    case "reuse": battle.ReusePreviousLinks(); break;
                    case "resolve": battle.ResolveRound(action.Grades); break;
                    case "next": battle.BeginNextRound(); break;
                    case "restart":
                        if (campaign.Active != null) battle = CampaignBattleFactory.Create(campaign.Active, progress.Data);
                        else battle = BattleEngine.CreatePrototype(progress.Data);
                        break;
                    case "lantern": battle.TryLantern(action.ActorId, out error); break;
                    case "prologue": progress.MarkPrologueComplete(); persist = true; break;
                    case "reward":
                        if (campaign.Active != null) persist = campaign.AcceptVictory(battle, progress, out reward, out error);
                        else if (!progress.ClaimVictory(battle, out reward)) error = "승리 후 한 번만 보상을 받을 수 있습니다."; else persist = true;
                        break;
                    case "synchronize":
                        if (campaign.Phase == CampaignPhase.Battle && battle.State.Outcome == BattleOutcome.Ongoing) error = "출격을 마친 뒤 잔영을 동기화해 주세요.";
                        else if (progress.Synchronize(action.ActorId, out error)) { persist = true; progress.ApplyToBattle(battle.State); }
                        break;
                    case "repair": if (progress.RepairKeep(out error)) persist = true; break;
                    default: error = "알 수 없는 동작입니다."; break;
                }
                if (persist) saves.Save(progress.Data);
                await RespondJson(context, Snapshot(error, reward), error == null ? 200 : 400); return;
            }
            if (request.Method != "GET" && request.Method != "HEAD")
            { await RespondJson(context, new { error = "Method not allowed." }, 405); return; }
            if (path.StartsWith("/art/", StringComparison.Ordinal))
            { await ServeFile(context, System.IO.Path.Combine(root, "RequiemLantern", "Assets", "Resources", "Art"), path.Substring(5), new[] { ".png", ".jpg", ".jpeg", ".webp" }, false); return; }
            if (path.StartsWith("/combat/", StringComparison.Ordinal))
            { await ServeFile(context, System.IO.Path.Combine(root, "RequiemLantern", "Assets", "Resources", "Combat"), path.Substring(8), new[] { ".png", ".json" }, false); return; }
            if (path.StartsWith("/fonts/", StringComparison.Ordinal))
            { await ServeFile(context, System.IO.Path.Combine(root, "RequiemLantern", "Assets", "Resources", "Fonts"), path.Substring(7), new[] { ".ttf", ".otf", ".woff", ".woff2" }, false); return; }
            await ServeFile(context, System.IO.Path.Combine(root, "Preview"), path == "/" ? "index.html" : path.TrimStart('/'), new[] { ".html", ".css", ".js", ".json", ".svg", ".png", ".webp", ".ico" }, true);
        }

        private static object Snapshot(string error, RewardData reward)
        {
            return new
            {
                battle = battle.State, progress = progress.Data, plannedCost = battle.PlannedCost, availableEmber = battle.AvailableEmber,
                application = "RequiemLantern", runtimeRevision = "campaign-sanity-v1",
                previews = battle.State.Links.Where(x => x.Kind == LinkKind.Clash).Select(x => new { actorId = x.ActorId, slot = x.Slot, preview = battle.Preview(x) }),
                story = new { prologue = StoryCatalog.Prologue, firstMeal = StoryCatalog.FirstMeal, epilogue = StoryCatalog.Epilogue },
                characterStories = CharacterStoryCatalog.All,
                campaign = new {
                    chapters = CampaignCatalog.Chapters.Select(chapter => new { chapter.Number, chapter.Title, chapter.Summary,
                        stages = chapter.Stages.Select(stage => new { stage.Id, stage.Number, stage.Title, stage.Summary, stage.Location, stage.Kind, stage.KindLabel,
                            stage.Chapter, stage.Participants, stage.Party, stage.EnemyName, stage.EnemyArt, stage.IsBoss, stage.HumanEnemy, stage.MusicId,
                            stage.HasBattle, unlocked = CampaignRules.IsUnlocked(progress.Data, stage), cleared = CampaignRules.IsCleared(progress.Data, stage) }) }),
                    activeStage = campaign.Active, phase = campaign.Phase, replay = campaign.Replay, currentStory = campaign.CurrentStory
                },
                warning = saves.LastWarning, error, reward
            };
        }

        private static async Task ServeFile(HttpContext context, string baseDirectory, string relativePath, string[] extensions, bool nested)
        {
            string relative;
            try { relative = Uri.UnescapeDataString(relativePath); }
            catch (UriFormatException) { await RespondJson(context, new { error = "Invalid path." }, 400); return; }
            if (string.IsNullOrEmpty(relative) || relative.IndexOfAny(new[] { '\\', ':', '\0' }) >= 0 || relative.Split('/').Any(x => x == ".." || x == "." || x.StartsWith(".", StringComparison.Ordinal)) || (!nested && relative.Contains('/')))
            { await RespondJson(context, new { error = "Invalid path." }, 403); return; }
            string directory = System.IO.Path.GetFullPath(baseDirectory).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, relative));
            if (!full.StartsWith(directory, StringComparison.OrdinalIgnoreCase) || !extensions.Contains(System.IO.Path.GetExtension(full), StringComparer.OrdinalIgnoreCase) || !File.Exists(full))
            { await RespondJson(context, new { error = "Not found." }, 404); return; }
            // Do not follow symlinks or junctions out of the static content directory.
            for (var current = new FileInfo(full) as FileSystemInfo; current != null && current.FullName.Length >= directory.Length; current = current is FileInfo ? ((FileInfo)current).Directory : ((DirectoryInfo)current).Parent)
            { if ((current.Attributes & FileAttributes.ReparsePoint) != 0) { await RespondJson(context, new { error = "Invalid path." }, 403); return; } }
            byte[] bytes = await File.ReadAllBytesAsync(full);
            context.Response.ContentType = Mime(System.IO.Path.GetExtension(full)); context.Response.ContentLength = bytes.Length;
            if (context.Request.Method != "HEAD") await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        private static string Mime(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8"; case ".css": return "text/css; charset=utf-8";
                case ".js": return "text/javascript; charset=utf-8"; case ".json": return "application/json; charset=utf-8";
                case ".png": return "image/png"; case ".jpg": case ".jpeg": return "image/jpeg"; case ".webp": return "image/webp";
                case ".svg": return "image/svg+xml"; case ".ico": return "image/x-icon"; case ".ttf": return "font/ttf";
                case ".otf": return "font/otf"; case ".woff": return "font/woff"; case ".woff2": return "font/woff2"; default: return "application/octet-stream";
            }
        }
        private static async Task RespondJson(HttpContext context, object data, int status = 200)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, Json);
            context.Response.StatusCode = status; context.Response.ContentType = "application/json; charset=utf-8"; context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}

