using System;
using System.Collections.Generic;
using System.Linq;

namespace PhuocDai.Saga
{
    // Pure C# rules model: no Unity objects, UI state or network dependencies.
    [Serializable] public class Definition
    {
        public string Id, Name, Color;
        public int Cost, Reduction, Symbols = 1;
        public int[] CoreLevels, BPLevels;
    }
    public enum Step { Mulligan, Main, Attack, FlashBefore, Block, FlashAfter, Finished }
    public sealed class Spirit
    {
        public int Id, Regular; public bool Soul, Exhausted; public Definition Card;
        public int Cores { get { return Regular + (Soul ? 1 : 0); } }
        public int Level { get { int lv=0; for(int i=0;i<Card.CoreLevels.Length;i++) if(Cores>=Card.CoreLevels[i]) lv=i+1; return lv; } }
        public int BP { get { return Level==0 ? 0 : Card.BPLevels[Level-1]; } }
    }
    public sealed class Player
    {
        public int Life=5, Reserve=3, TrashCores, Turns;
        public bool ReserveSoul=true, TrashSoul;
        public readonly List<Definition> Deck=new List<Definition>(), Hand=new List<Definition>(), Trash=new List<Definition>();
        public readonly List<Spirit> Field=new List<Spirit>();
    }
    // Source 0 = reserve; otherwise a spirit instance ID. Soul is tracked separately.
    [Serializable] public struct CorePick
    {
        public int Source; public bool Soul;
        public CorePick(int source, bool soul=false) { Source=source; Soul=soul; }
    }
    public sealed class Payment
    {
        public readonly List<CorePick> Cost=new List<CorePick>();
        public readonly List<CorePick> Maintain=new List<CorePick>();
    }
    [Serializable] public sealed class MatchCommand
    {
        public string Kind;
        public int Player, Index, Source, Target;
        public bool Flag;
        public CorePick[] Cost, Maintain;
    }
    public sealed class Match
    {
        public readonly Player[] Players={new Player(),new Player()};
        public readonly List<MatchCommand> Commands=new List<MatchCommand>();
        public readonly List<string> Log=new List<string>();
        public Step Phase { get; private set; }
        public int Active { get; private set; }
        public int Priority { get; private set; }
        public int Winner { get; private set; } = -1;
        public int Turn { get; private set; }
        public int Attacker { get; private set; }
        public int Blocker { get; private set; }
        public int First { get; private set; }
        public int Seed { get; private set; }
        private int serial=1, passes, mulligans;
        private readonly Random random;
        public int DecisionPlayer { get { return Phase==Step.Mulligan || Phase==Step.FlashBefore || Phase==Step.FlashAfter ? Priority : Phase==Step.Block ? 1-Active : Active; } }
        public Match(IEnumerable<Definition> a, IEnumerable<Definition> b, int seed, int first)
        {
            if(first<0 || first>1) throw new ArgumentException("Invalid first player.");
            Seed=seed;random=new Random(seed);First=Active=Priority=first;
            var decks=new[]{a.ToList(),b.ToList()};
            for(int i=0;i<2;i++) {
                ValidateDeck(decks[i]); Players[i].Deck.AddRange(decks[i]); Shuffle(Players[i].Deck); Draw(i,4);
            }
            Phase=Step.Mulligan;Say("Opening hands: first player decides mulligan first.");
        }
        public static void ValidateDeck(List<Definition> cards)
        {
            if(cards==null)throw new ArgumentException("Missing deck.");
            if(cards.Count<50 || cards.Count>60) throw new ArgumentException("A Saga deck needs 50–60 cards.");
            if(cards.Any(c=>c==null || string.IsNullOrEmpty(c.Name))) throw new ArgumentException("Invalid card.");
            if(cards.GroupBy(c=>c.Name.Trim(),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>4)) throw new ArgumentException("Maximum four copies per card name.");
            if(cards.Any(c=>c.CoreLevels==null || c.BPLevels==null || c.CoreLevels.Length==0 || c.CoreLevels.Length!=c.BPLevels.Length)) throw new ArgumentException("Missing battle data.");
            foreach(var c in cards) {
                if(c.Cost<0 || c.Reduction<0 || c.Symbols<1 || string.IsNullOrEmpty(c.Id) || string.IsNullOrEmpty(c.Color))throw new ArgumentException("Invalid card definition.");
                for(int i=0;i<c.CoreLevels.Length;i++)if(c.CoreLevels[i]<1 || c.BPLevels[i]<=0 || (i>0 && c.CoreLevels[i]<=c.CoreLevels[i-1]))throw new ArgumentException("Invalid spirit levels.");
            }
        }
        void Say(string s) { Log.Add(s); if(Log.Count>180) Log.RemoveAt(0); }
        string Who(int p) { return p==0 ? "You" : "AI"; }
        void Shuffle(List<Definition> d) { for(int i=d.Count-1;i>0;i--) {int j=random.Next(i+1);var c=d[i];d[i]=d[j];d[j]=c;} }
        void Draw(int p,int n) { while(n-->0 && Players[p].Deck.Count>0) { Players[p].Hand.Add(Players[p].Deck[0]);Players[p].Deck.RemoveAt(0); } }
        void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
        public void Mulligan(int p,bool replace)
        {
            Require(Phase==Step.Mulligan && p==Priority,"Not your mulligan decision.");
            var pl=Players[p];
            if(replace) { pl.Deck.AddRange(pl.Hand);pl.Hand.Clear();Draw(p,4);Shuffle(pl.Deck); }
            else Draw(p,1);
            Commands.Add(new MatchCommand{Kind="Mulligan",Player=p,Flag=replace});
            Say(Who(p)+(replace ? " mulligans: four cards." : " keeps: gains a fifth card."));
            if(++mulligans==2) BeginTurn(); else Priority=1-Priority;
        }
        void BeginTurn()
        {
            Turn++; var p=Players[Active];p.Turns++;
            Say("Turn "+Turn+" — "+Who(Active)+": Start.");
            if(p.Deck.Count==0) { Finish(1-Active,"empty deck at Start Step");return; }
            if(Turn!=1) {p.Reserve++;Say("Core: +1 reserve.");} else Say("First turn: skip Core Step.");
            Draw(Active,1);Say("Draw: +1 card.");
            foreach(var s in p.Field) s.Exhausted=false;
            p.Reserve+=p.TrashCores;p.TrashCores=0;
            if(p.TrashSoul) {p.ReserveSoul=true;p.TrashSoul=false;}
            Say("Refresh: ready spirits; trash cores return.");Phase=Step.Main;
        }
        public int Cost(int p,Definition card) { return Math.Max(0,card.Cost-Math.Min(card.Reduction,Players[p].Field.Where(s=>s.Card.Color==card.Color).Sum(s=>s.Card.Symbols))); }
        public List<CorePick> Available(int p)
        {
            var pl=Players[p];var result=new List<CorePick>();
            for(int i=0;i<pl.Reserve;i++)result.Add(new CorePick(0));
            if(pl.ReserveSoul)result.Add(new CorePick(0,true));
            foreach(var s in pl.Field) {for(int i=0;i<s.Regular;i++)result.Add(new CorePick(s.Id));if(s.Soul)result.Add(new CorePick(s.Id,true));}
            return result;
        }
        public Payment Suggest(int p,Definition card)
        {
            // Reserve first, then surplus field cores. Never auto-sacrifice a spirit.
            var pl=Players[p];var picks=new List<CorePick>();
            for(int i=0;i<pl.Reserve;i++)picks.Add(new CorePick(0));
            if(pl.ReserveSoul)picks.Add(new CorePick(0,true));
            foreach(var s in pl.Field) {
                int excess=s.Cores-s.Card.CoreLevels[0];
                for(int i=0;i<Math.Min(excess,s.Regular);i++)picks.Add(new CorePick(s.Id));
                if(excess>s.Regular && s.Soul)picks.Add(new CorePick(s.Id,true));
            }
            int cost=Cost(p,card),keep=card.CoreLevels[0];
            if(picks.Count<cost+keep)return null;
            var plan=new Payment();plan.Cost.AddRange(picks.Take(cost));plan.Maintain.AddRange(picks.Skip(cost).Take(keep));return plan;
        }
        void ValidatePicks(int p,IEnumerable<CorePick> picks)
        {
            var available=Available(p);
            foreach(var pick in picks) {int i=available.FindIndex(c=>c.Source==pick.Source && c.Soul==pick.Soul);Require(i>=0,"Selected core is unavailable or selected twice.");available.RemoveAt(i);}
        }
        void Take(int p,CorePick pick)
        {
            var pl=Players[p];
            if(pick.Source==0) {if(pick.Soul)pl.ReserveSoul=false;else pl.Reserve--;}
            else {var s=pl.Field.First(x=>x.Id==pick.Source);if(pick.Soul)s.Soul=false;else s.Regular--;}
        }
        void PutReserve(int p,bool soul) {if(soul)Players[p].ReserveSoul=true;else Players[p].Reserve++;}
        public void Summon(int p,int handIndex,Payment payment)
        {
            Require(Phase==Step.Main && Active==p,"Summon during your Main Step.");
            var pl=Players[p];Require(handIndex>=0 && handIndex<pl.Hand.Count,"Choose a card in hand.");var c=pl.Hand[handIndex];
            Require(payment!=null && payment.Cost.Count==Cost(p,c),"Incorrect cost payment.");
            Require(payment.Maintain.Count>=c.CoreLevels[0],"Provide at least LV1 cores.");
            ValidatePicks(p,payment.Cost.Concat(payment.Maintain));
            // Cost reduction is locked before paying. Check self-destruction after allocation.
            foreach(var k in payment.Cost) {Take(p,k);if(k.Soul)pl.TrashSoul=true;else pl.TrashCores++;}
            var spirit=new Spirit{Id=serial++,Card=c};
            foreach(var k in payment.Maintain) {Take(p,k);if(k.Soul)spirit.Soul=true;else spirit.Regular++;}
            pl.Hand.RemoveAt(handIndex);pl.Field.Add(spirit);Cleanup(p);
            Commands.Add(new MatchCommand{Kind="Summon",Player=p,Index=handIndex,Cost=payment.Cost.ToArray(),Maintain=payment.Maintain.ToArray()});
            Say(Who(p)+" summons "+c.Name+" (paid "+payment.Cost.Count+").");
        }
        public void MoveCore(int p,int from,int to,bool soul)
        {
            Require(Phase==Step.Main && Active==p,"Move cores during your Main Step.");Require(from!=to,"Choose different areas.");
            var pl=Players[p];Require(to==0 || pl.Field.Any(s=>s.Id==to),"Invalid destination.");var pick=new CorePick(from,soul);ValidatePicks(p,new[]{pick});Take(p,pick);
            if(to==0)PutReserve(p,soul);else {var s=pl.Field.First(x=>x.Id==to);if(soul)s.Soul=true;else s.Regular++;}
            Commands.Add(new MatchCommand{Kind="MoveCore",Player=p,Source=from,Target=to,Flag=soul});
            Cleanup(p);Say(Who(p)+" moves "+(soul ? "soul core." : "a core."));
        }
        void Cleanup(int p) {foreach(var s in Players[p].Field.Where(x=>x.Level==0).ToList())Destroy(p,s,"self-destroyed");}
        void Destroy(int p,Spirit s,string reason)
        {
            var pl=Players[p];pl.Field.Remove(s);pl.Trash.Add(s.Card);pl.Reserve+=s.Regular;if(s.Soul)pl.ReserveSoul=true;
            Say(s.Card.Name+" "+reason+"; its cores return to reserve.");
        }
        public void EndMain(int p)
        {
            Require(Phase==Step.Main && p==Active,"Not your Main Step.");
            Commands.Add(new MatchCommand{Kind="EndMain",Player=p});
            if(Turn==1) {Say("First turn: skip Attack Step.");EndTurn();} else {Phase=Step.Attack;Say(Who(p)+": Attack Step.");}
        }
        public void Attack(int p,int id)
        {
            Require(Phase==Step.Attack && p==Active,"Not your Attack Step.");var s=Players[p].Field.FirstOrDefault(x=>x.Id==id);Require(s!=null && !s.Exhausted,"Choose a ready spirit.");
            Commands.Add(new MatchCommand{Kind="Attack",Player=p,Index=id});
            s.Exhausted=true;Attacker=id;Blocker=0;Phase=Step.FlashBefore;Priority=1-p;passes=0;Say(s.Card.Name+" attacks. Defender has flash priority.");
        }
        public void PassFlash(int p)
        {
            Require((Phase==Step.FlashBefore || Phase==Step.FlashAfter) && Priority==p,"Not your flash priority.");
            Commands.Add(new MatchCommand{Kind="PassFlash",Player=p});
            Say(Who(p)+" passes flash.");Priority=1-p;
            if(++passes==2) {passes=0;if(Phase==Step.FlashBefore)Phase=Step.Block;else Resolve();}
        }
        public void Block(int p,int id)
        {
            Require(Phase==Step.Block && p==1-Active,"Not your block decision.");
            if(id==0) {Commands.Add(new MatchCommand{Kind="Block",Player=p,Index=0});Say(Who(p)+" takes the attack.");Resolve();return;}
            var s=Players[p].Field.FirstOrDefault(x=>x.Id==id);Require(s!=null && !s.Exhausted,"Choose a ready blocker.");
            Commands.Add(new MatchCommand{Kind="Block",Player=p,Index=id});
            s.Exhausted=true;Blocker=id;Phase=Step.FlashAfter;Priority=p;passes=0;Say(s.Card.Name+" blocks. Second flash window.");
        }
        void Resolve()
        {
            var a=Players[Active].Field.FirstOrDefault(s=>s.Id==Attacker);var b=Players[1-Active].Field.FirstOrDefault(s=>s.Id==Blocker);
            if(a!=null && Blocker==0) {var d=Players[1-Active];int damage=Math.Min(d.Life,a.Card.Symbols);d.Life-=damage;d.Reserve+=damage;Say(Who(1-Active)+" loses "+damage+" life; cores move to reserve.");if(d.Life==0){Finish(Active,"life reached zero");return;}}
            else if(a!=null && b!=null) {int ap=a.BP,bp=b.BP;Say("Battle: "+ap+" vs "+bp+" BP.");if(ap<=bp)Destroy(Active,a,"destroyed in battle");if(bp<=ap)Destroy(1-Active,b,"destroyed in battle");}
            Attacker=Blocker=0;Phase=Step.Attack;
        }
        public void EndAttack(int p) {Require(Phase==Step.Attack && p==Active,"Finish the current battle first.");Commands.Add(new MatchCommand{Kind="EndAttack",Player=p});EndTurn();}
        void EndTurn() {Say(Who(Active)+": End Step.");Active=1-Active;BeginTurn();}
        public void Concede(int p) {Require(Phase!=Step.Finished && p>=0 && p<2,"Game already finished.");Commands.Add(new MatchCommand{Kind="Concede",Player=p});Finish(1-p,"opponent conceded");}
        public void Apply(MatchCommand c)
        {
            if(c==null)throw new ArgumentException("Missing replay command.");
            switch(c.Kind)
            {
                case "Mulligan":Mulligan(c.Player,c.Flag);break;
                case "Summon":var plan=new Payment();if(c.Cost!=null)plan.Cost.AddRange(c.Cost);if(c.Maintain!=null)plan.Maintain.AddRange(c.Maintain);Summon(c.Player,c.Index,plan);break;
                case "MoveCore":MoveCore(c.Player,c.Source,c.Target,c.Flag);break;
                case "EndMain":EndMain(c.Player);break;
                case "Attack":Attack(c.Player,c.Index);break;
                case "PassFlash":PassFlash(c.Player);break;
                case "Block":Block(c.Player,c.Index);break;
                case "EndAttack":EndAttack(c.Player);break;
                case "Concede":Concede(c.Player);break;
                default:throw new ArgumentException("Unsupported replay command: "+c.Kind);
            }
        }
        void Finish(int p,string reason) {Winner=p;Phase=Step.Finished;Say(Who(p)+" wins — "+reason+".");}
    }
    public static class SagaAI
    {
        // Only reads its own hand and public field/life; never inspects the opponent's hand/deck order.
        public static void Act(Match m)
        {
            if(m.Phase==Step.Finished)return;int p=m.DecisionPlayer;var own=m.Players[p];var enemy=m.Players[1-p];
            if(m.Phase==Step.Mulligan) {m.Mulligan(p,own.Hand.Min(c=>c.Cost)>3);return;}
            if(m.Phase==Step.FlashBefore || m.Phase==Step.FlashAfter){m.PassFlash(p);return;}
            if(m.Phase==Step.Block) {
                var attack=enemy.Field.First(s=>s.Id==m.Attacker);
                var ready=own.Field.Where(s=>!s.Exhausted).OrderBy(s=>s.BP).ToList();
                var block=ready.FirstOrDefault(s=>s.BP>=attack.BP);
                if(block==null && own.Life<=2)block=ready.FirstOrDefault();
                m.Block(p,block==null?0:block.Id);return;
            }
            if(m.Phase==Step.Main) {
                var choices=own.Hand.Select((c,i)=>new{Card=c,Index=i,Plan=m.Suggest(p,c)}).Where(x=>x.Plan!=null).OrderBy(x=>m.Cost(p,x.Card)).ThenByDescending(x=>x.Card.BPLevels[0]).ToList();
                if(choices.Count>0) {var c=choices[0];m.Summon(p,c.Index,c.Plan);return;}
                // Invest remaining reserve cores in reachable level upgrades.
                foreach(var s in own.Field.OrderByDescending(s=>s.BP)) if(s.Level<s.Card.CoreLevels.Length && s.Card.CoreLevels[s.Level]-s.Cores<=own.Reserve+(own.ReserveSoul?1:0)) {m.MoveCore(p,0,s.Id,own.Reserve==0);return;}
                m.EndMain(p);return;
            }
            if(m.Phase==Step.Attack) {
                var ready=own.Field.Where(s=>!s.Exhausted).OrderByDescending(s=>s.BP).ToList();
                var blockers=enemy.Field.Where(s=>!s.Exhausted).ToList();
                var attack=ready.FirstOrDefault(s=>blockers.Count==0 || s.BP>=blockers.Max(b=>b.BP) || own.Field.Count>blockers.Count+1);
                if(attack!=null)m.Attack(p,attack.Id);else m.EndAttack(p);
            }
        }
    }
}
