using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("TrayTidy")]
[assembly: System.Reflection.AssemblyVersion("0.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.3.0.0")]

public class Rule {
 public bool Enabled {get;set;} public string Patterns {get;set;} public string Destination {get;set;} public int MinimumAgeSeconds {get;set;}
 public Rule() {Enabled=true; Patterns="*.pdf"; Destination="Documents"; MinimumAgeSeconds=30;}
}
public class Folder {
 public string Path {get;set;} public bool Enabled {get;set;} public List<Rule> Rules {get;set;}
 public Folder(){Enabled=true;Rules=new List<Rule>();}
 public override string ToString(){return Path;}
}
public class Settings {public List<Folder> Folders=new List<Folder>(); public bool Paused=true;}
public class MovePlan {public string Source, Destination; public override string ToString(){return Source+"  →  "+Destination;}}
public static class Engine {
 public static string Full(string p){return System.IO.Path.GetFullPath(p).TrimEnd('\\')+"\\";}
 public static void Validate(Settings s){
  var paths=new List<string>();
  foreach(var f in s.Folders){
   if(string.IsNullOrWhiteSpace(f.Path)||!System.IO.Path.IsPathRooted(f.Path))throw new Exception("Watch folders must be absolute paths.");
   string root=Full(f.Path);
   if(paths.Any(p=>root.StartsWith(p,StringComparison.OrdinalIgnoreCase)||p.StartsWith(root,StringComparison.OrdinalIgnoreCase)))throw new Exception("Watch folders must not overlap.");
   paths.Add(root);
   foreach(var r in f.Rules){
    if(string.IsNullOrWhiteSpace(r.Patterns)||string.IsNullOrWhiteSpace(r.Destination)||r.MinimumAgeSeconds<0)throw new Exception("Each rule needs patterns, a destination, and a nonnegative age.");
    if(r.Patterns.IndexOfAny(new[]{'\\','/'})>=0)throw new Exception("Patterns match filenames only. Separate patterns with semicolons.");
    string dest=Full(System.IO.Path.IsPathRooted(r.Destination)?r.Destination:System.IO.Path.Combine(f.Path,r.Destination));
    if(dest.Equals(root,StringComparison.OrdinalIgnoreCase))throw new Exception("Destination must differ from its watched folder.");
   }
  }
  foreach(var f in s.Folders)foreach(var r in f.Rules){
   string dest=Full(System.IO.Path.IsPathRooted(r.Destination)?r.Destination:System.IO.Path.Combine(f.Path,r.Destination));
   if(paths.Any(p=>dest.Equals(p,StringComparison.OrdinalIgnoreCase)))throw new Exception("A destination cannot be another watched folder.");
  }
 }
 public static List<MovePlan> Plan(Settings s, Action<string> log){
  var result=new List<MovePlan>();
  foreach(var f in s.Folders.Where(x=>x.Enabled))try{
   if(!Directory.Exists(f.Path))continue;
   if((File.GetAttributes(f.Path)&FileAttributes.ReparsePoint)!=0){log("Skipped linked watch folder: "+f.Path);continue;}
   foreach(string file in Directory.GetFiles(f.Path))try{
    var info=new FileInfo(file);
    if((info.Attributes&FileAttributes.ReparsePoint)!=0||new[]{".part",".crdownload",".tmp",".download"}.Contains(info.Extension.ToLowerInvariant()))continue;
    foreach(var r in f.Rules.Where(x=>x.Enabled)){
     bool match=r.Patterns.Split(';').Any(p=>Regex.IsMatch(info.Name,"^"+Regex.Escape(p.Trim()).Replace("\\*",".*").Replace("\\?",".")+"$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant));
     if(!match)continue;
     if((DateTime.UtcNow-info.LastWriteTimeUtc).TotalSeconds>=Math.Max(5,r.MinimumAgeSeconds)){
      string target=System.IO.Path.IsPathRooted(r.Destination)?r.Destination:System.IO.Path.Combine(f.Path,r.Destination);
      result.Add(new MovePlan{Source=file,Destination=System.IO.Path.Combine(System.IO.Path.GetFullPath(target),info.Name)});
     }
     break;
    }
   }catch(Exception e){log(e.Message);}
  }catch(Exception e){log(e.Message);}
  return result;
 }
 public static string Move(MovePlan p){
  var before=new FileInfo(p.Source);long length=before.Length;DateTime written=before.LastWriteTimeUtc;
  using(var stream=new FileStream(p.Source,FileMode.Open,FileAccess.Read,FileShare.None)){}
  var after=new FileInfo(p.Source);
  if(after.Length!=length||after.LastWriteTimeUtc!=written)throw new IOException("File is still changing: "+p.Source);
  string dir=System.IO.Path.GetDirectoryName(p.Destination);
  // Reject directory junctions/symlinks on destination ancestors.
  for(var d=new DirectoryInfo(dir);d!=null;d=d.Parent)if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Destination contains a linked directory: "+d.FullName);
  Directory.CreateDirectory(dir);
  for(int i=0;i<10000;i++){
   string dest=i==0?p.Destination:System.IO.Path.Combine(dir,System.IO.Path.GetFileNameWithoutExtension(p.Destination)+" ("+i+")"+System.IO.Path.GetExtension(p.Destination));
   if(File.Exists(dest)||Directory.Exists(dest))continue;
   try{File.Move(p.Source,dest);return dest;}catch(IOException){if(File.Exists(dest))continue;throw;}
  }
  throw new IOException("Too many filename collisions.");
 }
}
public partial class MainForm:Form {
 Settings settings; string store; ListBox folders=new ListBox(); DataGridView rules=new DataGridView(); CheckBox enabled=new CheckBox(); Button pause=new Button(); TextBox log=new TextBox(); NotifyIcon tray; System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); bool busy, exiting, loading; CancellationTokenSource stop=new CancellationTokenSource();
 static JavaScriptSerializer json=new JavaScriptSerializer();
 public static string SettingsPath(string appDirectory,string localData){return File.Exists(System.IO.Path.Combine(appDirectory,"portable.flag"))?System.IO.Path.Combine(appDirectory,"data","settings.json"):System.IO.Path.Combine(localData,"TrayTidy","settings.json");}
 public MainForm():this(null){}
 public MainForm(string settingsPath){
  store=settingsPath??SettingsPath(AppDomain.CurrentDomain.BaseDirectory,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
  settings=new Settings();
  if(File.Exists(store))try{settings=json.Deserialize<Settings>(File.ReadAllText(store));Engine.Validate(settings);}catch(Exception e){MessageBox.Show("Settings could not be loaded. The app will close to preserve them.\n"+e.Message);throw;}
  BuildUI();
  timer.Interval=5000;timer.Tick+=async delegate{await Scan();};timer.Start();RefreshFolders();UpdatePause();WriteLog("Ready. Preview rules before resuming. Configuration: "+store);
 }
 void AddButton(Control parent,string text,Action action){var b=new Button{Text=text,AutoSize=true,Height=28};b.Click+=delegate{try{action();}catch(Exception e){MessageBox.Show(e.Message,"TrayTidy");}};parent.Controls.Add(b);}
 void RefreshFolders(){folders.DataSource=null;folders.DataSource=settings.Folders;SelectFolder();}
 void SelectFolder(){loading=true;var f=folders.SelectedItem as Folder;enabled.Checked=f!=null&&f.Enabled;rules.DataSource=f==null?null:new BindingList<Rule>(f.Rules);loading=false;UpdateSelection();}
 void AddFolder(){using(var d=new FolderBrowserDialog()){d.Description="Choose a folder to organize";if(d.ShowDialog()!=DialogResult.OK)return;settings.Folders.Add(new Folder{Path=d.SelectedPath});RefreshFolders();folders.SelectedIndex=settings.Folders.Count-1;SetDirty(true);}}
 void Reorder(int delta){rules.EndEdit();var f=folders.SelectedItem as Folder;if(f==null||rules.CurrentRow==null)return;int i=rules.CurrentRow.Index,j=i+delta;if(i>=f.Rules.Count||j<0||j>=f.Rules.Count)return;var r=f.Rules[i];f.Rules.RemoveAt(i);f.Rules.Insert(j,r);SelectFolder();rules.CurrentCell=rules.Rows[j].Cells[1];}
 void CopyRules(){rules.EndEdit();var target=folders.SelectedItem as Folder;if(target==null)return;using(var dialog=new Form{Text="Copy rules to selected folder",Width=580,Height=350,StartPosition=FormStartPosition.CenterParent}){var list=new ListBox{Dock=DockStyle.Fill};foreach(var f in settings.Folders.Where(x=>x!=target))list.Items.Add(f);var button=new Button{Text="Replace selected folder's rules",Dock=DockStyle.Bottom,Height=38};button.Click+=delegate{if(list.SelectedItem==null)return;var source=(Folder)list.SelectedItem;target.Rules=json.Deserialize<List<Rule>>(json.Serialize(source.Rules));dialog.DialogResult=DialogResult.OK;};Theme(dialog);dialog.Controls.Add(list);dialog.Controls.Add(button);Theme(dialog);if(dialog.ShowDialog(this)==DialogResult.OK){SelectFolder();SetDirty(true);}}}
 bool Save(){try{if(!rules.EndEdit()||!Validate())return false;Engine.Validate(settings);Persist();SetDirty(false);WriteLog("Settings saved.");return true;}catch(Exception e){MessageBox.Show(e.Message,"Settings need attention");return false;}}
 void Persist(){PersistValue(settings);}
 void PersistValue(Settings value){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(store));string temp=store+".tmp";File.WriteAllText(temp,json.Serialize(value));if(File.Exists(store))File.Replace(temp,store,store+".bak");else File.Move(temp,store);}
 void UpdatePause(){pause.Text=settings.Paused?"▶  Resume":"Ⅱ  Pause";tray.Text=settings.Paused?"TrayTidy — paused":"TrayTidy — monitoring";UpdateStatus();}
 Settings Snapshot(){return json.Deserialize<Settings>(json.Serialize(settings));}
 void Preview(){if(!Save())return;var plans=Engine.Plan(Snapshot(),WriteLog);using(var d=new Form{Text="Preview — "+plans.Count+" eligible files (no files moved)",Width=950,Height=480,StartPosition=FormStartPosition.CenterParent}){var t=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=plans.Count==0?"No eligible files. Files must be at least 5 seconds old; rule age may require longer.":string.Join(Environment.NewLine,plans.Select(p=>p.ToString()))};d.Controls.Add(t);Theme(d);d.ShowDialog(this);}}
 async Task Scan(){if(busy||settings.Paused)return;busy=true;try{ // Only persisted settings run; unsaved grid edits cannot affect moves.
  var snapshot=json.Deserialize<Settings>(File.ReadAllText(store));Engine.Validate(snapshot);
  await Task.Run(delegate{foreach(var p in Engine.Plan(snapshot,WriteLog)){if(stop.IsCancellationRequested||settings.Paused)break;try{string dest=Engine.Move(p);Interlocked.Increment(ref movedCount);WriteLog("Moved: "+p.Source+" → "+dest);}catch(Exception e){WriteLog("Deferred: "+e.Message);}}});
 }catch(Exception e){WriteLog("Scan failed: "+e.Message);}finally{busy=false;lastScan=DateTime.Now;UpdateStatus();}}
 void WriteLog(string message){if(IsDisposed||Disposing)return;if(InvokeRequired){try{BeginInvoke(new Action<string>(WriteLog),message);}catch(InvalidOperationException){}return;}if(log.TextLength>50000)log.Clear();log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+message+Environment.NewLine);}
 [STAThread] public static void Main(string[] args){using(var mutex=new Mutex(true,"Local\\TrayTidy-"+Environment.UserName)){if(!mutex.WaitOne(0)){MessageBox.Show("TrayTidy is already running. Open it from the tray.");return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{Application.Run(new MainForm());}catch(Exception e){MessageBox.Show(e.Message,"TrayTidy could not start");}}}
}
