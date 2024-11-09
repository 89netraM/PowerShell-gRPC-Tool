using System;
using System.IO;
using System.Linq;
using System.Management.Automation;

namespace PowerShellGrpcTool;

[Cmdlet(VerbsLifecycle.Invoke, "GrpcMethod")]
public class InvokeGrpcMethod : PSCmdlet
{
    [Parameter(Mandatory = true)]
    public required FileInfo ProtoBuf { get; init; }

    [Parameter(Mandatory = false)]
    public DirectoryInfo ProtoBufRoot { get; init; } = new(".");

    [Parameter(Mandatory = true)]
    [ArgumentCompleter(typeof(ServiceNameArgumentCompleter))]
    public required string Service { get; init; }

    [Parameter(Mandatory = true)]
    [ArgumentCompleter(typeof(MethodNameArgumentCompleter))]
    public required string Method { get; init; }

    protected override void ProcessRecord()
    {
        var protoBufParser = new ProtoBufParser(ProtoBufRoot.FullName, ProtoBuf.FullName);

        if (protoBufParser.Errors is { Count: > 0 } errors)
        {
            foreach (var error in errors)
            {
                WriteError(
                    new(
                        new Exception($"Failed parsing {error.File}:{error.LineNumber}: {error.Message}"),
                        string.Empty,
                        ErrorCategory.InvalidData,
                        this
                    )
                );
            }
            return;
        }

        var serviceMethods = protoBufParser.Services.SelectMany(s =>
            s.Methods.Select(m => new ServiceMethodDescription(s.Name, m.Name))
        );
        WriteObject(serviceMethods, enumerateCollection: true);
    }

    private record ServiceMethodDescription(string Service, string Method);
}
