using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;

namespace ModelingEvolution.DirectConnect;

internal class ObjectSerializer
{
   private static readonly AsyncLocal<ArrayBufferWriter<byte>> _buffer= new ();
   private static readonly ConcurrentDictionary<Type, byte[]> _messageId = new();

   public void Init()
   {
       _buffer.Value ??= new ArrayBufferWriter<byte>();
       _buffer.Value.Clear();
    }
    public ObjectResult Serialize(object obj)
    {
        
        Serializer.Serialize(_buffer.Value, obj);
        var messageId = _messageId.GetOrAdd(obj.GetType(), x => x.NameHash());
        return new ObjectResult(messageId, _buffer.Value.WrittenMemory);
    }
}
internal class SingleRequestController
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TypeRegister _typeRegister;
    private readonly ConcurrentDictionary<Type, Type> _adapterTypeByMessageType = new();
    private readonly ObjectSerializer _objectSerializer = new();
    public SingleRequestController(IServiceProvider serviceProvider, TypeRegister typeRegister)
    {
        _serviceProvider = serviceProvider;
        _typeRegister = typeRegister;
    }

    public async Task<InvocationResult> Dispatch(Guid messageId, ReadOnlyMemory<byte> data)
    {
        var messageType = _typeRegister.GetRequiredType(messageId);

        var message = Serializer.NonGeneric.Deserialize(messageType, data);

        var adapterType = _adapterTypeByMessageType.GetOrAdd(messageType, x => typeof(RequestHandlerAdapter<>).MakeGenericType(x));

        var adapter = (IRequestHandlerAdapter)_serviceProvider.GetRequiredService(adapterType);

        try
        {
            await adapter.Handle(message);
            return new InvocationResult(InvocationResultType.Void, null,null);
        }
        catch (FaultException ex)
        {
            _objectSerializer.Init();
            return new InvocationResult(InvocationResultType.Fault,  _objectSerializer.Serialize(ex.GetFaultData()));

        }
        catch (Exception ex)
        {
            // Should be an option;
            throw;
        }
    }
    
}


public abstract class FaultException : Exception
{
    protected internal abstract object GetFaultData();
}

public class FaultException<TData> : FaultException
{
    public TData Data { get; init; }
    protected internal override object GetFaultData() => Data;

    public FaultException(TData data)
    {
        Data = data;
    }
    
}

enum InvocationResultType
{
    Void, Object, Fault, Objects
}
readonly record struct InvocationResult(InvocationResultType Type, ObjectResult? Result = null, ObjectResult[] Results = null,
    ReadOnlyMemory<byte>? Exception=null);

readonly record struct ObjectResult(ReadOnlyMemory<byte> MessageId, ReadOnlyMemory<byte> Payload);
internal class RequestResponseController
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TypeRegister _typeRegister;
    private readonly ObjectSerializer _objectSerializer = new();
    private readonly ConcurrentDictionary<Type, Type> _adapterTypeByMessageType = new();
    private readonly ArrayBufferWriter<byte> _buffer = new();
    public RequestResponseController(IServiceProvider serviceProvider, TypeRegister typeRegister)
    {
        _serviceProvider = serviceProvider;
        _typeRegister = typeRegister;
    }

    public async Task<InvocationResult> Dispatch(Guid messageId, ReadOnlyMemory<byte> data)
    {
        var messageType = _typeRegister.GetRequiredType(messageId);

        var message = Serializer.NonGeneric.Deserialize(messageType, data);

        var adapterType = _adapterTypeByMessageType.GetOrAdd(messageType, x => typeof(IRequestResponseHandlerAdapter<>).MakeGenericType(x));

        var adapter = (IRequestResponseHandlerAdapter)_serviceProvider.GetRequiredService(adapterType);

        try
        {
            var result = await adapter.Handle(message);
            _objectSerializer.Init();
            if (result.GetType().IsArray)
            {
                var array = (Array)result;
                ObjectResult[] results = new ObjectResult[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    results[i] = _objectSerializer.Serialize(array.GetValue(i));
                }

                return new InvocationResult(InvocationResultType.Objects, Results: results);
            }
            else
            return
                new InvocationResult(InvocationResultType.Object, _objectSerializer.Serialize(result));
            
        }
        catch (FaultException ex)
        {
            _objectSerializer.Init();
            return new InvocationResult(InvocationResultType.Fault, _objectSerializer.Serialize(ex.GetFaultData()));

        }
        catch (Exception ex)
        {
            // Should be an option;
            throw;
        }
    }
}