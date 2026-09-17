using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSO.Core.Evoker;

namespace DSO.Core.Evoker.Json
{
    /// <summary>
    /// System.Text.Json'a "DynamicTypeFactory'nin ürettiği HERHANGİ bir tipi biliyorum, nasıl
    /// serialize/deserialize edeceğimi biliyorum" diyen fabrika. CanConvert, core'un zaten
    /// verdiği DynamicTypeFactory.IsDynamicType(Type) ile çalışır - bu proje core'a HİÇBİR
    /// değişiklik yapmadan, sadece core'un genişletme noktalarını (GetSchema/IsDynamicType)
    /// kullanarak çalışır.
    /// </summary>
    public sealed class DynamicClassJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => DynamicTypeFactory.IsDynamicType(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type converterType = typeof(DynamicClassJsonConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    /// <summary>
    /// Tek bir dynamic Type için gerçek okuma/yazma mantığı. DynamicTypeFactory.GetSchema(T) ile
    /// property (ad, tip) listesini alır; DynamicEntityAccessor.GetGetter/GetSetter&lt;object&gt;
    /// ile (bilinen, önceki turlarda ölçülmüş - sadece kaçınılmaz boxing kadar maliyetli) okuma/
    /// yazma yapar.
    ///
    /// KAPSAM: Sadece AddProperty ile eklenen (DynamicTypeFactory.GetSchema'nın döndürdüğü)
    /// property'ler serialize edilir - AddMethod/AddGenericMethod/AddEvent ile eklenen davranışsal
    /// üyeler JSON'a hiç yansımaz (zaten bir JSON belgesinde "davranış" temsil edilemez, bu
    /// beklenen ve doğru bir sınırlamadır).
    /// </summary>
    internal sealed class DynamicClassJsonConverter<T> : JsonConverter<T> where T : class
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"[DSO.Core.Evoker.Json] '{typeToConvert.Name}' için JSON nesnesi (object) bekleniyordu, {reader.TokenType} geldi.");
            }

            var schema = DynamicTypeFactory.GetSchema(typeToConvert)
                ?? throw new JsonException($"[DSO.Core.Evoker.Json] '{typeToConvert.Name}' için şema bulunamadı (DynamicTypeFactory tarafından üretilmemiş).");

            // JSON alan adı -> (gerçek property adı, CLR tipi). PropertyNamingPolicy (ör. camelCase)
            // ve PropertyNameCaseInsensitive burada uygulanıyor.
            var byJsonName = new Dictionary<string, (string Name, Type Type)>(
                options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            foreach (var (name, type) in schema)
            {
                string jsonName = options.PropertyNamingPolicy?.ConvertName(name) ?? name;
                byJsonName[jsonName] = (name, type);
            }

            object instance = DynamicEntityAccessor.GetConstructor(typeToConvert)();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return (T)instance;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"[DSO.Core.Evoker.Json] Beklenmeyen JSON token: {reader.TokenType}.");
                }

                string jsonPropName = reader.GetString()!;
                reader.Read();

                if (byJsonName.TryGetValue(jsonPropName, out var propInfo))
                {
                    object? value = JsonSerializer.Deserialize(ref reader, propInfo.Type, options);
                    DynamicEntityAccessor.GetSetter<object>(typeToConvert, propInfo.Name)(instance, value!);
                }
                else
                {
                    // Şemada olmayan (bilinmeyen) bir JSON alanı - sessizce atla. Bu, "JSON'da
                    // fazladan alan varsa hata verme" konusunda System.Text.Json'ın varsayılan
                    // davranışıyla tutarlıdır.
                    reader.Skip();
                }
            }

            throw new JsonException($"[DSO.Core.Evoker.Json] '{typeToConvert.Name}' için JSON, kapanış parantezi olmadan bitti.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            Type type = typeof(T);
            var schema = DynamicTypeFactory.GetSchema(type)
                ?? throw new JsonException($"[DSO.Core.Evoker.Json] '{type.Name}' için şema bulunamadı (DynamicTypeFactory tarafından üretilmemiş).");

            writer.WriteStartObject();

            foreach (var (name, propType) in schema)
            {
                string jsonName = options.PropertyNamingPolicy?.ConvertName(name) ?? name;
                writer.WritePropertyName(jsonName);

                object? propValue = DynamicEntityAccessor.GetGetter<object>(type, name)(value);

                // JsonSerializer.Serialize(writer, value, TYPE, options) - propType'ın KENDİSİ
                // de dinamik bir tipse (iç içe DynamicClass property'si), System.Text.Json bu
                // converter fabrikasını TEKRAR devreye sokar - iç içe serialize OTOMATİK çalışır.
                JsonSerializer.Serialize(writer, propValue, propType, options);
            }

            writer.WriteEndObject();
        }
    }
}