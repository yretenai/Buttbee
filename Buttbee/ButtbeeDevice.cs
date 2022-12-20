using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Attributes;
using Buttbee.Builders;
using Buttbee.Events;
using Buttbee.Messages;

namespace Buttbee;

public class ButtbeeDevice {
    public ButtbeeDevice(ButtplugDeviceAdded device, ButtbeeClient client, IButtbeeLogger? logger) {
        Id = device.DeviceIndex;
        Name = device.DeviceName;
        DisplayName = device.DeviceDisplayName ?? Name;
        Delay = device.DeviceMessageGap;
        Client = client;
        IsConnected = true;
        RawDevice = device;
        for (var index = 0; index < device.DeviceMessages.ScalarCmd.Count; index++) {
            var scalar = device.DeviceMessages.ScalarCmd[index];
            if (!Capabilties.HasFlag(scalar.ActuatorType)) {
                Capabilties |= scalar.ActuatorType;
            }

            ScalarActuators.Add(new ButtbeeScalarActuator(this, (uint) index, scalar));
        }

        for (var index = 0; index < device.DeviceMessages.LinearCmd.Count; index++) {
            var linear = device.DeviceMessages.LinearCmd[index];
            if (!Capabilties.HasFlag(linear.ActuatorType)) {
                Capabilties |= linear.ActuatorType;
            }

            LinearActuators.Add(new ButtbeeLinearActuator(this, (uint) index, linear));
        }

        for (var index = 0; index < device.DeviceMessages.RotateCmd.Count; index++) {
            var rotator = device.DeviceMessages.RotateCmd[index];
            if (!Capabilties.HasFlag(rotator.ActuatorType)) {
                Capabilties |= rotator.ActuatorType;
            }

            RotatorActuators.Add(new ButtbeeRotatorActuator(this, (uint) index, rotator));
        }

        for (var index = 0; index < device.DeviceMessages.SensorReadCmd.Count; index++) {
            var sensor = device.DeviceMessages.SensorReadCmd[index];
            if (!SensorCapabilities.HasFlag(sensor.SensorType)) {
                SensorCapabilities |= sensor.SensorType;
            }

            Sensors.Add(new ButtbeeDeviceSensor(this, (uint) index, sensor));
        }

        Logger = logger?.AddContext<ButtbeeDevice>().AddContext("Device", DisplayName);
    }

    public uint Id { get; }
    public string Name { get; }
    public string DisplayName { get; }
    public uint Delay { get; }
    public DateTimeOffset CanSendNextMessageAt { get; private set; }
    public ButtplugDeviceActuatorType Capabilties { get; }
    public ButtplugDeviceSensorType SensorCapabilities { get; }
    public bool IsConnected { get; internal set; }

    public ButtbeeClient Client { get; }

    public ButtplugDeviceAdded RawDevice { get; }
    protected IButtbeeLogger? Logger { get; }

    public List<ButtbeeScalarActuator> ScalarActuators { get; } = new();
    public List<ButtbeeLinearActuator> LinearActuators { get; } = new();
    public List<ButtbeeRotatorActuator> RotatorActuators { get; } = new();
    public List<ButtbeeDeviceSensor> Sensors { get; } = new();

    public async Task<ButtplugError?> Send<T>(T message, string? name = null, CancellationToken cancellationToken = default) where T : ButtplugDeviceMessage => (await Send<ButtplugOk, T>(message, name, cancellationToken).ConfigureAwait(false)).Error;

    public async Task<(TRx? Message, ButtplugError? Error, ButtbeeMessageEventArgs RawMessage)> Send<TRx, TTx>(TTx message, string? name = null, CancellationToken cancellationToken = default) where TRx : ButtplugMessage where TTx : ButtplugDeviceMessage {
        if (!IsConnected) {
            throw new ButtbeeException("Device is not connected");
        }

        if (CanSendNextMessageAt > DateTimeOffset.Now) {
            await Task.Delay(CanSendNextMessageAt - DateTimeOffset.Now, cancellationToken).ConfigureAwait(false);
        }

        message.DeviceIndex = Id;

        var resp = await Client.Send<TRx, TTx>(message, name).ConfigureAwait(false);
        CanSendNextMessageAt = DateTimeOffset.Now.AddMilliseconds(Delay);
        return resp;
    }

    public async Task<ButtplugError?> SendImmediate<T>(T message, string? name = null) where T : ButtplugDeviceMessage => (await SendImmediate<ButtplugOk, T>(message, name).ConfigureAwait(false)).Error;

    public async Task<(TRx? Message, ButtplugError? Error, ButtbeeMessageEventArgs RawMessage)> SendImmediate<TRx, TTx>(TTx message, string? name = null) where TRx : ButtplugMessage where TTx : ButtplugDeviceMessage {
        if (!IsConnected) {
            throw new ButtbeeException("Device is not connected");
        }

        message.DeviceIndex = Id;

        var resp = await Client.Send<TRx, TTx>(message, name).ConfigureAwait(false);
        CanSendNextMessageAt = DateTimeOffset.Now.AddMilliseconds(Delay);
        return resp;
    }

    public async Task Stop() {
        Logger?.Info("Stopping...");
        var err = await SendImmediate(new ButtplugStopDeviceCmd()).ConfigureAwait(false);

        if (err is not null) {
            throw new ButtbeeException(err);
        }
    }

    public async Task Scalar(ButtplugDeviceActuatorType actuatorMask, double scalar) {
        if (!Capabilties.HasFlag(actuatorMask)) {
            return;
        }

        var cmd = new ButtplugScalarCmd();

        for (var index = 0; index < RawDevice.DeviceMessages.ScalarCmd.Count; index++) {
            var actuator = RawDevice.DeviceMessages.ScalarCmd[index];
            if (actuator.ActuatorType.HasFlag(actuatorMask)) {
                var scalarStepped = 1d / actuator.StepCount;
                var scalarValue = Math.Round(scalar / scalarStepped) * scalarStepped;
                Logger?.AddContext("Actuator", actuator.FeatureDescriptor).Info("Setting {Actuator} to {Scalar}%", actuator.ActuatorType, scalarValue * 100);
                cmd.Scalars.Add(new ButtplugScalar { Index = (uint) index, Scalar = scalarValue, ActuatorType = actuator.ActuatorType });
            }
        }

        var err = await Send(cmd).ConfigureAwait(false);

        if (err is not null) {
            throw new ButtbeeException(err);
        }
    }

    public async Task Linear(uint duration, double position) {
        if (!LinearActuators.Any()) {
            return;
        }

        var cmd = new ButtplugLinearCmd();

        for (var index = 0; index < RawDevice.DeviceMessages.LinearCmd.Count; index++) {
            var actuator = RawDevice.DeviceMessages.LinearCmd[index];
            var positionStepped = 1d / actuator.StepCount;
            var positionValue = Math.Round(position / positionStepped) * positionStepped;
            Logger?.AddContext("Actuator", actuator.FeatureDescriptor).Info("Setting {Actuator} to {Position} over {Duration}ms", actuator.FeatureDescriptor, positionValue, duration);
            cmd.Vectors.Add(new ButtplugVector { Index = (uint) index, Duration = duration, Position = positionValue });
        }

        var err = await Send(cmd).ConfigureAwait(false);

        if (err is not null) {
            throw new ButtbeeException(err);
        }
    }

    public async Task Linear(TimeSpan duration, double position) {
        await Linear((uint) duration.TotalMilliseconds, position).ConfigureAwait(false);
    }

    public async Task Rotate(double speed, bool clockwise) {
        if (!RotatorActuators.Any()) {
            return;
        }

        var cmd = new ButtplugRotateCmd();

        for (var index = 0; index < RawDevice.DeviceMessages.RotateCmd.Count; index++) {
            var actuator = RawDevice.DeviceMessages.RotateCmd[index];
            var speedStepped = 1d / actuator.StepCount;
            var speedValue = Math.Round(speed / speedStepped) * speedStepped;
            Logger?.AddContext("Actuator", actuator.FeatureDescriptor).Info("Setting {Actuator} rotation speed to {Speed} ({Clockwise})", actuator.FeatureDescriptor, speedValue, clockwise ? "clockwise" : "counter-clockwise");
            cmd.Rotations.Add(new ButtplugRotation { Index = (uint) index, Speed = speedValue, Clockwise = clockwise });
        }

        var err = await Send(cmd).ConfigureAwait(false);

        if (err is not null) {
            throw new ButtbeeException(err);
        }
    }

    internal void BroadcastSensorData(ButtplugSensorReading reading) {
        foreach (var sensor in Sensors) {
            if (sensor.Type == reading.SensorType && sensor.Id == reading.SensorIndex) {
                sensor.Update(reading.Data);
            }
        }
    }

    public ButtbeeScalarBuilder WithScalarBuilder() => new(this);

    public ButtbeeLinearBuilder WithLinearBuilder() => new(this);

    public ButtbeeRotatorBuilder WithRotatorBuilder() => new(this);

    // todo: raw message pub/sub
}
