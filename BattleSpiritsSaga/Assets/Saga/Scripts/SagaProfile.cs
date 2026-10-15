using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
namespace PhuocDai.Saga
{
    [Serializable] public sealed class SavedDeck
    {
        public string Id, Name;
        public List<string> Cards=new List<string>();
    }
    [Serializable] public sealed class MatchRecord
    {
        public int Seed, First, Winner, Turns;
        public string FinishedUtc, DeckName;
        public string[] PlayerDeck, AIDeck;
        public List<MatchCommand> Commands=new List<MatchCommand>();
    }
    [Serializable] public sealed class SagaProfile
    {
        public int Version=1, Wins, Losses;
        public float AIDelay=.65f;
        public bool Sound=true;
        public string ActiveDeck;
        public List<SavedDeck> Decks=new List<SavedDeck>();
        public List<MatchRecord> History=new List<MatchRecord>();
    }
    public static class DeckRules
    {
        public static List<Definition> Resolve(IEnumerable<string> ids,IEnumerable<Definition> catalog)
        {
            if(ids==null)throw new ArgumentException("This deck has no card list.");
            var byId=catalog.ToDictionary(c=>c.Id,StringComparer.Ordinal);var deck=new List<Definition>();
            foreach(var id in ids){Definition c;if(id==null || !byId.TryGetValue(id,out c))throw new ArgumentException("Unsupported card: "+id);deck.Add(c);}
            Match.ValidateDeck(deck);return deck;
        }
        public static string Problem(IEnumerable<string> ids,IEnumerable<Definition> catalog)
        {try{Resolve(ids,catalog);return "";}catch(ArgumentException e){return e.Message;}}
        public static string CleanName(string name)
        {name=(name??"").Trim();if(name.Length==0 || name.Length>48 || name.Any(char.IsControl))throw new ArgumentException("Deck name must contain 1–48 printable characters.");return name;}
    }
    // Serializer is injected: Unity uses JsonUtility; standalone tests use System.Text.Json.
    public sealed class ProfileStore
    {
        readonly string path;readonly Func<SagaProfile,string> encode;readonly Func<string,SagaProfile> decode;
        public string Warning {get;private set;}
        public ProfileStore(string directory,Func<SagaProfile,string> encode,Func<string,SagaProfile> decode)
        {Directory.CreateDirectory(directory);path=Path.Combine(directory,"saga-profile.json");this.encode=encode;this.decode=decode;}
        static SagaProfile Validate(SagaProfile p)
        {
            if(p==null || p.Version!=1 || p.Decks==null || p.History==null)throw new InvalidDataException("Unsupported or damaged profile format.");
            if(p.Decks.Any(d=>d==null || string.IsNullOrEmpty(d.Id) || d.Cards==null))throw new InvalidDataException("Damaged saved deck.");
            if(p.Decks.Select(d=>d.Id).Distinct().Count()!=p.Decks.Count)throw new InvalidDataException("Duplicate deck identifiers.");
            if(p.History.Any(h=>h==null || h.Commands==null || h.PlayerDeck==null || h.AIDeck==null))throw new InvalidDataException("Damaged match history.");
            p.AIDelay=Math.Max(.2f,Math.Min(2f,p.AIDelay));return p;
        }
        public SagaProfile Load()
        {
            Warning="";
            if(!File.Exists(path) && !File.Exists(path+".bak"))return new SagaProfile();
            try{return Validate(decode(File.ReadAllText(path)));}
            catch(Exception e) when(e is IOException || e is ArgumentException || e is InvalidOperationException || e is FormatException)
            {
                if(!File.Exists(path+".bak"))throw new InvalidDataException("Cannot read profile; original file was preserved.",e);
                var restored=Validate(decode(File.ReadAllText(path+".bak")));
                // Preserve damaged bytes before restoring. Never overwrite the valid backup with damaged data.
                if(File.Exists(path))File.Copy(path,path+".corrupt-"+DateTime.UtcNow.Ticks);
                File.Copy(path+".bak",path,true);Warning="Profile recovered from backup.";return restored;
            }
        }
        public void Save(SagaProfile profile)
        {
            Validate(profile);var json=encode(profile);var temp=path+".tmp";
            using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(f)){w.Write(json);w.Flush();f.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
        }
    }
}
