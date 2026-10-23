using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Preset;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.Preset.Shared.Personalities.BasePersonality;
using SAINServerMod.Generation;
using SAINServerMod.Generators;
using SAINServerMod.Services;
using SAINServerMod.Utils;
using SAINServerMod.Web.Components;
using SAINServerMod.Web.Services;

// Isolated static component rendering. No HTTP listener, SPT startup or game API is used.
var services=new ServiceCollection();
services.AddLogging(); services.AddMudServices();
services.AddSingleton<IJSRuntime,JsStub>(); services.AddSingleton<NavigationManager,NavigationStub>();
services.AddSingleton(new PresetGenerationService(null!,null!,null!,null!,null!));
// Missing raid records exercise the editor's fallback; the boundary has no disk/API connection.
services.AddSingleton((ClientDataStorageService)RuntimeHelpers.GetUninitializedObject(typeof(ClientDataStorageService)));
await using var provider=services.BuildServiceProvider();
await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
int count=0;
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
string outDir=Path.GetFullPath(Path.Combine(root,"../sain-1023-verification/render-1024")); Directory.CreateDirectory(outDir);
SAINPresetBundle Read(string name)=>SAINJsonUtil.Deserialize<SAINPresetBundle>(File.ReadAllText(Path.Combine(root,"SAINServerMod/Presets",name+".json")))!;
async Task<string> Render<T>(string name,Dictionary<string,object?> args) where T:IComponent
{
    string html=await renderer.Dispatcher.InvokeAsync(async()=>
    {
        var component=await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(args));
        return component.ToHtmlString();
    });
    await File.WriteAllTextAsync(Path.Combine(outDir,name+".html"),html);
    return WebUtility.HtmlDecode(html);
}
void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); count++; Console.WriteLine("PASS "+message); }
var easy=Read("zzap 쉬움"); var normal=Read("zzap 보통"); var hard=Read("zzap");
IReadOnlyList<(string Name,SAINPresetBundle Bundle)> refs=new[]{("zzap 쉬움",easy),("zzap 보통",normal),("zzap",hard)};
foreach(var mode in new[]{"sain","perbot","global","fixed"})
{
    var bundle=Read("zzap 쉬움");
    var role=mode=="fixed"?ESainWildSpawnType.bossZryachiy:ESainWildSpawnType.assault;
    var group=bundle.BotSettings[role]; bundle.BotSettings=new(){{role,group}};
    group.UseVanillaAI=mode=="perbot"; bundle.GlobalSettings.General.VanillaBots.VanillaScavs=mode=="global";
    var html=await Render<BotSettingsEditor>("bot-"+mode,new(){{"Bundle",bundle},{"Original",Read("zzap 쉬움")},{"References",refs},{"Zzap",hard}});
    Check(!html.Split("</aside>")[0].Contains("class=\"sain-bot-item "),mode+" bot groups initially collapsed");
    Check(html.Contains("이 봇의 AI"),mode+" can open bot panel and AI controls");
    Check(html.Contains("쉬움 (zzap 쉬움 프리셋 수준)"),mode+" combined difficulty label");
    bool disabled=html.Contains("<fieldset disabled");
    Check(disabled==(mode!="sain"),mode+" effective SAIN edit lock");
    if(mode=="global") Check(html.Contains("바닐라 · 전역 우선"),"global priority explanation");
    if(mode=="fixed") Check(html.Contains("바닐라 · 고정"),"Zryachiy fixed vanilla explanation");
    if(mode=="perbot") Check(html.Contains("생성할 때 균등 무작위"),"personality selection help remains visible while locked");
}
var globalHtml=await Render<SainSectionEditor>("global",new(){{"Target",easy.GlobalSettings},{"Original",normal.GlobalSettings}});
Check(globalHtml.Contains("빠른 조절")&&globalHtml.Contains("고급 · 모든 설정"),"global quick and advanced controls render");
var personalityHtml=await Render<PersonalityEditor>("personality",new(){{"Bundle",easy},{"Original",normal}});
Check(personalityHtml.Contains("기본값으로")&&!personalityHtml.Contains("수치 −10%")&&!personalityHtml.Contains("수치 +10%"),"personality toolbar contains only the defaults action");
var gearHtml=await Render<GearStealthEditor>("gear",new(){{"Bundle",easy},{"Original",normal}});
Check(gearHtml.Contains("검색 결과 선택")&&gearHtml.Contains("소음 설정은 아닙니다"),"gear batch controls and correct visibility explanation render");
var quickReferences = new (string Name,object Target)[]{("zzap",hard.GlobalSettings.Difficulty),("zzap 쉬움",easy.GlobalSettings.Difficulty),("zzap 보통",normal.GlobalSettings.Difficulty)};
var quickHtml=await Render<QuickObjectEditor>("quick-reference",new(){{"Target",easy.GlobalSettings.Difficulty},{"Original",normal.GlobalSettings.Difficulty},{"Defaults",easy.GlobalSettings.Difficulty},{"References",quickReferences}});
Check(quickHtml.Contains("비교 기준")&&quickHtml.Contains("sain-diffslider")&&quickHtml.Contains("class=\"r\""),"quick rows render preset reference selector and reference tick");
Check(quickHtml.Contains("기본값으로")&&!quickHtml.Contains("수치 −10%")&&!quickHtml.Contains("수치 +10%"),"quick toolbar removes both percentage actions");
WebText.Korean=false;
var englishHtml=await Render<QuickObjectEditor>("quick-reference-en",new(){{"Target",easy.GlobalSettings.Difficulty},{"Original",normal.GlobalSettings.Difficulty},{"Defaults",easy.GlobalSettings.Difficulty},{"References",quickReferences}});
Check(englishHtml.Contains("Compare with")&&englishHtml.Contains("Defaults")&&!englishHtml.Contains("기본값으로"),"English quick toolbar and reference labels render in English");
WebText.Korean=true;
var protectedService=(PresetService)RuntimeHelpers.GetUninitializedObject(typeof(PresetService));
foreach(var card in QuickSettings.ZzapPresets)
{
    bool rejected=false;try{await protectedService.SaveCustomAsync(card.Name,"{}");}catch(InvalidOperationException){rejected=true;}
    Check(rejected,card.Name+" server low-level write protection");
}
// Exercise the real server generator, with an empty offline bot database and registered role metadata.
var generation=new PresetGenerationService(new SPTarkov.Server.Core.Models.Spt.Tables.BotTable {Types=new(),Base=null!,Core=null!},null!,null!,null!,null!);
var registered=(List<SAIN.Preset.Shared.BotSettings.BotTypeInfo>)typeof(PresetGenerationService).GetField("_registered",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(generation)!;
foreach(int id in new[]{848420,848421,848422,848423,848424,848426}) registered.Add(new("offline "+id,(ESainWildSpawnType)id,.5f));
var generated=generation.Generate(SAINDifficulty.hard,"offline defaults","");
foreach(int id in new[]{848420,848421,848422,848423,848424,848426})
{
    var expected=id==848424?new[]{EPersonality.GigaChad}:new[]{EPersonality.Chad,EPersonality.GigaChad};
    Check(generated.BotSettings[(ESainWildSpawnType)id].Settings.Values.All(v=>BotPersonalityPool.Candidates((ESainWildSpawnType)id,v.Mind).SequenceEqual(expected)),"real server defaults for Black Division role "+id);
}
Console.WriteLine($"web smoke: {count} passed, 0 failed; static HTML: {outDir}");

sealed class JsStub:IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>ValueTask.FromResult(default(TValue)!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>ValueTask.FromResult(default(TValue)!);
}
sealed class NavigationStub:NavigationManager
{
    public NavigationStub(){Initialize("http://offline.invalid/","http://offline.invalid/SAIN/presets");}
    protected override void NavigateToCore(string uri,bool forceLoad)=>throw new InvalidOperationException("Navigation not allowed in static renderer");
}
