using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Google.Protobuf.Reflection;
using ProtoBuf;
using ProtoBuf.Meta;

namespace PowerShellGrpcTool;

public abstract class ConvertProtobufBase : Cmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public required FileInfo Protobuf { get; init; }

    [Parameter(Mandatory = false)]
    public DirectoryInfo ProtobufRoot { get; init; } = new(".");

    [Parameter(Mandatory = true, Position = 1)]
    [ArgumentCompleter(typeof(MessageTypeArgumentCompleter))]
    public required string MessageType { get; init; }

    protected IReadOnlyDictionary<string, DescriptorProto> types = new Dictionary<string, DescriptorProto>();
    protected DescriptorProto? messageTypeDescriptor = null;

    protected static readonly TypeModel TypeModel;

    static ConvertProtobufBase()
    {
        var runtimeTypeModel = RuntimeTypeModel.Create();
        runtimeTypeModel.Add(typeof(Message));
        TypeModel = runtimeTypeModel.Compile();
    }

    protected override void BeginProcessing()
    {
        var protoBufParser = new ProtoBufParser(ProtobufRoot.FullName, Protobuf.FullName);

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

        types = protoBufParser.Types;
        if (!types.TryGetValue(MessageType, out messageTypeDescriptor))
        {
            WriteError(
                new(
                    new($"No message type named \"{MessageType}\" found"),
                    string.Empty,
                    ErrorCategory.InvalidArgument,
                    this
                )
            );
            return;
        }
    }
}

public class Message : Extensible;
