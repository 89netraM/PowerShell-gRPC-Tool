using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PowerShellGrpcTool;

public class MessageTypeArgumentCompleter : IArgumentCompleter
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

        return protoBufParser
            .Types.Where(s => s.Key.StartsWith(wordToComplete, StringComparison.OrdinalIgnoreCase))
            .Select(s => new CompletionResult(s.Key));
    }

    private static string? TryGetProtoBufRootArgument(IDictionary fakeBoundParameters) =>
        TryGetStringArgument(fakeBoundParameters, nameof(ConvertProtobufBase.ProtobufRoot));

    private static string? TryGetProtoBufArgument(IDictionary fakeBoundParameters) =>
        TryGetStringArgument(fakeBoundParameters, nameof(ConvertProtobufBase.Protobuf));

    private static string? TryGetStringArgument(IDictionary fakeBoundParameters, string parameterName) =>
        fakeBoundParameters.Contains(parameterName) ? fakeBoundParameters[parameterName] as string : null;
}
