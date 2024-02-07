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
   

    public ObjectResult Serialize(object obj)
    {
        _buffer.Value ??= new ArrayBufferWriter<byte>();
        _buffer.Value.Clear();
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
            return new InvocationResult(InvocationResultType.Fault,  _objectSerializer.Serialize(ex.GetFaultData()));

        }
        catch (Exception ex)
        {
            // Should be an option;
            return new InvocationResult(InvocationResultType.Exception, Exception: ex.Serialize());
        }
    }
    
}

static class ExceptionExtensions
{
    public static Exception DeserializeAsException(this byte[] data)
    {
        using (var memoryStream = new MemoryStream(data))
        {
#pragma warning disable SYSLIB0011
            var formatter = new BinaryFormatter();
#pragma warning restore SYSLIB0011
            var obj = formatter.Deserialize(memoryStream);
            return (Exception)obj;
        }
    }
    public static byte[] Serialize(this Exception ex)
    {
        using (var memoryStream = new MemoryStream())
        {
#pragma warning disable SYSLIB0011
            var formatter = new BinaryFormatter();
#pragma warning restore SYSLIB0011
            formatter.Serialize(memoryStream, ex);
            return memoryStream.ToArray();
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
    Void, Object, Fault, Exception
}
readonly record struct InvocationResult(InvocationResultType Type, ObjectResult? Result=null,
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
            return new InvocationResult(InvocationResultType.Object, _objectSerializer.Serialize(result));
        }
        catch (FaultException ex)
        {
            return new InvocationResult(InvocationResultType.Fault, _objectSerializer.Serialize(ex.GetFaultData()));

        }
        catch (Exception ex)
        {
            // Should be an option;
            return new InvocationResult(InvocationResultType.Exception, Exception: ex.Serialize());
        }
    }
}