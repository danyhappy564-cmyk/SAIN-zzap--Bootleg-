using SAIN.Preset.Shared.Attributes;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Preset;

partial class Program
{
    static void SelectionSlotTests()
    {
        SAINSettingsGroupClass New() => new(Enum.GetValues<ESainBotDifficulty>()) { WildSpawnType = ESainWildSpawnType.pmcUSEC };
        var sight = BotQuickSettings.All.Single(k => k.Id == "sight");
        var row = BotAdvancedFields.Rows().Single(r => r.Category.Name == "Core" && r.Field.Name == "VisibleDistance");
        Test("selection detects displayed mixed knob and sets same value only on targets", () =>
        {
            var a=New(); var b=New(); var outside=New();
            a.Settings[ESainBotDifficulty.normal].Core.VisibleDistance=100;
            b.Settings[ESainBotDifficulty.normal].Core.VisibleDistance=200;
            var targets=new[]{a,b};
            Check(BotSelectionSettings.KnobMixed(targets,sight),"mixed values hidden");
            float before=BotLevels.NormalSlot(outside).Core.VisibleDistance;
            BotSelectionSettings.Apply(targets,sight,175);
            Check(!BotSelectionSettings.KnobMixed(targets,sight),"same values marked mixed");
            Check(targets.All(g=>BotLevels.NormalSlot(g).Core.VisibleDistance==175),"not the same displayed value");
            Check(BotLevels.NormalSlot(outside).Core.VisibleDistance==before,"unselected bot changed");
        });
        Test("selection advanced fields use the same value in each slot", () =>
        {
            var a=New(); var b=New();
            BotSelectionSettings.ApplyField(new[]{a,b},row,null,190f);
            Check(new[]{a,b}.All(g=>g.Settings.Values.All(s=>s.Core.VisibleDistance==190)),"advanced values scaled instead of assigned");
            Check(!BotSelectionSettings.FieldMixed(new[]{a,b},row,ESainBotDifficulty.normal),"same field marked mixed");
        });
        Test("selection mixed follows the formatted knob value", () =>
        {
            var a=New(); var b=New();
            a.Settings[ESainBotDifficulty.normal].Core.VisibleDistance=100.001f;
            b.Settings[ESainBotDifficulty.normal].Core.VisibleDistance=100.004f;
            Check(!BotSelectionSettings.KnobMixed(new[]{a,b},sight),"same displayed number marked mixed");
        });
        Test("selection reset restores each bot's original values", () =>
        {
            var first=New(); var second=New();
            foreach(var s in first.Settings.Values) s.Core.VisibleDistance=100;
            foreach(var s in second.Settings.Values) s.Core.VisibleDistance=200;
            var a=BotLevels.Snapshot(first); var b=BotLevels.Snapshot(second);
            var targets=new[]{a,b};
            BotSelectionSettings.Apply(targets,sight,300);
            Check(BotSelectionSettings.IsChanged(targets,sight,g=>new LevelSources{Reference=g==a?first:second}),"changed values not marked");
            BotSelectionSettings.Reset(targets,sight,g=>new LevelSources{Reference=g==a?first:second});
            Check(BotLevels.NormalSlot(a).Core.VisibleDistance==100 && BotLevels.NormalSlot(b).Core.VisibleDistance==200,"reset flattened originals");
        });
        Test("slot stores only changes and applies to a different bot", () =>
        {
            var original=New(); var source=BotLevels.Snapshot(original); var target=New();
            source.Settings[ESainBotDifficulty.normal].Core.VisibleDistance=321;
            var slot=BotSettingsSlots.Capture("test",source,new LevelSources{Reference=original});
            Check(slot.Fields.Count==1,"unchanged fields stored");
            var slots=new BotSettingsSlots(); int index=slots.Add(slot);
            var restored=BotSettingsSlots.Deserialize(slots.Serialize());
            Check(restored.Slots.Count==3 && index==0,"initial empty slots lost");
            Check(BotSettingsSlots.Apply(restored.Slots[0],target,new LevelSources{Reference=original})==0,"valid path skipped");
            Check(target.Settings[ESainBotDifficulty.normal].Core.VisibleDistance==321,"slot not applied");
            Check(target.Settings[ESainBotDifficulty.easy].Core.VisibleDistance==original.Settings[ESainBotDifficulty.easy].Core.VisibleDistance,"unchanged slot overwritten");
        });
        Test("share round trip has no creation time and rejects corrupt and oversized input", () =>
        {
            var original=New(); var source=BotLevels.Snapshot(original); source.Settings[ESainBotDifficulty.normal].Core.VisibleDistance=222;
            var slot=BotSettingsSlots.Capture("share",source,new LevelSources{Reference=original});
            string code=BotSettingsSlots.Share(slot);
            Check(code.StartsWith("SAIN1-"),"wrong prefix");
            Check(BotSettingsSlots.TryReadShare(code,out var copy,out var skipped,out _) && skipped==0,"round trip failed");
            Check(copy.CreatedUtc==null && copy.Name==slot.Name && copy.Fields.Count==1,"share metadata or fields wrong");
            Check(!BotSettingsSlots.TryReadShare("bad",out _,out _,out _),"bad prefix accepted");
            Check(!BotSettingsSlots.TryReadShare("SAIN1-!!!!",out _,out _,out _),"bad base64 accepted");
            Check(!BotSettingsSlots.TryReadShare("SAIN1-"+new string('A',100000),out _,out _,out _),"oversized accepted");
        });
        Test("unknown share field is discarded and counted", () =>
        {
            var original=New(); var slot=BotSettingsSlots.Capture("unknown",original,new LevelSources{Reference=New()});
            slot.Fields.Add(new BotSlotField{Path="Core.DoesNotExist",Difficulty=ESainBotDifficulty.normal,Value="123"});
            Check(BotSettingsSlots.TryReadShare(BotSettingsSlots.Share(slot),out var copy,out int skipped,out _),"unknown field rejected whole code");
            Check(skipped==1 && copy.Fields.Count==0,"unknown field not discarded");
        });
        Test("slot preserves personality candidates and group modifier", () =>
        {
            var original=New(); var source=BotLevels.Snapshot(original); var target=New();
            var pool=new[]{SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality.Chad,SAIN.Preset.Shared.Models.Preset.Personalities.EPersonality.GigaChad};
            BotPersonalityPool.Set(source,pool); source.DifficultyModifier=0.7f;
            var slot=BotSettingsSlots.Capture("pool",source,new LevelSources{Reference=original});
            Check(BotSettingsSlots.TryReadShare(BotSettingsSlots.Share(slot),out var copy,out _,out _),"pool share rejected");
            BotSettingsSlots.Apply(copy,target,new LevelSources{Reference=original});
            Check(target.DifficultyModifier==0.7f && target.Settings.Values.All(s=>BotPersonalityPool.Candidates(target.WildSpawnType,s.Mind).SequenceEqual(pool)),"candidate pool or modifier lost");
        });
        Test("slot skips unavailable difficulty slots and reports count", () =>
        {
            var original=New(); var source=BotLevels.Snapshot(original);
            source.Settings[ESainBotDifficulty.impossible].Core.VisibleDistance=333;
            var slot=BotSettingsSlots.Capture("one",source,new LevelSources{Reference=original});
            var target=new SAINSettingsGroupClass(new[]{ESainBotDifficulty.normal});
            Check(BotSettingsSlots.Apply(slot,target,new LevelSources{Reference=original})==1,"missing slot count wrong");
        });
        Test("share rejects decompression bombs and invalid JSON", () =>
        {
            string Pack(string json)
            {
                using var output=new MemoryStream();
                using(var gzip=new System.IO.Compression.GZipStream(output,System.IO.Compression.CompressionLevel.Optimal,true))
                { var bytes=System.Text.Encoding.UTF8.GetBytes(json); gzip.Write(bytes,0,bytes.Length); }
                return "SAIN1-"+Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+','-').Replace('/','_');
            }
            Check(!BotSettingsSlots.TryReadShare(Pack(new string(' ',BotSettingsSlots.MaxBytes+1)),out _,out _,out string reason) && reason=="size","decompression size limit missing");
            Check(!BotSettingsSlots.TryReadShare(Pack("{}"),out _,out _,out _),"missing JSON fields accepted");
            Check(!BotSettingsSlots.TryReadShare(Pack("null"),out _,out _,out _),"null JSON accepted");
            var bad=new BotSettingsSlot{Name="bad"};
            bad.Fields.Add(new BotSlotField{Path="Core.VisibleDistance",Difficulty=ESainBotDifficulty.normal,Value="NaN"});
            Check(!BotSettingsSlots.TryReadShare(BotSettingsSlots.Share(bad),out _,out _,out _),"NaN accepted");
        });
        Test("global field status compares collection contents and scalar changes", () =>
        {
            var baseline=new SAIN.Preset.Shared.GlobalSettings.Categories.General.DoorTacticSettings();
            var current=new SAIN.Preset.Shared.GlobalSettings.Categories.General.DoorTacticSettings();
            var rows=SettingsFieldStatus.Rows(current,baseline).ToArray();
            Check(rows.All(r=>!r.Changed),"same values marked changed");
            current.CarveOpenLeaves=!baseline.CarveOpenLeaves;
            Check(SettingsFieldStatus.Rows(current,baseline).Single(r=>r.Field.Name=="CarveOpenLeaves").Changed,"changed NEW field not marked");
            Check(SettingsFieldStatus.SameValue(new List<int>{1,2},new List<int>{1,2}),"same lists differ");
            Check(!SettingsFieldStatus.SameValue(new List<int>{1,2},new List<int>{1,3}),"different lists match");
        });
        Test("slots reuse empty cells and enforce maximum", () =>
        {
            var slots=new BotSettingsSlots();
            for(int i=0;i<9;i++) Check(slots.Add(new BotSettingsSlot{Name="x"})==i,"wrong slot reused");
            Check(slots.Add(new BotSettingsSlot{Name="full"})==-1,"more than nine slots");
            slots.Delete(3); Check(slots.Add(new BotSettingsSlot{Name="reuse"})==3,"empty cell not reused");
        });
        Test("selection level and slot use each target's level baseline without touching globals", () =>
        {
            LevelGlobals Globals(float sightRatio) => new(){VisibleDist=sightRatio,GainSight=1,Scattering=1,Hearing=1,AccuracySpeed=1,Recoil=1,FasterCqb=true};
            var first=New(); var second=New();
            foreach(var s in first.Settings.Values) s.Core.VisibleDistance=200;
            foreach(var s in second.Settings.Values) s.Core.VisibleDistance=400;
            var current=Globals(1); var easy=Globals(.5f);
            LevelSources Sources(SAINSettingsGroupClass reference) => new(){Reference=reference,Current=current,Easy=easy,Hard=Globals(1),Normal=Globals(1),EasyBot=reference,NormalBot=reference,HardBot=reference};
            var a=BotLevels.Snapshot(first); var b=BotLevels.Snapshot(second);
            BotSelectionSettings.ApplyLevel(new[]{a,b},EBotLevel.Easy,g=>Sources(g==a?first:second));
            Check(BotLevels.NormalSlot(a).Core.VisibleDistance==100 && BotLevels.NormalSlot(b).Core.VisibleDistance==200,"levels not based on each bot");
            var slot=BotSettingsSlots.Capture("easy",a,Sources(first));
            Check(slot.Level==EBotLevel.Easy && slot.Fields.Count==0,"unadjusted level values saved as field overrides");
            var target=BotLevels.Snapshot(second);
            BotSettingsSlots.Apply(slot,target,Sources(second));
            Check(target.DifficultyLevel==EBotLevel.Easy && BotLevels.NormalSlot(target).Core.VisibleDistance==200,"slot flattened another bot's level");
            Check(current.VisibleDist==1 && easy.VisibleDist==.5f,"global settings changed");
        });
        Test("NEW release family includes Remix7 and excludes remake6", () =>
        {
            Check(SettingsFieldStatus.IsNew("remake7") && SettingsFieldStatus.IsNew("Remix7.2"),"current release not NEW");
            Check(!SettingsFieldStatus.IsNew("remake6") && !SettingsFieldStatus.IsNew("Remix6.9"),"old release marked NEW");
            var field=typeof(SAIN.Preset.Shared.GlobalSettings.Categories.General.DoorTacticSettings).GetField("CarveOpenLeaves");
            Check(SettingsFieldStatus.IsNew(field),"open door leaf setting missing NEW");
        });
        Test("batch skips global and fixed vanilla without changing globals or outsiders", () =>
        {
            var a=New(); var b=New(); b.WildSpawnType=ESainWildSpawnType.assault;
            var c=New(); c.WildSpawnType=ESainWildSpawnType.marksman;
            var global=new SAIN.Preset.Shared.GlobalSettings.Categories.General.VanillaBotSettings{VanillaScavs=true};
            var editable=BotSelectionSettings.Editable(new[]{a,b,c},global).ToArray();
            float before=BotLevels.NormalSlot(b).Core.VisibleDistance;
            BotSelectionSettings.Apply(editable,sight,250);
            Check(editable.Length==1 && BotLevels.NormalSlot(a).Core.VisibleDistance==250,"editable selection wrong");
            Check(BotLevels.NormalSlot(b).Core.VisibleDistance==before && global.VanillaScavs,"vanilla or global changed");
        });
    }
}
