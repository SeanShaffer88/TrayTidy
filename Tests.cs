using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Web.Script.Serialization;
public class Tests {
 static int count;static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++;}
 static void Reject(Settings s,string name){bool rejected=false;try{Engine.Validate(s);}catch{rejected=true;}Check(rejected,name);}
 [STAThread] public static int Main(){string root=Path.Combine(Path.Combine(Environment.CurrentDirectory,"work"),"TrayTidy-tests-"+Guid.NewGuid());Directory.CreateDirectory(root);try{
  string watch=Path.Combine(root,"Inbox");Directory.CreateDirectory(watch);
  var f=new Folder{Path=watch};f.Rules.Add(new Rule{Patterns="*.pdf;*.DOCX",Destination="Docs",MinimumAgeSeconds=0});f.Rules.Add(new Rule{Patterns="*",Destination="Other",MinimumAgeSeconds=0});var s=new Settings();s.Folders.Add(f);Engine.Validate(s);
  Action<string> add=name=>{string p=Path.Combine(watch,name);File.WriteAllText(p,"fixture");File.SetLastWriteTimeUtc(p,DateTime.UtcNow.AddMinutes(-2));};
  add("report.PDF");add("letter.docx");add("image.png");add("pending.crdownload");add("partial.tmp");Directory.CreateDirectory(Path.Combine(watch,"Nested"));File.WriteAllText(Path.Combine(watch,"Nested","hidden.pdf"),"fixture");
  var plans=Engine.Plan(s,Console.WriteLine);Check(plans.Count==3,"multiple patterns, case-insensitivity, partial and nested exclusions");Check(plans.Single(p=>p.Source.EndsWith("report.PDF")).Destination.Contains("Docs"),"first match wins");
  string pdf=plans.Single(p=>p.Source.EndsWith("report.PDF")).Source;File.SetLastWriteTimeUtc(pdf,DateTime.UtcNow);Check(Engine.Plan(s,Console.WriteLine).Count==2,"settling delay");File.SetLastWriteTimeUtc(pdf,DateTime.UtcNow.AddMinutes(-2));
  f.Rules[0].MinimumAgeSeconds=600;Check(Engine.Plan(s,Console.WriteLine).Count==1,"age-gated first match does not fall through");f.Rules[0].MinimumAgeSeconds=0;
  var move=plans.Single(p=>p.Source.EndsWith("report.PDF"));Directory.CreateDirectory(Path.GetDirectoryName(move.Destination));File.WriteAllText(move.Destination,"original");string moved=Engine.Move(move);Check(moved.EndsWith("report (1).PDF")&&File.ReadAllText(move.Destination)=="original"&&!File.Exists(move.Source),"collision preserves existing file");
  var locked=plans.Single(p=>p.Source.EndsWith("letter.docx"));using(var h=new FileStream(locked.Source,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){bool blocked=false;try{Engine.Move(locked);}catch(IOException){blocked=true;}Check(blocked&&File.Exists(locked.Source),"locked file deferred without loss");}
  Engine.Move(locked);Check(File.Exists(locked.Destination),"deferred file can retry");
  f.Enabled=false;Check(Engine.Plan(s,Console.WriteLine).Count==0,"disabled folder");f.Enabled=true;f.Rules[1].Enabled=false;Check(Engine.Plan(s,Console.WriteLine).Count==0,"disabled rule");f.Rules[1].Enabled=true;
  var serializer=new JavaScriptSerializer();var clone=serializer.Deserialize<List<Rule>>(serializer.Serialize(f.Rules));clone[0].Destination="Independent";Check(f.Rules[0].Destination=="Docs","copied rules are independent");
  f.Rules[0].Destination=".";Reject(s,"same-folder destination rejected");f.Rules[0].Destination="Docs";
  s.Folders.Add(new Folder{Path=Path.Combine(watch,"Nested")});Reject(s,"overlapping watched folders rejected");s.Folders.RemoveAt(1);
  string other=Path.Combine(root,"OtherInbox");s.Folders.Add(new Folder{Path=other});f.Rules[0].Destination=other;Reject(s,"watch-to-watch loop rejected");f.Rules[0].Destination="Docs";
  f.Rules[0].MinimumAgeSeconds=-1;Reject(s,"negative age rejected");f.Rules[0].MinimumAgeSeconds=0;
  f.Rules[0].Patterns="folder/*.pdf";Reject(s,"path patterns rejected");f.Rules[0].Patterns="*.pdf";
  var restored=serializer.Deserialize<Settings>(serializer.Serialize(s));Engine.Validate(restored);Check(restored.Folders.Count==2&&restored.Folders[0].Rules.Count==2&&restored.Paused,"settings round trip");
  var presets=MainForm.PresetRules("All four");Check(presets.Count==4&&presets.Select(x=>x.Destination).Distinct().Count()==4,"four category presets");
  var presetSettings=new Settings();var presetFolder=new Folder{Path=watch,Rules=presets};presetSettings.Folders.Add(presetFolder);Engine.Validate(presetSettings);Check(MainForm.PresetRules("Images").Count==1,"individual preset is valid");
  using(var icon=typeof(MainForm).Assembly.GetManifestResourceStream("TrayTidy.ico")){Check(icon!=null&&icon.Length>1000,"custom multi-resolution icon embedded");}
  // Instantiate the real UI without displaying it; do not change user settings.
  string portable=Path.Combine(root,"portable");Directory.CreateDirectory(portable);string local=Path.Combine(root,"local");Check(MainForm.SettingsPath(portable,local)==Path.Combine(local,"TrayTidy","settings.json"),"installed settings use local application data");File.WriteAllText(Path.Combine(portable,"portable.flag"),"");Check(MainForm.SettingsPath(portable,local)==Path.Combine(portable,"data","settings.json"),"portable settings stay beside executable");
  using(var ui=new MainForm(Path.Combine(root,"ui-settings.json"))){
   Check(ui.Controls.Count>0&&ui.Text.Contains("TrayTidy"),"settings form constructs");
   Check(ui.BackColor==System.Drawing.Color.FromArgb(12,16,23),"terminal dark palette applied");
   Directory.CreateDirectory(other);string second=Path.Combine(other,"second.pdf");File.WriteAllText(second,"fixture");File.SetLastWriteTimeUtc(second,DateTime.UtcNow.AddMinutes(-2));s.Folders[1].Rules.Add(new Rule{Patterns="*.pdf",Destination="Separate",MinimumAgeSeconds=0});s.Paused=false;
   string config=Path.Combine(root,"settings.json");File.WriteAllText(config,serializer.Serialize(s));var flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(MainForm).GetField("store",flags).SetValue(ui,config);typeof(MainForm).GetField("settings",flags).SetValue(ui,s);
   var task=(System.Threading.Tasks.Task)typeof(MainForm).GetMethod("Scan",flags).Invoke(ui,null);while(!task.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}task.GetAwaiter().GetResult();
   Check(File.Exists(Path.Combine(watch,"Other","image.png"))&&File.Exists(Path.Combine(other,"Separate","second.pdf")),"background scan organizes two folders independently");
   s.Folders[0].Rules[0].Destination="Unsaved edit";typeof(MainForm).GetMethod("TogglePause",flags).Invoke(ui,null);var saved=serializer.Deserialize<Settings>(File.ReadAllText(config));Check(saved.Paused&&saved.Folders[0].Rules[0].Destination=="Docs"&&s.Folders[0].Rules[0].Destination=="Unsaved edit","pause preserves unsaved edits without committing them");
  }
  Console.WriteLine(count+" checks passed");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
}
