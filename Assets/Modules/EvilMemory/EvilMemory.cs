using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

public class EvilMemory : MonoBehaviour
{
    public static readonly string[] ignoredModules = {
        "Forget Me Not",     //Regular version.
        "Forget Everything", //Mandatory to prevent unsolvable bombs.
        "Turn The Key",      //TTK is timer based, and stalls the bomb if only it and FE are left.
        "Souvenir",          //Similar situation to TTK, stalls the bomb.
    };

    private static Color LED_OFF = new Color(0, 0, 0, 0);
    private static Color[] LED_COLS = new Color[]{new Color(0.7f, 0,    0, 1),
                                                  new Color(0.5f, 0.5f, 0, 1),
                                                  new Color(0,    0.6f, 0, 1),
                                                  new Color(0,    0.3f, 1, 1)};

    public static int loggingID = 1;
    public int thisLoggingID;

    public KMBombInfo BombInfo;
    public KMAudio Sound;

    public GameObject DialContainer;
    private GameObject[] Dials;
    public KMSelectable Submit;
    public TextMesh Text;
    public Nixie Nix1, Nix2;
    private MeshRenderer LED;

    private int[] StageOrdering;
    private int[] Solution;

    private int[][] DialDisplay;
    private int[]  NixieDisplay;
    private int[][]  LEDDisplay;

    private bool forcedSolve = false;

    void Awake()
    {
        thisLoggingID = loggingID++;
        
        transform.Find("Background").GetComponent<MeshRenderer>().material.color = new Color(0.3f, 0.3f, 0.3f);

        Dials = new GameObject[10];
        for(int a = 0; a < 10; a++) Dials[a] = DialContainer.transform.Find("Dial " + (a+1)).gameObject;

        MeshRenderer mr = transform.Find("Display").Find("Wiring").GetComponent<MeshRenderer>();
        mr.materials[0].color = new Color(0.1f, 0.1f, 0.1f);
        mr.materials[1].color = new Color(0.3f, 0.3f, 0.3f);
        mr.materials[2].color = new Color(0.1f, 0.4f, 0.8f);

        LED = transform.Find("Lights").GetComponent<MeshRenderer>();
        LED.materials[0].color = new Color(0.3f, 0.3f, 0.3f);
        LED.materials[1].color = LED_OFF;
        LED.materials[2].color = LED_OFF;
        LED.materials[3].color = LED_OFF;

        transform.Find("Display").Find("Edge").GetComponent<MeshRenderer>().material.color = new Color(0, 0, 0);
        DialContainer.transform.Find("Base").GetComponent<MeshRenderer>().material.color = new Color(0.3f, 0.3f, 0.3f);
        Submit.GetComponent<MeshRenderer>().material.color = new Color(0.8f, 0.8f, 0.2f);

        for(int a = 0; a < Dials.Length; a++) {
            int a2 = a;
            Dials[a].GetComponent<KMSelectable>().OnInteract += delegate() {Handle(a2); return false;};
        }
        Submit.OnInteract += HandleSubmit;

        GetComponent<KMBombModule>().OnActivate += ActivateModule;

        Text.text = "";
    }

    private void ActivateModule()
    {
        int count = BombInfo.GetSolvableModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
        if(count == 0) { //Prevent deadlock
            Debug.Log("[Forget Everything #"+thisLoggingID+"] No valid stage modules, auto-solving.");
            GetComponent<KMBombModule>().HandlePass();
            return;
        }
        if(count > 99) { //More than 99 stages will cause issues as the stage display only has 2 digits
            Debug.Log("[Forget Everything #"+thisLoggingID+"] More than 99 stages, capping at 99.");
            count = 99;
        }
        else Debug.Log("[Forget Everything #"+thisLoggingID+"] Stages: " + count);

        StageOrdering = new int[count];
        List<int> stages = new List<int>();
        for(int a = 0; a < count; a++) stages.Add(a);

        DialDisplay  = new int[count][];
        NixieDisplay = new int[count];
        LEDDisplay   = new int[count][];

        Solution = new int[10];

        int opCount = 0;
        int stageCounter = 1;
        for(int a = 0; a < count; a++) {
            int p = Random.Range(0, stages.Count);
            StageOrdering[a] = stages[p];
            stages.RemoveAt(p);

            DialDisplay[a] = new int[10];
            for(int b = 0; b < 10; b++) DialDisplay[a][b] = Random.Range(0, 10);
            NixieDisplay[a] = Random.Range(0, 100);
            LEDDisplay[a] = new int[3];
            for(int b = 0; b < 3; b++) LEDDisplay[a][b] = Random.Range(0, 4);

            if(a == 0) {
                //First stage, solution starts as the shown dials and the nixie tubes are ignored.
                string ans = "";
                for(int b = 0; b < 10; b++) {
                    Solution[b] = DialDisplay[0][b];
                    ans += Solution[b];
                }
                Debug.Log("[Forget Everything #"+thisLoggingID+"] Initial answer (stage 1 display): " + ans);
            }
            else {
                //Work out if the stage matters or not
                int n1 = NixieDisplay[a] / 10, n2 = NixieDisplay[a] % 10;

                bool found = false;
                if(stageCounter <= -2) found = true; //A stage is always valid if the last two were invalid
                else if(stageCounter < 2) {          //A stage is always invalid if the last two were valid
                    for(int b = 0; b < 10; b++) {    //A stage is valid if both nixie numbers are on the dials
                        if(DialDisplay[a][b] == n1) {
                            found = true;
                            break;
                        }
                    }
                    if(found && n1 != n2) {
                        found = false;
                        for(int b = 0; b < 10; b++) {
                            if(DialDisplay[a][b] == n2) {
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if(found) {
                    //This stage is important
                    opCount++;
                    if(stageCounter <= 0) stageCounter = 1;
                    else stageCounter++;

                    //Determine primary colour (the one missing, or the most frequent)
                    int primaryCol;
                    bool colFromMissing = false;
                    if(LEDDisplay[a][0] == LEDDisplay[a][1] || LEDDisplay[a][0] == LEDDisplay[a][2]) primaryCol = LEDDisplay[a][0];
                    else if (LEDDisplay[a][1] == LEDDisplay[a][2]) primaryCol = LEDDisplay[a][1];
                    else {
                        colFromMissing = true;
                             if(LEDDisplay[a][0] != 0 && LEDDisplay[a][1] != 0 && LEDDisplay[a][2] != 0) primaryCol = 0;
                        else if(LEDDisplay[a][0] != 1 && LEDDisplay[a][1] != 1 && LEDDisplay[a][2] != 1) primaryCol = 1;
                        else if(LEDDisplay[a][0] != 2 && LEDDisplay[a][1] != 2 && LEDDisplay[a][2] != 2) primaryCol = 2;
                        else                                                                             primaryCol = 3;
                    }
                    string colName;
                    if(primaryCol == 0) {
                        colName = "Red     (add odd)";
                        for(int b = 0; b < 10; b+=2) {
                            Solution[b] = (Solution[b] + DialDisplay[a][b]) % 10;
                        }
                    }
                    else if(primaryCol == 1) {
                        colName = "Yellow (add even)";
                        for(int b = 1; b < 10; b+=2) {
                            Solution[b] = (Solution[b] + DialDisplay[a][b]) % 10;
                        }
                    }
                    else if(primaryCol == 2) {
                        colName = "Green   (sub odd)";
                        for(int b = 0; b < 10; b+=2) {
                            Solution[b] = (Solution[b] - DialDisplay[a][b] + 10) % 10;
                        }
                    }
                    else {
                        colName = "Blue   (sub even)";
                        for(int b = 1; b < 10; b+=2) {
                            Solution[b] = (Solution[b] - DialDisplay[a][b] + 10) % 10;
                        }
                    }

                    string ans = "", disp = "";
                    for(int b = 0; b < 10; b++) {
                        ans += Solution[b];
                        disp += DialDisplay[a][b];
                    }
                    Debug.Log("[Forget Everything #"+thisLoggingID+"] Stage " + (a+1).ToString("D2") + " is an important stage. Display: " + disp + ", Colour: " + colName + " " + (colFromMissing ? "(missing colour)" : " (most frequent)") + ", New answer: " + ans);
                }
                else {
                    if(stageCounter >= 0) stageCounter = -1;
                    else stageCounter--;
                }
            }
        }
        Debug.Log("[Forget Everything #"+thisLoggingID+"] Total important stages: " + opCount);
        string ans2 = "";
        for(int b = 0; b < 10; b++) ans2 += Solution[b];
        Debug.Log("[Forget Everything #"+thisLoggingID+"] Final answer: " + ans2);
    }

    int ticker = 0, lastProgress = -1;
    bool done = false;
    void FixedUpdate()
    {
        if(forcedSolve || done || StageOrdering == null) return;

        ticker++;
        if(ticker == 15)
        {
            ticker = 0;
            int progress = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
            if(progress != lastProgress) {
                lastProgress = progress;
                if(progress >= StageOrdering.Length) {
                    //Ready to solve
                    Text.text = "--";
                    Nix1.SetValue(-1);
                    Nix2.SetValue(-1);
                    LED.materials[1].color = LED_OFF;
                    LED.materials[2].color = LED_OFF;
                    LED.materials[3].color = LED_OFF;
                }
                else {
                    //Showing stages
                    int stage = StageOrdering[progress];
                    Text.text = (stage+1).ToString("D2");
                    Nix1.SetValue(NixieDisplay[stage] / 10);
                    Nix2.SetValue(NixieDisplay[stage] % 10);
                    ShowNumber(DialDisplay[stage]);
                    LED.materials[1].color = LED_COLS[LEDDisplay[stage][0]];
                    LED.materials[2].color = LED_COLS[LEDDisplay[stage][1]];
                    LED.materials[3].color = LED_COLS[LEDDisplay[stage][2]];
                }
            }
        }
    }

    private void Handle(int val) {
        Dials[val].GetComponent<KMSelectable>().AddInteractionPunch(0.1f);
        if(done) return;

        int progress = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
        if(progress < StageOrdering.Length) {
            Debug.Log("[Forget Everything #"+thisLoggingID+"] Tried to turn a dial too early.");
            GetComponent<KMBombModule>().HandleStrike();
            return;
        }

        Dials[val].GetComponent<Dial>().Increment();
        Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
    }

    private bool HandleSubmit() {
        Submit.GetComponent<KMSelectable>().AddInteractionPunch(0.5f);
        if(done) return false;

        int progress = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
        if(progress < StageOrdering.Length) {
            Debug.Log("[Forget Everything #"+thisLoggingID+"] Tried to submit an answer too early.");
            GetComponent<KMBombModule>().HandleStrike();
            return false;
        }

        bool correct = true;
        string ans = "";
        for(int a = 0; a < 10; a++) {
            int dial = Dials[a].GetComponent<Dial>().GetValue();
            if(dial == -1) {
                Debug.Log("[Forget Everything #"+thisLoggingID+"] Tried to submit whilst dials are spinning.");
                GetComponent<KMBombModule>().HandleStrike();
                Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
                return false;
            }

            ans += dial;
            if(dial != Solution[a]) correct = false;
        }

        if(correct) {
            Debug.Log("[Forget Everything #"+thisLoggingID+"] Module solved.");
            GetComponent<KMBombModule>().HandlePass();
            Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
            Submit.transform.localRotation = Quaternion.Euler(0, 115, 0);
            done = true;
        }
        else {
            Debug.Log("[Forget Everything #"+thisLoggingID+"] Incorrect answer: " + ans);
            GetComponent<KMBombModule>().HandleStrike();
        }

        return false;
    }

    private void ShowNumber(int[] nums) {
        for(int a = 0; a < 10; a++) {
            Dials[a].GetComponent<Dial>().Move(nums[a]);
        }
    }
}