using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PhuocDai.Saga;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runs actual imported assets, JsonUtility and UI callbacks in Play Mode.
// Uses a temporary profile, never the player's saved decks or history.
[InitializeOnLoad]
public static class SagaSmokeTests
{
    const string Running="Saga.SmokeTests.Running", Scene="Assets/Saga/Scenes/SagaGame.unity";
    static IEnumerator routine;
    static int lastFrame=-1, assertions;
    static readonly List<string> errors=new List<string>();
    static string profilePath;
    static double deadline;
    static SagaBattleView view;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static SagaSmokeTests(){EditorApplication.playModeStateChanged+=ModeChanged;}

    [MenuItem("Saga/Run Play Mode Smoke Tests")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before running smoke tests.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        if(Application.unityVersion!="6000.6.3f1")throw new InvalidOperationException("Run with Unity 6000.6.3f1.");
        SessionState.SetBool(Running,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void ModeChanged(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Running,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){
            assertions=0;errors.Clear();lastFrame=-1;deadline=EditorApplication.timeSinceStartup+120;
            Application.logMessageReceived+=Log;routine=Exercise();EditorApplication.update+=Tick;
        }
        if(state==PlayModeStateChange.EnteredEditMode){
            SessionState.SetBool(Running,false);
            EditorSceneManager.OpenScene(Scene);
            if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt("Saga.SmokeTests.Exit",1));
        }
    }
    static void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message);}
    static void Tick()
    {
        try{
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Play Mode smoke tests timed out.");
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(!routine.MoveNext())Finish(null);
        }catch(Exception e){Finish(e);}
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(error!=null)errors.Add(error.ToString());
        string report="Unity "+Application.unityVersion+"\nAssertions: "+assertions+"\n"+(errors.Count==0?"PASS":"FAIL\n"+string.Join("\n",errors));
        Directory.CreateDirectory("TestResults");File.WriteAllText("TestResults/unity-smoke.txt",report);
        SessionState.SetInt("Saga.SmokeTests.Exit",errors.Count==0?0:1);
        if(errors.Count==0)Debug.Log(report);else Debug.LogError(report);
        if(profilePath!=null && Directory.Exists(profilePath))Directory.Delete(profilePath,true);
        EditorApplication.ExitPlaymode();
    }
    static void Check(bool condition,string name){assertions++;if(!condition)throw new Exception("FAIL: "+name);}
    static object Get(string name){return typeof(SagaBattleView).GetField(name,Flags).GetValue(view);}
    static void Set(string name,object value){typeof(SagaBattleView).GetField(name,Flags).SetValue(view,value);}
    static void Call(string name,params object[] args){typeof(SagaBattleView).GetMethod(name,Flags).Invoke(view,args);}
    static Button Button(string name){return view.GetComponentsInChildren<Button>().Single(b=>b.name==name);}
    static void Click(string name){var b=Button(name);Check(b.interactable,"enabled button "+name);b.onClick.Invoke();}
    static IEnumerator Exercise()
    {
        profilePath=Path.Combine(Path.GetTempPath(),"saga-smoke-"+Guid.NewGuid().ToString("N"));
        typeof(SagaBattleView).GetField("TestProfileDirectory",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,profilePath);
        SceneManager.LoadScene(Path.GetFileNameWithoutExtension(Scene));
        yield return null;yield return null;yield return null;
        view=UnityEngine.Object.FindAnyObjectByType<SagaBattleView>();Check(view!=null,"scene component imports and starts");
        var catalog=Resources.Load<SagaCatalog>("Saga/TrainingCatalog");Check(catalog!=null && catalog.cards.Length==20,"20 catalog entries");
        foreach(var card in catalog.cards)Check(card.art!=null && card.art.texture!=null,"sprite imported: "+card.id);
        Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Length==1,"one audio listener");
        Check(EventSystem.current!=null,"event system exists");
        Click("DECKS");yield return null;yield return null;
        var field=view.GetComponentsInChildren<InputField>().First();
        field.ActivateInputField();yield return null;
        field.text="Smoke deck";
        var duplicate=Button("DUPLICATE");
        EventSystem.current.SetSelectedGameObject(duplicate.gameObject);
        Check(duplicate.gameObject.activeInHierarchy,"ending input does not destroy next click target");
        duplicate.onClick.Invoke();yield return null;yield return null;
        var profile=(SagaProfile)Get("profile");
        Check(profile.Decks.Count==2 && profile.Decks[0].Name=="Smoke deck","rename then duplicate in same input frame");
        Check(profile.Decks[1].Name=="Copy of Smoke deck","duplicate uses committed input");
        Click("HOME");yield return null;yield return null;
        Call("StartMatch",0);Call("Render");yield return null;yield return null;
        var match=(Match)Get("match");match.Concede(0);Call("Render");yield return null;yield return null;
        Check(profile.Losses==1 && profile.History.Count==1,"record completed live match once");
        // Simulate reopening saved history in a fresh session (no result has been written).
        Set("outcomeWritten",false);
        Call("OpenReplay",profile.History[0]);Call("Render");yield return null;yield return null;
        Click("NEXT ACTION");yield return null;yield return null;
        Check(((Match)Get("match")).Phase==Step.Finished,"replay completes");
        Click("EXIT REPLAY");yield return null;yield return null;
        Check(profile.Losses==1 && profile.History.Count==1,"exiting completed replay does not record a second result");
        Check(Get("match")==null,"playback discarded on exit");
        var restored=JsonUtility.FromJson<SagaProfile>(File.ReadAllText(Path.Combine(profilePath,"saga-profile.json")));
        Check(restored.Decks.Count==2 && restored.History.Count==1,"Unity JsonUtility roundtrip");
        Call("StartMatch",1);Call("Render");yield return null;yield return null;
        Check(Get("match")!=null && !(bool)Get("replayMode"),"new match works after replay");
        Check(errors.Count==0,"no Unity errors or exceptions");
    }
}
