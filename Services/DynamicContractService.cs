using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Qubic.ContractGen.Generation;
using Qubic.ContractGen.Parsing;
using Qubic.Core;
using Qubic.Core.Entities;

namespace Qubic.Toolkit;

public class ParseResult
{
    public ContractDefinition? Definition { get; set; }
    public string? Error { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public class DynamicContractService
{
    private static readonly HttpClient Http = new();
    private readonly CSharpEmitter _emitter = new();

    public ParseResult ParseSource(string cppSource, int contractIndex)
    {
        // Auto-detect contract struct name from "struct XXXX : public ContractBase"
        var structNameMatch = Regex.Match(cppSource, @"struct\s+(\w+)\s*:\s*public\s+\w*ContractBase");
        if (!structNameMatch.Success)
            return new ParseResult { Error = "Could not find a struct inheriting ContractBase." };

        var cppStructName = structNameMatch.Groups[1].Value;
        var csharpName = cppStructName;

        var parser = new CppHeaderParser();
        try
        {
            var def = parser.ParseText(cppSource, contractIndex, csharpName, cppStructName);
            return new ParseResult
            {
                Definition = def,
                Warnings = parser.Warnings
            };
        }
        catch (Exception ex)
        {
            return new ParseResult { Error = ex.Message, Warnings = parser.Warnings };
        }
    }

    public byte[] SerializeInput(StructDef structDef, Dictionary<string, object?> values)
    {
        var offsets = _emitter.ComputeFieldOffsets(structDef, out var totalSize, out _);
        var bytes = new byte[totalSize];

        for (int fi = 0; fi < structDef.Fields.Count; fi++)
        {
            var field = structDef.Fields[fi];
            var offset = offsets[fi];

            if (!values.TryGetValue(field.Name, out var value) || value == null)
                continue;

            WriteFieldValue(bytes, offset, field, value);
        }

        return bytes;
    }

    public Dictionary<string, object?> DeserializeOutput(StructDef structDef, byte[] data)
    {
        var result = new Dictionary<string, object?>();
        var offsets = _emitter.ComputeFieldOffsets(structDef, out _, out _);

        for (int fi = 0; fi < structDef.Fields.Count; fi++)
        {
            var field = structDef.Fields[fi];
            if (fi >= offsets.Count) break;
            var offset = offsets[fi];
            if (offset >= data.Length) break;

            result[field.Name] = ReadFieldValue(data, offset, field);
        }

        return result;
    }

    public int GetStructSize(StructDef structDef)
    {
        _emitter.ComputeFieldOffsets(structDef, out var totalSize, out _);
        return totalSize;
    }

    public async Task<string> FetchSourceFromUrl(string url)
    {
        url = ConvertGitHubUrl(url.Trim());
        var response = await Http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static object? ConvertFormValue(string cppType, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (cppType is "id" or "m256i") return new byte[32];
            return GetDefaultValue(cppType);
        }

        var str = rawValue.Trim();

        if (cppType is "id" or "m256i")
        {
            if (str.Length == 60 && str.All(c => c is >= 'A' and <= 'Z'))
            {
                try { return QubicIdentity.FromIdentity(str).PublicKey; }
                catch { }
            }
            if (str.Length % 2 == 0 && str.All(c => "0123456789abcdefABCDEF".Contains(c)))
            {
                var bytes = Convert.FromHexString(str);
                if (bytes.Length >= 32) return bytes[..32];
                var padded = new byte[32];
                bytes.CopyTo(padded, 0);
                return padded;
            }
            return new byte[32];
        }

        if (cppType is "bit" or "bool")
            return bool.TryParse(str, out var b) && b;

        if (cppType == "uint64")
        {
            if (ulong.TryParse(str, out var v)) return v;
            if (str.Length <= 7 && str.All(c => c is >= ' ' and <= '~'))
                return AssetNameHelper.ToUlong(str);
            return 0UL;
        }
        if (cppType == "sint64") return long.TryParse(str, out var v) ? v : 0L;
        if (cppType == "uint32") return uint.TryParse(str, out var v) ? v : 0U;
        if (cppType == "sint32") return int.TryParse(str, out var v) ? v : 0;
        if (cppType == "uint16") return ushort.TryParse(str, out var v) ? v : (ushort)0;
        if (cppType == "sint16") return short.TryParse(str, out var v) ? v : (short)0;
        if (cppType == "uint8") return byte.TryParse(str, out var v) ? v : (byte)0;
        if (cppType == "sint8") return sbyte.TryParse(str, out var v) ? v : (sbyte)0;

        return null;
    }

    public static string FormatOutputValue(string fieldName, string cppType, object? value)
    {
        if (value == null) return "(null)";

        if (value is byte[] bytes)
        {
            if (bytes.Length == 0) return "(empty)";
            if (bytes.Length == 32)
            {
                try { return QubicIdentity.FromPublicKey(bytes).Identity; }
                catch { }
            }
            if (bytes.Length <= 64) return Convert.ToHexString(bytes).ToLowerInvariant();
            return Convert.ToHexString(bytes[..32]).ToLowerInvariant() + $"... ({bytes.Length} bytes)";
        }

        if (value is ulong ul && ul != 0)
        {
            var lower = fieldName.ToLowerInvariant();
            if (lower.Contains("assetname") || lower.Contains("unitofmeasurement"))
            {
                var decoded = AssetNameHelper.FromUlong(ul);
                if (decoded != null) return $"{ul} ({decoded})";
            }
        }

        return value.ToString() ?? "(null)";
    }

    public static string GetTypeLabel(string cppType)
    {
        var info = TypeMapper.GetPrimitiveType(cppType);
        if (info != null) return $"{info.CSharpType} ({cppType})";
        return cppType;
    }

    private static string ConvertGitHubUrl(string url)
    {
        // Convert github.com/user/repo/blob/branch/path → raw.githubusercontent.com/user/repo/branch/path
        var m = Regex.Match(url, @"^https?://github\.com/([^/]+)/([^/]+)/blob/(.+)$");
        if (m.Success)
            return $"https://raw.githubusercontent.com/{m.Groups[1].Value}/{m.Groups[2].Value}/{m.Groups[3].Value}";
        return url;
    }

    private void WriteFieldValue(byte[] bytes, int offset, FieldDef field, object value)
    {
        if (field.IsArray)
        {
            WriteArrayField(bytes, offset, field, value);
            return;
        }

        WritePrimitiveValue(bytes, offset, field.CppType, value);
    }

    private void WriteArrayField(byte[] bytes, int offset, FieldDef field, object value)
    {
        var elemType = field.ArrayElementType ?? field.CppType;
        var elemSize = TypeMapper.GetPrimitiveSize(elemType);
        if (elemSize <= 0) return;

        if (value is not Array arr) return;

        for (int i = 0; i < field.ArrayLength && i < arr.Length; i++)
        {
            var elem = arr.GetValue(i);
            if (elem != null)
                WritePrimitiveValue(bytes, offset + i * elemSize, elemType, elem);
        }
    }

    private static void WritePrimitiveValue(byte[] bytes, int offset, string cppType, object value)
    {
        if (offset < 0 || offset >= bytes.Length) return;

        switch (cppType)
        {
            case "id" or "m256i" when value is byte[] pk && pk.Length >= 32:
                if (offset + 32 <= bytes.Length)
                    pk.AsSpan(0, 32).CopyTo(bytes.AsSpan(offset));
                break;
            case "uint64" when value is ulong u64:
                if (offset + 8 <= bytes.Length)
                    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), u64);
                break;
            case "sint64" when value is long s64:
                if (offset + 8 <= bytes.Length)
                    BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(offset), s64);
                break;
            case "uint32" when value is uint u32:
                if (offset + 4 <= bytes.Length)
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), u32);
                break;
            case "sint32" when value is int s32:
                if (offset + 4 <= bytes.Length)
                    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), s32);
                break;
            case "uint16" when value is ushort u16:
                if (offset + 2 <= bytes.Length)
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), u16);
                break;
            case "sint16" when value is short s16:
                if (offset + 2 <= bytes.Length)
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), s16);
                break;
            case "uint8" when value is byte u8:
                bytes[offset] = u8;
                break;
            case "sint8" when value is sbyte s8:
                bytes[offset] = (byte)s8;
                break;
            case "bit" or "bool" when value is bool b:
                bytes[offset] = (byte)(b ? 1 : 0);
                break;
        }
    }

    private object? ReadFieldValue(byte[] data, int offset, FieldDef field)
    {
        if (field.IsArray)
            return ReadArrayField(data, offset, field);

        return ReadPrimitiveValue(data, offset, field.CppType);
    }

    private object? ReadArrayField(byte[] data, int offset, FieldDef field)
    {
        var elemType = field.ArrayElementType ?? field.CppType;
        var elemSize = TypeMapper.GetPrimitiveSize(elemType);
        if (elemSize <= 0)
        {
            // Unknown element type - return raw bytes
            var totalBytes = field.ArrayLength * Math.Max(elemSize, 1);
            if (offset + totalBytes <= data.Length)
                return data.AsSpan(offset, totalBytes).ToArray();
            return Array.Empty<byte>();
        }

        var results = new object?[field.ArrayLength];
        for (int i = 0; i < field.ArrayLength; i++)
        {
            var elemOffset = offset + i * elemSize;
            if (elemOffset + elemSize > data.Length) break;
            results[i] = ReadPrimitiveValue(data, elemOffset, elemType);
        }
        return results;
    }

    private static object? ReadPrimitiveValue(byte[] data, int offset, string cppType)
    {
        return cppType switch
        {
            "id" or "m256i" when offset + 32 <= data.Length =>
                data.AsSpan(offset, 32).ToArray(),
            "uint64" when offset + 8 <= data.Length =>
                BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset)),
            "sint64" when offset + 8 <= data.Length =>
                BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(offset)),
            "uint32" when offset + 4 <= data.Length =>
                BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset)),
            "sint32" when offset + 4 <= data.Length =>
                BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset)),
            "uint16" when offset + 2 <= data.Length =>
                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset)),
            "sint16" when offset + 2 <= data.Length =>
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset)),
            "uint8" when offset < data.Length =>
                data[offset],
            "sint8" when offset < data.Length =>
                (sbyte)data[offset],
            "bit" or "bool" when offset < data.Length =>
                data[offset] != 0,
            _ => null
        };
    }

    private static object? GetDefaultValue(string cppType)
    {
        return cppType switch
        {
            "id" or "m256i" => new byte[32],
            "uint64" => 0UL,
            "sint64" => 0L,
            "uint32" => 0U,
            "sint32" => 0,
            "uint16" => (ushort)0,
            "sint16" => (short)0,
            "uint8" => (byte)0,
            "sint8" => (sbyte)0,
            "bit" or "bool" => false,
            _ => null
        };
    }
}
