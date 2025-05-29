using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using Google.Protobuf.Reflection;
using ProtoBuf;
using ProtoBuf.Meta;
using static Google.Protobuf.Reflection.FieldDescriptorProto.Type;

namespace PowerShellGrpcTool;

[Cmdlet(VerbsData.ConvertTo, "Protobuf")]
public class ConvertToProtobuf : ConvertProtobufBase
{
    [Parameter(Mandatory = true, Position = 2, ValueFromPipeline = true)]
    public required IDictionary Message { get; init; }

    protected override void ProcessRecord()
    {
        if (messageTypeDescriptor is null)
        {
            WriteError(
                new(
                    new("Message type descriptor is not initialized"),
                    string.Empty,
                    ErrorCategory.InvalidOperation,
                    this
                )
            );
            return;
        }

        var serializedRequest = ConvertToMessage(messageTypeDescriptor, Message);
        using var stream = new MemoryStream();
        TypeModel.Serialize(stream, serializedRequest);

        WriteObject(stream.ToArray(), enumerateCollection: false);
    }

    private Message ConvertToMessage(DescriptorProto messageTypeDescriptor, IDictionary message)
    {
        var serializedMessage = new Message();
        foreach (var field in messageTypeDescriptor.Fields)
        {
            object? value;
            if (!message.Contains(field.Name) || (value = message[field.Name]) is null)
            {
                if (!field.Proto3Optional)
                {
                    WriteWarning($"Field '{field.Name}' is not present in the input, but it is required.");
                }
                continue;
            }

            switch (field.type)
            {
                case TypeBool:
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        Convert.ToBoolean(value)
                    );
                    break;
                case TypeDouble:
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        Convert.ToDouble(value)
                    );
                    break;
                case TypeFloat:
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        Convert.ToSingle(value)
                    );
                    break;
                case TypeInt32:
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        Convert.ToInt32(value)
                    );
                    break;
                case TypeInt64:
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        Convert.ToInt64(value)
                    );
                    break;
                case TypeString:
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        Convert.ToString(value)
                    );
                    break;
                case TypeUint32:
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        Convert.ToUInt32(value)
                    );
                    break;
                case TypeUint64:
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        Convert.ToUInt64(value)
                    );
                    break;
                case TypeMessage:
                    if (value is not IDictionary subMessage)
                    {
                        throw new ArgumentException(
                            $"Field '{field.Name}' is of type '{field.type}', but the value is not a dictionary"
                        );
                    }
                    if (!types.TryGetValue(field.TypeName, out var subMessageDescriptor))
                    {
                        throw new InvalidOperationException(
                            $"No nested message type named '{field.TypeName}' found for field '{field.Name}'"
                        );
                    }
                    Extensible.AppendValue(
                        TypeModel,
                        serializedMessage,
                        field.Number,
                        DataFormat.Default,
                        ConvertToMessage(subMessageDescriptor, subMessage)
                    );
                    break;
                default:
                    WriteError(
                        new(
                            new($"Unsupported field type '{field.type}' for field '{field.Name}'"),
                            string.Empty,
                            ErrorCategory.InvalidArgument,
                            this
                        )
                    );
                    break;
            }
        }
        return serializedMessage;
    }
}
