using System;

namespace Buttbee;

public interface IButtbeeLogger {
    public void Verbose(string message, params object?[] values);
    public void Info(string message, params object?[] values);
    public void Warn(string message, params object?[] values);
    public void Error(string message, params object?[] values);
    public void Critical(string message, params object?[] values);
    public void Critical(Exception exception, string message, params object?[] values);
    public IButtbeeLogger AddContext(string key, string? value);
    public IButtbeeLogger AddContext<T>();
}
