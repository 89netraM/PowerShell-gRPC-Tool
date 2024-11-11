using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Google.Protobuf.Reflection;
using Grpc.Net.Client;
using ProtoBuf;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Meta;
using ProtoBuf.Reflection;
using ProtoBuf.Serializers;

namespace PowerShellGrpcTool;

[Cmdlet(VerbsLifecycle.Invoke, "GrpcMethod")]
public class InvokeGrpcMethod : PSCmdlet
{
    [Parameter(Mandatory = true)]
    public required FileInfo ProtoBuf { get; init; }

    [Parameter(Mandatory = false)]
    public DirectoryInfo ProtoBufRoot { get; init; } = new(".");

    [Parameter(Mandatory = true)]
    public required Uri Endpoint { get; init; }

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

        var service = protoBufParser.Services.SingleOrDefault(s =>
            s.Name.Equals(Service, StringComparison.OrdinalIgnoreCase)
        );
        if (service is null)
        {
            WriteError(
                new(new($"No service named \"{Service}\" found"), string.Empty, ErrorCategory.InvalidArgument, this)
            );
            return;
        }

        var method = service.Methods.SingleOrDefault(m => m.Name.Equals(Method, StringComparison.OrdinalIgnoreCase));
        if (method is null)
        {
            WriteError(
                new(new($"No method named \"{Method}\" found"), string.Empty, ErrorCategory.InvalidArgument, this)
            );
            return;
        }

        using var channel = GrpcChannel.ForAddress(Endpoint);
        var client = new GrpcClient(
            channel,
            "greet.Greeter",
            BinderConfiguration.Create(
                [
                    new IDictionaryMarshallerFactory(
                        protoBufParser.Types[method.InputType],
                        protoBufParser.Types[method.OutputType]
                    ),
                ]
            )
        );
        var request = new Hashtable { ["name"] = "Mårten" };
        var result = client.BlockingUnary<IDictionary, IDictionary>(request, method.Name);
        WriteObject(result, enumerateCollection: true);
    }
}

class IDictionaryMarshallerFactory(DescriptorProto inputDescriptorProto, DescriptorProto outputDescriptorProto)
    : MarshallerFactory
{
    protected override byte[] Serialize<T>(T value)
    {
        if (value is not IDictionary dictionary)
        {
            throw new ArgumentException(message: $"Can only serialize {nameof(IDictionary)}", paramName: nameof(value));
        }

        using var ms = new MemoryStream();
        var state = ProtoWriter.State.Create(ms, new IDictionaryTypeModel(inputDescriptorProto));
        state.GetSerializer<IDictionary>().Write(ref state, dictionary);
        state.Flush();
        state.Dispose();
        return ms.ToArray();
    }

    protected override T Deserialize<T>(byte[] payload)
    {
        if (typeof(T) != typeof(IDictionary))
        {
            throw new ArgumentException(message: $"Can only deserialize {nameof(IDictionary)}");
        }

        var state = ProtoReader.State.Create(payload, new IDictionaryTypeModel(outputDescriptorProto));
        var result = state.GetSerializer<IDictionary>().Read(ref state, new Hashtable());
        state.Dispose();
        return (T)result;
    }

    protected override bool CanSerialize(Type type) => type == typeof(IDictionary);
}

class IDictionaryTypeModel(DescriptorProto descriptorProto) : TypeModel
{
    protected override ISerializer<T> GetSerializer<T>() =>
        typeof(T) == typeof(IDictionary)
            ? (ISerializer<T>)new IDictionarySerializer(descriptorProto)
            : base.GetSerializer<T>();
}

class IDictionarySerializer(DescriptorProto descriptorProto) : ISerializer<IDictionary>
{
    public SerializerFeatures Features => SerializerFeatures.WireTypeVarint;

    public void Write(ref ProtoWriter.State state, IDictionary dictionary)
    {
        foreach (var field in descriptorProto.Fields)
        {
            var value = dictionary[field.Name];
            state.WriteFieldHeader(field.Number, WireType.String);
            switch (field.type)
            {
                case FieldDescriptorProto.Type.TypeDouble:
                {
                    if (value is double d)
                        state.WriteDouble(d);
                    else
                        throw new Exception($"ProtoBuf said double, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case FieldDescriptorProto.Type.TypeFloat:
                {
                    if (value is float f)
                        state.WriteSingle(f);
                    else
                        throw new Exception($"ProtoBuf said float, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case FieldDescriptorProto.Type.TypeInt64:
                {
                    if (value is long l)
                        state.WriteInt64(l);
                    else
                        throw new Exception($"ProtoBuf said long, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case FieldDescriptorProto.Type.TypeUint64:
                {
                    if (value is ulong u)
                        state.WriteUInt64(u);
                    else
                        throw new Exception($"ProtoBuf said ulong, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case FieldDescriptorProto.Type.TypeInt32:
                {
                    if (value is int i)
                        state.WriteInt32(i);
                    else
                        throw new Exception($"ProtoBuf said int, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case FieldDescriptorProto.Type.TypeUint32:
                {
                    if (value is uint i)
                        state.WriteUInt32(i);
                    else
                        throw new Exception($"ProtoBuf said uint, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case FieldDescriptorProto.Type.TypeBool:
                {
                    if (value is bool b)
                        state.WriteBoolean(b);
                    else
                        throw new Exception($"ProtoBuf said bool, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case FieldDescriptorProto.Type.TypeString:
                {
                    if (value is string s)
                        state.WriteString(s);
                    else
                        throw new Exception($"ProtoBuf said string, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case FieldDescriptorProto.Type.TypeBytes:
                {
                    if (value is byte[] bytes)
                        state.WriteBytes(bytes);
                    else
                        throw new Exception(
                            $"ProtoBuf said byte array, but value was {value?.GetType().Name ?? "null"}"
                        );
                    break;
                }
                case FieldDescriptorProto.Type.TypeMessage:
                {
                    if (value is IDictionarySerializer innerDictionary)
                        state.WriteMessage(Features, innerDictionary);
                    else
                        throw new Exception($"ProtoBuf said object, but value was {value?.GetType().Name ?? "null"}");
                    break;
                }
                case var type:
                    throw new Exception($"Unsupported ProtoBuf type {Enum.GetName(type)}");
            }
        }
    }

    public IDictionary Read(ref ProtoReader.State state, IDictionary dictionary)
    {
        while (state.ReadFieldHeader() is int filedNumber and > 0)
        {
            if (descriptorProto.Fields.FirstOrDefault(f => f.Number == filedNumber) is not FieldDescriptorProto field)
            {
                continue;
            }

            switch (field.type)
            {
                case FieldDescriptorProto.Type.TypeString:
                {
                    dictionary[field.Name] = (string?)state.ReadString();
                    break;
                }
                case FieldDescriptorProto.Type.TypeMessage:
                {
                    var ogDescriptor = descriptorProto;
                    descriptorProto = field.GetMessageType();
                    dictionary[field.Name] = Read(ref state, new Hashtable());
                    descriptorProto = ogDescriptor;
                    break;
                }
                case var type:
                    throw new Exception($"Unsupported ProtoBuf type {Enum.GetName(type)}");
            }
        }

        return dictionary;
    }
}
