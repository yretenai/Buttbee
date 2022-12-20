using Buttbee.Messages;

namespace Buttbee.Attributes;

public abstract class ButtbeeDeviceAttribute {
    protected ButtbeeDeviceAttribute(ButtbeeDevice device, uint id, ButtplugDeviceAttribute attribute) {
        Id = id;
        Device = device;
        Name = attribute.FeatureDescriptor ?? $"Attribute {id}";
    }

    public uint Id { get; }
    public ButtbeeDevice Device { get; }
    public string Name { get; protected init; }
}
