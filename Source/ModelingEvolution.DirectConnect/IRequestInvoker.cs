using Microsoft.AspNetCore.Authentication.BearerToken;

namespace ModelingEvolution.DirectConnect;

public interface IRequestInvoker
{
    Task InvokeVoid<TRequest>(TRequest request);
    Task<TResponse> Invoke<TRequest,TResponse>(TRequest request);
}