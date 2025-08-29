namespace Bot.CallbackQueries;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CallbackAttribute : Attribute
{
    public string Id { get; }
    public CallbackAttribute(string id) => Id = id;
}
