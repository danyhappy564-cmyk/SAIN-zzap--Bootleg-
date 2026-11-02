using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using SAIN.Preset.Shared.BotSettings.SAINSettings;
using SAIN.Preset.Shared.Enums;
using SAIN.Preset.Shared.Models.Preset.Personalities;

namespace SAIN.Preset.Shared.Preset;

[DataContract]
public sealed class BotSlotField
{
    [DataMember(IsRequired=true)] public string Path { get; set; }
    [DataMember(IsRequired=true)] public ESainBotDifficulty Difficulty { get; set; }
    [DataMember(IsRequired=true)] public string Value { get; set; }
}

[DataContract]
public sealed class BotSettingsSlot
{
    [DataMember(IsRequired=true)] public string Name { get; set; }
    [DataMember(EmitDefaultValue=false)] public string CreatedUtc { get; set; }
    [DataMember(IsRequired=true)] public EBotLevel? Level { get; set; }
    [DataMember(IsRequired=true)] public bool? Vanilla { get; set; }
    [DataMember(IsRequired=true)] public List<BotSlotField> Fields { get; set; } = new();
}

/// <summary>F6·웹 공용 슬롯. JSON은 기존 /sain/data 저장소에 보관하고 프리셋 저장과 독립적입니다.</summary>
[DataContract]
public sealed class BotSettingsSlots
{
    public const string DataKey="botSettingsSlots";
    public const int MaxSlots=9;
    public const int MaxBytes=64*1024;
    public const string Prefix="SAIN1-";
    [DataMember(IsRequired=true)] public List<BotSettingsSlot> Slots { get; set; } = new() { null,null,null };

    public static string PathOf(BotFieldRow row) => row.Category.Name+"."+(row.Nested==null?"":row.Nested.Name+".")+row.Field.Name;
    private const string ModifierPath="DifficultyModifier";
    private static readonly Dictionary<string,BotFieldRow> Paths=BuildPaths();
    private static bool Plain(Type type) => type==typeof(float) || type==typeof(int) || type==typeof(bool) || type.IsEnum || type==typeof(List<EPersonality>);
    private static Dictionary<string,BotFieldRow> BuildPaths()
    {
        var paths=BotAdvancedFields.Rows().ToDictionary(PathOf,StringComparer.Ordinal);
        foreach(var category in typeof(SAINSettingsClass).GetFields(BindingFlags.Public|BindingFlags.Instance))
        {
            if(!category.FieldType.IsClass || category.FieldType==typeof(string)) continue;
            foreach(var field in category.FieldType.GetFields(BindingFlags.Public|BindingFlags.Instance))
            {
                if(Plain(field.FieldType))
                {
                    var row=new BotFieldRow(category.Name,category,null,field); paths[PathOf(row)]=row;
                }
                else if(field.FieldType.IsClass && field.FieldType!=typeof(string) && !typeof(System.Collections.IEnumerable).IsAssignableFrom(field.FieldType))
                    foreach(var leaf in field.FieldType.GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>Plain(f.FieldType)))
                    { var row=new BotFieldRow(category.Name,category,field,leaf); paths[PathOf(row)]=row; }
            }
        }
        return paths;
    }
    private static string Text(object value) => value is List<EPersonality> pool?string.Join(",",pool)
        :value is float number?number.ToString("R",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture);
    private static bool Same(object a,object b) => a is List<EPersonality> aa && b is List<EPersonality> bb
        ? aa.SequenceEqual(bb) : BotSelectionSettings.Same(a,b);
    public static BotSettingsSlot Capture(string name, SAINSettingsGroupClass source, LevelSources sources)
    {
        var result=new BotSettingsSlot { Name=CleanName(name),CreatedUtc=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture),
            Level=source.DifficultyLevel==EBotLevel.None?null:source.DifficultyLevel,Vanilla=source.UseVanillaAI };
        var baseline=BotSelectionSettings.Baseline(source,sources,true);
        foreach(var pair in source.Settings)
        {
            if(pair.Value==null) continue;
            var reference=baseline!=null && baseline.Settings.TryGetValue(pair.Key,out var same)?same:BotLevels.NormalSlot(baseline);
            foreach(var row in Paths.Values)
            {
                var value=row.Get(pair.Value);
                if(value!=null && (reference==null || !Same(value,row.Get(reference))))
                    result.Fields.Add(new BotSlotField { Path=PathOf(row),Difficulty=pair.Key,Value=Text(value) });
            }
        }
        if(baseline==null || !BotSelectionSettings.Same(source.DifficultyModifier,baseline.DifficultyModifier))
            result.Fields.Add(new BotSlotField { Path=ModifierPath,Difficulty=ESainBotDifficulty.normal,Value=Text(source.DifficultyModifier) });
        return result;
    }
    public static string CleanName(string name)
    {
        string clean=(name??"").Trim();
        if(clean.Length==0) throw new ArgumentException("name");
        return clean.Length>24?clean.Substring(0,24):clean;
    }
    public int Add(BotSettingsSlot slot)
    {
        if(slot==null) throw new ArgumentNullException(nameof(slot));
        slot.CreatedUtc??=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture);
        int index=Slots.FindIndex(s=>s==null);
        if(index>=0) { Slots[index]=slot; return index; }
        if(Slots.Count>=MaxSlots) return -1;
        Slots.Add(slot); return Slots.Count-1;
    }
    public void Rename(int index,string name) { if(Slots[index]!=null) Slots[index].Name=CleanName(name); }
    public void Delete(int index) { Slots[index]=null; }

    /// <summary>호출자는 전역·고정 바닐라를 제외합니다. 난이도→필드→AI 순서, 누락 필드/칸 개수를 반환합니다.</summary>
    public static int Apply(BotSettingsSlot slot,SAINSettingsGroupClass target,LevelSources sources)
    {
        if(slot==null || target?.Settings==null) return 0;
        if(slot.Level.HasValue) BotLevels.Apply(slot.Level.Value,target,sources);
        int skipped=0;
        foreach(var item in slot.Fields)
        {
            if(item.Path==ModifierPath)
            {
                if(TryModifier(item.Value,out float modifier)) target.DifficultyModifier=modifier; else skipped++;
                continue;
            }
            if(!Paths.TryGetValue(item.Path??"",out var row) || !target.Settings.TryGetValue(item.Difficulty,out var settings)
                || row.Owner(settings)==null || !TryValue(row,item.Value,out var value)) { skipped++; continue; }
            BotAdvancedEdit.Apply(target,item.Difficulty,item.Difficulty,row,value);
        }
        if(slot.Vanilla.HasValue) target.UseVanillaAI=slot.Vanilla.Value;
        return skipped;
    }
    private static bool TryValue(BotFieldRow row,string text,out object value)
    {
        value=null;
        var type=row.Field.FieldType;
        if(type==typeof(bool)) { if(bool.TryParse(text,out var b)) value=b; }
        else if(type==typeof(float))
        {
            if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var f) && !float.IsNaN(f) && !float.IsInfinity(f)
                && (row.Range==null || f>=row.Range.Min && f<=row.Range.Max)) value=f;
        }
        else if(type==typeof(int))
        {
            if(int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var i)
                && (row.Range==null || i>=row.Range.Min && i<=row.Range.Max)) value=i;
        }
        else if(type.IsEnum && Enum.TryParse(type,text,false,out var e) && Enum.IsDefined(type,e)) value=e;
        else if(type==typeof(List<EPersonality>) && text!=null)
        {
            var pool=new List<EPersonality>();
            foreach(string part in text.Length==0?Array.Empty<string>():text.Split(','))
            {
                if(!Enum.TryParse(part,false,out EPersonality p) || p==EPersonality.None || !Enum.IsDefined(typeof(EPersonality),p) || pool.Contains(p)) return false;
                pool.Add(p);
            }
            value=pool;
        }
        return value!=null;
    }
    private static bool TryModifier(string text,out float value) => float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)
        && !float.IsNaN(value) && value>=0.01f && value<=1f;
    private static byte[] Encode<T>(T value)
    {
        using var stream=new MemoryStream();
        new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);
        return stream.ToArray();
    }
    private static T Decode<T>(byte[] bytes)
    {
        using var stream=new MemoryStream(bytes);
        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
    }
    public string Serialize() => Encoding.UTF8.GetString(Encode(this));
    public static BotSettingsSlots Deserialize(string json)
    {
        if(string.IsNullOrWhiteSpace(json)) return new();
        // 9 individual slots can each approach the share limit.
        if(Encoding.UTF8.GetByteCount(json)>MaxSlots*MaxBytes) throw new SerializationException("size");
        var result=Decode<BotSettingsSlots>(Encoding.UTF8.GetBytes(json));
        if(result?.Slots==null || result.Slots.Count>MaxSlots) throw new SerializationException("slots");
        while(result.Slots.Count<3) result.Slots.Add(null);
        foreach(var slot in result.Slots.Where(s=>s!=null)) Validate(slot,out _);
        return result;
    }
    public static string Share(BotSettingsSlot slot)
    {
        var payload=new BotSettingsSlot { Name=slot.Name,Level=slot.Level,Vanilla=slot.Vanilla,Fields=slot.Fields };
        var bytes=Encode(payload);
        if(bytes.Length>MaxBytes) throw new SerializationException("size");
        using var compressed=new MemoryStream();
        using(var gzip=new GZipStream(compressed,CompressionLevel.Optimal,true)) gzip.Write(bytes,0,bytes.Length);
        return Prefix+Convert.ToBase64String(compressed.ToArray()).TrimEnd('=').Replace('+','-').Replace('/','_');
    }
    public static bool TryReadShare(string code,out BotSettingsSlot slot,out int skipped,out string reason)
    {
        slot=null; skipped=0; reason=null;
        code=code?.Trim();
        if(code==null || !code.StartsWith(Prefix,StringComparison.Ordinal)) { reason="prefix"; return false; }
        if(code.Length>MaxBytes*2) { reason="size"; return false; }
        try
        {
            string raw=code.Substring(Prefix.Length);
            if(raw.Any(c=>!char.IsLetterOrDigit(c) && c!='-' && c!='_')) throw new FormatException();
            raw=raw.Replace('-','+').Replace('_','/'); raw+=new string('=',(4-raw.Length%4)%4);
            using var input=new MemoryStream(Convert.FromBase64String(raw));
            using var gzip=new GZipStream(input,CompressionMode.Decompress);
            using var output=new MemoryStream();
            var buffer=new byte[4096]; int count;
            while((count=gzip.Read(buffer,0,buffer.Length))>0)
            {
                if(output.Length+count>MaxBytes) { reason="size"; return false; }
                output.Write(buffer,0,count);
            }
            var parsed=Decode<BotSettingsSlot>(output.ToArray());
            Validate(parsed,out skipped);
            parsed.CreatedUtc=null;
            slot=parsed; return true;
        }
        catch(Exception ex) when(ex is FormatException || ex is InvalidDataException || ex is SerializationException
            || ex is ArgumentException || ex is System.Xml.XmlException || ex is IOException)
        { reason="format"; return false; }
    }
    private static void Validate(BotSettingsSlot slot,out int skipped)
    {
        skipped=0;
        if(slot==null || slot.Name==null || slot.Name.Trim().Length==0 || slot.Name.Length>24 || slot.Fields==null
            || slot.Fields.Count>2048 || slot.Level.HasValue && (slot.Level==EBotLevel.None || !Enum.IsDefined(typeof(EBotLevel),slot.Level.Value)))
            throw new SerializationException("format");
        var valid=new List<BotSlotField>();
        var seen=new HashSet<string>();
        foreach(var item in slot.Fields)
        {
            if(item?.Path==null || !Enum.IsDefined(typeof(ESainBotDifficulty),item.Difficulty)) throw new SerializationException("format");
            if(item.Path==ModifierPath)
            {
                if(!TryModifier(item.Value,out _) || !seen.Add(ModifierPath)) throw new SerializationException("value");
                valid.Add(item); continue;
            }
            if(!Paths.TryGetValue(item.Path,out var row)) { skipped++; continue; }
            if(!TryValue(row,item.Value,out _) || !seen.Add(item.Path+":"+item.Difficulty)) throw new SerializationException("value");
            valid.Add(item);
        }
        slot.Fields=valid;
    }
    public static string Error(string reason,bool korean) => reason switch
    {
        "prefix" => korean?"SAIN1-로 시작하는 공유 코드가 필요합니다.":"Share code must start with SAIN1-.",
        "size" => korean?"공유 코드가 너무 큽니다 (최대 64KB).":"Share code is too large (64KB maximum).",
        _ => korean?"공유 코드의 형식이나 값이 올바르지 않습니다.":"Invalid share code format or value.",
    };
}
