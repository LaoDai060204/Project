using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
namespace PhuocDai.Saga
{
    public partial class SagaBattleView
    {
        enum Page { Home, Decks, History, Settings, Result }
        Page page;
        SagaProfile profile; ProfileStore storage; bool storageBlocked, outcomeWritten;
        string profileWarning="", deckSearch="", colorFilter="ALL";
        int collectionPage, historyPage; bool replayMode; int replayIndex;
        MatchRecord replay, lastResult;
        string[] startingPlayerDeck, startingAIDeck;
        Action confirmAction; string confirmText;
        AudioSource audioSource; AudioClip clickSound;
        SavedDeck ActiveDeck {get{return profile.Decks.FirstOrDefault(d=>d.Id==profile.ActiveDeck)??profile.Decks[0];}}
        List<Definition> Definitions {get{return catalog.cards.Select(c=>c.Rules()).ToList();}}
#if UNITY_EDITOR
        internal static string TestProfileDirectory=null;
#endif
        void InitializeProfile()
        {
            string directory=Application.persistentDataPath;
#if UNITY_EDITOR
            if(!string.IsNullOrEmpty(TestProfileDirectory))directory=TestProfileDirectory;
#endif
            try{storage=new ProfileStore(directory,p=>JsonUtility.ToJson(p,true),s=>JsonUtility.FromJson<SagaProfile>(s));profile=storage.Load();profileWarning=storage.Warning;}catch(Exception e){profile=new SagaProfile();storageBlocked=true;profileWarning=e.Message+" Saving is disabled; your file has not been overwritten.";}
            if(profile.Decks.Count==0){profile.Decks.Add(new SavedDeck{Id=Guid.NewGuid().ToString("N"),Name="Crimson Vanguard",Cards=catalog.cards.Take(13).SelectMany(c=>Enumerable.Repeat(c.id,4)).ToList()});profile.ActiveDeck=profile.Decks[0].Id;Persist();}
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;
            clickSound=AudioClip.Create("UI click",2205,1,22050,false);var samples=new float[2205];for(int i=0;i<samples.Length;i++)samples[i]=(float)Math.Sin(i*2*Math.PI*660/22050)*.045f*(1f-(float)i/samples.Length);clickSound.SetData(samples,0);
        }
        void Persist()
        {
            if(storageBlocked)return;
            try{storage.Save(profile);}catch(Exception e){profileWarning="Could not save changes: "+e.Message;}
        }
        void ClickSound(){if(profile!=null && profile.Sound && audioSource!=null)audioSource.PlayOneShot(clickSound);}
        void OnDestroy(){if(clickSound!=null)Destroy(clickSound);}
        void CompleteMatch()
        {
            if(match==null || match.Phase!=Step.Finished || outcomeWritten || replayMode)return;
            outcomeWritten=true;
            lastResult=new MatchRecord{Seed=match.Seed,First=match.First,Winner=match.Winner,Turns=match.Turn,FinishedUtc=DateTime.UtcNow.ToString("o"),DeckName=ActiveDeck.Name,PlayerDeck=startingPlayerDeck,AIDeck=startingAIDeck,Commands=new List<MatchCommand>(match.Commands)};
            if(match.Winner==0)profile.Wins++;else profile.Losses++;
            profile.History.Insert(0,lastResult);if(profile.History.Count>20)profile.History.RemoveAt(20);
            try{Persist();}catch(Exception e){profileWarning="Match finished but could not save: "+e.Message;}
        }
        void Navigate(Page next){page=next;menu=true;inspect=null;notice="";tossPending=false;confirmAction=null;}
        void RenderFrontEnd(RectTransform root)
        {
            Label(root,"BATTLE SPIRITS SAGA",.035f,.025f,.6f,.055f,32,gold);
            Button(root,"HOME",.60f,.03f,.09f,.043f,()=>Navigate(Page.Home),true,panel);
            Button(root,"DECKS",.70f,.03f,.09f,.043f,()=>Navigate(Page.Decks),true,panel);
            Button(root,"HISTORY",.80f,.03f,.09f,.043f,()=>Navigate(Page.History),true,panel);
            Button(root,"SETTINGS",.90f,.03f,.08f,.043f,()=>Navigate(Page.Settings),true,panel);
            Box(root,"Header rule",.035f,.095f,.94f,.002f,gold);
            switch(page){case Page.Home:HomeHub(root);break;case Page.Decks:DeckScreen(root);break;case Page.History:HistoryScreen(root);break;case Page.Settings:SettingsScreen(root);break;case Page.Result:ResultScreen(root);break;}
            Label(root,notice!=""?notice:profileWarning!=""?profileWarning:"PHUOC DAI  /  Development build 0.2.1  /  20 supported spirits  /  Offline AI",.035f,.945f,.94f,.045f,16,notice!=""||profileWarning!=""?new Color(1,.65f,.4f):ink);
            if(confirmAction!=null)ConfirmPanel(root);
        }
        void HomeHub(Transform root)
        {
            Label(root,"YOUR NEXT BATTLE\nSTARTS HERE.",.05f,.17f,.60f,.22f,56,gold);
            Label(root,"Manage your cores. Build your deck. Break through.",.052f,.41f,.58f,.06f,24);
            var stats=Box(root,"Career",.05f,.52f,.55f,.11f,panel);Label(stats,"WINS  "+profile.Wins+"      LOSSES  "+profile.Losses+"      SAVED DECKS  "+profile.Decks.Count,.04f,.12f,.92f,.75f,25);
            var d=ActiveDeck;string problem=DeckRules.Problem(d.Cards,Definitions);
            Label(root,"ACTIVE DECK  /  "+d.Name+"  ("+d.Cards.Count+")",.052f,.66f,.56f,.055f,22,gold);
            Label(root,problem==""?"Ready to play. Deck is valid for this supported card pool.":problem,.052f,.72f,.56f,.055f,18);
            if(!tossPending)Button(root,"PLAY VS AI",.05f,.81f,.27f,.08f,()=>{tossHuman=UnityEngine.Random.value<.5f;tossPending=true;},problem=="");
            else{
                Label(root,tossHuman?"Coin toss won. Choose your turn order.":"AI won the toss and chose to go first.",.05f,.79f,.56f,.045f,19,gold);
                if(tossHuman){Button(root,"GO FIRST",.05f,.85f,.25f,.065f,()=>StartMatch(0));Button(root,"GO SECOND",.33f,.85f,.25f,.065f,()=>StartMatch(1));}else Button(root,"BEGIN MATCH",.05f,.85f,.27f,.065f,()=>StartMatch(1));
            }
            if(!tossPending)Button(root,"EDIT DECK",.34f,.81f,.26f,.08f,()=>Navigate(Page.Decks),true,panel);
            Art(root,catalog.cards[3].art,.69f,.15f,.24f,.62f);
            Label(root,"SAGA RULES / VANILLA SPIRIT FORMAT\nMagic, Nexus, Burst and online play are not implemented yet.",.65f,.80f,.31f,.10f,18,ink,TextAnchor.MiddleCenter);
        }
        InputField Field(Transform parent,string text,float x,float y,float w,float h,Action<string> changed)
        {
            var r=Box(parent,"Input",x,y,w,h,new Color(.11f,.16f,.21f));var t=Label(r,"",.04f,.04f,.92f,.92f,19);var input=r.gameObject.AddComponent<InputField>();input.textComponent=t;input.text=text;input.characterLimit=48;input.onEndEdit.AddListener(value=>{if(rendering)return;try{changed(value);notice="";}catch(Exception e){notice=e.Message;}Render();});return input;
        }
        void SelectDeck(int delta){int i=profile.Decks.IndexOf(ActiveDeck);profile.ActiveDeck=profile.Decks[(i+delta+profile.Decks.Count)%profile.Decks.Count].Id;collectionPage=0;Persist();}
        void DeckScreen(Transform root)
        {
            var d=ActiveDeck;
            Button(root,"<",.035f,.12f,.04f,.052f,()=>SelectDeck(-1),profile.Decks.Count>1,panel);
            Field(root,d.Name,.085f,.12f,.33f,.052f,v=>{d.Name=DeckRules.CleanName(v);Persist();});
            Button(root,">",.425f,.12f,.04f,.052f,()=>SelectDeck(1),profile.Decks.Count>1,panel);
            Button(root,"NEW DECK",.48f,.12f,.13f,.052f,()=>{if(profile.Decks.Count>=20)throw new InvalidOperationException("Maximum 20 saved decks.");var n=new SavedDeck{Id=Guid.NewGuid().ToString("N"),Name="New deck"};profile.Decks.Add(n);profile.ActiveDeck=n.Id;Persist();});
            Button(root,"DUPLICATE",.62f,.12f,.13f,.052f,()=>{if(profile.Decks.Count>=20)throw new InvalidOperationException("Maximum 20 saved decks.");var n=new SavedDeck{Id=Guid.NewGuid().ToString("N"),Name=("Copy of "+d.Name).Substring(0,Math.Min(48,8+d.Name.Length)),Cards=new List<string>(d.Cards)};profile.Decks.Add(n);profile.ActiveDeck=n.Id;Persist();},true,panel);
            Button(root,"DELETE",.76f,.12f,.10f,.052f,()=>{confirmText="Delete deck \""+d.Name+"\"?";confirmAction=()=>{profile.Decks.Remove(d);profile.ActiveDeck=profile.Decks[0].Id;Persist();};},profile.Decks.Count>1,panel);
            Button(root,"PLAY",.87f,.12f,.105f,.052f,()=>Navigate(Page.Home),DeckRules.Problem(d.Cards,Definitions)=="");
            Field(root,deckSearch,.035f,.195f,.20f,.046f,v=>{deckSearch=v;collectionPage=0;});
            var colors=new[]{"ALL","RED","PURPLE","WHITE","YELLOW"};for(int i=0;i<colors.Length;i++){string color=colors[i];Button(root,color,.25f+i*.09f,.195f,.08f,.046f,()=>{colorFilter=color;collectionPage=0;},true,colorFilter==color?teal:panel);}
            var cards=catalog.cards.Where(c=>(colorFilter=="ALL"||c.color==colorFilter)&&(string.IsNullOrEmpty(deckSearch)||c.title.IndexOf(deckSearch,StringComparison.OrdinalIgnoreCase)>=0||c.id.IndexOf(deckSearch,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
            int pages=Math.Max(1,(cards.Count+9)/10);collectionPage=Math.Min(collectionPage,pages-1);
            for(int i=0;i<10 && collectionPage*10+i<cards.Count;i++){
                var c=cards[collectionPage*10+i];int count=d.Cards.Count(id=>id==c.id);float x=.035f+(i%5)*.132f,y=.265f+(i/5)*.306f;
                var tile=Box(root,c.title,x,y,.123f,.29f,panel);Art(tile,c.art,.06f,.02f,.88f,.69f);Label(tile,c.title,.025f,.715f,.95f,.11f,14,gold,TextAnchor.MiddleCenter);
                Button(tile,"−",.04f,.84f,.24f,.135f,()=>{d.Cards.Remove(c.id);Persist();},count>0,panel);
                Label(tile,count+" / 4",.30f,.84f,.40f,.135f,17,ink,TextAnchor.MiddleCenter);
                Button(tile,"+",.72f,.84f,.24f,.135f,()=>{d.Cards.Add(c.id);Persist();},count<4 && d.Cards.Count<60);
            }
            Button(root,"PREVIOUS",.035f,.895f,.15f,.04f,()=>collectionPage--,collectionPage>0,panel);Label(root,(collectionPage+1)+" / "+pages,.21f,.895f,.15f,.04f,18);Button(root,"NEXT",.40f,.895f,.15f,.04f,()=>collectionPage++,collectionPage<pages-1,panel);
            var sidebar=Box(root,"Deck summary",.715f,.195f,.26f,.74f,panel);Label(sidebar,"DECK  "+d.Cards.Count+" / 60",.07f,.03f,.86f,.065f,30,gold);
            var problem=DeckRules.Problem(d.Cards,Definitions);Label(sidebar,problem==""?"VALID  /  Ready for battle":"DRAFT  /  "+problem,.07f,.115f,.86f,.09f,18,problem==""?teal:gold);
            string list=string.Join("\n",d.Cards.GroupBy(id=>id).OrderBy(g=>g.Key).Select(g=>g.Count()+" × "+(catalog.cards.FirstOrDefault(c=>c.id==g.Key)?.title??g.Key)));
            Label(sidebar,list==""?"Add cards from the collection.":list,.07f,.235f,.86f,.65f,17);
            Label(sidebar,storageBlocked?"Saving disabled. See warning below.":"Changes save automatically.\n50–60 cards, up to 4 per name.",.07f,.90f,.86f,.08f,15);
        }
        void SettingsScreen(Transform r)
        {
            Label(r,"SETTINGS",.07f,.16f,.8f,.08f,42,gold);Label(r,"AI action speed",.07f,.30f,.5f,.06f,25);
            var delays=new[]{1.2f,.65f,.25f};var names=new[]{"RELAXED","NORMAL","FAST"};for(int i=0;i<3;i++){float delay=delays[i];Button(r,names[i],.07f+i*.22f,.39f,.20f,.075f,()=>{profile.AIDelay=delay;Persist();},true,Math.Abs(profile.AIDelay-delay)<.01f?teal:panel);}
            Button(r,profile.Sound?"UI SOUND: ON":"UI SOUND: OFF",.07f,.55f,.42f,.075f,()=>{profile.Sound=!profile.Sound;Persist();});
            Label(r,"Decks and the last 20 matches are saved locally.\nNo account or online service is required.\n\nSupported format: 20 no-effect spirits, with Saga core and battle rules.\nThis is a development build, not a complete implementation of every Saga card.",.07f,.68f,.84f,.20f,23);
        }
        void HistoryScreen(Transform r)
        {
            Label(r,"MATCH HISTORY",.05f,.14f,.8f,.07f,40,gold);
            if(profile.History.Count==0){Label(r,"Finish your first match to see it here.",.05f,.34f,.8f,.1f,26);return;}
            int pages=(profile.History.Count+7)/8;historyPage=Math.Min(historyPage,pages-1);
            for(int i=0;i<8 && historyPage*8+i<profile.History.Count;i++){
                var record=profile.History[historyPage*8+i];var row=Box(r,"Match",.05f,.245f+i*.078f,.90f,.065f,panel);
                string date=record.FinishedUtc;DateTime parsed;if(DateTime.TryParse(date,out parsed))date=parsed.ToLocalTime().ToString("dd MMM HH:mm");
                Label(row,(record.Winner==0?"WIN":"LOSS")+"  /  "+record.DeckName+"  •  "+record.Turns+" turns  •  "+date,.02f,0,.74f,1,20,gold);
                Button(row,"WATCH REPLAY",.79f,.12f,.19f,.76f,()=>OpenReplay(record));
            }
            HistoryPager(r,pages);
        }
        void HistoryPager(Transform r,int pages)
        {
            Button(r,"PREVIOUS",.05f,.89f,.16f,.04f,()=>historyPage--,historyPage>0,panel);
            Label(r,(historyPage+1)+" / "+pages,.24f,.89f,.16f,.04f,18);
            Button(r,"NEXT",.43f,.89f,.16f,.04f,()=>historyPage++,historyPage+1<pages,panel);
        }
        void ResultScreen(Transform r)
        {
            var result=lastResult;if(result==null){Navigate(Page.Home);return;}
            Label(r,result.Winner==0?"VICTORY":"DEFEAT",.1f,.19f,.8f,.15f,78,gold,TextAnchor.MiddleCenter);
            Label(r,result.DeckName+"  /  "+result.Turns+" TURNS",.1f,.39f,.8f,.07f,28,ink,TextAnchor.MiddleCenter);
            Label(r,"Every battle is a lesson in core management.",.1f,.50f,.8f,.07f,24,ink,TextAnchor.MiddleCenter);
            Button(r,"PLAY AGAIN",.18f,.69f,.28f,.09f,()=>Navigate(Page.Home));Button(r,"WATCH REPLAY",.54f,.69f,.28f,.09f,()=>OpenReplay(result),true,panel);
        }
        void ConfirmPanel(Transform root)
        {
            var r=Modal(root,"CONFIRM");Label(r,confirmText,.08f,.25f,.84f,.25f,27);Button(r,"CANCEL",.08f,.70f,.39f,.12f,()=>confirmAction=null,true,panel);Button(r,"CONFIRM",.53f,.70f,.39f,.12f,()=>{var action=confirmAction;action();confirmAction=null;});
        }
        void OpenReplay(MatchRecord record)
        {
            var a=DeckRules.Resolve(record.PlayerDeck,Definitions);var b=DeckRules.Resolve(record.AIDeck,Definitions);
            match=new Match(a,b,record.Seed,record.First);replay=record;replayIndex=0;replayMode=true;menu=false;payment=null;selectedHand=-1;selectedSpirit=0;inspect=null;showHelp=showLog=showTrash=confirmConcede=false;
        }
        void ExitReplay()
        {
            // A playback is not a live match. Drop it before Render can record a result.
            match=null;replay=null;replayMode=false;replayIndex=0;
            Navigate(Page.History);
        }
        void ReplayScreen(RectTransform root)
        {
            var field=Box(root,"Replay board",0,0,1,1,Color.clear);Board(field);field.gameObject.AddComponent<CanvasGroup>().interactable=false;
            var bar=Box(root,"Replay controls",.20f,.90f,.60f,.075f,new Color(.04f,.08f,.12f));
            Label(bar,"REPLAY  "+replayIndex+" / "+replay.Commands.Count,.025f,0,.38f,1,19,gold);
            Button(bar,"NEXT ACTION",.41f,.12f,.28f,.76f,()=>{match.Apply(replay.Commands[replayIndex]);replayIndex++;},replayIndex<replay.Commands.Count);
            Button(bar,"EXIT REPLAY",.71f,.12f,.265f,.76f,ExitReplay,true,panel);
        }
    }
}
