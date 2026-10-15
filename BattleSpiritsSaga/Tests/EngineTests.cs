using System;
using System.Linq;
using System.Collections.Generic;
using PhuocDai.Saga;
class EngineTests
{
 static int checks;
 static void Check(bool b,string name){checks++;if(!b)throw new Exception("FAIL: "+name);}
 static Definition Card(int i=0,int cost=1){return new Definition{Id="T"+i,Name="Test "+i,Color="RED",Cost=cost,Reduction=1,CoreLevels=new[]{1,2,4},BPLevels=new[]{1000,3000,6000}};}
 static List<Definition> Deck(){return Enumerable.Range(0,13).SelectMany(i=>Enumerable.Repeat(Card(i),4)).ToList();}
 static Match Ready(int first=0){var m=new Match(Deck(),Deck(),123,first);m.Mulligan(first,false);m.Mulligan(1-first,false);return m;}
 static void Reject(Action a,string name){bool fail=false;try{a();}catch(InvalidOperationException){fail=true;}Check(fail,name);}
 static Spirit Add(Match m,int p,int id,int regular=1,bool soul=false){var s=new Spirit{Id=id,Regular=regular,Soul=soul,Card=Card(id)};m.Players[p].Field.Add(s);return s;}
 static void Main()
 {
  var m=new Match(Deck(),Deck(),17,0);Check(m.Players[0].Hand.Count==4,"opening four");Check(m.Players[0].Reserve==3 && m.Players[0].ReserveSoul && m.Players[0].Life==5,"initial core zones");
  Reject(()=>m.Mulligan(1,false),"mulligan order");m.Mulligan(0,false);Check(m.Players[0].Hand.Count==5,"keep bonus");m.Mulligan(1,true);Check(m.Players[1].Hand.Count==4,"mulligan no bonus");Check(m.Players[0].Hand.Count==6,"first player draws on turn one");Check(m.Players[0].Reserve==3,"skip first core");
  Reject(()=>m.Attack(0,99),"no attack in main");m.EndMain(0);Check(m.Active==1 && m.Phase==Step.Main,"skip first attack");Check(m.Players[1].Reserve==4,"second player gains core");
  m=Ready();var plan=m.Suggest(0,m.Players[0].Hand[0]);m.Summon(0,0,plan);Check(m.Players[0].TrashCores==1 && m.Players[0].Field[0].Cores==1,"summon payment versus maintenance");Check(m.Cost(0,Card())==0,"cost reduction");var s=m.Players[0].Field[0];m.MoveCore(0,0,s.Id,false);Check(s.Level==2 && s.BP==3000,"core level BP");m.MoveCore(0,0,s.Id,true);Check(s.Soul && !m.Players[0].ReserveSoul,"soul moved distinctly");m.MoveCore(0,s.Id,0,true);Check(m.Players[0].ReserveSoul,"soul returned");
  var hand=m.Players[0].Hand.Count;var bad=new Payment();bad.Maintain.Add(new CorePick(0));bad.Maintain.Add(new CorePick(0));Reject(()=>m.Summon(0,0,bad),"duplicate unavailable cores atomic rejection");Check(m.Players[0].Hand.Count==hand,"invalid payment preserves hand");
  m.MoveCore(0,s.Id,0,false);m.MoveCore(0,s.Id,0,false);Check(m.Players[0].Field.Count==0 && m.Players[0].Trash.Count==1,"self destruction");
  m=Ready();m.EndMain(0);var atk=Add(m,1,100);var block=Add(m,0,200);m.EndMain(1);m.Attack(1,100);Check(atk.Exhausted && m.Priority==0,"attacker exhausts defender flash first");Reject(()=>m.Block(0,200),"cannot block before flash passes");m.PassFlash(0);m.PassFlash(1);m.Block(0,200);Check(block.Exhausted && m.Phase==Step.FlashAfter,"block exhaust and second flash");m.PassFlash(0);m.PassFlash(1);Check(m.Players.All(p=>p.Field.Count==0),"equal BP destroys both");Check(m.Players[0].Trash.Count==1 && m.Players[1].Trash.Count==1,"battle trash");
  m=Ready();m.EndMain(0);atk=Add(m,1,100);m.EndMain(1);m.Attack(1,100);m.PassFlash(0);m.PassFlash(1);int reserve=m.Players[0].Reserve;m.Block(0,0);Check(m.Players[0].Life==4 && m.Players[0].Reserve==reserve+1,"unblocked life becomes reserve");Check(m.Phase==Step.Attack,"no after-block flash when unblocked");Reject(()=>m.Attack(1,100),"exhausted attacker cannot repeat");
  m=Ready();m.EndMain(0);m.Players[0].Deck.Clear();Check(m.Phase!=Step.Finished,"empty deck not immediate loss");m.EndMain(1);m.EndAttack(1);Check(m.Winner==1,"deck-out at start only");
  m=Ready();m.Concede(0);Check(m.Winner==1 && m.Phase==Step.Finished,"concede");Reject(()=>m.EndMain(0),"finished game rejects actions");
  m=Ready();m.EndMain(0);atk=Add(m,1,101,2);block=Add(m,0,201,1);m.EndMain(1);m.Attack(1,101);m.PassFlash(0);m.PassFlash(1);m.Block(0,201);m.PassFlash(0);m.PassFlash(1);Check(m.Players[1].Field.Count==1 && m.Players[0].Field.Count==0,"higher BP survives");
  m=Ready();m.EndMain(0);atk=Add(m,1,102);m.Players[0].Life=1;m.EndMain(1);m.Attack(1,102);m.PassFlash(0);m.PassFlash(1);m.Block(0,0);Check(m.Winner==1 && m.Phase==Step.Finished,"lethal life damage wins");
  m=Ready();m.Players[0].TrashCores=2;m.Players[0].ReserveSoul=false;m.Players[0].TrashSoul=true;atk=Add(m,0,103);atk.Exhausted=true;m.EndMain(0);m.EndMain(1);m.EndAttack(1);Check(!atk.Exhausted && m.Players[0].TrashCores==0 && m.Players[0].ReserveSoul && !m.Players[0].TrashSoul,"refresh readies and recovers both core types");
  bool invalid=false;try{new Match(Deck().Take(49),Deck(),1,0);}catch(ArgumentException){invalid=true;}Check(invalid,"deck minimum");invalid=false;try{new Match(Enumerable.Repeat(Card(),52),Deck(),1,0);}catch(ArgumentException){invalid=true;}Check(invalid,"four copies maximum");
  // Locked cost reduction remains valid when its source is sacrificed to pay.
  m=Ready();m.Players[0].Reserve=0;m.Players[0].ReserveSoul=false;s=Add(m,0,70,2);var pay=new Payment();pay.Maintain.Add(new CorePick(70));pay.Maintain.Add(new CorePick(70));m.Summon(0,0,pay);Check(m.Players[0].Field.Count==1 && m.Players[0].Field[0].Cores==2,"reduction locked before sacrificing source");
  var json=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"TrainingData.json")));
  var real=new List<Definition>();
  foreach(var e in json.RootElement.EnumerateArray()) real.Add(new Definition { Id=e.GetProperty("id").GetString(),Name=e.GetProperty("title").GetString(),Color=e.GetProperty("color").GetString(),Cost=e.GetProperty("cost").GetInt32(),Reduction=e.GetProperty("reduction").GetInt32(),CoreLevels=e.GetProperty("cores").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),BPLevels=e.GetProperty("bp").EnumerateArray().Select(v=>v.GetInt32()).ToArray() });
  var realDeck=real.Take(13).SelectMany(c=>Enumerable.Repeat(c,4)).ToList();
  int longest=0;
  for(int seed=0;seed<200;seed++){
   var rng=new Random(seed);
   var deckA=seed<100?realDeck:real.OrderBy(c=>rng.Next()).Take(13).SelectMany(c=>Enumerable.Repeat(c,4)).ToList();
   var deckB=seed<100?realDeck:real.OrderBy(c=>rng.Next()).Take(13).SelectMany(c=>Enumerable.Repeat(c,4)).ToList();
   m=new Match(deckA,deckB,seed,seed%2);int actions=0;
   while(m.Phase!=Step.Finished && actions++<3000){SagaAI.Act(m);foreach(var p in m.Players){Check(p.Reserve>=0 && p.TrashCores>=0,"nonnegative cores");Check(p.Deck.Count+p.Hand.Count+p.Field.Count+p.Trash.Count==52,"card conservation");Check((p.ReserveSoul?1:0)+(p.TrashSoul?1:0)+p.Field.Count(x=>x.Soul)==1,"one soul conserved");int gain=p.Turns-((p==m.Players[m.First] && p.Turns>0)?1:0);Check(p.Life+p.Reserve+p.TrashCores+p.Field.Sum(x=>x.Cores)+(p.ReserveSoul?1:0)+(p.TrashSoul?1:0)==9+gain,"all core conservation");}}
   Check(m.Phase==Step.Finished,"AI match terminates");
   var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
   var serialized=System.Text.Json.JsonSerializer.Serialize(m.Commands,options);
   var restored=System.Text.Json.JsonSerializer.Deserialize<List<MatchCommand>>(serialized,options);
   var playback=new Match(deckA,deckB,seed,seed%2);foreach(var command in restored)playback.Apply(command);
   Check(playback.Winner==m.Winner && playback.Turn==m.Turn && string.Join("|",playback.Log)==string.Join("|",m.Log),"deterministic replay matches original");longest=Math.Max(longest,actions);
  }
  ProfileTests.Run();
  Console.WriteLine("PASS: "+checks+" assertions; 200 AI matches completed; longest "+longest+" actions.");
 }
}
