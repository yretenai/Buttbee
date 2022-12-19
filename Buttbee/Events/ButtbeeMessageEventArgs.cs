using System;
using System.Text.Json;

namespace Buttbee.Events;

public class ButtbeeMessageEventArgs : EventArgs {
    public ButtbeeMessageEventArgs(string @event, JsonElement data) {
        Event = @event;
        Data = data;
    }

    public string Event { get; init; }
    public JsonElement Data { get; init; }
}
