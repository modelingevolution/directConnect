namespace ModelingEvolution.DirectConnect;

public struct ImplementedInterface
{
    public Type ArgumentType { get; init; }
    public Type ConcreteInterface { get; init; }
    public Type ImplementationType { get; init; }
    public bool IsImplemented => ArgumentType != null;

}