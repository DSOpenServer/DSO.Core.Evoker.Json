using DSO.Core.Evoker;
using DSO.Core.Evoker.Json;
using DSO.Core.Evoker.Extend;
using System.Text.Json;

namespace DSO.Core.Evoker.Json.TestApi
{
    public interface ITestCustomer
    {
        int Id { get; set; }
        string Name { get; set; }
    }

    public class JsonTests
    {
        public static void RunAll()
        {

            int failures = 0;
            void Check(string name, bool condition, string detail = "")
            {
                if (condition) Console.WriteLine($"  [OK]   {name}");
                else { Console.WriteLine($"  [FAIL] {name}  {detail}"); failures++; }
            }

            Console.WriteLine("=== TEST J1: Temel round-trip - ToJson() -> FromJson() ===");
            {
                var customer = DynamicClass.CreateClass("JsonCustomer")
                    .AddProperty<int>("Id")
                    .AddProperty<string>("Name")
                    .AddProperty<decimal>("Balance");

                customer.SetValue<int>("Id", 42);
                customer.SetValue<string>("Name", "Dokuz Sistem");
                customer.SetValue<decimal>("Balance", 1234.56m);

                string json = customer.ToJson();
                Console.WriteLine($"  JSON: {json}");

                var restored = DynamicClassJsonExtensions.FromJson(customer.Type, json);

                Check("Id doğru geri geldi", restored.GetValue<int>("Id") == 42, $"got={restored.GetValue<int>("Id")}");
                Check("Name doğru geri geldi", restored.GetValue<string>("Name") == "Dokuz Sistem", $"got={restored.GetValue<string>("Name")}");
                Check("Balance doğru geri geldi", restored.GetValue<decimal>("Balance") == 1234.56m, $"got={restored.GetValue<decimal>("Balance")}");
            }

            Console.WriteLine("=== TEST J2: JSON alan adı eşleşmesi - AddProperty adlarıyla birebir ===");
            {
                var dc = DynamicClass.CreateClass("JsonFieldNameTest").AddProperty<int>("Id").AddProperty<string>("Name");
                dc.SetValue<int>("Id", 1);
                dc.SetValue<string>("Name", "test");

                string json = dc.ToJson();
                Check("JSON'da 'Id' alan adı var (property adıyla birebir, varsayılan naming policy yok)", json.Contains("\"Id\""), $"json={json}");
                Check("JSON'da 'Name' alan adı var", json.Contains("\"Name\""), $"json={json}");
            }

            Console.WriteLine("=== TEST J3: camelCase naming policy ===");
            {
                var dc = DynamicClass.CreateClass("CamelCaseTest").AddProperty<int>("UserId");
                dc.SetValue<int>("UserId", 7);

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                string json = dc.ToJson(options);

                Check("camelCase policy ile 'userId' üretildi", json.Contains("\"userId\""), $"json={json}");

                var restored = DynamicClassJsonExtensions.FromJson(dc.Type, json, options);
                Check("camelCase JSON'dan geri okuma doğru", restored.GetValue<int>("UserId") == 7, $"got={restored.GetValue<int>("UserId")}");
            }

            Console.WriteLine("=== TEST J4: Bilinmeyen JSON alanı sessizce atlanmalı (hata vermemeli) ===");
            {
                var dc = DynamicClass.CreateClass("UnknownFieldTest").AddProperty<int>("Id");
                string jsonWithExtra = "{\"Id\":5,\"BilinmeyenAlan\":\"deger\",\"BaskaBir\":123}";

                DynamicClass? restored = null;
                bool threw = false;
                try { restored = DynamicClassJsonExtensions.FromJson(dc.Type, jsonWithExtra); }
                catch { threw = true; }

                Check("Bilinmeyen alanlar içeren JSON hata vermeden parse edildi", !threw);
                Check("Bilinen alan (Id) doğru okundu", restored != null && restored.GetValue<int>("Id") == 5, $"got={restored?.GetValue<int>("Id")}");
            }

            Console.WriteLine("=== TEST J5: İÇ İÇE DynamicClass property'si - iç içe (nested) JSON OTOMATİK çalışıyor mu ===");
            {
                var address = DynamicClass.CreateClass("JsonAddress").AddProperty<string>("City").AddProperty<string>("Country");
                var person = DynamicClass.CreateClass("JsonPerson")
                    .AddProperty<string>("Name")
                    .AddProperty("Address", address.Type); // property tipi başka bir dynamic Type

                person.SetValue<string>("Name", "Ayşe");

                var addressInstance = DynamicEntityAccessor.GetConstructor(address.Type)();
                DynamicEntityAccessor.GetSetter<string>(address.Type, "City")(addressInstance, "Ankara");
                DynamicEntityAccessor.GetSetter<string>(address.Type, "Country")(addressInstance, "Türkiye");
                person.SetValue("Address", addressInstance);

                string json = person.ToJson();
                Console.WriteLine($"  JSON: {json}");

                Check("İç içe nesne JSON'da gerçekten iç içe (nested object) göründü", json.Contains("\"Address\":{"), $"json={json}");

                var restoredPerson = DynamicClassJsonExtensions.FromJson(person.Type, json);
                Check("Person.Name geri geldi", restoredPerson.GetValue<string>("Name") == "Ayşe", $"got={restoredPerson.GetValue<string>("Name")}");

                object restoredAddress = restoredPerson.GetValue("Address")!;
                var cityGetter = DynamicEntityAccessor.GetGetter<string>(address.Type, "City");
                Check("İç içe Address.City geri geldi", cityGetter(restoredAddress) == "Ankara", $"got={cityGetter(restoredAddress)}");
            }

            Console.WriteLine("=== TEST J6: Implement<T>() ile üretilmiş bir tip de JSON'a serialize edilebiliyor mu ===");
            {
                var dc = DynamicClass.CreateClass("JsonInterfaceTest").Implement<ITestCustomer>();
                dc.SetValue<int>("Id", 99);
                dc.SetValue<string>("Name", "Interface Testi");

                string json = dc.ToJson();
                Console.WriteLine($"  JSON: {json}");

                var restored = DynamicClassJsonExtensions.FromJson(dc.Type, json);
                Check("Implement<T> ile üretilen tip de doğru serialize/deserialize oldu",
                    restored.GetValue<int>("Id") == 99 && restored.GetValue<string>("Name") == "Interface Testi",
                    $"Id={restored.GetValue<int>("Id")}, Name={restored.GetValue<string>("Name")}");
            }

            Console.WriteLine("=== TEST J7: DynamicTypeFactory.IsDynamicType/CanConvert normal (dinamik olmayan) bir tip için false ===");
            {
                var factory = new DynamicClassJsonConverterFactory();
                Check("string için CanConvert false", !factory.CanConvert(typeof(string)));
                Check("int için CanConvert false", !factory.CanConvert(typeof(int)));

                // Normal (non-dynamic) bir tipin varsayılan System.Text.Json davranışına HİÇ karışmadığını doğrula.
                var options = new JsonSerializerOptions();
                options.Converters.Add(factory);
                string normalJson = JsonSerializer.Serialize(new { Name = "test", Age = 5 }, options);
                Check("Normal (anonim) tip serialize'ı bizim converter'dan etkilenmedi", normalJson.Contains("\"Name\":\"test\""), $"json={normalJson}");
            }

            Console.WriteLine("=== TEST J8: FromJsonUsingSchemaOf - şema şablonu üzerinden ===");
            {
                var template = DynamicClass.CreateClass("SchemaTemplateTest").AddProperty<int>("X").AddProperty<int>("Y");
                string json = "{\"X\":3,\"Y\":4}";

                var restored = template.FromJsonUsingSchemaOf(json);
                Check("Şablon üzerinden FromJson doğru çalıştı", restored.GetValue<int>("X") == 3 && restored.GetValue<int>("Y") == 4,
                    $"X={restored.GetValue<int>("X")}, Y={restored.GetValue<int>("Y")}");
                Check("Template'in kendisi DEĞİŞMEDİ (ayrı bir instance)", !ReferenceEquals(template.RawInstance, restored.RawInstance));
            }

            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "TÜM JSON TESTLERİ GEÇTİ ✅" : $"{failures} TEST BAŞARISIZ ❌");

        }
    }
}


