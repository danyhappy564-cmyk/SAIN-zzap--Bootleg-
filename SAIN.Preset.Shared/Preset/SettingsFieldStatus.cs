using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SAIN.Preset.Shared.Attributes;

namespace SAIN.Preset.Shared.Preset;

public static class SettingsFieldStatus
{
    public static readonly int ReleaseFamily=Family(SAINVersionInfo.ZzapRelease);
    public static bool IsNew(string release) => ReleaseFamily>0 && Family(release)==ReleaseFamily;
    private static int Family(string release)
    {
        if(string.IsNullOrWhiteSpace(release)) return 0;
        release=release.Trim();
        string number=release.StartsWith("remake",StringComparison.OrdinalIgnoreCase)?release.Substring(6)
            :release.StartsWith("remix",StringComparison.OrdinalIgnoreCase)?release.Substring(5):"";
        string major=number.Split('.')[0];
        return int.TryParse(major,out int family)?family:0;
    }
    public static bool IsNew(FieldInfo field) => IsNew(field?.GetCustomAttribute<NewInAttribute>()?.Value);

    /// <summary>편집 화면에서만 순회합니다. 중첩은 최대 6단계, 컬렉션은 기존 편집기를 유지합니다.</summary>
    public static IEnumerable<SettingsObjectField> Rows(object target,object baseline,string path="",int depth=0,string heading="")
    {
        if(target==null || depth>6) yield break;
        foreach(var field in target.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))
        {
            if(field.IsInitOnly || field.GetCustomAttribute<HiddenAttribute>()!=null) continue;
            object value=field.GetValue(target);
            object reference=baseline!=null && baseline.GetType()==target.GetType()?field.GetValue(baseline):null;
            bool hasBaseline=baseline!=null && baseline.GetType()==target.GetType();
            string key=path.Length==0?field.Name:path+"."+field.Name;
            bool named=field.GetCustomAttribute<NameAttribute>()!=null || field.GetCustomAttribute<NameAndDescriptionAttribute>()!=null;
            if(field.FieldType==typeof(float) || field.FieldType==typeof(int) || field.FieldType==typeof(bool) || field.FieldType.IsEnum)
            {
                if(named) yield return new SettingsObjectField(key,field,target,reference,heading,hasBaseline);
            }
            else if(value is IEnumerable || value is string)
            {
                if(named) yield return new SettingsObjectField(key,field,target,reference,heading,hasBaseline);
            }
            else if(value!=null && field.FieldType.IsClass)
            {
                string title=field.GetCustomAttribute<NameAndDescriptionAttribute>()?.Name ?? field.GetCustomAttribute<NameAttribute>()?.Value ?? field.Name;
                string parent=heading.Length==0?title:heading+" › "+title;
                foreach(var row in Rows(value,reference,key,depth+1,parent)) yield return row;
            }
        }
    }
    public static bool SameValue(object a,object b,int depth=0)
    {
        if(ReferenceEquals(a,b)) return true;
        if(a==null || b==null || a.GetType()!=b.GetType()) return false;
        if(a.GetType().IsValueType || a is string) return BotSelectionSettings.Same(a,b);
        if(depth>=6) return false;
        if(a is IDictionary ad && b is IDictionary bd)
        {
            if(ad.Count!=bd.Count) return false;
            foreach(var key in ad.Keys) if(!bd.Contains(key) || !SameValue(ad[key],bd[key],depth+1)) return false;
            return true;
        }
        if(a is IList al && b is IList bl)
        {
            if(al.Count!=bl.Count) return false;
            for(int i=0;i<al.Count;i++) if(!SameValue(al[i],bl[i],depth+1)) return false;
            return true;
        }
        return a.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance).All(f=>SameValue(f.GetValue(a),f.GetValue(b),depth+1));
    }
}

public sealed class SettingsObjectField
{
    public readonly string Path;
    public readonly FieldInfo Field;
    public readonly object Owner;
    public readonly object Baseline;
    public readonly string Heading;
    public readonly bool HasBaseline;
    public bool Changed => HasBaseline && !SettingsFieldStatus.SameValue(Field.GetValue(Owner),Baseline);
    public bool New => SettingsFieldStatus.IsNew(Field);
    public SettingsObjectField(string path,FieldInfo field,object owner,object baseline,string heading="",bool hasBaseline=true)
    { Path=path; Field=field; Owner=owner; Baseline=baseline; Heading=heading; HasBaseline=hasBaseline; }
}
