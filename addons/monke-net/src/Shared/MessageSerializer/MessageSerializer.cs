using Godot;
using MonkeNet.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MonkeNet.Serializer;

/// <summary>
/// Defines methods to pack/unpack fields into byte array
/// </summary>
public interface IPackableMessage
{
    public void WriteBytes(MessageWriter writer);
    public void ReadBytes(MessageReader reader);
}

/// <summary>
/// Workaround interface to pack/unpack IPackableMessage into other IPackableMessage (as arrays, lists, etc)
/// </summary>
public interface IPackableElement : IPackableMessage
{
    public IPackableElement GetCopy();
}

public class MessageSerializer
{
    private static readonly Dictionary<IPackableMessage, byte> Types = [];

    /// <summary>
    /// Takes a IPackableMessage <paramref name="message"/> and packs it into a byte array as <paramref name="messageType"/>.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static byte[] Serialize(IPackableMessage message)
    {
        using var stream = new MemoryStream();
        using var writer = new MessageWriter(stream);

        writer.Write(GetByteTypeFromMessage(message));
        message.WriteBytes(writer);
        return stream.ToArray();
    }

    /// <summary>
    /// Reads from a byte array <paramref name="bin"/> and produces an IPackableMessage.
    /// </summary>
    /// <param name="bin"></param>
    /// <returns></returns>
    public static IPackableMessage Deserialize(byte[] bin)
    {
        using var stream = new MemoryStream(bin);
        using var reader = new MessageReader(stream);

        byte typeByte = reader.ReadByte();

        // Get instance of the message and "fill it"
        IPackableMessage instance = GetMessageFromByteType(typeByte);
        instance.ReadBytes(reader);

        // Return the struct, essentialy creating a copy of it (in c# structs are passed by value)
        return instance;
    }

    //TODO: this should bo some type of hash map, not this foreach shit
    public static IPackableMessage GetMessageFromByteType(byte type)
    {
        foreach (var t in Types)
        {
            if (t.Value == type) return t.Key;
        }

        throw new MonkeNetException($"Couldn't find type {type}");
    }

    //TODO: this should bo some type of hash map, not this foreach shit
    public static byte GetByteTypeFromMessage(IPackableMessage message)
    {
        foreach (var t in Types)
        {
            if (t.Key.GetType() == message.GetType()) return t.Value;
        }

        throw new MonkeNetException($"Couldn't find message {message}");
    }

    // Scans the assembly and registers all Messages for the MessageSerializer
    public static void RegisterNetworkMessages()
    {
        Type[] registeredMessages = GetTypesImplementingInterface(typeof(IPackableMessage));
        byte key = 0;

        foreach (Type t in registeredMessages)
        {
            var messageInstance = Activator.CreateInstance(t) as IPackableMessage;
            Types.Add(messageInstance, key++);
            GD.Print($"Registered network message {t.FullName}");
        }
    }

    private static Type[] GetTypesImplementingInterface(Type type)
    {
        return Assembly.GetExecutingAssembly()
                       .GetTypes()
                       .Where(t => type.IsAssignableFrom(t) && !t.IsAbstract)
                       .ToArray();
    }
}