# DirectConnect

GRPC directly with:
```
public interface IRequestHandler<in TRequest> 
{
    Task Handle(TRequest request);
}

public interface IRequestHandler<in TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request);
}

```

- Decorator support
- FaultException\<TMessage\> support
