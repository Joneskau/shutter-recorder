using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Shutter.Core;

public enum RecordingEventType
{
    RecordingStarted,
    RecordingSaved,
    ClipboardCopied,
    RecordingFailed,
    SilenceDetected,
    MicNotFound,
    HotkeyCollision
}

public abstract class RecordingEvent
{
    public RecordingEventType Type { get; }
    public bool IsSuccessPath { get; }

    protected RecordingEvent(RecordingEventType type, bool isSuccessPath)
    {
        Type = type;
        IsSuccessPath = isSuccessPath;
    }
}

public class RecordingStartedEvent : RecordingEvent
{
    public bool IsPushToTalk { get; }
    public RecordingStartedEvent(bool isPushToTalk) : base(RecordingEventType.RecordingStarted, true) { IsPushToTalk = isPushToTalk; }
}

public class RecordingSavedEvent : RecordingEvent
{
    public string FilePath { get; }
    public RecordingSavedEvent(string filePath) : base(RecordingEventType.RecordingSaved, true) { FilePath = filePath; }
}

public class ClipboardCopiedEvent : RecordingEvent
{
    public string Text { get; }
    public ClipboardCopiedEvent(string text) : base(RecordingEventType.ClipboardCopied, true) { Text = text; }
}

public class RecordingFailedEvent : RecordingEvent
{
    public string Reason { get; }
    public RecordingFailedEvent(string reason) : base(RecordingEventType.RecordingFailed, false) { Reason = reason; }
}

public class SilenceDetectedEvent : RecordingEvent
{
    public SilenceDetectedEvent() : base(RecordingEventType.SilenceDetected, false) { }
}

public class MicNotFoundEvent : RecordingEvent
{
    public MicNotFoundEvent() : base(RecordingEventType.MicNotFound, false) { }
}

public class HotkeyCollisionEvent : RecordingEvent
{
    public string Action { get; }
    public HotkeyCollisionEvent(string action) : base(RecordingEventType.HotkeyCollision, false) { Action = action; }
}

public class EventBus
{
    private readonly IStealthConfig _stealthConfig;
    private readonly List<Subscription> _subscriptions = new();

    public EventBus(IStealthConfig stealthConfig)
    {
        _stealthConfig = stealthConfig;
    }

    public void Subscribe<T>(string handlerId, Action<T> handler) where T : RecordingEvent
    {
        _subscriptions.Add(new Subscription
        {
            HandlerId = handlerId,
            EventType = typeof(T),
            Action = e => handler((T)e)
        });
    }

    public void Publish(RecordingEvent ev)
    {
        var suppressOnSuccess = _stealthConfig.SuppressOnSuccess ?? Array.Empty<string>();
        var neverSuppress = _stealthConfig.NeverSuppress ?? Array.Empty<string>();

        var eventName = ev.Type.ToString();
        var camelCaseEventName = char.ToLowerInvariant(eventName[0]) + eventName.Substring(1);

        foreach (var sub in _subscriptions.Where(s => s.EventType.IsAssignableFrom(ev.GetType())))
        {
            bool isSuppressed = suppressOnSuccess.Contains(sub.HandlerId, StringComparer.OrdinalIgnoreCase) ||
                                suppressOnSuccess.Contains(camelCaseEventName, StringComparer.OrdinalIgnoreCase) ||
                                suppressOnSuccess.Contains(eventName, StringComparer.OrdinalIgnoreCase);

            bool isNeverSuppress = neverSuppress.Contains(sub.HandlerId, StringComparer.OrdinalIgnoreCase) ||
                                   neverSuppress.Contains(camelCaseEventName, StringComparer.OrdinalIgnoreCase) ||
                                   neverSuppress.Contains(eventName, StringComparer.OrdinalIgnoreCase);

            if (isSuppressed && isNeverSuppress)
            {
                Debug.WriteLine($"WARNING: Caller attempted to suppress neverSuppress-listed entity (Handler: {sub.HandlerId}, Event: {eventName}). Bypassing suppression.");
            }

            if (ev.IsSuccessPath && isSuppressed && !isNeverSuppress)
            {
                continue;
            }

            sub.Action(ev);
        }
    }

    private class Subscription
    {
        public string HandlerId { get; set; } = string.Empty;
        public Type EventType { get; set; } = typeof(RecordingEvent);
        public Action<RecordingEvent> Action { get; set; } = _ => { };
    }
}
