using System.Reflection;
using SAIN.Preset.Server;
using SAIN.Plugin;
using SAIN.Preset.Shared.Preset;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.BotSettings.SAINSettings.Categories;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SAIN.Preset.Shared.GlobalSettings.Categories.General;

partial class Program
{
    static Type SharedType(string name)
    {
        var t = typeof(BotLevels).Assembly.GetType("SAIN.Preset.Shared.Preset." + name);
        Check(t != null, name + " API missing");
        return t;
    }
    static object Call(string type, string method, params object[] args)
    {
        var m = SharedType(type).GetMethod(method);
        Check(m != null, method + " API missing");
        return m.Invoke(null, args);
    }
    static object Member(object owner, string name)
        => owner.GetType().GetProperty(name)?.GetValue(owner) ?? owner.GetType().GetField(name)?.GetValue(owner);
    static void Field(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name);
        Check(field != null, name + " field missing");
        field.SetValue(owner, value);
    }
    static List<EPersonality> Pool(ESainWildSpawnType role, SAINMindSettings mind)
        => ((IEnumerable<EPersonality>)Call("BotPersonalityPool", "Candidates", role, mind)).ToList();
    static void SettingsPolicyTests()
    {
        Test("shipped easy normal and hard datasets retain their family badge through level roundtrips", () =>
        {
            string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
            SAINPresetBundle Read(string name)=>Newtonsoft.Json.JsonConvert.DeserializeObject<SAINPresetBundle>(File.ReadAllText(Path.Combine(root,"SAINServerMod/Presets",name+".json")));
            var easy=Read("zzap 쉬움"); var normal=Read("zzap 보통"); var hard=Read("zzap");
            int bots=0;
            foreach(var family in new[]{easy,normal,hard})
            foreach(var pair in family.BotSettings)
            {
                var live=BotLevels.Snapshot(pair.Value);
                var sources=new LevelSources {Reference=hard.BotSettings.GetValueOrDefault(pair.Key),BaselineBot=pair.Value,BaselineLevel=BotLevels.PresetLevel(family.Info.Name),
                    Current=LevelGlobals.From(family),Easy=LevelGlobals.From(easy),Normal=LevelGlobals.From(normal),Hard=LevelGlobals.From(hard),
                    EasyBot=easy.BotSettings.GetValueOrDefault(pair.Key),NormalBot=normal.BotSettings.GetValueOrDefault(pair.Key),HardBot=hard.BotSettings.GetValueOrDefault(pair.Key)};
                var badge=BotLevels.Badge(live,sources);
                Check(!badge.Custom&&!badge.Adjusted&&badge.Level==sources.BaselineLevel,family.Info.Name+" initial badge "+pair.Key);
                foreach(var level in new[]{EBotLevel.Easy,EBotLevel.Normal,EBotLevel.Hard,EBotLevel.Easy})
                {
                    BotLevels.Apply(level,live,sources);
                    badge=BotLevels.Badge(live,sources);
                    Check(!badge.Custom&&!badge.Adjusted&&badge.Level==level,family.Info.Name+" roundtrip "+pair.Key);
                }
                BotLevels.ResetToOriginal(live,sources);
                Check(!BotLevels.Badge(live,sources).Adjusted,family.Info.Name+" original reset "+pair.Key);
                bots++;
            }
            Check(bots>100,"dataset unexpectedly small");
            Console.WriteLine("DATASET "+bots+" bot/preset combinations checked");
        });
        Test("slot comparison and old single-choice edits include personality pools", () =>
        {
            var group=Group(Fixture());
            var knob=BotQuickSettings.All.Single(k=>k.Kind==BotKnobKind.Personality);
            BotPersonalityPool.Set(group,new[]{EPersonality.Chad,EPersonality.GigaChad});
            group.Settings[ESainBotDifficulty.easy].Mind.PersonalityPool=new(){EPersonality.Rat};
            Check(BotQuickSettings.DiffersBySlot(knob,group),"pool slot variation invisible");
            Check(!BotQuickSettings.AllSlotsSame(group),"pool slots falsely identical");
            Check(BotQuickSettings.IsChanged(knob,group,Group(Fixture())),"old personality changed indicator ignores pool");
            Check(BotQuickSettings.AnyChanged(group,Group(Fixture())),"old group changed indicator ignores pool");
            Check(BotQuickSettings.SlotValues(knob,group,true).Any(v=>v.Text.Contains("Rat")),"pool slot label missing");
            BotQuickSettings.ApplyPersonality(knob,group,null,EPersonality.Coward);
            Check(group.Settings.Values.All(v=>BotPersonalityPool.Candidates(group.WildSpawnType,v.Mind).SequenceEqual(new[]{EPersonality.Coward})),"old single-choice edit ignored behind pool");
        });
        Test("client rejects direct writes to bundled original", () =>
        {
            LoadFixture(); var before=ServerPresetClient.Json["zzap"];
            Check(!PresetSync.PublishCustom(PresetHandler.LoadedPreset,PresetHandler.LoadedPreset.Info),"protected name accepted");
            Check(ServerPresetClient.Json["zzap"]==before,"original overwritten");
        });
        Test("personality knob detects and restores a multi-choice pool independently of level", () =>
        {
            var original=Group(Fixture()); original.WildSpawnType=(ESainWildSpawnType)848420;
            var live=BotLevels.Snapshot(original); live.WildSpawnType=original.WildSpawnType;
            BotPersonalityPool.Set(live,new[]{EPersonality.Rat,EPersonality.Chad});
            var src=new LevelSources { Reference=original, BaselineBot=original, BaselineLevel=EBotLevel.Easy };
            var knob=BotQuickSettings.All.Single(k=>k.Kind==BotKnobKind.Personality);
            Check(BotLevels.KnobChanged(knob,live,src),"pool change invisible");
            BotLevels.ResetKnobToBaseline(knob,live,src);
            Check(!BotLevels.KnobChanged(knob,live,src),"pool did not reset");
            Check(BotPersonalityPool.Candidates(live.WildSpawnType,BotLevels.NormalSlot(live).Mind).SequenceEqual(new[]{EPersonality.Chad,EPersonality.GigaChad}),"reset lost role default");
        });
        Test("quick controls preserve switches during numeric batches and reject invalid numbers", () =>
        {
            var p=new SAIN.Preset.Shared.Personalities.BasePersonality.PersonalitySettingsClass();
            var fields=SettingsQuickFields.For(p);
            Check(fields.Count>=8,"personality shortcuts missing");
            var enabled=fields.Single(f=>f.Key=="Assignment.Enabled");
            enabled.Set(0);
            var aggression=fields.Single(f=>f.Key=="Behavior.General.AggressionMultiplier");
            aggression.Set(2); SettingsQuickFields.Scale(fields,1.1f);
            Check(Math.Abs(aggression.Value-2.2f)<.001f,"batch not applied");
            Check(enabled.Value==0,"numeric batch changed switch");
            aggression.Set(float.NaN); aggression.Set(float.PositiveInfinity);
            Check(Math.Abs(aggression.Value-2.2f)<.001f,"invalid numeric input applied");
            aggression.Set(999); Check(aggression.Value==5,"attribute bound ignored");
        });
        Test("global vanilla excludes an already registered boss", () =>
        {
            LoadFixture(); Role(EFT.WildSpawnType.bossKilla,"Killa",false);
            var bot=Bot(EFT.WildSpawnType.bossKilla);
            SAIN.Components.BotManagerComponent.Instance=new();
            SAIN.Components.BotManagerComponent.Instance.Bots[bot.ProfileId]=new();
            PresetHandler.LoadedPreset.GlobalSettings.General.VanillaBots.VanillaBosses=true;
            Check(SAIN.SAINEnableClass.IsBotExcluded(bot),"registered bot bypassed global vanilla");
            SAIN.Components.BotManagerComponent.Instance=null;
        });
        Test("personality pool JSON roundtrip preserves explicit automatic and multiple modes", () =>
        {
            foreach(var values in new[]{new List<EPersonality>(),new List<EPersonality>{EPersonality.Chad,EPersonality.GigaChad}})
            {
                var group=Group(Fixture()); BotPersonalityPool.Set(group,values);
                var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<SAINSettingsGroupClass>(Newtonsoft.Json.JsonConvert.SerializeObject(group));
                Check(BotPersonalityPool.Candidates((ESainWildSpawnType)848420,BotLevels.NormalSlot(copy).Mind).SequenceEqual(values),"JSON changed pool mode");
            }
        });
        Test("editable bundle does not mutate a preset source or another working copy", () =>
        {
            LoadFixture(); PresetSync.TryGetCustomBundle("zzap", out var source);
            var m = typeof(PresetSync).GetMethod("CopyForEditing"); Check(m != null, "CopyForEditing API missing");
            var one = (SAINPresetBundle)m.Invoke(null, new object[] { source });
            var two = (SAINPresetBundle)m.Invoke(null, new object[] { source });
            Group(one).Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 999;
            Check(Group(source).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 225, "source was edited");
            Check(Group(two).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 225, "other preset inherited edit");
            Check(ReferenceEquals(SAIN.Preset.Shared.GlobalSettings.GlobalSettingsClass.Instance, PresetHandler.LoadedPreset.GlobalSettings), "copy changed active globals");
        });
        Test("saved custom cache stays detached from later edits", () =>
        {
            LoadFixture(); var p = PresetHandler.LoadedPreset;
            Role(EFT.WildSpawnType.pmcUSEC, "PmcUsec", false);
            p.BotSettings.SAINSettings[EFT.WildSpawnType.pmcUSEC].Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 225;
            p.Info.Name="zzap [Modified]";
            Check(PresetSync.PublishCustom(p,p.Info),"user copy save rejected");
            p.BotSettings.SAINSettings[EFT.WildSpawnType.pmcUSEC].Settings[ESainBotDifficulty.normal].Core.VisibleDistance = 999;
            PresetSync.TryGetCustomBundle("zzap [Modified]",out var stored);
            Check(Group(stored).Settings[ESainBotDifficulty.normal].Core.VisibleDistance == 225, "cache aliases editor after save");
        });
        Test("bundled preset saves choose a free Modified name", () =>
        {
            Func<string,bool> taken = n => n == "zzap 보통 [Modified]";
            var name = (string)Call("PresetSavePolicy", "NameForSave", "zzap 보통", "zzap 보통", taken);
            Check(name == "zzap 보통 [Modified 2]", "original or occupied copy name reused");
            Check((bool)Call("PresetSavePolicy", "IsProtected", "ZZAP"), "case variant bypassed protection");
            Check(!(bool)Call("PresetSavePolicy", "IsProtected", "zzap 보통 [Modified]"), "user copy treated as bundled original");
            name = (string)Call("PresetSavePolicy", "NameForSave", "zzap 보통 [Modified]", "zzap 보통 [Modified]", taken);
            Check(name == "zzap 보통 [Modified]", "second save cloned user copy again");
        });
        Test("zzap family level follows easy and normal including Modified copies", () =>
        {
            Check((EBotLevel)Call("BotLevels", "PresetLevel", "zzap 쉬움") == EBotLevel.Easy, "easy family missing");
            Check((EBotLevel)Call("BotLevels", "PresetLevel", "zzap 보통 [Modified 2]") == EBotLevel.Normal, "normal copy family missing");
            Check((EBotLevel)Call("BotLevels", "PresetLevel", "my own preset") == EBotLevel.None, "custom incorrectly labelled");
        });
        Test("untouched easy baseline is easy with no changed knob despite per-slot differences", () =>
        {
            var baseline = Group(Fixture("zzap 쉬움")); baseline.Settings[ESainBotDifficulty.easy].Core.VisibleDistance=150;
            baseline.Settings[ESainBotDifficulty.hard].Core.VisibleDistance=250;
            var group = BotLevels.Snapshot(baseline); var reference = Group(Fixture());
            var src = new LevelSources { Reference=reference, Current=LevelGlobals.From(Fixture()), Hard=LevelGlobals.From(Fixture()), HardBot=reference };
            Field(src,"BaselineBot",baseline); Field(src,"BaselineLevel",EBotLevel.Easy);
            Check(BotLevels.Badge(group,src)==(EBotLevel.Easy,false,false), "untouched easy labelled custom");
            var knob=BotQuickSettings.All.First(k=>k.Id=="sight");
            Check(!BotLevels.KnobChanged(knob,group,src), "untouched easy marked changed");
            group.Settings[ESainBotDifficulty.hard].Core.VisibleDistance=300;
            Check(BotLevels.Badge(group,src)==(EBotLevel.Easy,true,false), "adjusted easy lost its family");
            BotLevels.ResetKnobToBaseline(knob,group,src);
            Check(group.Settings[ESainBotDifficulty.hard].Core.VisibleDistance==250, "reset flattened easy slots");
        });
        Test("global vanilla overrides stored SAIN and preserves saved bot values", () =>
        {
            var settings = new VanillaBotSettings {VanillaScavs=true}; var group = Group(Fixture());
            var state=Call("BotAiPolicy","Resolve",ESainWildSpawnType.assault,settings,group,false);
            Check(Member(state,"Mode").ToString()=="GlobalVanilla", "global vanilla ignored");
            Check(!(bool)Member(state,"CanEditSain") && !(bool)Member(state,"CanChooseAI"), "locked bot editable");
            settings.VanillaScavs=false;
            state=Call("BotAiPolicy","Resolve",ESainWildSpawnType.assault,settings,group,false);
            Check(Member(state,"Mode").ToString()=="Sain", "global removal did not restore stored choice");
            Check(Group(Fixture()).DifficultyModifier==group.DifficultyModifier, "policy mutated stored setting");
        });
        Test("Zryachiy guards and ordinary snipers stay vanilla even if stored choice says SAIN", () =>
        {
            foreach(var role in new[]{ESainWildSpawnType.bossZryachiy,ESainWildSpawnType.followerZryachiy,ESainWildSpawnType.peacefullZryachiyEvent,ESainWildSpawnType.ravangeZryachiyEvent,ESainWildSpawnType.marksman})
            {
                var state=Call("BotAiPolicy","Resolve",role,new VanillaBotSettings(),Group(Fixture()),false);
                Check(Member(state,"Mode").ToString()=="AlwaysVanilla", role+" not protected");
                Check(!(bool)Member(state,"CanChooseAI"),role+" may be changed to SAIN");
            }
        });
        Test("Black Division default pool is Chad and GigaChad while Wedge is GigaChad", () =>
        {
            foreach(var role in new[]{848420,848421,848422,848423,848426})
                Check(Pool((ESainWildSpawnType)role,new SAINMindSettings()).SequenceEqual(new[]{EPersonality.Chad,EPersonality.GigaChad}),"wrong default pool for "+role);
            Check(Pool((ESainWildSpawnType)848424,new SAINMindSettings()).SequenceEqual(new[]{EPersonality.GigaChad}),"Wedge must start GigaChad");
        });
        Test("personality pool supports automatic single and sanitized multiple choices", () =>
        {
            var mind=new SAINMindSettings();
            Field(mind,"PersonalityPool",new List<EPersonality>());
            Check(Pool((ESainWildSpawnType)848420,mind).Count==0,"empty explicit pool must mean automatic");
            Field(mind,"PersonalityPool",new List<EPersonality>{EPersonality.Chad,EPersonality.Chad,EPersonality.None,(EPersonality)999,EPersonality.GigaChad});
            Check(Pool(ESainWildSpawnType.pmcUSEC,mind).SequenceEqual(new[]{EPersonality.Chad,EPersonality.GigaChad}),"pool not sanitized");
            Field(mind,"PersonalityPool",null); mind.PersonalityOverride=EPersonality.Rat;
            Check(Pool((ESainWildSpawnType)848420,mind).SequenceEqual(new[]{EPersonality.Rat}),"legacy user selection overwritten");
        });
        Test("personality pool selection skips disabled choices and falls back when none remain", () =>
        {
            var mind=new SAINMindSettings();Field(mind,"PersonalityPool",new List<EPersonality>{EPersonality.Chad,EPersonality.GigaChad});
            var method=SharedType("BotPersonalityPool").GetMethod("TryPick");Check(method!=null,"TryPick missing");
            for(int i=0;i<8;i++)
            {
                object[] args={ESainWildSpawnType.pmcUSEC,mind,(Func<EPersonality,bool>)(p=>p==EPersonality.Chad),i,EPersonality.None};
                Check((bool)method.Invoke(null,args) && (EPersonality)args[4]==EPersonality.Chad,"disabled candidate picked");
            }
            object[] none={ESainWildSpawnType.pmcUSEC,mind,(Func<EPersonality,bool>)(_=>false),0,EPersonality.None};
            Check(!(bool)method.Invoke(null,none),"all-disabled pool did not return to automatic assignment");
        });
        Test("personality pools are deep copied by snapshot restore and original reset", () =>
        {
            var group=Group(Fixture());var slot=group.Settings[ESainBotDifficulty.normal];
            Field(slot.Mind,"PersonalityPool",new List<EPersonality>{EPersonality.Chad,EPersonality.GigaChad});
            var saved=BotLevels.Snapshot(group);
            ((List<EPersonality>)Member(slot.Mind,"PersonalityPool")).Clear();
            Check(((List<EPersonality>)Member(saved.Settings[ESainBotDifficulty.normal].Mind,"PersonalityPool")).Count==2,"snapshot aliases pool");
            BotLevels.RestoreSaved(group,saved);
            Check(((List<EPersonality>)Member(slot.Mind,"PersonalityPool")).Count==2,"restore lost pool");
        });
    }
}
