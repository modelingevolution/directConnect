using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using ProtoBuf;
using ProtoBuf.Meta;

namespace ModelingEvolution.DirectConnect.Tests
{
    [ProtoContract]
    public class FooVoidRequest
    {
        [ProtoMember(1)] public string Name { get; set; } = "Test";
    }

    [ProtoContract]
    public class FooRequest
    {
        [ProtoMember(1)] public string Name { get; set; } = "Test";
    }
    [ProtoContract]
    public class FooResponse
    {
        [ProtoMember(1)] public string Name { get; set; }
    }
}