using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using Google.Protobuf.Reflection;

namespace PowerShellGrpcTool;

public class MethodNameArgumentCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters
    )
    {
        if (TryGetProtoBufArgument(fakeBoundParameters) is not string protoBuf)
        {
            return [];
        }

        var protoBufParser = new ProtoBufParser(TryGetProtoBufRootArgument(fakeBoundParameters) ?? ".", protoBuf);

        if (protoBufParser.Errors.Count > 0)
        {
            return [];
        }

        var services = protoBufParser.Services.Select(s => s.service);
        if (TryGetServiceArgument(fakeBoundParameters) is string service)
        {
            services = services.Where(s => s.Name.Equals(service, StringComparison.OrdinalIgnoreCase));
        }

        return services
            .SelectMany(s => s.Methods)
            .Where(m => m.Name.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            .Select(m => new CompletionResult(m.Name));
    }

    private static string? TryGetProtoBufRootArgument(IDictionary fakeBoundParameters) =>
        TryGetStringArgument(fakeBoundParameters, nameof(InvokeGrpcMethod.ProtoBufRoot));

    private static string? TryGetProtoBufArgument(IDictionary fakeBoundParameters) =>
        TryGetStringArgument(fakeBoundParameters, nameof(InvokeGrpcMethod.ProtoBuf));

    private static string? TryGetServiceArgument(IDictionary fakeBoundParameters) =>
        TryGetStringArgument(fakeBoundParameters, nameof(InvokeGrpcMethod.Service));

    private static string? TryGetStringArgument(IDictionary fakeBoundParameters, string parameterName) =>
        fakeBoundParameters.Contains(parameterName) ? fakeBoundParameters[parameterName] as string : null;
}
