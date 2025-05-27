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

        var packageService = protoBufParser.Services.SingleOrDefault(s =>
            s.service.Name.Equals(Service, StringComparison.OrdinalIgnoreCase)
        );
        if (packageService is not (string package, ServiceDescriptorProto service))
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
            $"{package}.{service.Name}",
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
        using var state = ProtoWriter.State.Create(ms, new IDictionaryTypeModel(inputDescriptorProto));
        state.SerializeRoot(dictionary);
        return ms.ToArray();
    }

    protected override T Deserialize<T>(byte[] payload)
    {
        if (typeof(T) != typeof(IDictionary))
        {
            throw new ArgumentException(message: $"Can only deserialize {nameof(IDictionary)}", paramName: nameof(T));
        }

        using var state = ProtoReader.State.Create(payload, new IDictionaryTypeModel(outputDescriptorProto));
        var result = state.DeserializeRoot<IDictionary>();
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

class IDictionarySerializer(DescriptorProto descriptorProto) : IRepeatedSerializer<IDictionary>
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
                    if (value is IDictionary innerDictionary)
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

    public IDictionary Read(ref ProtoReader.State state, IDictionary? dictionary)
    {
        dictionary ??= new Hashtable();

        for (int fieldNumber = state.FieldNumber; fieldNumber > 0; fieldNumber = state.ReadFieldHeader())
        {
            if (descriptorProto.Fields.FirstOrDefault(f => f.Number == fieldNumber) is not FieldDescriptorProto field)
            {
                continue;
            }

            switch (field.type)
            {
                case FieldDescriptorProto.Type.TypeDouble:
                {
                    dictionary[field.Name] = state.ReadDouble();
                    break;
                }
                case FieldDescriptorProto.Type.TypeFloat:
                {
                    dictionary[field.Name] = state.ReadSingle();
                    break;
                }
                case FieldDescriptorProto.Type.TypeInt64:
                {
                    dictionary[field.Name] = state.ReadInt64();
                    break;
                }
                case FieldDescriptorProto.Type.TypeUint64:
                {
                    dictionary[field.Name] = state.ReadUInt64();
                    break;
                }
                case FieldDescriptorProto.Type.TypeInt32:
                {
                    dictionary[field.Name] = state.ReadInt32();
                    break;
                }
                case FieldDescriptorProto.Type.TypeUint32:
                {
                    dictionary[field.Name] = state.ReadUInt32();
                    break;
                }
                case FieldDescriptorProto.Type.TypeBool:
                {
                    dictionary[field.Name] = state.ReadBoolean();
                    break;
                }
                case FieldDescriptorProto.Type.TypeString:
                {
                    dictionary[field.Name] = state.ReadString();
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

    public void WriteRepeated(
        ref ProtoWriter.State state,
        int fieldNumber,
        SerializerFeatures features,
        IDictionary dictionary
    ) => Write(ref state, dictionary);

    public IDictionary ReadRepeated(ref ProtoReader.State state, SerializerFeatures features, IDictionary dictionary) =>
        Read(ref state, dictionary);
}
