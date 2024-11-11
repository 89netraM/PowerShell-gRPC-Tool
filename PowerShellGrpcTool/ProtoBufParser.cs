using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace PowerShellGrpcTool;

public class ProtoBufParser
{
    public FileDescriptorSet FileDescriptorSet { get; } = new FileDescriptorSet();

    private readonly Lazy<Error[]> errors;
    public IReadOnlyList<Error> Errors => errors.Value;

    private readonly Lazy<ServiceDescriptorProto[]> services;
    public IReadOnlyList<ServiceDescriptorProto> Services => services.Value;

    private readonly Lazy<Dictionary<string, DescriptorProto>> types;
    public IReadOnlyDictionary<string, DescriptorProto> Types => types.Value;

    public ProtoBufParser(string protoBufRoot, string protoBufPath)
    {
        FileDescriptorSet.AddImportPath(protoBufRoot);
        using (var protoBufStream = File.OpenText(protoBufPath))
        {
            FileDescriptorSet.Add(
                Path.GetRelativePath(protoBufRoot, protoBufPath),
                includeInOutput: true,
                protoBufStream
            );
        }
        FileDescriptorSet.Process();

        errors = new(FileDescriptorSet.GetErrors);
        services = new(() => FileDescriptorSet.Files.SelectMany(f => f.Services).ToArray());
        types = new(
            () =>
                FileDescriptorSet
                    .Files.SelectMany(f => f.MessageTypes)
                    .ToDictionary(t => t.GetFullyQualifiedName(), t => t)
        );
    }
}
