using System;
using System.Linq;
using System.Threading.Tasks;
using Buttbee.Events;
using Buttbee.Messages;
using Serilog;

namespace Buttbee;

public class ButtbeeDevice {
    public ButtbeeDevice(ButtplugDeviceAdded device, ButtbeeClient client) {
        Id = device.DeviceIndex;
        Name = device.DeviceName;
        DisplayName = device.DeviceDisplayName ?? Name;
        Delay = device.DeviceMessageGap;
        Client = client;
        IsConnected = true;
        RawDevice = device;
        foreach (var scalar in device.DeviceMessages.ScalarCmd) {
            if (!Capabilties.HasFlag(scalar.ActuatorType)) {
                Capabilties |= scalar.ActuatorType;
            }
        }

        foreach (var sensor in device.DeviceMessages.SensorReadCmd) {
            if (!Sensors.HasFlag(sensor.SensorType)) {
                Sensors |= sensor.SensorType;
            }
        }

        HasLinearControl = RawDevice.DeviceMessages.LinearCmd.Any();
        HasRotators = RawDevice.DeviceMessages.RotateCmd.Any();

        Logger = Log.ForContext<ButtbeeDevice>().ForContext("Device", DisplayName);
    }

    public uint Id { get; }
    public string Name { get; }
    public string DisplayName { get; }
    public uint Delay { get; }
    public DateTimeOffset CanSendNextMessageAt { get; private set; }
    public ButtplugDeviceActuatorType Capabilties { get; }
    public ButtplugDeviceSensorType Sensors { get; }
    public bool HasRotators { get; }
    public bool HasLinearControl { get; }
    public bool IsConnected { get; internal set; }

    public ButtbeeClient Client { get; }

    // todo: public event EventArgs<ButtplugSensorReading> SensorReadingReceived;
    public ButtplugDeviceAdded RawDevice { get; }
    protected ILogger Logger { get; }

    public async Task<ButtplugError?> Send<T>(T message, string? name = null) where T : ButtplugMessage => (await Send<ButtplugOk, T>(message, name).ConfigureAwait(false)).Error;

    public async Task<(TRx? Message, ButtplugError? Error, ButtbeeMessageEventArgs RawMessage)> Send<TRx, TTx>(TTx message, string? name = null) where TRx : ButtplugMessage where TTx : ButtplugMessage {
        if (!IsConnected) {
            throw new ButtbeeException("Device is not connected");
        }

        if (CanSendNextMessageAt > DateTimeOffset.Now) {
            await Task.Delay(CanSendNextMessageAt - DateTimeOffset.Now).ConfigureAwait(false);
        }

        var resp = await Client.Send<TRx, TTx>(message, name).ConfigureAwait(false);
        CanSendNextMessageAt = DateTimeOffset.Now.AddMilliseconds(Delay);
        return resp;
    }

    public async Task Stop() {
        Logger.Information("Stopping...");
        var err = await Send(new ButtplugStopDeviceCmd { DeviceIndex = Id }).ConfigureAwait(false);

        if (err != null) {
            throw new ButtbeeException(err);
        }
    }

    public async Task Scalar(ButtplugDeviceActuatorType actuatorMask, double scalar) {
        if (!Capabilties.HasFlag(actuatorMask)) {
            return;
        }

        var cmd = new ButtplugScalarCmd { DeviceIndex = Id };

        for (var index = 0; index < RawDevice.DeviceMessages.ScalarCmd.Count; index++) {
            var actuator = RawDevice.DeviceMessages.ScalarCmd[index];
            if (actuator.ActuatorType.HasFlag(actuatorMask)) {
                var scalarStepped = 1d / actuator.StepCount;
                var scalarValue = Math.Round(scalar / scalarStepped) * scalarStepped;
                Logger.Information("Setting {Actuator} to {Scalar}%", actuator.ActuatorType, scalarValue * 100);
                cmd.Scalars.Add(new ButtplugScalar { Index = (uint) index, Scalar = scalarValue, ActuatorType = actuator.ActuatorType });
            }
        }

        var err = await Send(cmd).ConfigureAwait(false);

        if (err != null) {
            throw new ButtbeeException(err);
        }
    }

    public async Task Linear(uint duration, double position) {
        if (!HasLinearControl) {
            return;
        }

        var cmd = new ButtplugLinearCmd { DeviceIndex = Id };

        for (var index = 0; index < RawDevice.DeviceMessages.LinearCmd.Count; index++) {
            Logger.Information("Setting linear position to {Position} over {Duration}ms", position, duration);
            cmd.Vectors.Add(new ButtplugVector { Index = (uint) index, Duration = duration, Position = position });
        }

        var err = await Send(cmd).ConfigureAwait(false);

        if (err != null) {
            throw new ButtbeeException(err);
        }
    }

    public async Task Rotate(double speed, bool clockwise) {
        if (!HasLinearControl) {
            return;
        }

        var cmd = new ButtplugRotateCmd { DeviceIndex = Id };

        for (var index = 0; index < RawDevice.DeviceMessages.LinearCmd.Count; index++) {
            Logger.Information("Setting rotation speed to {Speed} ({Clockwise})", speed, clockwise ? "clockwise" : "counter-clockwise");
            cmd.Rotations.Add(new ButtplugRotation { Index = (uint) index, Speed = speed, Clockwise = clockwise });
        }

        var err = await Send(cmd).ConfigureAwait(false);

        if (err != null) {
            throw new ButtbeeException(err);
        }
    }

    // todo: sensors
    // todo: individual actuator control
}
