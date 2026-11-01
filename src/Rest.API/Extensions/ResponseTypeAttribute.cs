namespace Rest.API.Extensions;

/// <summary>
/// Documents the payload type produced by an endpoint. Used above controllers and
/// actions: <c>[ResponseType&lt;GetStudentResponse&gt;]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ResponseTypeAttribute<TResponse> : Attribute
{
    public Type PayloadType => typeof(TResponse);
}
