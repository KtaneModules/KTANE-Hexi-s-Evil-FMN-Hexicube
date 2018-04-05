using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

public class EvilMemory : MonoBehaviour
{
    //How intense should stages be shuffled? 1 is no shuffle, 99 is full shuffle.
    private const int STAGE_RANDOM_FACTOR = 10;

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
            ShowNumber(new int[]{0,1,2,3,4,5,6,7,8,9});
            done = true;
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
            int p = Random.Range(0, Mathf.Min(stages.Count, STAGE_RANDOM_FACTOR));
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
                    int dial = a % 10;
                    if(primaryCol == 0) {
                        colName = "Red    (a+b)";
                        Solution[dial] = (Solution[dial] + DialDisplay[a][dial]) % 10;
                    }
                    else if(primaryCol == 1) {
                        colName = "Yellow (a-b)";
                        Solution[dial] = (Solution[dial] - DialDisplay[a][dial] + 10) % 10;
                    }
                    else if(primaryCol == 2) {
                        colName = "Green(a+b+5)";
                        Solution[dial] = (Solution[dial] + DialDisplay[a][dial] + 5) % 10;
                    }
                    else {
                        colName = "Blue   (b-a)";
                        Solution[dial] = (DialDisplay[a][dial] - Solution[dial] + 10) % 10;
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

    int ticker = 0, displayOverride = -1;
    bool done = false;
    void FixedUpdate()
    {
        if(done || StageOrdering == null) return;

        ticker++;
        if(ticker == 15)
        {
            ticker = 0;
            int progress = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
            if(progress >= StageOrdering.Length && displayOverride == -1) {
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
                int stage;
                if(displayOverride != -1) stage = displayOverride;
                else stage = StageOrdering[progress];

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

    private void Handle(int val) {
        Dials[val].GetComponent<KMSelectable>().AddInteractionPunch(0.1f);
        if(done || StageOrdering == null) return;

        int progress = BombInfo.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
        if(progress < StageOrdering.Length) {
            Debug.Log("[Forget Everything #"+thisLoggingID+"] Tried to turn a dial too early.");
            GetComponent<KMBombModule>().HandleStrike();
            return;
        }

        displayOverride = -1;
        Dials[val].GetComponent<Dial>().Increment();
        Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
    }

    private bool HandleSubmit() {
        Submit.GetComponent<KMSelectable>().AddInteractionPunch(0.5f);
        if(done || StageOrdering == null) return false;

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
            int dispOver = -1;
            bool wantsOver = true;
            for(int a = 0; a < 8; a++) {
                if(Dials[a].GetComponent<Dial>().GetValue() != 0) {
                    wantsOver = false;
                    break;
                }
            }
            if(wantsOver) {
                dispOver = Dials[8].GetComponent<Dial>().GetValue() * 10 + Dials[9].GetComponent<Dial>().GetValue();
                if(dispOver == 0 || dispOver > StageOrdering.Length) Debug.Log("[Forget Everything #"+thisLoggingID+"] Incorrect answer: " + ans);
                else {
                    Debug.Log("[Forget Everything #"+thisLoggingID+"] Stage display override in exchange for strike: Stage " + dispOver.ToString("D2"));
                    displayOverride = dispOver-1;
                }
            }
            else Debug.Log("[Forget Everything #"+thisLoggingID+"] Incorrect answer: " + ans);
            GetComponent<KMBombModule>().HandleStrike();
        }

        return false;
    }

    private void ShowNumber(int[] nums) {
        for(int a = 0; a < 10; a++) {
            Dials[a].GetComponent<Dial>().Move(nums[a]);
        }
    }

    //Twitch Plays support

    #pragma warning disable 0414
    string TwitchHelpMessage = "Submit answers with 'submit 1234567890'. Re-show stage info with 'submit 12' or 'submit 0000000012'.";
    #pragma warning restore 0414

    public void TwitchHandleForcedSolve() {
        Debug.Log("[Forget Everything #"+thisLoggingID+"] Module forcibly solved.");
        ShowNumber(Solution);
        Submit.GetComponent<KMSelectable>().AddInteractionPunch(0.5f);
        Sound.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        GetComponent<KMBombModule>().HandlePass();
        done = true;
    }

    public IEnumerator ProcessTwitchCommand(string cmd) {
        cmd = cmd.ToLowerInvariant();
        if(cmd.StartsWith("press ") || cmd.StartsWith("submit ")) {
            cmd = cmd.Substring(cmd.IndexOf(' ')+1);
            if(cmd.Length == 10) {
                int[] values = new int[10];
                for(int a = 0; a < 10; a++) {
                    int v = cmd[a] - '0';
                    if(v < 0 || v > 9) {
                        yield return "sendtochaterror Unknown character: " + cmd[a];
                        yield break;
                    }
                    values[a] = v;
                }

                yield return "Forget Everything";
                ShowNumber(values);
                for(int a = 0; a < 10; a++) {
                    while(Dials[a].GetComponent<Dial>().GetValue() == -1) yield return new WaitForSeconds(0.1f);
                }
                HandleSubmit();
                yield break;
            }
            if(cmd.Length == 2) {
                int stage = 0;
                bool valid = int.TryParse(cmd, out stage);
                if(!valid || stage < 1 || stage > StageOrdering.Length) {
                    yield return "sendtochaterror Bad stage: " + cmd + ", stage number must be between 1 and " + StageOrdering.Length + " inclusive.";
                    yield break;
                }

                yield return "Forget Everything";
                ShowNumber(new int[]{0,0,0,0,0,0,0,0,stage/10,stage%10});
                for(int a = 0; a < 10; a++) {
                    while(Dials[a].GetComponent<Dial>().GetValue() == -1) yield return new WaitForSeconds(0.1f);
                }
                HandleSubmit();
                yield break;
            }
            yield return "sendtochaterror Answers need either 2 or 10 digits.";
            yield break;
        }
        yield return "sendtochaterror Commands must start with 'submit'.";
        yield break;
    }
}