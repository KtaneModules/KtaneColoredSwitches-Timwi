using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ColoredSwitches;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Colored Switches
/// Created by Timwi
/// </summary>
public class ColoredSwitchesModule : MonoBehaviour
{
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable[] Switches;
    public MeshRenderer[] LedsUp;
    public MeshRenderer[] LedsDown;
    public Material LedOn, LedOff;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private SwitchColor[] _switchColors = new SwitchColor[5];
    private int _switchState;
    private int _solutionState = -1;
    private int _numInitialToggles;
    private Coroutine[] _coroutines = new Coroutine[5];
    private bool _isSolved;

    private static T[] newArray<T>(params T[] array) { return array; }
    private static Color[] _materialColors = "f65353|0fe325|5155f4|da6cf2|ffad0b|53d3ff".Split('|').Select(str => new Color(Convert.ToInt32(str.Substring(0, 2), 16) / 255f, Convert.ToInt32(str.Substring(2, 2), 16) / 255f, Convert.ToInt32(str.Substring(4, 2), 16) / 255f)).ToArray();
    private const int _switchAngle = 55;

    class Transition { public SwitchColor Color; public int TransitionTo; }

    private static Transition[][] _allowedTransitions = @"3>16|1>16|2>16|5>16|4>16|0>16
5>5|2>5|3>5|4>5|0>5|1>5
4>6|5>10|3>6|5>6|4>10|1>6|1>10|2>10|2>6|0>10|3>10|0>6
4>7|5>7|3>7|0>7|2>7|1>2|2>2|1>7
4>5|1>5|2>20|1>20|2>5|0>5|5>5|3>5|4>20
3>21|0>4|0>21|2>4|1>4|5>21|5>4|4>4|3>4|3>7
3>4|0>4|2>4|5>4|4>4|1>4
5>15|1>15|2>15|3>15|0>15|4>15|3>6
5>0|3>0|0>0|2>0|4>0|1>0
3>13|5>13|1>1|0>13|1>13|4>1|0>1|4>13|3>1|2>13|2>1|5>1|1>11
4>2|0>2|2>2|5>2|3>2|1>2
1>3|5>3|4>3|2>3|3>3|0>3
5>14|4>14|2>14|0>14|3>14|1>14
5>12|2>12|1>12|3>12|0>12|4>12
0>30|4>30|2>30|5>30|1>30|3>30
3>31|4>31|2>31|5>31|1>31|0>31
4>17|2>17|1>17|5>17|3>17|0>17
1>25|3>25|5>25|0>25|2>25|4>25
2>19|0>19|3>19|4>19|5>19|5>2|2>2|1>19|4>2
1>17|5>17|3>17|2>17|0>17|4>17
0>22|2>22|5>22|3>22|0>16|5>16|1>22|4>22|3>16
3>20|2>20|1>20|0>20|4>20|4>17|2>17|5>20
1>23|5>23|3>23|2>23|0>23|4>23|1>18
3>21|1>21|2>21|5>21|0>21|4>21
3>8|2>26|0>26|4>8|4>26|5>8|5>26|3>26|1>26|1>8|2>8|0>8
3>9|2>9|4>9|1>24|3>24|0>9|5>9|1>9|4>27|2>24
2>18|0>18|4>18|1>18|4>24|1>24|2>24|0>10|3>18|5>24|1>10|3>24|5>18|0>24|3>10
5>11|1>11|4>26|0>26|0>11|3>11|2>11|4>11|5>26
3>29|1>30|5>20|0>29|5>29|3>30|2>29|4>29|2>20|1>29|4>30
3>28|2>28|0>28|4>21|2>31|5>31|1>28|3>21|4>28|5>28|0>31
5>28|0>28|0>22|4>28|2>28|1>22|1>28|3>28
5>27|3>27|0>27|2>27|1>27|4>27".Replace("\r", "").Split('\n').Select(row => row.Split('|').Select(elem => elem.Split('>').Select(i => int.Parse(i)).ToArray()).Select(arr => new Transition { Color = (SwitchColor) arr[0], TransitionTo = arr[1] }).ToArray()).ToArray();

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _switchState = Rnd.Range(0, 32);

        Debug.LogFormat("[Colored Switches #{0}] Initial state of the switches: {1}", _moduleId, string.Join("", Enumerable.Range(0, 5).Reverse().Select(i => (_switchState & (1 << i)) == 0 ? "▼" : "▲").ToArray()));

        for (int i = 0; i < 5; i++)
        {
            _switchColors[i] = (SwitchColor) Rnd.Range(0, 6);
            Switches[i].gameObject.GetComponent<MeshRenderer>().material.color = _materialColors[(int) _switchColors[i]];
            Switches[i].OnInteract = getToggler(i);
            Switches[i].transform.localEulerAngles = new Vector3((_switchState & (1 << i)) != 0 ? _switchAngle : -_switchAngle, 0, 0);
        }

        Debug.LogFormat("[Colored Switches #{0}] Colors of the switches: {1}", _moduleId, string.Join(", ", Enumerable.Range(0, 5).Reverse().Select(i => _switchColors[i].ToString()).ToArray()));

        for (int i = 0; i < 5; i++)
        {
            LedsUp[i].material = LedOff;
            LedsDown[i].material = LedOff;
        }
    }

    private KMSelectable.OnInteractHandler getToggler(int i)
    {
        return delegate
        {
            if (_coroutines[i] == null)
            {
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Switches[i].transform);
                Switches[i].AddInteractionPunch(.25f);

                if (_isSolved)
                    return false;

                if (!transitionAllowed(i))
                {
                    Debug.LogFormat("[Colored Switches #{0}] Toggling switch #{1} is invalid here.", _moduleId, 5 - i);
                    Module.HandleStrike();
                }
                else
                {
                    _switchState ^= 1 << i;
                    _coroutines[i] = StartCoroutine(toggleSwitch(i));

                    Debug.LogFormat("[Colored Switches #{0}] Valid transition made. Switches now: {1}", _moduleId, string.Join("", Enumerable.Range(0, 5).Reverse().Select(j => (_switchState & (1 << j)) == 0 ? "▼" : "▲").ToArray()));

                    if (_solutionState == -1)
                    {
                        _numInitialToggles++;
                        if (_numInitialToggles == 3)
                        {
                            // Find a suitable solution state
                            var q = new Queue<int>();
                            var dist = new Dictionary<int, int>();
                            q.Enqueue(_switchState);
                            dist[_switchState] = 0;
                            while (q.Count > 0)
                            {
                                var elem = q.Dequeue();
                                for (int j = 0; j < 5; j++)
                                {
                                    var to = (elem ^ (1 << j));
                                    if (!dist.ContainsKey(to) && _allowedTransitions[elem].Any(tr => tr.Color == _switchColors[j] && tr.TransitionTo == to))
                                    {
                                        dist[to] = dist[elem] + 1;
                                        q.Enqueue(to);
                                    }
                                }
                            }

                            var eligibleSolutions = dist.Where(p => p.Value >= 7 && p.Value <= 9).Select(p => p.Key).ToArray();
                            _solutionState = eligibleSolutions[Rnd.Range(0, eligibleSolutions.Length)];
                            for (int j = 0; j < 5; j++)
                            {
                                LedsUp[j].material = (_solutionState & (1 << j)) != 0 ? LedOn : LedOff;
                                LedsDown[j].material = (_solutionState & (1 << j)) != 0 ? LedOff : LedOn;
                            }

                            Debug.LogFormat("[Colored Switches #{0}] Three valid transitions made. LEDs show: {1}", _moduleId, string.Join("", Enumerable.Range(0, 5).Reverse().Select(j => (_solutionState & (1 << j)) == 0 ? "▼" : "▲").ToArray()));
                        }
                    }
                    else if (_switchState == _solutionState)
                    {
                        Debug.LogFormat("[Colored Switches #{0}] Module solved.", _moduleId);
                        Module.HandlePass();
                        _isSolved = true;
                    }
                }
            }
            return false;
        };
    }

    private bool transitionAllowed(int i)
    {
        return _allowedTransitions[_switchState].Any(tr => tr.Color == _switchColors[i] && tr.TransitionTo == (_switchState ^ (1 << i)));
    }

    private float easeOutSine(float time, float duration, float from, float to)
    {
        return (to - from) * Mathf.Sin(time / duration * (Mathf.PI / 2)) + from;
    }

    private IEnumerator toggleSwitch(int i)
    {
        var switchFrom = (_switchState & (1 << i)) != 0 ? -_switchAngle : _switchAngle;
        var switchTo = (_switchState & (1 << i)) != 0 ? _switchAngle : -_switchAngle;
        var startTime = Time.fixedTime;
        const float duration = .3f;

        do
        {
            Switches[i].transform.localEulerAngles = new Vector3(easeOutSine(Time.fixedTime - startTime, duration, switchFrom, switchTo), 0, 0);
            yield return null;
        }
        while (Time.fixedTime < startTime + duration);
        Switches[i].transform.localEulerAngles = new Vector3(switchTo, 0, 0);
        _coroutines[i] = null;
    }

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var pieces = command.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length < 2 || pieces[0] != "toggle" || pieces.Skip(1).Any(p => { int val; return !int.TryParse(p.Trim(), out val) || val < 1 || val > 5; }))
            yield break;

        yield return null;

        foreach (var p in pieces.Skip(1))
        {
            var which = 5 - int.Parse(p.Trim());
            while (_coroutines[which] != null)
                yield return new WaitForSeconds(.1f);
            Switches[which].OnInteract();
            yield return new WaitForSeconds(.25f);
        }
    }
}
