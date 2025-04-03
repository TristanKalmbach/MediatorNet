namespace MediatorNet.Abstractions;

/// <summary>
/// Marker interface to represent a request with no response
/// </summary>
public interface IRequest : IBaseRequest;

/// <summary>
/// Marker interface to represent a request with a response
/// </summary>
/// <typeparam name="TResponse">Response type</typeparam>
public interface IRequest<out TResponse> : IBaseRequest;

/// <summary>
/// Base request interface for all requests
/// </summary>
public interface IBaseRequest;