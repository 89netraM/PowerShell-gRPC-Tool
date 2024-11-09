using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Google.Protobuf.Reflection;

namespace PowerShellGrpcTool;

[Cmdlet(VerbsLifecycle.Invoke, "GrpcMethod")]
public class InvokeGrpcMethod : PSCmdlet
{
    [Parameter(Mandatory = true)]
    public required FileInfo ProtoBuf { get; init; }

    [Parameter(Mandatory = false)]
    public DirectoryInfo ProtoBufRoot { get; init; } = new(".");

    protected override void ProcessRecord()
    {
        var protoBufSet = new FileDescriptorSet();
        protoBufSet.AddImportPath(ProtoBufRoot.FullName);
        using (var protoBufFile = ProtoBuf.OpenText())
        {
            protoBufSet.Add(Path.GetRelativePath(ProtoBufRoot.FullName, ProtoBuf.FullName), true, protoBufFile);
        }
        protoBufSet.Process();

        if (protoBufSet.GetErrors() is { Length: > 0 } errors)
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

        var serviceMethods = protoBufSet
            .Files.SelectMany(f => f.Services)
            .SelectMany(s => s.Methods.Select(m => new ServiceMethodDescription(s.Name, m.Name)));
        WriteObject(serviceMethods, enumerateCollection: true);
    }

    private record ServiceMethodDescription(string Service, string Method);
}
