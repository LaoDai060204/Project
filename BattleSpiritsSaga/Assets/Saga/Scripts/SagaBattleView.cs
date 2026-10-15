using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace PhuocDai.Saga
{
    public partial class SagaBattleView : MonoBehaviour
    {
        SagaCatalog catalog; Match match; Font font; RectTransform screen;
        int selectedHand=-1, selectedSpirit, selectedOwner, payHand=-1;
        Payment payment; bool showTrash; int trashOwner, trashPage; bool menu=true, tossPending, tossHuman, showHelp, showLog, confirmConcede;
        float nextAI; string notice=""; CatalogEntry inspect;
        readonly Color bg=new Color(.025f,.043f,.072f), panel=new Color(.055f,.085f,.125f), gold=new Color(.86f,.69f,.36f), teal=new Color(.12f,.57f,.58f), ink=new Color(.85f,.9f,.94f);
        void Awake()
        {
            var camera=new GameObject("Arena Camera",typeof(Camera)).GetComponent<Camera>();camera.backgroundColor=bg;camera.clearFlags=CameraClearFlags.SolidColor;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            catalog=Resources.Load<SagaCatalog>("Saga/TrainingCatalog");
            var canvas=new GameObject("Saga UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            screen=canvas.GetComponent<RectTransform>();
            if(EventSystem.current==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            Application.targetFrameRate=60;if(catalog!=null)InitializeProfile();Render();
        }
        void Update()
        {
            if(menu || replayMode || match==null || match.Phase==Step.Finished || match.DecisionPlayer!=1 || Time.unscaledTime<nextAI)return;
            try {SagaAI.Act(match);notice="";} catch(Exception e) {notice="AI stopped: "+e.Message;Debug.LogException(e);menu=true;}
            nextAI=Time.unscaledTime+(profile==null?.65f:profile.AIDelay);Render();
        }
        RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();
            r.anchorMin=new Vector2(x,1-y-h);r.anchorMax=new Vector2(x+w,1-y);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
            go.GetComponent<Image>().color=color;return r;
        }
        Text Label(Transform p,string text,float x,float y,float w,float h,int size=20,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(p,false);var r=go.GetComponent<RectTransform>();r.anchorMin=new Vector2(x,1-y-h);r.anchorMax=new Vector2(x+w,1-y);r.offsetMin=r.offsetMax=Vector2.zero;var t=go.GetComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=color??ink;t.alignment=align;t.raycastTarget=false;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        void Button(Transform p,string label,float x,float y,float w,float h,Action action,bool enabled=true,Color? color=null)
        {
            var r=Box(p,label,x,y,w,h,color??teal);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.interactable=enabled;
            var hover=r.gameObject.AddComponent<ButtonHover>();hover.hoverIn=()=>{if(b.interactable)r.localScale=Vector3.one*1.025f;};hover.hoverOut=()=>r.localScale=Vector3.one;
            b.onClick.AddListener(()=>{try{notice="";ClickSound();action();}catch(Exception e){notice=e.Message;}nextAI=Time.unscaledTime+(profile==null?.65f:profile.AIDelay);Render();});
            var c=b.colors;c.highlightedColor=new Color(1.15f,1.15f,1.15f);c.disabledColor=new Color(.35f,.35f,.35f,.65f);b.colors=c;
            Label(r,label,.035f,.04f,.93f,.92f,18,Color.white,TextAnchor.MiddleCenter);
        }
        CatalogEntry Entry(Definition d) {return catalog.cards.First(c=>c.id==d.Id);}
        void Art(Transform p,Sprite art,float x,float y,float w,float h)
        {
            var r=Box(p,"Card artwork",x,y,w,h,Color.white);var im=r.GetComponent<Image>();im.sprite=art;im.preserveAspect=true;im.raycastTarget=false;
        }
        void Render()
        {
            for(int i=screen.childCount-1;i>=0;i--){var o=screen.GetChild(i).gameObject;o.SetActive(false);Destroy(o);}
            var root=Box(screen,"Background",0,0,1,1,bg);
            if(catalog==null){Label(root,"Missing TrainingCatalog. Reimport the project.",.1f,.3f,.8f,.2f,28);return;}
            CompleteMatch();
            if(menu){RenderFrontEnd(root);return;}
            if(replayMode){ReplayScreen(root);return;}
            Board(root);
            if(payment!=null)PaymentPanel(root);
            if(showHelp)Help(root);
            if(showLog)LogPanel(root);
            if(showTrash)TrashPanel(root);
            if(confirmConcede){var r=Modal(root,"CONCEDE MATCH");Label(r,"End this match and award the win to AI?",.08f,.25f,.84f,.2f,24);Button(r,"Keep playing",.08f,.65f,.39f,.13f,()=>confirmConcede=false);Button(r,"Concede",.53f,.65f,.39f,.13f,()=>{match.Concede(0);confirmConcede=false;});}
        }
        RectTransform Modal(Transform root,string title)
        {
            var shade=Box(root,"Overlay",0,0,1,1,new Color(0,0,0,.88f));var r=Box(shade,title,.18f,.12f,.64f,.76f,panel);Label(r,title,.05f,.03f,.9f,.09f,28,gold);return r;
        }
        void StartMatch(int first)
        {
            var deck=DeckRules.Resolve(ActiveDeck.Cards,Definitions);
            var aiIds=catalog.cards.Take(13).SelectMany(c=>Enumerable.Repeat(c.id,4)).ToArray();
            var ai=DeckRules.Resolve(aiIds,Definitions);
            startingPlayerDeck=ActiveDeck.Cards.ToArray();startingAIDeck=aiIds;
            match=new Match(deck,ai,UnityEngine.Random.Range(1,int.MaxValue),first);menu=false;replayMode=false;outcomeWritten=false;tossPending=false;selectedHand=-1;selectedSpirit=0;inspect=null;payment=null;
        }
        string Stats(int p)
        {
            var s=match.Players[p];return "LIFE  "+s.Life+"     RESERVE  "+s.Reserve+(s.ReserveSoul?" + SOUL":"")+"     TRASH CORES  "+s.TrashCores+(s.TrashSoul?" + SOUL":"")+"     DECK  "+s.Deck.Count+"     HAND  "+s.Hand.Count;
        }
        void Board(Transform r)
        {
            Label(r,"SAGA  /  TRAINING ARENA",.018f,.012f,.45f,.044f,26,gold);
            Button(r,"Trash",.54f,.016f,.10f,.036f,()=>{showTrash=true;trashPage=0;},true,panel);
            Button(r,"Help",.65f,.016f,.09f,.036f,()=>showHelp=true,true,panel);Button(r,"Battle log",.75f,.016f,.11f,.036f,()=>showLog=true,true,panel);Button(r,"Concede",.87f,.016f,.11f,.036f,()=>confirmConcede=true,match.Phase!=Step.Finished,panel);
            var arena=Box(r,"Arena",.018f,.074f,.73f,.905f,panel);
            Label(arena,"OPPONENT  /  AI",.018f,.005f,.9f,.039f,18,gold);Label(arena,Stats(1),.018f,.05f,.965f,.04f,18);
            CardRow(arena,match.Players[1].Field,1,.104f,.244f);
            var step=Box(arena,"Turn",.012f,.36f,.975f,.073f,new Color(.085f,.15f,.2f));
            Label(step,"TURN "+match.Turn+"  •  "+(match.Active==0?"YOUR TURN":"AI TURN")+"  /  "+match.Phase,.018f,0,.95f,.54f,23,gold);
            Label(step,Prompt(),.018f,.52f,.95f,.47f,17);
            CardRow(arena,match.Players[0].Field,0,.446f,.244f);
            Label(arena,Stats(0),.018f,.708f,.965f,.04f,18);
            HandRow(arena,.764f,.218f);
            var side=Box(r,"Inspector",.76f,.074f,.222f,.905f,panel);Inspector(side);
        }
        string Prompt()
        {
            if(notice!="")return notice;
            if(match.Phase==Step.Finished)return match.Winner==0?"Victory! Your strategy prevailed.":"Defeat. Try a different core strategy.";
            if(match.DecisionPlayer==1)return "AI is thinking…";
            switch(match.Phase){case Step.Mulligan:return "Keep four + draw one, or replace all four once.";case Step.Main:return "Select a hand card to summon, or a spirit to manage cores.";case Step.Attack:return "Select a ready spirit to attack, or end your turn.";case Step.Block:return "Select a ready spirit to block, or take life damage.";default:return "Your Flash priority. This training deck has no Flash effects; pass.";}
        }
        RectTransform ScrollRow(Transform p,string title,float y,float h,int count,out RectTransform content)
        {
            var viewport=Box(p,title,.012f,y,.975f,h,new Color(.025f,.05f,.075f));viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            var go=new GameObject("Cards",typeof(RectTransform));go.transform.SetParent(viewport,false);content=go.GetComponent<RectTransform>();content.anchorMin=new Vector2(0,0);content.anchorMax=new Vector2(0,1);content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(1,count)*144,0);content.anchoredPosition=Vector2.zero;scroll.content=content;return viewport;
        }
        void CardRow(Transform p,List<Spirit> spirits,int owner,float y,float h)
        {
            RectTransform content;var row=ScrollRow(p,"Field",y,h,spirits.Count,out content);
            if(spirits.Count==0){Label(row,owner==0?"YOUR FIELD  /  Summon spirits from your hand":"AI FIELD  /  No spirits yet",.025f,.35f,.95f,.3f,23,new Color(.35f,.46f,.55f));return;}
            for(int i=0;i<spirits.Count;i++) {
                var s=spirits[i];int id=s.Id;var e=Entry(s.Card);float x=(float)i/spirits.Count,w=1f/spirits.Count;
                var cell=Box(content,s.Card.Name,x+.004f,y:0,w:w-.008f,h:1,color:selectedSpirit==id?teal:new Color(.08f,.12f,.16f));
                var b=cell.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>{selectedSpirit=id;selectedOwner=owner;selectedHand=-1;inspect=e;Render();});
                Art(cell,e.art,.075f,.02f,.85f,.69f);
                Label(cell,"LV"+s.Level+"  "+s.BP+" BP",.03f,.71f,.94f,.13f,17,gold,TextAnchor.MiddleCenter);
                Label(cell,s.Regular+" core"+(s.Soul?" + S":"")+" | "+(s.Exhausted?"EXHAUSTED":"READY"),.025f,.85f,.95f,.12f,13,s.Exhausted?new Color(.95f,.52f,.4f):ink,TextAnchor.MiddleCenter);
                if(s.Exhausted)Box(cell,"Exhausted stripe",0,0,1,.02f,new Color(.85f,.3f,.22f));
            }
        }
        void HandRow(Transform p,float y,float h)
        {
            var hand=match.Players[0].Hand;RectTransform content;ScrollRow(p,"Hand",y,h,hand.Count,out content);
            for(int i=0;i<hand.Count;i++) {
                int index=i;var c=hand[i];var e=Entry(c);float w=1f/hand.Count;
                var cell=Box(content,c.Name,i*w+.004f,0,w-.008f,1,selectedHand==i?teal:new Color(.08f,.12f,.16f));
                var b=cell.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>{selectedHand=index;selectedSpirit=0;inspect=e;Render();});Art(cell,e.art,.09f,.025f,.82f,.76f);
                Label(cell,"COST "+match.Cost(0,c)+" + LV1 core",.03f,.81f,.94f,.16f,14,gold,TextAnchor.MiddleCenter);
            }
        }
        void Inspector(RectTransform r)
        {
            if(inspect!=null){Art(r,inspect.art,.08f,.012f,.84f,.365f);Label(r,inspect.title,.065f,.384f,.87f,.065f,23,gold);Label(r,inspect.id+"  •  "+inspect.color+"\n"+string.Join(" / ",inspect.cores.Select((n,i)=>"LV"+(i+1)+": "+n+"c "+inspect.bp[i]+"BP")),.065f,.457f,.87f,.085f,16);}
            else {Label(r,"CORE COMMAND",.07f,.055f,.86f,.07f,26,gold);Label(r,"Click any visible card to inspect.\n\nHand: choose a summon.\nField: manage cores, attack or block.\n\nDrag a card row horizontally to see more cards.",.07f,.15f,.86f,.33f,21);}
            bool human=match.DecisionPlayer==0;
            if(match.Phase==Step.Mulligan){Button(r,"Keep + draw 1",.06f,.57f,.88f,.067f,()=>match.Mulligan(0,false),human);Button(r,"Mulligan 4",.06f,.65f,.88f,.067f,()=>match.Mulligan(0,true),human);}
            else if(match.Phase==Step.Main && match.Active==0) {
                if(selectedHand>=0 && selectedHand<match.Players[0].Hand.Count)Button(r,"Choose summon payment",.06f,.56f,.88f,.067f,()=>{payHand=selectedHand;payment=match.Suggest(0,match.Players[0].Hand[payHand])??new Payment();});
                var s=match.Players[0].Field.FirstOrDefault(x=>x.Id==selectedSpirit);
                if(s!=null && selectedOwner==0) {
                    Label(r,"Move one core: RESERVE ↔ CARD",.06f,.55f,.88f,.04f,16,gold);
                    Button(r,"+ Core",.06f,.60f,.42f,.058f,()=>match.MoveCore(0,0,s.Id,false),match.Players[0].Reserve>0);Button(r,"− Core",.52f,.60f,.42f,.058f,()=>match.MoveCore(0,s.Id,0,false),s.Regular>0);
                    Button(r,"+ Soul",.06f,.67f,.42f,.058f,()=>match.MoveCore(0,0,s.Id,true),match.Players[0].ReserveSoul);Button(r,"− Soul",.52f,.67f,.42f,.058f,()=>match.MoveCore(0,s.Id,0,true),s.Soul);
                    Label(r,"Removing the last core destroys this spirit.",.06f,.735f,.88f,.06f,15,new Color(.95f,.63f,.46f));
                }
                Button(r,match.Turn==1?"End first turn":"Go to Attack Step",.06f,.82f,.88f,.067f,()=>{match.EndMain(0);selectedHand=-1;});
            }
            else if(match.Phase==Step.Attack && human) {
                var s=match.Players[0].Field.FirstOrDefault(x=>x.Id==selectedSpirit);
                Button(r,"ATTACK",.06f,.60f,.88f,.075f,()=>match.Attack(0,selectedSpirit),s!=null && !s.Exhausted);
                Button(r,"End turn",.06f,.70f,.88f,.075f,()=>match.EndAttack(0));
            }
            else if(match.Phase==Step.Block && human) {
                var s=match.Players[0].Field.FirstOrDefault(x=>x.Id==selectedSpirit);
                Button(r,"Block with selected spirit",.06f,.60f,.88f,.075f,()=>match.Block(0,selectedSpirit),s!=null && !s.Exhausted);
                Button(r,"Take life damage",.06f,.70f,.88f,.075f,()=>match.Block(0,0));
            }
            else if((match.Phase==Step.FlashBefore || match.Phase==Step.FlashAfter) && human)Button(r,"Pass Flash",.06f,.63f,.88f,.08f,()=>match.PassFlash(0));
            else if(match.Phase==Step.Finished){Label(r,match.Winner==0?"VICTORY":"DEFEAT",.06f,.55f,.88f,.09f,34,gold,TextAnchor.MiddleCenter);Button(r,"MATCH RESULT",.06f,.69f,.88f,.075f,()=>{Navigate(Page.Result);confirmConcede=false;});}
            Label(r,notice!=""?notice:match.Log.LastOrDefault()??"",.06f,.91f,.88f,.075f,15,notice!=""?new Color(1,.5f,.4f):ink);
        }
        void PaymentPanel(Transform root)
        {
            var r=Modal(root,"SUMMON  /  CHOOSE YOUR CORES");var c=match.Players[0].Hand[payHand];int cost=match.Cost(0,c);
            Label(r,c.Name+"   •   Pay "+cost+" to trash + allocate at least "+c.CoreLevels[0]+" core",.05f,.14f,.9f,.08f,23);
            Label(r,"Payment: "+payment.Cost.Count+" / "+cost+"    |    On new spirit: "+payment.Maintain.Count+"\nFirst clicks pay cost; further clicks allocate cores. Soul counts as one core.",.05f,.235f,.9f,.095f,19,gold);
            var picks=match.Available(0);foreach(var pick in payment.Cost.Concat(payment.Maintain)){int i=picks.FindIndex(x=>x.Source==pick.Source && x.Soul==pick.Soul);if(i>=0)picks.RemoveAt(i);}
            var sources=picks.Select(x=>x.Source).Distinct().ToList();
            // Scroll source rows vertically so large fields do not hide payment sources.
            var area=Box(r,"Payment sources",.05f,.35f,.9f,.36f,bg);area.gameObject.AddComponent<RectMask2D>();var sr=area.gameObject.AddComponent<ScrollRect>();sr.viewport=area;sr.horizontal=false;sr.scrollSensitivity=35;
            var go=new GameObject("Sources",typeof(RectTransform));go.transform.SetParent(area,false);var content=go.GetComponent<RectTransform>();content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,Mathf.Max(1,sources.Count)*48);content.anchoredPosition=Vector2.zero;sr.content=content;
            for(int i=0;i<sources.Count;i++) {
                int source=sources[i];var spirit=match.Players[0].Field.FirstOrDefault(s=>s.Id==source);float row=1f/Mathf.Max(1,sources.Count);var own=picks.Where(x=>x.Source==source).ToList();
                Label(content,source==0?"Reserve":spirit.Card.Name,.02f,i*row,.54f,row,18);
                Action<bool> add=soul=>{var k=new CorePick(source,soul);if(payment.Cost.Count<cost)payment.Cost.Add(k);else payment.Maintain.Add(k);};
                Button(content,"Core ("+own.Count(x=>!x.Soul)+")",.57f,i*row+.005f,.23f,row-.01f,()=>add(false),own.Any(x=>!x.Soul));Button(content,"Soul",.82f,i*row+.005f,.16f,row-.01f,()=>add(true),own.Any(x=>x.Soul));
            }
            var sacrificed=match.Players[0].Field.Where(s=>s.Cores-payment.Cost.Concat(payment.Maintain).Count(k=>k.Source==s.Id)<s.Card.CoreLevels[0]).Select(s=>s.Card.Name).ToArray();
            Label(r,sacrificed.Length>0?"Will self-destroy: "+string.Join(", ",sacrificed):"No existing spirit will be sacrificed.",.05f,.72f,.9f,.075f,17,sacrificed.Length>0?new Color(1,.6f,.4f):ink);
            Button(r,"Reset",.05f,.83f,.25f,.09f,()=>payment=new Payment());Button(r,"Cancel",.34f,.83f,.25f,.09f,()=>payment=null);
            Button(r,"SUMMON",.63f,.83f,.32f,.09f,()=>{match.Summon(0,payHand,payment);payment=null;selectedHand=-1;},payment.Cost.Count==cost && payment.Maintain.Count>=c.CoreLevels[0]);
        }
        void Help(Transform root)
        {
            var r=Modal(root,"HOW TO PLAY");Label(r,"1  Keep your hand (+1 card), or mulligan once.\n2  Main: summon using cost cores + LV1 cores. Paid cores go to trash.\n3  Add/remove field cores to change levels. The last core removal destroys a spirit.\n4  Attack: select a ready spirit. It exhausts when attacking.\n5  Pass Flash, then defender chooses a ready blocker or takes damage.\n6  If blocked, pass Flash again. Lower BP is destroyed; equal BP destroys both.\n7  Damage moves life cores into reserve. Zero life loses.\n\nEach turn: Start → Core → Draw → Refresh → Main → Attack → End.\nFirst player skips Core and Attack on the first turn.\nThis build supports 20 verified no-effect spirits and your saved legal decks.\nNo Magic, Nexus, Burst, keywords or online matches yet.",.06f,.15f,.88f,.64f,22);
            Button(r,"Back to match",.3f,.85f,.4f,.08f,()=>showHelp=false);
        }
        void TrashPanel(Transform root)
        {
            var r=Modal(root,"PUBLIC TRASH");
            Button(r,trashOwner==0?"YOUR TRASH":"AI TRASH",.05f,.14f,.4f,.08f,()=>{trashOwner=1-trashOwner;trashPage=0;});
            var cards=match.Players[trashOwner].Trash;int pages=Math.Max(1,(cards.Count+5)/6);trashPage=Math.Min(trashPage,pages-1);
            for(int i=0;i<6 && trashPage*6+i<cards.Count;i++){
                var entry=Entry(cards[trashPage*6+i]);float x=.04f+i*.155f;
                Art(r,entry.art,x,.29f,.145f,.35f);Label(r,entry.title,x,.66f,.145f,.10f,15,gold,TextAnchor.MiddleCenter);
            }
            if(cards.Count==0)Label(r,"No cards in this trash.",.1f,.38f,.8f,.15f,25);
            Button(r,"PREVIOUS",.05f,.85f,.25f,.08f,()=>trashPage--,trashPage>0);
            Button(r,"CLOSE",.375f,.85f,.25f,.08f,()=>showTrash=false);
            Button(r,"NEXT",.70f,.85f,.25f,.08f,()=>trashPage++,trashPage+1<pages);
        }
        void LogPanel(Transform root)
        {
            var r=Modal(root,"BATTLE LOG");Label(r,string.Join("\n",match.Log.Skip(Math.Max(0,match.Log.Count-16))),.05f,.15f,.9f,.65f,19);Button(r,"Close",.3f,.86f,.4f,.08f,()=>showLog=false);
        }
    }
}
