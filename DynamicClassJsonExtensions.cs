using System;
using System.Linq;
using System.Text.Json;
using DSO.Core.Evoker;

namespace DSO.Core.Evoker.Json
{
    /// <summary>
    /// DynamicClass için basit ToJson()/FromJson() kolaylıkları. Alttan DynamicClassJsonConverterFactory
    /// kullanır - options vermezseniz, bu converter'ın önceden eklendiği varsayılan bir
    /// JsonSerializerOptions kullanılır; kendi options'ınızı verirseniz, converter yoksa
    /// otomatik (idempotent) olarak eklenir.
    /// </summary>
    public static class DynamicClassJsonExtensions
    {
        private static readonly JsonSerializerOptions DefaultOptions = CreateDefaultOptions();

        private static JsonSerializerOptions CreateDefaultOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new DynamicClassJsonConverterFactory());
            return options;
        }

        private static JsonSerializerOptions EnsureConverter(JsonSerializerOptions? options)
        {
            if (options == null) return DefaultOptions;

            if (!options.Converters.Any(c => c is DynamicClassJsonConverterFactory))
            {
                options.Converters.Add(new DynamicClassJsonConverterFactory());
            }

            return options;
        }

        /// <summary>dc'nin GÜNCEL değerlerini JSON string'e serialize eder.</summary>
        public static string ToJson(this DynamicClass dc, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Serialize(dc.RawInstance, dc.Type, EnsureConverter(options));
        }

        /// <summary>
        /// Belirtilen dynamic Type'a göre JSON'dan YENİ bir DynamicClass üretir (Wrap ile).
        /// Tip, önceden DynamicTypeFactory/DynamicClass ile üretilmiş olmalıdır (ör. daha önce
        /// AddProperty ile tanımladığınız bir şema).
        /// </summary>
        public static DynamicClass FromJson(Type dynamicType, string json, JsonSerializerOptions? options = null)
        {
            object instance = JsonSerializer.Deserialize(json, dynamicType, EnsureConverter(options))
                ?? throw new JsonException("[DSO.Core.Evoker.Json] JSON null bir nesneye deserialize oldu.");

            return DynamicClass.Wrap(dynamicType, instance);
        }

        /// <summary>
        /// Var olan bir DynamicClass'ın ŞEMASINI (Type'ını) şablon olarak kullanarak JSON'dan
        /// YENİ bir DynamicClass üretir. schemaTemplate'in KENDİSİ değiştirilmez.
        /// </summary>
        public static DynamicClass FromJsonUsingSchemaOf(this DynamicClass schemaTemplate, string json, JsonSerializerOptions? options = null)
        {
            return FromJson(schemaTemplate.Type, json, options);
        }
    }
}