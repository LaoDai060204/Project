using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using PhuocDai.Saga;
static class ProfileTests
{
 public static void Run()
 {
  int count=0;Action<bool,string> check=(b,n)=>{count++;if(!b)throw new Exception("Profile FAIL: "+n);};
  var dir=Path.Combine(Path.GetTempPath(),"saga-tests-"+Guid.NewGuid().ToString("N"));
  try {
   var options=new JsonSerializerOptions{IncludeFields=true};
   var store=new ProfileStore(dir,p=>JsonSerializer.Serialize(p,options),s=>{try{return JsonSerializer.Deserialize<SagaProfile>(s,options);}catch(JsonException e){throw new ArgumentException("Invalid JSON",e);}});
   var profile=store.Load();check(profile.Decks.Count==0,"new profile");profile.Decks.Add(new SavedDeck{Id="one",Name="My deck",Cards=new List<string>{"ST01-004"}});profile.ActiveDeck="one";store.Save(profile);
   profile.Wins=2;store.Save(profile);check(store.Load().Wins==2,"save/load stats");check(store.Load().Decks[0].Cards[0]=="ST01-004","deck ids preserved");
   File.WriteAllText(Path.Combine(dir,"saga-profile.json"),"{bad-json");var recovered=store.Load();check(recovered.Wins==0 && store.Warning!="","valid backup recovery");check(Directory.GetFiles(dir,"*.corrupt-*").Length==1,"corrupt file preserved");store.Save(recovered);check(store.Load().Decks.Count==1,"recovered state can save");
   bool rejected=false;try{DeckRules.Resolve(new[]{"UNKNOWN"},new List<Definition>());}catch(ArgumentException){rejected=true;}check(rejected,"unsupported card rejected");
   rejected=false;try{DeckRules.CleanName("  ");}catch(ArgumentException){rejected=true;}check(rejected,"empty name rejected");check(DeckRules.CleanName("  Deck A ")=="Deck A","trim names");
   // A damaged profile with no backup must fail, not silently replace user data.
   File.Delete(Path.Combine(dir,"saga-profile.json.bak"));File.WriteAllText(Path.Combine(dir,"saga-profile.json"),"{still-bad");rejected=false;try{store.Load();}catch(InvalidDataException){rejected=true;}check(rejected && File.ReadAllText(Path.Combine(dir,"saga-profile.json"))=="{still-bad","do not overwrite unreadable profile");
   Console.WriteLine("PASS: "+count+" persistence/validation tests.");
  } finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
}
