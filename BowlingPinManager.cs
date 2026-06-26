using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BowlingPinManager : MonoBehaviour
{
    [SerializeField] private float despawnHeight = -2.0f;
    [SerializeField] private BowlingPin[] pins;
    [SerializeField] private bool findPinsInChildrenOnStart = true;

    [Header("Screen Display")]
    [SerializeField] private bool showDespawnedCountOnScreen = true;
    [SerializeField] private string despawnedCountLabelFormat = "場外ピン: {0} / {1}";
    [SerializeField] private Rect despawnedCountLabelRect = new Rect(20.0f, 20.0f, 260.0f, 44.0f);
    [SerializeField] private int despawnedCountFontSize = 28;

    [Header("Events")]
    public UnityEvent<int> onDespawnedPinCountChanged = new UnityEvent<int>();

    private readonly List<BowlingPin> registeredPins = new List<BowlingPin>();
    private readonly HashSet<BowlingPin> despawnedPins = new HashSet<BowlingPin>();
    private Coroutine scheduledRespawn;
    private GUIStyle despawnedCountStyle;

    public float DespawnHeight => despawnHeight;
    public int DespawnedPinCount { get; private set; }

    private void Awake()
    {
        if (findPinsInChildrenOnStart && (pins == null || pins.Length == 0))
        {
            pins = GetComponentsInChildren<BowlingPin>(true);
        }

        if (pins == null)
        {
            return;
        }

        foreach (BowlingPin pin in pins)
        {
            RegisterPin(pin);
        }
    }

    public void RegisterPin(BowlingPin pin)
    {
        if (pin == null || registeredPins.Contains(pin))
        {
            return;
        }

        registeredPins.Add(pin);
        pin.SetManager(this);
    }

    public void NotifyPinReachedDespawnHeight(BowlingPin pin)
    {
        if (pin == null || !registeredPins.Contains(pin))
        {
            return;
        }

        if (!despawnedPins.Add(pin))
        {
            return;
        }

        DespawnedPinCount = despawnedPins.Count;
        onDespawnedPinCountChanged.Invoke(DespawnedPinCount);
    }

    public void RespawnAllPins()
    {
        if (scheduledRespawn != null)
        {
            StopCoroutine(scheduledRespawn);
            scheduledRespawn = null;
        }

        despawnedPins.Clear();
        DespawnedPinCount = 0;

        foreach (BowlingPin pin in registeredPins)
        {
            pin.Respawn();
        }

        onDespawnedPinCountChanged.Invoke(DespawnedPinCount);
    }

    public void RespawnAllPinsAfterSeconds(float delaySeconds)
    {
        if (scheduledRespawn != null)
        {
            StopCoroutine(scheduledRespawn);
        }

        scheduledRespawn = StartCoroutine(RespawnAfterDelay(Mathf.Max(0.0f, delaySeconds)));
    }

    private IEnumerator RespawnAfterDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        scheduledRespawn = null;
        RespawnAllPins();
    }

    private void OnGUI()
    {
        if (!showDespawnedCountOnScreen)
        {
            return;
        }

        if (despawnedCountStyle == null || despawnedCountStyle.fontSize != despawnedCountFontSize)
        {
            despawnedCountStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = despawnedCountFontSize,
                fontStyle = FontStyle.Bold
            };
            despawnedCountStyle.normal.textColor = Color.white;
        }

        GUI.Label(
            despawnedCountLabelRect,
            string.Format(despawnedCountLabelFormat, DespawnedPinCount, registeredPins.Count),
            despawnedCountStyle
        );
    }
}
