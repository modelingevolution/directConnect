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
    public class FooFailed
    {
        [ProtoMember(1)] public string Name { get; set; } = "Error";
    }

    [ProtoContract]
    public class FooRequest
    {
        [ProtoMember(1)] public string Name { get; set; } = "Test";
    }
    [ProtoContract]
    public class FooResponse : IFooResponse
    {
        [ProtoMember(1)] public string Name { get; set; }
    }

    public interface IFooResponse
    {
        public string Name { get; }
    }
}