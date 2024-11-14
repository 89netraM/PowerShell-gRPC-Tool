using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using Google.Protobuf.Reflection;

namespace PowerShellGrpcTool;

public class ServiceNameArgumentCompleter : IArgumentCompleter
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
        if (TryGetMethodArgument(fakeBoundParameters) is string method)
        {
            services = services.Where(s =>
                s.Methods.Any(m => m.Name.Equals(method, StringComparison.OrdinalIgnoreCase))
            );
        }

        return services
            .Where(s => s.Name.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            .Select(s => new CompletionResult(s.Name));
    }

    private static string? TryGetProtoBufRootArgument(IDictionary fakeBoundParameters) =>
        TryGetStringArgument(fakeBoundParameters, nameof(InvokeGrpcMethod.ProtoBufRoot));

    private static string? TryGetProtoBufArgument(IDictionary fakeBoundParameters) =>
        TryGetStringArgument(fakeBoundParameters, nameof(InvokeGrpcMethod.ProtoBuf));

    private static string? TryGetMethodArgument(IDictionary fakeBoundParameters) =>
        TryGetStringArgument(fakeBoundParameters, nameof(InvokeGrpcMethod.Method));

    private static string? TryGetStringArgument(IDictionary fakeBoundParameters, string parameterName) =>
        fakeBoundParameters.Contains(parameterName) ? fakeBoundParameters[parameterName] as string : null;
}
