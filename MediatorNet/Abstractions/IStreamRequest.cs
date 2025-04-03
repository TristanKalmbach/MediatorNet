namespace MediatorNet.Abstractions;

/// <summary>
/// Marker interface to represent a streaming request
/// </summary>
/// <typeparam name="TResponse">Response item type</typeparam>
public interface IStreamRequest<out TResponse> : IBaseRequest;