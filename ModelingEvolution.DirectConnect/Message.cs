using ProtoBuf;

namespace ModelingEvolution.DirectConnect;

[ProtoContract]
public class Message
{
        
    [ProtoMember(1)]
    public Guid TypeId { get; set; }
    [ProtoMember(2)]
    public byte[] Data { get; set; }
}