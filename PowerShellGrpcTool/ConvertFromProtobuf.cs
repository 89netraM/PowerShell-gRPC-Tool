using System;
using System.IO;
using System.Management.Automation;
using Google.Protobuf.Reflection;
using ProtoBuf;
using ProtoBuf.Meta;
using static Google.Protobuf.Reflection.FieldDescriptorProto.Type;

namespace PowerShellGrpcTool;

[Cmdlet(VerbsData.ConvertFrom, "Protobuf")]
public class ConvertFromProtobuf : ConvertProtobufBase
{
    [Parameter(Mandatory = true, Position = 2, ValueFromPipeline = true)]
    public required byte[] Message { get; init; }

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

        using var stream = new MemoryStream(Message);
        var serializedMessage = TypeModel.Deserialize<Message>(stream);

        WriteObject(ConvertFromMessage(messageTypeDescriptor, serializedMessage), enumerateCollection: false);
    }

    private PSObject ConvertFromMessage(DescriptorProto messageTypeDescriptor, Message serializedMessage)
    {
        var message = new PSObject();
        foreach (var field in messageTypeDescriptor.Fields)
        {
            switch (field.type)
            {
                case TypeBool:
                    message.Members.Add(
                        new PSNoteProperty(field.Name, Extensible.GetValue<bool>(serializedMessage, field.Number))
                    );
                    break;
                case TypeDouble:
                    message.Members.Add(
                        new PSNoteProperty(field.Name, Extensible.GetValue<double>(serializedMessage, field.Number))
                    );
                    break;
                case TypeFloat:
                    message.Members.Add(
                        new PSNoteProperty(field.Name, Extensible.GetValue<float>(serializedMessage, field.Number))
                    );
                    break;
                case TypeInt32:
                    message.Members.Add(
                        new PSNoteProperty(field.Name, Extensible.GetValue<int>(serializedMessage, field.Number))
                    );
                    break;
                case TypeInt64:
                    message.Members.Add(
                        new PSNoteProperty(field.Name, Extensible.GetValue<long>(serializedMessage, field.Number))
                    );
                    break;
                case TypeString:
                    message.Members.Add(
                        new PSNoteProperty(field.Name, Extensible.GetValue<string>(serializedMessage, field.Number))
                    );
                    break;
                case TypeUint32:
                    message.Members.Add(
                        new PSNoteProperty(field.Name, Extensible.GetValue<uint>(serializedMessage, field.Number))
                    );
                    break;
                case TypeUint64:
                    message.Members.Add(
                        new PSNoteProperty(field.Name, Extensible.GetValue<ulong>(serializedMessage, field.Number))
                    );
                    break;
                case TypeMessage:
                    if (!types.TryGetValue(field.TypeName, out var subMessageDescriptor))
                    {
                        throw new InvalidOperationException(
                            $"No nested message type named '{field.TypeName}' found for field '{field.Name}'"
                        );
                    }
                    var serializedSubMessage = Extensible.GetValue<Message>(TypeModel, serializedMessage, field.Number);
                    message.Members.Add(
                        new PSNoteProperty(field.Name, ConvertFromMessage(subMessageDescriptor, serializedSubMessage))
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
        return message;
    }
}
