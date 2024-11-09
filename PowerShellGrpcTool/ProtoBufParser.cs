using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace PowerShellGrpcTool;

public class ProtoBufParser
{
    private readonly Lazy<Error[]> errors;
    public IReadOnlyList<Error> Errors => errors.Value;

    private readonly Lazy<ServiceDescriptorProto[]> services;
    public IReadOnlyList<ServiceDescriptorProto> Services => services.Value;

    public ProtoBufParser(string protoBufRoot, string protoBufPath)
    {
        var fileDescriptorSet = new FileDescriptorSet();
        fileDescriptorSet.AddImportPath(protoBufRoot);
        using (var protoBufStream = File.OpenText(protoBufPath))
        {
            fileDescriptorSet.Add(
                Path.GetRelativePath(protoBufRoot, protoBufPath),
                includeInOutput: true,
                protoBufStream
            );
        }
        fileDescriptorSet.Process();

        errors = new(fileDescriptorSet.GetErrors);
        services = new(() => fileDescriptorSet.Files.SelectMany(f => f.Services).ToArray());
    }
}
