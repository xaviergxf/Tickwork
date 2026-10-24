using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Tickwork.Utilities
{
    public static class TickerHelper
    {
        private static readonly byte[] GZipSignature = [0x1f, 0x8b, 0x08, 0x00];
        
        /// <summary>
        /// JsonSerializerOptions specifically for ticker request serialization/deserialization.
        /// Can be configured during application startup via TickerOptionsBuilder.
        /// </summary>
        public static JsonSerializerOptions RequestJsonSerializerOptions { get; set; } = new();
        
        /// <summary>
        /// Controls whether ticker requests are GZip-compressed.
        /// When false (default), requests are stored as plain UTF-8 JSON bytes without compression.
        /// </summary>
        public static bool UseGZipCompression { get; set; } = false;

        // Reflection-based JSON is the fallback when RequestJsonSerializerOptions has no resolver for T.
        // A trimmed or native AOT application configures RequestJsonSerializerOptions.TypeInfoResolver with a
        // JsonSerializerContext that covers its request types, or calls the JsonTypeInfo<T> overload.
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Falls back to reflection only when no resolver supplies T; see the comment above.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Falls back to reflection only when no resolver supplies T; see the comment above.")]
        public static byte[] CreateTickerRequest<T>(T data)
        {
            // If data is already a byte array, short-circuit where possible
            if (data is byte[] existingBytes)
            {
                // If compression is enabled and data already has the GZip signature, assume it is in the final format
                if (UseGZipCompression &&
                    existingBytes.Length >= GZipSignature.Length &&
                    existingBytes.TakeLast(GZipSignature.Length).SequenceEqual(GZipSignature))
                {
                    return existingBytes;
                }

                // If compression is disabled, treat the provided bytes as the final representation
                if (!UseGZipCompression)
                {
                    return existingBytes;
                }
            }

            var serialized = data is byte[] bytes
                ? bytes
                : RequestJsonSerializerOptions?.TypeInfoResolver?.GetTypeInfo(typeof(T), RequestJsonSerializerOptions) is JsonTypeInfo<T> typeInfo
                    ? JsonSerializer.SerializeToUtf8Bytes(data, typeInfo)
                    : JsonSerializer.SerializeToUtf8Bytes(data, RequestJsonSerializerOptions);

            if (!UseGZipCompression)
            {
                return serialized;
            }

            Span<byte> compressedBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var stream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    stream.Write(serialized);
                }

                compressedBytes = memoryStream.GetBuffer().AsSpan()[..(int)memoryStream.Length];
            }

            var returnVal = new byte[compressedBytes.Length + GZipSignature.Length];
            var returnValSpan = returnVal.AsSpan();
            compressedBytes.CopyTo(returnValSpan);
            GZipSignature.AsSpan().CopyTo(returnValSpan[compressedBytes.Length..]);

            return returnVal;
        }

        // Reflection-based JSON is the fallback when RequestJsonSerializerOptions has no resolver for T.
        // A trimmed or native AOT application configures RequestJsonSerializerOptions.TypeInfoResolver with a
        // JsonSerializerContext that covers its request types, or calls the JsonTypeInfo<T> overload.
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Falls back to reflection only when no resolver supplies T; see the comment above.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Falls back to reflection only when no resolver supplies T; see the comment above.")]
        public static T ReadTickerRequest<T>(byte[] gzipBytes)
        {
            if (gzipBytes == null || gzipBytes.Length == 0)
                return default;

            var serializedObject = ReadTickerRequestAsString(gzipBytes);

            if (string.IsNullOrWhiteSpace(serializedObject))
                return default;

            if (RequestJsonSerializerOptions?.TypeInfoResolver?.GetTypeInfo(typeof(T), RequestJsonSerializerOptions) is JsonTypeInfo<T> typeInfo)
                return JsonSerializer.Deserialize(serializedObject, typeInfo);

            return JsonSerializer.Deserialize<T>(serializedObject, RequestJsonSerializerOptions);
        }

        public static T ReadTickerRequest<T>(byte[] gzipBytes, JsonTypeInfo<T> typeInfo)
        {
            if (gzipBytes == null || gzipBytes.Length == 0)
                return default;

            var serializedObject = ReadTickerRequestAsString(gzipBytes);

            if (string.IsNullOrWhiteSpace(serializedObject))
                return default;

            return JsonSerializer.Deserialize(serializedObject, typeInfo);
        }

        public static byte[] CreateTickerRequest<T>(T data, JsonTypeInfo<T> typeInfo)
        {
            var serialized = JsonSerializer.SerializeToUtf8Bytes(data, typeInfo);

            if (!UseGZipCompression)
                return serialized;

            Span<byte> compressedBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var stream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    stream.Write(serialized);
                }

                compressedBytes = memoryStream.GetBuffer().AsSpan()[..(int)memoryStream.Length];
            }

            var returnVal = new byte[compressedBytes.Length + GZipSignature.Length];
            var returnValSpan = returnVal.AsSpan();
            compressedBytes.CopyTo(returnValSpan);
            GZipSignature.AsSpan().CopyTo(returnValSpan[compressedBytes.Length..]);

            return returnVal;
        }
        
        public static string ReadTickerRequestAsString(byte[] gzipBytes)
        {
            // The bytes say whether they are compressed, not the current setting: a persisted row
            // can outlive a change to UseGZipCompression in either direction.
            var isCompressed = gzipBytes.Length >= GZipSignature.Length &&
                               gzipBytes.AsSpan()[^GZipSignature.Length..].SequenceEqual(GZipSignature);
            if (!isCompressed)
                return Encoding.UTF8.GetString(gzipBytes);

            var compressedBytes = gzipBytes.Take(gzipBytes.Length - GZipSignature.Length).ToArray();

            using var memoryStream = new MemoryStream(compressedBytes);
            
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);

            using var streamReader = new StreamReader(gzipStream);

            var serializedObject = streamReader.ReadToEnd();

            return serializedObject;
        }
    }
}
